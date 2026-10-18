// Production Update, navigation, OnGUI and SaveStore against offline Unity
// declarations. Native import, GPU output and player feel stay unverified.
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Starfall;
using UnityEngine;

public static class RuntimeLandmarkChecks
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static string captures;
    static int captureCount, walkingFrames;
    static readonly Rect OpenButton = new Rect(913, 692, 304, 30);
    static readonly Rect SetButton = new Rect(866, 572, 351, 42);
    static readonly Rect ClearButton = new Rect(866, 622, 351, 36);
    static readonly Rect BackButton = new Rect(866, 670, 351, 28);
    static readonly Rect PreviousButton = new Rect(279, 625, 160, 32);
    static readonly Rect NextButton = new Rect(654, 625, 160, 32);
    static void Check(bool value, string text) { if (!value) throw new Exception(text); }
    static object Call(FrontierRuntime app, string method, params object[] args)
    { return typeof(FrontierRuntime).GetMethod(method, Private).Invoke(app, args); }
    static void Set(FrontierRuntime app, string field, object value)
    { typeof(FrontierRuntime).GetField(field, Private).SetValue(app, value); }
    static T Get<T>(FrontierRuntime app, string field)
    { return (T)typeof(FrontierRuntime).GetField(field, Private).GetValue(app); }
    static void Update(FrontierRuntime app) { Call(app, "Update"); Input.keys.Clear(); }
    static void Key(FrontierRuntime app, KeyCode key) { Input.keys.Add(key); Update(app); }
    static bool HasText(string text) { return GUI.commands.Exists(c => c.Contains("\"text\":" + GUI.S(text))); }
    static bool ColorIs(Color a, Color b)
    { return Math.Abs(a.r-b.r)<.001f && Math.Abs(a.g-b.g)<.001f && Math.Abs(a.b-b.b)<.001f && Math.Abs(a.a-b.a)<.001f; }
    static bool Same(Rect a, Rect b)
    { return Math.Abs(a.x-b.x)<.01f && Math.Abs(a.y-b.y)<.01f && Math.Abs(a.width-b.width)<.01f && Math.Abs(a.height-b.height)<.01f; }
    sealed class GuiText { public float[] r = new float[0]; }
    static Rect TextRect(float x, float y)
    {
        foreach(string command in GUI.commands)
        {
            if(!command.StartsWith("{\"k\":\"text\"",StringComparison.Ordinal))continue;
            var text=JsonUtility.FromJson<GuiText>(command);
            if(text.r!=null && text.r.Length==4 && Math.Abs(text.r[0]-x)<.01f && Math.Abs(text.r[1]-y)<.01f)
                return new Rect(text.r[0],text.r[1],text.r[2],text.r[3]);
        }
        throw new Exception("The actual local-landmark caption is missing its drawn text rectangle");
    }
    static bool Intersects(Rect a, Rect b)
    { return a.x<b.x+b.width && a.x+a.width>b.x && a.y<b.y+b.height && a.y+a.height>b.y; }
    static void Draw(FrontierRuntime app, string capture = null)
    {
        GUI.commands.Clear(); GUI.textureCalls.Clear(); GUI.buttonCalls.Clear(); Call(app, "OnGUI");
        if (captures != null && capture != null)
        {
            var commands=GUI.commands.ToArray(); var textures=GUI.textureCalls.ToArray(); var buttons=GUI.buttonCalls.ToArray();
            GUI.Export(captures,"qa-landmarks-"+capture); GUI.commands.AddRange(commands); GUI.textureCalls.AddRange(textures); GUI.buttonCalls.AddRange(buttons); captureCount++;
        }
    }
    static GUI.ButtonCall Button(FrontierRuntime app, Rect rect, bool enabled = true)
    {
        Draw(app); var button=GUI.buttonCalls.Find(b=>Same(b.rect,rect));
        Check(button!=null && button.enabled==enabled,"Actual displayed control has expected bounds and enabled state: "+rect.x+","+rect.y+" mode="+app.Game.mode);
        return button;
    }
    static void Click(FrontierRuntime app, Rect rect)
    { Button(app,rect); GUI.queuedClick=rect; Draw(app); Check(!GUI.queuedClick.HasValue,"Production control consumes its real click"); Draw(app); }
    static void FocusActivate(FrontierRuntime app, Rect rect, bool pad = false)
    {
        Button(app,rect); string id=(string)Call(app,"ControlId",rect.x,rect.y,rect.width,rect.height);
        var nav=Get<MenuNavigation>(app,"navigation"); int attempts=0;
        while(nav.focused!=id && attempts++<64) { Key(app,KeyCode.Tab); Draw(app); }
        Check(nav.focused==id && nav.CanActivate(id),"Actual Tab focus reaches the enabled local-landmark control");
        Key(app,pad?KeyCode.JoystickButton0:KeyCode.Return); Draw(app);
    }
    static WorldObject Object(GameCore game, string kind)
    { var o=Array.Find(game.Map.objects,x=>x.kind==kind); Check(o!=null,"The authored map has "+kind); return o; }
    static void Position(FrontierRuntime app, Point point)
    {
        Check(app.Game.CanStand(point),"Explicit fixture feet use actual legal terrain");
        Check(app.SetVisualReviewPosition(point) && (app.Game.Position-point).Length<.001f,"Fixture staging does not silently repair position");
        Set(app,"camera",app.Game.Position);
    }
    static void Prepare(FrontierRuntime app, int room = 0, bool discovered = true)
    {
        Input.Clear(); GUI.queuedClick=null; Screen.width=1280; Screen.height=800; Time.unscaledDeltaTime=.02f;
        app.BeginVisualReview(0,room); app.VisualReviewPaused=false; var game=app.Game;
        game.save.flags.RemoveAll(f=>f.StartsWith("landmark_pin:")); game.save.flags.Add("landmark_runtime_mature_story");
        game.save.companion=-1; game.save.english=true; game.save.largeText=false; game.save.reducedMotion=true; game.save.effectsQuality=0; game.save.showGuidance=true;
        game.save.trackedQuest="main";
        if(discovered)game.save.explored[room]=16777215; else game.save.explored[room]=0;
        Set(app,"applicationPaused",false); Set(app,"pointerSeen",false); Set(app,"controllerUsed",false); Set(app,"arrivalTime",0f); Set(app,"resumeTime",0f); Set(app,"combatHudTime",0f); Set(app,"selectedRegion",room);
        Update(app); game.toastTime=0; Set(app,"arrivalTime",0f); Set(app,"resumeTime",0f); Call(app,"Resize");
    }
    static void Open(FrontierRuntime app)
    { Check(app.Game.mode=="world","Map entry begins in actual world mode"); Key(app,KeyCode.M); Check(app.Game.mode=="map","Real M opens the world map"); Click(app,OpenButton); Check(app.Game.mode=="landmarks","Real map button opens local landmarks"); }
    static void Select(FrontierRuntime app, WorldObject target)
    {
        var list=app.Game.DiscoveredLandmarks(); int index=list.IndexOf(target); Check(index>=0,"Target is a canonical discovered and currently present point");
        int page=index/6, limit=0;
        while(Get<int>(app,"landmarkPage")<page && limit++<20)Click(app,NextButton);
        while(Get<int>(app,"landmarkPage")>page && limit++<20)Click(app,PreviousButton);
        Check(Get<int>(app,"landmarkPage")==page,"Actual previous/next buttons reach the point's page");
        Click(app,new Rect(279,225+(index%6)*64,535,54));
        Check(Get<int>(app,"selectedLandmark")==index && HasText(app.Game.L(target.name,target.en)),"Actual list selection shows the selected complete named point");
    }
    static void Pin(FrontierRuntime app, WorldObject target)
    { Select(app,target); Click(app,SetButton); Check(object.ReferenceEquals(app.Game.PinnedLandmark,target),"Only explicit set confirms the canonical named destination"); }
    static void Stop() { Input.axes.Clear(); Input.held.Clear(); }
    static void Walk(FrontierRuntime app, WorldObject target)
    {
        var game=app.Game; var destination=new Point(target.x,target.y); var path=game.NavigationPath(destination);
        Check(path.Count>0,"Pinned local point has an actual walkable approach");
        foreach(Point waypoint in path)
        {
            int steps=0;
            while((game.Position-waypoint).Length>.11f && steps++<160)
            {
                Point before=game.Position, direction=(waypoint-before).Normalized;
                Input.axes["Horizontal"]=direction.x; Input.axes["Vertical"]=-direction.y; Input.held.Clear();
                if(direction.x>.01f)Input.held.Add(KeyCode.D);if(direction.x<-.01f)Input.held.Add(KeyCode.A);if(direction.y>.01f)Input.held.Add(KeyCode.S);if(direction.y<-.01f)Input.held.Add(KeyCode.W);
                Update(app);walkingFrames++;
                Check(game.mode=="world" && game.save.room==0 && game.CanStand(game.Position) && (game.Position-before).Length<.3f,"Real WASD follows legal continuous feet without travel or teleport");
            }
            Stop();Check(steps<160,"Actual movement reaches the next navigation waypoint");
        }
        Stop();Check(game.Nearest()==target,"Pinned approach ends in the actual named point's E interaction range");
    }
    static void Load(FrontierRuntime app, SaveData save)
    { app.Game.Load(save);app.Game.Start();app.Game.Close();Set(app,"camera",app.Game.Position);Set(app,"arrivalTime",0f);Set(app,"resumeTime",0f); }
    public static void RealSelectionNavigationAndArrival(FrontierRuntime app)
    {
        Prepare(app);var game=app.Game;var cook=Object(game,"cook");string tracked=game.save.trackedQuest;var questTarget=game.NavigationTarget();int coins=game.save.coins;var items=(int[])game.save.inventory.Clone();
        Open(app);var list=game.DiscoveredLandmarks();Check(list.Count>6,"Authored town exposes genuine multipage discovered points");
        Select(app,list[list.Count-1]);Check(!game.HasLandmarkPin && game.save.trackedQuest==tracked,"Selection and pagination do not silently pin or replace a quest");
        Pin(app,cook);Check(game.save.trackedQuest==tracked && game.NavigationTarget().HasValue && (game.NavigationTarget().Value-new Point(cook.x,cook.y)).Length<.001f,"Local pin overrides only the navigation target, preserving original tracked story");
        Click(app,BackButton);Check(game.mode=="map","Local back button returns to the original world map");Key(app,KeyCode.Escape);Check(game.mode=="world","Map Escape returns to world");
        Walk(app,cook);Check(walkingFrames>25,"Positive arrival uses real held movement rather than fixture writes");Key(app,KeyCode.E);Check(game.mode=="dialog" && game.conversation!=null && game.conversation.choices[0].action=="cook","Arriving never autointeracts; real E opens the unchanged chef menu");
        Check(game.save.coins==coins && game.save.trackedQuest==tracked,"Local navigation neither purchases nor changes story tracking");for(int i=0;i<items.Length;i++)Check(game.save.inventory[i]==items[i],"Navigation consumes no inventory");
        game.Close();Open(app);Click(app,ClearButton);Check(!game.HasLandmarkPin && game.PinnedLandmark==null && game.save.trackedQuest==tracked,"Explicit clear removes only the optional local pin");
        Check(game.NavigationTarget().HasValue==questTarget.HasValue && (!questTarget.HasValue || (game.NavigationTarget().Value-questTarget.Value).Length<.001f),"Clear restores the original tracked-quest target");
        Console.WriteLine("PASS actual M/list paging/selection/explicit pin/clear/back, "+walkingFrames+" continuous WASD frames and manual original chef E interaction; NATIVE_UNVERIFIED");
    }
    public static void PersistentPinAndLegacySaveIsolation(FrontierRuntime app)
    {
        Prepare(app);var game=app.Game;var cook=Object(game,"cook");Open(app);Pin(app,cook);Key(app,KeyCode.Escape);Key(app,KeyCode.Escape);
        game.PrepareSave();Check(SaveStore.Write(game.save,0),"Actual checksum envelope writes the optional pin");string warning;var saved=SaveStore.Read(0,out warning);Check(saved!=null && warning==null,"Actual checksum reader accepts the saved pin");
        string quest=saved.trackedQuest;Load(app,saved);cook=Object(game,"cook");Check(game.PinnedLandmark==cook && game.save.trackedQuest==quest && game.Has("landmark_runtime_mature_story"),"Actual reload restores only the earned current-map pin and retains mature story");
        int flags=game.save.flags.Count,revision=game.revision;for(int i=0;i<10;i++){game.DiscoveredLandmarks();Check(game.PinnedLandmark==cook && game.NavigationTarget().HasValue,"Repeated pin queries retain the same active target");}Check(game.save.flags.Count==flags && game.revision==revision,"Reading discovered points and active pin never mutates persistent flags or revision");
        game.Enter(1);Check(game.PinnedLandmark==null && game.save.trackedQuest==quest,"Other-map travel suspends the local pin without replacing story tracking");game.Enter(0);Check(game.PinnedLandmark==Object(game,"cook"),"Returning activates the same canonical town point");
        Check(!game.PinLandmark(Object(game,"cook")) && !game.ClearLandmarkPin(),"World-mode forged pin/clear calls cannot bypass explicit menu confirmation");
        foreach(int version in new[]{1,2})
        {
            var legacy=JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(game.save));legacy.version=version;legacy.flags.RemoveAll(f=>f.StartsWith("landmark_pin:"));legacy.flags.Add("supper_complete");legacy.coins=137;
            File.WriteAllText(SaveStore.PathFor(2),JsonUtility.ToJson(legacy));var loaded=SaveStore.Read(2,out warning);Check(loaded!=null && warning==null,"Raw legacy save v"+version+" loads through production SaveStore");Load(app,loaded);
            Check(!game.HasLandmarkPin && game.PinnedLandmark==null && game.save.coins==137 && game.Has("supper_complete") && game.save.trackedQuest==quest,"Old saves invent no pin and retain economy, completed stories and tracking");
        }
        Console.WriteLine("PASS actual checksum pin restoration, read-only queries, travel suspension/return, forged world mutation rejection and raw legacy v1/v2 compatibility; NATIVE_UNVERIFIED");
    }
    public static void FocusPauseDiscoveryAndSafety(FrontierRuntime app)
    {
        Prepare(app,0,false);var game=app.Game;var cook=Object(game,"cook");Check(!game.DiscoveredLandmarks().Contains(cook),"Undiscovered named resident does not leak from the map");
        Position(app,new Point(8.5f,17.5f));Update(app);Check(game.DiscoveredLandmarks().Contains(cook),"Actual world Update discovers the resident's explored sector");
        game.save.explored[0]=16777215;Open(app);Select(app,cook);float time=game.save.totalTime,energy=game.save.energy;var point=game.Position;game.attackCooldown=.8f;game.skillCooldown=2.4f;
        Input.held.Add(KeyCode.J);for(int i=0;i<20;i++)Update(app);Input.held.Clear();Check(game.playerAction==null && game.save.totalTime==time && game.save.energy==energy && (game.Position-point).Length<.001f && game.attackCooldown==.8f && game.skillCooldown==2.4f,"Named-point menu pauses actual combat, movement, resources and cooldowns");
        FocusActivate(app,SetButton,true);Check(game.PinnedLandmark==cook,"Real pad A activates a Tab-focused explicit waypoint control");Key(app,KeyCode.JoystickButton1);Check(game.mode=="map","Real pad B returns to world-map context");
        Key(app,KeyCode.JoystickButton1);Draw(app);Check(HasText("In reach · B to interact"),"Actual controller arrival hint names B rather than the keyboard-only E control");
        Open(app);Key(app,KeyCode.JoystickButton4);Check(game.mode=="journal","Landmark LB uses the existing map menu's previous destination");game.Close();Open(app);Key(app,KeyCode.JoystickButton5);Check(game.mode=="camp","Landmark RB uses the existing map menu's next destination");game.Close();
        Position(app,new Point(11,13.5f));var flagCopy=JsonUtility.ToJson(game.save.flags);game.StartSparring();Check(game.SparringActive,"Fixture starts the original earned Ada sparring through its guarded API");
        Check(game.PinnedLandmark==null && !game.PinLandmark(cook),"Sparring does not inherit a local waypoint or mutate it");Key(app,KeyCode.M);Button(app,OpenButton,false);Check(game.mode=="map","Sparring may inspect the old map but cannot enter local navigation");game.Close();game.EndPractice();
        Check(JsonUtility.ToJson(game.save.flags)==flagCopy,"Training and rejected navigation controls retain original persistent flags");
        Prepare(app,8);game=app.Game;Check(game.InAbyss && game.PinnedLandmark==null,"Seeded abyss keeps its original floor objective");Key(app,KeyCode.M);Button(app,OpenButton,false);Check(game.mode=="map","Abyss map exposes no enabled persistent-local navigation entry");
        Prepare(app);game=app.Game;Open(app);Pin(app,Object(game,"cook"));game.Close();typeof(GameCore).GetMethod("BeginDeath",Private).Invoke(game,null);Check(game.Dying && game.PinnedLandmark==null,"Death presentation suspends optional world waypoints");
        Call(app,"OpenLandmarks");Check(game.mode!="landmarks","Dying cannot open the named-point menu via a stale entry call");
        Console.WriteLine("PASS real sector discovery, pad A/B and LB/RB, menu freeze, practice/abyss/death guards with original training and story isolation; NATIVE_UNVERIFIED");
    }
    public static void BlockedCacheAndRealRootTopology(FrontierRuntime app)
    {
        Prepare(app,GameCore.ReachRoom);var game=app.Game;
        // Isolate navigation from the unchanged seven-guard combat fixture;
        // no kill, defeat flag, statistic or reward is granted by this staging.
        Check(game.enemies.Count==7,"The mature riverbank still spawns its seven guards before navigation isolation");game.enemies.Clear();
        var cache=Object(game,"reachCache");int coins=game.save.coins;var items=(int[])game.save.inventory.Clone();string quest=game.save.trackedQuest;
        Check(!game.ReachRootsOpen && !game.ReachCacheRecovered && game.NavigationPath(new Point(cache.x,cache.y)).Count==0,"The authored cache route is genuinely blocked by original closed roots");
        Open(app);Pin(app,cache);Check(game.PinnedLandmark==cache && !game.ReachRootsOpen && !game.ReachCacheRecovered,"Pinning an explored blocked point opens no roots and takes no hidden supplies");
        Check(HasText("No walking route now. Check tides, gates or bridges."),"Actual detail view explains a blocked route rather than claiming a walkable path");
        Key(app,KeyCode.Escape);Key(app,KeyCode.Escape);Update(app);Check(Get<List<Point>>(app,"route").Count==0,"Actual world route cache keeps an unreachable landmark empty");
        Position(app,new Point(44,12));Key(app,KeyCode.R);Check(game.ReachRootsOpen && game.PinnedLandmark==cache,"Real R at the canonical root mechanism opens only the existing exploration route");
        Position(app,new Point(game.Map.spawnX,game.Map.spawnY));for(int i=0;i<40;i++)Update(app);
        Check(Get<List<Point>>(app,"route").Count>0 && game.NavigationPath(new Point(cache.x,cache.y)).Count>0,"The same pinned point refreshes to a real path after the actual topology change");
        Check(game.mode=="world" && !game.ReachCacheRecovered && game.save.coins==coins && game.save.trackedQuest==quest,"A newly reachable destination still requires manual arrival and original interaction, without rewards or quest changes");
        for(int i=0;i<items.Length;i++)Check(game.save.inventory[i]==items[i],"Pinning and using the original root ability do not consume or invent inventory");
        Open(app);Select(app,cache);Check(HasText("Follow the route, then press E to interact."),"The actual selected-point detail reflects the now-walkable topology");Draw(app,"reach-cache-open");
        Console.WriteLine("PASS real blocked riverbank cache/detail/empty world route, actual R root opening and same-pin topology refresh without auto rewards, travel or quest mutation; NATIVE_UNVERIFIED");
    }
    public static void BilingualLayoutAndRouteWitnesses(FrontierRuntime app)
    {
        foreach(bool english in new[]{false,true})foreach(bool large in new[]{false,true})foreach(int width in new[]{960,1920})
        {
            Prepare(app);var game=app.Game;game.save.english=english;game.save.largeText=large;Screen.width=width;Screen.height=width==960?640:1080;Call(app,"Resize");
            string suffix=(english?"en":"zh")+(large?"-large":"-normal")+"-"+width;Open(app);var cook=Object(game,"cook");Select(app,cook);
            game.Notify("路标提示","Waypoint notice");Draw(app,"selected-"+suffix);
            Rect toast=new Rect(382,706,516,67);
            Check(game.toastTime>0 && HasText(game.L("路标提示","Waypoint notice")) && GUI.textureCalls.Exists(t=>Same(t.rect,toast)),"An actual live menu toast is drawn, rather than cleared to make the new page fit");
            Check(!Intersects(TextRect(291,670),toast),"The actual new local-landmark footer caption must not intersect the preserved visible menu toast");
            foreach(var button in GUI.buttonCalls)
                if(button.rect.x==279 && button.rect.width==535 || Same(button.rect,SetButton) || Same(button.rect,ClearButton) || Same(button.rect,BackButton) || Same(button.rect,PreviousButton) || Same(button.rect,NextButton))
                    Check(button.rect.y>=178 && button.rect.y+button.rect.height<=733 && button.rect.x+button.rect.width<=1218,"Every new local-page control stays above footer and inside its logical window, preserving the separate existing menu chrome");
            var list=game.DiscoveredLandmarks();for(int page=0;page<(list.Count+5)/6;page++)
            {
                if(page>0)Click(app,NextButton);Draw(app,"page"+page+"-"+suffix);
                for(int i=0;i<6 && page*6+i<list.Count;i++)
                {
                    var row=GUI.buttonCalls.Find(b=>Same(b.rect,new Rect(279,225+i*64,535,54)));Check(row!=null && row.enabled && row.rect.height>=row.style.fontSize,"Every canonical point on every page retains a readable full-height selectable row");
                    Check(HasText(game.L(list[page*6+i].name,list[page*6+i].en)) || GUI.commands.Exists(c=>c.Contains(game.L(list[page*6+i].name,list[page*6+i].en))),"Every row draws its real discovered object's name");
                }
            }
            Pin(app,cook);Draw(app,"pinned-"+suffix);Key(app,KeyCode.Escape);Key(app,KeyCode.Escape);Position(app,new Point(18,17));Update(app);
            GUI.textureCalls.Clear();GUI.commands.Clear();Call(app,"DrawNavigation");Color mint=PixelArt.C("#7acabb"),gold=PixelArt.C("#ebc58b");
            Check(GUI.textureCalls.Exists(t=>ColorIs(t.color,mint)),"Actual world destination witness uses distinct mint for a local waypoint");
            Check(!GUI.textureCalls.Exists(t=>ColorIs(t.color,gold)),"Pinned world navigation does not falsely draw the gold quest destination");
            GUI.textureCalls.Clear();GUI.commands.Clear();Call(app,"MiniTerrain",888f,274f,8.4f);float cell=8.4f;
            Rect goalRect=new Rect(888+cook.x*cell-3,274+cook.y*cell-3,7,7);Check(GUI.textureCalls.Exists(t=>Same(t.rect,goalRect) && ColorIs(t.color,mint)),"Actual minimap's precise pinned point is mint at map coordinates");
            Draw(app,"world-"+suffix);Check(HasText(game.L(cook.name,cook.en)),"Actual exploration HUD explains the current waypoint name");
            game.save.showGuidance=false;GUI.textureCalls.Clear();GUI.commands.Clear();Call(app,"DrawNavigation");Check(GUI.textureCalls.Count==0,"Existing guide-off accessibility setting hides waypoint navigation submissions");game.save.showGuidance=true;
            Open(app);Click(app,ClearButton);Key(app,KeyCode.Escape);Key(app,KeyCode.Escape);Update(app);GUI.textureCalls.Clear();GUI.commands.Clear();Call(app,"DrawNavigation");
            Check(!game.HasLandmarkPin && game.save.trackedQuest=="main","All language/text-size/screen variants preserve the original tracked quest after clear");
        }
        Console.WriteLine("PASS zh/en normal/large 960/1920 complete multipage UI, strict controls, actual mint world/minimap witnesses, named HUD and guide-off isolation; captures="+captureCount+"; NATIVE_UNVERIFIED");
    }
    public static int Main(string[] args)
    {
        captures=args.Length>0 && args[0]!="-"?args[0]:null;string previous=Application.persistentDataPath;string directory=Path.Combine(Path.GetTempPath(),"starfall-landmarks-"+Guid.NewGuid());Application.persistentDataPath=directory;int failed=0;var app=new FrontierRuntime();
        try
        {
            Directory.CreateDirectory(directory);Call(app,"Awake");Check(app.Game!=null,"Production Awake loads the authored campaign");
            foreach(Action<FrontierRuntime> test in new Action<FrontierRuntime>[]{RealSelectionNavigationAndArrival,PersistentPinAndLegacySaveIsolation,FocusPauseDiscoveryAndSafety,BlockedCacheAndRealRootTopology,BilingualLayoutAndRouteWitnesses})
                try{test(app);}catch(Exception error){failed++;Console.WriteLine("FAIL "+test.Method.Name+": "+(error.InnerException??error));}
            app.EndVisualReview();Call(app,"OnDestroy");
        }
        finally{Application.persistentDataPath=previous;Input.Clear();GUI.queuedClick=null;GUI.matrix=Matrix4x4.identity;GUI.color=Color.white;GUI.enabled=true;if(Directory.Exists(directory))Directory.Delete(directory,true);}
        Console.WriteLine("Runtime Local Landmarks: "+(5-failed)+"/5 groups passed; NATIVE_UNVERIFIED");return failed==0?0:1;
    }
}
