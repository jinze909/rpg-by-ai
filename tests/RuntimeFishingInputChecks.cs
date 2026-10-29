// Actual production Update -> OnGUI input routing under offline Unity declarations.
// Native import, rendering, hardware input and gameplay acceptance are NATIVE_UNVERIFIED.
using System;
using System.IO;
using System.Reflection;
using Starfall;
using UnityEngine;

public static class RuntimeFishingInputChecks
{
    private const BindingFlags All = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static readonly Rect LakeReel = new Rect(473,601,543,56), RiverReel = new Rect(473,611,543,52);
    private static void Check(bool value,string message) { if(!value)throw new Exception(message); }
    private static bool Near(float a,float b) { return Math.Abs(a-b)<.0001f; }
    private static object Call(object target,string name,params object[] args)
    { var method=target.GetType().GetMethod(name,All);Check(method!=null,"Production entry point exists: "+name);return method.Invoke(target,args); }
    private static void Set(object target,string name,object value) { target.GetType().GetField(name,All).SetValue(target,value); }
    private static T Get<T>(object target,string name) { return (T)target.GetType().GetField(name,All).GetValue(target); }
    private static void Draw(FrontierRuntime app)
    { GUI.commands.Clear();GUI.textureCalls.Clear();GUI.buttonCalls.Clear();Call(app,"OnGUI"); }
    private static void Update(FrontierRuntime app,params KeyCode[] keys)
    { foreach(var key in keys)Input.keys.Add(key);Call(app,"Update");Input.Clear(); }
    private static bool ButtonEnabled(Rect rect)
    { return GUI.buttonCalls.Exists(b=>b.rect.x==rect.x&&b.rect.y==rect.y&&b.rect.width==rect.width&&b.rect.height==rect.height&&b.enabled); }
    private static void ClickAfterUpdate(FrontierRuntime app,Rect rect)
    { Check(ButtonEnabled(rect),"Actual Reel control is enabled");GUI.queuedClick=rect;Draw(app);Check(!GUI.queuedClick.HasValue,"Actual OnGUI consumes Reel click"); }
    private static void Beside(FrontierRuntime app,string kind)
    {
        var g=app.Game;var obj=Array.Find(g.Map.objects,o=>o.kind==kind);Check(obj!=null,"Authored object exists: "+kind);
        foreach(var offset in new[]{new Point(0,-.7f),new Point(0,.7f),new Point(-.7f,0),new Point(.7f,0)})
        {
            Point point=new Point(obj.x,obj.y)+offset;if(!g.CanStand(point))continue;
            Check(app.SetVisualReviewPosition(point)&&(g.Position-point).Length<.001f,"Fixture uses full-footprint walkable ground");
            if(ReferenceEquals(g.Nearest(),obj)){Set(app,"camera",g.Position);return;}
        }
        throw new Exception("Real interaction targets canonical "+kind);
    }
    private static void Choose(FrontierRuntime app,string action)
    {
        var g=app.Game;Check(g.mode=="dialog"&&g.conversation!=null,"Real dialogue is open for "+action);
        int choice=Array.FindIndex(g.conversation.choices,c=>c.action==action);Check(choice>=0&&choice<9,"Actual dialogue exposes "+action);
        for(int page=0;page<30;page++){Draw(app);if((bool)Call(app,"DialogueChoicesVisible"))break;Update(app,KeyCode.Return);}
        Check((bool)Call(app,"DialogueChoicesVisible"),"Narrative pages precede the numeric choice");Update(app,(KeyCode)((int)KeyCode.Alpha1+choice));
    }
    private static void Prepare(FrontierRuntime app)
    {
        Input.Clear();GUI.queuedClick=null;GUI.enabled=true;GUI.color=Color.white;GUI.matrix=Matrix4x4.identity;
        Screen.width=1280;Screen.height=800;Time.unscaledDeltaTime=.02f;Event.current.type=EventType.Repaint;
        app.BeginVisualReview(0,0);app.VisualReviewPaused=false;Set(app,"applicationPaused",false);Set(app,"pointerSeen",false);
        var g=app.Game;g.enemies.Clear();g.fallen.Clear();g.effects.Clear();g.hazards.Clear();g.projectiles.Clear();
        g.save.companion=-1;g.save.english=true;g.save.reducedMotion=true;g.save.effectsQuality=0;g.save.bounty=2;g.save.bountyProgress=0;
    }
    private static void Lake(FrontierRuntime app)
    {
        Prepare(app);Beside(app,"fish");Update(app,KeyCode.E);Choose(app,"fish");Check(app.Game.mode=="fish"&&!app.Game.RiverFishingActive,"Original lake dialogue casts into mature fishing");Draw(app);
        // Window also contains navigation/Back controls. Return activates only
        // the actual focused control, so reach Reel through real Tab input.
        string reel="473:601:543:56";int tabs=0;
        while(Get<MenuNavigation>(app,"navigation").focused!=reel&&tabs++<30){Update(app,KeyCode.Tab);Draw(app);}
        Check(Get<MenuNavigation>(app,"navigation").focused==reel,"Actual Tab navigation focuses the visible lake Reel");app.Game.fishTime=0;
    }
    private static void River(FrontierRuntime app,int site=0)
    {
        Prepare(app);var g=app.Game;g.save.flags.Add("ally_2");Beside(app,"fish");Update(app,KeyCode.E);Choose(app,"river_offer");Choose(app,"river_accept");Choose(app,"close");
        Check(g.RiverLessonAccepted,"Actual Old Tide numeric acceptance unlocks the lesson");g.Enter(11);g.enemies.Clear();BeginRiver(app,site);
    }
    private static void BeginRiver(FrontierRuntime app,int site)
    { Beside(app,"riverFishing"+site);Update(app,KeyCode.E);Choose(app,"river_cast_"+site);Check(app.Game.RiverFishingActive&&app.Game.RiverPhase==RiverFishingPhase.Waiting,"Real site dialogue starts Waiting");Draw(app); }
    private static void WaitPhase(FrontierRuntime app,RiverFishingPhase phase)
    { int frames=0;while(app.Game.RiverFishingActive&&app.Game.RiverPhase!=phase&&frames++<260)Update(app);Check(app.Game.RiverFishingActive&&app.Game.RiverPhase==phase,"Accepted Update clock reaches "+phase); }
    private static void AtBand(FrontierRuntime app)
    {
        WaitPhase(app,RiverFishingPhase.Reeling);var g=app.Game;int frames=0;
        while(g.RiverPhase==RiverFishingPhase.Reeling&&g.fishCursor<g.RiverFishingTarget-.025f&&frames++<100)Update(app);
        Check(g.RiverPhase==RiverFishingPhase.Reeling&&Math.Abs(g.fishCursor-g.RiverFishingTarget)<.06f,"Actual river cursor reaches its authored band");
    }
    private static void LakePull(FrontierRuntime app,bool good,params KeyCode[] keys)
    {
        // Lake's sine cursor has no per-round phase. Pin only its clock before
        // the real Update tick so both good and bad timing can be exercised.
        app.Game.fishTime=good?0:.55f;Update(app,keys);Draw(app);
    }

    private static void IndividualInputsAndGuiOrdering(FrontierRuntime app)
    {
        foreach(var key in new[]{KeyCode.E,KeyCode.Space,KeyCode.JoystickButton0,KeyCode.Return})
        {
            Lake(app);var g=app.Game;float world=g.save.totalTime;Update(app,key);
            if(key==KeyCode.Return)Check(g.fishHits==0,"Return waits for actual OnGUI focused-control activation");
            Draw(app);Check(g.fishHits==1&&g.fishMisses==0,"Fresh "+key+" accepts exactly one real lake pull");Draw(app);Draw(app);
            Check(g.fishHits==1&&g.fishMisses==0&&Near(world,g.save.totalTime),"Repeated GUI draws add no pull or world time");
        }
        Lake(app);var lake=app.Game;Input.buttonDown.Add(0);Update(app);Draw(app);Check(lake.fishHits==0,"Mouse press alone waits for its GUI button");ClickAfterUpdate(app,LakeReel);
        Check(lake.fishHits==1&&lake.fishMisses==0,"Mouse Update followed by actual Reel OnGUI accepts one pull");
        Update(app);Draw(app);ClickAfterUpdate(app,LakeReel);Check(lake.fishHits==2,"Fresh mouse frame accepts another pull");
        Console.WriteLine("PASS individual E/Space/pad A, focused Return and mouse Update-to-OnGUI routing; repeated drawing and world pause; NATIVE_UNVERIFIED");
    }

    private static void SameFrameAliasesAndExactLakeThresholds(FrontierRuntime app)
    {
        KeyCode[][] aliases={new[]{KeyCode.JoystickButton0,KeyCode.E},new[]{KeyCode.JoystickButton0,KeyCode.Space},new[]{KeyCode.E,KeyCode.Space},new[]{KeyCode.Return,KeyCode.E},new[]{KeyCode.Return,KeyCode.Space},new[]{KeyCode.Return,KeyCode.JoystickButton0},new[]{KeyCode.JoystickButton0,KeyCode.E,KeyCode.Space,KeyCode.Return}};
        foreach(bool good in new[]{true,false})foreach(var keys in aliases)
        {
            Lake(app);var g=app.Game;int fish=g.save.inventory[3],coins=g.save.coins;
            LakePull(app,good,keys);
            Check(g.fishHits==(good?1:0)&&g.fishMisses==(good?0:1),"Same-frame aliases accept one lake pull; observed hits="+g.fishHits+", misses="+g.fishMisses);
            ClickAfterUpdate(app,LakeReel);ClickAfterUpdate(app,LakeReel);Draw(app);
            Check(g.fishHits==(good?1:0)&&g.fishMisses==(good?0:1),"Same-frame aliases plus Reel accept one "+(good?"good":"missed")+" pull; observed hits="+g.fishHits+", misses="+g.fishMisses);
            Check(g.mode=="fish"&&g.save.inventory[3]==fish&&g.save.coins==coins&&g.save.bountyProgress==0,"One input frame cannot finish or pay a lake catch");
            LakePull(app,good,keys);ClickAfterUpdate(app,LakeReel);Check(g.fishHits==(good?2:0)&&g.fishMisses==(good?0:2)&&g.mode=="fish","A fresh Update permits exactly the second pull");
            LakePull(app,good,keys);Check(g.mode=="world"&&g.fishHits==(good?3:0)&&g.fishMisses==(good?0:3),"Exactly three distinct input frames finish the lake attempt");
            Check(g.save.inventory[3]==fish+(good?1:0)&&g.save.coins==coins+(good?4:0)&&g.save.bountyProgress==(good?1:0),"Three good frames pay one fish/four coins/one bounty; three misses cost nothing");
            Draw(app);Check(g.save.inventory[3]==fish+(good?1:0),"Post-close drawing cannot repeat the catch");
        }
        Console.WriteLine("PASS seven same-frame keyboard/pad/Return alias sets plus Reel, good/miss coalescing, fresh Update reset and exact three-pull lake payout/failure; NATIVE_UNVERIFIED");
    }

    private static void RiverPhasesPauseAndBack(FrontierRuntime app)
    {
        foreach(var back in new[]{KeyCode.Escape,KeyCode.JoystickButton1})
        {
            River(app);var g=app.Game;float world=g.save.totalTime;int fish=g.save.inventory[3],coins=g.save.coins;
            Check(!ButtonEnabled(RiverReel),"Waiting Reel is actually disabled");Update(app,KeyCode.E,KeyCode.Space,KeyCode.JoystickButton0,KeyCode.Return);Draw(app);
            Check(g.fishHits==0&&g.fishMisses==0&&g.RiverPhase==RiverFishingPhase.Waiting,"Waiting aliases do not consume a river round");
            AtBand(app);Draw(app);Update(app,KeyCode.E,KeyCode.Space,KeyCode.JoystickButton0,KeyCode.Return);Draw(app);
            Check(g.fishHits==1&&g.fishMisses==0&&g.RiverPhase==RiverFishingPhase.Resting&&!ButtonEnabled(RiverReel),"One combined input ends the river round with disabled rest Reel");
            for(int frame=0;frame<3;frame++){Update(app,KeyCode.E,KeyCode.Space,KeyCode.JoystickButton0,KeyCode.Return);Draw(app);}
            Check(g.fishHits==1&&g.fishMisses==0&&g.RiverPhase==RiverFishingPhase.Resting,"Rest aliases cannot score or miss");
            WaitPhase(app,RiverFishingPhase.Reeling);WaitPhase(app,RiverFishingPhase.Resting);
            Check(g.fishHits==1&&g.fishMisses==1,"One complete no-input reel window produces exactly one timeout miss");
            AtBand(app);Draw(app);float remaining=g.RiverPhaseRemaining,cursor=g.fishCursor;
            foreach(bool visualPause in new[]{false,true})
            {
                if(visualPause)app.VisualReviewPaused=true;else Set(app,"applicationPaused",true);
                Update(app,KeyCode.E,KeyCode.JoystickButton0,KeyCode.Return);Draw(app);ClickAfterUpdate(app,RiverReel);
                Check(g.fishHits==1&&g.fishMisses==1&&Near(g.RiverPhaseRemaining,remaining)&&Near(g.fishCursor,cursor),"Paused Update and GUI input leave the live reel unchanged");
                app.VisualReviewPaused=false;Set(app,"applicationPaused",false);
            }
            Update(app,KeyCode.Space);Draw(app);Check(g.fishHits==2&&g.RiverPhase==RiverFishingPhase.Resting,"Fresh valid Update resumes and accepts the next river pull");
            Update(app,back,KeyCode.E,KeyCode.Space,KeyCode.JoystickButton0,KeyCode.Return);Draw(app);Check(!g.RiverFishingActive&&g.mode=="world"&&!g.RiverLessonRecorded(0)&&g.fishHits==2&&g.fishMisses==1,"Real "+back+" wins over simultaneous reel aliases and cancels the partial river session");
            Check(g.save.inventory[3]==fish&&g.save.coins==coins&&g.save.bountyProgress==0&&Near(g.save.totalTime,world),"River wait/rest/timeout/pause/Back preserve resources and pause world time");
        }
        Console.WriteLine("PASS real river wait/reel/rest/timeout boundaries, ignored rest aliases, application/review pause, fresh resume and Escape/pad B cancellation; NATIVE_UNVERIFIED");
    }

    private static void RiverCatchBountyAndAutosave(FrontierRuntime app)
    {
        River(app);var g=app.Game;int fish=g.save.inventory[3],coins=g.save.coins;
        for(int pull=0;pull<3;pull++)
        {
            AtBand(app);Update(app,KeyCode.E,KeyCode.Space,KeyCode.JoystickButton0,KeyCode.Return);Draw(app);
            Check(g.fishHits==pull+1,"Each fresh river round accepts exactly one good pull");
            if(pull<2)Check(g.RiverFishingActive&&g.RiverPhase==RiverFishingPhase.Resting&&g.save.inventory[3]==fish&&g.save.bountyProgress==0,"Partial river rounds grant no reward");
        }
        Check(!g.RiverFishingActive&&g.mode=="world"&&g.RiverLessonRecorded(0)&&!g.RiverLessonRecorded(1),"Third real good round records only its canonical river site");
        Check(g.save.inventory[3]==fish+1&&g.save.coins==coins+4&&g.save.bountyProgress==1,"River catch retains one fish/four coins/one mature fishing bounty event");
        BeginRiver(app,1);AtBand(app);Update(app,KeyCode.E);Draw(app);float remaining=g.RiverPhaseRemaining;
        GameCore original=Get<GameCore>(app,"reviewOriginal");
        try
        {
            // Disable only the visual lab's disk-write bypass; the actual
            // Update autosave and SaveStore still own snapshot and envelope.
            Set(app,"reviewOriginal",null);Set(app,"saveTimer",13f);Update(app);
            Check(File.Exists(SaveStore.PathName)&&Near(Get<float>(app,"saveTimer"),0),"Real Update autosave writes its isolated checksummed slot");
            Check(g.RiverFishingActive&&g.fishHits==1&&g.fishMisses==0&&Near(g.RiverPhaseRemaining,remaining-.02f),"Autosave does not settle or reset the partial live river round");
        }
        finally {Set(app,"reviewOriginal",original);}
        string warning;SaveData saved=SaveStore.Read(out warning);Check(saved!=null&&warning==null,"Actual SaveStore reads the autosave envelope");g.Load(saved);g.Start();g.enemies.Clear();
        Check(g.Has("ally_2")&&g.Has("core_1")&&g.RiverLessonAccepted&&g.RiverLessonRecorded(0)&&!g.RiverLessonRecorded(1)&&!g.RiverLessonComplete&&!g.RiverFishingActive,"Reload preserves mature commission/core and earned lesson progress, discarding the partial cast");
        Check(g.save.inventory[3]==fish+1&&g.save.coins==coins+4&&g.save.bountyProgress==1,"Autosave reload cannot duplicate fish, coins or bounty progress");
        Console.WriteLine("PASS actual river three-round catch, per-site lesson record, ordinary fish/coins/bounty, real Update autosave and SaveStore partial-cast reload; NATIVE_UNVERIFIED");
    }

    public static int Main(string[] args)
    {
        string previous=Application.persistentDataPath,directory=Path.Combine(Path.GetTempPath(),"starfall-fishing-input-check-"+Guid.NewGuid());Application.persistentDataPath=directory;
        int failed=0;var app=new FrontierRuntime();
        try
        {
            Call(app,"Awake");Check(app.Game!=null,"Actual Awake loads the production campaign");
            foreach(Action<FrontierRuntime> test in new Action<FrontierRuntime>[]{IndividualInputsAndGuiOrdering,SameFrameAliasesAndExactLakeThresholds,RiverPhasesPauseAndBack,RiverCatchBountyAndAutosave})
                try {test(app);}catch(Exception error){failed++;Console.WriteLine("FAIL "+test.Method.Name+": "+(error.InnerException??error).Message);}
            app.EndVisualReview();Call(app,"OnDestroy");
        }
        finally {Application.persistentDataPath=previous;Input.Clear();GUI.queuedClick=null;if(Directory.Exists(directory))Directory.Delete(directory,true);}
        Console.WriteLine("Runtime fishing input: "+(4-failed)+"/4 groups passed; NATIVE_UNVERIFIED");return failed==0?0:1;
    }
}
