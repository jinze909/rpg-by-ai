// Real production E, numbered services, menu focus, route preview and SaveStore.
// Geometry/progression fixtures are offline: NATIVE_UNVERIFIED.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using Starfall;
using UnityEngine;

public static class RuntimeSupplyIngredientChecks
{
    const BindingFlags All=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
    static readonly Rect FindButton=new Rect(112,539,174,37),BackButton=new Rect(866,670,351,28),PinButton=new Rect(866,572,351,38),MapButton=new Rect(866,622,351,36),MapBack=new Rect(279,690,266,30),RouteButton=new Rect(559,690,275,30);
    static readonly int[] Items={0,1,2,3,4,7};
    static string folder,prefix;static int captures;
    [Serializable]public sealed class Label{public string k,text;public float[] r;public int size;}
    static void Check(bool ok,string why){if(!ok)throw new Exception(why);}
    static object Call(object o,string name,params object[] args){var m=o.GetType().GetMethod(name,All);Check(m!=null,"Shipped method exists: "+name);return m.Invoke(o,args);}
    static T Get<T>(object o,string name){var f=o.GetType().GetField(name,All);Check(f!=null,"Shipped field exists: "+name);return(T)f.GetValue(o);}
    static void Set(object o,string name,object value){var f=o.GetType().GetField(name,All);Check(f!=null,"Shipped field exists: "+name);f.SetValue(o,value);}
    static bool Near(float a,float b){return Math.Abs(a-b)<.002f;}
    static bool Same(Rect a,Rect b){return Near(a.x,b.x)&&Near(a.y,b.y)&&Near(a.width,b.width)&&Near(a.height,b.height);}
    static void Update(FrontierRuntime app,params KeyCode[] keys){foreach(var key in keys)Input.keys.Add(key);Call(app,"Update");Input.Clear();}
    static void Draw(FrontierRuntime app,string capture=null)
    {
        GUI.commands.Clear();GUI.textureCalls.Clear();GUI.buttonCalls.Clear();Call(app,"OnGUI");
        if(folder!=null&&capture!=null){var c=GUI.commands.ToArray();var t=GUI.textureCalls.ToArray();var b=GUI.buttonCalls.ToArray();GUI.Export(folder,prefix+"-"+capture);captures++;GUI.commands.AddRange(c);GUI.textureCalls.AddRange(t);GUI.buttonCalls.AddRange(b);}
    }
    static List<Label> Labels(){var result=new List<Label>();foreach(string c in GUI.commands)if(c.StartsWith("{\"k\":\"text\"",StringComparison.Ordinal))result.Add(JsonUtility.FromJson<Label>(c));return result;}
    static string ControlText(FrontierRuntime app,Rect rect){Control(app,rect);var label=Labels().Find(l=>Near(l.r[0],rect.x)&&Near(l.r[1],rect.y)&&Near(l.r[2],rect.width)&&Near(l.r[3],rect.height));Check(label!=null,"Actual control has visible text");return label.text;}
    static GUI.ButtonCall Control(FrontierRuntime app,Rect rect,bool enabled=true){Draw(app);var b=GUI.buttonCalls.Find(v=>Same(v.rect,rect));Check(b!=null&&b.enabled==enabled,"Actual Find ingredients/parent control exists with live availability at "+rect.x+","+rect.y+" in "+app.Game.mode);return b;}
    static void Click(FrontierRuntime app,Rect rect){Control(app,rect);GUI.queuedClick=rect;Draw(app);Check(!GUI.queuedClick.HasValue,"Actual enabled control consumes mouse click");Draw(app);}
    static void Activate(FrontierRuntime app,Rect rect,int input)
    {
        if(input==0){Click(app,rect);return;}Control(app,rect);var nav=Get<MenuNavigation>(app,"navigation");string id=(string)Call(app,"ControlId",rect.x,rect.y,rect.width,rect.height);int n=0;
        while(nav.focused!=id&&n++<100){Update(app,KeyCode.Tab);Draw(app);}Check(nav.focused==id&&nav.CanActivate(id),"Real Tab reaches enabled unnumbered shortcut");Update(app,input==1?KeyCode.Return:KeyCode.JoystickButton0);Draw(app);
    }
    static void Prepare(FrontierRuntime app,int room=0)
    {
        Input.Clear();GUI.queuedClick=null;GUI.enabled=true;GUI.color=Color.white;GUI.matrix=Matrix4x4.identity;Event.current.type=EventType.Repaint;Screen.width=1280;Screen.height=800;Time.unscaledDeltaTime=.02f;
        app.BeginVisualReview(0,room);app.VisualReviewPaused=false;var g=app.Game;g.enemies.Clear();g.projectiles.Clear();g.hazards.Clear();g.effects.Clear();g.fallen.Clear();g.save.companion=-1;
        g.save.english=true;g.save.largeText=false;g.save.showGuidance=false;g.save.effectsQuality=0;g.save.reducedMotion=true;g.save.inventory[1]=g.save.inventory[2]=g.save.inventory[3]=0;g.save.inventory[6]=3;g.save.tea=4;g.save.coins=313;g.save.totalTime=100;g.save.trackedQuest="main";g.save.flags.Add("supply_ingredient_mature_story");
        for(int i=0;i<g.save.explored.Length;i++)g.save.explored[i]=16777215;
        Set(app,"applicationPaused",false);Set(app,"pointerSeen",false);Set(app,"controllerUsed",false);Set(app,"confirmControl",null);Set(app,"pressedControl",null);Set(app,"dialogParent",null);Set(app,"saveTimer",-1000f);Set(app,"arrivalTime",0f);Set(app,"resumeTime",0f);Set(app,"combatHudTime",0f);Set(app,"lastRevision",g.revision);Call(app,"Resize");
    }
    static WorldObject Host(FrontierRuntime app,string kind="cook")
    {
        var g=app.Game;var host=Array.Find(g.Map.objects,o=>o.kind==kind);Check(host!=null,"Original authored service host exists: "+kind);bool found=false;
        for(int y=-7;y<=7&&!found;y++)for(int x=-7;x<=7&&!found;x++){var p=new Point(host.x+x*.2f,host.y+y*.2f);if(g.CanStand(p)&&app.SetVisualReviewPosition(p)&&ReferenceEquals(g.Nearest(),host))found=true;}
        Check(found,"Fixture uses legal canonical service E footing");Set(app,"camera",g.Position);return host;
    }
    static void Pages(FrontierRuntime app){for(int n=0;n<40;n++){Draw(app);if((bool)Call(app,"DialogueChoicesVisible"))return;Update(app,KeyCode.Return);}throw new Exception("Original dialogue pagination terminates");}
    static int Choice(GameCore g,string action){int i=Array.FindIndex(g.conversation.choices,c=>c.action==action);Check(i>=0&&i<9,"Original numbered action remains: "+action);return i;}
    static Rect Slot(GameCore g,int index){int count=g.conversation.choices.Length,cols=count>2?2:count;float w=(832-(cols-1)*14)/(float)Math.Max(1,cols);return new Rect(320+index%cols*(w+14),539+index/cols*45,w,37);}
    static void Number(FrontierRuntime app,string action){Pages(app);int i=Choice(app.Game,action);Update(app,(KeyCode)((int)KeyCode.Alpha1+i));Draw(app);}
    static void Open(FrontierRuntime app,bool pantry=false,string kind="cook"){if(app.Game.mode!="world")app.Game.Close();Host(app,kind);Update(app,KeyCode.E);Check(app.Game.mode=="dialog","Actual E opens original cook/camp dialogue");Pages(app);if(pantry){Number(app,"supply_prepare");Pages(app);Check(app.Game.conversation.en=="Pantry preparation","Original numbered entry opens real Pantry preparation");}}
    // Each group reaches observable new button before reflecting any new query.
    static void Entry(FrontierRuntime app,int input=0)
    {
        Control(app,FindButton);var g=app.Game;Check(Labels().Exists(l=>l.text==g.L("查找材料","Find ingredients")),"Shortcut has exact localized visible text");
        var needs=new List<object>();foreach(object need in(IEnumerable)Call(g,"SupplyIngredientNeeds"))needs.Add(need);Check(needs.Count>0&&needs.Count<=2,"Real shortcut exposes only the current first blocked recipe's missing ingredients");
        string action=Get<string>(needs[0],"action"),quantity=action.StartsWith("supply_batch_",StringComparison.Ordinal)?action.Substring(action.LastIndexOf('_')+1):"1";bool stew=action=="cook"||action.StartsWith("supply_batch_0_",StringComparison.Ordinal);
        Label summary=Labels().Find(l=>Near(l.r[0],112)&&Near(l.r[1],585));Check(summary!=null&&summary.text.StartsWith(g.L(stew?"炖汤":"凉茶",stew?"Stew":"Tea")+" ×"+quantity+"\n"+g.L("还需：","Need:"),StringComparison.Ordinal),"Actual side summary labels exact original recipe, quantity and ingredient shortfalls");
        foreach(object need in needs){int item=Get<int>(need,"item"),required=Get<int>(need,"required"),held=Get<int>(need,"held"),missing=Get<int>(need,"missing");Check(missing==required-held&&missing>0&&held==g.save.inventory[item]&&summary.text.Contains(g.L(g.data.items[item].name,g.data.items[item].en)+" ×"+missing),"Visible summary uses actual held amount and exact original recipe deficit");}
        Activate(app,FindButton,input);Check(g.mode=="materials","Actual shortcut opens existing source menu");
    }
    static void Return(FrontierRuntime app,int input=0){Check(ControlText(app,BackButton)==app.Game.L("返回对话","Back to dialogue"),"Sources visibly names its real conversation parent");if(input==0)Click(app,BackButton);else Update(app,input==1?KeyCode.Escape:KeyCode.JoystickButton1);Draw(app);Check(app.Game.mode=="dialog","Actual Back/Escape/pad B restores original conversation");}
    static List<object> Sources(GameCore g,int item){var result=new List<object>();foreach(object s in(IEnumerable)Call(g,"MaterialSources",item))result.Add(s);return result;}
    static int Room(object s){return Get<int>(s,"room");}static WorldObject Target(object s){return Get<WorldObject>(s,"target");}
    static void Select(FrontierRuntime app,object source)
    {
        int item=Get<int>(source,"item");Click(app,new Rect(279+Array.IndexOf(Items,item)*158,177,148,42));var sources=Sources(app.Game,item);int index=sources.FindIndex(s=>Room(s)==Room(source)&&ReferenceEquals(Target(s),Target(source)));Check(index>=0,"Selected source remains actual known canonical object");int n=0;
        while(Get<int>(app,"materialPage")<index/5&&n++<40)Click(app,new Rect(654,625,160,32));while(Get<int>(app,"materialPage")>index/5&&n++<40)Click(app,new Rect(279,625,160,32));Check(Get<int>(app,"materialPage")==index/5,"Original source pagination reaches selected actual object");Click(app,new Rect(279,241+index%5*72,535,62));
    }
    static void ShortcutInputsReturnAndOriginalCosts(FrontierRuntime app)
    {
        foreach(bool pantry in new[]{false,true})for(int input=0;input<3;input++)
        {
            Prepare(app);Open(app,pantry);var g=app.Game;Conversation conversation=g.conversation;int page=Get<int>(app,"dialoguePage");string parent=Get<string>(app,"dialogParent"),before=JsonUtility.ToJson(g.save);int revision=g.revision;string[] actions=Array.ConvertAll(conversation.choices,c=>c.action);
            Entry(app,input);Check(Get<int>(app,"materialItem")==2&&JsonUtility.ToJson(g.save)==before&&g.revision==revision,"First missing herbs selected without spending, revealing or revising save");Return(app,input);
            Check(ReferenceEquals(g.conversation,conversation)&&Get<int>(app,"dialoguePage")==page&&Get<string>(app,"dialogParent")==parent&&JsonUtility.ToJson(g.save)==before,"Shortcut roundtrip preserves exact conversation, last read page, prior parent and every save field");for(int i=0;i<actions.Length;i++)Check(g.conversation.choices[i].action==actions[i],"Unnumbered shortcut never shifts original choices");
            string action=pantry?"supply_batch_0_1":"cook";Control(app,Slot(g,Choice(g,action)),false);g.save.inventory[2]=2;g.save.inventory[3]=1;Control(app,Slot(g,Choice(g,action)));int herbs=g.save.inventory[2],fish=g.save.inventory[3],stew=g.save.inventory[6],coins=g.save.coins;Number(app,action);
            Check(g.save.inventory[2]==herbs-2&&g.save.inventory[3]==fish-1&&g.save.inventory[6]==stew+1&&g.save.coins==coins,"Returned original numeric action alone pays exactly two herbs and one fish for one stew");Check(g.Has("supply_ingredient_mature_story")&&g.save.trackedQuest=="main","Original story and quest survive original transaction");
        }
        foreach(int item in new[]{3,1}){Prepare(app);var g=app.Game;g.save.inventory[2]=20;if(item==1){g.save.inventory[3]=20;g.save.inventory[6]=9999;}Open(app);Entry(app);Check(Get<int>(app,"materialItem")==item,"Live first genuinely missing ingredient selects fish/crystal tab");Return(app);}
        Console.WriteLine("PASS real E cook and Pantry, unnumbered mouse/Tab-Enter/controller A entry, Back/Escape/B exact dialogue-page restoration, unchanged action numbers, live disabled-to-ready original numeric recipe and exact original costs; NATIVE_UNVERIFIED");
    }
    static void KnownSourcesPinMapAndTravelRetirement(FrontierRuntime app)
    {
        Prepare(app);Open(app);var g=app.Game;Conversation conversation=g.conversation;string tracked=g.save.trackedQuest;Entry(app);var fish=Sources(g,3).Find(s=>Room(s)==g.save.room&&Target(s).kind=="fish");Check(fish!=null,"Only known real local fishing source exists");Select(app,fish);Click(app,PinButton);Check(ReferenceEquals(g.PinnedLandmark,Target(fish))&&g.save.trackedQuest==tracked,"Explicit source pin retains original tracked story");Return(app);Check(ReferenceEquals(g.conversation,conversation),"Pinning preserves same host dialogue parent");
        Entry(app);var remote=Sources(g,2).Find(s=>Room(s)!=g.save.room&&g.CanEnter(Room(s)));Check(remote!=null,"Explored unlocked remote herb source exists");Select(app,remote);string before=JsonUtility.ToJson(g.save);int generation=g.WorldGeneration;Click(app,MapButton);Check(g.mode=="map"&&Get<int>(app,"selectedRegion")==Room(remote)&&g.WorldGeneration==generation&&JsonUtility.ToJson(g.save)==before,"Map preview never travels, pays or reveals a source");Click(app,MapBack);Check(g.mode=="materials","Actual map Back restores source list");Return(app);Check(ReferenceEquals(g.conversation,conversation),"Map preview roundtrip preserves exact original dialogue");
        foreach(KeyCode back in new[]{KeyCode.Escape,KeyCode.JoystickButton1}){Entry(app);Select(app,remote);Click(app,MapButton);Update(app,back);Draw(app);Check(g.mode=="materials","Actual map Escape/B returns to owned source list before the dialogue");Return(app);Check(ReferenceEquals(g.conversation,conversation)&&g.save.room==0,"Keyboard/pad map return preserves original host and conversation");}
        Entry(app);Select(app,remote);Click(app,MapButton);Click(app,RouteButton);Check(g.mode=="map"&&g.save.room==0,"First route click asks original confirmation without traveling");Click(app,RouteButton);Check(g.mode=="world"&&g.save.room==Room(remote)&&g.WorldGeneration!=generation&&g.save.trackedQuest==tracked,"Explicit second route confirmation travels and keeps quest identity");
        g.enemies.Clear();Update(app,KeyCode.I);Click(app,new Rect(947,687,246,32));Check(g.mode=="materials"&&ControlText(app,BackButton)==g.L("返回背包","Back to inventory"),"Travel retires stale conversation parent; later original inventory sources return to inventory");Click(app,BackButton);Check(g.mode=="inventory","Post-travel Back cannot revive remote conversation");
        Prepare(app);g=app.Game;Open(app);for(int i=0;i<g.save.explored.Length;i++)g.save.explored[i]=0;Entry(app);Check(Sources(g,2).Count==0,"Shortcut does not reveal undiscovered ingredient locations");Control(app,PinButton,false);Control(app,MapButton,false);Return(app);
        Console.WriteLine("PASS all original known-source filtering, explicit local fish waypoint without replacing quest, remote preview/back dialogue ownership, separately confirmed travel invalidating parent and undiscovered sources remaining hidden; NATIVE_UNVERIFIED");
    }
    static string Files(){if(!Directory.Exists(Application.persistentDataPath))return "";var paths=Directory.GetFiles(Application.persistentDataPath,"*",SearchOption.AllDirectories);Array.Sort(paths,StringComparer.Ordinal);var s=new StringBuilder();foreach(string path in paths)s.Append(path).Append(Convert.ToBase64String(File.ReadAllBytes(path)));return s.ToString();}
    static void StaleOwnersSaveReloadAndUnrelatedMenus(FrontierRuntime app)
    {
        foreach(string invalid in new[]{"move","load","save-owner","game-owner","new-dialogue","generation","menu"})
        {
            Prepare(app);Open(app,true);var g=app.Game;Conversation original=g.conversation;Entry(app);string before=JsonUtility.ToJson(g.save);
            if(invalid=="move"){Check(app.SetVisualReviewPosition(new Point(18,15)),"Move fixture uses legal world ground");}
            else if(invalid=="load"){var saved=JsonUtility.FromJson<SaveData>(before);g.Load(saved);g.Start();g.Close();g.enemies.Clear();g.mode="materials";}
            else if(invalid=="save-owner")g.save=JsonUtility.FromJson<SaveData>(before);
            else if(invalid=="game-owner"){var replacement=new GameCore(g.data,JsonUtility.FromJson<SaveData>(before));replacement.conversation=original;replacement.mode="materials";replacement.enemies.Clear();app.GetType().GetProperty("Game",All).SetValue(app,replacement,null);g=replacement;}
            else if(invalid=="new-dialogue"){g.Close();Check(g.ShowSupplyPreparation(),"A newer real pantry conversation can open at the same host");Check(!ReferenceEquals(g.conversation,original),"New dialogue has distinct identity");g.mode="materials";}
            else if(invalid=="generation"){g.Enter(g.save.room);g.enemies.Clear();g.mode="materials";}
            else{Update(app,KeyCode.JoystickButton5);Draw(app);Check(g.mode=="loadout","Actual RB switches to unrelated existing menu");Update(app,KeyCode.Escape);Update(app,KeyCode.I);Draw(app);Click(app,new Rect(947,687,246,32));}
            Draw(app);Click(app,BackButton);Check(g.mode=="inventory"&&(!ReferenceEquals(g.conversation,original)||g.mode!="dialog"),"Unsafe "+invalid+" parent cannot restore stale conversation");
        }
        foreach(int version in new[]{1,2})
        {
            Prepare(app);Open(app,true);var g=app.Game;Entry(app);g.PrepareSave();g.save.version=version;Directory.CreateDirectory(Application.persistentDataPath);File.WriteAllText(SaveStore.PathFor(1),JsonUtility.ToJson(g.save));string warning;var legacy=SaveStore.Read(1,out warning);Check(legacy!=null&&warning==null,"Real raw legacy v"+version+" reads");g.Load(legacy);g.Start();g.Close();g.enemies.Clear();
            Check(g.save.inventory[1]==0&&g.save.inventory[2]==0&&g.save.inventory[3]==0&&g.Has("supply_ingredient_mature_story")&&g.save.trackedQuest=="main","Actual legacy Load retains deficits, mature story and tracking");g.PrepareSave();Check(SaveStore.Write(g.save,0),"Real checksum save writes unchanged shortage state");var loaded=SaveStore.Read(0,out warning);Check(loaded!=null&&warning==null&&loaded.inventory[6]==3&&loaded.tea==4,"Actual checksum read retains original pantry without free supplies");string raw=JsonUtility.ToJson(loaded);Check(!raw.Contains("SupplyIngredient")&&!raw.Contains("materialDialogue")&&!raw.Contains("ingredientParent"),"Derived shortage and parent ownership never add save fields");
            Update(app,KeyCode.I);Draw(app);Click(app,new Rect(947,687,246,32));Check(ControlText(app,BackButton)==g.L("返回背包","Back to inventory"),"Reloaded ordinary sources have no resurrected dialogue parent");
        }
        Console.WriteLine("PASS moved/loaded/save-replaced/regenerated/new-dialogue/unrelated-menu owner rejection and real raw v1/v2 plus checksum SaveStore continuity without schema expansion, phantom ingredients, story loss or restored stale parent; NATIVE_UNVERIFIED");
    }
    static void BilingualLayoutsReadOnlyAndOriginalGeometry(FrontierRuntime app)
    {
        foreach(bool pantry in new[]{false,true})foreach(bool english in new[]{false,true})foreach(bool large in new[]{false,true})foreach(int width in new[]{960,1280})
        {
            Prepare(app);var g=app.Game;g.save.english=english;g.save.largeText=large;Screen.width=width;Screen.height=width==960?600:800;Call(app,"Resize");Host(app);Update(app,KeyCode.E);Check(g.mode=="dialog","Real E opens localized service dialogue");
            if(pantry){Pages(app);Number(app,"supply_prepare");}
            for(int n=0;n<40;n++){Draw(app);if((bool)Call(app,"DialogueChoicesVisible"))break;Check(!GUI.buttonCalls.Exists(b=>Same(b.rect,FindButton)),"Unnumbered shortcut waits until all original narrative pages are read");Update(app,KeyCode.Return);}
            Pages(app);Control(app,FindButton);string suffix=(pantry?"pantry":"cook")+(english?"-en":"-zh")+(large?"-large":"-normal")+"-"+width;
            var conversation=g.conversation;int page=Get<int>(app,"dialoguePage"),revision=g.revision;string before=JsonUtility.ToJson(g.save);g.PrepareSave();Check(SaveStore.Write(g.save,0),"Read-only fixture writes real checksum bytes");before=JsonUtility.ToJson(g.save);string files=Files();
            foreach(var type in new[]{EventType.Layout,EventType.Repaint,EventType.Repaint}){Event.current.type=type;Draw(app);Check(JsonUtility.ToJson(g.save)==before&&g.revision==revision&&Files()==files&&ReferenceEquals(g.conversation,conversation)&&Get<int>(app,"dialoguePage")==page,"Repeated actual layout/repaint preserves original resources, revision, story, page and disk bytes");}
            Event.current.type=EventType.Repaint;Draw(app,"dialog-"+suffix);for(int i=0;i<conversation.choices.Length;i++){Rect slot=Slot(g,i);var button=GUI.buttonCalls.Find(b=>Same(b.rect,slot));Check(button!=null,"Every original numbered service retains exact original control geometry");Check(Labels().Exists(l=>Near(l.r[0],slot.x)&&Near(l.r[1],slot.y)&&l.text.StartsWith((i+1)+"  ",StringComparison.Ordinal)),"Shortcut adds no number and shifts no numbered original choice");}
            Entry(app);Draw(app,"sources-"+suffix);Control(app,BackButton);Check(JsonUtility.ToJson(g.save)==before&&Files()==files,"Source query and actual OnGUI never buy, craft, reveal or write saves");Return(app);Check(ReferenceEquals(g.conversation,conversation)&&Get<int>(app,"dialoguePage")==page,"Every text/viewport policy restores exact final read page");
        }
        Check(folder==null||captures==32,"Sixteen bilingual text/viewport policies capture actual dialogue and source screens");
        Console.WriteLine("PASS actual cook/Pantry zh/en normal/large 960/1280 layouts, all original numbered button positions, unnumbered shortcut, paginated last-page restore, read-only Layout/Repaint/source queries and protected checksum bytes with 32 optional captures; NATIVE_UNVERIFIED");
    }
    public static int Main(string[] args)
    {
        CultureInfo.CurrentCulture=CultureInfo.InvariantCulture;string prior=Application.persistentDataPath,directory=Path.Combine(Path.GetTempPath(),"starfall-supply-ingredient-"+Guid.NewGuid());Application.persistentDataPath=directory;folder=args.Length>0&&args[0]!="-"?args[0]:null;prefix=args.Length>1?args[1]:"qa-supply-ingredient";var app=new FrontierRuntime();int failed=0;
        try{Call(app,"Awake");Check(app.Game!=null,"Real Awake loads shipped campaign");foreach(Action<FrontierRuntime> test in new Action<FrontierRuntime>[]{ShortcutInputsReturnAndOriginalCosts,KnownSourcesPinMapAndTravelRetirement,StaleOwnersSaveReloadAndUnrelatedMenus,BilingualLayoutsReadOnlyAndOriginalGeometry})try{test(app);}catch(Exception error){failed++;Console.WriteLine("FAIL "+test.Method.Name+": "+(error.InnerException??error).Message);}app.EndVisualReview();Call(app,"OnDestroy");}
        finally{Application.persistentDataPath=prior;Input.Clear();GUI.queuedClick=null;GUI.color=Color.white;GUI.matrix=Matrix4x4.identity;GUI.enabled=true;if(Directory.Exists(directory))Directory.Delete(directory,true);}
        Console.WriteLine("Runtime supply ingredients: "+(4-failed)+"/4 groups passed; NATIVE_UNVERIFIED");return failed==0?0:1;
    }
}
