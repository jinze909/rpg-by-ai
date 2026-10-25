// Real Update / OnGUI / navigation / SaveStore gathering-waypoint contracts.
// Offline declarations and command streams are NATIVE_UNVERIFIED.
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Starfall;
using UnityEngine;

public static class RuntimeGatheringLandmarkChecks
{
    const BindingFlags All=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
    static readonly Rect OpenButton=new Rect(913,692,304,30),PlacesButton=new Rect(279,178,253,34),GatherButton=new Rect(548,178,266,34);
    static readonly Rect PinButton=new Rect(866,572,351,42),ClearButton=new Rect(866,622,351,36),NextButton=new Rect(654,625,160,32),PreviousButton=new Rect(279,625,160,32);
    static string folder,prefix;static int captures,walking;
    [Serializable] public sealed class Label{public string k,text;public float[] r,m;public int size;public int[] padding;}
    static void Check(bool ok,string why){if(!ok)throw new Exception(why);}
    static object Call(object obj,string name,params object[] args){var method=obj.GetType().GetMethod(name,All);Check(method!=null,"Shipped method exists: "+name);return method.Invoke(obj,args);}
    static T Get<T>(object obj,string name){var field=obj.GetType().GetField(name,All);Check(field!=null,"Shipped field exists: "+name);return(T)field.GetValue(obj);}
    static void Set(object obj,string name,object value){obj.GetType().GetField(name,All).SetValue(obj,value);}
    static bool Near(float a,float b){return Math.Abs(a-b)<.002f;}
    static bool Same(Rect a,Rect b){return Near(a.x,b.x)&&Near(a.y,b.y)&&Near(a.width,b.width)&&Near(a.height,b.height);}
    static bool Same(Point? a,Point? b){return a.HasValue==b.HasValue&&(!a.HasValue||(a.Value-b.Value).Length<.002f);}
    static void Update(FrontierRuntime app){Call(app,"Update");Input.Clear();}
    static void Key(FrontierRuntime app,KeyCode key){Input.keys.Add(key);Update(app);}
    static void Draw(FrontierRuntime app,string capture=null)
    {
        GUI.commands.Clear();GUI.textureCalls.Clear();GUI.buttonCalls.Clear();Call(app,"OnGUI");
        if(folder!=null&&capture!=null){var commands=GUI.commands.ToArray();var textures=GUI.textureCalls.ToArray();var buttons=GUI.buttonCalls.ToArray();GUI.Export(folder,prefix+"-"+capture);captures++;GUI.commands.AddRange(commands);GUI.textureCalls.AddRange(textures);GUI.buttonCalls.AddRange(buttons);}
    }
    static List<Label> Labels(){var result=new List<Label>();foreach(string command in GUI.commands)if(command.StartsWith("{\"k\":\"text\"",StringComparison.Ordinal))result.Add(JsonUtility.FromJson<Label>(command));return result;}
    static bool Text(string value){return Labels().Exists(label=>label.text==value);}
    static GUI.ButtonCall Control(FrontierRuntime app,Rect rect,bool enabled=true)
    {Draw(app);var button=GUI.buttonCalls.Find(b=>Same(b.rect,rect));Check(button!=null&&button.enabled==enabled,"Actual gathering-landmark control has expected geometry/availability at "+rect.x+","+rect.y);return button;}
    static void Click(FrontierRuntime app,Rect rect){Control(app,rect);GUI.queuedClick=rect;Draw(app);Check(!GUI.queuedClick.HasValue,"Actual visible gathering-landmark control consumes its click");Draw(app);}
    static void Activate(FrontierRuntime app,Rect rect,bool pad=false)
    {Control(app,rect);var nav=Get<MenuNavigation>(app,"navigation");string id=(string)Call(app,"ControlId",rect.x,rect.y,rect.width,rect.height);int count=0;while(nav.focused!=id&&count++<80){Key(app,KeyCode.Tab);Draw(app);}Check(nav.focused==id&&nav.CanActivate(id),"Actual Tab reaches the selected enabled gathering control");Key(app,pad?KeyCode.JoystickButton0:KeyCode.Return);Draw(app);}
    // Reflection keeps immutable pre-feature production compilable. Every
    // group first asserts the actual new Gathering button before this API.
    static List<WorldObject> Nodes(GameCore game){return(List<WorldObject>)Call(game,"DiscoveredGatheringNodes");}
    static WorldObject Object(GameCore game,string kind){var found=Array.Find(game.Map.objects,o=>o.kind==kind);Check(found!=null,"Canonical map object exists: "+kind);return found;}
    static void Prepare(FrontierRuntime app,int room=1,bool explored=true)
    {
        typeof(RuntimeLandmarkChecks).GetMethod("Prepare",All).Invoke(null,new object[]{app,room,explored});var game=app.Game;
        game.enemies.Clear();game.projectiles.Clear();game.hazards.Clear();game.effects.Clear();game.fallen.Clear();game.save.companion=-1;
        game.save.flags.RemoveAll(f=>f.StartsWith("node_")||f.StartsWith("landmark_pin:"));game.save.health=53;game.save.energy=61;game.save.trackedQuest="main";
        game.toastTime=0;GUI.color=Color.white;GUI.matrix=Matrix4x4.identity;GUI.enabled=true;Event.current.type=EventType.Repaint;
        Set(app,"saveTimer",-1000f);Set(app,"lastRevision",game.revision);Call(app,"Resize");
    }
    static void Open(FrontierRuntime app,bool gathering=true,bool focused=false)
    {
        if(app.Game.mode!="world")app.Game.Close();Key(app,KeyCode.M);Check(app.Game.mode=="map","Actual M opens the mature world map");Click(app,OpenButton);Check(app.Game.mode=="landmarks","Actual local map action opens landmarks");
        // Baseline RED is a missing real control, never a new-API compile error.
        Control(app,GatherButton);Control(app,PlacesButton);Check(!Get<bool>(app,"gatheringLandmarks"),"Opening landmarks starts on the preserved Places view");
        if(gathering){if(focused)Activate(app,GatherButton,true);else Click(app,GatherButton);Check(Get<bool>(app,"gatheringLandmarks"),"Actual Gathering tab selects its explicit list");}
    }
    static void Select(FrontierRuntime app,WorldObject target,bool focused=false)
    {
        var list=Get<bool>(app,"gatheringLandmarks")?Nodes(app.Game):app.Game.DiscoveredLandmarks();int index=list.IndexOf(target);Check(index>=0,"Selection uses a canonical eligible member of the current filter");int count=0;
        while(Get<int>(app,"landmarkPage")<index/6&&count++<20)Click(app,NextButton);while(Get<int>(app,"landmarkPage")>index/6&&count++<20)Click(app,PreviousButton);
        Rect row=new Rect(279,225+index%6*64,535,54);if(focused)Activate(app,row);else Click(app,row);
        Check(Get<int>(app,"selectedLandmark")==index&&Text(app.Game.L(target.name,target.en)),"Actual list row selects the exact full resource/place name");
    }
    static void Pin(FrontierRuntime app,WorldObject target,int input=0)
    {Select(app,target,input==1);if(input>0)Activate(app,PinButton,input==2);else Click(app,PinButton);Check(ReferenceEquals(app.Game.PinnedLandmark,target),"Explicit Set pins exactly the selected canonical resource/place");}
    static void ToWorld(FrontierRuntime app){Key(app,KeyCode.Escape);Check(app.Game.mode=="map","Escape/B preserves local-map parent");Key(app,KeyCode.Escape);Check(app.Game.mode=="world","Map Back resumes the original world");}
    static void Walk(FrontierRuntime app,WorldObject target)
    {
        var game=app.Game;var path=game.InteractionPath(target);Check(path.Count>0||ReferenceEquals(game.Nearest(),target),"Selected resource has a real canonical interaction approach");int frames=0;
        while(!ReferenceEquals(game.Nearest(),target)&&frames++<5000)
        {
            while(path.Count>0&&(path[0]-game.Position).Length<.14f)path.RemoveAt(0);Check(path.Count>0,"An interaction waypoint remains until E actually owns the selected resource");Point move=(path[0]-game.Position).Normalized;
            Input.axes["Horizontal"]=move.x;Input.axes["Vertical"]=-move.y;if(move.x>.05f)Input.held.Add(KeyCode.D);if(move.x<-.05f)Input.held.Add(KeyCode.A);if(move.y>.05f)Input.held.Add(KeyCode.S);if(move.y<-.05f)Input.held.Add(KeyCode.W);
            Point before=game.Position;Update(app);walking++;Check(game.mode=="world"&&game.CanStand(game.Position)&&(game.Position-before).Length<.3f,"Actual held-WASD uses original speed/collision without teleporting");
        }
        Check(frames<5000&&ReferenceEquals(game.Nearest(),target),"Real walking ends with the intended resource as actual nearest E");
    }
    static void Yield(FrontierRuntime app,WorldObject target,bool pinClears)
    {
        var game=app.Game;int[] before=(int[])game.save.inventory.Clone();int coins=game.save.coins,shards=game.save.shards,xp=game.save.xp,seed=game.save.seed;string tracked=game.save.trackedQuest;WorldObject pin=game.PinnedLandmark;
        int bounty=game.save.bountyProgress,revision=game.revision;Key(app,KeyCode.E);int item=target.kind=="wood"?0:target.kind=="herb"?2:1;
        for(int index=0;index<8;index++)Check(game.save.inventory[index]==before[index]+(index==item||target.kind=="ore"&&index==4?3:0),"Original actual E resource yield stays exactly +3, with ore's original +3 parts only");
        Check(game.Has("node_"+target.id)&&game.save.trackedQuest==tracked&&game.save.coins==coins&&game.save.shards==shards&&game.save.xp==xp&&game.save.seed==seed,"Gathering preserves original depletion, quest tracking, currency/XP and RNG");
        Check(game.save.bountyProgress==bounty+(target.kind=="ore"&&game.save.bounty==1?1:0),"Only the original ore contract event is counted");
        Check(game.revision==revision+1+(target.kind=="ore"&&game.save.bounty==1?1:0),"Retiring a gathered waypoint adds no revision or extra reward event");
        Check(pinClears?!game.HasLandmarkPin&&game.PinnedLandmark==null:ReferenceEquals(game.PinnedLandmark,pin),"Only the selected resource's successful harvest retires its waypoint");
    }
    static void ActualFiltersPinWalkAndYield(FrontierRuntime app)
    {
        for(int input=0;input<3;input++)
        {
            Prepare(app);var game=app.Game;var target=Object(game,new[]{"wood","herb","ore"}[input]);game.save.bounty=input==2?1:-1;game.save.bountyProgress=0;string quest=game.save.trackedQuest;Point? questTarget=game.NavigationTarget();Open(app,true,input==2);
            Check(Nodes(game).Count==3&&game.DiscoveredLandmarks().TrueForAll(o=>o.kind!="wood"&&o.kind!="herb"&&o.kind!="ore"),"Three current explored resources use an explicit filter while mature Places stays places-only");
            string saved=JsonUtility.ToJson(game.save);Select(app,target,input==1);Check(JsonUtility.ToJson(game.save)==saved&&!game.HasLandmarkPin,"Selecting a resource does not silently pin/spend/reveal/replace quest tracking");
            Pin(app,target,input);Check(game.save.trackedQuest==quest&&Same(game.NavigationTarget(),new Point(target.x,target.y)),"Confirmed gathering waypoint overrides navigation without replacing the quest");ToWorld(app);Walk(app,target);Yield(app,target,true);
            Check(Same(game.NavigationTarget(),questTarget)&&game.save.trackedQuest==quest,"Successful selected harvest restores the original quest target immediately");
            Open(app);Check(!Nodes(game).Contains(target),"A gathered node disappears from Gathering rather than presenting an empty destination");Control(app,PinButton,Nodes(game).Count>0);ToWorld(app);
        }
        Check(walking>25,"Positive gathering arrival uses real walking frames");
        Console.WriteLine("PASS actual M/local map/Gathering filters, mouse/Tab-Enter/controller A resource selection and explicit pin, "+walking+" legal held-WASD frames, original wood/herb/ore E yields and ore contract event, selected-only pin retirement and unchanged quest restoration; NATIVE_UNVERIFIED");
    }

    static void DiscoveryDepletionOtherPinsAndPlaces(FrontierRuntime app)
    {
        Prepare(app,1,false);var game=app.Game;Open(app);Check(Nodes(game).Count==0,"Unexplored resources never leak into Gathering");Control(app,PinButton,false);game.save.explored[1]=16777215;Draw(app);Check(Nodes(game).Count==3,"Actual UI queries refresh only the newly discovered current-map resource sectors");
        WorldObject wood=Object(game,"wood"),herb=Object(game,"herb");game.save.flags.Add("node_"+herb.id);Draw(app);Check(Nodes(game).Count==2&&!Nodes(game).Contains(herb)&&!game.PinLandmark(herb),"Depleted nodes are excluded and cannot be forged as pins");
        var foreign=Array.Find(game.data.maps[2].objects,o=>o.kind=="wood");Check(!game.PinLandmark(foreign)&&!game.PinLandmark(new WorldObject{id=wood.id,kind=wood.kind,name=wood.name,en=wood.en,x=wood.x,y=wood.y}),"Foreign room and noncanonical cloned resource references are rejected");
        Pin(app,wood);string pinFlags=JsonUtility.ToJson(game.save.flags);Click(app,PlacesButton);Check(!Get<bool>(app,"gatheringLandmarks")&&Get<int>(app,"selectedLandmark")==0&&Get<int>(app,"landmarkPage")==0&&ReferenceEquals(game.PinnedLandmark,wood)&&JsonUtility.ToJson(game.save.flags)==pinFlags,"Filter switches reset only list focus/page and preserve the existing deliberate resource pin");
        Click(app,GatherButton);Check(Get<int>(app,"selectedLandmark")==0&&Get<int>(app,"landmarkPage")==0,"Gathering starts from a valid first page after switching");
        string tracked=game.save.trackedQuest;Click(app,ClearButton);Check(!game.HasLandmarkPin&&game.save.trackedQuest==tracked,"Original explicit Clear removes a gathering pin while retaining quest tracking");Pin(app,wood);
        ToWorld(app);var ore=Object(game,"ore");Walk(app,ore);Yield(app,ore,false);Check(ReferenceEquals(game.PinnedLandmark,wood),"Gathering another live node preserves the selected wood route");
        Open(app,false);var place=game.DiscoveredLandmarks()[0];Pin(app,place);ToWorld(app);Walk(app,wood);Yield(app,wood,false);Check(ReferenceEquals(game.PinnedLandmark,place),"Original place pins remain persistent when an unrelated resource is gathered");
        Prepare(app,GameCore.ReachRoom);game=app.Game;Open(app);Check(Nodes(game).Count==12,"Larger authored riverbank exposes its twelve active discovered nodes");var last=Nodes(game)[11];Select(app,last);Check(Get<int>(app,"landmarkPage")==1,"Original six-row pagination reaches the second Gathering page");Click(app,PlacesButton);Check(Get<int>(app,"landmarkPage")==0&&!game.HasLandmarkPin,"Changing a multipage filter invents no pin");
        Prepare(app,0);Open(app);Check(Nodes(app.Game).Count==0,"Town has no invented gathering markers");Control(app,PinButton,false);
        Console.WriteLine("PASS hidden/depleted/current-room/canonical resource gates, live refresh, twelve-node pagination, filter-only state changes, nonselected gathers preserving resource or mature place pins, and truthful empty-town controls; NATIVE_UNVERIFIED");
    }

    static string Files(){if(!Directory.Exists(Application.persistentDataPath))return "";var files=Directory.GetFiles(Application.persistentDataPath,"*",SearchOption.AllDirectories);Array.Sort(files,StringComparer.Ordinal);var text=new StringBuilder();foreach(string file in files)text.Append(file).Append(':').Append(Convert.ToBase64String(File.ReadAllBytes(file))).Append(';');return text.ToString();}
    static string Core(GameCore game){return(string)typeof(RuntimeSceneryCoverChecks).GetMethod("State",All).Invoke(null,new object[]{game});}
    static string WithoutToastCountdown(string state)
    {
        const string field="toastTime=";int start=state.IndexOf(field,StringComparison.Ordinal);Check(start>=0,"Complete model witness includes the existing presentation toast clock");int end=state.IndexOf(';',start);
        Check(end>start,"Toast clock has its own bounded serialized witness field");return state.Substring(0,start+field.Length)+"<existing-toast-countdown>"+state.Substring(end);
    }
    static string Difference(string expected,string actual)
    {int index=0;while(index<expected.Length&&index<actual.Length&&expected[index]==actual[index])index++;int begin=Math.Max(0,index-90);return " at "+index+" expected="+expected.Substring(begin,Math.Min(220,expected.Length-begin))+" actual="+actual.Substring(begin,Math.Min(220,actual.Length-begin));}
    static void Load(FrontierRuntime app,SaveData saved){app.Game.Load(saved);app.Game.Start();app.Game.Close();app.Game.enemies.Clear();Set(app,"selectedRegion",app.Game.save.room);Set(app,"camera",app.Game.Position);}
    static void LegacySaveAndPreparedActionPurity(FrontierRuntime app)
    {
        foreach(int version in new[]{1,2})
        {
            Prepare(app);var game=app.Game;Open(app);var wood=Object(game,"wood");Pin(app,wood);game.PrepareSave();game.save.version=version;Directory.CreateDirectory(Application.persistentDataPath);File.WriteAllText(SaveStore.PathFor(1),JsonUtility.ToJson(game.save));string warning;var old=SaveStore.Read(1,out warning);Check(old!=null&&warning==null,"Raw legacy v"+version+" is read by real SaveStore");Load(app,old);
            Check(game.save.version==2&&ReferenceEquals(game.PinnedLandmark,wood)&&game.Has("landmark_runtime_mature_story")&&game.save.trackedQuest=="main","Actual old-save migration restores a discovered active resource pin and all mature tracking/story");
            game.PrepareSave();Check(SaveStore.Write(game.save,0),"Actual checksum writer preserves resource pin");var saved=SaveStore.Read(0,out warning);Check(saved!=null&&warning==null,"Actual checksum reader restores the original flag namespace");Load(app,saved);game.Enter(0);Check(game.PinnedLandmark==null&&game.HasLandmarkPin,"Ordinary travel suspends a gathering pin in another region");game.Enter(1);game.enemies.Clear();Check(ReferenceEquals(game.PinnedLandmark,wood),"Ordinary return restores the same still-active canonical resource");
            game.save.flags.Add("node_"+wood.id);game.PrepareSave();Check(SaveStore.Write(game.save,0),"Actual exhausted-pin fixture is checksummed");Load(app,SaveStore.Read(0,out warning));Check(!game.HasLandmarkPin&&game.PinnedLandmark==null&&!game.save.flags.Exists(f=>f.StartsWith("landmark_pin:")),"Reload sanitizes an exhausted resource pin without regenerating the node");
        }
        foreach(bool flying in new[]{false,true})
        {
            Prepare(app);var game=app.Game;game.save.weapon=1;game.save.flags.Add("weapon_1");Check(app.SetVisualReviewPosition(new Point(13,13)),"Prepared bow fixture uses legal forest ground");game.aim=new Point(1,0);Set(app,"camera",game.Position);Key(app,KeyCode.J);var action=game.playerAction;Check(action!=null&&!action.released,"Actual J prepares the mature bow action");if(flying){int frames=0;while(!action.released&&frames++<50)Update(app);Check(action.released&&game.projectiles.Count>0,"Actual Update releases a real flying bow shot");}
            Open(app);GameCore review=Get<GameCore>(app,"reviewOriginal");Set(app,"reviewOriginal",null);
            try
            {
                Check(SaveStore.Write(game.save,0),"Purity fixture protects actual saved bytes without the review shortcut");
                // The actual tab click has an existing UI sound receipt that
                // Update consumes once. Settle it before comparing complete
                // game graphs; this frame still cannot advance combat.
                float acceptedElapsed=action.elapsed;Update(app);Draw(app);Check(action.elapsed==acceptedElapsed,"Consuming the original menu sound cannot advance an accepted attack");
                string state=WithoutToastCountdown(Core(game)),files=Files();float elapsed=action.elapsed,toast=game.toastTime;
                for(int count=0;count<8;count++)
                {
                    string raw=Core(game);Nodes(game);Draw(app);Check(Core(game)==raw&&Files()==files,"Resource queries and actual OnGUI alone preserve the raw complete Core graph and save bytes");
                    Update(app);toast-=Time.unscaledDeltaTime;string actual=WithoutToastCountdown(Core(game));Check(actual==state,"Menu Update preserves the full model apart from its original toast countdown"+(actual==state?"":Difference(state,actual)));
                    Check(Near(game.toastTime,toast)&&Files()==files&&ReferenceEquals(game.playerAction,action)&&action.elapsed==elapsed,"The original menu toast alone counts down while disk bytes and accepted action identity/clocks remain exact");
                }
                // Application pause deliberately saves through its mature
                // lifecycle. Protect the completed lifecycle state afterward.
                SaveData before=JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(game.save));Call(app,"OnApplicationPause",true);SaveData after=JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(game.save));after.savedAtUtc=before.savedAtUtc;after.snapshotRoom=before.snapshotRoom;after.worldEnemies=before.worldEnemies;after.savedRunes=before.savedRunes;
                Check(JsonUtility.ToJson(after)==JsonUtility.ToJson(before)&&ReferenceEquals(game.playerAction,action)&&action.elapsed==elapsed,"Original pause autosave may capture world/timestamp only, never alter resources/story or the accepted action");state=Core(game);files=Files();float clock=Get<float>(app,"clock");for(int count=0;count<6;count++){Nodes(game);Update(app);Draw(app);}Check(Core(game)==state&&Files()==files&&Get<float>(app,"clock")==clock,"Actual application pause freezes query/action clocks and protects the completed autosave");Call(app,"OnApplicationPause",false);
            }
            finally{Set(app,"reviewOriginal",review);}
        }
        Console.WriteLine("PASS real raw v1/v2 and checksum resource-pin roundtrips, travel suspension/return and depleted reload sanitation, complete read-only menu/query/disk graphs with prepared/flying real bow actions and original pause-save lifecycle; NATIVE_UNVERIFIED");
    }

    static bool Overlap(Rect a,Rect b){return a.x<b.x+b.width&&b.x<a.x+a.width&&a.y<b.y+b.height&&b.y<a.y+a.height;}
    static void BilingualLayoutsAndParentControls(FrontierRuntime app)
    {
        foreach(bool english in new[]{false,true})foreach(bool large in new[]{false,true})foreach(int width in new[]{960,1920})
        {
            Prepare(app,GameCore.ReachRoom);var game=app.Game;game.save.english=english;game.save.largeText=large;Screen.width=width;Screen.height=width==960?600:1080;Call(app,"Resize");Open(app);string suffix=(english?"en":"zh")+(large?"-large-":"-normal-")+width;
            Check(Text(game.L("地点","Places"))&&Text(game.L("采集","Gathering")),"Both explicit filter labels are visible in each language/text/screen policy");
            for(int page=0;page<2;page++)
            {
                if(page>0)Click(app,NextButton);for(int row=0;row<6;row++){var button=Control(app,new Rect(279,225+row*64,535,54));Check(button.rect.height>=button.style.fontSize,"All twelve canonical gathering rows retain full glyph-height selectable controls");}
                var nodes=Nodes(game);Select(app,nodes[page*6+5]);game.Notify("采集路标提示","Gathering waypoint notice");Draw(app,"gathering-page"+page+"-"+suffix);
                Check(Labels().Exists(label=>Near(label.r[0],278)&&Near(label.r[1],106)&&label.text.Contains(game.L(game.Map.name,game.Map.en))),"Existing real region name remains in the new page subtitle");
                var hint=Labels().Find(label=>Near(label.r[0],291)&&Near(label.r[1],670));Check(hint!=null,"Actual gathering page displays its yield/renewal hint");
                Rect toast=new Rect(382,706,516,67);Check(game.toastTime>0&&Text(game.L("采集路标提示","Gathering waypoint notice"))&&GUI.textureCalls.Exists(call=>Same(call.rect,toast)),"The original live menu toast remains actually drawn");
                Check(!Overlap(new Rect(hint.r[0],hint.r[1],hint.r[2],hint.r[3]),toast),"New yield/renewal hint does not overlap or require clearing the mature live toast");
            }
            var selected=Nodes(game)[11];Pin(app,selected,2);Draw(app,"pinned-"+suffix);string flags=JsonUtility.ToJson(game.save.flags);Click(app,PlacesButton);Draw(app,"places-"+suffix);Check(JsonUtility.ToJson(game.save.flags)==flags&&ReferenceEquals(game.PinnedLandmark,selected),"Bilingual Places switch preserves the deliberate gathering pin");
            Key(app,KeyCode.JoystickButton1);Check(game.mode=="map","Controller B keeps the mature map parent");Click(app,OpenButton);Check(!Get<bool>(app,"gatheringLandmarks"),"Reopening always resets the filter to Places");Key(app,KeyCode.JoystickButton4);Check(game.mode=="journal","Landmarks LB keeps the existing map sidebar destination");
            game.Close();Open(app);Key(app,KeyCode.JoystickButton5);Check(game.mode=="camp","Landmarks RB keeps the existing map sidebar destination");
            Prepare(app,0);game=app.Game;game.save.english=english;game.save.largeText=large;Screen.width=width;Screen.height=width==960?600:1080;Call(app,"Resize");Open(app);Control(app,PinButton,false);Draw(app,"empty-"+suffix);
        }
        Check(folder==null||captures==40,"This execution captures two Gathering pages, pin, Places and empty state in all eight policies");
        Console.WriteLine("PASS zh/en normal/large 960/1920 twelve-row pagination, explicit tabs and map caption, filter purity, controller B/LB/RB/reopen parent controls, truthful empty state and40 optional OnGUI captures; NATIVE_UNVERIFIED");
    }

    public static int Main(string[] args)
    {
        string previous=Application.persistentDataPath,directory=Path.Combine(Path.GetTempPath(),"starfall-gathering-landmarks-"+Guid.NewGuid());Application.persistentDataPath=directory;folder=args.Length>0&&args[0]!="-"?args[0]:null;prefix=args.Length>1?args[1]:"qa-gathering-landmark";var app=new FrontierRuntime();int failed=0;
        try{Call(app,"Awake");Check(app.Game!=null,"Actual Awake loads the full campaign");foreach(Action<FrontierRuntime> test in new Action<FrontierRuntime>[]{ActualFiltersPinWalkAndYield,DiscoveryDepletionOtherPinsAndPlaces,LegacySaveAndPreparedActionPurity,BilingualLayoutsAndParentControls})try{test(app);}catch(Exception error){failed++;Console.WriteLine("FAIL "+test.Method.Name+": "+(error.InnerException??error).Message);}app.EndVisualReview();Call(app,"OnDestroy");}
        finally{Application.persistentDataPath=previous;Input.Clear();GUI.queuedClick=null;GUI.color=Color.white;GUI.matrix=Matrix4x4.identity;GUI.enabled=true;if(Directory.Exists(directory))Directory.Delete(directory,true);}
        Console.WriteLine("Runtime gathering landmarks: "+(4-failed)+"/4 groups passed; NATIVE_UNVERIFIED");return failed==0?0:1;
    }
}
