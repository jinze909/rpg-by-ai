// Shipped Update/E/dialogue/OnGUI/SaveStore evidence under offline Unity declarations.
// This does not validate native Editor import, GPU/font rendering, pad hardware or play feel.
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Starfall;
using UnityEngine;

public static class RuntimeFirstHarvestChecks
{
    const BindingFlags All=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    static string captures,prefix; static int captureCount;
    static void Check(bool value,string text) { if(!value)throw new Exception(text); }
    static bool Near(float a,float b) { return Math.Abs(a-b)<.006f; }
    static bool Same(Rect a,Rect b) { return Near(a.x,b.x)&&Near(a.y,b.y)&&Near(a.width,b.width)&&Near(a.height,b.height); }
    static object Call(object obj,string name,params object[] args) { var method=obj.GetType().GetMethod(name,All);Check(method!=null,"Shipped method exists: "+name);return method.Invoke(obj,args); }
    static void Set(object obj,string name,object value) { var field=obj.GetType().GetField(name,All);Check(field!=null,"Fixture field exists: "+name);field.SetValue(obj,value); }
    static T Get<T>(object obj,string name) { return (T)obj.GetType().GetField(name,All).GetValue(obj); }
    static bool State(GameCore g,string name) { var property=g.GetType().GetProperty("FirstHarvest"+name,All);Check(property!=null,"Authored first harvest exposes stage "+name);return (bool)property.GetValue(g,null); }
    static bool Api(GameCore g,string name) { return (bool)Call(g,name); }
    static void Update(FrontierRuntime app) { Call(app,"Update");Input.keys.Clear(); }
    static void Key(FrontierRuntime app,KeyCode key) { Input.keys.Add(key);Update(app); }
    static bool Text(string value) { return GUI.commands.Exists(c=>c.Contains("\"k\":\"text\"")&&c.Contains(value)); }
    static void Draw(FrontierRuntime app,string name=null)
    {
        GUI.commands.Clear();GUI.textureCalls.Clear();GUI.buttonCalls.Clear();Call(app,"OnGUI");
        if(captures!=null&&name!=null)
        { var commands=GUI.commands.ToArray();var textures=GUI.textureCalls.ToArray();var buttons=GUI.buttonCalls.ToArray();GUI.Export(captures,prefix+"-"+name);GUI.commands.AddRange(commands);GUI.textureCalls.AddRange(textures);GUI.buttonCalls.AddRange(buttons);captureCount++; }
    }
    static void Pages(FrontierRuntime app,string name=null)
    {
        for(int page=0;page<30;page++)
        {
            Draw(app,name==null?null:name+"-page"+page);
            if((bool)Call(app,"DialogueChoicesVisible"))
            {
                int count=app.Game.conversation.choices.Length,columns=count>2?2:Math.Max(1,count),rows=(count+columns-1)/columns;
                float bottom=180+Math.Max(506,539-180+Math.Max(0,rows-1)*45+37+20);
                Check(GUI.textureCalls.Exists(t=>Near(t.rect.x,88)&&Near(t.rect.y,180)&&Near(t.rect.width,1104)&&Near(t.rect.height,bottom-180)),"Actual dialogue background extends around all retained choice rows");
                for(int i=0;i<count;i++)
                { Rect rect=ChoiceRect(i,count);Check(GUI.buttonCalls.Exists(b=>Same(b.rect,rect))&&rect.y+rect.height<=bottom-20&&rect.y+rect.height<=780,"Every actual numbered choice remains visible inside the dialogue card"); }
                return;
            }
            Key(app,KeyCode.Return);
        }
        Check(false,"Actual narrative pagination finishes before selecting a choice");
    }
    static Rect ChoiceRect(int index,int count)
    { int columns=count>2?2:Math.Max(1,count);float width=(832-(columns-1)*14)/(float)columns;return new Rect(320+index%columns*(width+14),539+index/columns*45,width,37); }
    static void Choose(FrontierRuntime app,string action,int input=1,string capture=null)
    {
        Check(app.Game.mode=="dialog"&&app.Game.conversation!=null,"Choice belongs to actual open dialogue: "+action);
        int index=Array.FindIndex(app.Game.conversation.choices,c=>c.action==action),count=app.Game.conversation.choices.Length;
        Check(index>=0&&index<9,"Actual earned NPC dialogue exposes "+action);Pages(app,capture);Rect rect=ChoiceRect(index,count);
        if(input==0) { Check(GUI.buttonCalls.Exists(b=>Same(b.rect,rect)&&b.enabled),"Actual mouse choice is visible and enabled");GUI.queuedClick=rect;Draw(app);Check(!GUI.queuedClick.HasValue,"Production OnGUI consumes the visible choice click"); }
        else if(input==1)Key(app,(KeyCode)((int)KeyCode.Alpha1+index));
        else
        {
            var nav=Get<MenuNavigation>(app,"navigation");string id=(string)Call(app,"ControlId",rect.x,rect.y,rect.width,rect.height);int keys=0;
            while(nav.focused!=id&&keys++<32){Key(app,KeyCode.Tab);Draw(app);}Check(nav.focused==id&&nav.CanActivate(id),"Real focus navigation reaches the enabled quest choice");Key(app,KeyCode.JoystickButton0);Draw(app);
        }
    }
    static void Click(FrontierRuntime app,Rect rect)
    { Draw(app);Check(GUI.buttonCalls.Exists(b=>Same(b.rect,rect)&&b.enabled),"Actual journal/map control is enabled");GUI.queuedClick=rect;Draw(app);Check(!GUI.queuedClick.HasValue,"Actual journal/map control consumes its click"); }
    static WorldObject Object(GameCore g,string kind)
    { var obj=Array.Find(g.Map.objects,o=>o.kind==kind);Check(obj!=null,"Authored canonical object exists: "+kind);return obj; }
    static void Position(FrontierRuntime app,Point point)
    { Check(app.Game.CanStand(point)&&app.SetVisualReviewPosition(point)&&(app.Game.Position-point).Length<.001f,"Fixture uses exact full-footprint walkable ground at "+point.x+","+point.y);Set(app,"camera",app.Game.Position); }
    static void Beside(FrontierRuntime app,string kind)
    {
        var obj=Object(app.Game,kind);bool found=false;
        foreach(Point offset in new[]{new Point(0,-.7f),new Point(0,.7f),new Point(-.7f,0),new Point(.7f,0),new Point(0,-1.2f),new Point(0,1.2f)})
        { Point p=new Point(obj.x,obj.y)+offset;if(!app.Game.CanStand(p))continue;Position(app,p);if(ReferenceEquals(app.Game.Nearest(),obj)){found=true;break;} }
        Check(found,"Actual nearest E target is canonical "+kind);
    }
    static void Interact(FrontierRuntime app,string kind,bool pad=false)
    { Beside(app,kind);Key(app,pad?KeyCode.JoystickButton1:KeyCode.E);Check(app.Game.mode=="dialog","Real E/B opens the authored "+kind+" conversation"); }
    static void Prefix(GameCore g,params string[] actions)
    { Check(g.conversation!=null&&g.conversation.choices.Length>=actions.Length,"Mature choices remain present");for(int i=0;i<actions.Length;i++)Check(g.conversation.choices[i].action==actions[i],"Mature numbered choice retained: "+actions[i]); }
    static void Prepare(FrontierRuntime app,bool ready=true,bool english=true,bool large=false,int width=1280)
    {
        Input.Clear();GUI.queuedClick=null;GUI.enabled=true;GUI.color=Color.white;GUI.matrix=Matrix4x4.identity;Event.current.type=EventType.Repaint;Screen.width=width;Screen.height=width==960?600:800;Time.unscaledDeltaTime=.02f;
        app.BeginVisualReview(0,0);app.VisualReviewPaused=false;var g=app.Game;g.enemies.Clear();g.projectiles.Clear();g.hazards.Clear();g.effects.Clear();g.fallen.Clear();
        g.save.flags.RemoveAll(f=>f.StartsWith("firstharvest_")||f.StartsWith("watergarden_")||f=="scout_saved");
        foreach(string flag in new[]{"ally_1","ally_3","intro"})if(!g.Has(flag))g.save.flags.Add(flag);
        if(ready)foreach(string flag in new[]{"watergarden_accept","watergarden_drawn","watergarden_watered","watergarden_complete","scout_saved"})g.save.flags.Add(flag);
        g.save.english=english;g.save.largeText=large;g.save.showGuidance=true;g.save.reducedMotion=true;g.save.effectsQuality=0;g.save.companion=-1;g.save.plantedAt=-1;g.save.projects[0]=2;g.save.frontierEnding=0;g.save.inventory[7]=6;g.save.inventory[2]=10;g.save.tea=3;g.save.coins=100;g.save.tutorialBits=255;
        Set(app,"applicationPaused",false);Set(app,"controllerUsed",false);Set(app,"pointerSeen",false);Set(app,"resumeTime",0f);Set(app,"arrivalTime",0f);Set(app,"combatHudTime",0f);Set(app,"journalTab",0);Set(app,"selectedQuest",0);Set(app,"selectedRegion",-1);Set(app,"dialogParent",null);Call(app,"Resize");
    }
    static void Menu(FrontierRuntime app,string kind,int input=1,string capture=null)
    { Interact(app,kind);Choose(app,"firstharvest_menu",input);Pages(app,capture); }
    static void Accept(FrontierRuntime app,int input=1,string capture=null)
    { Menu(app,"garden",input,capture);Choose(app,"firstharvest_accept",input);Check(State(app.Game,"Accepted")&&app.Game.mode=="world","Actual An dialogue accepts the earned first harvest"); }
    static void Pack(FrontierRuntime app,int input=1,string capture=null)
    { Menu(app,"cook",input,capture);Choose(app,"firstharvest_pack",input); }
    static void Deliver(FrontierRuntime app,int input=1,string capture=null)
    { Menu(app,"returnscout",input,capture);Choose(app,"firstharvest_deliver",input); }
    static void Report(FrontierRuntime app,int input=1,string capture=null)
    { Menu(app,"garden",input,capture);Choose(app,"firstharvest_finish",input); }
    static void Plant(FrontierRuntime app)
    { Interact(app,"garden");Choose(app,"plant");Check(app.Game.save.plantedAt>=0,"Ordinary planting still owns the crop"); }
    static void Rest(FrontierRuntime app)
    { Interact(app,"rest");Choose(app,"rest"); }
    static void Harvest(FrontierRuntime app)
    { Interact(app,"garden");Choose(app,"harvest"); }
    static void EarnHarvest(FrontierRuntime app)
    { Plant(app);Rest(app);Rest(app);Harvest(app);Check(State(app.Game,"Harvested"),"A real ordinary harvest after acceptance earns the quest stage"); }
    static void Walk(FrontierRuntime app,string kind)
    {
        var g=app.Game;WorldObject obj=Object(g,kind);List<Point> path=g.InteractionPath(obj);Check(path.Count>0,"Guide destination has an actual interaction path");int frames=0;
        while(!ReferenceEquals(g.Nearest(),obj)&&frames++<5000)
        {
            while(path.Count>0&&(path[0]-g.Position).Length<.14f)path.RemoveAt(0);Check(path.Count>0,"A real walking approach remains until E owns the intended target");Point move=(path[0]-g.Position).Normalized;
            Input.axes["Horizontal"]=move.x;Input.axes["Vertical"]=-move.y;Input.held.Clear();if(move.x>.05f)Input.held.Add(KeyCode.D);if(move.x<-.05f)Input.held.Add(KeyCode.A);if(move.y>.05f)Input.held.Add(KeyCode.S);if(move.y<-.05f)Input.held.Add(KeyCode.W);Update(app);Check(g.CanStand(g.Position),"Actual held walking retains full-footprint collision");
        }
        Input.axes.Clear();Input.held.Clear();Check(frames>0&&frames<5000&&ReferenceEquals(g.Nearest(),obj),"Actual Update/WASD route reaches canonical "+kind);Set(app,"camera",g.Position);
    }
    static void Guide(GameCore g,string kind)
    { Point? target=g.NavigationTarget();WorldObject obj=Object(g,kind);Check(target.HasValue&&(target.Value-new Point(obj.x,obj.y)).Length<.001f,"Tracked quest guide owns next canonical "+kind); }

    static void ActualHarvestHandoffJourney(FrontierRuntime app)
    {
        Prepare(app);var g=app.Game;int[] inventory=(int[])g.save.inventory.Clone();int coins=g.save.coins,tea=g.save.tea;Accept(app);Guide(g,"garden");
        Interact(app,"garden");Prefix(g,"plant","seeds","close");Choose(app,"plant");float planted=g.save.plantedAt;Check(g.save.inventory[7]==inventory[7]-1&&!State(g,"Harvested"),"Acceptance and planting do not fabricate a harvest");
        Interact(app,"garden");Choose(app,"harvest");Check(Near(g.save.plantedAt,planted)&&g.save.inventory[2]==inventory[2]&&!State(g,"Harvested"),"Checking an immature crop does not pay or advance the delivery");
        Time.unscaledDeltaTime=.05f;for(int i=0;i<100;i++)Update(app);Time.unscaledDeltaTime=.02f;Walk(app,"rest");Key(app,KeyCode.E);Choose(app,"rest");Rest(app);Walk(app,"garden");Key(app,KeyCode.E);Choose(app,"harvest");
        Check(State(g,"Harvested")&&g.save.plantedAt<0&&g.save.inventory[2]==inventory[2]+9&&g.save.inventory[7]==inventory[7]+1&&g.save.coins==coins+8,"Actual waiting/rest and harvest preserve the original 60s crop, nine herbs, seed refund and eight coins");
        Guide(g,"cook");Walk(app,"cook");Check(g.CanStand(g.Position)&&ReferenceEquals(g.Nearest(),Object(g,"cook"))&&!(bool)Call(g,"ClearAttackPath",g.Position,new Point(Object(g,"cook").x,Object(g,"cook").y)),"Actual house-edge guide arrival gives canonical cook ordinary E ownership even though the projectile ray is blocked");Key(app,KeyCode.E);Choose(app,"firstharvest_menu");Choose(app,"firstharvest_pack");Check(State(g,"Packed")&&g.save.inventory[2]==inventory[2]+7&&g.save.inventory[6]==inventory[6],"Ari spends two harvested herbs for the quest packet without a stew inventory or buff grant");
        Guide(g,"returnscout");Walk(app,"returnscout");Key(app,KeyCode.E);Choose(app,"firstharvest_menu");Choose(app,"firstharvest_deliver");Check(State(g,"Delivered")&&!State(g,"Complete")&&g.save.coins==coins+8,"Actual scout handoff does not pay An's unreported reward");
        Guide(g,"garden");Walk(app,"garden");Key(app,KeyCode.E);Choose(app,"firstharvest_menu");Choose(app,"firstharvest_finish");
        Check(State(g,"Complete")&&g.GetQuest("firstharvest").complete&&g.save.coins==coins+33&&g.save.tea==tea+1,"Actual E/numeric/WASD sequence reports once for exactly 25 coins and one tea");
        for(int i=0;i<inventory.Length;i++)if(i!=2&&i!=7)Check(g.save.inventory[i]==inventory[i],"Original inventory retains unrelated slot "+i);
        Menu(app,"garden");Check(!Array.Exists(g.conversation.choices,c=>c.action=="firstharvest_finish"),"Completed reread has no second payout");Choose(app,"close");
        Console.WriteLine("PASS actual E/numeric/WASD acceptance, immature check, waiting/rest, ordinary harvest, two-herb packing, scout handoff and exact once-only reporting; NATIVE_UNVERIFIED");
    }
    static void GuardsAndPreservedTown(FrontierRuntime app)
    {
        foreach(string missing in new[]{"watergarden_complete","scout_saved"})
        { Prepare(app);var g=app.Game;g.save.flags.Remove(missing);Interact(app,"garden");Check(!State(g,"Ready")&&!Array.Exists(g.conversation.choices,c=>c.action=="firstharvest_menu"),"Offer remains earned by both mature story paths: "+missing);Choose(app,"close"); }
        Prepare(app);var game=app.Game;Plant(app);float planted=game.save.plantedAt;Accept(app);Check(Near(planted,game.save.plantedAt),"An existing growing crop remains valid and unchanged by acceptance");Rest(app);Rest(app);Harvest(app);Check(State(game,"Harvested"),"Existing growing crop earns a real post-acceptance harvest");
        foreach(string method in new[]{"AcceptFirstHarvest","PackFirstHarvest","DeliverFirstHarvest","FinishFirstHarvest"})
        { Position(app,new Point(23,15));string snapshot=JsonUtility.ToJson(game.save);Check(!Api(game,method)&&JsonUtility.ToJson(game.save)==snapshot,"Remote direct API cannot spend or advance: "+method); }
        game.save.inventory[2]=1;Menu(app,"cook");int pack=Array.FindIndex(game.conversation.choices,c=>c.action=="firstharvest_pack");Check(pack>=0,"Insufficient herb packet retains its visible action");Pages(app);string snapshot1=JsonUtility.ToJson(game.save);game.Choose(pack);Check(!State(game,"Packed")&&game.save.inventory[2]==1&&JsonUtility.ToJson(game.save)==snapshot1,"Insufficient packet resources never advance, consume or manufacture supplies");if(game.mode=="dialog")Choose(app,"close");
        game.save.inventory[2]=2;Menu(app,"cook");Pages(app);int stale=Array.FindIndex(game.conversation.choices,c=>c.action=="firstharvest_pack");Position(app,new Point(23,15));game.Choose(stale);Check(!State(game,"Packed")&&game.save.inventory[2]==2,"A stale visible choice rechecks the actual cook location");if(game.mode=="dialog")Choose(app,"close");Pack(app);Check(State(game,"Packed")&&game.save.inventory[2]==0,"Retry spends exactly the available two herbs");Deliver(app);
        foreach(bool coinCap in new[]{true,false})
        { game.save.coins=coinCap?999975:999974;game.save.tea=coinCap?998:999;int gold=game.save.coins,tea=game.save.tea;Report(app);Check(!State(game,"Complete")&&game.save.coins==gold&&game.save.tea==tea,"Coin or tea cap refuses the whole reward atomically");if(game.mode=="dialog")Choose(app,"close"); }
        game.save.coins=999974;game.save.tea=998;Report(app);Check(State(game,"Complete")&&game.save.coins==999999&&game.save.tea==999,"Exact capacity retry accepts the complete original reward");
        Prepare(app);game=app.Game;Accept(app);EarnHarvest(app);int herbs=game.save.inventory[2];game.Enter(9);game.enemies.Clear();Check(game.save.room==9,"Existing inn is reachable by its original travel path");Pack(app);Check(State(game,"Packed")&&game.save.inventory[2]==herbs-2,"Actual canonical innCook packs the same two-herb quest parcel");Menu(app,"cook");Check(!Array.Exists(game.conversation.choices,c=>c.action=="firstharvest_pack"),"Inn cook cannot repack or spend a second pair of herbs");Choose(app,"close");
        Console.WriteLine("PASS earned story gates, pre-existing crop continuity, remote/stale actions, herb denial/retry, independent atomic coin/tea caps and no repeated inn packing; NATIVE_UNVERIFIED");
    }
    static void Reload(FrontierRuntime app,int slot)
    { var g=app.Game;g.PrepareSave();Check(SaveStore.Write(g.save,slot),"Actual SaveStore writes the handoff envelope");string warning;SaveData loaded=SaveStore.Read(slot,out warning);Check(loaded!=null&&warning==null,"Actual SaveStore reads the handoff envelope");g.Load(loaded);g.Start();g.enemies.Clear();Set(app,"camera",g.Position); }
    static void StagedSavesAndReadOnly(FrontierRuntime app)
    {
        Prepare(app);var g=app.Game;string[] original={"ally_1","ally_3","scout_saved","watergarden_complete"};int tea=g.save.tea,coins=g.save.coins;Accept(app);
        for(int phase=0;phase<5;phase++)
        {
            if(phase==1)EarnHarvest(app);if(phase==2)Pack(app);if(phase==3)Deliver(app);if(phase==4)Report(app);
            foreach(int version in new[]{1,2})
            { g.save.version=version;Reload(app,phase%3);Check(State(g,"Accepted")&&State(g,"Harvested")== (phase>=1)&&State(g,"Packed")== (phase>=2)&&State(g,"Delivered")== (phase>=3)&&State(g,"Complete")== (phase>=4),"Saved v"+version+" retains exact earned phase "+phase);foreach(string flag in original)Check(g.Has(flag),"Save round trip retains mature story flag "+flag); }
            Check(g.save.trackedQuest=="firstharvest","Tracking survives each actual stage envelope");
        }
        Check(g.save.coins==coins+33&&g.save.tea==tea+1,"All staged save loads preserve one harvest and one report reward");
        foreach(int version in new[]{1,2})
        {
            Prepare(app);g=app.Game;g.PrepareSave();SaveData old=JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(g.save));old.version=version;old.trackedQuest="watergarden";File.WriteAllText(SaveStore.PathFor(2),JsonUtility.ToJson(old));string warning;SaveData loaded=SaveStore.Read(2,out warning);Check(loaded!=null&&warning==null,"Raw legacy v"+version+" remains readable");g.Load(loaded);g.Start();g.enemies.Clear();Check(State(g,"Ready")&&!State(g,"Accepted")&&!State(g,"Harvested")&&!State(g,"Packed")&&!State(g,"Delivered")&&!State(g,"Complete")&&g.save.trackedQuest=="watergarden","Legacy story grants only the optional offer, without fabricated stages or changed tracking");
        }
        Prepare(app);Accept(app);g=app.Game;g.PrepareSave();Check(SaveStore.Write(g.save,1),"Checksum test writes real envelope");string path=SaveStore.PathFor(1),valid=File.ReadAllText(path);File.WriteAllText(path,valid.Replace("firstharvest_accept","firstharvest_forged"));foreach(string suffix in new[]{".bak",".tmp"})if(File.Exists(path+suffix))File.Delete(path+suffix);string denied;Check(SaveStore.Read(1,out denied)==null&&denied!=null,"A changed quest payload fails the original checksum instead of inventing progress");
        Menu(app,"garden");Pages(app);string snapshot=JsonUtility.ToJson(g.save);int revision=g.revision;float clock=Get<float>(app,"clock");var art=Get<PixelArt>(app,"art");Draw(app);int textures=Get<List<Texture2D>>(art,"owned").Count;
        foreach(EventType type in new[]{EventType.Layout,EventType.Repaint}){Event.current.type=type;Draw(app);Check(snapshot==JsonUtility.ToJson(g.save)&&g.revision==revision&&Get<float>(app,"clock")==clock,"Repeated actual OnGUI does not advance save state, revision or simulation");}Check(textures==Get<List<Texture2D>>(art,"owned").Count,"Reading allocates no repeated art textures");
        Console.WriteLine("PASS actual stage envelopes and legacy v1/v2, original story/tracking continuity, once-only saved reward, checksum rejection and read-only Layout/Repaint; NATIVE_UNVERIFIED");
    }
    static void Journal(FrontierRuntime app,string suffix)
    {
        Set(app,"selectedQuest",0);Key(app,KeyCode.Tab);Draw(app);Check(app.Game.mode=="journal","Actual Tab opens the quest journal");int index=Array.IndexOf(GameCore.QuestIds,"firstharvest");Check(index==GameCore.QuestIds.Length-1&&index==22&&GameCore.QuestIds[21]=="watergarden","New quest appends without reindexing existing stories");
        Click(app,new Rect(927,699,263,26));float spacing=464f/18;Click(app,new Rect(279,226+spacing*3,321,spacing-7));Draw(app,"journal-"+suffix);Check(Get<int>(app,"selectedQuest")==index&&Text(app.Game.GetQuest("firstharvest").title),"Actual second-page row selects first harvest");if(!app.Game.GetQuest("firstharvest").complete)Click(app,new Rect(650,647,258,31));Check(app.Game.save.trackedQuest=="firstharvest","Journal tracking is deliberate and stable");Click(app,new Rect(927,647,263,31));Draw(app,"map-"+suffix);Check(app.Game.mode=="map"&&Get<int>(app,"selectedRegion")==0,"Destination preview opens the actual town quest region");Key(app,KeyCode.Escape);if(app.Game.mode!="world")Key(app,KeyCode.Escape);
    }
    static void AccessibleLocalizedJourney(FrontierRuntime app)
    {
        foreach(bool english in new[]{false,true})foreach(bool large in new[]{false,true})foreach(int width in new[]{960,1280})
        {
            Prepare(app,true,english,large,width);var g=app.Game;string suffix=(english?"en":"zh")+(large?"-large":"-normal")+"-"+width;int input=width==960?0:2;
            Accept(app,input,"offer-"+suffix);Journal(app,suffix);Guide(g,"garden");Plant(app);Menu(app,"garden",input,"growing-"+suffix);Choose(app,"close",input);Rest(app);Rest(app);Harvest(app);Check(State(g,"Harvested"),"Localized real crop harvest advances the optional delivery");Guide(g,"cook");Pack(app,input,"pack-"+suffix);Guide(g,"returnscout");Deliver(app,input,"deliver-"+suffix);Guide(g,"garden");Report(app,input,"report-"+suffix);Check(State(g,"Complete"),"Mouse/pad choices complete localized accessible journey");Menu(app,"garden",input,"complete-"+suffix);Choose(app,"close",input);Journal(app,"complete-"+suffix);
        }
        Console.WriteLine("PASS bilingual normal/large 960/1280 real paginated offers/packets/delivery/reports, mouse/pad activation, appended journal rows, map previews and stage guidance; NATIVE_UNVERIFIED");
    }
    public static int Main(string[] args)
    {
        string previous=Application.persistentDataPath,temp=Path.Combine(Path.GetTempPath(),"starfall-first-harvest-"+Guid.NewGuid());Application.persistentDataPath=temp;captures=args.Length>0&&args[0]!="-"?args[0]:null;prefix=args.Length>1?args[1]:"qa-first-harvest";int failed=0;var app=new FrontierRuntime();
        try
        { Directory.CreateDirectory(temp);if(captures!=null)Directory.CreateDirectory(captures);Call(app,"Awake");foreach(Action<FrontierRuntime> test in new Action<FrontierRuntime>[]{ActualHarvestHandoffJourney,GuardsAndPreservedTown,StagedSavesAndReadOnly,AccessibleLocalizedJourney})try{test(app);}catch(Exception error){failed++;Console.WriteLine("FAIL "+test.Method.Name+": "+(error.InnerException??error));}app.EndVisualReview();Call(app,"OnDestroy"); }
        finally{Application.persistentDataPath=previous;Input.Clear();GUI.queuedClick=null;GUI.enabled=true;GUI.color=Color.white;GUI.matrix=Matrix4x4.identity;Event.current.type=EventType.Repaint;if(Directory.Exists(temp))Directory.Delete(temp,true);}
        Console.WriteLine("Runtime first harvest: "+(4-failed)+"/4 groups passed; captures="+captureCount+"; NATIVE_UNVERIFIED");return failed==0?0:1;
    }
}
