// Actual shipped Update -> OnGUI fishing receipts under offline Unity declarations.
// Native import, GPU/font rendering, hardware input and gameplay acceptance are NATIVE_UNVERIFIED.
using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using Starfall;
using UnityEngine;

public static class RuntimeFishingFeedbackChecks
{
    private const BindingFlags All = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static readonly Rect LakeReel = new Rect(473,601,543,56), RiverReel = new Rect(473,611,543,52);
    private static string captureFolder, capturePrefix;
    private sealed class Label { public string text = null; public float[] r = null, color = null; public int size = 0; public bool center = false; }
    private static void Check(bool value,string message) { if(!value)throw new Exception(message); }
    private static bool Near(float a,float b) { return Math.Abs(a-b)<.0002f; }
    private static object Call(object target,string name,params object[] args)
    { var method=target.GetType().GetMethod(name,All);Check(method!=null,"Production entry point exists: "+name);return method.Invoke(target,args); }
    private static void Set(object target,string name,object value) { target.GetType().GetField(name,All).SetValue(target,value); }
    private static T Get<T>(object target,string name) { return (T)target.GetType().GetField(name,All).GetValue(target); }
    // Reflection keeps this harness compilable against the immutable pre-feature
    // baseline; the first assertion in every group is the actual missing HUD.
    private static object Receipt(GameCore g)
    { var property=g.GetType().GetProperty("RecentFishingPull");Check(property!=null,"Authoritative transient fishing receipt exists");return property.GetValue(g,null); }
    private static float Remaining(GameCore g)
    { var property=g.GetType().GetProperty("FishingPullFeedbackRemaining");Check(property!=null,"Authoritative receipt clock exists");return (float)property.GetValue(g,null); }
    private static string ReceiptState(object value)
    { return value==null?"none":Get<object>(value,"outcome")+"/"+Get<float>(value,"cursor")+"/"+Get<float>(value,"lower")+"/"+Get<float>(value,"upper")+"/"+Get<int>(value,"goodPulls")+"/"+Get<int>(value,"misses")+"/"+Get<bool>(value,"river"); }
    private static string State(FrontierRuntime app)
    { var g=app.Game;return JsonUtility.ToJson(g.save)+"|"+g.revision+"|"+g.fishHits+"|"+g.fishMisses+"|"+g.fishTime+"|"+g.fishCursor+"|"+Remaining(g)+"|"+Get<float>(app,"clock")+"|"+Get<float>(app,"saveTimer"); }
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
    private static bool ButtonExists(Rect rect,bool enabled)
    { return GUI.buttonCalls.Exists(b=>Near(b.rect.x,rect.x)&&Near(b.rect.y,rect.y)&&Near(b.rect.width,rect.width)&&Near(b.rect.height,rect.height)&&b.enabled==enabled); }
    private static void Click(FrontierRuntime app,Rect rect)
    { Check(ButtonExists(rect,true),"Original Reel remains enabled at its existing bounds");GUI.queuedClick=rect;Draw(app);Check(!GUI.queuedClick.HasValue,"Actual OnGUI consumes original Reel click"); }
    private static string Expected(GameCore g,string outcome)
    {
        return outcome=="Good"?g.L("收线成功 · 成功 ","Good pull · ")+g.fishHits+"/3":
            outcome=="Left"?g.L("收线在左侧 · 失误 ","Left of band · miss ")+g.fishMisses+"/3":
            outcome=="Right"?g.L("收线在右侧 · 失误 ","Right of band · miss ")+g.fishMisses+"/3":
            g.L("错过收线 · 失误 ","Window missed · miss ")+g.fishMisses+"/3";
    }
    private static Label AssertLabel(FrontierRuntime app,string outcome)
    {
        string expected=Expected(app.Game,outcome),command=GUI.commands.Find(c=>c.Contains("\"text\":"+GUI.S(expected)));
        Check(command!=null,"Actual fishing OnGUI shows accepted "+outcome+" feedback: "+expected);
        var label=JsonUtility.FromJson<Label>(command);
        Check(Near(label.r[0],321)&&Near(label.r[1],665)&&Near(label.r[2],849)&&Near(label.r[3],22),"Feedback uses the reserved text bounds below the original Reel");
        Check(label.size==(app.Game.save.largeText?15:14)&&label.center,"Localized feedback honors supporting-text size and centered alignment");
        return label;
    }
    private static void AssertNoLabel()
    { Check(!GUI.commands.Exists(c=>c.StartsWith("{\"k\":\"text\"",StringComparison.Ordinal)&&c.Contains("\"r\":[321,665,849,22]")),"No accepted receipt leaves the feedback region empty"); }
    private static object AssertFeedback(FrontierRuntime app,string outcome,bool river)
    {
        Label label=AssertLabel(app,outcome); // Semantic RED precedes all new API access.
        var g=app.Game;object receipt=Receipt(g);Check(receipt!=null,"Accepted pull exposes one live receipt");
        Check(Get<object>(receipt,"outcome").ToString()==outcome&&Get<int>(receipt,"goodPulls")==g.fishHits&&Get<int>(receipt,"misses")==g.fishMisses&&Get<bool>(receipt,"river")==river,"Visible feedback agrees with immutable authoritative outcome and counters");
        foreach(string field in new[]{"outcome","cursor","lower","upper","goodPulls","misses","river"})Check(receipt.GetType().GetField(field).IsInitOnly,"Receipt field is immutable: "+field);
        float target=river?g.RiverFishingTarget:.5f,half=river?GameCore.RiverFishingHalfWidth:.18f;
        Check(Near(Get<float>(receipt,"lower"),target-half)&&Near(Get<float>(receipt,"upper"),target+half),"Receipt captures the painted lake/site-specific river band");
        Check(Remaining(g)>0&&Remaining(g)<=1.2001f,"Accepted feedback has a bounded 1.2 second lifetime");
        float x=335+826*Get<float>(receipt,"cursor");Color color=Get<Color>(app,outcome=="Good"?"mint":"gold");
        Check(Near(label.color[0],color.r)&&Near(label.color[1],color.g)&&Near(label.color[2],color.b),"Good and missed feedback use the existing readable mint/gold palette");
        Check(GUI.textureCalls.Exists(c=>Near(c.rect.x,x-5)&&Near(c.rect.y,477)&&Near(c.rect.width,10)&&Near(c.rect.height,3)&&Near(c.color.r,color.r)&&Near(c.color.g,color.g)&&Near(c.color.b,color.b))&&
              GUI.textureCalls.Exists(c=>Near(c.rect.x,x-1)&&Near(c.rect.y,480)&&Near(c.rect.width,2)&&Near(c.rect.height,4)&&Near(c.color.r,color.r)&&Near(c.color.g,color.g)&&Near(c.color.b,color.b)),"Small last-pull marker remains at the accepted cursor below the live bar");
        Check(GUI.textureCalls.Exists(c=>Near(c.rect.x,335)&&Near(c.rect.y,436)&&Near(c.rect.width,826)&&Near(c.rect.height,37)),"Original timing bar bounds remain unchanged");
        Check(ButtonExists(river?RiverReel:LakeReel,!river||g.RiverPhase==RiverFishingPhase.Reeling),"Original lake/river Reel retains its exact bounds and phase availability");
        Check(!GUI.buttonCalls.Exists(b=>b.rect.y>=665&&b.rect.y<687),"Feedback adds no input control over its label");return receipt;
    }
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
        var g=app.Game;g.enemies.Clear();g.fallen.Clear();g.effects.Clear();g.hazards.Clear();g.projectiles.Clear();
        g.save.companion=-1;g.save.english=true;g.save.largeText=false;g.save.reducedMotion=true;g.save.effectsQuality=0;g.save.bounty=2;g.save.bountyProgress=0;
    }
    private static void Lake(FrontierRuntime app)
    { Prepare(app);Beside(app,"fish");Update(app,KeyCode.E);Choose(app,"fish");Check(app.Game.mode=="fish"&&!app.Game.RiverFishingActive,"Original lake dialogue starts lake fishing");Draw(app); }
    private static void FocusReel(FrontierRuntime app)
    {
        string reel="473:601:543:56";int tabs=0;
        while(Get<MenuNavigation>(app,"navigation").focused!=reel&&tabs++<30){Update(app,KeyCode.Tab);Draw(app);}
        Check(Get<MenuNavigation>(app,"navigation").focused==reel,"Real Tab navigation focuses existing lake Reel");
    }
    private static void LakePull(FrontierRuntime app,string outcome,params KeyCode[] keys)
    {
        var g=app.Game;float rate=2.5f+g.fishHits*.35f;
        // Pin only the pre-existing sine clock; production Update computes the
        // cursor and owns input acceptance, score, receipt and GUI ordering.
        g.fishTime=outcome=="Good"?0:(float)(outcome=="Left"?Math.PI*1.5:Math.PI*.5)/rate-Time.unscaledDeltaTime;
        Update(app,keys);Draw(app);
    }
    private static void River(FrontierRuntime app,int site=0)
    {
        Prepare(app);var g=app.Game;g.save.flags.Add("ally_2");Beside(app,"fish");Update(app,KeyCode.E);Choose(app,"river_offer");Choose(app,"river_accept");Choose(app,"close");
        Check(g.RiverLessonAccepted,"Actual numeric acceptance unlocks river lesson");g.Enter(11);g.enemies.Clear();BeginRiver(app,site);
    }
    private static void BeginRiver(FrontierRuntime app,int site)
    { Beside(app,"riverFishing"+site);Update(app,KeyCode.E);Choose(app,"river_cast_"+site);Check(app.Game.RiverFishingActive&&app.Game.RiverPhase==RiverFishingPhase.Waiting,"Real site dialogue starts Waiting");Draw(app); }
    private static void WaitPhase(FrontierRuntime app,RiverFishingPhase phase)
    { int frames=0;while(app.Game.RiverFishingActive&&app.Game.RiverPhase!=phase&&frames++<260)Update(app);Check(app.Game.RiverFishingActive&&app.Game.RiverPhase==phase,"Accepted Update clock reaches "+phase); }
    private static void RiverPull(FrontierRuntime app,string outcome)
    {
        WaitPhase(app,RiverFishingPhase.Reeling);var g=app.Game;
        if(outcome=="Timeout")WaitPhase(app,RiverFishingPhase.Resting);
        else
        {
            float cursor=outcome=="Good"?g.RiverFishingTarget-.02f:outcome=="Right"?g.RiverFishingTarget+GameCore.RiverFishingHalfWidth+.08f:0;
            int frames=0;while(g.RiverPhase==RiverFishingPhase.Reeling&&g.fishCursor<cursor&&frames++<160)Update(app);
            Check(g.RiverPhase==RiverFishingPhase.Reeling,"Actual moving float reaches requested pull timing");Update(app,KeyCode.E);
        }
        Draw(app);
    }
    private static void Guard(FrontierRuntime app)
    { Lake(app);LakePull(app,"Right",KeyCode.E);AssertFeedback(app,"Right",false); }

    private static void LocalizedHudMarkerAndReadOnlyDrawing(FrontierRuntime app)
    {
        foreach(bool english in new[]{false,true})foreach(bool large in new[]{false,true})foreach(bool river in new[]{false,true})
        foreach(string outcome in river?new[]{"Left","Good","Right","Timeout"}:new[]{"Left","Good","Right"})
        {
            if(river)River(app,outcome=="Right"?1:0);else Lake(app);
            var g=app.Game;g.save.english=english;g.save.largeText=large;Draw(app);AssertNoLabel();
            if(river)RiverPull(app,outcome);else LakePull(app,outcome,KeyCode.E);
            object receipt=AssertFeedback(app,outcome,river);string receiptBefore=ReceiptState(receipt),before=State(app);
            foreach(EventType type in new[]{EventType.Layout,EventType.Repaint,EventType.Repaint})
            { Event.current.type=type;Draw(app);AssertFeedback(app,outcome,river);Check(State(app)==before&&ReferenceEquals(Receipt(g),receipt)&&ReceiptState(receipt)==receiptBefore,"Repeated actual Layout/Repaint preserves complete save, scores, clocks and immutable receipt identity"); }
            Draw(app,(river?"river":"lake")+"-"+outcome.ToLowerInvariant()+"-"+(english?"en":"zh")+(large?"-large":"-normal"));
            if(!river)
            { float accepted=Get<float>(receipt,"cursor");for(int frame=0;frame<8;frame++)Update(app);Draw(app);AssertFeedback(app,outcome,false);Check(!Near(g.fishCursor,accepted)&&ReceiptState(receipt)==receiptBefore,"Live lake float continues while accepted last-pull marker stays fixed"); }
        }
        Console.WriteLine("PASS 28 bilingual normal/large actual lake/river outcome HUD states, site bands, saved-cursor marker, original controls and read-only Layout/Repaint; NATIVE_UNVERIFIED");
    }

    private static void ActualInputsAndSameFrameReceiptCoalescing(FrontierRuntime app)
    {
        Guard(app);
        foreach(var key in new[]{KeyCode.E,KeyCode.Space,KeyCode.JoystickButton0,KeyCode.Return})
        {
            Lake(app);if(key==KeyCode.Return)FocusReel(app);LakePull(app,"Good",key);
            object receipt=AssertFeedback(app,"Good",false);Check(app.Game.fishHits==1&&app.Game.fishMisses==0,"Fresh "+key+" accepts one production pull and receipt");
            Click(app,LakeReel);Draw(app);Check(ReferenceEquals(Receipt(app.Game),receipt)&&app.Game.fishHits==1,"Repeated same-frame Reel does not replace "+key+" receipt");
        }
        Lake(app);Input.buttonDown.Add(0);LakePull(app,"Good");Check(app.Game.fishHits==0,"Mouse Update waits for original OnGUI Reel");Click(app,LakeReel);object mouse=AssertFeedback(app,"Good",false);Click(app,LakeReel);Check(ReferenceEquals(Receipt(app.Game),mouse)&&app.Game.fishHits==1,"Repeated mouse GUI activation keeps one accepted receipt");
        KeyCode[][] aliases={new[]{KeyCode.E,KeyCode.Space},new[]{KeyCode.JoystickButton0,KeyCode.E},new[]{KeyCode.E,KeyCode.Space,KeyCode.JoystickButton0,KeyCode.Return}};
        foreach(string outcome in new[]{"Good","Left"})foreach(var keys in aliases)
        {
            Lake(app);var g=app.Game;int fish=g.save.inventory[3],coins=g.save.coins;LakePull(app,outcome,keys);object receipt=AssertFeedback(app,outcome,false);float remaining=Remaining(g);
            Click(app,LakeReel);Click(app,LakeReel);Check(ReferenceEquals(Receipt(g),receipt)&&Near(Remaining(g),remaining)&&g.fishHits+g.fishMisses==1,"Keyboard/pad/Return aliases and repeated actual GUI buttons leave exactly one accepted receipt");
            LakePull(app,outcome,keys);Check(!ReferenceEquals(Receipt(g),receipt)&&g.fishHits+g.fishMisses==2&&g.mode=="fish","Fresh Update produces exactly the second receipt without extra cooldown");AssertFeedback(app,outcome,false);
            LakePull(app,outcome,keys);Check(g.mode=="world"&&Receipt(g)==null&&Near(Remaining(g),0),"Third distinct pull ends the cast and clears transient feedback");
            Check(g.save.inventory[3]==fish+(outcome=="Good"?1:0)&&g.save.coins==coins+(outcome=="Good"?4:0)&&g.save.bountyProgress==(outcome=="Good"?1:0),"Receipt coalescing preserves exact three-pull fish/coins/bounty settlement");
        }
        Console.WriteLine("PASS real E/Space/pad A/focused Return/mouse Update-to-OnGUI receipt routes, alias coalescing, fresh-frame replacement and exact terminal settlement; NATIVE_UNVERIFIED");
    }

    private static void PausedExpiryAndIgnoredRiverPhases(FrontierRuntime app)
    {
        Guard(app);Lake(app);LakePull(app,"Left",KeyCode.E);object receipt=AssertFeedback(app,"Left",false);string before=State(app);
        foreach(bool review in new[]{false,true})
        {
            if(review)app.VisualReviewPaused=true;else Set(app,"applicationPaused",true);
            for(int frame=0;frame<4;frame++){Update(app,KeyCode.E,KeyCode.Space,KeyCode.JoystickButton0,KeyCode.Return);Draw(app);Click(app,LakeReel);AssertFeedback(app,"Left",false);}
            Check(State(app)==before&&ReferenceEquals(Receipt(app.Game),receipt),"Application/review pause freezes receipt, all clocks, cursor and input acceptance while retaining visible feedback");
            app.VisualReviewPaused=false;Set(app,"applicationPaused",false);
        }
        float world=app.Game.save.totalTime;for(int frame=0;frame<61;frame++)Update(app);Draw(app);
        Check(Receipt(app.Game)==null&&Near(Remaining(app.Game),0)&&Near(world,app.Game.save.totalTime),"Accepted Update expires 1.2-second lake feedback while world time remains paused");AssertNoLabel();
        River(app);var g=app.Game;world=g.save.totalTime;Update(app,KeyCode.E,KeyCode.Space,KeyCode.JoystickButton0,KeyCode.Return);Draw(app);
        Check(Receipt(g)==null&&g.fishHits==0&&g.fishMisses==0&&g.RiverPhase==RiverFishingPhase.Waiting,"Ignored Waiting aliases invent no receipt or score");AssertNoLabel();
        RiverPull(app,"Good");receipt=AssertFeedback(app,"Good",true);float age=Remaining(g);string captured=ReceiptState(receipt);
        for(int frame=0;frame<5;frame++)
        { Update(app,KeyCode.E,KeyCode.Space,KeyCode.JoystickButton0,KeyCode.Return);Draw(app);GUI.queuedClick=RiverReel;Draw(app);Check(GUI.queuedClick.HasValue,"Disabled resting Reel consumes no mouse click");GUI.queuedClick=null; }
        Check(ReferenceEquals(Receipt(g),receipt)&&ReceiptState(receipt)==captured&&g.fishHits==1&&g.fishMisses==0&&Near(Remaining(g),age-.1f),"Ignored Resting aliases/buttons preserve original receipt and never extend its clock");
        for(int frame=0;frame<61;frame++)Update(app);Draw(app);Check(Receipt(g)==null&&Near(Remaining(g),0),"River receipt expires across rest/wait phase transitions");AssertNoLabel();
        RiverPull(app,"Timeout");receipt=AssertFeedback(app,"Timeout",true);
        Check(g.fishHits==1&&g.fishMisses==1&&Near(Get<float>(receipt,"cursor"),1)&&Near(world,g.save.totalTime),"Actual untouched reeling window records one timeout at the terminal cursor without advancing world time");
        Console.WriteLine("PASS application/review freeze and visible paused receipt, accepted-clock expiry, world pause, ignored river wait/rest input and actual no-input timeout; NATIVE_UNVERIFIED");
    }

    private static void BackRecastAutosaveAndReload(FrontierRuntime app)
    {
        Guard(app);
        foreach(var key in new[]{KeyCode.Escape,KeyCode.JoystickButton1})
        {
            River(app);RiverPull(app,"Left");AssertFeedback(app,"Left",true);var g=app.Game;int fish=g.save.inventory[3],coins=g.save.coins;
            Update(app,key,KeyCode.E,KeyCode.Space,KeyCode.JoystickButton0,KeyCode.Return);Draw(app);
            Check(g.mode=="world"&&!g.RiverFishingActive&&Receipt(g)==null&&Near(Remaining(g),0),"Actual "+key+" cancellation wins over aliases and clears receipt");AssertNoLabel();
            Check(g.save.inventory[3]==fish&&g.save.coins==coins&&!g.RiverLessonRecorded(0),"Cancelled partial feedback grants no catch or lesson progress");
            BeginRiver(app,0);Check(Receipt(g)==null&&g.fishHits==0&&g.fishMisses==0,"Real recast starts with no previous receipt or counters");AssertNoLabel();
        }
        River(app);RiverPull(app,"Good");var game=app.Game;object receipt=AssertFeedback(app,"Good",true);int savedFish=game.save.inventory[3],savedCoins=game.save.coins;
        GameCore original=Get<GameCore>(app,"reviewOriginal");
        try
        {
            Set(app,"reviewOriginal",null);Set(app,"saveTimer",13f);Update(app);
            Check(File.Exists(SaveStore.PathName)&&Near(Get<float>(app,"saveTimer"),0)&&ReferenceEquals(Receipt(game),receipt)&&game.fishHits==1,"Real Update autosave writes a partial cast without replacing or settling its live receipt");
        }
        finally { Set(app,"reviewOriginal",original); }
        string warning;SaveData saved=SaveStore.Read(out warning);Check(saved!=null&&warning==null,"Actual checksum reader accepts partial-cast autosave");
        string raw=JsonUtility.ToJson(saved);Check(!raw.Contains("FishingPull")&&!raw.Contains("fishingPull")&&!raw.Contains("goodPulls")&&!raw.Contains("fishCursor"),"Transient outcome/cursor/receipt adds no persistent save fields");
        game.Load(saved);game.Start();game.enemies.Clear();Draw(app);
        Check(Receipt(game)==null&&Near(Remaining(game),0)&&!game.RiverFishingActive&&game.save.inventory[3]==savedFish&&game.save.coins==savedCoins&&game.save.bountyProgress==0,"Actual Load/Start drops partial cast and feedback without duplicate catch or bounty");AssertNoLabel();
        Lake(app);LakePull(app,"Right",KeyCode.E);AssertFeedback(app,"Right",false);Update(app,KeyCode.Escape);Draw(app);Check(Receipt(app.Game)==null,"Lake Back also clears receipt");
        Beside(app,"fish");Update(app,KeyCode.E);Choose(app,"fish");Draw(app);Check(Receipt(app.Game)==null&&app.Game.fishHits==0&&app.Game.fishMisses==0,"Original lake dialogue recast cannot revive last attempt feedback");AssertNoLabel();
        Console.WriteLine("PASS actual Escape/pad B priority, lake/river recast clearing, unchanged cancellation resources, real Update autosave/checksummed SaveStore and transient-free reload; NATIVE_UNVERIFIED");
    }

    public static int Main(string[] args)
    {
        CultureInfo.CurrentCulture=CultureInfo.InvariantCulture;
        captureFolder=args.Length>0&&args[0]!="-"?args[0]:null;capturePrefix=args.Length>1?args[1]:"qa-fishing-feedback";
        string previous=Application.persistentDataPath,directory=Path.Combine(Path.GetTempPath(),"starfall-fishing-feedback-check-"+Guid.NewGuid());Application.persistentDataPath=directory;
        int failed=0;var app=new FrontierRuntime();
        try
        {
            Call(app,"Awake");Check(app.Game!=null,"Actual Awake loads production campaign");
            foreach(Action<FrontierRuntime> test in new Action<FrontierRuntime>[]{LocalizedHudMarkerAndReadOnlyDrawing,ActualInputsAndSameFrameReceiptCoalescing,PausedExpiryAndIgnoredRiverPhases,BackRecastAutosaveAndReload})
                try {test(app);}catch(Exception error){failed++;Console.WriteLine("FAIL "+test.Method.Name+": "+(error.InnerException??error).Message);}
            app.EndVisualReview();Call(app,"OnDestroy");
        }
        finally {Application.persistentDataPath=previous;Input.Clear();GUI.queuedClick=null;if(Directory.Exists(directory))Directory.Delete(directory,true);}
        Console.WriteLine("Runtime fishing feedback: "+(4-failed)+"/4 groups passed; NATIVE_UNVERIFIED");return failed==0?0:1;
    }
}
