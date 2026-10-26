// Real shipped Update, OnGUI and SaveStore paths; no native Editor or GPU acceptance.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using Starfall;
using UnityEngine;

public static class RuntimeQuestMapPreviewChecks
{
    const BindingFlags All=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
    static readonly Rect Destination=new Rect(927,647,263,31), NextPage=new Rect(927,699,263,26), Travel=new Rect(888,616,306,47);
    static string folder,prefix="qa-quest-map-preview";static int captures;
    [Serializable] public sealed class Label{public string k,text;public float[] r;}
    static void Check(bool good,string why){if(!good)throw new Exception(why);}
    static bool Near(float a,float b){return Math.Abs(a-b)<.005f;}
    static bool Same(Rect a,Rect b){return Near(a.x,b.x)&&Near(a.y,b.y)&&Near(a.width,b.width)&&Near(a.height,b.height);}
    static bool Same(Point? a,Point? b){return a.HasValue==b.HasValue&&(!a.HasValue||(a.Value-b.Value).Length<.005f);}
    static object Call(object obj,string name,params object[] args){var method=obj.GetType().GetMethod(name,All);Check(method!=null,"Actual shipped entry exists: "+name);return method.Invoke(obj,args);}
    static T Get<T>(object obj,string name){var field=obj.GetType().GetField(name,All);Check(field!=null,"Actual shipped field exists: "+name);return(T)field.GetValue(obj);}
    static void Set(object obj,string name,object value){var field=obj.GetType().GetField(name,All);Check(field!=null,"Actual shipped field exists: "+name);field.SetValue(obj,value);}
    static void Update(FrontierRuntime app){Call(app,"Update");Input.Clear();}
    static void Key(FrontierRuntime app,KeyCode key){Input.keys.Add(key);Update(app);}
    static void Draw(FrontierRuntime app,string capture=null)
    {
        GUI.commands.Clear();GUI.textureCalls.Clear();GUI.buttonCalls.Clear();Call(app,"OnGUI");
        if(folder!=null&&capture!=null){var c=GUI.commands.ToArray();var t=GUI.textureCalls.ToArray();var b=GUI.buttonCalls.ToArray();GUI.Export(folder,prefix+"-"+capture);captures++;GUI.commands.AddRange(c);GUI.textureCalls.AddRange(t);GUI.buttonCalls.AddRange(b);}
    }
    static List<Label> Labels(){var result=new List<Label>();foreach(string c in GUI.commands)if(c.StartsWith("{\"k\":\"text\"",StringComparison.Ordinal))result.Add(JsonUtility.FromJson<Label>(c));return result;}
    static bool HasText(string text){return Labels().Exists(l=>l.text==text);}
    static bool ContainsText(string text){return Labels().Exists(l=>l.text.Contains(text));}
    static bool PreviewTitle(string text){return Labels().Exists(l=>Near(l.r[0],888)&&Near(l.r[1],489)&&l.text==text);}
    static bool PreviewNext(string text){return Labels().Exists(l=>Near(l.r[0],888)&&Near(l.r[1],541)&&l.text==text);}
    static GUI.ButtonCall Control(FrontierRuntime app,Rect rect,bool enabled=true)
    {Draw(app);var found=GUI.buttonCalls.Find(b=>Same(b.rect,rect));Check(found!=null&&found.enabled==enabled,"Actual control availability at "+rect.x+","+rect.y+" in "+app.Game.mode);return found;}
    static void Click(FrontierRuntime app,Rect rect){Control(app,rect);GUI.queuedClick=rect;Draw(app);Check(!GUI.queuedClick.HasValue,"Actual displayed enabled control consumes mouse click");Draw(app);}
    static void Focus(FrontierRuntime app,Rect rect)
    {Control(app,rect);var nav=Get<MenuNavigation>(app,"navigation");string id=(string)Call(app,"ControlId",rect.x,rect.y,rect.width,rect.height);int steps=0;while(nav.focused!=id&&steps++<100){Key(app,KeyCode.Tab);Draw(app);}Check(nav.focused==id&&nav.CanActivate(id),"Actual Tab reaches the displayed destination control");}
    static void Activate(FrontierRuntime app,Rect rect,int device)
    {if(device==0){Click(app,rect);return;}Focus(app,rect);Key(app,device==1?KeyCode.Return:KeyCode.JoystickButton0);Draw(app);Draw(app);}
    static Rect Row(int cell){float spacing=Math.Min(46,464f/Math.Min(GameCore.QuestIds.Length,18));return new Rect(279,226+cell*spacing-(cell==18?3:0),321,spacing-7);}
    static void Prepare(FrontierRuntime app)
    {
        Input.Clear();GUI.queuedClick=null;GUI.enabled=true;GUI.color=Color.white;GUI.matrix=Matrix4x4.identity;Event.current.type=EventType.Repaint;Screen.width=1280;Screen.height=800;Time.unscaledDeltaTime=.02f;
        app.BeginVisualReview(0,0);app.VisualReviewPaused=false;var g=app.Game;g.NewGame(0);g.Close();g.SkipTutorial();g.save.stage=1;g.save.flags.Add("core_1");g.save.flags.Add("exp_intro");g.save.flags.Add("ally_1");g.save.flags.Add("ally_3");g.save.flags.Add("quest_preview_mature_story");g.save.companion=-1;g.save.english=true;g.save.largeText=false;g.save.showGuidance=true;g.save.reducedMotion=true;g.save.effectsQuality=0;g.save.trackedQuest="main";g.save.inventory[3]=0;g.save.coins=123;g.save.shards=9;g.save.xp=11;g.save.totalTime=100;g.toastTime=0;g.enemies.Clear();g.hazards.Clear();g.projectiles.Clear();g.effects.Clear();g.fallen.Clear();for(int i=0;i<g.save.explored.Length;i++)g.save.explored[i]=16777215;
        Set(app,"journalTab",0);Set(app,"selectedQuest",0);Set(app,"selectedRegion",-1);Set(app,"arrivalTime",0f);Set(app,"resumeTime",0f);Set(app,"combatHudTime",0f);Set(app,"applicationPaused",false);Set(app,"pointerSeen",false);Set(app,"controllerUsed",false);Set(app,"saveTimer",-1000f);Set(app,"lastRevision",g.revision);Set(app,"camera",g.Position);Call(app,"Resize");
    }
    static void SelectQuest(FrontierRuntime app,string id)
    {
        if(app.Game.mode!="world")app.Game.Close();Key(app,KeyCode.Tab);Check(app.Game.mode=="journal","Actual world Tab opens journal");int index=Array.IndexOf(GameCore.QuestIds,id);Check(index>=0,"Existing canonical story id is present");
        if(index/19>0){while(Get<int>(app,"selectedQuest")/19<index/19)Click(app,NextPage);}else if(Get<int>(app,"selectedQuest")>=19)Click(app,new Rect(650,699,258,26));Click(app,Row(index%19));Check(Get<int>(app,"selectedQuest")==index,"Actual journal row selects "+id);
    }
    static float Cell(FrontierRuntime app,int room){return(float)typeof(FrontierRuntime).GetMethod("MapCellSize",All).Invoke(null,new object[]{app.Game.data.maps[room],8.4f});}
    static bool Marker(FrontierRuntime app,Point target,int room)
    {float cell=Cell(app,room);return GUI.textureCalls.Exists(t=>t.texture==Texture2D.whiteTexture&&Same(t.rect,new Rect(888+target.x*cell-3,264+target.y*cell-3,7,7))&&t.color.a>0&&Near(t.color.r,235f/255)&&Near(t.color.g,197f/255)&&Near(t.color.b,139f/255));}
    static int Markers(){return GUI.textureCalls.FindAll(t=>t.texture==Texture2D.whiteTexture&&Near(t.rect.width,7)&&Near(t.rect.height,7)&&t.rect.x>=885&&t.rect.x<1200&&t.rect.y>=261&&t.rect.y<490).Count;}
    static void AssertPreview(FrontierRuntime app,string id,bool active=true)
    {
        var q=app.Game.GetQuest(id);Draw(app);Check(app.Game.mode=="map"&&Get<int>(app,"selectedRegion")==q.room,"Show destination selects actual authored quest region");
        // Semantic RED guard comes before any new helper or field is inspected.
        if(active)Check(Marker(app,q.target,q.room),"Selected quest's actual seven-pixel gold destination must be drawn, independent of tracked quest, local route or guidance preference: "+id);
        else Check(Markers()==0,"Completed/locked destination has no misleading objective marker");
        Check(PreviewTitle(q.title)&&PreviewNext(q.next),"Map explicitly identifies selected quest and its fresh next step: "+id);
    }
    static void Open(FrontierRuntime app,string id,int device=0){SelectQuest(app,id);Control(app,Destination,!app.Game.GetQuest(id).locked);Activate(app,Destination,device);AssertPreview(app,id,!app.Game.GetQuest(id).complete);}
    static string Files(){if(!Directory.Exists(Application.persistentDataPath))return"";var paths=Directory.GetFiles(Application.persistentDataPath,"*",SearchOption.AllDirectories);Array.Sort(paths,StringComparer.Ordinal);var s=new StringBuilder();foreach(string path in paths)s.Append(path).Append(':').Append(Convert.ToBase64String(File.ReadAllBytes(path))).Append(';');return s.ToString();}
    static void Append(StringBuilder text,object value,HashSet<object> seen)
    {if(value==null){text.Append("null;");return;}var type=value.GetType();if(type.IsPrimitive||type.IsEnum||value is string||value is decimal){text.Append(Convert.ToString(value,CultureInfo.InvariantCulture)).Append(';');return;}if(!type.IsValueType&&!seen.Add(value)){text.Append("ref;");return;}var seq=value as IEnumerable;if(seq!=null){foreach(object item in seq)Append(text,item,seen);text.Append("end;");return;}var fields=type.GetFields(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);Array.Sort(fields,(a,b)=>string.CompareOrdinal(a.Name,b.Name));foreach(var field in fields){text.Append(field.Name).Append('=');Append(text,field.GetValue(value),seen);}}
    static string State(GameCore g){var s=new StringBuilder();Append(s,g,new HashSet<object>());return s.ToString();}
    static void ActualLocalRemoteAndIndependentRoute(FrontierRuntime app)
    {
        foreach(string id in new[]{"ally2","shore"})foreach(bool guide in new[]{true,false})foreach(bool pinned in new[]{false,true})
        {
            Prepare(app);var g=app.Game;g.save.showGuidance=guide;WorldObject pin=null;
            if(pinned){g.mode="landmarks";pin=g.DiscoveredLandmarks().Find(o=>o.kind=="forge");Check(pin!=null&&g.PinLandmark(pin),"Existing canonical explored forge pin can be chosen");g.Close();}
            SelectQuest(app,id);QuestGuide q=g.GetQuest(id),tracked=g.CurrentGuide;Point? nav=g.NavigationTarget();string save=JsonUtility.ToJson(g.save),files=Files();int revision=g.revision,generation=g.WorldGeneration;Activate(app,Destination,0);AssertPreview(app,id);
            Check(JsonUtility.ToJson(g.save)==save&&g.revision==revision&&g.WorldGeneration==generation&&Files()==files,"Viewing a selected destination cannot save, travel, discover, track or pay");Check(g.save.trackedQuest=="main"&&Same(g.NavigationTarget(),nav)&&ReferenceEquals(g.PinnedLandmark,pin),"Actual local/remote preview retains deliberate tracked quest and independent personal pin");
            if(q.room==g.save.room&&nav.HasValue&&(nav.Value-q.target).Length>.1f)Check(!Marker(app,nav.Value,q.room),"Local preview must not paint tracked portal/personal waypoint instead of selected quest");
            Draw(app,"local-remote-"+id+"-guide"+guide+"-pin"+pinned);
        }
        Prepare(app);var danger=app.Game;danger.enemies.Add(new Enemy(65501,0,danger.Position.x,danger.Position.y,999){stun=1000,timer=1000});Open(app,"shore");Check(!danger.SafeTravel,"Nearby real hostile retains original fast-travel safety gate");Control(app,Travel,false);Draw(app,"nearby-hostile");AssertPreview(app,"shore");
        Prepare(app);Open(app,"ally2");var game=app.Game;Click(app,new Rect(478,224,156,80));Draw(app);Check(Get<int>(app,"selectedRegion")==1&&Markers()==0&&!PreviewTitle(game.GetQuest("ally2").title),"Browsing a different region never projects another region's selected quest or unrelated tracked marker there");
        Prepare(app);Open(app,"ally2");game=app.Game;game.save.flags.Add("ally_2");AssertPreview(app,"ally2",false);Check(ContainsText(game.GetQuest("ally2").next),"Completion refreshes destination explanation without phantom marker");
        Prepare(app);SelectQuest(app,"personal0");Check(app.Game.GetQuest("personal0").locked,"Unrecruited companion retains original story gate");Control(app,Destination,false);GUI.queuedClick=Destination;Draw(app);Check(app.Game.mode=="journal"&&GUI.queuedClick.HasValue,"Locked destination rejects mouse activation");GUI.queuedClick=null;
        Console.WriteLine("PASS actual selected local/remote destination markers independent of tracked quest, guidance toggle and canonical pins; read-only selection, region browsing, completion and locked story guards; NATIVE_UNVERIFIED");
    }
    static Rect BackControl(FrontierRuntime app)
    {Draw(app);var label=Labels().Find(l=>l.text==app.Game.L("返回任务手帐","Back to journal"));Check(label!=null,"Explicit Back to journal control is visible");return new Rect(label.r[0],label.r[1],label.r[2],label.r[3]);}
    static void InputDevicesBackAndOrdinaryMap(FrontierRuntime app)
    {
        Prepare(app);Open(app,"ally2"); // Semantic baseline must fail on actual absent selected destination first.
        foreach(int device in new[]{0,1,2})foreach(string back in new[]{"explicit","escape","pad"})
        {
            Prepare(app);var g=app.Game;Open(app,"watergarden",device);int row=Get<int>(app,"selectedQuest");string save=JsonUtility.ToJson(g.save);int revision=g.revision;Check(row>=19,"Back fixture really begins on an appended journal page");
            if(back=="explicit")Activate(app,BackControl(app),device);else Key(app,back=="escape"?KeyCode.Escape:KeyCode.JoystickButton1);Draw(app);
            Check(JsonUtility.ToJson(g.save)==save&&g.revision==revision,"Returning from preview neither tracks nor pays nor advances the story");
            if(back!="explicit"){Check(g.mode=="world","Original Escape/B resumes world from destination map");Key(app,KeyCode.Tab);Draw(app);}
            Check(g.mode=="journal"&&Get<int>(app,"selectedQuest")==row&&HasText(g.GetQuest("watergarden").title),"Mouse/Enter/gamepad A explicit return and Escape/B then reopening retain real second-page selection");
        }
        foreach(string exit in new[]{"M","menu","shoulder","travel","landmarks"})
        {
            Prepare(app);var g=app.Game;Open(app,"ally2");
            if(exit=="M"){Key(app,KeyCode.M);Check(g.mode=="world","M closes the visible map");Key(app,KeyCode.M);}
            else if(exit=="menu"){Key(app,KeyCode.I);Check(g.mode=="inventory","Ordinary I leaves preview for actual inventory");Key(app,KeyCode.M);}
            else if(exit=="shoulder"){Key(app,KeyCode.JoystickButton5);Check(g.mode=="camp","Original RB cycles to adjacent mature menu");Key(app,KeyCode.M);}
            else if(exit=="travel"){Click(app,Travel);Check(g.mode=="world","Existing Return to region remains a separate world action");Key(app,KeyCode.M);}
            else{Click(app,new Rect(913,692,304,30));Check(g.mode=="landmarks","Existing local landmark control remains usable");Key(app,KeyCode.Escape);}
            Draw(app);Check(g.mode=="map"&&!PreviewTitle(g.GetQuest("ally2").title),"Ordinary map reopening clears temporary quest preview context: "+exit);Key(app,KeyCode.Escape);Check(g.mode=="world","Ordinary map Back retains original world destination: "+exit);
        }
        Prepare(app);var game=app.Game;Open(app,"shore",2);int room=game.GetQuest("shore").room;Click(app,Travel);Check(game.mode=="world"&&game.save.room==room,"Fast travel still requires its distinct original map control");Key(app,KeyCode.M);Draw(app);Check(!PreviewTitle(game.GetQuest("shore").title),"Successful original travel cannot leak prior preview context");
        Console.WriteLine("PASS real mouse/Tab-Enter/gamepad-A destination and Back controls, original Escape/B world return and remembered second-page selection, ordinary M/menu/LB-RB/local-landmark exits and separately confirmed original fast travel; NATIVE_UNVERIFIED");
    }
    static void FreshTargetsPauseAndSessionOwners(FrontierRuntime app)
    {
        Prepare(app);Open(app,"ally2");var g=app.Game;var old=g.GetQuest("ally2");g.save.inventory[3]=3;Draw(app);var fresh=g.GetQuest("ally2");Check(ContainsText(fresh.next)&&fresh.next!=old.next,"Open map recomputes the current story step after inventory progress");AssertPreview(app,"ally2");
        Prepare(app);Open(app,"shore");g=app.Game;old=g.GetQuest("shore");g.save.harborPledge=1;Draw(app);fresh=g.GetQuest("shore");Check(fresh.room==old.room&&(fresh.target-old.target).Length>.1f,"Actual saved harbor pledge advances the selected shore objective to a different live relay");AssertPreview(app,"shore");Check(!Marker(app,old.target,old.room),"Fresh quest target replaces stale captain marker");g.save.flags.Add("harbor_report");Draw(app);fresh=g.GetQuest("shore");Check(fresh.room==6&&Get<int>(app,"selectedRegion")==5&&Markers()==0&&!PreviewTitle(fresh.title),"Story advancement to another region does not silently jump the inspected map or retain stale marker");Click(app,new Rect(289,532,156,80));AssertPreview(app,"shore");
        Prepare(app);g=app.Game;g.save.weapon=1;g.save.flags.Add("weapon_1");Key(app,KeyCode.J);ActorAction action=g.playerAction;Check(action!=null&&!action.released,"Real J prepares original bow attack before journal pause");Open(app,"ally2");string state=State(g),files=Files();
        foreach(EventType type in new[]{EventType.Layout,EventType.Repaint}){Event.current.type=type;Draw(app);Check(State(g)==state&&Files()==files,"Selected map Layout/Repaint is fully read-only for save, prepared actions, world and disk");}Event.current.type=EventType.Repaint;
        float age=action.elapsed,time=g.save.totalTime,cooldown=g.attackCooldown;for(int frame=0;frame<30;frame++){Input.held.Add(KeyCode.J);Input.held.Add(KeyCode.W);Input.axes["Horizontal"]=1;Input.buttonDown.Add(0);Update(app);Draw(app);}Check(ReferenceEquals(g.playerAction,action)&&action.elapsed==age&&!action.released&&g.save.totalTime==time&&g.attackCooldown==cooldown,"Preview holds original world/action/cooldown clocks; held attacks, movement and mouse input cannot pass through");
        foreach(KeyCode confirm in new[]{KeyCode.Return,KeyCode.JoystickButton0})
        {
            Prepare(app);Open(app,"shore");g=app.Game;Focus(app,Travel);Key(app,confirm);Check(Get<string>(app,"confirmControl")!=null,"Actual Enter/A queues visible original fast-travel confirmation before next OnGUI");g.Enter(1);g.enemies.Clear();g.mode="map";Set(app,"selectedRegion",5);Draw(app);Check(g.mode=="map"&&g.save.room==1,"Stale confirmation cannot travel after owner generation changes between Update and OnGUI");
        }
        foreach(string boundary in new[]{"travel","checksum","legacy1","legacy2","newgame","practice","review"})
        {
            Prepare(app);g=app.Game;Open(app,"ally2");g.PrepareSave();string raw=JsonUtility.ToJson(g.save);Check(!raw.Contains("mapPreview")&&!raw.Contains("previewQuest"),"Existing persistent save schema receives no temporary UI quest preview state");
            if(boundary=="travel")g.Enter(1);
            else if(boundary=="newgame"){g.NewGame(0);g.Close();g.SkipTutorial();}
            else if(boundary=="practice"){g.Close();g.StartPractice();Check(g.practicing,"Actual safe practice starts");}
            else if(boundary=="review"){app.EndVisualReview();app.BeginVisualReview(1,0);app.VisualReviewPaused=false;g=app.Game;}
            else{string warning;SaveData loaded;if(boundary=="checksum"){Check(SaveStore.Write(g.save,1),"Actual checksum writer succeeds");loaded=SaveStore.Read(1,out warning);}else{var legacy=JsonUtility.FromJson<SaveData>(raw);legacy.version=boundary=="legacy1"?1:2;File.WriteAllText(SaveStore.PathFor(1),JsonUtility.ToJson(legacy));loaded=SaveStore.Read(1,out warning);}Check(loaded!=null&&warning==null,"Actual checksum/raw legacy reader succeeds");g.Load(loaded);g.Start();Check(g.Has("quest_preview_mature_story")&&g.save.coins==123&&g.save.shards==9&&g.save.trackedQuest=="main","Actual load keeps mature story, economy and deliberate tracked quest");}
            g.mode="map";Set(app,"selectedRegion",g.save.room);Draw(app);Check(!PreviewTitle(g.GetQuest("ally2").title),"Session owner boundary discards the prior temporary selection: "+boundary);
        }
        Console.WriteLine("PASS fresh live next steps, completely read-only Layout/Repaint, paused prepared attacks/world/cooldowns and no input pass-through, plus travel/checksum/raw-v1/v2/new-game/practice/review ownership boundaries without save-schema/story loss; NATIVE_UNVERIFIED");
    }
    static void LocalizedActualMapLayouts(FrontierRuntime app)
    {
        Prepare(app);Open(app,"ally2"); // Same baseline semantic guard as other groups.
        foreach(bool english in new[]{false,true})foreach(bool large in new[]{false,true})foreach(int width in new[]{960,1920})foreach(string id in new[]{"ally2","shore","watergarden","riverlesson"})
        {
            Prepare(app);var g=app.Game;g.save.english=english;g.save.largeText=large;if(id=="riverlesson")g.save.flags.Add("ally_2");Screen.width=width;Screen.height=width*5/8;Call(app,"Resize");Open(app,id);Draw(app,"layout-"+(english?"en":"zh")+"-"+(large?"large":"normal")+"-"+width+"-"+id);
            var q=g.GetQuest(id);Check(PreviewTitle(q.title)&&PreviewNext(q.next),"Actual localized map retains complete selected title and current next step");Rect back=BackControl(app);Check(back.x>=279&&back.y>=160&&back.y+back.height<=728&&back.x+back.width<=1217,"Explicit return stays inside established menu content above unchanged footer");
            var texts=Labels();foreach(var l in texts)if(Near(l.r[0],888)&&(l.text==q.title||l.text.Contains(q.next)))Check(l.r[0]>=279&&l.r[1]>=160&&l.r[0]+l.r[2]<=1217&&l.r[1]+l.r[3]<=728,"Actual selected quest information stays inside map card content bounds");
        }
        int canonical=0;foreach(bool english in new[]{false,true})foreach(string id in GameCore.QuestIds)
        {
            Prepare(app);var g=app.Game;g.save.english=english;g.save.largeText=true;Screen.width=960;Screen.height=600;Call(app,"Resize");if(g.GetQuest(id).locked)continue;Open(app,id);Draw(app,"canonical-"+(english?"en":"zh")+"-large960-"+id);canonical++;
        }
        Console.WriteLine("PASS 32 real zh/en normal/large 960/1920 local/remote/appended quest map layouts plus "+canonical+" canonical unlocked/completed large960 story captures, complete selected title/next-step content and explicit Back geometry; captures="+captures+"; NATIVE_UNVERIFIED");
    }
    public static int Main(string[] args)
    {
        string previous=Application.persistentDataPath,path=Path.Combine(Path.GetTempPath(),"starfall-quest-map-preview-"+Guid.NewGuid());Application.persistentDataPath=path;folder=args.Length>0&&args[0]!="-"?args[0]:null;if(args.Length>1)prefix=args[1];if(folder!=null)Directory.CreateDirectory(folder);var app=new FrontierRuntime();int failed=0;
        try{Call(app,"Awake");Check(app.Game!=null,"Actual Awake loads shipped campaign");foreach(Action<FrontierRuntime> test in new Action<FrontierRuntime>[]{ActualLocalRemoteAndIndependentRoute,InputDevicesBackAndOrdinaryMap,FreshTargetsPauseAndSessionOwners,LocalizedActualMapLayouts})try{test(app);}catch(Exception error){failed++;Console.WriteLine("FAIL "+test.Method.Name+": "+(error.InnerException??error));}}
        finally{app.EndVisualReview();Call(app,"OnDestroy");Application.persistentDataPath=previous;Input.Clear();GUI.queuedClick=null;if(Directory.Exists(path))Directory.Delete(path,true);}
        Console.WriteLine("Runtime quest map preview: "+(4-failed)+"/4 groups passed; NATIVE_UNVERIFIED");return failed==0?0:1;
    }
}
