// Actual production Update, OnGUI, walking, E and SaveStore with offline Unity
// declarations. Import, GPU rendering, native input and feel stay unverified.
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Starfall;
using UnityEngine;

public static class RuntimeInteractionRouteChecks
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static string captures;
    static int captureCount, walkingFrames;
    static readonly Rect OpenButton = new Rect(913, 692, 304, 30);
    static readonly Rect SetButton = new Rect(866, 572, 351, 42);
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
    static bool Same(Rect a, Rect b)
    { return Math.Abs(a.x-b.x)<.01f && Math.Abs(a.y-b.y)<.01f && Math.Abs(a.width-b.width)<.01f && Math.Abs(a.height-b.height)<.01f; }
    static bool ColorIs(Color a, Color b)
    { return Math.Abs(a.r-b.r)<.001f && Math.Abs(a.g-b.g)<.001f && Math.Abs(a.b-b.b)<.001f && Math.Abs(a.a-b.a)<.001f; }
    static List<Point> Route(GameCore game, string method, object argument)
    {
        var api = typeof(GameCore).GetMethod(method, BindingFlags.Instance | BindingFlags.Public);
        // The immutable pre-change baseline must compile and fail semantically,
        // rather than produce a missing-member compiler failure.
        if(api == null)
        {
            WorldObject obj=argument as WorldObject;
            return game.NavigationPath(obj == null ? (Point)argument : new Point(obj.x,obj.y));
        }
        return (List<Point>)api.Invoke(game, new[]{argument});
    }
    static WorldObject Object(GameCore game, string id)
    { var result=Array.Find(game.Map.objects,o=>o.id==id || o.kind==id); Check(result!=null,"The unchanged authored map has "+id); return result; }
    static void Draw(FrontierRuntime app, string capture=null)
    {
        GUI.commands.Clear();GUI.textureCalls.Clear();GUI.buttonCalls.Clear();Call(app,"OnGUI");
        if(captures!=null && capture!=null)
        {
            var commands=GUI.commands.ToArray();var textures=GUI.textureCalls.ToArray();var buttons=GUI.buttonCalls.ToArray();
            GUI.Export(captures,"qa-interaction-routes-"+capture);GUI.commands.AddRange(commands);GUI.textureCalls.AddRange(textures);GUI.buttonCalls.AddRange(buttons);captureCount++;
        }
    }
    static void Click(FrontierRuntime app, Rect rect)
    {
        Draw(app);var button=GUI.buttonCalls.Find(b=>Same(b.rect,rect));
        Check(button!=null && button.enabled,"The actual enabled navigation control is drawn at "+rect.x+","+rect.y);
        GUI.queuedClick=rect;Draw(app);Check(!GUI.queuedClick.HasValue,"The actual navigation control consumes its click");Draw(app);
    }
    static void Position(FrontierRuntime app, Point point)
    {
        Check(app.Game.CanStand(point),"Explicit fixture feet use unchanged legal terrain");
        Check(app.SetVisualReviewPosition(point) && (app.Game.Position-point).Length<.001f,"Fixture staging does not silently repair position");Set(app,"camera",point);
    }
    static void Prepare(FrontierRuntime app, int room)
    {
        Input.Clear();GUI.queuedClick=null;Screen.width=1280;Screen.height=800;Time.unscaledDeltaTime=.02f;
        app.BeginVisualReview(0,room);app.VisualReviewPaused=false;var game=app.Game;
        game.save.flags.RemoveAll(f=>f.StartsWith("landmark_pin:"));game.save.flags.Add("interaction_route_mature_story");game.save.companion=-1;
        game.save.english=true;game.save.largeText=false;game.save.reducedMotion=true;game.save.effectsQuality=0;game.save.showGuidance=true;
        game.save.trackedQuest="main";game.save.explored[room]=16777215;
        // Leave the authored spawn intact, then isolate walking from its real
        // enemies without granting any kills, flags, statistics or rewards.
        game.enemies.Clear();Set(app,"applicationPaused",false);Set(app,"pointerSeen",false);Set(app,"controllerUsed",false);
        Set(app,"arrivalTime",0f);Set(app,"resumeTime",0f);Set(app,"combatHudTime",0f);Set(app,"selectedRegion",room);
        Update(app);game.toastTime=0;Set(app,"arrivalTime",0f);Set(app,"resumeTime",0f);Call(app,"Resize");
    }
    static void Open(FrontierRuntime app)
    { Check(app.Game.mode=="world","Local map begins in the real world");Key(app,KeyCode.M);Check(app.Game.mode=="map","Actual M opens the map");Click(app,OpenButton);Check(app.Game.mode=="landmarks","Actual local-map button opens named points"); }
    static void Select(FrontierRuntime app, WorldObject target)
    {
        var list=app.Game.DiscoveredLandmarks();int index=list.IndexOf(target);Check(index>=0,"Target is a canonical discovered present place");int page=index/6;
        for(int attempts=0;Get<int>(app,"landmarkPage")!=page && attempts<20;attempts++)Click(app,Get<int>(app,"landmarkPage")<page?NextButton:PreviousButton);
        Check(Get<int>(app,"landmarkPage")==page,"Actual paging reaches the intended named place");Click(app,new Rect(279,225+(index%6)*64,535,54));
        Check(Get<int>(app,"selectedLandmark")==index && HasText(app.Game.L(target.name,target.en)),"Actual selection shows the correct canonical named place");
    }
    static void Pin(FrontierRuntime app, WorldObject target)
    { Open(app);Select(app,target);Click(app,SetButton);Check(app.Game.PinnedLandmark==target,"Only explicit confirm sets the canonical point");Key(app,KeyCode.Escape);Key(app,KeyCode.Escape);Check(app.Game.mode=="world","Two real back controls return to world"); }
    static void Stop() { Input.axes.Clear();Input.held.Clear(); }
    static void Refresh(FrontierRuntime app) { for(int i=0;i<36;i++)Update(app); }
    static void Walk(FrontierRuntime app, List<Point> path, WorldObject target)
    {
        var game=app.Game;int room=game.save.room;Check(path.Count>0,"The selected place has a nonempty actual route");
        foreach(Point waypoint in path)
        {
            Check(game.CanStand(waypoint),"Every suggested waypoint uses the unchanged live footprint");int steps=0;
            while((game.Position-waypoint).Length>.045f && steps++<220)
            {
                Point before=game.Position,direction=(waypoint-before).Normalized;
                Input.axes["Horizontal"]=direction.x;Input.axes["Vertical"]=-direction.y;Input.held.Clear();
                if(direction.x>.01f)Input.held.Add(KeyCode.D);if(direction.x<-.01f)Input.held.Add(KeyCode.A);if(direction.y>.01f)Input.held.Add(KeyCode.S);if(direction.y<-.01f)Input.held.Add(KeyCode.W);
                Update(app);walkingFrames++;
                Check(game.mode=="world" && game.save.room==room && game.CanStand(game.Position) && (game.Position-before).Length<.3f,"Actual held walking stays continuous and legal, without travel or automatic action");
            }
            Stop();Check(steps<220,"Actual held input reaches the next route waypoint");
        }
        Stop();Check(game.Nearest()==target,"The final feet select this exact named place under the actual E nearest-object rule");
    }
    static void SamePath(List<Point> a,List<Point> b,string message)
    { Check(a.Count==b.Count,message);for(int i=0;i<a.Count;i++)Check((a[i]-b[i]).Length<.001f,message); }
    public static void PinnedSealUsesActualManualInteraction(FrontierRuntime app)
    {
        Prepare(app,6);var game=app.Game;var seal=Object(game,"seal6");var herb=Object(game,"glassherb");string quest=game.save.trackedQuest;int coins=game.save.coins;var items=(int[])game.save.inventory.Clone();
        Check(game.Ability(1) && !game.Has("seal_6") && !game.Has("node_"+herb.id),"Authored Grove seal is closed, its original power is earned and adjacent mint remains ungathered");
        var old=game.NavigationPath(new Point(seal.x,seal.y));Check(old.Count>0 && (old[old.Count-1]-new Point(7.5f,19.5f)).Length<.001f,"Immutable authored repro: coordinate-only BFS ends at the competing mint's approach");
        Pin(app,seal);Refresh(app);var route=new List<Point>(Get<List<Point>>(app,"route"));
        Check(!game.Has("seal_6") && !game.Has("node_"+herb.id),"Selecting and waiting for the cached route do not open or gather anything");
        Walk(app,route,seal);Check(walkingFrames>25,"Arrival uses genuinely continuous held movement");
        Check(!game.Has("seal_6") && !game.Has("node_"+herb.id),"Arrival has no automatic E or exploration ability side effect");
        Key(app,KeyCode.E);Check(game.Has("seal_6") && !game.Has("node_"+herb.id),"Only actual E at the correct endpoint opens the original Grove route; mint remains untouched");
        Check(game.save.coins==coins && game.save.trackedQuest==quest && game.Has("interaction_route_mature_story"),"Navigation preserves story, reward ownership and original quest tracking");
        for(int i=0;i<items.Length;i++)Check(game.save.inventory[i]==items[i],"Walking and the original seal opening invent or consume no inventory");
        Console.WriteLine("PASS actual M/list/pin/cached Grove route, continuous WASD and manual seal E without adjacent mint gathering or automatic rewards; NATIVE_UNVERIFIED");
    }
    public static void WrongNeighborHasTruthfulArrivalAndLayout(FrontierRuntime app)
    {
        foreach(bool english in new[]{false,true})foreach(bool large in new[]{false,true})
        {
            Prepare(app,6);var game=app.Game;game.save.english=english;game.save.largeText=large;var seal=Object(game,"seal6");var herb=Object(game,"glassherb");
            Position(app,new Point(7.5f,19.5f));Check((game.Position-new Point(seal.x,seal.y)).Length<1.75f && game.Nearest()==herb,"Real close-enough coordinate still makes E choose the adjacent mint");
            Pin(app,seal);Refresh(app);Draw(app,"wrong-neighbor-world-"+(english?"en":"zh")+(large?"-large":"-normal"));
            Check(!HasText(game.L("已到达附近 · ","In reach · ")+"E"+game.L(" 交互"," to interact")),"Actual HUD must not claim that E can interact with a different nearby object");
            Check(Get<List<Point>>(app,"route").Count>0,"Actual route cache continues guiding to the requested seal even inside its distance radius");
            GUI.textureCalls.Clear();GUI.commands.Clear();Call(app,"DrawNavigation");Color mint=PixelArt.C("#7acabb");
            Check(GUI.textureCalls.Exists(t=>ColorIs(t.color,mint)),"The real pinned world destination retains its mature distinct mint rendering");
            Open(app);Select(app,seal);Draw(app,"wrong-neighbor-detail-"+(english?"en":"zh")+(large?"-large":"-normal"));
            Check(HasText(game.L("路线通往附近。到达后按 ","Follow the route, then press ")+"E"+game.L(" 交互。"," to interact.")),"Actual bilingual detail advises following the route instead of claiming false arrival");
            Check(!HasText(game.L("已在附近。返回地区，按 ","In reach. Return and press ")+"E"+game.L(" 交互。"," to interact.")),"Normal and large detail never advertise the competing object as the destination");
            Key(app,KeyCode.Escape);Key(app,KeyCode.Escape);Walk(app,Route(game,"InteractionPath",seal),seal);Refresh(app);Draw(app,"actual-arrival-world-"+(english?"en":"zh")+(large?"-large":"-normal"));
            Check(HasText(game.L("已到达附近 · ","In reach · ")+"E"+game.L(" 交互"," to interact")),"HUD advertises arrival only when actual E selects the intended object");
            Open(app);Select(app,seal);Check(HasText(game.L("已在附近。返回地区，按 ","In reach. Return and press ")+"E"+game.L(" 交互。"," to interact.")),"Detail reports true arrival with unchanged bilingual control copy");
        }
        Console.WriteLine("PASS competing-neighbor rejection, truthful actual HUD/detail arrival, retained mint witness and zh/en normal/large captures="+captureCount+"; NATIVE_UNVERIFIED");
    }
    public static void RealClosedRootsRefreshSamePin(FrontierRuntime app)
    {
        Prepare(app,GameCore.ReachRoom);var game=app.Game;var cache=Object(game,"reachCache");int coins=game.save.coins,shards=game.save.shards,stew=game.save.inventory[6];string quest=game.save.trackedQuest;
        Check(!game.ReachRootsOpen && !game.ReachCacheRecovered,"The original Reach topology starts with its closed northern roots and unclaimed cache");
        Pin(app,cache);Refresh(app);Check(Get<List<Point>>(app,"route").Count==0,"Actual cached route respects the closed root gate");
        Open(app);Select(app,cache);Check(HasText("No walking route now. Check tides, gates or bridges."),"Real detail explains the unreachable authored cache");Key(app,KeyCode.Escape);Key(app,KeyCode.Escape);
        Position(app,new Point(44,12));Key(app,KeyCode.R);Check(game.ReachRootsOpen && !game.ReachCacheRecovered,"Only actual R at the original root mechanism changes its topology");
        Position(app,new Point(game.Map.spawnX,game.Map.spawnY));Refresh(app);var route=new List<Point>(Get<List<Point>>(app,"route"));
        Check(route.Count>0 && game.PinnedLandmark==cache,"The same pin refreshes through the newly opened live topology");Walk(app,route,cache);
        Check(!game.ReachCacheRecovered && game.save.coins==coins && game.save.shards==shards && game.save.inventory[6]==stew,"Arrival alone never claims the original story reward");
        Key(app,KeyCode.E);Check(game.mode=="dialog" && game.conversation!=null && game.conversation.choices[0].action=="reach_cache" && !game.ReachCacheRecovered,"Manual E opens the original Investigate dialogue without skipping its confirmation");
        Draw(app);Key(app,KeyCode.Alpha1);Check(game.ReachCacheRecovered && game.save.coins==coins+20 && game.save.shards==shards+2 && game.save.inventory[6]==stew+1,"Actual original numbered Investigate confirmation grants the unchanged cache reward once");
        Check(game.save.trackedQuest==quest && game.Has("interaction_route_mature_story"),"Live topology refresh preserves mature story and original tracked quest");
        Console.WriteLine("PASS original blocked Reach roots, actual R, same-pin cached topology refresh, continuous legal arrival and original manual E cache reward; NATIVE_UNVERIFIED");
    }
    public static void SaveReloadAndGenericNavigationIsolation(FrontierRuntime app)
    {
        Prepare(app,6);var game=app.Game;var seal=Object(game,"seal6");Pin(app,seal);game.PrepareSave();Check(SaveStore.Write(game.save,0),"Actual checksum envelope persists the existing landmark pin");string warning;var loaded=SaveStore.Read(0,out warning);
        Check(loaded!=null && warning==null,"Production SaveStore reads the unchanged pin envelope");game.Load(loaded);game.Start();game.Close();game.enemies.Clear();Set(app,"camera",game.Position);Set(app,"arrivalTime",0f);Set(app,"resumeTime",0f);Set(app,"routeTimer",0f);Refresh(app);
        seal=Object(game,"seal6");Check(game.PinnedLandmark==seal && Get<List<Point>>(app,"route").Count>0,"Reload resolves saved pin identity to the current map's canonical seal");
        string before=JsonUtility.ToJson(game.save);int revision=game.revision;Point goal=new Point(seal.x,seal.y);
        for(int i=0;i<8;i++){Route(game,"InteractionPath",seal);Route(game,"GuidancePath",goal);game.NavigationTarget();game.DiscoveredLandmarks();}
        Check(before==JsonUtility.ToJson(game.save) && revision==game.revision,"Repeated route/status queries mutate no saved state, tracking, economy or revision");
        game.mode="landmarks";Check(game.ClearLandmarkPin(),"Existing explicit clear removes only the optional pin");game.Close();Point generic=new Point(12.5f,13.5f);
        Check(Array.Find(game.Map.objects,o=>(new Point(o.x,o.y)-generic).Length<.001f)==null,"Generic route fixture is not an authored interaction object");SamePath(game.NavigationPath(generic),Route(game,"GuidancePath",generic),"Generic coordinate goals preserve their original route");
        foreach(int version in new[]{1,2})
        {
            var legacy=JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(game.save));legacy.version=version;legacy.flags.RemoveAll(f=>f.StartsWith("landmark_pin:"));legacy.coins=137;legacy.flags.Add("supper_complete");
            File.WriteAllText(SaveStore.PathFor(2),JsonUtility.ToJson(legacy));var old=SaveStore.Read(2,out warning);Check(old!=null && warning==null,"Raw old save v"+version+" remains readable");game.Load(old);game.Start();game.Close();
            Check(!game.HasLandmarkPin && game.save.coins==137 && game.Has("supper_complete") && game.Has("interaction_route_mature_story"),"Old saves gain no waypoint, lose no money or mature story and require no schema migration");
        }
        Prepare(app,0);game=app.Game;game.StartPractice();Check(game.practicing && game.enemies.Count>0,"The mature three-stage practice still starts through its original guarded API");Point practice=game.NavigationTarget().Value;
        SamePath(game.NavigationPath(practice),Route(game,"GuidancePath",practice),"Practice enemy coordinate goals preserve the original path contract");game.EndPractice();
        Console.WriteLine("PASS actual checksum pin reload/canonical resolution, read-only path queries, raw legacy v1/v2 saves and unchanged generic/practice navigation; NATIVE_UNVERIFIED");
    }
    public static int Main(string[] args)
    {
        captures=args.Length>0 && args[0]!="-"?args[0]:null;string previous=Application.persistentDataPath;string directory=Path.Combine(Path.GetTempPath(),"starfall-interaction-routes-"+Guid.NewGuid());Application.persistentDataPath=directory;int failed=0;var app=new FrontierRuntime();
        try
        {
            Directory.CreateDirectory(directory);Call(app,"Awake");Check(app.Game!=null,"Production Awake loads the authored campaign");
            foreach(Action<FrontierRuntime> test in new Action<FrontierRuntime>[]{PinnedSealUsesActualManualInteraction,WrongNeighborHasTruthfulArrivalAndLayout,RealClosedRootsRefreshSamePin,SaveReloadAndGenericNavigationIsolation})
                try{test(app);}catch(Exception error){failed++;Stop();Console.WriteLine("FAIL "+test.Method.Name+": "+(error.InnerException??error));}
            app.EndVisualReview();Call(app,"OnDestroy");
        }
        finally{Application.persistentDataPath=previous;Input.Clear();GUI.queuedClick=null;GUI.matrix=Matrix4x4.identity;GUI.color=Color.white;GUI.enabled=true;if(Directory.Exists(directory))Directory.Delete(directory,true);}
        Console.WriteLine("Runtime Interaction Routes: "+(4-failed)+"/4 groups passed; held walking frames="+walkingFrames+"; NATIVE_UNVERIFIED");return failed==0?0:1;
    }
}
