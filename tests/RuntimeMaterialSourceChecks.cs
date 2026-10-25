// Real Update / OnGUI / navigation / original interactions / SaveStore contracts.
// Explicit progression and geometry fixtures are offline: NATIVE_UNVERIFIED.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Starfall;
using UnityEngine;

public static class RuntimeMaterialSourceChecks
{
    const BindingFlags All=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
    static readonly Rect OpenButton=new Rect(947,687,246,32),PinButton=new Rect(866,572,351,38),MapButton=new Rect(866,622,351,36),BackButton=new Rect(866,670,351,28),NextButton=new Rect(654,625,160,32),PreviousButton=new Rect(279,625,160,32);
    static readonly int[] Items={0,1,2,3,4,7};
    static string folder,prefix;static int captures,walking;
    [Serializable] public sealed class Label{public string k,text;public float[] r,m;public int size;public int[] padding;}
    static void Check(bool ok,string why){if(!ok)throw new Exception(why);}
    static object Call(object obj,string name,params object[] args){var m=obj.GetType().GetMethod(name,All);Check(m!=null,"Shipped method exists: "+name);return m.Invoke(obj,args);}
    static T Get<T>(object obj,string name){var f=obj.GetType().GetField(name,All);Check(f!=null,"Shipped field exists: "+name);return(T)f.GetValue(obj);}
    static void Set(object obj,string name,object value){var f=obj.GetType().GetField(name,All);Check(f!=null,"Shipped field exists: "+name);f.SetValue(obj,value);}
    static bool Near(float a,float b){return Math.Abs(a-b)<.002f;}
    static bool Same(Rect a,Rect b){return Near(a.x,b.x)&&Near(a.y,b.y)&&Near(a.width,b.width)&&Near(a.height,b.height);}
    static bool Same(Point? a,Point? b){return a.HasValue==b.HasValue&&(!a.HasValue||(a.Value-b.Value).Length<.002f);}
    static Rect Bounds(Label l){return new Rect(l.r[0],l.r[1],l.r[2],l.r[3]);}
    static void Update(FrontierRuntime app){Call(app,"Update");Input.Clear();}
    static void Key(FrontierRuntime app,KeyCode key){Input.keys.Add(key);Update(app);}
    static void Draw(FrontierRuntime app,string capture=null)
    {
        GUI.commands.Clear();GUI.textureCalls.Clear();GUI.buttonCalls.Clear();Call(app,"OnGUI");
        if(folder!=null&&capture!=null){var c=GUI.commands.ToArray();var t=GUI.textureCalls.ToArray();var b=GUI.buttonCalls.ToArray();GUI.Export(folder,prefix+"-"+capture);captures++;GUI.commands.AddRange(c);GUI.textureCalls.AddRange(t);GUI.buttonCalls.AddRange(b);}
    }
    static List<Label> Labels(){var result=new List<Label>();foreach(string c in GUI.commands)if(c.StartsWith("{\"k\":\"text\"",StringComparison.Ordinal))result.Add(JsonUtility.FromJson<Label>(c));return result;}
    static bool Text(string value){return Labels().Exists(l=>l.text==value);}
    static GUI.ButtonCall Control(FrontierRuntime app,Rect rect,bool enabled=true)
    {Draw(app);var b=GUI.buttonCalls.Find(v=>Same(v.rect,rect));Check(b!=null&&b.enabled==enabled,"Actual material source control availability at "+rect.x+","+rect.y+" in "+app.Game.mode);return b;}
    static void Click(FrontierRuntime app,Rect rect){Control(app,rect);GUI.queuedClick=rect;Draw(app);Check(!GUI.queuedClick.HasValue,"Actual enabled control consumes click");Draw(app);}
    static void Activate(FrontierRuntime app,Rect rect,bool pad=false)
    {Control(app,rect);var nav=Get<MenuNavigation>(app,"navigation");string id=(string)Call(app,"ControlId",rect.x,rect.y,rect.width,rect.height);int n=0;while(nav.focused!=id&&n++<100){Key(app,KeyCode.Tab);Draw(app);}Check(nav.focused==id&&nav.CanActivate(id),"Actual Tab reaches the enabled material source control");Key(app,pad?KeyCode.JoystickButton0:KeyCode.Return);Draw(app);}
    // New API is reached only after the real new Inventory button assertion;
    // immutable production must compile and fail semantically, not by reflection.
    static List<object> Sources(GameCore g,int item){var result=new List<object>();foreach(object source in(IEnumerable)Call(g,"MaterialSources",item))result.Add(source);return result;}
    static int Room(object source){return Get<int>(source,"room");}static WorldObject Target(object source){return Get<WorldObject>(source,"target");}
    static bool Depleted(object source){return Get<bool>(source,"depleted");}
    static Rect Tab(int item){return new Rect(279+Array.IndexOf(Items,item)*158,177,148,42);}
    static object Find(GameCore g,int item,string kind,int room=-1){var source=Sources(g,item).Find(s=>Target(s).kind==kind&&(room<0||Room(s)==room));Check(source!=null,"Canonical eligible material source exists: "+item+"/"+kind+"/"+room);return source;}
    static void Prepare(FrontierRuntime app,int room=1,bool explored=true)
    {
        typeof(RuntimeLandmarkChecks).GetMethod("Prepare",All).Invoke(null,new object[]{app,room,explored});var g=app.Game;
        g.enemies.Clear();g.projectiles.Clear();g.hazards.Clear();g.effects.Clear();g.fallen.Clear();g.save.companion=-1;g.save.flags.RemoveAll(f=>f.StartsWith("node_")||f.StartsWith("landmark_pin:")||f.StartsWith("riverlesson_"));
        for(int i=0;i<g.save.explored.Length;i++)g.save.explored[i]=explored?16777215:0;
        g.save.health=53;g.save.energy=61;g.save.coins=313;g.save.shards=17;g.save.xp=9;g.save.trackedQuest="main";g.save.plantedAt=-1;g.toastTime=0;
        GUI.color=Color.white;GUI.matrix=Matrix4x4.identity;GUI.enabled=true;Event.current.type=EventType.Repaint;Set(app,"saveTimer",-1000f);Set(app,"lastRevision",g.revision);Call(app,"Resize");
    }
    static void Open(FrontierRuntime app,int item=0,int input=0)
    {
        if(app.Game.mode!="world")app.Game.Close();Key(app,KeyCode.I);Check(app.Game.mode=="inventory","Actual I opens original inventory");
        // Every test group first performs this assertion, before new APIs.
        Control(app,OpenButton);Check(Text(app.Game.L("材料来源","Material sources")),"New inventory entry has its real visible localized label");
        Click(app,new Rect(286+item%4*158,236+item/4*126,131,46));Check(Get<int>(app,"selectedItem")==item,"Original inventory cell selects material "+item);
        if(input>0)Activate(app,OpenButton,input==2);else Click(app,OpenButton);Check(app.Game.mode=="materials","Actual entry opens separate read-only materials menu");
        Check(Text(app.Game.L(app.Game.data.items[Items[Array.IndexOf(Items,item)<0?0:Array.IndexOf(Items,item)]].name,app.Game.data.items[Items[Array.IndexOf(Items,item)<0?0:Array.IndexOf(Items,item)]].en)),"Selected material retains actual authored item label");
    }
    static void Select(FrontierRuntime app,object source,bool focused=false)
    {
        int item=Get<int>(source,"item");Click(app,Tab(item));var list=Sources(app.Game,item);int index=list.FindIndex(s=>Room(s)==Room(source)&&ReferenceEquals(Target(s),Target(source)));Check(index>=0,"Only a canonical fresh source is selected");
        int turns=0;while(Get<int>(app,"materialPage")<index/5&&turns++<30)Click(app,NextButton);while(Get<int>(app,"materialPage")>index/5&&turns++<30)Click(app,PreviousButton);Check(Get<int>(app,"materialPage")==index/5,"Actual page controls reach the exact source row");Rect row=new Rect(279,241+index%5*72,535,62);if(focused)Activate(app,row);else Click(app,row);
        Check(Labels().Exists(l=>Near(l.r[0],860)&&Near(l.r[1],283)&&l.text==app.Game.L(Target(source).name,Target(source).en)),"Actual detail identifies the selected canonical source");
    }
    static void Pin(FrontierRuntime app,object source,int input=0)
    {Select(app,source,input==1);if(input>0)Activate(app,PinButton,input==2);else Click(app,PinButton);Check(ReferenceEquals(app.Game.PinnedLandmark,Target(source)),"Explicit source Set creates the existing canonical personal pin");Control(app,PinButton,false);}
    static void Resume(FrontierRuntime app){Key(app,KeyCode.Escape);Check(app.Game.mode=="inventory","Material Escape returns to its actual inventory parent");Key(app,KeyCode.Escape);Check(app.Game.mode=="world","Inventory Escape resumes world");}
    static void Walk(FrontierRuntime app,WorldObject target)
    {
        var g=app.Game;var path=g.InteractionPath(target);Check(path.Count>0||ReferenceEquals(g.Nearest(),target),"Actual canonical source has an E-owned walkable approach");int frames=0;
        while(!ReferenceEquals(g.Nearest(),target)&&frames++<7000)
        {
            while(path.Count>0&&(path[0]-g.Position).Length<.14f)path.RemoveAt(0);Check(path.Count>0,"A real route remains until the selected source owns E");Point move=(path[0]-g.Position).Normalized,before=g.Position;
            Input.axes["Horizontal"]=move.x;Input.axes["Vertical"]=-move.y;if(move.x>.05f)Input.held.Add(KeyCode.D);if(move.x<-.05f)Input.held.Add(KeyCode.A);if(move.y>.05f)Input.held.Add(KeyCode.S);if(move.y<-.05f)Input.held.Add(KeyCode.W);Update(app);walking++;
            Check(g.mode=="world"&&g.CanStand(g.Position)&&(g.Position-before).Length<.3f,"Actual WASD preserves collision and original continuous speed without teleporting");
        }
        Check(frames<7000&&ReferenceEquals(g.Nearest(),target),"Real walking ends at the source's actual nearest E interaction");
    }
    static void Choose(FrontierRuntime app,string action)
    {
        var g=app.Game;Check(g.mode=="dialog"&&g.conversation!=null,"Original dialogue owns choice "+action);int index=Array.FindIndex(g.conversation.choices,c=>c.action==action);Check(index>=0&&index<9,"Original visible dialogue exposes "+action);
        int pages=0;do{Draw(app);if((bool)Call(app,"DialogueChoicesVisible"))break;Key(app,KeyCode.Return);}while(++pages<40);Check((bool)Call(app,"DialogueChoicesVisible"),"Actual narrative pages precede choice");Key(app,(KeyCode)((int)KeyCode.Alpha1+index));
    }
    static void RealEntryPinWalkingAndHarvest(FrontierRuntime app)
    {
        foreach(int item in new[]{0,2,4})
        {
            Prepare(app);var g=app.Game;Open(app,item,item==0?0:item==2?1:2);string kind=item==0?"wood":item==2?"herb":"ore";object source=Find(g,item,kind,g.save.room);string tracked=g.save.trackedQuest;Point? quest=g.NavigationTarget();string before=JsonUtility.ToJson(g.save);Select(app,source);
            Check(JsonUtility.ToJson(g.save)==before&&!g.HasLandmarkPin,"Inspecting an actual source never spends, pins, reveals or replaces a quest");Pin(app,source,item==0?0:item==2?1:2);Check(g.save.trackedQuest==tracked&&Same(g.NavigationTarget(),new Point(Target(source).x,Target(source).y)),"Confirmed material pin leaves original tracked quest intact");Resume(app);Walk(app,Target(source));int[] stock=(int[])g.save.inventory.Clone();int coins=g.save.coins,shards=g.save.shards,xp=g.save.xp;Key(app,KeyCode.E);
            for(int i=0;i<8;i++)Check(g.save.inventory[i]==stock[i]+(kind=="wood"&&i==0||kind=="herb"&&i==2||kind=="ore"&&(i==1||i==4)?3:0),"Actual E keeps original +3 node yield; ore yields both crystals and parts");
            Check(g.Has("node_"+Target(source).id)&&!g.HasLandmarkPin&&g.save.coins==coins&&g.save.shards==shards&&g.save.xp==xp&&g.save.trackedQuest==tracked&&Same(g.NavigationTarget(),quest),"Only successful selected harvest retires its pin and restores unchanged quest navigation");Open(app,item);source=Find(g,item,kind,g.save.room);Check(Depleted(source),"Harvested known node remains a truthful depleted source");Select(app,source);Control(app,PinButton,false);Check(Labels().Exists(l=>l.text.Contains(g.L("已采集","Gathered"))),"Depleted source explains inn renewal");Resume(app);
        }
        foreach(int item in new[]{5,6}){Prepare(app);Open(app,item);Check(Sources(app.Game,0).Count>0,"Non-material inventory selection falls back to known timber sources");Control(app,Tab(0));}
        Check(walking>25,"Positive source arrival uses actual movement frames");Console.WriteLine("PASS actual I/item/material entry and mouse/Tab-Enter/controller A controls, "+walking+" legal held-WASD frames, real wood/herb/ore E yields, explicit pin retirement and unchanged quest restoration; NATIVE_UNVERIFIED");
    }
    static void RemoteMapAndOriginalEconomicInteractions(FrontierRuntime app)
    {
        Prepare(app);var g=app.Game;Open(app,0);object cook=Find(g,0,"cook",0);Select(app,cook);Control(app,PinButton,false);string save=JsonUtility.ToJson(g.save);int generation=g.WorldGeneration;Click(app,MapButton);Check(g.mode=="map"&&Get<int>(app,"selectedRegion")==0&&g.save.room==1&&g.WorldGeneration==generation&&JsonUtility.ToJson(g.save)==save&&!g.HasLandmarkPin,"Show region map only selects the existing map; it never travels, pins, buys or changes the quest");
        Click(app,new Rect(888,616,306,47));Check(g.mode=="world"&&g.save.room==0,"Travel requires the original separate Fast travel confirmation");g.enemies.Clear();Open(app,0);cook=Find(g,0,"cook",0);Check(Get<string>(cook,"detail").Contains(g.ShopTimberCost.ToString())&&Get<string>(cook,"detail").Contains("4"),"Cook source quote uses actual reputation timber cost and quantity");Pin(app,cook);Resume(app);Walk(app,Target(cook));int wood=g.save.inventory[0],coins=g.save.coins;Key(app,KeyCode.E);Check(g.mode=="dialog"&&g.conversation.choices.ExistsChoice("buywood"),"Only original cook E exposes timber purchase");Choose(app,"buywood");Check(g.save.inventory[0]==wood+4&&g.save.coins==coins-g.ShopTimberCost,"Original paid choice alone exchanges exactly four timber at actual quote");g.Close();
        Open(app,7);object garden=Find(g,7,"garden",0);Select(app,garden);Check(Get<string>(garden,"detail").Contains("5")&&Get<string>(garden,"detail").Contains("3")&&Get<string>(garden,"detail").Contains("60"),"Seed source explains original 5-coin three-seed purchase and 60-second growth");Pin(app,garden);Resume(app);Walk(app,Target(garden));int seeds=g.save.inventory[7];coins=g.save.coins;Key(app,KeyCode.E);Choose(app,"seeds");Check(g.save.inventory[7]==seeds+3&&g.save.coins==coins-5,"Actual gardener still sells exactly three seeds for five coins");Key(app,KeyCode.E);Choose(app,"plant");Check(g.save.inventory[7]==seeds+2&&g.save.plantedAt>=0,"Original actual plant choice alone consumes one seed");
        Open(app,2);garden=Find(g,2,"garden",0);Select(app,garden);Check(Get<string>(garden,"detail").Contains("Ready in"),"Growing source is a live read-only maturity explanation");Resume(app);int herbs=g.save.inventory[2];seeds=g.save.inventory[7];coins=g.save.coins;for(int n=0;n<3020;n++)Update(app);Key(app,KeyCode.E);Choose(app,"harvest");Check(g.save.inventory[2]==herbs+5+g.save.projects[0]*2+(g.save.frontierEnding==1?2:0)&&g.save.inventory[7]==seeds+2&&g.save.coins==coins+8&&g.save.plantedAt<0,"Original elapsed-world crop harvest keeps exact herb/seed/coin benefits");
        Open(app,3);object fish=Find(g,3,"fish",0);Select(app,fish);Check(Get<string>(fish,"detail").Contains("Three good pulls")&&Get<string>(fish,"detail").Contains("+4"),"Fishing source describes original three-pull and four-coin reward");Pin(app,fish);Resume(app);Walk(app,Target(fish));int catches=g.save.inventory[3];coins=g.save.coins;Key(app,KeyCode.E);Choose(app,"fish");Check(g.mode=="fish","Real Old Tide choice alone casts into original lake minigame");for(int good=0;good<3;good++){int frames=0;while(Math.Abs(g.fishCursor-.5f)>.15f&&frames++<1000)Update(app);Check(frames<1000,"Actual lake clock reaches original green band");Key(app,KeyCode.E);}Check(g.mode=="world"&&g.save.inventory[3]==catches+1&&g.save.coins==coins+4,"Actual lake pulls retain original catch and coins without bait");
        Open(app,3);Check(!Sources(g,3).Exists(s=>Target(s).kind.StartsWith("riverFishing")),"Unaccepted river lesson sites are not disclosed");g.save.flags.Add("ally_2");g.save.flags.Add("riverlesson_accept");Draw(app);Check(Sources(g,3).FindAll(s=>Target(s).kind.StartsWith("riverFishing")).Count==2,"Only accepted and explored real river sites join known fishing sources");
        Console.WriteLine("PASS remote source map preview without travel/payment/pin, separately confirmed mature Fast travel, original cook purchase, seed purchase/plant/elapsed harvest, actual three-pull lake catch and accepted-only river sources; NATIVE_UNVERIFIED");
    }

    static string Files(){if(!Directory.Exists(Application.persistentDataPath))return "";var paths=Directory.GetFiles(Application.persistentDataPath,"*",SearchOption.AllDirectories);Array.Sort(paths,StringComparer.Ordinal);var s=new StringBuilder();foreach(string p in paths)s.Append(p).Append(':').Append(Convert.ToBase64String(File.ReadAllBytes(p))).Append(';');return s.ToString();}
    static string Core(GameCore g){return(string)typeof(RuntimeSceneryCoverChecks).GetMethod("State",All).Invoke(null,new object[]{g});}
    static string WithoutToast(string s){const string f="toastTime=";int i=s.IndexOf(f,StringComparison.Ordinal);Check(i>=0,"Complete graph contains original toast clock");int end=s.IndexOf(';',i);Check(end>i,"Toast field has bounded witness");return s.Substring(0,i+f.Length)+"<existing-toast-countdown>"+s.Substring(end);}
    static void Load(FrontierRuntime app,SaveData saved){app.Game.Load(saved);app.Game.Start();app.Game.Close();app.Game.enemies.Clear();Set(app,"selectedRegion",app.Game.save.room);Set(app,"camera",app.Game.Position);}
    static void GatesLegacySavesAndPreparedPurity(FrontierRuntime app)
    {
        Prepare(app,1,false);var g=app.Game;Open(app,4);Check(Sources(g,4).Count==0,"Hidden sources do not leak from unexplored maps");Control(app,PinButton,false);Control(app,MapButton,false);for(int room=0;room<g.save.explored.Length;room++)g.save.explored[room]=16777215;
        g.save.stage=0;g.save.flags.RemoveAll(f=>f=="core_1"||f=="core_2"||f=="core_3"||f=="harbor_report"||f=="desert_report"||f=="exp_intro");Draw(app);foreach(int item in Items)foreach(object s in Sources(g,item))Check(g.CanEnter(Room(s)),"Discovered but currently locked maps cannot leak material sources");
        foreach(int version in new[]{1,2})
        {
            Prepare(app);g=app.Game;Open(app,4);object ore=Find(g,4,"ore",1);Pin(app,ore);g.PrepareSave();g.save.version=version;Directory.CreateDirectory(Application.persistentDataPath);File.WriteAllText(SaveStore.PathFor(1),JsonUtility.ToJson(g.save));string warning;var raw=SaveStore.Read(1,out warning);Check(raw!=null&&warning==null,"Real raw legacy v"+version+" reads");Load(app,raw);Check(g.save.version==2&&ReferenceEquals(g.PinnedLandmark,Target(ore))&&g.Has("landmark_runtime_mature_story")&&g.save.trackedQuest=="main","Legacy migration preserves earned source pin and mature story");
            g.PrepareSave();Check(SaveStore.Write(g.save,0),"Actual checksum writer persists original landmark namespace");Load(app,SaveStore.Read(0,out warning));Check(warning==null&&g.HasLandmarkPin,"Actual checksum reader restores source pin");g.Enter(0);g.enemies.Clear();Check(g.PinnedLandmark==null&&g.HasLandmarkPin,"Original travel suspends remote source pin");g.Enter(1);g.enemies.Clear();Check(ReferenceEquals(g.PinnedLandmark,Target(ore)),"Original return reactivates canonical source pin");g.save.flags.Add("node_"+Target(ore).id);g.PrepareSave();Check(SaveStore.Write(g.save,0),"Depleted pin saved through original checksum path");Load(app,SaveStore.Read(0,out warning));Check(!g.HasLandmarkPin&&g.PinnedLandmark==null,"Reload retires depleted node pin without adding resources");
        }
        foreach(bool flying in new[]{false,true})
        {
            Prepare(app);g=app.Game;g.save.weapon=1;g.save.flags.Add("weapon_1");Check(app.SetVisualReviewPosition(new Point(13,13)),"Prepared bow fixture uses legal actual ground");g.aim=new Point(1,0);Set(app,"camera",g.Position);Key(app,KeyCode.J);ActorAction action=g.playerAction;Check(action!=null&&!action.released,"Actual J accepts original prepared bow action");if(flying){int n=0;while(!action.released&&n++<50)Update(app);Check(action.released&&g.projectiles.Count>0,"Actual Update releases real flying shot");}Open(app,4);var original=Get<GameCore>(app,"reviewOriginal");Set(app,"reviewOriginal",null);
            try
            {
                Check(SaveStore.Write(g.save,0),"Purity fixture writes real protected save bytes");float elapsed=action.elapsed;Update(app);Draw(app);Check(action.elapsed==elapsed,"Settling original UI receipt cannot advance accepted attack");string state=WithoutToast(Core(g)),files=Files();float toast=g.toastTime;
                for(int n=0;n<8;n++){string raw=Core(g);foreach(int item in Items)Sources(g,item);Draw(app);Check(Core(g)==raw&&Files()==files,"Queries and actual OnGUI preserve exact full Core graph and disk");Update(app);toast-=Time.unscaledDeltaTime;Check(WithoutToast(Core(g))==state&&Near(g.toastTime,toast)&&Files()==files&&ReferenceEquals(g.playerAction,action)&&action.elapsed==elapsed,"Actual menu Update changes only original toast countdown; accepted actions/projectiles/resources/story/RNG remain exact");}
                Call(app,"OnApplicationPause",true);state=Core(g);files=Files();float clock=Get<float>(app,"clock");for(int n=0;n<6;n++){Sources(g,4);Update(app);Draw(app);}Check(Core(g)==state&&Files()==files&&Get<float>(app,"clock")==clock,"Application pause freezes source queries, all action clocks and completed autosave bytes");Call(app,"OnApplicationPause",false);
            }
            finally{Set(app,"reviewOriginal",original);}
        }
        Console.WriteLine("PASS unknown/locked/depleted source guards, real raw v1/v2 plus checksum pin roundtrips, travel/reload continuity and complete read-only query/OnGUI/save-byte/action graphs for prepared/flying bow and application pause; NATIVE_UNVERIFIED");
    }
    static bool Overlap(Rect a,Rect b){return a.x<b.x+b.width&&b.x<a.x+a.width&&a.y<b.y+b.height&&b.y<a.y+a.height;}
    static void BilingualLayoutsAndParentControls(FrontierRuntime app)
    {
        foreach(bool english in new[]{false,true})foreach(bool large in new[]{false,true})foreach(int width in new[]{960,1920})
        {
            Prepare(app,GameCore.ReachRoom);var g=app.Game;g.save.flags.Add("ally_2");g.save.flags.Add("riverlesson_accept");g.save.english=english;g.save.largeText=large;Screen.width=width;Screen.height=width==960?600:1080;Call(app,"Resize");Open(app,4);string suffix=(english?"en":"zh")+(large?"-large-":"-normal-")+width;
            foreach(int item in Items){Click(app,Tab(item));Control(app,Tab(item));Check(Text(g.L(g.data.items[item].name,g.data.items[item].en)),"Six actual authored material labels remain visible");Draw(app,"item"+item+"-"+suffix);}
            var list=Sources(g,4);Check(list.Count>5,"Real discovered ore sources require five-row pagination");Click(app,Tab(4));Click(app,NextButton);Control(app,PreviousButton);var last=list[Math.Min(list.Count-1,9)];Select(app,last);g.Notify("材料来源提示","Material source notice");Draw(app,"page1-"+suffix);
            Rect toast=new Rect(382,706,516,67);Check(Text(g.L("材料来源提示","Material source notice")),"Original live menu toast remains visible");foreach(Label label in Labels())if(Near(label.r[0],860)&&label.r[1]>=247&&label.r[1]<=487)Check(!Overlap(Bounds(label),toast),"New material detail labels never overlap preserved toast");
            Prepare(app,1,false);g=app.Game;g.save.english=english;g.save.largeText=large;Screen.width=width;Screen.height=width==960?600:1080;Call(app,"Resize");Open(app,4);Draw(app,"empty-"+suffix);Control(app,PinButton,false);Control(app,MapButton,false);Click(app,BackButton);Check(g.mode=="inventory","Visible Back returns to inventory");Open(app,7);Key(app,KeyCode.JoystickButton1);Check(g.mode=="inventory","Controller B preserves inventory parent");Open(app,3);Key(app,KeyCode.JoystickButton4);Check(g.mode=="help","Material LB switches from the inventory sidebar index");Open(app,2);Key(app,KeyCode.JoystickButton5);Check(g.mode=="loadout","Material RB switches from the inventory sidebar index");
        }
        Check(folder==null||captures==64,"Six material tabs, multipage and empty state are captured in all eight policies");Console.WriteLine("PASS six material categories, actual five-row pagination, zh/en normal/large 960/1920 text bounds, preserved live toast and Back/B/LB/RB parent routing with 64 optional OnGUI captures; NATIVE_UNVERIFIED");
    }
    public static int Main(string[] args)
    {
        string prior=Application.persistentDataPath,directory=Path.Combine(Path.GetTempPath(),"starfall-material-source-"+Guid.NewGuid());Application.persistentDataPath=directory;folder=args.Length>0&&args[0]!="-"?args[0]:null;prefix=args.Length>1?args[1]:"qa-material-source";var app=new FrontierRuntime();int failed=0;
        try{Call(app,"Awake");Check(app.Game!=null,"Actual Awake loads full campaign");foreach(Action<FrontierRuntime> test in new Action<FrontierRuntime>[]{RealEntryPinWalkingAndHarvest,RemoteMapAndOriginalEconomicInteractions,GatesLegacySavesAndPreparedPurity,BilingualLayoutsAndParentControls})try{test(app);}catch(Exception error){failed++;Console.WriteLine("FAIL "+test.Method.Name+": "+(error.InnerException??error).Message);}app.EndVisualReview();Call(app,"OnDestroy");}
        finally{Application.persistentDataPath=prior;Input.Clear();GUI.queuedClick=null;GUI.color=Color.white;GUI.matrix=Matrix4x4.identity;GUI.enabled=true;if(Directory.Exists(directory))Directory.Delete(directory,true);}
        Console.WriteLine("Runtime material sources: "+(4-failed)+"/4 groups passed; NATIVE_UNVERIFIED");return failed==0?0:1;
    }
    static bool ExistsChoice(this Choice[] choices,string action){return Array.Exists(choices,c=>c.action==action);}
}
