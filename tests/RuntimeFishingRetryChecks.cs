// Actual production Update -> OnGUI retry routing under offline Unity declarations.
// Native import, GPU/font rendering, hardware input and play acceptance are NATIVE_UNVERIFIED.
using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using Starfall;
using UnityEngine;

public static class RuntimeFishingRetryChecks
{
    private const BindingFlags All=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    private static readonly Rect Retry=new Rect(394,638,492,30),LakeReel=new Rect(473,601,543,56),RiverReel=new Rect(473,611,543,52);
    private static string captureFolder,capturePrefix;
    private sealed class Label { public string text=null;public float[] r=null;public int size=0; }
    private static void Check(bool value,string message) { if(!value)throw new Exception(message); }
    private static bool Near(float a,float b) { return Math.Abs(a-b)<.0002f; }
    private static object Call(object target,string name,params object[] args)
    { var method=target.GetType().GetMethod(name,All);Check(method!=null,"Production entry point exists: "+name);return method.Invoke(target,args); }
    private static void Set(object target,string name,object value) { target.GetType().GetField(name,All).SetValue(target,value); }
    private static T Get<T>(object target,string name) { return (T)target.GetType().GetField(name,All).GetValue(target); }
    // New APIs are reflected only after asserting the actual HUD. The unchanged
    // baseline therefore compiles and fails for the missing player-visible action.
    private static object Offer(GameCore g)
    { var property=g.GetType().GetProperty("RecentFishingRetry");Check(property!=null,"Authoritative retry offer exists");return property.GetValue(g,null); }
    private static bool CanRetry(GameCore g)
    { var property=g.GetType().GetProperty("CanRetryFishing");Check(property!=null,"Authoritative retry eligibility exists");return (bool)property.GetValue(g,null); }
    private static float Remaining(GameCore g) { return Get<float>(g,"fishingRetryRemaining"); }
    private static string State(FrontierRuntime app)
    { var g=app.Game;return JsonUtility.ToJson(g.save)+"|"+g.revision+"|"+g.mode+"|"+g.toast+"|"+g.toastTime+"|"+g.fishHits+"|"+g.fishMisses+"|"+g.fishTime+"|"+g.fishCursor+"|"+Remaining(g)+"|"+Get<float>(app,"clock")+"|"+Get<float>(app,"saveTimer"); }
    private static void Draw(FrontierRuntime app,string capture=null)
    {
        GUI.commands.Clear();GUI.textureCalls.Clear();GUI.buttonCalls.Clear();Call(app,"OnGUI");
        if(captureFolder!=null&&capture!=null)
        {
            var commands=GUI.commands.ToArray();var textures=GUI.textureCalls.ToArray();var buttons=GUI.buttonCalls.ToArray();
            GUI.Export(captureFolder,capturePrefix+"-"+capture);
            GUI.commands.AddRange(commands);GUI.textureCalls.AddRange(textures);GUI.buttonCalls.AddRange(buttons);
        }
    }
    private static void Update(FrontierRuntime app,params KeyCode[] keys)
    { foreach(var key in keys)Input.keys.Add(key);Call(app,"Update");Input.Clear(); }
    private static bool Same(Rect a,Rect b) { return Near(a.x,b.x)&&Near(a.y,b.y)&&Near(a.width,b.width)&&Near(a.height,b.height); }
    private static bool ButtonExists(Rect rect,bool enabled) { return GUI.buttonCalls.Exists(b=>Same(b.rect,rect)&&b.enabled==enabled); }
    private static string Expected(GameCore g,bool success) { return success?g.L("再钓一条","Fish again"):g.L("再试一次","Try again"); }
    private static object AssertCard(FrontierRuntime app,bool success,bool river,bool enabled)
    {
        string expected=Expected(app.Game,success);
        Check(ButtonExists(Retry,enabled)&&GUI.commands.Exists(c=>c.Contains("\"r\":[394,638,492,30]")&&c.Contains("\"text\":"+GUI.S(expected))),"Actual world HUD shows "+(enabled?"enabled":"disabled")+" retry button: "+expected);
        var g=app.Game;object offer=Offer(g);Check(offer!=null&&CanRetry(g)&&g.mode=="world","Only an eligible completed world attempt presents retry");
        int site=Get<int>(offer,"site");
        Check(Get<bool>(offer,"river")==river&&Get<bool>(offer,"success")==success&&(river?g.save.room==11&&site>=0&&site<=1:site==-1),"Retry identity agrees with the settled outcome and fishing type");
        foreach(string field in new[]{"river","success","site"})Check(offer.GetType().GetField(field).IsInitOnly,"Retry field is immutable: "+field);
        Check(GUI.textureCalls.Exists(c=>Same(c.rect,new Rect(382,604,516,72))),"Retry uses the reserved world card bounds");
        string header=GUI.commands.Find(c=>c.StartsWith("{\"k\":\"text\"",StringComparison.Ordinal)&&c.Contains("\"r\":[422,610,464,22]"));
        Check(header!=null,"Retry has one localized supporting header");var label=JsonUtility.FromJson<Label>(header);
        Check(label.text==g.L("本次垂钓已结束","This cast is finished")&&label.size==(g.save.largeText?16:15),"Localized supporting header honors normal/large text size");
        return offer;
    }
    private static void NoCard() { Check(!GUI.buttonCalls.Exists(b=>Same(b.rect,Retry)),"Ineligible or non-world context shows no retry control"); }
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
        var g=app.Game;Check(g.mode=="dialog"&&g.conversation!=null,"Real dialogue opens for "+action);
        int choice=Array.FindIndex(g.conversation.choices,c=>c.action==action);Check(choice>=0&&choice<9,"Actual dialogue exposes "+action);
        for(int page=0;page<30;page++){Draw(app);if((bool)Call(app,"DialogueChoicesVisible"))break;Update(app,KeyCode.Return);}
        Check((bool)Call(app,"DialogueChoicesVisible"),"Narrative pages precede numeric choice");Update(app,(KeyCode)((int)KeyCode.Alpha1+choice));
    }
    private static void Prepare(FrontierRuntime app)
    {
        Input.Clear();GUI.queuedClick=null;GUI.enabled=true;GUI.color=Color.white;GUI.matrix=Matrix4x4.identity;
        Screen.width=1280;Screen.height=800;Time.unscaledDeltaTime=.02f;Event.current.type=EventType.Repaint;
        app.BeginVisualReview(0,0);app.VisualReviewPaused=false;Set(app,"applicationPaused",false);Set(app,"pointerSeen",false);
        Set(app,"confirmControl",null);Set(app,"pressedControl",null);
        var g=app.Game;g.enemies.Clear();g.fallen.Clear();g.effects.Clear();g.hazards.Clear();g.projectiles.Clear();
        g.save.companion=-1;g.save.english=true;g.save.largeText=false;g.save.reducedMotion=true;g.save.effectsQuality=0;g.save.bounty=2;g.save.bountyProgress=0;
    }
    private static void Lake(FrontierRuntime app)
    { Prepare(app);Beside(app,"fish");Update(app,KeyCode.E);Choose(app,"fish");Check(app.Game.mode=="fish"&&!app.Game.RiverFishingActive,"Original lake dialogue starts the mature cast");Draw(app); }
    private static void River(FrontierRuntime app,int site=0)
    {
        Prepare(app);var g=app.Game;g.save.flags.Add("ally_2");Beside(app,"fish");Update(app,KeyCode.E);Choose(app,"river_offer");Choose(app,"river_accept");Choose(app,"close");
        Check(g.RiverLessonAccepted,"Original acceptance unlocks river notes");g.Enter(11);g.enemies.Clear();Beside(app,"riverFishing"+site);Update(app,KeyCode.E);Choose(app,"river_cast_"+site);
        Check(g.RiverFishingActive&&g.RiverPhase==RiverFishingPhase.Waiting,"Original river dialogue starts Waiting");Draw(app);
    }
    private static void WaitPhase(FrontierRuntime app,RiverFishingPhase phase)
    { int frames=0;while(app.Game.RiverFishingActive&&app.Game.RiverPhase!=phase&&frames++<260)Update(app);Check(app.Game.RiverFishingActive&&app.Game.RiverPhase==phase,"Accepted Update reaches "+phase); }
    private static void Pull(FrontierRuntime app,bool river,bool success,bool timeout,params KeyCode[] keys)
    {
        var g=app.Game;
        if(!river) { g.fishTime=success?0:.55f;Update(app,keys);return; }
        WaitPhase(app,RiverFishingPhase.Reeling);
        if(timeout)
        { int frames=0;while(g.RiverFishingActive&&g.RiverPhase==RiverFishingPhase.Reeling&&frames++<180)Update(app);Check(g.mode=="world"||g.RiverPhase==RiverFishingPhase.Resting,"Untouched reel window records a timeout");return; }
        if(success)
        { int frames=0;while(g.RiverPhase==RiverFishingPhase.Reeling&&g.fishCursor<g.RiverFishingTarget-.025f&&frames++<100)Update(app);Check(g.RiverPhase==RiverFishingPhase.Reeling,"Real float reaches its authored band"); }
        Update(app,keys);
    }
    private static void Finish(FrontierRuntime app,bool river,bool success,bool timeout=false,int site=0)
    {
        if(river)River(app,site);else Lake(app);var g=app.Game;int fish=g.save.inventory[3],coins=g.save.coins;float world=g.save.totalTime;
        bool ally=g.Has("ally_2"),complete=g.RiverLessonComplete;int stew=g.save.inventory[6];
        for(int pull=0;pull<3;pull++)
        { Pull(app,river,success,timeout,KeyCode.E);if(pull<2){Check(g.mode=="fish","Partial attempt remains in mature fishing");Draw(app);NoCard();} }
        Check(g.mode=="world"&&!g.RiverFishingActive&&g.fishHits==(success?3:0)&&g.fishMisses==(success?0:3),"Exactly three real good pulls/misses finish and return to world");
        Check(g.save.inventory[3]==fish+(success?1:0)&&g.save.coins==coins+(success?4:0)&&g.save.bountyProgress==(success?1:0),"Existing one-fish/four-coin/bounty settlement is unchanged");
        Check(g.Has("ally_2")==ally&&g.RiverLessonComplete==complete&&g.save.inventory[6]==stew&&Near(g.save.totalTime,world),"Retry does not complete the companion commission/river lesson or advance fishing world time");
        if(river)Check(g.RiverLessonRecorded(site)==success&&!g.RiverLessonRecorded(1-site),"Original successful river catch records exactly its authored site");
        string toast=success?(river?"River Silverfin x1! This site is recorded.":"Caught a Silverfin!"):(river?"The fish slipped away. Notes and supplies are safe; try again.":"The fish slipped away. Nothing lost; try again.");
        Check(g.toast==toast&&g.toastTime>0,"Original terminal toast survives the optional retry offer");
    }
    private static void Guard(FrontierRuntime app) { Finish(app,false,true);Draw(app);AssertCard(app,true,false,false); }
    private static void Arm(FrontierRuntime app,bool success,bool river)
    { Draw(app);AssertCard(app,success,river,false);Update(app);Draw(app);AssertCard(app,success,river,true); }
    private static void ClickRetry(FrontierRuntime app)
    { Check(ButtonExists(Retry,true),"A visible retry was armed by a later accepted Update");GUI.queuedClick=Retry;Draw(app);Check(!GUI.queuedClick.HasValue,"Actual OnGUI consumes retry click"); }

    private static void LocalizedSettledCardsAndReadOnlyDrawing(FrontierRuntime app)
    {
        foreach(bool river in new[]{false,true})foreach(bool success in new[]{false,true})foreach(bool english in new[]{false,true})foreach(bool large in new[]{false,true})
        {
            Finish(app,river,success,false,river&&success?1:0);var g=app.Game;g.save.english=english;g.save.largeText=large;
            Draw(app);object offer=AssertCard(app,success,river,false);if(river)Check(Get<int>(offer,"site")== (success?1:0),"Offer retains the original river site");
            string before=State(app);
            foreach(EventType type in new[]{EventType.Layout,EventType.Repaint,EventType.Repaint})
            { Event.current.type=type;Draw(app);AssertCard(app,success,river,false);Check(State(app)==before&&ReferenceEquals(Offer(g),offer),"Repeated Layout/Repaint neither arms retry nor changes save, clocks, scores, toast or immutable offer"); }
            Update(app);Draw(app,(river?"river":"lake")+"-"+(success?"success":"failure")+"-"+(english?"en":"zh")+(large?"-large":"-normal"));
            AssertCard(app,success,river,true);before=State(app);Draw(app);Check(State(app)==before&&ReferenceEquals(Offer(g),offer),"Enabled card drawing remains read-only");
        }
        Console.WriteLine("PASS 16 bilingual normal/large lake/river success/failure world cards, immutable site/outcome identity, exact mature rewards/story/toasts and read-only Layout/Repaint; NATIVE_UNVERIFIED");
    }

    private static void TerminalFrameArmingAndRetryInputIsolation(FrontierRuntime app)
    {
        Guard(app);
        foreach(bool river in new[]{false,true})foreach(bool success in new[]{false,true})
        {
            if(river)River(app,1);else Lake(app);var g=app.Game;
            for(int pull=0;pull<2;pull++){Pull(app,river,success,false,KeyCode.E);Draw(app);}
            // Stale fishing focus/confirm intent and all reel aliases belong to
            // the ending attempt, even when a timeout closes inside Tick.
            Set(app,"confirmControl","394:638:492:30");Pull(app,river,success,false,KeyCode.E,KeyCode.Space,KeyCode.JoystickButton0,KeyCode.Return);
            Check(g.mode=="world","Ending aliases leave the settled attempt in world");Draw(app);object offer=AssertCard(app,success,river,false);
            GUI.queuedClick=Retry;Draw(app);Check(GUI.queuedClick.HasValue&&g.mode=="world"&&ReferenceEquals(Offer(g),offer),"First disabled card rejects a same-ending-frame mouse activation");GUI.queuedClick=null;
            Draw(app);Draw(app);AssertCard(app,success,river,false);
            Update(app);Draw(app);AssertCard(app,success,river,true);int fish=g.save.inventory[3],coins=g.save.coins,bounty=g.save.bountyProgress;float energy=g.save.energy;
            ClickRetry(app);Check(g.mode=="fish"&&g.fishHits==0&&g.fishMisses==0&&Near(g.fishTime,0)&&Near(g.fishCursor,0)&&Offer(g)==null,"Fresh retry starts a clean cast and consumes its one offer");
            Check(g.save.inventory[3]==fish&&g.save.coins==coins&&g.save.bountyProgress==bounty&&Near(g.save.energy,energy)&&g.playerAction==null,"Retry click does not settle another catch, spend energy or attack");
            if(river)Check(g.RiverFishingActive&&g.RiverFishingSite==1&&g.RiverPhase==RiverFishingPhase.Waiting,"River retry returns to the identical site's Waiting phase");
            GUI.queuedClick=Retry;Draw(app);Check(GUI.queuedClick.HasValue,"Repeated old retry callback cannot restart the new cast");GUI.queuedClick=null;
            GUI.queuedClick=river?RiverReel:LakeReel;Draw(app);GUI.queuedClick=null;Call(app,"ReelFishingFromInput");Draw(app);
            Check(g.fishHits==0&&g.fishMisses==0,"Repeated OnGUI and Reel callbacks cannot pull the fresh cast in the retry frame");
            if(!river){g.fishTime=0;Update(app,KeyCode.E);Draw(app);Check(g.fishHits==1&&g.fishMisses==0,"Next accepted Update permits the new cast's first pull");}
        }
        River(app);for(int pull=0;pull<2;pull++){Pull(app,true,false,true);Draw(app);}Pull(app,true,false,true);
        Check(app.Game.mode=="world"&&app.Game.fishMisses==3,"Third real no-input timeout finishes the river attempt inside Tick");
        Draw(app);AssertCard(app,false,true,false);Draw(app);AssertCard(app,false,true,false);Update(app);Draw(app);AssertCard(app,false,true,true);
        Console.WriteLine("PASS terminal keyboard/pad/Return aliases and no-input timeout cannot arm new offers, first draw stays disabled, later Update arms, retry resets without cost and repeated GUI/Reel cannot pull in its frame; NATIVE_UNVERIFIED");
    }

    private static void OriginalWorldControlsSafetyAndPointerRouting(FrontierRuntime app)
    {
        Guard(app);
        foreach(bool combat in new[]{false,true})foreach(float y in new[]{610f,653f})
        {
            Finish(app,false,true);Arm(app,true,false);var g=app.Game;Set(app,"combatHudTime",combat?2f:0f);Draw(app);
            Check((bool)app.GetType().GetProperty("CombatHud",All).GetValue(app,null)==combat,"Fixture reaches the actual requested HUD");
            Check((bool)Call(app,"PointerOverInterface",new Point(640,y)),"Entire retry card protects mouse input in both HUD modes");
            float energy=g.save.energy;Input.mousePosition=new Vector3(640,800-y,0);Input.buttonDown.Add(0);Input.buttons.Add(0);Update(app);Draw(app);
            Check(g.playerAction==null&&Near(g.save.energy,energy)&&CanRetry(g),"Mouse press/hold over retry header/button never starts a world attack");
            if(y>638){ClickRetry(app);Check(g.mode=="fish"&&g.fishHits==0,"Protected pointer click still activates the armed retry");}
        }
        foreach(var key in new[]{KeyCode.E,KeyCode.JoystickButton1})
        { Finish(app,false,true);Arm(app,true,false);Update(app,key);Draw(app);Check(app.Game.mode=="dialog"&&Offer(app.Game)==null,"Original "+key+" interaction opens the canonical host dialogue");NoCard(); }
        foreach(var key in new[]{KeyCode.Space,KeyCode.JoystickButton0})
        { Finish(app,false,true);Arm(app,true,false);float energy=app.Game.save.energy;Update(app,key);Draw(app);Check(app.Game.mode=="world"&&app.Game.dashTime>0&&app.Game.save.energy<energy&&Offer(app.Game)==null,"Original "+key+" remains a paid world dodge");NoCard(); }
        Finish(app,false,true);Arm(app,true,false);object original=Offer(app.Game);Update(app,KeyCode.Return);Draw(app);Check(app.Game.mode=="world"&&ReferenceEquals(Offer(app.Game),original)&&app.Game.playerAction==null,"Return keeps its existing world behavior and cannot trigger retry");
        foreach(bool queued in new[]{false,true})
        {
            Finish(app,false,true);Arm(app,true,false);var g=app.Game;if(queued)g.attackCooldown=.1f;Update(app,KeyCode.J);Draw(app);
            Check(queued?Get<object>(g,"bufferedAttack")!=null:g.playerAction!=null,"Real J input creates the intended paid action/queued attack");
            Check(Offer(g)==null&&!CanRetry(g),"Live or queued combat intent hides retry before any click");NoCard();GUI.queuedClick=Retry;Draw(app);Check(GUI.queuedClick.HasValue&&g.mode=="world","Stale GUI click cannot override a paid action/queue");GUI.queuedClick=null;
        }
        Finish(app,false,true);Arm(app,true,false);var game=app.Game;Point origin=game.Position;
        for(int frame=0;frame<8;frame++){Input.axes["Horizontal"]=1;Update(app);}Draw(app);
        Check((game.Position-origin).Length>.2f&&Offer(game)==null,"Real movement beyond the small completion origin retires retry");NoCard();
        game.save.x=origin.x;game.save.y=origin.y;Update(app);Draw(app);Check(Offer(game)==null,"Walking back cannot revive an invalidated offer");NoCard();
        Console.WriteLine("PASS both HUDs protect retry header/button from mouse attacks, E/pad B interaction and Space/pad A dodge retain world behavior, Return does not retry, and paid actions/queues/movement reject stale clicks; NATIVE_UNVERIFIED");
    }

    private static void CancellationMenusPauseExpiryAndReload(FrontierRuntime app)
    {
        Guard(app);
        foreach(bool river in new[]{false,true})foreach(var key in new[]{KeyCode.Escape,KeyCode.JoystickButton1})
        { if(river)River(app);else Lake(app);Pull(app,river,true,false,KeyCode.E);Draw(app);int fish=app.Game.save.inventory[3],coins=app.Game.save.coins;Update(app,key);Draw(app);Check(app.Game.mode=="world"&&Offer(app.Game)==null&&app.Game.save.inventory[3]==fish&&app.Game.save.coins==coins,"Partial-cast "+key+" cancellation never creates retry or rewards");NoCard(); }
        foreach(var key in new[]{KeyCode.Escape,KeyCode.I,KeyCode.Tab,KeyCode.JoystickButton7})
        { Finish(app,false,true);Arm(app,true,false);Update(app,key);Draw(app);Check(app.Game.mode!="world"&&Offer(app.Game)==null,"Opening real "+key+" world menu clears retry");NoCard();Update(app,KeyCode.Escape);Draw(app);Check(app.Game.mode=="world"&&Offer(app.Game)==null,"Closing the menu cannot revive retry");NoCard(); }
        foreach(bool review in new[]{false,true})
        {
            Finish(app,false,true);Arm(app,true,false);var g=app.Game;object offer=Offer(g);string before=State(app);
            if(review)app.VisualReviewPaused=true;else Call(app,"OnApplicationPause",true);
            for(int frame=0;frame<3;frame++){Update(app);Draw(app);NoCard();}
            Check(State(app)==before&&ReferenceEquals(Offer(g),offer),"Application/review pause hides the shortcut and freezes save, offer and clocks");
            app.VisualReviewPaused=false;if(!review)Call(app,"OnApplicationPause",false);Draw(app);AssertCard(app,true,false,false);
            GUI.queuedClick=Retry;Draw(app);Check(GUI.queuedClick.HasValue&&g.mode=="world"&&ReferenceEquals(Offer(g),offer),"A resumed card rejects the old click until a fresh Update rearms it");GUI.queuedClick=null;
            Update(app);Draw(app);AssertCard(app,true,false,true);
        }
        Finish(app,false,true);Arm(app,true,false);var expiryGame=app.Game;
        while(Remaining(expiryGame)>.04f)Update(app);
        Draw(app);AssertCard(app,true,false,true);float expiryEnergy=expiryGame.save.energy;
        Time.unscaledDeltaTime=.05f;Input.mousePosition=new Vector3(640,800-653,0);Input.buttonDown.Add(0);Input.buttons.Add(0);Update(app);Draw(app);Time.unscaledDeltaTime=.02f;
        Check(Offer(expiryGame)==null&&expiryGame.mode=="world"&&expiryGame.playerAction==null&&Near(expiryGame.save.energy,expiryEnergy),"Clicking the last visible retry on its expiry frame must not pay for a world attack");NoCard();
        Finish(app,false,false);Arm(app,false,false);var game=app.Game;float duration=(float)game.GetType().GetField("FishingRetryDuration",BindingFlags.Public|BindingFlags.Static).GetValue(null);
        Check(Near(duration,8),"Offer has the bounded eight-second world lifetime");float world=game.save.totalTime;
        for(int frame=0;frame<390;frame++)Update(app);Draw(app);AssertCard(app,false,false,true);
        for(int frame=0;frame<12;frame++)Update(app);Draw(app);Check(Offer(game)==null&&Near(Remaining(game),0)&&game.save.totalTime-world>=8,"Accepted world Update clock expires retry after eight seconds");NoCard();
        Finish(app,true,true);Arm(app,true,true);game=app.Game;int savedFish=game.save.inventory[3],savedCoins=game.save.coins;GameCore originalGame=Get<GameCore>(app,"reviewOriginal");
        try { Set(app,"reviewOriginal",null);Set(app,"saveTimer",13f);Update(app);Check(File.Exists(SaveStore.PathName)&&Near(Get<float>(app,"saveTimer"),0)&&Offer(game)!=null,"Real Update autosave writes the settled save without consuming retry"); }
        finally {Set(app,"reviewOriginal",originalGame);}
        string warning;SaveData saved=SaveStore.Read(out warning);Check(saved!=null&&warning==null,"Actual checksum reader accepts settled-catch autosave");string raw=JsonUtility.ToJson(saved);
        Check(!raw.Contains("FishingRetry")&&!raw.Contains("fishingRetry"),"Retry adds no persistent save fields");game.Load(saved);game.Start();game.enemies.Clear();Draw(app);
        Check(Offer(game)==null&&!CanRetry(game)&&game.save.inventory[3]==savedFish&&game.save.coins==savedCoins&&game.RiverLessonRecorded(0),"Real Load/Start drops transient retry and retains the original one-time catch/site settlement");NoCard();
        Console.WriteLine("PASS lake/river cancellation creates no retry, real menus clear it, application/review pause hides and freezes it, world-clock expiry is eight seconds, and real autosave/checksummed reload preserves rewards without a retry field; NATIVE_UNVERIFIED");
    }

    public static int Main(string[] args)
    {
        CultureInfo.CurrentCulture=CultureInfo.InvariantCulture;captureFolder=args.Length>0&&args[0]!="-"?args[0]:null;capturePrefix=args.Length>1?args[1]:"qa-fishing-retry";
        string previous=Application.persistentDataPath,directory=Path.Combine(Path.GetTempPath(),"starfall-fishing-retry-check-"+Guid.NewGuid());Application.persistentDataPath=directory;
        int failed=0;var app=new FrontierRuntime();
        try
        {
            Call(app,"Awake");Check(app.Game!=null,"Actual Awake loads the production campaign");
            foreach(Action<FrontierRuntime> test in new Action<FrontierRuntime>[]{LocalizedSettledCardsAndReadOnlyDrawing,TerminalFrameArmingAndRetryInputIsolation,OriginalWorldControlsSafetyAndPointerRouting,CancellationMenusPauseExpiryAndReload})
                try{test(app);}catch(Exception error){failed++;Console.WriteLine("FAIL "+test.Method.Name+": "+(error.InnerException??error).Message);}
            app.EndVisualReview();Call(app,"OnDestroy");
        }
        finally{Application.persistentDataPath=previous;Input.Clear();GUI.queuedClick=null;if(Directory.Exists(directory))Directory.Delete(directory,true);}
        Console.WriteLine("Runtime fishing retry: "+(4-failed)+"/4 groups passed; NATIVE_UNVERIFIED");return failed==0?0:1;
    }
}
