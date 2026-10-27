// Real journal Update / OnGUI controls, canonical quest indices and SaveStore.
// Offline integration and layout evidence only: NATIVE_UNVERIFIED.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Starfall;
using UnityEngine;

public static class RuntimeQuestJournalChecks
{
    const BindingFlags All=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
    static readonly Rect Jump=new Rect(279,110,321,31),Track=new Rect(650,647,258,31),Destination=new Rect(927,647,263,31),Previous=new Rect(650,699,258,26),Next=new Rect(927,699,263,26);
    static string folder,prefix="qa-quest-journal";static int captures;
    [Serializable] public sealed class Label{public string k,text;public float[] r;}
    static void Check(bool ok,string why){if(!ok)throw new Exception(why);}
    static bool Near(float a,float b){return Math.Abs(a-b)<.005f;}
    static bool Same(Rect a,Rect b){return Near(a.x,b.x)&&Near(a.y,b.y)&&Near(a.width,b.width)&&Near(a.height,b.height);}
    static object Call(object obj,string method,params object[] args){var m=obj.GetType().GetMethod(method,All);Check(m!=null,"Actual production method exists: "+method);return m.Invoke(obj,args);}
    static object Helper(string method,params object[] args){return typeof(RuntimeQuestMapPreviewChecks).GetMethod(method,All).Invoke(null,args);}
    static T Get<T>(object obj,string name){var f=obj.GetType().GetField(name,All);Check(f!=null,"Existing runtime field: "+name);return(T)f.GetValue(obj);}
    static void Set(object obj,string name,object value){var f=obj.GetType().GetField(name,All);Check(f!=null,"Existing fixture field: "+name);f.SetValue(obj,value);}
    static Rect Filter(int filter){return new Rect(625+filter*201,110,189,31);}
    static Rect Tab(int tab){return new Rect(279+tab*238,172,222,37);}
    static Rect Row(int row,int count=23){float spacing=Math.Min(46,464f/Math.Min(count,18));return new Rect(279,226+row*spacing-(row==18?3:0),321,spacing-7);}
    static void Update(FrontierRuntime app){Call(app,"Update");Input.Clear();}
    static void Key(FrontierRuntime app,KeyCode key){Input.keys.Add(key);Update(app);}
    static void Draw(FrontierRuntime app,string capture=null)
    {
        GUI.commands.Clear();GUI.textureCalls.Clear();GUI.buttonCalls.Clear();Call(app,"OnGUI");
        if(folder!=null&&capture!=null){var c=GUI.commands.ToArray();var t=GUI.textureCalls.ToArray();var b=GUI.buttonCalls.ToArray();GUI.Export(folder,prefix+"-"+capture);captures++;GUI.commands.AddRange(c);GUI.textureCalls.AddRange(t);GUI.buttonCalls.AddRange(b);}
    }
    static List<Label> Labels(){var list=new List<Label>();foreach(string command in GUI.commands)if(command.StartsWith("{\"k\":\"text\"",StringComparison.Ordinal))list.Add(JsonUtility.FromJson<Label>(command));return list;}
    static bool Text(float x,float y,string value){return Labels().Exists(l=>Near(l.r[0],x)&&Near(l.r[1],y)&&l.text==value);}
    static bool Has(Rect rect){return GUI.buttonCalls.Exists(b=>Same(b.rect,rect));}
    static GUI.ButtonCall Control(FrontierRuntime app,Rect rect,bool enabled=true){Draw(app);var b=GUI.buttonCalls.Find(v=>Same(v.rect,rect));Check(b!=null&&b.enabled==enabled,"Actual control availability at "+rect.x+","+rect.y+" in "+app.Game.mode);return b;}
    static void Click(FrontierRuntime app,Rect rect){Control(app,rect);GUI.queuedClick=rect;Draw(app);Check(!GUI.queuedClick.HasValue,"Actual enabled control consumes its click");Draw(app);}
    static void Focus(FrontierRuntime app,Rect rect){Control(app,rect);var nav=Get<MenuNavigation>(app,"navigation");string id=(string)Call(app,"ControlId",rect.x,rect.y,rect.width,rect.height);int n=0;while(nav.focused!=id&&n++<130){Key(app,KeyCode.Tab);Draw(app);}Check(nav.focused==id&&nav.CanActivate(id),"Real Tab reaches the requested enabled journal control");}
    static void Activate(FrontierRuntime app,Rect rect,int device){if(device==0){Click(app,rect);return;}Focus(app,rect);Key(app,device==1?KeyCode.Return:KeyCode.JoystickButton0);Draw(app);Draw(app);}
    static void Prepare(FrontierRuntime app){Helper("Prepare",app);app.Game.save.flags.Add("quest_journal_mature_story");app.Game.save.companion=-1;app.Game.toastTime=0;Set(app,"lastRevision",app.Game.revision);}
    static void Open(FrontierRuntime app)
    {
        if(app.Game.mode!="world")app.Game.Close();Key(app,KeyCode.Tab);Draw(app);Check(app.Game.mode=="journal","Actual world Tab opens the established journal");
        // The old production must compile and fail here for its missing real
        // toolbar, before any newly introduced API or private field is read.
        Check(Has(Jump)&&Has(Filter(0))&&Has(Filter(1))&&Has(Filter(2)),"Quest journal needs visible Jump tracked and All/Available/Completed toolbar controls");
    }
    static void Guard(FrontierRuntime app){Prepare(app);Open(app);}
    static List<int> Expected(GameCore game,int filter)
    {var rows=new List<int>();for(int i=0;i<GameCore.QuestIds.Length;i++){var quest=game.GetQuest(GameCore.QuestIds[i]);if(filter!=1&&filter!=2||filter==1&&!quest.locked&&!quest.complete||filter==2&&quest.complete)rows.Add(i);}return rows;}
    static List<int> Api(GameCore game,int filter){var rows=new List<int>();foreach(object index in(IEnumerable)Call(game,"JournalQuestRows",filter))rows.Add((int)index);return rows;}
    static string Indices(List<int> rows){return string.Join(",",rows.ToArray());}
    static void AssertRows(FrontierRuntime app,int filter)
    {
        Draw(app);var game=app.Game;List<int> expected=Expected(game,filter);Check(Indices(Api(game,filter))==Indices(expected),"Core catalogue exposes exactly canonical indices for the live filter");int count=filter==0?GameCore.QuestIds.Length:expected.Count;int selected=Get<int>(app,"selectedQuest"),position=expected.IndexOf(selected);if(expected.Count==0){Check(!Has(Row(0)),"Empty filter cannot leave an unrelated quest row visible");Control(app,Track,false);Control(app,Destination,false);Check(!Labels().Exists(l=>Near(l.r[0],650)&&Near(l.r[1],281)&&Array.Exists(GameCore.QuestIds,id=>game.GetQuest(id).title==l.text)),"Empty filter cannot display stale unrelated quest details");return;}Check(position>=0,"Selected canonical quest belongs to the active filter");int page=position/19;for(int cell=0;cell<19;cell++){int index=page*19+cell;var button=GUI.buttonCalls.Find(b=>Same(b.rect,Row(cell,count)));if(index<expected.Count){Check(button!=null&&button.enabled,"Canonical quest retains the established row geometry and availability");string title=game.GetQuest(GameCore.QuestIds[expected[index]]).title;Check(Labels().Exists(l=>Near(l.r[0],Row(cell,count).x)&&Near(l.r[1],Row(cell,count).y)&&l.text.EndsWith(title,StringComparison.Ordinal)),"Visible filtered row maps to the authored global quest index "+expected[index]);}else Check(button==null,"Filtering cannot leave an old row below the current list");}QuestGuide q=game.GetQuest(GameCore.QuestIds[selected]);Check(Text(650,281,q.title),"Details describe the selected global quest, not its filtered-list position");Control(app,Track,!q.locked&&!q.complete);Control(app,Destination,!q.locked);
    }
    static void Select(FrontierRuntime app,int canonical,int filter,int device=0)
    {
        var rows=Expected(app.Game,filter);int index=rows.IndexOf(canonical);Check(index>=0,"Requested quest is part of the actual current filter");int attempts=0;while(rows.IndexOf(Get<int>(app,"selectedQuest"))/19<index/19&&attempts++<10)Click(app,Next);while(rows.IndexOf(Get<int>(app,"selectedQuest"))/19>index/19&&attempts++<10)Click(app,Previous);Activate(app,Row(index%19,filter==0?GameCore.QuestIds.Length:rows.Count),device);Check(Get<int>(app,"selectedQuest")==canonical,"Real row selection preserves global canonical selectedQuest index");AssertRows(app,filter);
    }
    static void FilterTo(FrontierRuntime app,int filter,int device=0){Activate(app,Filter(filter),device);AssertRows(app,filter);}
    static string Files(){if(!Directory.Exists(Application.persistentDataPath))return"";var paths=Directory.GetFiles(Application.persistentDataPath,"*",SearchOption.AllDirectories);Array.Sort(paths,StringComparer.Ordinal);var s=new StringBuilder();foreach(string p in paths)s.Append(p).Append(':').Append(Convert.ToBase64String(File.ReadAllBytes(p))).Append(';');return s.ToString();}
    static string State(GameCore game){return(string)Helper("State",game);}
    static string WithoutToast(string text){const string key="toastTime=";int start=text.IndexOf(key,StringComparison.Ordinal);Check(start>=0,"Full core snapshot includes original toast clock");int end=text.IndexOf(';',start);return text.Substring(0,start+key.Length)+"<existing-toast>"+text.Substring(end);}
    static void Pin(GameCore game){game.mode="landmarks";WorldObject host=game.DiscoveredLandmarks().Find(o=>o.kind=="forge");Check(host!=null&&game.PinLandmark(host),"Existing explored forge waypoint can remain independent of journal filters");game.Close();}
    static string PinFlags(GameCore game){return string.Join("|",game.save.flags.FindAll(f=>f.StartsWith("landmark_pin:")).ToArray());}
    static void EmptyCompleted(GameCore game){game.save.flags.RemoveAll(flag=>flag=="ally_1"||flag=="ally_2"||flag=="ally_3");Check(Expected(game,2).Count==0,"Fresh unfinished campaign has no completed quests");}

    static void ActualFiltersCanonicalRowsAndOtherTabs(FrontierRuntime app)
    {
        Guard(app);Check(GameCore.QuestIds.Length==23,"Fixture retains all twenty-three existing canonical quests");
        foreach(int device in new[]{0,1,2})
        {
            Prepare(app);var game=app.Game;Pin(game);Open(app);AssertRows(app,0);string save=JsonUtility.ToJson(game.save),files=Files(),pin=PinFlags(game);int revision=game.revision,generation=game.WorldGeneration;Click(app,Next);AssertRows(app,0);Check(Get<int>(app,"selectedQuest")==19,"Original Next page starts at canonical quest nineteen");Click(app,Previous);Check(Get<int>(app,"selectedQuest")==0,"Original Previous page returns to canonical zero");FilterTo(app,2,device);Check(Get<int>(app,"selectedQuest")==Expected(game,2)[0],"Hidden current selection falls back to first visible completed quest");FilterTo(app,1,device);Check(Get<int>(app,"selectedQuest")==Expected(game,1)[0],"Available filter excludes completed and locked stories and chooses a valid canonical selection");
            if(device>0){string id=(string)Call(app,"ControlId",Filter(1).x,Filter(1).y,Filter(1).width,Filter(1).height);Check(Get<MenuNavigation>(app,"navigation").focused==id,"Changing a filter preserves that control's actual keyboard/pad focus across its new input scope");Key(app,device==1?KeyCode.Return:KeyCode.JoystickButton0);Draw(app);AssertRows(app,1);Check(Get<MenuNavigation>(app,"navigation").focused==id,"Fresh Enter/A after filtering remains on the deliberately selected filter");}
            FilterTo(app,0,device);Select(app,5,0,device);if(device>0){Rect row=Row(5);string rowId=(string)Call(app,"ControlId",row.x,row.y,row.width,row.height);Check(Get<MenuNavigation>(app,"navigation").focused==rowId,"Selecting a row retains that row focus across the canonical-selection scope change");Key(app,device==1?KeyCode.Return:KeyCode.JoystickButton0);Draw(app);Check(Get<int>(app,"selectedQuest")==5&&Get<MenuNavigation>(app,"navigation").focused==rowId,"Fresh Enter/A after row selection acts on the same deliberate row");}Check(game.GetQuest(GameCore.QuestIds[5]).locked,"Canonical companion story is a genuine locked case");Control(app,Track,false);Control(app,Destination,false);Check(JsonUtility.ToJson(game.save)==save&&game.revision==revision&&game.WorldGeneration==generation&&Files()==files&&PinFlags(game)==pin,"Filtering, paging and selecting never accept, track, pin, travel, spend, discover or save");
            for(int tab=1;tab<4;tab++){Click(app,Tab(tab));Draw(app);Check(!Has(Jump)&&!Has(Filter(0))&&!Has(Filter(1))&&!Has(Filter(2)),"Quest toolbar appears only on the quest tab; original tab "+tab+" stays distinct");if(tab==1)Control(app,new Rect(279,650,221,38),false);if(tab==2)Check(Labels().Exists(l=>l.text.Contains(game.L(GameCore.FactionNames[0],GameCore.FactionNamesEn[0]))),"Original faction entries remain");}Click(app,Tab(0));AssertRows(app,0);Check(Get<int>(app,"selectedQuest")==5,"Visiting other tabs preserves the actual selected quest");Key(app,device==2?KeyCode.JoystickButton1:KeyCode.Escape);Check(game.mode=="world","Original journal Escape/B returns the world");
        }
        Prepare(app);Open(app);Check(Indices(Api(app.Game,-1))==Indices(Expected(app.Game,0))&&Indices(Api(app.Game,99))==Indices(Expected(app.Game,0)),"Invalid filter inputs safely return the complete canonical catalogue");Console.WriteLine("PASS actual Tab/mouse/Tab-Enter/pad-A filters and retained focus, all 23 canonical indices/19-row pagination, locked detail controls, independent quest/pin/economy and unchanged other tabs/Escape/B; NATIVE_UNVERIFIED");
    }
    static void LiveCompletionEmptyAndStaleConfirmations(FrontierRuntime app)
    {
        Guard(app);Prepare(app);var game=app.Game;EmptyCompleted(game);Open(app);FilterTo(app,2);string saved=JsonUtility.ToJson(game.save);Control(app,Track,false);Control(app,Destination,false);game.save.flags.Add("ally_2");AssertRows(app,2);Check(Get<int>(app,"selectedQuest")==3,"Live completion makes the new canonical completed row selectable in an already-open empty view");game.save.flags.Add("ally_1");AssertRows(app,2);Check(Get<int>(app,"selectedQuest")==3,"Adding an earlier completed quest preserves an existing visible selection");game.save.flags.Remove("ally_2");AssertRows(app,2);Check(Get<int>(app,"selectedQuest")==2,"A removed current completion selects the first remaining canonical row");game.save.flags.Remove("ally_1");AssertRows(app,2);Check(JsonUtility.ToJson(game.save)==saved,"Filtering/empty refresh makes none of its own permanent changes");
        foreach(KeyCode key in new[]{KeyCode.Return,KeyCode.JoystickButton0})foreach(int filter in new[]{1,2})
        {
            Prepare(app);game=app.Game;Open(app);FilterTo(app,filter);int selected=filter==1?0:4;Select(app,selected,filter);var original=Expected(game,filter);int row=filter==1?original.IndexOf(3):0;Check(row>=0,"Stale row fixture has a genuine original target");Focus(app,Row(row,original.Count));Key(app,key);Check(Get<string>(app,"confirmControl")!=null,"Actual Enter/A queues an existing filtered row before rendering");game.save.flags.Remove("ally_1");game.save.flags.Add("ally_2");var changed=Expected(game,filter);Check(changed.Count==original.Count&&changed.Contains(selected)&&changed[row]!=original[row],"Live flags replace row identity while both selected quest and row count stay constant");Draw(app);Check(Get<int>(app,"selectedQuest")==selected&&game.mode=="journal"&&game.save.trackedQuest=="main","Stale filtered row activation cannot select a different quest after a same-count membership swap");
        }
        foreach(KeyCode key in new[]{KeyCode.Return,KeyCode.JoystickButton0})foreach(string boundary in new[]{"selection","filter","tab","owner"})
        {
            Prepare(app);game=app.Game;Open(app);Focus(app,Track);Key(app,key);Check(Get<string>(app,"confirmControl")!=null,"Actual Enter/A queues a visible Track activation before OnGUI");string tracked=game.save.trackedQuest;
            if(boundary=="selection")Set(app,"selectedQuest",3);else if(boundary=="filter")GUI.queuedClick=Filter(2);else if(boundary=="tab")GUI.queuedClick=Tab(1);else{SaveData raw=JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(game.save));game.Load(raw);game.Close();game.mode="journal";}
            Draw(app);Check(game.mode=="journal"&&game.save.trackedQuest==tracked,"Stale Track activation cannot cross a changed journal "+boundary+" scope");GUI.queuedClick=null;
        }
        foreach(KeyCode key in new[]{KeyCode.Return,KeyCode.JoystickButton0})
        {
            Prepare(app);game=app.Game;Open(app);Focus(app,Destination);Key(app,key);Set(app,"selectedQuest",3);Draw(app);Check(game.mode=="journal"&&game.save.trackedQuest=="main","Stale destination confirmation cannot open a different canonical quest's map");
        }
        Console.WriteLine("PASS true empty completed state with disabled actions/no stale details, live completion/removal and selection repair, plus actual queued Enter/A guards across selection/filter/tab/owner and destination changes; NATIVE_UNVERIFIED");
    }
    static void JumpTrackedTrackAndExactDestination(FrontierRuntime app)
    {
        Guard(app);
        foreach(int device in new[]{0,1,2})foreach(string tracked in new[]{"riverlesson","ally1","personal0"})
        {
            Prepare(app);var game=app.Game;game.save.flags.Add("ally_2");game.save.trackedQuest=tracked;Pin(game);Open(app);int filter=tracked=="riverlesson"?2:1;FilterTo(app,filter,device);Check(!Expected(game,filter).Contains(Array.IndexOf(GameCore.QuestIds,tracked)),"Jump fixture actually hides the tracked unfinished/completed/locked task");string save=JsonUtility.ToJson(game.save),pin=PinFlags(game);Activate(app,Jump,device);AssertRows(app,0);int index=Array.IndexOf(GameCore.QuestIds,tracked);Check(Get<int>(app,"selectedQuest")==index&&Text(650,281,game.GetQuest(tracked).title),"Jump selects the actual global tracked index and switches to All only when hidden");Check(JsonUtility.ToJson(game.save)==save&&PinFlags(game)==pin,"Jumping is read-only and cannot change tracked quest or independent pin");if(tracked=="riverlesson"){Check(index>=19,"Actual tracked river lesson lives on the established second page");Activate(app,Destination,device);Helper("AssertPreview",app,tracked,true);Check(game.mode=="map"&&game.save.trackedQuest==tracked&&PinFlags(game)==pin,"Jumped second-page map identifies the exact tracked story without replacing pin");Click(app,new Rect(279,690,300,30));Check(game.mode=="journal"&&Get<int>(app,"selectedQuest")==index,"Original explicit quest-map Back returns to exact selected canonical journal row");AssertRows(app,0);}
        }
        Prepare(app);var g=app.Game;g.save.flags.Add("ally_2");g.save.trackedQuest="riverlesson";Open(app);FilterTo(app,1);Activate(app,Jump,1);AssertRows(app,1);Check(Get<int>(app,"selectedQuest")==20,"Jump preserves Available when its tracked story is already visible");Select(app,21,1,2);QuestGuide q=g.GetQuest("watergarden");Check(!q.locked&&!q.complete,"Appended water-garden row is a genuinely available task");Activate(app,Track,2);Check(g.save.trackedQuest=="watergarden"&&Get<int>(app,"selectedQuest")==21,"Track uses canonical appended quest twenty-one, not its short filtered row number");Activate(app,Destination,1);Helper("AssertPreview",app,"watergarden",true);Click(app,new Rect(279,690,300,30));Check(g.mode=="journal"&&Get<int>(app,"selectedQuest")==21,"Quest map returns to canonical filtered task");AssertRows(app,1);
        Console.WriteLine("PASS real Jump tracked for hidden unfinished/completed/locked tasks, second-page canonical selection and exact map/Back, retained visible filter, plus filtered appended Track/Map actions using the correct global task; NATIVE_UNVERIFIED");
    }
    static void OwnerSavesPreparedAttackAndLocalizedLayouts(FrontierRuntime app)
    {
        Guard(app);
        foreach(string boundary in new[]{"checksum","v1","v2","newgame","review"})
        {
            Prepare(app);var game=app.Game;Pin(game);Open(app);FilterTo(app,2);game.PrepareSave();string raw=JsonUtility.ToJson(game.save),pin=PinFlags(game);Check(!raw.Contains("journalFilter")&&!raw.Contains("journalQuestFilter"),"Transient journal filter never enters the existing SaveData schema");if(boundary=="newgame"){game.NewGame(0);game.Close();game.SkipTutorial();}else if(boundary=="review"){app.EndVisualReview();app.BeginVisualReview(0,0);app.VisualReviewPaused=false;game=app.Game;}else{string warning;SaveData loaded;if(boundary=="checksum"){Check(SaveStore.Write(game.save,1),"Actual checksummed writer persists original journey");loaded=SaveStore.Read(1,out warning);}else{var legacy=JsonUtility.FromJson<SaveData>(raw);legacy.version=boundary=="v1"?1:2;File.WriteAllText(SaveStore.PathFor(1),JsonUtility.ToJson(legacy));loaded=SaveStore.Read(1,out warning);}Check(loaded!=null&&warning==null,"Actual SaveStore accepts raw/checksummed journal journey");game.Load(loaded);game.Close();Check(game.Has("quest_journal_mature_story")&&game.save.coins==123&&game.save.shards==9&&game.save.trackedQuest=="main"&&PinFlags(game)==pin,"Loading preserves mature story, economy, tracked task and pin");}Open(app);AssertRows(app,0);Check(Expected(game,0).Count==23,"New Game/Save ownership resets to full canonical All rather than an old transient filter");
        }
        Prepare(app);var paid=app.Game;paid.save.weapon=1;paid.save.flags.Add("weapon_1");paid.SetStyle(1);float paidCost=paid.Profile(1,1).energy;Key(app,KeyCode.J);ActorAction action=paid.playerAction;Check(action!=null&&!action.released&&paidCost==4&&Near(paid.save.energy,100-paidCost),"Actual J paid for a real prepared bow shot before journal entry");Open(app);FilterTo(app,1);Update(app);Draw(app);string world=WithoutToast(State(paid)),files=Files();float age=action.elapsed,time=paid.save.totalTime,energy=paid.save.energy,cooldown=paid.attackCooldown;for(int frame=0;frame<12;frame++){Input.held.Add(KeyCode.J);Input.held.Add(KeyCode.W);Input.axes["Horizontal"]=1;Input.buttonDown.Add(0);Update(app);Draw(app);Check(WithoutToast(State(paid))==world&&Files()==files,"Actual journal Update/OnGUI is read-only for the full paused paid-action/world/save graph except original toast countdown");}Check(ReferenceEquals(action,paid.playerAction)&&action.elapsed==age&&!action.released&&paid.save.totalTime==time&&paid.save.energy==energy&&paid.attackCooldown==cooldown,"Filters cannot pass through held attack/movement, release/refund a paid shot, advance cooldowns or world time");foreach(EventType type in new[]{EventType.Layout,EventType.Repaint}){Event.current.type=type;string state=State(paid);Draw(app);Check(State(paid)==state&&Files()==files,"Layout/Repaint alone never mutates Core or files");}Event.current.type=EventType.Repaint;
        foreach(bool english in new[]{false,true})foreach(bool large in new[]{false,true})foreach(int width in new[]{960,1280})
        {
            Prepare(app);var game=app.Game;game.save.english=english;game.save.largeText=large;game.save.flags.Add("ally_2");game.save.trackedQuest="riverlesson";Screen.width=width;Screen.height=width*5/8;Call(app,"Resize");Open(app);string suffix=(english?"en":"zh")+"-"+(large?"large":"normal")+"-"+width;AssertRows(app,0);Draw(app,"all-page0-"+suffix);Click(app,Next);AssertRows(app,0);Draw(app,"all-page1-"+suffix);FilterTo(app,1);Draw(app,"available-"+suffix);FilterTo(app,2);Draw(app,"completed-"+suffix);EmptyCompleted(game);Draw(app,"empty-"+suffix);AssertRows(app,2);game.save.flags.Add("ally_2");Activate(app,Jump,0);AssertRows(app,0);Check(Get<int>(app,"selectedQuest")==20,"Localized Jump reaches second-page river notes");Draw(app,"jump-journal-"+suffix);Click(app,Destination);Helper("AssertPreview",app,"riverlesson",true);Draw(app,"jump-map-"+suffix);Click(app,new Rect(279,690,300,30));Click(app,Tab(1));Draw(app,"records-"+suffix);
        }
        Check(folder==null||captures==64,"All eight language/size/viewport policies have eight fresh journal/empty/jump/map/records captures");Console.WriteLine("PASS actual raw-v1/v2/checksum and new Game/Save ownership resets, preserved paid bow/action/world clocks and full drawing purity, plus 64 zh/en normal/large 960/1280 journal/filter/empty/Jump/map/records layouts; NATIVE_UNVERIFIED");
    }
    public static int Main(string[] args)
    {
        string previous=Application.persistentDataPath,path=Path.Combine(Path.GetTempPath(),"starfall-quest-journal-"+Guid.NewGuid());Application.persistentDataPath=path;Directory.CreateDirectory(path);folder=args.Length>0&&args[0]!="-"?args[0]:null;if(args.Length>1)prefix=args[1];if(folder!=null)Directory.CreateDirectory(folder);var app=new FrontierRuntime();int failed=0;
        try{Call(app,"Awake");Check(app.Game!=null,"Actual Awake loads shipped campaign");foreach(Action<FrontierRuntime> test in new Action<FrontierRuntime>[]{ActualFiltersCanonicalRowsAndOtherTabs,LiveCompletionEmptyAndStaleConfirmations,JumpTrackedTrackAndExactDestination,OwnerSavesPreparedAttackAndLocalizedLayouts})try{test(app);}catch(Exception error){failed++;Console.WriteLine("FAIL "+test.Method.Name+": "+(error.InnerException??error));}}
        finally{app.EndVisualReview();Call(app,"OnDestroy");Application.persistentDataPath=previous;Input.Clear();GUI.queuedClick=null;GUI.matrix=Matrix4x4.identity;GUI.color=Color.white;GUI.enabled=true;Event.current.type=EventType.Repaint;if(Directory.Exists(path))Directory.Delete(path,true);}
        Console.WriteLine("Runtime quest journal: "+(4-failed)+"/4 groups passed; captures="+captures+"; NATIVE_UNVERIFIED");return failed==0?0:1;
    }
}
