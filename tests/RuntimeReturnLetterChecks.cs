// Production Update, E/numbered dialogue, OnGUI and SaveStore paths with offline
// Unity declarations. Staged earned archives are not a claim of native play.
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Starfall;
using UnityEngine;

public static class RuntimeReturnLetterChecks
{
    private const BindingFlags All = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static string captureFolder;
    private static int captures;
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private static bool Near(float a, float b, float tolerance = .006f) { return Math.Abs(a-b) <= tolerance; }
    private static object Call(object obj, string name, params object[] args)
    { var method=obj.GetType().GetMethod(name,All); Check(method!=null,"Production entry point exists: "+name); return method.Invoke(obj,args); }
    private static void Set(object obj,string name,object value)
    { var field=obj.GetType().GetField(name,All);Check(field!=null,"Fixture field exists: "+name);field.SetValue(obj,value); }
    private static T Get<T>(object obj,string name) { return (T)obj.GetType().GetField(name,All).GetValue(obj); }
    private static T Property<T>(object obj,string name)
    { var property=obj.GetType().GetProperty(name,All);Check(property!=null,"Shipped reply behavior exists: "+name);return (T)property.GetValue(obj,null); }
    private static bool Active(GameCore g) { return Property<bool>(g,"ReturnLetterSignalActive"); }
    private static bool Confirming(GameCore g) { return Property<bool>(g,"ReturnLetterSignalConfirming"); }
    private static float Remaining(GameCore g) { return Property<float>(g,"ReturnLetterSignalRemaining"); }
    private static bool Same(Rect a,Rect b) { return Near(a.x,b.x)&&Near(a.y,b.y)&&Near(a.width,b.width)&&Near(a.height,b.height); }
    private static bool Contains(Rect r,Point p) { return p.x>=r.x&&p.x<r.x+r.width&&p.y>=r.y&&p.y<r.y+r.height; }
    private static bool Overlap(Rect a,Rect b) { return a.x<b.x+b.width&&b.x<a.x+a.width&&a.y<b.y+b.height&&b.y<a.y+a.height; }
    private static void Update(FrontierRuntime app) { Call(app,"Update");Input.keys.Clear(); }
    private static void Frames(FrontierRuntime app,int count) { for(int i=0;i<count;i++)Update(app); }
    private static void Key(FrontierRuntime app,KeyCode key) { Input.keys.Add(key);Update(app); }
    private static void Draw(FrontierRuntime app,string capture=null)
    {
        GUI.commands.Clear();GUI.textureCalls.Clear();GUI.buttonCalls.Clear();Call(app,"OnGUI");
        if(captureFolder!=null&&capture!=null)
        {
            var commands=GUI.commands.ToArray();var textures=GUI.textureCalls.ToArray();var buttons=GUI.buttonCalls.ToArray();
            GUI.Export(captureFolder,"qa-return-letter-"+capture);captures++;
            GUI.commands.AddRange(commands);GUI.textureCalls.AddRange(textures);GUI.buttonCalls.AddRange(buttons);
        }
    }
    private static bool Text(string text) { return GUI.commands.Exists(c=>c.Contains("\"k\":\"text\"")&&c.Contains(text)); }
    private static void Pointer(FrontierRuntime app,Point point)
    { float scale=Get<float>(app,"scale");Input.mousePosition=new Vector3(Get<float>(app,"offsetX")+point.x*scale,Screen.height-Get<float>(app,"offsetY")-point.y*scale,0); }
    private static WorldObject Object(GameCore g,string kind)
    { var obj=Array.Find(g.Map.objects,o=>o.kind==kind);Check(obj!=null,"The mature authored object exists: "+kind);return obj; }
    private static void Position(FrontierRuntime app,Point point)
    { Check(app.SetVisualReviewPosition(point),"Staged positioning uses actual walkable ground");Set(app,"camera",app.Game.Position); }
    private static void Beside(FrontierRuntime app,string kind)
    { var obj=Object(app.Game,kind);Position(app,new Point(obj.x,obj.y-.7f));Check(ReferenceEquals(app.Game.Nearest(),obj),"Real E targets the nearest authored "+kind); }
    private static void Interact(FrontierRuntime app,string kind,bool pad=false)
    { Beside(app,kind);Key(app,pad?KeyCode.JoystickButton1:KeyCode.E);Check(app.Game.mode=="dialog","Real E/B opens the shipped "+kind+" conversation"); }
    private static void ReadPages(FrontierRuntime app,string capture=null)
    {
        Check(app.Game.mode=="dialog"&&app.Game.conversation!=null,"Actual dialogue owns its reading pages");int page=0;
        do { Draw(app,capture==null?null:capture+"-page"+page);if((bool)Call(app,"DialogueChoicesVisible"))break;Key(app,KeyCode.Return);page++; } while(page<30);
        Check((bool)Call(app,"DialogueChoicesVisible"),"Every actual narrative page can be read before numbered selection");
    }
    private static void Choose(FrontierRuntime app,string action,string capture=null)
    {
        Check(app.Game.mode=="dialog"&&app.Game.conversation!=null,"Numbered choice "+action+" belongs to an actual open conversation");
        int index=Array.FindIndex(app.Game.conversation.choices,c=>c.action==action);
        Check(index>=0&&index<9,"The actual dialogue exposes numbered choice "+action);ReadPages(app,capture);Key(app,(KeyCode)((int)KeyCode.Alpha1+index));
        if(action=="returnletter_signal_menu")Check(app.Game.mode=="dialog", "Signal menu opened: mode="+app.Game.mode+", pos="+app.Game.Position.x+","+app.Game.Position.y+", checked="+Property<bool>(app.Game,"ReturnLetterChecked")+", repaired="+app.Game.Has("relay_2")+", world="+Property<bool>(app.Game,"ReturnLetterWorldAction")+", near="+Call(app.Game,"ReturnLetterNear",5,"relay2",1.75f));
    }
    private static void Prefix(GameCore g,params string[] actions)
    { Check(g.conversation!=null&&g.conversation.choices.Length>=actions.Length,"Mature dialogue choices remain present");for(int i=0;i<actions.Length;i++)Check(g.conversation.choices[i].action==actions[i],"Mature numbered choice is preserved: "+actions[i]); }
    private static void Click(FrontierRuntime app,Rect rect)
    { Draw(app);Check(GUI.buttonCalls.Exists(b=>Same(b.rect,rect)&&b.enabled),"Actual map control is visibly enabled");GUI.queuedClick=rect;Draw(app);Check(!GUI.queuedClick.HasValue,"Production OnGUI consumes the visible map control"); }
    private static void Travel(FrontierRuntime app,int room)
    { Key(app,KeyCode.M);Set(app,"selectedRegion",room);Click(app,new Rect(888,616,306,47));Check(app.Game.mode=="world"&&app.Game.save.room==room,"Ordinary map input travels to region "+room);Set(app,"camera",app.Game.Position); }
    private static void Prepare(FrontierRuntime app,bool family=true,bool english=true,bool large=false,int profession=0)
    {
        Input.Clear();GUI.queuedClick=null;GUI.matrix=Matrix4x4.identity;GUI.color=Color.white;GUI.enabled=true;
        Screen.width=1280;Screen.height=800;Time.unscaledDeltaTime=.02f;Event.current.type=EventType.Repaint;
        app.BeginVisualReview(profession,0);app.VisualReviewPaused=false;var g=app.Game;
        g.enemies.Clear();g.fallen.Clear();g.hazards.Clear();g.effects.Clear();g.projectiles.Clear();
        g.save.flags.RemoveAll(f=>f.StartsWith("returnletter_")||f=="archive_family"||f=="archive_public"||f.StartsWith("rescue_")||f.StartsWith("relay_"));
        foreach(string flag in new[]{"intro","archive_accept","archive_drain","archive_vent","archive_wave","archive_record_0","archive_record_1","archive_record_2","archive_defeated_74000","archive_defeated_74001","archive_defeated_74002","archive_complete","harbor_report",family?"archive_family":"archive_public"})if(!g.Has(flag))g.save.flags.Add(flag);
        g.save.english=english;g.save.largeText=large;g.save.showGuidance=false;g.save.reducedMotion=true;g.save.effectsQuality=0;g.save.companion=-1;
        g.save.inventory[0]=20;g.save.inventory[2]=20;g.save.inventory[4]=20;g.save.tea=7;g.save.reputation[1]=2;g.save.tideHeldUntil=g.save.totalTime+900;
        Set(app,"applicationPaused",false);Set(app,"pointerSeen",false);Set(app,"controllerUsed",false);Set(app,"resumeTime",0f);Set(app,"arrivalTime",0f);Set(app,"combatHudTime",0f);Call(app,"Resize");Pointer(app,new Point(900,390));
        Check(Property<bool>(g,"ReturnLetterReady")&&!Property<bool>(g,"ReturnLetterAccepted"),"Earned archive/report fixture unlocks only the optional first reply");
    }
    private static void AcceptAndVerify(FrontierRuntime app,string capture=null)
    {
        Interact(app,"envoy");Prefix(app.Game,"goto5","camp","close");Choose(app,"returnletter_offer",capture==null?null:capture+"-lune-menu");
        Choose(app,"returnletter_accept",capture==null?null:capture+"-offer");Check(Property<bool>(app.Game,"ReturnLetterAccepted"),"Actual Lune pages and numbered acceptance begin the optional journey");
        Interact(app,"guild",true);Prefix(app.Game,"routes","close");Choose(app,"returnletter_review",capture==null?null:capture+"-guild-menu");Choose(app,"returnletter_check",capture==null?null:capture+"-lyra-review");
        Check(Property<bool>(app.Game,"ReturnLetterChecked"),"Actual Lyra numbered check verifies the three earned originals");
    }
    private static void HarborWitnesses(FrontierRuntime app,string capture=null)
    {
        Travel(app,5);var g=app.Game;g.enemies.Clear();g.projectiles.Clear();g.effects.Clear();g.hazards.Clear();
        for(int id=0;id<3;id++)
        {
            Interact(app,"castaway"+id);Prefix(g,"rescue"+id,"close");int wood=g.save.inventory[0],herbs=g.save.inventory[2];Choose(app,"rescue"+id);
            Check(g.Has("rescue_"+id)&&g.save.inventory[0]==wood-1&&g.save.inventory[2]==herbs-1,"Actual mature rescue "+id+" retains its original material cost and flag");
            Interact(app,"castaway"+id);Prefix(g,"close","close");Choose(app,id==0?"returnletter_child":id==1?"returnletter_ferryman":"returnletter_lampmaker");Choose(app,"close",capture==null?null:capture+(id==0?"-child-account":id==1?"-ferryman-account":"-lampmaker"));
        }
        Check(Property<bool>(g,"ReturnLetterChild")&&Property<bool>(g,"ReturnLetterFerryman"),"Each actual rescued witness checks their own separate page");
        Interact(app,"relay2");Prefix(g,"relay2","close");int parts=g.save.inventory[4];Choose(app,"relay2");
        Check(g.Has("relay_2")&&g.save.inventory[4]==parts-1,"Actual original southern relay repair still consumes exactly one part");
        var relay=Object(g,"relay2");Check(!g.CanStand(new Point(relay.x,relay.y)),"The authored third relay remains in its original nonwalkable water/scenery cell");
        Beside(app,"relay2");Check(g.CanStand(g.Position)&&Near(g.save.x,25)&&Near(g.save.y,18.3f),"Real relay interaction remains on authored walkable north-shore footing");
    }
    private static void Begin(FrontierRuntime app,string capture=null)
    { Interact(app,"relay2");Prefix(app.Game,"close","close");Choose(app,"returnletter_signal_menu");Choose(app,"returnletter_begin",capture);Check(Active(app.Game)&&!Confirming(app.Game),"Actual numbered relay rules start a preparation phase, not instant delivery"); }
    private static void StageToRelay(FrontierRuntime app,bool family=true)
    { Prepare(app,family);AcceptAndVerify(app);HarborWitnesses(app); }
    private static void WalkTo(FrontierRuntime app,Point destination)
    {
        int frames=0;Point start=app.Game.Position;
        while((app.Game.Position-destination).Length>.13f&&frames++<500)
        {
            Point direction=app.Game.DirectionTo(app.Game.Position,destination);Input.axes["Horizontal"]=direction.x;Input.axes["Vertical"]=-direction.y;Update(app);Set(app,"camera",app.Game.Position);
        }
        Input.axes.Clear();Check(frames<500&&(app.Game.Position-start).Length>1,"Actual axis/Update walking reaches the authored relay approach without teleporting");
    }
    private static void EarnedStoryInputsRewardsAndBranches(FrontierRuntime app)
    {
        for(int branch=0;branch<2;branch++)
        {
            Prepare(app,branch==0);var g=app.Game;AcceptAndVerify(app,branch==0?"family":"public");HarborWitnesses(app,branch==0?"family":"public");
            Beside(app,"castaway2");WalkTo(app,new Point(25,18.3f));Check(ReferenceEquals(g.Nearest(),Object(g,"relay2")),"Real walking leaves the southern relay as the nearest E target");
            Key(app,KeyCode.E);Choose(app,"returnletter_signal_menu");Choose(app,"returnletter_begin");Frames(app,63);
            Check(Confirming(g)&&!Property<bool>(g,"ReturnLetterSignalDelivered"),"The real accepted 1.25-second clock opens the window without auto-delivering");
            Key(app,KeyCode.E);Check(!Active(g)&&Property<bool>(g,"ReturnLetterSignalDelivered")&&g.mode=="world","Actual E inside the bright window records one receipt");
            int tea=g.save.tea,trust=g.save.reputation[1],coins=g.save.coins,xp=g.save.xp,shards=g.save.shards,points=g.save.skillPoints;
            Travel(app,0);Interact(app,"envoy");Choose(app,"returnletter_offer");Choose(app,"returnletter_finish",branch==0?"family-delivery":"public-delivery");
            Check(Property<bool>(g,"ReturnLetterComplete")&&g.save.tea==tea+2&&g.save.reputation[1]==trust+1,"Actual Lune receipt grants exactly two teas and Ferrymen trust +1");
            Check(g.save.coins==coins&&g.save.xp==xp&&g.save.shards==shards&&g.save.skillPoints==points,"The new receipt cannot repay original archive currency, shards, XP or skill points");
            Check(g.Has(branch==0?"archive_family":"archive_public")&&!g.Has(branch==0?"archive_public":"archive_family")&&g.GetQuest("archive").complete,"Family-first/public choice and mature archive completion remain unchanged");
            Check(g.conversation.textEn.Contains(branch==0?"Families still verify first":"public guild copy")&&g.conversation.textEn.Contains(branch==0?"not the missing people's return":"no evidence that the missing returned"),"Actual reread honors the player's archive policy without promising a missing person's return");
            Choose(app,"close",branch==0?"family-receipt":"public-receipt");Interact(app,"envoy");Choose(app,"returnletter_offer");Choose(app,"close");
            Call(g,"ChooseReturnLetter","returnletter_finish");Check(g.save.tea==tea+2&&g.save.reputation[1]==trust+1,"Repeated real reread and stale direct finish cannot duplicate reward");
            Check(g.GetQuest("returnletter").complete&&g.GetQuest("returnletter").room==0,"The new quest becomes complete at Lune without replacing the archive quest");
        }
        Console.WriteLine("PASS actual Lune/Lyra/three original rescues/one repair, separate witnesses, real axis walking and E window, both archive policies, exactly-once 2 teas/trust +1; NATIVE_UNVERIFIED");
    }
    private static void TimedWindowPauseAndFreeRetry(FrontierRuntime app)
    {
        StageToRelay(app);Begin(app);var g=app.Game;int wood=g.save.inventory[0],herbs=g.save.inventory[2],parts=g.save.inventory[4],tea=g.save.tea;
        Key(app,KeyCode.E);Check(Active(g)&&!Confirming(g)&&!g.Has("returnletter_signal")&&g.mode=="world","Early actual E waits without opening old dialogue or completing/resetting the handoff");
        foreach(string pause in new[]{"menu","application","review","hitstop"})
        {
            if(pause=="menu")Key(app,KeyCode.Escape);else if(pause=="application")Call(app,"OnApplicationPause",true);else if(pause=="review")app.VisualReviewPaused=true;else g.hitStop=.4f;
            float remaining=Remaining(g),world=g.save.totalTime;Frames(app,8);Draw(app,"pause-"+pause);
            Check(Near(Remaining(g),remaining)&&Near(g.save.totalTime,world),"Actual "+pause+" pauses both the light and accepted world clock");
            if(pause=="menu")Key(app,KeyCode.Escape);else if(pause=="application")Call(app,"OnApplicationPause",false);else if(pause=="review")app.VisualReviewPaused=false;else g.hitStop=0;
        }
        Frames(app,180);Check(!Active(g)&&!g.Has("returnletter_signal"),"A missed real confirmation window expires without fabricating delivery");
        Begin(app);Frames(app,64);Check(Confirming(g),"Free retry opens a new real bright window");Key(app,KeyCode.JoystickButton1);
        Check(g.Has("returnletter_signal")&&!Active(g),"Actual gamepad B confirms the same live light window");
        Check(g.save.inventory[0]==wood&&g.save.inventory[2]==herbs&&g.save.inventory[4]==parts&&g.save.tea==tea,"Early press, pause, timeout, retry and success consume no materials or tea");
        Console.WriteLine("PASS real early E, menu/application/review/hitstop pause, timeout without delivery, free retry and gamepad-B confirmation; NATIVE_UNVERIFIED");
    }
    private static void CancellationPhysicalGuardsAndReload(FrontierRuntime app)
    {
        StageToRelay(app);Begin(app);var g=app.Game;Position(app,new Point(28,18));Update(app);Check(!Active(g)&&!g.Has("returnletter_signal"),"Leaving the real relay radius cancels its temporary light without delivery");
        Begin(app);Travel(app,0);Check(!Active(g)&&g.Has("returnletter_checked")&&g.Has("returnletter_child"),"Real map travel resets only the live handoff, retaining checked pages");
        Travel(app,5);g.enemies.Clear();Begin(app);g.PrepareSave();Check(SaveStore.Write(g.save,2),"Actual SaveStore writes an in-progress reply envelope");string warning;SaveData loaded=SaveStore.Read(2,out warning);
        Check(loaded!=null&&warning==null,"Actual checksummed reply envelope reads successfully");g.Load(loaded);g.Start();Check(!Active(g)&&!g.Has("returnletter_signal")&&g.Has("returnletter_ferryman"),"Loading keeps permanent verifications but does not resume an invisible light timer");
        g.enemies.Clear();Begin(app);g.enemies.Add(new Enemy(99171,0,g.save.x+1,g.save.y,20));Update(app);Check(!Active(g)&&!g.Has("returnletter_signal"),"A real nearby hostile cancels an unsafe handoff");g.enemies.Clear();
        Travel(app,0);var before=JsonUtility.ToJson(g.save);Call(g,"ChooseReturnLetter","returnletter_begin");Call(g,"ChooseReturnLetter","returnletter_child");Call(g,"ChooseReturnLetter","returnletter_finish");
        Check(!Active(g)&&JsonUtility.ToJson(g.save)==before,"Wrong-room stale callbacks cannot start, verify a remote page or grant reward");
        foreach(int version in new[]{1,2})
        {
            Prepare(app);g=app.Game;g.PrepareSave();SaveData old=JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(g.save));old.version=version;File.WriteAllText(SaveStore.PathFor(2),JsonUtility.ToJson(old));
            loaded=SaveStore.Read(2,out warning);Check(loaded!=null&&warning==null,"Legacy raw v"+version+" remains readable");g.Load(loaded);g.Start();
            Check(Property<bool>(g,"ReturnLetterReady")&&!Property<bool>(g,"ReturnLetterAccepted")&&g.Has("archive_family")&&g.Has("harbor_report"),"Legacy v"+version+" unlocks the follow-up without fabricating acceptance or losing the old ending");
        }
        Console.WriteLine("PASS actual leaving/travel/envelope reload/nearby threat cancellation, retained page flags, remote callback guards and raw v1/v2 compatibility; NATIVE_UNVERIFIED");
    }
    private static void SaveStagesAndDeliveryContinuity(FrontierRuntime app)
    {
        Prepare(app,false);AcceptAndVerify(app);HarborWitnesses(app);var g=app.Game;Begin(app);Frames(app,64);Key(app,KeyCode.E);
        g.PrepareSave();Check(SaveStore.Write(g.save,2),"Actual SaveStore writes a delivered receipt awaiting Lune");string warning;SaveData loaded=SaveStore.Read(2,out warning);g.Load(loaded);g.Start();g.enemies.Clear();
        Check(g.Has("returnletter_signal")&&!Active(g)&&!Property<bool>(g,"ReturnLetterComplete")&&g.save.trackedQuest=="returnletter","Delivered receipt and tracking survive reload without an active clock");
        Travel(app,0);Interact(app,"envoy");Choose(app,"returnletter_offer");int tea=g.save.tea,trust=g.save.reputation[1];Choose(app,"returnletter_finish");Choose(app,"close");g.PrepareSave();Check(SaveStore.Write(g.save,2),"Actual SaveStore writes the completed new journey");
        loaded=SaveStore.Read(2,out warning);g.Load(loaded);g.Start();Check(Property<bool>(g,"ReturnLetterComplete")&&g.Has("archive_public")&&g.save.tea==tea+2&&g.save.reputation[1]==trust+1,"Completed receipt/reward/public policy survive actual envelope reload");
        Interact(app,"envoy");Choose(app,"returnletter_offer");Check(Array.FindIndex(g.conversation.choices,c=>c.action=="returnletter_finish")<0,"Reloaded complete reread exposes no second reward action");Choose(app,"close");
        Check(g.save.tea==tea+2&&g.save.reputation[1]==trust+1,"Reloaded reread cannot duplicate the completed reward");
        Console.WriteLine("PASS real checksummed delivered/completed snapshots, new tracked quest, public-policy continuity and reload-safe once-only reward; NATIVE_UNVERIFIED");
    }
    // These isolated prose fixtures stage already-earned progress. The two
    // journeys above prove real inputs/costs; these record every new full page.
    private static void CaptureNarrativeLayouts(FrontierRuntime app)
    {
        for(int language=0;language<2;language++)for(int large=0;large<2;large++)
        {
            Prepare(app,true,language==1,large==1);var g=app.Game;
            string suffix="narrative-"+(language==1?"en":"zh")+(large==1?"-large":"-normal");
            Interact(app,"envoy");Choose(app,"returnletter_offer");Choose(app,"returnletter_accept",suffix+"-offer");
            Interact(app,"guild");Choose(app,"returnletter_review");Choose(app,"returnletter_check",suffix+"-lyra-review");
            // Only the pre-existing rescued state is staged for these page captures.
            foreach(string flag in new[]{"rescue_0","rescue_1","rescue_2","relay_2"})if(!g.Has(flag))g.save.flags.Add(flag);
            g.Enter(5);g.enemies.Clear();g.projectiles.Clear();g.effects.Clear();g.hazards.Clear();
            foreach(int witness in new[]{0,1,2})
            {
                Interact(app,"castaway"+witness);Choose(app,witness==0?"returnletter_child":witness==1?"returnletter_ferryman":"returnletter_lampmaker");
                Choose(app,"close",suffix+(witness==0?"-child-account":witness==1?"-ferryman-account":"-lampmaker"));
            }
            Interact(app,"relay2");Choose(app,"returnletter_signal_menu");Choose(app,"close",suffix+"-relay-rules");
            g.save.flags.Add("returnletter_signal");g.Enter(0);g.enemies.Clear();
            Interact(app,"envoy");Choose(app,"returnletter_offer");Choose(app,"returnletter_finish");Choose(app,"close",suffix+"-receipt");
        }
    }
    private static void AuthoritativeBilingualHudAndReadOnlyPointer(FrontierRuntime app)
    {
        CaptureNarrativeLayouts(app);
        foreach(int width in new[]{960,1280,1920})for(int language=0;language<2;language++)for(int large=0;large<2;large++)
        {
            StageToRelay(app);var g=app.Game;g.save.english=language==1;g.save.largeText=large==1;g.save.effectsQuality=0;g.save.reducedMotion=true;
            Screen.width=width;Screen.height=width==960?600:width==1920?1080:800;Call(app,"Resize");Begin(app);
            string suffix=(language==1?"en":"zh")+(large==1?"-large":"")+"-"+width;
            foreach(bool ready in new[]{false,true})
            {
                if(ready)Frames(app,64);Check(Confirming(g)==ready,"Fixture reaches the actual preparation/confirmation phase");
                Check(Property<bool>(app,"ReturnLetterHudVisible"),"Actual world HUD exposes the live timing panel under low FX and reduced motion");
                Rect panel=(Rect)Call(app,"ReturnLetterHudBounds");string save=JsonUtility.ToJson(g.save);float remaining=Remaining(g);Draw(app,(ready?"confirm-":"prepare-")+suffix);
                Check(GUI.textureCalls.Exists(t=>Same(t.rect,panel)),"Actual OnGUI draws the authoritative timing panel");
                Check(Text(language==1?(ready?"Signal aligned":"Wait for alignment"):(ready?"灯光已亮":"等候短光")),"Actual localized panel names its real timing phase");
                Check(!GUI.buttonCalls.Exists(b=>Overlap(b.rect,panel)),"Read-only timing panel adds no hidden clickable UI");
                Point center=new Point(panel.x+panel.width/2,panel.y+10);var regions=(List<Rect>)Call(app,"HudPointerRegions");
                Check(!regions.Exists(r=>Contains(r,center))&&!(bool)Call(app,"PointerOverInterface",center),"Read-only light information passes world pointer input through");
                Check(save==JsonUtility.ToJson(g.save)&&Near(remaining,Remaining(g)),"Repeated actual OnGUI cannot advance the light, save flags or reward");
            }
            Key(app,KeyCode.Escape);Draw(app);Check(!Property<bool>(app,"ReturnLetterHudVisible"),"Actual pause menu hides the world handoff HUD");
        }
        Console.WriteLine("PASS four full localized/new-prose page sets and 24 actual preparation/confirmation OnGUI layouts across Chinese/English, normal/large and 960/1280/1920; low-FX/reduced-motion, read-only clock/save and pointer transparency; NATIVE_UNVERIFIED");
    }
    public static int Main(string[] args)
    {
        string previous=Application.persistentDataPath,directory=Path.Combine(Path.GetTempPath(),"starfall-return-letter-check-"+Guid.NewGuid());Application.persistentDataPath=directory;
        captureFolder=args.Length>0?args[0]:null;if(captureFolder!=null)Directory.CreateDirectory(captureFolder);int failed=0;var app=new FrontierRuntime();
        try
        {
            Call(app,"Awake");Check(app.Game!=null,"Actual runtime Awake loads the complete shipped campaign");
            foreach(Action<FrontierRuntime> test in new Action<FrontierRuntime>[]{EarnedStoryInputsRewardsAndBranches,TimedWindowPauseAndFreeRetry,CancellationPhysicalGuardsAndReload,SaveStagesAndDeliveryContinuity,AuthoritativeBilingualHudAndReadOnlyPointer})
                try{test(app);}catch(Exception error){failed++;Console.WriteLine("FAIL "+test.Method.Name+": "+(error.InnerException??error).Message);}
            app.EndVisualReview();Call(app,"OnDestroy");
        }
        finally { Application.persistentDataPath=previous;Input.Clear();GUI.queuedClick=null;if(Directory.Exists(directory))Directory.Delete(directory,true); }
        Console.WriteLine("Runtime Return Letter: "+(5-failed)+"/5 groups passed; captures="+captures+"; NATIVE_UNVERIFIED");return failed==0?0:1;
    }
}
