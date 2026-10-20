// Actual runtime Update, dialogue pages, buttons, navigation and SaveStore paths.
// Encounter clearance and imported legacy states are explicitly staged. These
// offline declarations do not establish native Unity rendering or play feel.
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Starfall;
using UnityEngine;

public static class RuntimeAbyssPlanningChecks
{
    private const BindingFlags All = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static string captureFolder;
    private static int captures;
    private static void Check(bool condition,string message) { if(!condition)throw new Exception(message); }
    private static bool Near(float a,float b) { return Math.Abs(a-b)<.006f; }
    private static object Call(object obj,string name,params object[] args)
    { var m=obj.GetType().GetMethod(name,All);Check(m!=null,"Production API exists: "+name);return m.Invoke(obj,args); }
    private static void Set(object obj,string name,object value) { obj.GetType().GetField(name,All).SetValue(obj,value); }
    private static T Get<T>(object obj,string name) { return (T)obj.GetType().GetField(name,All).GetValue(obj); }
    private static bool Enabled(GameCore g,string action) { return (bool)Call(g,"AbyssBoonChoiceEnabled",action); }
    private static string Description(GameCore g,bool english) { return (string)Call(g,"AbyssDecisionText",english); }
    private static void Update(FrontierRuntime app) { Call(app,"Update");Input.keys.Clear(); }
    private static void Key(FrontierRuntime app,KeyCode key) { Input.keys.Add(key);Update(app); }
    private static bool Same(Rect a,Rect b) { return Near(a.x,b.x)&&Near(a.y,b.y)&&Near(a.width,b.width)&&Near(a.height,b.height); }
    private static bool Text(string text) { return GUI.commands.Exists(c=>c.Contains("\"text\":"+GUI.S(text))); }
    private static SaveData Copy(SaveData save) { return JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(save)); }
    private static void Draw(FrontierRuntime app,string capture=null)
    {
        GUI.commands.Clear();GUI.textureCalls.Clear();GUI.buttonCalls.Clear();Call(app,"OnGUI");
        if(captureFolder!=null&&capture!=null)
        {
            var commands=GUI.commands.ToArray();var textures=GUI.textureCalls.ToArray();var buttons=GUI.buttonCalls.ToArray();
            GUI.Export(captureFolder,"qa-abyss-planning-"+capture);captures++;
            GUI.commands.AddRange(commands);GUI.textureCalls.AddRange(textures);GUI.buttonCalls.AddRange(buttons);
        }
    }
    private static void Position(FrontierRuntime app,Point point)
    { Check(app.Game.CanStand(point)&&app.SetVisualReviewPosition(point),"Fixture stages legal authored footing");Set(app,"camera",app.Game.Position); }
    private static void Interact(FrontierRuntime app,string kind,bool pad=false)
    {
        var obj=Array.Find(app.Game.Map.objects,o=>o.kind==kind);Check(obj!=null,"Original authored object remains: "+kind);
        Position(app,new Point(obj.x,obj.y+.5f));Check(ReferenceEquals(app.Game.Nearest(),obj),"Actual E targets authored "+kind);
        Key(app,pad?KeyCode.JoystickButton1:KeyCode.E);
    }
    private static int Choice(GameCore g,string action)
    { Check(g.mode=="dialog"&&g.conversation!=null,"Actual dialogue owns "+action);int index=Array.FindIndex(g.conversation.choices,c=>c.action==action);Check(index>=0&&index<9,"Actual numbered choice exists: "+action);return index; }
    private static Rect ChoiceBounds(GameCore g,int index)
    { int count=g.conversation.choices.Length,cols=count>2?2:count;float width=(832-(cols-1)*14)/Math.Max(1,cols);return new Rect(320+index%cols*(width+14),539+index/cols*45,width,37); }
    private static string ChoiceControl(FrontierRuntime app,int index)
    { Rect rect=ChoiceBounds(app.Game,index);return (string)Call(app,"ControlId",rect.x,rect.y,rect.width,rect.height); }
    private static void Pages(FrontierRuntime app,string capture=null)
    {
        var g=app.Game;Check(g.mode=="dialog"&&g.conversation!=null,"Production dialogue is open");
        List<string> pages=DialogueLayout.Pages(g.L(g.conversation.text,g.conversation.textEn),g.save.largeText);
        string state=JsonUtility.ToJson(g.save);Draw(app);int page=Get<int>(app,"dialoguePage");
        while(true)
        {
            Draw(app,capture==null?null:capture+"-page"+page);Check(Text(pages[page]),"Actual OnGUI displays each complete authoritative page");
            Check(JsonUtility.ToJson(g.save)==state,"Reading actual pages cannot bank, heal, change stacks or advance floor");
            if((bool)Call(app,"DialogueChoicesVisible"))break;
            Key(app,KeyCode.Return);page++;Check(page<pages.Count,"The actual final decision page is reachable");
        }
        Check(page==pages.Count-1,"Numbered choices are exposed only after every actual page");
    }
    private static void Choose(FrontierRuntime app,string action)
    { int index=Choice(app.Game,action);Pages(app);Key(app,(KeyCode)((int)KeyCode.Alpha1+index)); }
    private static void ResetRuntime(FrontierRuntime app)
    {
        Set(app,"applicationPaused",false);Set(app,"pointerSeen",false);Set(app,"controllerUsed",false);Set(app,"resumeTime",0f);
        Set(app,"arrivalTime",0f);Set(app,"combatHudTime",0f);Set(app,"dialoguePage",0);Set(app,"lastConversation",null);
        Input.Clear();GUI.queuedClick=null;GUI.enabled=true;GUI.color=Color.white;GUI.matrix=Matrix4x4.identity;
        Call(app,"Resize");Update(app);Set(app,"arrivalTime",0f);Set(app,"resumeTime",0f);app.Game.toastTime=0;
    }
    private static void Prepare(FrontierRuntime app,int profession=0,int covenant=1)
    {
        Input.Clear();Screen.width=1280;Screen.height=800;Time.unscaledDeltaTime=.02f;Event.current.type=EventType.Repaint;
        app.BeginVisualReview(profession,0);app.VisualReviewPaused=false;var g=app.Game;
        g.Close();g.SkipTutorial();g.save.stage=3;g.save.frontierEnding=covenant;g.save.companion=-1;g.save.showGuidance=false;
        g.save.english=true;g.save.largeText=false;g.save.effectsQuality=0;g.save.reducedMotion=true;
        g.save.flags.RemoveAll(f=>f=="abyss_complete"||f=="relic_5"||f.StartsWith("abyss_clear_")||f.StartsWith("abyss_echo_"));
        g.enemies.Clear();g.fallen.Clear();g.hazards.Clear();g.effects.Clear();g.projectiles.Clear();ResetRuntime(app);
        Interact(app,"abyssEntry");Choose(app,"abyssStart0");
        Check(g.InAbyss&&g.save.abyssFloor==1&&Array.TrueForAll(g.save.boons,b=>b==0),"Actual town E and numbered risk choice begin unchanged six-floor expedition");
    }
    private static void Gate(FrontierRuntime app,bool pad=false)
    {
        var g=app.Game;g.Close();g.enemies.Clear();g.fallen.Clear();g.hazards.Clear();g.projectiles.Clear();g.effects.Clear();
        g.hitStop=0;g.toastTime=0;Set(app,"arrivalTime",0f);Set(app,"resumeTime",0f);
        Interact(app,"abyssGate",pad);Check(g.mode=="dialog"&&g.Has("abyss_clear_"+g.save.abyssFloor),"Real E at original east gate records staged encounter clearance");
    }
    private static void EarnCap(FrontierRuntime app,int boon=0)
    {
        Prepare(app,boon);var g=app.Game;
        for(int floor=1;floor<=4;floor++)
        { Gate(app);Choose(app,"boon"+boon);Check(g.save.abyssFloor==floor+1&&g.save.boons[boon]==floor,"Four stacks come from four real numbered descents, not injected flags"); }
    }
    private static void ActualCapMouseNumberAndNavigation(FrontierRuntime app)
    {
        for(int boon=0;boon<3;boon++)
        {
            EarnCap(app,boon);var g=app.Game;g.save.runCoins=17;g.save.runShards=3;g.save.runXp=0;g.save.health=11;g.save.energy=13;
            Gate(app,boon==1);Pages(app);int index=Choice(g,"boon"+boon);Rect rect=ChoiceBounds(g,index);
            Check(!Enabled(g,"boon"+boon)&&GUI.buttonCalls.Exists(b=>Same(b.rect,rect)&&!b.enabled),"Actual fifth-floor full boon is visibly disabled");
            string before=JsonUtility.ToJson(g.save);Conversation original=g.conversation;
            Key(app,(KeyCode)((int)KeyCode.Alpha1+index));Draw(app);
            Check(g.mode=="dialog"&&ReferenceEquals(g.conversation,original)&&before==JsonUtility.ToJson(g.save),"Actual disabled number preserves open decision, all loot, health, energy and floor");
            GUI.queuedClick=rect;Draw(app);Check(GUI.queuedClick.HasValue&&before==JsonUtility.ToJson(g.save)&&ReferenceEquals(g.conversation,original),"Actual disabled GUI button cannot close, heal or bank");GUI.queuedClick=null;
            var navigation=Get<MenuNavigation>(app,"navigation");int first=Array.FindIndex(g.conversation.choices,c=>Enabled(g,c.action));
            Check(navigation.focused==ChoiceControl(app,first)&&navigation.CanActivate(navigation.focused),"Actual initial focus skips the disabled full boon on the final decision page");
            int target=(boon+1)%3;int steps=0;
            while(navigation.focused!=ChoiceControl(app,target)&&steps++<9) { Key(app,KeyCode.Tab);Draw(app);Check(navigation.focused!=ChoiceControl(app,index),"Actual Tab focus never visits the disabled full boon"); }
            Check(steps<9,"A real alternative is reachable by Tab");int coins=g.save.coins,shards=g.save.shards;
            Key(app,boon==1?KeyCode.JoystickButton0:KeyCode.Return);Draw(app);
            Check(g.mode=="world"&&g.save.abyssFloor==6&&g.save.boons[boon]==4&&g.save.boons[target]==1&&g.save.coins==coins+17&&g.save.shards==shards+3&&g.save.runCoins==0&&g.save.runShards==0,"Real keyboard or pad confirm banks exactly once and descends with a different genuine boon");
            Check(Near(g.save.health,11+g.save.maxHealth*.5f)&&g.save.energy==100&&g.enemies.Exists(e=>e.boss),"Real accepted alternative retains half-maximum heal, energy refill and final Reaper");
        }
        Console.WriteLine("PASS actual town E, four numbered descents for each cap, disabled numeric/mouse transactions, initial/Tab focus and keyboard/pad confirmation to actual floor-six Reaper; NATIVE_UNVERIFIED");
    }
    private static void BankingFirstRepeatAndCovenant(FrontierRuntime app)
    {
        foreach(int covenant in new[]{1,3})
        {
            Prepare(app,0,covenant);var g=app.Game;int firstPoints=g.save.skillPoints;
            for(int expedition=0;expedition<2;expedition++)
            {
                if(expedition>0) { g.enemies.Clear();Interact(app,"abyssEntry");Choose(app,"abyssStart1"); }
                for(int floor=1;floor<=6;floor++)
                {
                    g.save.runCoins=11;g.save.runShards=7;g.save.runXp=0;Gate(app);Pages(app);
                    string text=Description(g,true);int expected=7+(covenant==3?2:0)+(floor==6&&expedition==0?12:0);
                    Check(text.Contains("Bank now: 11 coins, "+expected+" shards, 0 XP."),"Actual bank preview reports exact pending plus covenant and first-clear increment");
                    int coins=g.save.coins,shards=g.save.shards,points=g.save.skillPoints;
                    Choose(app,floor<6?"boon"+((floor-1)%3):"abyssBank");
                    Check(g.save.coins==coins+11&&g.save.shards==shards+expected&&g.save.skillPoints==points+(floor==6&&expedition==0?2:0),"Actual numbered bank transaction matches preview without repeating first-clear milestones");
                    Check(g.save.bestFloor>=floor&&g.Has("relic_5")== (floor>=3||expedition>0),"Original best floor and third-floor permanent relic remain intact");
                }
                Check(!g.InAbyss&&g.save.room==0&&g.save.abyssFloor==0&&g.Has("abyss_complete")&&Array.TrueForAll(g.save.boons,b=>b==0),"Original sixth-floor extraction retains completion and clears only temporary boons");
                Check(g.save.skillPoints==firstPoints+2,"First-completion talent reward is once only across real repeat expedition");
            }
        }
        Console.WriteLine("PASS actual first/repeat six-floor numbered expeditions, exact preview-to-bank increments, signed covenant, permanent relic and once-only first completion; NATIVE_UNVERIFIED");
    }
    private static void BilingualDecisionPagesAndTruth(FrontierRuntime app)
    {
        foreach(string state in new[]{"first","cap","middle","six-first","six-repeat","covenant","legacy-full"})
        foreach(bool english in new[]{false,true})foreach(bool large in new[]{false,true})
        {
            Prepare(app,0,state=="covenant"?3:1);var g=app.Game;
            // These are prose/layout fixtures of earned or imported states.
            // Transactions and earned caps are tested independently above.
            SaveData save=Copy(g.save);save.abyssFloor=state=="first"?1:state=="middle"?3:state=="cap"||state=="legacy-full"?5:6;
            save.abyssEnemies=null;save.abyssSnapshotFloor=0;save.flags.Add("abyss_clear_"+save.abyssFloor);
            save.boons=state=="first"?new[]{0,0,0}:state=="middle"?new[]{1,1,0}:state=="legacy-full"?new[]{4,4,4}:save.abyssFloor==6?new[]{4,1,0}:new[]{4,0,0};
            if(state=="six-repeat")save.flags.Add("abyss_complete");
            save.runCoins=23;save.runShards=6;save.runXp=7;save.english=english;save.largeText=large;
            g.Load(save);g.Start();g.Close();ResetRuntime(app);Gate(app);
            string description=Description(g,english);Check(g.L(g.conversation.text,g.conversation.textEn)==description,"Actual gate uses authoritative localized decision body");
            for(int id=0;id<3;id++)
            {
                string summary=(string)Call(g,"AbyssBoonSummary",id,english);
                Check(description.Contains(summary)&&summary.Contains(g.save.boons[id]+"/4")&&summary.Contains((g.save.boons[id]*(id==0?15:id==1?4:12)).ToString()),"Each actual page reports current stacks and actual additive total, including full legacy builds");
                if(g.save.abyssFloor<6)
                {
                    var choice=g.conversation.choices[id];Check(choice.action=="boon"+id,"Original three numbered boon slots are unchanged");
                    string label=g.L(choice.label,choice.en);
                    Check(g.save.boons[id]==4?label.Contains("4/4")&&!Enabled(g,choice.action):label.Contains(g.save.boons[id]+"→"+(g.save.boons[id]+1)+"/4")&&label.Contains((id==0?15:id==1?4:12).ToString()),"Actual choice labels show one additive gain or an unavailable cap, never an invented fifth gain");
                }
            }
            if(english)
            {
                int bank=6+(state=="covenant"?2:0)+(g.save.abyssFloor==6&&state!="six-repeat"?12:0);
                Check(description.Contains("Bank now: 23 coins, "+bank+" shards, 7 XP."),"Decision pages show exact actual bank amount");
                Check(description.Contains("echo")== (g.save.abyssFloor==1||g.save.abyssFloor==3),"Only actual next floors two/four advertise optional echoes");
                Check(description.Contains("Reaper")== (g.save.abyssFloor==5),"Only actual descent to sixth floor advertises Reaper");
                string next=(string)Call(g,"AbyssNextFloorHint",true);
                Check(next.IndexOf("rift",StringComparison.OrdinalIgnoreCase)>=0== (g.save.abyssFloor>=2&&g.save.abyssFloor<6),"Only actual dangerous next floors advertise rift marks");
                if(g.save.abyssFloor==6)Check(description.Contains("no further descent")&&!description.Contains("heals 50%"),"Final floor truthfully offers return without false descent healing");
            }
            Pages(app,state+"-"+(english?"en":"zh")+(large?"-large":"-normal"));
            foreach(var button in GUI.buttonCalls)Check(button.rect.y+button.rect.height<=800,"Actual decision controls stay within reference viewport");
            Check(g.conversation.choices[g.save.abyssFloor<6?3:0].action=="abyssBank","Original bank slot is preserved in all decision states");
        }
        Console.WriteLine("PASS 28 actual Chinese/English normal/large complete decision page sets, additive totals/increments, true bank values, next-floor echo/rift/Reaper warnings, final return and original bank slots; captures="+captures+"; NATIVE_UNVERIFIED");
    }
    private static SaveData Roundtrip(GameCore g,int slot,bool raw,int version)
    {
        Directory.CreateDirectory(Application.persistentDataPath);g.PrepareSave();SaveData copy=Copy(g.save);copy.version=version;
        if(raw)File.WriteAllText(SaveStore.PathFor(slot),JsonUtility.ToJson(copy));else Check(SaveStore.Write(copy,slot),"Actual SaveStore writes checksummed run");
        string warning;SaveData read=SaveStore.Read(slot,out warning);Check(read!=null&&warning==null,"Actual SaveStore reads "+(raw?"raw v"+version:"checksum envelope"));return read;
    }
    private static void LegacyPartialCappedRetentionAndWithdrawal(FrontierRuntime app)
    {
        foreach(bool raw in new[]{true,false})foreach(int version in new[]{1,2})
        {
            Prepare(app);var g=app.Game;g.enemies.RemoveAt(g.enemies.Count-1);g.enemies[0].hp-=5;int enemies=g.enemies.Count;float hp=g.enemies[0].hp;
            g.save.runCoins=13;g.save.runShards=8;g.save.flags.Add("homeward_complete");int wallet=g.save.coins,shards=g.save.shards,identity=g.save.profession;
            SaveData loaded=Roundtrip(g,2,raw,version);g.Load(Copy(loaded));g.Start();g.Close();ResetRuntime(app);
            Check(g.InAbyss&&g.save.abyssFloor==1&&g.enemies.Count==enemies&&Near(g.enemies[0].hp,hp)&&g.save.runCoins==13&&g.save.runShards==8&&g.Has("homeward_complete")&&g.save.profession==identity,"Partial-floor raw/envelope resume preserves original enemy damage, unbanked loot, mature story and class");
            Interact(app,"abyssExit");Choose(app,"abyssLeave");Check(g.save.room==0&&g.save.coins==wallet&&g.save.shards==shards&&g.save.runCoins==0&&g.save.runShards==0,"Actual west-exit numbered withdrawal discards only current unbanked floor and preserves prior wallet");
            EarnCap(app);g=app.Game;Gate(app);g.Close();g.save.runCoins=23;g.save.runShards=6;loaded=Roundtrip(g,2,raw,version);
            g.Load(Copy(loaded));g.Start();g.Close();ResetRuntime(app);Gate(app);Pages(app);Check(g.save.abyssFloor==5&&g.save.boons[0]==4&&!Enabled(g,"boon0"),"Real four-earned-stack cap remains disabled after raw/envelope resume");
            g.Close();g.save.boons=new[]{4,4,4};loaded=Roundtrip(g,2,raw,version);g.Load(Copy(loaded));g.Start();g.Close();ResetRuntime(app);Gate(app);Pages(app);
            Check(Choice(g,"abyssBank")==3&&Choice(g,"abyssRetain")>3&&Enabled(g,"abyssRetain"),"Legacy all-capped run keeps original extraction slot and appends explicit safe retention");
            wallet=g.save.coins;shards=g.save.shards;Choose(app,"abyssRetain");
            Check(g.save.abyssFloor==6&&Array.TrueForAll(g.save.boons,b=>b==4)&&g.save.coins==wallet+23&&g.save.shards==shards+6,"Actual numbered retain banks once and descends without fabricating another stack");
            Gate(app);Choose(app,"abyssBank");Check(g.save.room==0&&Array.TrueForAll(g.save.boons,b=>b==0),"Imported all-capped build still completes the real final gate and exits");
            g.Load(Copy(loaded));g.Start();g.Close();ResetRuntime(app);Gate(app);Choose(app,"abyssBank");
            Check(g.save.room==0&&g.save.coins==wallet+23&&g.save.shards==shards+6,"Original fourth numbered extraction remains usable immediately on the same all-capped legacy snapshot");
        }
        Console.WriteLine("PASS actual raw v1/v2 and checksum envelope partial-enemy/capped resumes, preserved story/class/economy, live west withdrawal, all-cap retain and original-slot extraction; NATIVE_UNVERIFIED");
    }
    public static int Main(string[] args)
    {
        string previous=Application.persistentDataPath,directory=Path.Combine(Path.GetTempPath(),"starfall-abyss-planning-"+Guid.NewGuid());Application.persistentDataPath=directory;
        captureFolder=args.Length>0?args[0]:null;if(captureFolder!=null)Directory.CreateDirectory(captureFolder);int failures=0;var app=new FrontierRuntime();
        try
        {
            Call(app,"Awake");Check(app.Game!=null,"Actual runtime loads shipped campaign");
            foreach(Action<FrontierRuntime> test in new Action<FrontierRuntime>[]{ActualCapMouseNumberAndNavigation,BankingFirstRepeatAndCovenant,BilingualDecisionPagesAndTruth,LegacyPartialCappedRetentionAndWithdrawal})
                try { test(app); } catch(Exception error) { failures++;Console.WriteLine("FAIL "+test.Method.Name+": "+(error.InnerException??error).Message); }
            app.EndVisualReview();Call(app,"OnDestroy");
        }
        finally { Application.persistentDataPath=previous;Input.Clear();GUI.queuedClick=null;if(Directory.Exists(directory))Directory.Delete(directory,true); }
        Console.WriteLine("Runtime Abyss Planning: "+(4-failures)+"/4 groups passed; captures="+captures+"; NATIVE_UNVERIFIED");return failures==0?0:1;
    }
}
