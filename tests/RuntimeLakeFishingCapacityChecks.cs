// Real Awake -> E/dialogue -> three accepted pulls -> autosave/checksum/Load.
// Offline declarations and deterministic input: NATIVE_UNVERIFIED.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using Starfall;
using UnityEngine;

public static class RuntimeLakeFishingCapacityChecks
{
    const BindingFlags All=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    const string StoryZh="我想给避难的孩子们煮一锅鱼汤，需要三条银鳍鱼。\n\n钓鱼时按 E 或点收线，让浮标停在绿色区，成功三次就能钓到鱼。没有鱼饵消耗，慢慢试。";
    const string StoryEn="Three Silverfin would make soup for the refugee children.\n\nPress E or Reel while the marker is in the green band. Three good pulls catch a fish. No bait cost; take your time.";
    static string folder,prefix;static int captures;
    [Serializable] public sealed class Label { public string k,text;public float[] r;public int size; }
    static void Check(bool ok,string why){if(!ok)throw new Exception(why);}
    static object Call(object target,string name,params object[] args){var m=target.GetType().GetMethod(name,All);Check(m!=null,"Shipped method exists: "+name);return m.Invoke(target,args);}
    static void Set(object target,string name,object value){target.GetType().GetField(name,All).SetValue(target,value);}
    static T Get<T>(object target,string name){return (T)target.GetType().GetField(name,All).GetValue(target);}
    static bool Near(float a,float b){return Math.Abs(a-b)<.002f;}
    static bool Same(Rect a,Rect b){return Near(a.x,b.x)&&Near(a.y,b.y)&&Near(a.width,b.width)&&Near(a.height,b.height);}
    static void Update(FrontierRuntime app,params KeyCode[] keys){foreach(var key in keys)Input.keys.Add(key);Call(app,"Update");Input.Clear();}
    static void Draw(FrontierRuntime app,string capture=null)
    {
        GUI.commands.Clear();GUI.textureCalls.Clear();GUI.buttonCalls.Clear();Call(app,"OnGUI");
        if(folder!=null&&capture!=null){var c=GUI.commands.ToArray();var t=GUI.textureCalls.ToArray();var b=GUI.buttonCalls.ToArray();GUI.Export(folder,prefix+"-"+capture);captures++;GUI.commands.AddRange(c);GUI.textureCalls.AddRange(t);GUI.buttonCalls.AddRange(b);}
    }
    static void Prepare(FrontierRuntime app,int fish=2,int coins=77)
    {
        Input.Clear();GUI.queuedClick=null;GUI.enabled=true;GUI.color=Color.white;GUI.matrix=Matrix4x4.identity;
        Screen.width=1280;Screen.height=800;Time.unscaledDeltaTime=.02f;Event.current.type=EventType.Repaint;
        app.BeginVisualReview(0,0);app.VisualReviewPaused=false;Set(app,"applicationPaused",false);Set(app,"pointerSeen",false);Set(app,"confirmControl",null);Set(app,"pressedControl",null);
        var g=app.Game;g.enemies.Clear();g.projectiles.Clear();g.hazards.Clear();g.effects.Clear();g.fallen.Clear();g.save.companion=-1;
        g.save.english=true;g.save.largeText=false;g.save.reducedMotion=true;g.save.effectsQuality=0;g.save.showGuidance=false;
        g.save.inventory[3]=fish;g.save.coins=coins;g.save.flags.RemoveAll(f=>f=="ally_2");g.save.flags.Add("lake_capacity_mature_story");g.save.bounty=2;g.save.bountyProgress=0;
        Set(app,"saveTimer",-1000f);Set(app,"lastRevision",g.revision);Set(app,"arrivalTime",0f);Set(app,"resumeTime",0f);Set(app,"combatHudTime",0f);Call(app,"Resize");
        var host=Array.Find(g.Map.objects,o=>o.kind=="fish");Check(host!=null,"Authored lake fisher exists");bool found=false;
        for(int y=-7;y<=7&&!found;y++)for(int x=-7;x<=7&&!found;x++){var p=new Point(host.x+x*.2f,host.y+y*.2f);if(g.CanStand(p)&&app.SetVisualReviewPosition(p)&&ReferenceEquals(g.Nearest(),host))found=true;}
        Check(found,"Fixture uses actual legal fisher interaction footing");Set(app,"camera",g.Position);
    }
    static void Pages(FrontierRuntime app)
    {for(int page=0;page<40;page++){Draw(app);if((bool)Call(app,"DialogueChoicesVisible"))return;Update(app,KeyCode.Return);}throw new Exception("Real paginated narrative reaches original choices");}
    static void Open(FrontierRuntime app){Update(app,KeyCode.E);Check(app.Game.mode=="dialog","Real E opens original lake conversation");Pages(app);}
    static int Choice(GameCore g,string action){int i=Array.FindIndex(g.conversation.choices,c=>c.action==action);Check(i>=0,"Mature dialogue retains "+action);return i;}
    static Rect Slot(GameCore g,string action){int i=Choice(g,action),count=g.conversation.choices.Length,cols=count>2?2:count;float w=(832-(cols-1)*14)/(float)Math.Max(1,cols);return new Rect(320+i%cols*(w+14),539+i/cols*45,w,37);}
    static void Button(FrontierRuntime app,string action,bool enabled)
    {Draw(app);Rect r=Slot(app.Game,action);var b=GUI.buttonCalls.Find(c=>Same(c.rect,r));Check(b!=null&&b.enabled==enabled,"Actual original "+action+" button has live capacity readiness "+enabled);}
    static void Number(FrontierRuntime app,string action){int i=Choice(app.Game,action);Update(app,(KeyCode)((int)KeyCode.Alpha1+i));Draw(app);}
    static void Start(FrontierRuntime app){Open(app);Button(app,"fish",true);Number(app,"fish");Check(app.Game.mode=="fish","Original numbered Cast starts lake fishing");}
    static void Good(FrontierRuntime app){app.Game.fishTime=0;Update(app,KeyCode.E);Draw(app);}
    static string State(FrontierRuntime app){var g=app.Game;return JsonUtility.ToJson(g.save)+"|"+g.revision+"|"+g.mode+"|"+g.fishHits+"|"+g.fishMisses+"|"+g.fishTime+"|"+g.toast+"|"+g.toastTime;}
    static void AutoSaveReload(FrontierRuntime app,int fish,int coins)
    {
        var g=app.Game;var original=Get<GameCore>(app,"reviewOriginal");
        try{Set(app,"reviewOriginal",null);Set(app,"saveTimer",13f);Update(app);Check(File.Exists(SaveStore.PathName)&&Near(Get<float>(app,"saveTimer"),0),"Actual production Update autosaves settled catch");}
        finally{Set(app,"reviewOriginal",original);}
        string warning;var saved=SaveStore.Read(out warning);Check(saved!=null&&warning==null,"Actual checksum reader accepts lake autosave");
        Check(saved.inventory[3]==fish&&saved.coins==coins&&saved.bountyProgress==1,"Checksum payload contains exact bounded catch, coin and bounty settlement");
        g.Load(saved);g.Start();g.enemies.Clear();Check(g.save.inventory[3]==fish&&g.save.coins==coins&&g.save.bountyProgress==1&&g.Has("lake_capacity_mature_story")&&!g.Has("ally_2"),"Actual Load retains fish and coins without truncation, mature flags and unrelated commission identity");
        Check(g.mode=="world"&&g.RecentFishingRetry==null,"Load does not duplicate a catch or resurrect session retry");
    }
    static void FullBasketBlocksOriginalInputsAndCommissionFreesRoom(FrontierRuntime app)
    {
        foreach(var key in new[]{KeyCode.Return,KeyCode.JoystickButton0})
        {
            Prepare(app,9999);Open(app);var ready=app.Game;string before=JsonUtility.ToJson(ready.save);
            Update(app,key);Draw(app);
            Check(ready.mode=="world"&&JsonUtility.ToJson(ready.save)==before&&!ready.Has("ally_2"),"Fresh default confirmation of a full basket leaves safely without silently delivering fish");
        }
        foreach(int stock in new[]{9999,-1})
        {
            Prepare(app,stock);Open(app);var g=app.Game;Button(app,"fish",false);var conversation=g.conversation;string before=JsonUtility.ToJson(g.save);int revision=g.revision;
            Number(app,"fish");Check(g.mode=="dialog"&&ReferenceEquals(g.conversation,conversation)&&JsonUtility.ToJson(g.save)==before&&g.revision==revision,"Numbered disabled cast preserves every resource and current conversation");
            Rect r=Slot(g,"fish");GUI.queuedClick=r;Draw(app);GUI.queuedClick=null;Check(g.mode=="dialog"&&ReferenceEquals(g.conversation,conversation)&&JsonUtility.ToJson(g.save)==before,"Mouse disabled cast cannot hide conversation or start fishing");
            string id=(string)Call(app,"ControlId",r.x,r.y,r.width,r.height);var nav=Get<MenuNavigation>(app,"navigation");Check(!nav.CanActivate(id),"Disabled cast cannot be activated by keyboard/pad focus");
            foreach(var key in new[]{KeyCode.Return,KeyCode.JoystickButton0}){nav.focused=id;Update(app,key);Draw(app);Check(g.mode!="fish"&&JsonUtility.ToJson(g.save)==before,"Focused disabled cast cannot enter fishing or charge resources");if(g.mode!="dialog"){Update(app,KeyCode.E);Pages(app);}}
            g.Choose(Choice(g,"fish"));Check(g.mode=="dialog"&&JsonUtility.ToJson(g.save)==before,"Direct stale choice revalidates full or invalid stock before closing");
        }
        Prepare(app,9999);Open(app);var game=app.Game;Button(app,"ally2",true);Number(app,"ally2");Check(game.mode=="world"&&game.Has("ally_2")&&game.save.inventory[3]==9996,"Original three-fish commission remains available and frees exactly three spaces");
        Open(app);Button(app,"fish",true);Number(app,"fish");Check(game.mode=="fish","Original cast becomes available after original commission frees space");
        Console.WriteLine("PASS full/invalid baskets reject original numbered, mouse, focused Enter/A and stale direct Cast without transaction; original three-fish commission frees room and enables fishing; NATIVE_UNVERIFIED");
    }
    static void ExactNearLimitCatchesSurviveAutosaveChecksumAndLoad(FrontierRuntime app)
    {
        foreach(int coins in new[]{999995,999997,999999})
        {
            Prepare(app,9998,coins);Start(app);var g=app.Game;float world=g.save.totalTime;Good(app);Good(app);Check(g.mode=="fish"&&g.fishHits==2,"First two accepted good pulls remain partial, unpaid");Good(app);
            int total=Math.Min(999999,coins+4);Check(g.mode=="world"&&g.fishHits==3&&g.save.inventory[3]==9999&&g.save.coins==total&&g.save.bountyProgress==1,"Third accepted pull awards exactly one fish, only available coin space, and original one bounty progress");
            Check(Near(g.save.totalTime,world),"Original fishing keeps world clock paused");int fish=g.save.inventory[3];g.FishHit();Draw(app);Check(g.save.inventory[3]==fish&&g.save.coins==total&&g.save.bountyProgress==1,"Repeated terminal callback cannot settle twice");
            AutoSaveReload(app,9999,total);
        }
        // Other mature rewards can temporarily leave an already over-limit live
        // balance. Fishing must add zero coins, never confiscate those coins.
        Prepare(app,9998,1000004);Start(app);var overflow=app.Game;Good(app);Good(app);Good(app);
        Check(overflow.mode=="world"&&overflow.save.inventory[3]==9999&&overflow.save.coins==1000004&&overflow.save.bountyProgress==1,"Fishing preserves pre-existing over-limit live coins and adds zero, while retaining its one fish and original bounty");
        Console.WriteLine("PASS actual three-pull catches at fish 9998 and coins 999995/999997/999999 retain exact bounded rewards through real autosave, checksum Read and Load/Start; pre-existing over-limit live coins are preserved without added coins; original bounty and paused clock remain; NATIVE_UNVERIFIED");
    }
    static void StaleDialogueAndTerminalCapacityAreRechecked(FrontierRuntime app)
    {
        Prepare(app,9998,99);Open(app);var g=app.Game;Button(app,"fish",true);g.save.inventory[3]=9999;var c=g.conversation;int revision=g.revision;g.Choose(Choice(g,"fish"));Check(g.mode=="dialog"&&ReferenceEquals(g.conversation,c)&&g.revision==revision&&g.save.inventory[3]==9999&&g.save.coins==99,"Already displayed eligible dialogue rechecks changed stock without closing or paying");
        Prepare(app,9998,99);Start(app);g=app.Game;Good(app);Good(app);g.save.inventory[3]=9999;int rev=g.revision;Good(app);Check(g.save.inventory[3]==9999&&g.save.coins==99&&g.save.bountyProgress==0&&g.revision==rev&&g.RecentFishingRetry==null,"A full basket at the third pull blocks the entire reward, bounty, revision and retry settlement");
        Prepare(app,9998,999995);Start(app);g=app.Game;Good(app);Good(app);g.save.coins=999999;Good(app);Check(g.mode=="world"&&g.save.inventory[3]==9999&&g.save.coins==999999&&g.save.bountyProgress==1,"Coin space is rechecked at actual catch, independently retaining fish and bounty");
        Prepare(app,2,77);Start(app);g=app.Game;for(int i=0;i<3;i++){g.fishTime=.55f;Update(app,KeyCode.E);Draw(app);}Check(g.mode=="world"&&g.fishMisses==3&&g.save.inventory[3]==2&&g.save.coins==77&&g.save.bountyProgress==0,"Original three-miss failure remains cost-free and awards nothing");
        Prepare(app,2,77);Start(app);g=app.Game;Good(app);Update(app,KeyCode.Escape);Draw(app);Check(g.mode=="world"&&g.save.inventory[3]==2&&g.save.coins==77&&g.save.bountyProgress==0,"Original partial-cast Escape cancellation leaves all supplies and bounty intact");
        Console.WriteLine("PASS stale eligible Cast and third-pull full baskets are revalidated before any reward, coin growth is bounded at actual catch, and original three-miss failure/Escape cancellation remain cost-free; NATIVE_UNVERIFIED");
    }
    static void BilingualPaginatedDialogueIsReadableAndReadOnly(FrontierRuntime app)
    {
        foreach(bool english in new[]{false,true})foreach(bool large in new[]{false,true})foreach(bool full in new[]{false,true})
        {
            Prepare(app,full?9999:9998,999997);var g=app.Game;g.save.english=english;g.save.largeText=large;Update(app,KeyCode.E);Check(g.mode=="dialog","Real E opens localized lake dialogue");
            string text=g.L(g.conversation.text,g.conversation.textEn);Check(text.StartsWith(english?StoryEn:StoryZh,StringComparison.Ordinal),"Entire original refugee soup narrative and fishing instructions remain unchanged before appended preview");
            Check(text.Contains((full?"9999":"9998")+"/9999")&&text.Contains("+2"),"Lake dialogue exposes live bounded fish stock and exact current coin gain");
            Check(Choice(g,"fish")==0&&Choice(g,"ally2")==1&&Choice(g,"close")==2&&g.conversation.choices[0].label=="开始钓鱼"&&g.conversation.choices[0].en=="Cast the line","Original cast/commission/goodbye action, labels and numbering remain intact");
            var pages=DialogueLayout.Pages(text,large);var shown=new List<string>();
            for(int page=0;page<pages.Count;page++)
            {
                Draw(app);string before=State(app);string command=GUI.commands.Find(line=>line.StartsWith("{\"k\":\"text\"",StringComparison.Ordinal)&&line.Contains("\"r\":[320,264,832,228]"));Check(command!=null,"Actual OnGUI draws the authored narrative body");var label=JsonUtility.FromJson<Label>(command);Check(label.text==pages[page],"Actual body follows original production pagination without dropping preview/narrative");shown.Add(label.text);
                foreach(var type in new[]{EventType.Layout,EventType.Repaint,EventType.Repaint}){Event.current.type=type;Draw(app);Check(State(app)==before,"Repeated page Layout/Repaint preserves inventory, coins, story, revision and fishing state");}
                if(page<pages.Count-1){Check(!(bool)Call(app,"DialogueChoicesVisible"),"Original choice readiness waits for all narrative pages");Update(app,KeyCode.Return);}
            }
            Check((bool)Call(app,"DialogueChoicesVisible")&&shown.Count==pages.Count,"All pages are visible before original cast/commission actions");Button(app,"fish",!full);Button(app,"ally2",true);
            Draw(app,(english?"en":"zh")+(large?"-large":"-normal")+(full?"-full":"-ready"));
        }
        Console.WriteLine("PASS eight bilingual normal/large full/ready paginated lake dialogues retain original narrative, exact live preview, numbered mature choices, readiness and read-only Layout/Repaint; NATIVE_UNVERIFIED");
    }
    public static int Main(string[] args)
    {
        CultureInfo.CurrentCulture=CultureInfo.InvariantCulture;folder=args.Length>0&&args[0]!="-"?args[0]:null;prefix=args.Length>1?args[1]:"qa-lake-fishing-capacity";
        string previous=Application.persistentDataPath,directory=Path.Combine(Path.GetTempPath(),"starfall-lake-capacity-"+Guid.NewGuid());Application.persistentDataPath=directory;int failed=0;var app=new FrontierRuntime();
        try{Call(app,"Awake");Check(app.Game!=null,"Actual Awake loads shipped campaign");foreach(Action<FrontierRuntime> test in new Action<FrontierRuntime>[]{FullBasketBlocksOriginalInputsAndCommissionFreesRoom,ExactNearLimitCatchesSurviveAutosaveChecksumAndLoad,StaleDialogueAndTerminalCapacityAreRechecked,BilingualPaginatedDialogueIsReadableAndReadOnly})try{test(app);}catch(Exception error){failed++;Console.WriteLine("FAIL "+test.Method.Name+": "+(error.InnerException??error).Message);}app.EndVisualReview();Call(app,"OnDestroy");}
        finally{Application.persistentDataPath=previous;Input.Clear();GUI.queuedClick=null;if(Directory.Exists(directory))Directory.Delete(directory,true);}
        Console.WriteLine("Runtime lake fishing capacity: "+(4-failed)+"/4 groups passed; NATIVE_UNVERIFIED");return failed==0?0:1;
    }
}
