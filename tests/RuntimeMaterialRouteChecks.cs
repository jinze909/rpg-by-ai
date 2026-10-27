// Actual Update / OnGUI / movement / original exchanges and real SaveStore paths.
// Offline integration evidence only: NATIVE_UNVERIFIED.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Starfall;
using UnityEngine;

public static class RuntimeMaterialRouteChecks
{
    const BindingFlags All=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
    static readonly Rect MapButton=new Rect(866,622,351,36), BackSources=new Rect(279,690,266,30), RouteButton=new Rect(559,690,275,30), Travel=new Rect(888,616,306,47);
    static readonly int[] Items={0,1,2,3,4,7};
    static string folder,prefix="qa-material-route"; static int captures;
    [Serializable] public sealed class Label {public string k,text;public float[] r;}
    static void Check(bool ok,string reason){if(!ok)throw new Exception(reason);}
    static bool Near(float a,float b){return Math.Abs(a-b)<.005f;}
    static bool Same(Rect a,Rect b){return Near(a.x,b.x)&&Near(a.y,b.y)&&Near(a.width,b.width)&&Near(a.height,b.height);}
    static object Call(object obj,string name,params object[] args){var m=obj.GetType().GetMethod(name,All);Check(m!=null,"Actual production method exists: "+name);return m.Invoke(obj,args);}
    static object Helper(string name,params object[] args){return typeof(RuntimeMaterialSourceChecks).GetMethod(name,All).Invoke(null,args);}
    static object QuestHelper(string name,params object[] args){return typeof(RuntimeQuestMapPreviewChecks).GetMethod(name,All).Invoke(null,args);}
    static T Get<T>(object obj,string name){var f=obj.GetType().GetField(name,All);Check(f!=null,"Production field exists: "+name);return(T)f.GetValue(obj);}
    static void Set(object obj,string name,object value){var f=obj.GetType().GetField(name,All);Check(f!=null,"Production field exists: "+name);f.SetValue(obj,value);}
    static int Item(object source){return Get<int>(source,"item");} static int Room(object source){return Get<int>(source,"room");} static WorldObject Target(object source){return Get<WorldObject>(source,"target");}
    static object Find(GameCore game,int item,string kind,int room){return Helper("Find",game,item,kind,room);}
    static List<object> Sources(GameCore game,int item){return(List<object>)Helper("Sources",game,item);}
    static void Update(FrontierRuntime app){Call(app,"Update");Input.Clear();}
    static void Key(FrontierRuntime app,KeyCode key){Input.keys.Add(key);Update(app);}
    static void Draw(FrontierRuntime app,string capture=null)
    {
        GUI.commands.Clear();GUI.textureCalls.Clear();GUI.buttonCalls.Clear();Call(app,"OnGUI");
        if(folder!=null&&capture!=null){var c=GUI.commands.ToArray();var t=GUI.textureCalls.ToArray();var b=GUI.buttonCalls.ToArray();GUI.Export(folder,prefix+"-"+capture);captures++;GUI.commands.AddRange(c);GUI.textureCalls.AddRange(t);GUI.buttonCalls.AddRange(b);}
    }
    static List<Label> Labels(){var list=new List<Label>();foreach(string command in GUI.commands)if(command.StartsWith("{\"k\":\"text\"",StringComparison.Ordinal))list.Add(JsonUtility.FromJson<Label>(command));return list;}
    static bool AtText(float x,float y,string text){return Labels().Exists(l=>Near(l.r[0],x)&&Near(l.r[1],y)&&l.text==text);}
    static bool HasControl(Rect rect){return GUI.buttonCalls.Exists(b=>Same(b.rect,rect));}
    static GUI.ButtonCall Control(FrontierRuntime app,Rect rect,bool enabled=true){Draw(app);var b=GUI.buttonCalls.Find(v=>Same(v.rect,rect));Check(b!=null&&b.enabled==enabled,"Actual control availability at "+rect.x+","+rect.y+" in "+app.Game.mode);return b;}
    static void Click(FrontierRuntime app,Rect rect){Control(app,rect);GUI.queuedClick=rect;Draw(app);Check(!GUI.queuedClick.HasValue,"Real enabled control consumes click");Draw(app);}
    static void Focus(FrontierRuntime app,Rect rect){Control(app,rect);var nav=Get<MenuNavigation>(app,"navigation");string id=(string)Call(app,"ControlId",rect.x,rect.y,rect.width,rect.height);int n=0;while(nav.focused!=id&&n++<120){Key(app,KeyCode.Tab);Draw(app);}Check(nav.focused==id&&nav.CanActivate(id),"Actual Tab reaches enabled source-route control");}
    static void Activate(FrontierRuntime app,Rect rect,int device){if(device==0){Click(app,rect);return;}Focus(app,rect);Key(app,device==1?KeyCode.Return:KeyCode.JoystickButton0);Draw(app);Draw(app);}
    static void Prepare(FrontierRuntime app,int room=1)
    {
        Helper("Prepare",app,room,true);var g=app.Game;g.save.flags.Add("material_route_mature_story");g.save.inventory[3]=3;g.TrackQuest("ally2");g.save.showGuidance=true;g.save.largeText=false;g.save.english=true;g.toastTime=0;Set(app,"lastRevision",g.revision);
    }
    static WorldObject OldPin(GameCore game)
    {
        WorldObject pin=Array.Find(game.Map.objects,o=>o.kind=="ore")??Array.Find(game.Map.objects,o=>o.kind=="garden");Check(pin!=null,"Fixture has an existing canonical independent local waypoint");game.mode="landmarks";Check(game.PinLandmark(pin),"Original explicit waypoint can be set before reviewing a source");game.Close();return pin;
    }
    static void Open(FrontierRuntime app,object source,int device=0)
    {
        Helper("Open",app,Item(source),0);Helper("Select",app,source,false);Activate(app,MapButton,device);AssertPreview(app,source);
    }
    static float Cell(FrontierRuntime app,int room){return(float)typeof(FrontierRuntime).GetMethod("MapCellSize",All).Invoke(null,new object[]{app.Game.data.maps[room],8.4f});}
    static bool ColorNear(Color a,float r,float g,float b){return Near(a.r,r)&&Near(a.g,g)&&Near(a.b,b)&&a.a>.99f;}
    static bool Marker(FrontierRuntime app,object source,bool mint=true)
    {
        var target=Target(source);float cell=Cell(app,Room(source));return GUI.textureCalls.Exists(t=>t.texture==Texture2D.whiteTexture&&Same(t.rect,new Rect(888+target.x*cell-3,264+target.y*cell-3,7,7))&&(mint?ColorNear(t.color,122f/255,202f/255,187f/255):ColorNear(t.color,235f/255,197f/255,139f/255)));
    }
    static bool DarkCenter(FrontierRuntime app,object source){var target=Target(source);float cell=Cell(app,Room(source));return GUI.textureCalls.Exists(t=>t.texture==Texture2D.whiteTexture&&Same(t.rect,new Rect(888+target.x*cell-1,264+target.y*cell-1,3,3))&&t.color.r<.2f&&t.color.g<.2f&&t.color.b<.25f);}
    static void AssertPreview(FrontierRuntime app,object source)
    {
        Draw(app);Check(app.Game.mode=="map"&&Get<int>(app,"selectedRegion")==Room(source),"Actual source map selects the canonical source region");
        // This semantic assertion precedes every new API/field lookup, so old
        // production must compile and fail for the real missing behavior.
        Check(Marker(app,source)&&DarkCenter(app,source),"Selected material source needs its own mint seven-pixel marker and dark center, independent of tracked quest and old pin");
        Check(AtText(888,489,app.Game.L(Target(source).name,Target(source).en)),"Actual map identifies the selected material source by its authored name");
        string method=Get<string>(source,"method");Check(Labels().Exists(l=>Near(l.r[0],888)&&Near(l.r[1],541)&&l.text.Contains(method)),"Actual selected-source detail describes its live method/status");
        Control(app,BackSources);Control(app,Travel);Control(app,new Rect(913,692,304,30),Get<int>(app,"selectedRegion")==app.Game.save.room);
    }
    static string Files(){if(!Directory.Exists(Application.persistentDataPath))return"";var paths=Directory.GetFiles(Application.persistentDataPath,"*",SearchOption.AllDirectories);Array.Sort(paths,StringComparer.Ordinal);var s=new StringBuilder();foreach(string p in paths)s.Append(p).Append(':').Append(Convert.ToBase64String(File.ReadAllBytes(p))).Append(';');return s.ToString();}
    static string State(GameCore game){return(string)typeof(RuntimeSceneryCoverChecks).GetMethod("State",All).Invoke(null,new object[]{game});}
    static string PinFlags(GameCore game){return string.Join("|",game.save.flags.FindAll(f=>f.StartsWith("landmark_pin:")).ToArray());}
    static void SelectRegion(FrontierRuntime app,int region)
    {
        int limit=0;while(Get<int>(app,"mapPage")<region/9&&limit++<10)Click(app,new Rect(685,633,149,24));while(Get<int>(app,"mapPage")>region/9&&limit++<10)Click(app,new Rect(279,633,148,24));int cell=region%9;Click(app,new Rect(289+cell%3*189,224+cell/3*154,156,80));Check(Get<int>(app,"selectedRegion")==region,"Actual authored region card changes inspected terrain");
    }
    static void AssertNoSourceContext(FrontierRuntime app)
    {Draw(app);Check(!HasControl(BackSources)&&!HasControl(RouteButton),"Ordinary map retains no source return/route controls after session exit");}
    static void SemanticGuard(FrontierRuntime app){Prepare(app);Open(app,Find(app.Game,0,"cook",0));}

    static void PurePreviewReturnAndIndependentTracking(FrontierRuntime app)
    {
        SemanticGuard(app);
        foreach(bool local in new[]{false,true})foreach(bool guidance in new[]{false,true})foreach(int device in new[]{0,1,2})
        {
            Prepare(app,local?0:1);var g=app.Game;WorldObject pin=OldPin(g);g.save.showGuidance=guidance;object source=Find(g,0,"cook",0);Helper("Open",app,0,0);Helper("Select",app,source,false);int page=Get<int>(app,"materialPage");string saved=JsonUtility.ToJson(g.save),files=Files();int generation=g.WorldGeneration,revision=g.revision;
            Activate(app,MapButton,device);AssertPreview(app,source);Check(JsonUtility.ToJson(g.save)==saved&&g.revision==revision&&Files()==files&&g.WorldGeneration==generation,"Source map opening changes only menu mode, never economy, discovery, actions, story, world or files");
            Check(g.save.trackedQuest=="ally2"&&ReferenceEquals(g.PinnedLandmark,pin),"Preview leaves deliberately tracked quest and existing waypoint unchanged");
            var quest=g.CurrentGuide;float cell=Cell(app,0);Check(!GUI.textureCalls.Exists(t=>Same(t.rect,new Rect(888+quest.target.x*cell-3,264+quest.target.y*cell-3,7,7))&&ColorNear(t.color,235f/255,197f/255,139f/255)),"Source-origin map suppresses the misleading unrelated tracked Old Tide marker");
            string settled=State(g);foreach(EventType type in new[]{EventType.Layout,EventType.Repaint}){Event.current.type=type;Draw(app);Check(State(g)==settled&&Files()==files,"Repeated real Layout/Repaint source map is read-only");}Event.current.type=EventType.Repaint;
            SelectRegion(app,local?1:2);Control(app,RouteButton,false);Activate(app,BackSources,device);Check(g.mode=="materials"&&Get<int>(app,"materialItem")==0&&Get<int>(app,"materialPage")==page,"Explicit source Back preserves item/page despite browsing other terrain");Check(AtText(860,283,g.L(Target(source).name,Target(source).en)),"Back restores exact selected canonical source");Check(g.save.trackedQuest=="ally2"&&ReferenceEquals(g.PinnedLandmark,pin),"Returning from map does not replace pin or tracked quest");
        }
        Prepare(app);var game=app.Game;var list=Sources(game,4);Check(list.Count>5,"Authored known ore sources require real pagination");object last=list[list.Count-1];Helper("Open",app,4,0);Helper("Select",app,last,false);int selected=Get<int>(app,"selectedMaterialSource"),lastPage=Get<int>(app,"materialPage");Click(app,MapButton);AssertPreview(app,last);Click(app,BackSources);Check(Get<int>(app,"selectedMaterialSource")==selected&&Get<int>(app,"materialPage")==lastPage&&AtText(860,283,game.L(Target(last).name,Target(last).en)),"Multipage return keeps exact material source identity rather than the first row");
        Console.WriteLine("PASS real local/remote source-specific map mint targets, exact multipage Back, mouse/Tab-Enter/pad-A, independent old pin/quest and complete read-only OnGUI/save/action graphs; NATIVE_UNVERIFIED");
    }
    static void RouteTransactionWalkingAndOriginalInteractions(FrontierRuntime app)
    {
        SemanticGuard(app);
        foreach(int device in new[]{0,1,2})
        {
            Prepare(app);var g=app.Game;OldPin(g);string old=PinFlags(g),tracked=g.save.trackedQuest;object source=Find(g,0,"cook",0);Open(app,source,device);string saved=JsonUtility.ToJson(g.save);float health=g.save.health,energy=g.save.energy;int coinsBefore=g.save.coins,shardsBefore=g.save.shards,xpBefore=g.save.xp;int[] inventoryBefore=(int[])g.save.inventory.Clone();int generation=g.WorldGeneration;
            Activate(app,RouteButton,device);Check(g.mode=="map"&&g.WorldGeneration==generation&&JsonUtility.ToJson(g.save)==saved&&PinFlags(g)==old,"First remote Go + waypoint request only reviews, without travel or replacement");Check(AtText(RouteButton.x,RouteButton.y,g.L("确认前往并设路标","Confirm travel + pin")),"Pending route has an explicit confirmation label");
            if(device==0)Click(app,RouteButton);else {Key(app,device==1?KeyCode.Return:KeyCode.JoystickButton0);Draw(app);Draw(app);}Check(g.mode=="world"&&g.save.room==0&&g.WorldGeneration==generation+1&&ReferenceEquals(g.PinnedLandmark,Target(source))&&g.save.trackedQuest==tracked,"Only second fresh remote confirmation performs original travel and replaces exact source waypoint");
            Check(g.save.health==health&&g.save.energy==energy&&g.save.coins==coinsBefore&&g.save.shards==shardsBefore&&g.save.xp==xpBefore&&string.Join(",",g.save.inventory)==string.Join(",",inventoryBefore),"Confirmed routing never grants supplies, heals or spends currency");
            Helper("Walk",app,Target(source));int timber=g.save.inventory[0],coins=g.save.coins;Key(app,KeyCode.E);Check(g.mode=="dialog"&&Array.Exists(g.conversation.choices,c=>c.action=="buywood"),"Real route walking and E still require original shop interaction");Helper("Choose",app,"buywood");Check(g.save.inventory[0]==timber+4&&g.save.coins==coins-g.ShopTimberCost&&g.save.trackedQuest==tracked,"Only original cook choice buys exactly four timber at reputation price");
        }
        Prepare(app);var local=app.Game;OldPin(local);object ore=Find(local,4,"ore",1);Open(app,ore);int beforeGeneration=local.WorldGeneration;Click(app,RouteButton);Check(local.mode=="world"&&local.WorldGeneration==beforeGeneration&&ReferenceEquals(local.PinnedLandmark,Target(ore)),"Local source waypoint is one explicit activation with no travel/world reset");Helper("Walk",app,Target(ore));int crystal=local.save.inventory[1],parts=local.save.inventory[4];Key(app,KeyCode.E);Check(local.save.inventory[1]==crystal+3&&local.save.inventory[4]==parts+3&&local.Has("node_"+Target(ore).id)&&!local.HasLandmarkPin,"Actual E retains +3 crystals/+3 parts and retires only the successfully gathered source pin");
        Prepare(app);var plain=app.Game;OldPin(plain);string oldPin=PinFlags(plain);object cook=Find(plain,0,"cook",0);Open(app,cook);Click(app,Travel);Check(plain.mode=="world"&&plain.save.room==0&&PinFlags(plain)==oldPin&&plain.PinnedLandmark==null&&plain.save.trackedQuest=="ally2","Original single-activation Fast travel remains plain travel, retaining old remote pin and quest");Key(app,KeyCode.M);AssertNoSourceContext(app);
        Prepare(app);var unsafeGame=app.Game;object remote=Find(unsafeGame,0,"cook",0);Open(app,remote);unsafeGame.enemies.Add(new Enemy(9876,0,unsafeGame.save.x+1,unsafeGame.save.y,20));Control(app,RouteButton,false);Control(app,Travel,false);Check(unsafeGame.save.room==1&&!unsafeGame.HasLandmarkPin,"Nearby real enemy locks both route and original fast travel without implicit route");
        Console.WriteLine("PASS distinct remote two-step/local single-step route confirmation, actual WASD/E shop purchase and ore yield, unchanged economy and original plain single-step Fast travel plus live enemy safety; NATIVE_UNVERIFIED");
    }
    static void LiveStateOwnershipStaleInputAndLegacySaves(FrontierRuntime app)
    {
        SemanticGuard(app);
        Prepare(app);var g=app.Game;object wood=Find(g,0,"wood",1);Open(app,wood);g.save.flags.Add("node_"+Target(wood).id);Draw(app);Control(app,RouteButton,false);Check(Labels().Exists(l=>Near(l.r[0],888)&&Near(l.r[1],541)&&l.text.Contains(g.L("已采完","Gathered"))),"Open source preview recomputes depletion and denies the outdated route");Click(app,BackSources);Check(g.mode=="materials"&&AtText(860,283,g.L(Target(wood).name,Target(wood).en)),"Depleted source remains a truthful selected source on return");
        foreach(KeyCode key in new[]{KeyCode.Return,KeyCode.JoystickButton0})foreach(string boundary in new[]{"region","generation","newpreview"})
        {
            Prepare(app);g=app.Game;OldPin(g);string pin=PinFlags(g);object cook=Find(g,0,"cook",0);Open(app,cook);Click(app,RouteButton);Focus(app,RouteButton);Key(app,key);Check(Get<string>(app,"confirmControl")!=null,"Actual Enter/A queues fresh pending route confirmation before OnGUI");
            if(boundary=="region")Set(app,"selectedRegion",2);
            else if(boundary=="generation"){g.Enter(0);g.enemies.Clear();g.mode="map";Set(app,"selectedRegion",0);}
            else {Call(app,"OpenMenu","inventory");g.Close();Open(app,Find(g,1,"ore",1));}
            int room=g.save.room,generation=g.WorldGeneration;Draw(app);Check(g.mode=="map"&&g.save.room==room&&g.WorldGeneration==generation&&PinFlags(g)==pin,"Stale Update confirmation cannot activate another source/region/world after "+boundary);
            if(boundary=="region"){SelectRegion(app,0);string saved=JsonUtility.ToJson(g.save);Click(app,RouteButton);Check(g.mode=="map"&&JsonUtility.ToJson(g.save)==saved,"Browsing a region cancels prior pending approval; returning requires first confirmation again");}
        }
        foreach(string exit in new[]{"M","Escape","padB","inventory","plaintravel","landmarks"})
        {
            Prepare(app);g=app.Game;object cook=Find(g,0,"cook",0);Open(app,cook);Click(app,RouteButton);
            if(exit=="M")Key(app,KeyCode.M);else if(exit=="Escape")Key(app,KeyCode.Escape);else if(exit=="padB")Key(app,KeyCode.JoystickButton1);else if(exit=="inventory")Key(app,KeyCode.I);else if(exit=="plaintravel")Click(app,Travel);else {SelectRegion(app,g.save.room);Click(app,new Rect(913,692,304,30));}
            if(exit=="Escape"||exit=="padB"||exit=="M")Check(g.mode=="world","Original map Escape/B/M returns world even with pending source route");if(g.mode!="world")g.Close();Key(app,KeyCode.M);AssertNoSourceContext(app);Check(!g.HasLandmarkPin&&g.save.trackedQuest=="ally2","Map exits cannot auto-apply pending source route");
        }
        foreach(string boundary in new[]{"checksum","legacy1","legacy2","newgame","practice","death","review"})
        {
            Prepare(app,boundary=="practice"?0:1);g=app.Game;OldPin(g);object cook=boundary=="practice"?Find(g,0,"wood",1):Find(g,0,"cook",0);Open(app,cook);Click(app,RouteButton);g.PrepareSave();string raw=JsonUtility.ToJson(g.save),pin=PinFlags(g);Check(!raw.Contains("materialRoute")&&!raw.Contains("mapMaterial"),"Transient route preview/pending approval never enters SaveData schema");
            if(boundary=="newgame"){g.NewGame(0);g.Close();g.SkipTutorial();}else if(boundary=="practice"){g.Close();g.StartPractice();Check(g.practicing,"Actual practice begins");}else if(boundary=="death"){g.Close();g.invulnerable=0;Call(g,"Hurt",99999f,"environment",null);Check(g.Dying,"Actual lethal damage begins death");}else if(boundary=="review"){app.EndVisualReview();app.BeginVisualReview(1,0);app.VisualReviewPaused=false;g=app.Game;}
            else{string warning;SaveData saved;if(boundary=="checksum"){Check(SaveStore.Write(g.save,1),"Actual checksum writer saves mature state");saved=SaveStore.Read(1,out warning);}else{var old=JsonUtility.FromJson<SaveData>(raw);old.version=boundary=="legacy1"?1:2;File.WriteAllText(SaveStore.PathFor(1),JsonUtility.ToJson(old));saved=SaveStore.Read(1,out warning);}Check(saved!=null&&warning==null,"Original raw/checksum save reader succeeds");g.Load(saved);g.Start();g.Close();Check(g.Has("material_route_mature_story")&&g.save.coins==313&&g.save.shards==17&&g.save.trackedQuest=="ally2"&&PinFlags(g)==pin,"Legacy/checksum load preserves original story/economy/quest/pin without resuming pending route");}
            g.mode="map";Set(app,"selectedRegion",g.save.room);Draw(app);AssertNoSourceContext(app);
        }
        Prepare(app);QuestHelper("Open",app,"shore",0);var quest=app.Game.GetQuest("shore");Draw(app);Check(AtText(888,489,quest.title)&&!HasControl(BackSources)&&!HasControl(RouteButton),"Existing quest destination preview keeps its own title and original Back contract");Key(app,KeyCode.Escape);Check(app.Game.mode=="world","Original quest-map Escape still returns world");
        Console.WriteLine("PASS live source depletion, stale Enter/A between Update-OnGUI and region/owner changes, original map/quest exits, no saved transient fields and actual checksum/raw-v1/v2/newgame/practice/death/review cleanup; NATIVE_UNVERIFIED");
    }
    static void BilingualScaledMapLayouts(FrontierRuntime app)
    {
        SemanticGuard(app);
        foreach(bool english in new[]{false,true})foreach(bool large in new[]{false,true})foreach(int width in new[]{960,1280})
        {
            string suffix=(english?"en":"zh")+"-"+(large?"large":"normal")+"-"+width;
            foreach(int item in Items)
            {
                Prepare(app);var g=app.Game;g.save.english=english;g.save.largeText=large;Screen.width=width;Screen.height=width*5/8;Call(app,"Resize");object source=item==0?Find(g,0,"cook",0):item==1?Find(g,1,"ore",GameCore.ReachRoom):item==2?Find(g,2,"herb",1):item==3?Find(g,3,"fish",0):item==4?Find(g,4,"ore",1):Find(g,7,"garden",0);Open(app,source);Draw(app,"item"+item+"-"+suffix);AssertPreview(app,source);Control(app,RouteButton);if(Room(source)==GameCore.ReachRoom)Check(Near(Cell(app,Room(source)),4.2f),"72x48 optional region source uses existing half-size map cells");
                foreach(var label in Labels())if((Near(label.r[0],888)&&label.r[1]>=489&&label.r[1]<=541)||(Near(label.r[1],690)&&(Near(label.r[0],279)||Near(label.r[0],559))))Check(label.r[0]>=279&&label.r[1]>=160&&label.r[0]+label.r[2]<=1217&&label.r[1]+label.r[3]<=728,"New map source/footer labels remain inside established content area");
            }
            Prepare(app);var game=app.Game;game.save.english=english;game.save.largeText=large;Screen.width=width;Screen.height=width*5/8;Call(app,"Resize");object wood=Find(game,0,"wood",1);Open(app,wood);game.save.flags.Add("node_"+Target(wood).id);Draw(app,"depleted-"+suffix);Control(app,RouteButton,false);
            Prepare(app);game=app.Game;game.save.english=english;game.save.largeText=large;Screen.width=width;Screen.height=width*5/8;Call(app,"Resize");object cook=Find(game,0,"cook",0);Open(app,cook);Click(app,RouteButton);Draw(app,"confirm-"+suffix);Control(app,BackSources);Control(app,Travel);Control(app,new Rect(913,692,304,30),Get<int>(app,"selectedRegion")==app.Game.save.room);
        }
        Check(folder==null||captures==64,"All six material categories plus live depleted/pending states have 64 fresh actual OnGUI captures");Console.WriteLine("PASS 64 actual zh/en normal/large 960/1280 source-map layouts, six materials, local/remote/72x48 targets, depleted/pending controls and all original map control geometry; NATIVE_UNVERIFIED");
    }
    public static int Main(string[] args)
    {
        string previous=Application.persistentDataPath,path=Path.Combine(Path.GetTempPath(),"starfall-material-route-"+Guid.NewGuid());Application.persistentDataPath=path;folder=args.Length>0&&args[0]!="-"?args[0]:null;if(args.Length>1)prefix=args[1];if(folder!=null)Directory.CreateDirectory(folder);var app=new FrontierRuntime();int failed=0;
        try{Call(app,"Awake");Check(app.Game!=null,"Actual Awake loads shipped campaign");foreach(Action<FrontierRuntime> test in new Action<FrontierRuntime>[]{PurePreviewReturnAndIndependentTracking,RouteTransactionWalkingAndOriginalInteractions,LiveStateOwnershipStaleInputAndLegacySaves,BilingualScaledMapLayouts})try{test(app);}catch(Exception error){failed++;Console.WriteLine("FAIL "+test.Method.Name+": "+(error.InnerException??error));}}
        finally{app.EndVisualReview();Call(app,"OnDestroy");Application.persistentDataPath=previous;Input.Clear();GUI.queuedClick=null;GUI.color=Color.white;GUI.matrix=Matrix4x4.identity;GUI.enabled=true;if(Directory.Exists(path))Directory.Delete(path,true);}
        Console.WriteLine("Runtime material route: "+(4-failed)+"/4 groups passed; captures="+captures+"; NATIVE_UNVERIFIED");return failed==0?0:1;
    }
}
