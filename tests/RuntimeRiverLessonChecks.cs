// Actual Update, E/numeric dialogue, OnGUI and SaveStore paths under offline
// Unity declarations. Native import, GPU font rendering and pad hardware are unverified.
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Starfall;
using UnityEngine;

public static class RuntimeRiverLessonChecks
{
    private const BindingFlags All = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static readonly Rect Reel = new Rect(473,611,543,52), Next = new Rect(927,699,263,26);
    private static string captureFolder;
    private static int captures;
    private static void Check(bool value,string message) { if(!value)throw new Exception(message); }
    private static bool Near(float a,float b) { return Math.Abs(a-b)<.006f; }
    private static bool Same(Rect a,Rect b) { return Near(a.x,b.x)&&Near(a.y,b.y)&&Near(a.width,b.width)&&Near(a.height,b.height); }
    private static object Call(object obj,string name,params object[] args)
    { var method=obj.GetType().GetMethod(name,All);Check(method!=null,"Production entry point exists: "+name);return method.Invoke(obj,args); }
    private static void Set(object obj,string name,object value)
    { var field=obj.GetType().GetField(name,All);Check(field!=null,"Fixture field exists: "+name);field.SetValue(obj,value); }
    private static T Get<T>(object obj,string name) { return (T)obj.GetType().GetField(name,All).GetValue(obj); }
    private static T Property<T>(object obj,string name)
    { var property=obj.GetType().GetProperty(name,All);Check(property!=null,"Shipped river behavior exists: "+name);return (T)property.GetValue(obj,null); }
    private static bool Active(GameCore g) { return Property<bool>(g,"RiverFishingActive"); }
    private static string Phase(GameCore g) { return Property<object>(g,"RiverPhase").ToString(); }
    private static float Remaining(GameCore g) { return Property<float>(g,"RiverPhaseRemaining"); }
    private static bool Recorded(GameCore g,int site) { return (bool)Call(g,"RiverLessonRecorded",site); }
    private static void Update(FrontierRuntime app) { Call(app,"Update");Input.keys.Clear(); }
    private static void Key(FrontierRuntime app,KeyCode key) { Input.keys.Add(key);Update(app); }
    private static void Frames(FrontierRuntime app,int count) { for(int i=0;i<count;i++)Update(app); }
    private static void Draw(FrontierRuntime app,string capture=null)
    {
        GUI.commands.Clear();GUI.textureCalls.Clear();GUI.buttonCalls.Clear();Call(app,"OnGUI");
        if(captureFolder!=null&&capture!=null)
        {
            var commands=GUI.commands.ToArray();var textures=GUI.textureCalls.ToArray();var buttons=GUI.buttonCalls.ToArray();
            GUI.Export(captureFolder,"qa-river-lesson-"+capture);captures++;
            GUI.commands.AddRange(commands);GUI.textureCalls.AddRange(textures);GUI.buttonCalls.AddRange(buttons);
        }
    }
    private static bool Text(string value) { return GUI.commands.Exists(c=>c.Contains("\"k\":\"text\"")&&c.Contains(value)); }
    private static void Click(FrontierRuntime app,Rect rect)
    { Draw(app);Check(GUI.buttonCalls.Exists(b=>Same(b.rect,rect)&&b.enabled),"Actual visible control is enabled at "+rect.x+","+rect.y);GUI.queuedClick=rect;Draw(app);Check(!GUI.queuedClick.HasValue,"Production OnGUI consumes the visible control"); }
    private static WorldObject Obj(GameCore g,string kind)
    { var obj=Array.Find(g.Map.objects,o=>o.kind==kind);Check(obj!=null,"Canonical authored object exists: "+kind);return obj; }
    private static void Position(FrontierRuntime app,Point point)
    { Check(app.Game.CanStand(point)&&app.SetVisualReviewPosition(point)&&(app.Game.Position-point).Length<.001f,"Fixture uses exact full-footprint walkable ground");Set(app,"camera",app.Game.Position); }
    private static void Beside(FrontierRuntime app,string kind)
    {
        var obj=Obj(app.Game,kind);Point[] offsets={new Point(0,-.7f),new Point(0,.7f),new Point(-.7f,0),new Point(.7f,0)};
        bool found=false;foreach(Point offset in offsets)
        { Point p=new Point(obj.x,obj.y)+offset;if(!app.Game.CanStand(p))continue;Position(app,p);if(ReferenceEquals(app.Game.Nearest(),obj)){found=true;break;} }
        Check(found,"Real E selects the intended canonical object "+kind);
    }
    private static void Interact(FrontierRuntime app,string kind)
    { Beside(app,kind);Key(app,KeyCode.E);Check(app.Game.mode=="dialog","Actual E opens "+kind+" dialogue"); }
    private static void Choose(FrontierRuntime app,string action)
    {
        var g=app.Game;Check(g.mode=="dialog"&&g.conversation!=null,"Numeric action belongs to an actual open dialogue: "+action);
        int index=Array.FindIndex(g.conversation.choices,c=>c.action==action);Check(index>=0&&index<9,"Actual dialogue exposes action "+action);
        int pages=0;do{Draw(app);if((bool)Call(app,"DialogueChoicesVisible"))break;Key(app,KeyCode.Return);}while(++pages<30);
        Check((bool)Call(app,"DialogueChoicesVisible"),"All actual narrative pages precede the numeric choice");Key(app,(KeyCode)((int)KeyCode.Alpha1+index));
    }
    private static void Prepare(FrontierRuntime app,bool english=true,bool large=false)
    {
        Input.Clear();GUI.queuedClick=null;GUI.enabled=true;GUI.color=Color.white;GUI.matrix=Matrix4x4.identity;
        Screen.width=1280;Screen.height=800;Time.unscaledDeltaTime=.02f;Event.current.type=EventType.Repaint;
        app.BeginVisualReview(0,0);app.VisualReviewPaused=false;var g=app.Game;
        g.enemies.Clear();g.fallen.Clear();g.effects.Clear();g.hazards.Clear();g.projectiles.Clear();
        g.save.flags.RemoveAll(f=>f.StartsWith("riverlesson_"));if(!g.Has("ally_2"))g.save.flags.Add("ally_2");
        g.save.english=english;g.save.largeText=large;g.save.reducedMotion=true;g.save.effectsQuality=0;g.save.showGuidance=true;g.save.companion=-1;
        Set(app,"applicationPaused",false);Set(app,"controllerUsed",false);Set(app,"pointerSeen",false);Set(app,"resumeTime",0f);Set(app,"arrivalTime",0f);Set(app,"combatHudTime",0f);Set(app,"journalTab",0);Set(app,"selectedQuest",0);Set(app,"selectedRegion",-1);Call(app,"Resize");
        Check(Property<bool>(g,"RiverLessonReady")&&!Property<bool>(g,"RiverLessonAccepted"),"Earned Old Tide commission plus core one unlock only the optional offer");
    }
    private static void Accept(FrontierRuntime app)
    {
        Interact(app,"fish");Check(app.Game.conversation.choices[0].action=="fish"&&app.Game.conversation.choices[1].action=="close","Original Old Tide lake and goodbye choices retain their numeric prefix");
        Choose(app,"river_offer");Choose(app,"river_accept");Check(Property<bool>(app.Game,"RiverLessonAccepted"),"Actual Old Tide E and numeric acceptance begin the optional lesson");
        if(app.Game.mode=="dialog")Choose(app,"close");
    }
    private static void Travel(FrontierRuntime app,int room)
    { Key(app,KeyCode.M);Set(app,"selectedRegion",room);Click(app,new Rect(888,616,306,47));Check(app.Game.mode=="world"&&app.Game.save.room==room,"Actual map travel reaches region "+room);app.Game.enemies.Clear();Set(app,"camera",app.Game.Position); }
    private static void Begin(FrontierRuntime app,int site)
    { Interact(app,"riverFishing"+site);Choose(app,"river_cast_"+site);Check(Active(app.Game)&&Phase(app.Game)=="Waiting"&&app.Game.mode=="fish","Actual numbered site cast starts its waiting phase"); }
    private static void Stage(FrontierRuntime app,int site=0)
    { Prepare(app);Accept(app);Travel(app,11);Begin(app,site); }
    private static void WaitPhase(FrontierRuntime app,string phase)
    { int frames=0;while(Active(app.Game)&&Phase(app.Game)!=phase&&frames++<250)Update(app);Check(Active(app.Game)&&Phase(app.Game)==phase,"Actual accepted clock reaches "+phase); }
    private static void AtBand(FrontierRuntime app)
    {
        WaitPhase(app,"Reeling");float target=Property<float>(app.Game,"RiverFishingTarget");int frames=0;
        while(Phase(app.Game)=="Reeling"&&app.Game.fishCursor<target-.025f&&frames++<100)Update(app);
        Check(Phase(app.Game)=="Reeling"&&Math.Abs(app.Game.fishCursor-target)<.06f,"Actual timed cursor reaches this site's distinct band");
    }
    private static void Catch(FrontierRuntime app,KeyCode key=KeyCode.E)
    { for(int pull=0;pull<3;pull++){AtBand(app);Key(app,key);if(pull<2)Check(Phase(app.Game)=="Resting","Each good input ends the round before another can count");}Check(!Active(app.Game)&&app.Game.mode=="world","Three actual good pulls finish and close the cast"); }
    private static void WalkToObject(FrontierRuntime app,string kind)
    {
        var g=app.Game;var obj=Obj(g,kind);List<Point> path=g.InteractionPath(obj);Check(path.Count>0,"Canonical site has an actual interaction path from the arrival");int frames=0;
        while(!ReferenceEquals(g.Nearest(),obj)&&frames++<6500)
        {
            while(path.Count>0&&(path[0]-g.Position).Length<.14f)path.RemoveAt(0);
            Check(path.Count>0,"The real route retains a walking point until E targets the exact site");Point move=(path[0]-g.Position).Normalized;
            Input.axes["Horizontal"]=move.x;Input.axes["Vertical"]=-move.y;Input.held.Clear();if(move.x>.05f)Input.held.Add(KeyCode.D);if(move.x<-.05f)Input.held.Add(KeyCode.A);if(move.y>.05f)Input.held.Add(KeyCode.S);if(move.y<-.05f)Input.held.Add(KeyCode.W);Update(app);
            Check(g.CanStand(g.Position),"Actual held movement preserves full-footprint collision on the site route");
        }
        Input.axes.Clear();Input.held.Clear();Check(frames>0&&frames<6500&&ReferenceEquals(g.Nearest(),obj),"Real held WASD follows the manual interaction route to "+kind);Set(app,"camera",g.Position);
    }

    private static void StoryJournalRoutesAndReward(FrontierRuntime app)
    {
        Prepare(app);var g=app.Game;Accept(app);Key(app,KeyCode.Tab);Draw(app);Check(g.mode=="journal","Actual world Tab opens the quest journal");
        int quest=Array.IndexOf(GameCore.QuestIds,"riverlesson");Check(quest==20&&GameCore.QuestIds[19]=="returnletter","River lesson appends after the existing second-page quest");
        Click(app,Next);float spacing=464f/18;Click(app,new Rect(279,226+spacing,321,spacing-7));Draw(app);
        Check(Get<int>(app,"selectedQuest")==quest&&Text(g.GetQuest("riverlesson").title),"Actual second-page row selects the new lesson");
        Click(app,new Rect(650,647,258,31));Check(g.save.trackedQuest=="riverlesson","Actual journal tracking retains the new quest ID");Click(app,new Rect(927,647,263,31));
        Check(g.mode=="map"&&Get<int>(app,"selectedRegion")==11,"Actual Show destination opens the authored riverbank");Click(app,new Rect(888,616,306,47));g.enemies.Clear();
        Check(g.save.room==11&&!g.ReachBridgeOpen&&!g.ReachRootsOpen,"The lesson reaches the original closed riverbank layout");
        int fish=g.save.inventory[3],coins=g.save.coins;g.save.bounty=2;g.save.bountyProgress=0;
        for(int site=0;site<2;site++)
        {
            Update(app);Point? target=g.NavigationTarget();var obj=Obj(g,"riverFishing"+site);
            Check(target.HasValue&&(target.Value-new Point(obj.x,obj.y)).Length<.001f,"Quest guide targets the next unrecorded canonical site");
            Point? routeGoal=Get<Point?>(app,"routeGoal");Check(routeGoal.HasValue&&(routeGoal.Value-target.Value).Length<.001f&&Get<List<Point>>(app,"route").Count>0,"Actual Update refreshes the visible walking route toward the next lesson site");
            WalkToObject(app,"riverFishing"+site);Key(app,KeyCode.E);Choose(app,"river_cast_"+site);Catch(app,site==0?KeyCode.E:KeyCode.Space);
            Check(Recorded(g,site)&&g.save.inventory[3]==fish+site+1&&g.save.coins==coins+(site+1)*4&&g.save.bountyProgress==site+1,"Actual site catch pays one ordinary fish, four coins, one fishing bounty event and its own record");
        }
        Check(!g.GetQuest("reach").complete&&!g.ReachBridgeOpen&&!g.ReachRootsOpen&&!g.ReachCacheRecovered,"Fishing never fabricates the original survey, shortcut, roots or cache");
        Travel(app,0);Interact(app,"fish");Choose(app,"river_offer");int stew=g.save.inventory[6],trust=g.save.reputation[2];Choose(app,"river_finish");
        Check(Property<bool>(g,"RiverLessonComplete")&&g.save.inventory[6]==stew+2&&g.save.reputation[2]==trust+1,"Actual Old Tide report grants exactly two stews and Wild Assembly trust +1");
        if(g.mode=="dialog")Choose(app,"close");Interact(app,"fish");Choose(app,"river_offer");Check(Array.FindIndex(g.conversation.choices,c=>c.action=="river_finish")<0,"Completed reread offers no repeated reward");Choose(app,"close");
        Check(g.save.inventory[6]==stew+2&&g.save.reputation[2]==trust+1&&g.GetQuest("riverlesson").complete,"Rereading retains the exact once-only reward and completed journal");
        Console.WriteLine("PASS actual E/numeric acceptance, Tab second-page track/map, two held-WASD site approaches and casts, per-site ordinary fish/bounty rewards and once-only final report; NATIVE_UNVERIFIED");
    }

    private static void RealInputsOnePullTimeoutAndCancel(FrontierRuntime app)
    {
        foreach(KeyCode key in new[]{KeyCode.E,KeyCode.Space,KeyCode.JoystickButton0})
        {
            Stage(app);var g=app.Game;int coins=g.save.coins,fish=g.save.inventory[3];float world=g.save.totalTime;
            Key(app,key);Check(Phase(g)=="Waiting"&&g.fishHits==0&&g.fishMisses==0,"Actual early keyboard/pad input is ignored while waiting");AtBand(app);Key(app,key);
            Check(g.fishHits==1&&g.fishMisses==0&&Phase(g)=="Resting","Actual E/Space/A can score exactly one good pull");
            for(int i=0;i<4;i++)Key(app,key);Check(g.fishHits==1&&g.fishMisses==0&&Phase(g)=="Resting","Repeated actual input cannot score during rest");
            Key(app,KeyCode.Escape);Check(!Active(g)&&g.mode=="world"&&!Recorded(g,0)&&g.save.coins==coins&&g.save.inventory[3]==fish,"Actual Escape cancels without a catch, site record or fee");
            Check(Near(g.save.totalTime,world),"The complete live cast pauses ordinary world time");
        }
        Stage(app,1);var game=app.Game;Draw(app);Check(GUI.buttonCalls.Exists(b=>Same(b.rect,Reel)&&!b.enabled),"Actual waiting Reel control is disabled");AtBand(app);Click(app,Reel);
        Check(game.fishHits==1&&Phase(game)=="Resting","Actual enabled Reel click scores exactly once");Draw(app);Check(GUI.buttonCalls.Exists(b=>Same(b.rect,Reel)&&!b.enabled),"Actual rest Reel control is disabled");Key(app,KeyCode.JoystickButton1);
        Check(!Active(game)&&game.mode=="world"&&!Recorded(game,1),"Actual pad B uses the existing menu Back cancellation");
        Begin(app,1);int beforeFish=game.save.inventory[3],beforeCoins=game.save.coins;Frames(app,510);
        Check(!Active(game)&&game.fishHits==0&&game.fishMisses==3&&!Recorded(game,1)&&game.save.inventory[3]==beforeFish&&game.save.coins==beforeCoins,"Three real no-input timeouts fail for free without recording a catch");
        Begin(app,1);Catch(app,KeyCode.JoystickButton0);Check(Recorded(game,1),"Fresh real pad A inputs can complete a free retry");
        Console.WriteLine("PASS actual E/Space/pad A/click single-pull routing, disabled wait/rest controls, Escape/B cancel, three no-input timeout misses, paused world and free pad retry; NATIVE_UNVERIFIED");
    }

    private static void SaveStoreLegacyAndReadOnlyDrawing(FrontierRuntime app)
    {
        Stage(app);var g=app.Game;Catch(app);Begin(app,1);AtBand(app);Key(app,KeyCode.E);
        int partialFish=g.save.inventory[3],partialCoins=g.save.coins;float partialRemaining=Remaining(g),partialCursor=g.fishCursor;
        GameCore reviewOriginal=Get<GameCore>(app,"reviewOriginal");
        try
        {
            // Disable only the visual-lab save bypass. The real Update -> Save
            // path still owns snapshot preparation and writes this isolated temp directory.
            Set(app,"reviewOriginal",null);Set(app,"saveTimer",13f);Update(app);
            Check(File.Exists(SaveStore.PathName)&&Near(Get<float>(app,"saveTimer"),0),"Actual Update autosave writes its main checksummed slot after the twelve-second threshold");
            Check(Active(g)&&Phase(g)=="Resting"&&g.fishHits==1&&g.fishMisses==0&&Near(Remaining(g),partialRemaining-.02f)&&Near(g.fishCursor,partialCursor),"Actual autosave keeps the live phase, counts and cursor; only the accepted Update tick advances remaining time");
            Check(g.save.inventory[3]==partialFish&&g.save.coins==partialCoins&&!Recorded(g,1),"Actual autosave cannot settle the partial second-site catch");
        }
        finally { Set(app,"reviewOriginal",reviewOriginal); }
        string warning;SaveData loaded=SaveStore.Read(out warning);Check(loaded!=null&&warning==null,"Actual SaveStore reads the real autosave river envelope");g.Load(loaded);g.Start();g.enemies.Clear();
        Check(Property<bool>(g,"RiverLessonAccepted")&&Recorded(g,0)&&!Recorded(g,1)&&!Active(g)&&g.save.trackedQuest=="riverlesson","Envelope reload preserves permanent lesson progress and tracking while discarding the partial cast");
        Check(g.save.inventory[3]==partialFish&&g.save.coins==partialCoins,"Reloading the actual autosave grants no partial-cast fish or coins");
        Begin(app,1);Catch(app);Travel(app,0);g.save.inventory[6]=9998;Interact(app,"fish");Choose(app,"river_offer");int trust=g.save.reputation[2];Choose(app,"river_finish");
        Check(!Property<bool>(g,"RiverLessonComplete")&&g.save.inventory[6]==9998&&g.save.reputation[2]==trust,"A real full-pantry report refuses both reward and completion atomically");
        if(g.mode=="dialog")Choose(app,"close");g.save.inventory[6]=9997;Interact(app,"fish");Choose(app,"river_offer");Choose(app,"river_finish");if(g.mode=="dialog")Choose(app,"close");g.PrepareSave();Check(SaveStore.Write(g.save,2),"Actual SaveStore writes the completed lesson");
        loaded=SaveStore.Read(2,out warning);g.Load(loaded);g.Start();Check(Property<bool>(g,"RiverLessonComplete")&&g.save.inventory[6]==9999&&g.save.reputation[2]==trust+1,"Completed envelope retains exactly two stews and one trust");
        foreach(int version in new[]{1,2})
        {
            Prepare(app);g=app.Game;g.PrepareSave();SaveData old=JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(g.save));old.version=version;File.WriteAllText(SaveStore.PathFor(2),JsonUtility.ToJson(old));
            loaded=SaveStore.Read(2,out warning);Check(loaded!=null&&warning==null,"Raw legacy v"+version+" remains readable");g.Load(loaded);g.Start();
            Check(Property<bool>(g,"RiverLessonReady")&&!Property<bool>(g,"RiverLessonAccepted")&&!Recorded(g,0)&&!Recorded(g,1)&&!Property<bool>(g,"RiverLessonComplete"),"Legacy saves unlock only the offer without fabricated progress");
            Check(g.Has("ally_2")&&g.Has("core_1")&&g.GetQuest("ally2").complete,"Legacy upgrade preserves the original fishing commission and core");
        }
        Stage(app);g=app.Game;foreach(string phase in new[]{"Waiting","Reeling","Resting"})
        {
            if(phase=="Reeling")AtBand(app);if(phase=="Resting")Key(app,KeyCode.E);
            string save=JsonUtility.ToJson(g.save);float remaining=Remaining(g),cursor=g.fishCursor;int hits=g.fishHits,misses=g.fishMisses;
            Draw(app);Draw(app);Check(save==JsonUtility.ToJson(g.save)&&Near(remaining,Remaining(g))&&Near(cursor,g.fishCursor)&&g.fishHits==hits&&g.fishMisses==misses,"Actual repeated OnGUI is read-only in "+phase);
        }
        Console.WriteLine("PASS actual Update autosave preserves live phase/counts without reward, reload cancels partial cast, completed envelope/raw legacy v1/v2, atomic pantry refusal and read-only OnGUI; NATIVE_UNVERIFIED");
    }

    private static void BilingualLayoutsMarkersAndMatureLake(FrontierRuntime app)
    {
        for(int language=0;language<2;language++)for(int large=0;large<2;large++)foreach(int width in new[]{960,1280})
        {
            Stage(app,(language+large)%2);var g=app.Game;g.save.english=language==1;g.save.largeText=large==1;g.toastTime=0;Screen.width=width;Screen.height=width==960?600:800;Call(app,"Resize");string suffix=(language==1?"en":"zh")+(large==1?"-large":"-normal")+"-"+width;
            foreach(string phase in new[]{"Waiting","Reeling","Resting"})
            {
                if(phase=="Reeling")AtBand(app);if(phase=="Resting")Key(app,KeyCode.E);Draw(app,phase.ToLowerInvariant()+"-"+suffix);
                string name=language==1?(phase=="Waiting"?"Wait for the float":phase=="Reeling"?"Choose when to reel":"Let the line rest"):(phase=="Waiting"?"等候浮标":phase=="Reeling"?"选择时机收线":"放松钓线");
                Check(Text(name)&&Text(language==1?"Phase remaining ":"阶段剩余 "),"Localized phase and separate authoritative remaining time are drawn");
                Check(Text(language==1?"River fishing":"河岸练钓")&&!Text(language==1?"Fishing by the lake":"湖畔垂钓"),"Actual fish renderer enters the river-only branch");
                float target=Property<float>(g,"RiverFishingTarget");Check(GUI.textureCalls.Exists(t=>Same(t.rect,new Rect(335+826*(target-.14f),436,826*.28f,37))),"Actual teal band matches the site's true center and shared half-width");
                Check(GUI.buttonCalls.Exists(b=>Same(b.rect,Reel)&&b.enabled==(phase=="Reeling")),"Rendered Reel enablement matches the actual phase");
                Check(Text("Good pulls ")||Text("成功 "),"The authoritative success/miss counters remain visible under reduced motion and low effects");
            }
            Key(app,KeyCode.Escape);
        }
        Prepare(app);var game=app.Game;Travel(app,11);game.save.explored[11]=16777215;Draw(app);
        foreach(int site in new[]{0,1}){var obj=Obj(game,"riverFishing"+site);Check(!game.IsObjectPresent(obj)&&!game.DiscoveredLandmarks().Contains(obj),"Hidden pre-acceptance fishing sites never leak into discovered landmarks");}
        Travel(app,0);Accept(app);Travel(app,11);game.save.explored[11]=16777215;
        foreach(int site in new[]{0,1})
        {
            var obj=Obj(game,"riverFishing"+site);Check(game.IsObjectPresent(obj)&&game.DiscoveredLandmarks().Contains(obj),"Accepted explored site is an ordinary discovered landmark");Beside(app,obj.kind);Draw(app);
            Point p=(Point)Call(app,"ScreenPoint",new Point(obj.x,obj.y));Texture2D icons=Get<Texture2D>(app,"iconSheet");
            Check(GUI.textureCalls.Exists(t=>t.texture==icons&&Same(t.rect,new Rect(p.x-20,p.y-46,40,40))&&Near(t.uv.x,.75f)&&Near(t.uv.y,.5f)),"Actual world marker uses the existing fish icon atlas instead of an NPC");
            float cell=3;Check(GUI.textureCalls.Exists(t=>Same(t.rect,new Rect(1024+obj.x*cell-1,42+obj.y*cell-1,3,3))),"Actual world OnGUI minimap includes the canonical fishing site marker");
            Key(app,KeyCode.M);Draw(app);cell=4.2f;Check(GUI.textureCalls.Exists(t=>Same(t.rect,new Rect(888+obj.x*cell-1,264+obj.y*cell-1,3,3))),"Actual local map OnGUI includes the discovered canonical fishing site marker");Key(app,KeyCode.Escape);
        }
        Travel(app,0);Interact(app,"fish");Choose(app,"fish");Check(!Active(game)&&game.mode=="fish","The original lake action remains its own mature mode");Draw(app);
        Check(Text("Fishing by the lake")&&GUI.textureCalls.Exists(t=>Same(t.rect,new Rect(335+826*.32f,436,826*.36f,37))),"Original lake title, center and wider band remain unchanged");
        int fish=game.save.inventory[3],coins=game.save.coins;for(int hit=0;hit<3;hit++){game.fishCursor=.5f;Time.unscaledDeltaTime=0;Key(app,KeyCode.E);}Time.unscaledDeltaTime=.02f;
        Check(game.mode=="world"&&game.save.inventory[3]==fish+1&&game.save.coins==coins+4&&!Recorded(game,0)&&!Recorded(game,1),"Original lake three-pull payout remains unchanged and records no river site");
        Console.WriteLine("PASS 24 actual bilingual normal/large 960/1280 phase layouts, truthful band/control state, accepted-only discovered landmarks/minimap/fish-icon markers and unchanged lake input/payout; NATIVE_UNVERIFIED");
    }
    public static int Main(string[] args)
    {
        string previous=Application.persistentDataPath,directory=Path.Combine(Path.GetTempPath(),"starfall-river-lesson-check-"+Guid.NewGuid());Application.persistentDataPath=directory;
        captureFolder=args.Length>0?args[0]:null;if(captureFolder!=null)Directory.CreateDirectory(captureFolder);int failed=0;var app=new FrontierRuntime();
        try
        {
            Call(app,"Awake");Check(app.Game!=null,"Actual runtime Awake loads the shipped campaign");
            foreach(Action<FrontierRuntime> test in new Action<FrontierRuntime>[]{StoryJournalRoutesAndReward,RealInputsOnePullTimeoutAndCancel,SaveStoreLegacyAndReadOnlyDrawing,BilingualLayoutsMarkersAndMatureLake})
                try{test(app);}catch(Exception error){failed++;Console.WriteLine("FAIL "+test.Method.Name+": "+(error.InnerException??error).Message);}
            app.EndVisualReview();Call(app,"OnDestroy");
        }
        finally{Application.persistentDataPath=previous;Input.Clear();GUI.queuedClick=null;if(Directory.Exists(directory))Directory.Delete(directory,true);}
        Console.WriteLine("Runtime River Lesson: "+(4-failed)+"/4 groups passed; captures="+captures+"; NATIVE_UNVERIFIED");return failed==0?0:1;
    }
}
