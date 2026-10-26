// Real production E/dialogue Update, OnGUI controls, original transactions and SaveStore.
// Explicit progression/footing fixtures are offline; native Unity is NATIVE_UNVERIFIED.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using Starfall;
using UnityEngine;

public static class RuntimeSupplyServiceChecks
{
    const BindingFlags All = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static readonly string[] Actions = { "cook", "buymeal", "buywood", "tea", "seeds" };
    static string folder, prefix; static int captures;
    [Serializable] public sealed class Label { public string k, text; public float[] r; public int size; }
    static void Check(bool ok, string why) { if (!ok) throw new Exception(why); }
    static object Call(object obj, string name, params object[] args)
    { var m = obj.GetType().GetMethod(name, All); Check(m != null, "Shipped method exists: " + name); return m.Invoke(obj, args); }
    static T Get<T>(object obj, string name)
    { var f = obj.GetType().GetField(name, All); Check(f != null, "Shipped field exists: " + name); return (T)f.GetValue(obj); }
    static void Set(object obj, string name, object value) { obj.GetType().GetField(name, All).SetValue(obj, value); }
    static bool Near(float a, float b) { return Math.Abs(a - b) < .003f; }
    static bool Same(Rect a, Rect b) { return Near(a.x,b.x) && Near(a.y,b.y) && Near(a.width,b.width) && Near(a.height,b.height); }
    static void Update(FrontierRuntime app) { Call(app, "Update"); Input.Clear(); }
    static void Key(FrontierRuntime app, KeyCode key) { Input.keys.Add(key); Update(app); }
    static void Draw(FrontierRuntime app, string capture = null)
    {
        GUI.commands.Clear(); GUI.textureCalls.Clear(); GUI.buttonCalls.Clear(); Call(app, "OnGUI");
        if (folder != null && capture != null)
        {
            var c = GUI.commands.ToArray(); var t = GUI.textureCalls.ToArray(); var b = GUI.buttonCalls.ToArray();
            GUI.Export(folder, prefix + "-" + capture); captures++;
            GUI.commands.AddRange(c); GUI.textureCalls.AddRange(t); GUI.buttonCalls.AddRange(b);
        }
    }
    static List<Label> Labels()
    { var list = new List<Label>(); foreach(string c in GUI.commands) if(c.StartsWith("{\"k\":\"text\"",StringComparison.Ordinal)) list.Add(JsonUtility.FromJson<Label>(c)); return list; }
    static int Item(string action) { return action == "tea" ? -1 : action == "buywood" ? 0 : action == "seeds" ? 7 : 6; }
    static int Quantity(string action) { return action == "buywood" ? 4 : action == "seeds" ? 3 : 1; }
    static int Limit(string action) { return action == "tea" ? 999 : 9999; }
    static int Stock(GameCore g, string action) { return Item(action) < 0 ? g.save.tea : g.save.inventory[Item(action)]; }
    static void Stock(GameCore g, string action, int n) { if(Item(action) < 0)g.save.tea=n; else g.save.inventory[Item(action)]=n; }
    static void Prepare(FrontierRuntime app, int room = 0)
    {
        Input.Clear(); GUI.queuedClick=null; GUI.matrix=Matrix4x4.identity; GUI.color=Color.white; GUI.enabled=true;
        Screen.width=1280; Screen.height=800; Time.unscaledDeltaTime=.02f; Event.current.type=EventType.Repaint;
        app.BeginVisualReview(0,room); app.VisualReviewPaused=false; var g=app.Game;
        g.enemies.Clear(); g.projectiles.Clear(); g.hazards.Clear(); g.effects.Clear(); g.fallen.Clear();
        g.save.companion=-1; g.save.english=true; g.save.largeText=false; g.save.reducedMotion=true; g.save.effectsQuality=0;
        g.save.showGuidance=false; g.save.health=53; g.save.energy=61; g.save.totalTime=100; g.save.foodUntil=g.save.teaUntil=g.save.mealUntil=0;
        g.save.coins=313; for(int i=0;i<8;i++)g.save.inventory[i]=31; g.save.tea=17;
        g.save.reputation[1]=0; g.save.plantedAt=-1; g.save.trackedQuest="main"; g.save.flags.Add("supply_runtime_mature_story"); g.toastTime=0;
        Set(app,"applicationPaused",false); Set(app,"pointerSeen",false); Set(app,"pointerAimActive",false); Set(app,"controllerUsed",false);
        Set(app,"combatHudTime",0f); Set(app,"resumeTime",0f); Set(app,"arrivalTime",0f); Set(app,"saveTimer",-1000f); Set(app,"lastRevision",g.revision);
        Call(app,"Resize");
    }
    // Staging only: select legal canonical E-owned footing; no transaction is inferred here.
    static WorldObject Host(FrontierRuntime app, string kind)
    {
        var g=app.Game; var host=Array.Find(g.Map.objects,o=>o.kind==kind); Check(host!=null,"Authored map has original service host "+kind);
        bool found=false; for(int y=-7;y<=7&&!found;y++)for(int x=-7;x<=7&&!found;x++)
        { Point p=new Point(host.x+x*.2f,host.y+y*.2f); if(g.CanStand(p)&&app.SetVisualReviewPosition(p)&&ReferenceEquals(g.Nearest(),host))found=true; }
        Check(found,"Original host has legal nearest-interaction footing: "+kind); Set(app,"camera",g.Position); return host;
    }
    static void Open(FrontierRuntime app, string action, bool camp=false)
    {
        var g=app.Game; if(g.mode!="world")g.Close(); Host(app,camp?"campfire":action=="seeds"?"garden":"cook"); Key(app,KeyCode.E);
        Check(g.mode=="dialog"&&g.conversation!=null,"Real E opens original "+action+" dialogue"); Pages(app);
        Check(Array.FindIndex(g.conversation.choices,c=>c.action==action)>=0,"Original dialogue keeps paid service "+action);
    }
    static void Pages(FrontierRuntime app)
    { int n=0; do { Draw(app); if((bool)Call(app,"DialogueChoicesVisible"))return; Key(app,KeyCode.Return); } while(n++<40); throw new Exception("Actual original dialogue pages reach choices"); }
    static Rect Slot(GameCore g, string action)
    { int i=Array.FindIndex(g.conversation.choices,c=>c.action==action),count=g.conversation.choices.Length,cols=count>2?2:count; Check(i>=0,"Original paid action exists"); float w=(832-(cols-1)*14)/(float)Math.Max(1,cols); return new Rect(320+i%cols*(w+14),539+i/cols*45,w,37); }
    static GUI.ButtonCall Control(FrontierRuntime app, string action, bool enabled)
    { Draw(app); Rect r=Slot(app.Game,action);var b=GUI.buttonCalls.Find(c=>Same(c.rect,r));Check(b!=null&&b.enabled==enabled,"Actual "+action+" button must be "+(enabled?"enabled":"disabled")+" at live capacity/material balance");return b; }
    static void Number(FrontierRuntime app, string action)
    { int i=Array.FindIndex(app.Game.conversation.choices,c=>c.action==action);Check(i>=0&&i<9,"Original numbered service is reachable"); Key(app,(KeyCode)((int)KeyCode.Alpha1+i));Draw(app); }
    static void Activate(FrontierRuntime app,string action,int input)
    {
        Control(app,action,true); if(input==0){Number(app,action);return;} Rect r=Slot(app.Game,action);
        if(input==1){GUI.queuedClick=r;Draw(app);Check(!GUI.queuedClick.HasValue,"Actual mouse consumes original service button");return;}
        string id=(string)Call(app,"ControlId",r.x,r.y,r.width,r.height);var nav=Get<MenuNavigation>(app,"navigation");int n=0;
        while(nav.focused!=id&&n++<90){Key(app,KeyCode.Tab);Draw(app);}Check(nav.focused==id&&nav.CanActivate(id),"Actual Tab reaches enabled original service");Key(app,input==2?KeyCode.Return:KeyCode.JoystickButton0);Draw(app);
    }
    static void Denied(FrontierRuntime app,string action)
    {
        var g=app.Game; Control(app,action,false);var conversation=g.conversation;string before=JsonUtility.ToJson(g.save);int revision=g.revision;
        Number(app,action); Check(ReferenceEquals(g.conversation,conversation)&&g.mode=="dialog"&&JsonUtility.ToJson(g.save)==before&&g.revision==revision,"Disabled numbered service preserves original dialogue and every saved resource");
        Rect r=Slot(g,action);GUI.queuedClick=r;Draw(app);GUI.queuedClick=null;
        Check(ReferenceEquals(g.conversation,conversation)&&g.mode=="dialog"&&JsonUtility.ToJson(g.save)==before,"Disabled mouse service cannot charge or close dialogue");
        var nav=Get<MenuNavigation>(app,"navigation");string id=(string)Call(app,"ControlId",r.x,r.y,r.width,r.height);Check(!nav.CanActivate(id),"Disabled service cannot be activated by menu focus");
        // An already-focused control can become disabled while the dialogue remains open.
        nav.focused=id;Key(app,KeyCode.JoystickButton0);Draw(app);Check(g.mode=="dialog"&&ReferenceEquals(g.conversation,conversation)&&JsonUtility.ToJson(g.save)==before,"Controller A on stale disabled focus cannot buy or craft");
        g.Choose(Array.FindIndex(conversation.choices,c=>c.action==action));Check(g.mode=="dialog"&&ReferenceEquals(g.conversation,conversation)&&JsonUtility.ToJson(g.save)==before,"Direct original Choose revalidates stock and does not charge or close");
    }
    // Every group asserts observable public UI behavior before reflection on the new API.
    static void CapacityBaseline(FrontierRuntime app)
    { Prepare(app);Stock(app.Game,"cook",9999);Open(app,"cook");Control(app,"cook",false); }
    static void CapacityAndFailureInputs(FrontierRuntime app)
    {
        foreach(string action in Actions)
        {
            Prepare(app);Stock(app.Game,action,Limit(action));Open(app,action);Denied(app,action);
            for(int delta=1;delta<Quantity(action);delta++){Stock(app.Game,action,Limit(action)-delta);Denied(app,action);}
            Stock(app.Game,action,0);if(action=="cook")app.Game.save.inventory[3]=0;else if(action=="tea")app.Game.save.inventory[1]=0;else app.Game.save.coins=0;Denied(app,action);
        }
        foreach(int room in new[]{6,7,GameCore.ReachRoom}){Prepare(app,room);Stock(app.Game,"tea",999);Open(app,"tea",true);Denied(app,"tea");}
        Console.WriteLine("PASS real Ari/garden/camp E dialogues, all five paid services at storage cap and partial timber/seed capacity, insufficient fish/crystal/coins, actual numbered/mouse/stale-focus controller A and direct Choose retaining dialogue with no payment; NATIVE_UNVERIFIED");
    }
    static void OriginalCostsBoundaryAndIndependentSystems(FrontierRuntime app)
    {
        CapacityBaseline(app);
        foreach(string action in Actions)foreach(int input in new[]{0,1,2,3})foreach(int reputation in new[]{0,3})
        {
            Prepare(app);var g=app.Game;Stock(g,action,Limit(action)-Quantity(action));g.save.reputation[1]=reputation;Open(app,action);
            int[] stock=(int[])g.save.inventory.Clone();int tea=g.save.tea,coins=g.save.coins;float hp=g.save.health,energy=g.save.energy,food=g.save.foodUntil,teaUntil=g.save.teaUntil;
            int cost=action=="buymeal"?g.ShopStewCost:action=="buywood"?g.ShopTimberCost:action=="seeds"?5:0;
            Activate(app,action,input);Check(g.mode=="world"&&g.conversation==null,"Successful original paid service closes original dialogue");
            for(int i=0;i<8;i++){int expected=stock[i]+(Item(action)==i?Quantity(action):0)-(i==2&&(action=="cook"||action=="tea")?2:0)-(i==3&&action=="cook"?1:0)-(i==1&&action=="tea"?1:0);Check(g.save.inventory[i]==expected,"Actual original exact inventory cost/yield for "+action+" item"+i);}
            Check(Stock(g,action)==Limit(action)&&g.save.tea==tea+(action=="tea"?1:0)&&g.save.coins==coins-cost&&g.save.health==hp&&g.save.energy==energy&&g.save.foodUntil==food&&g.save.teaUntil==teaUntil,"Boundary success keeps exact original stock/discount price and never consumes/heals");
        }
        Prepare(app);var game=app.Game;game.save.coins=0;Open(app,"buymeal");Denied(app,"buymeal");game.save.coins=100;Control(app,"buymeal",true);game.save.reputation[1]=3;int original=game.save.coins;Activate(app,"buymeal",0);Check(game.save.coins==original-game.ShopStewCost,"Original open dialogue uses fresh reputation discount at payment");
        Prepare(app);game=app.Game;Open(app,"cook");Activate(app,"supply_prepare",0);Check(game.mode=="dialog"&&game.conversation.en=="Pantry preparation","Original batch menu remains reachable from Ari");int stew=game.save.inventory[6],herbs=game.save.inventory[2],fish=game.save.inventory[3];Activate(app,"supply_batch_0_5",0);Check(game.save.inventory[6]==stew+5&&game.save.inventory[2]==herbs-10&&game.save.inventory[3]==fish-5,"Original batch keeps five stew for ten herbs and five fish");
        game.Close();Open(app,"cook");Activate(app,"kitchen",0);float health=game.save.health,energy2=game.save.energy;herbs=game.save.inventory[2];fish=game.save.inventory[3];Activate(app,"kitchen_cook_1",0);Check(game.ActiveRoadMeal==1&&game.save.inventory[2]==herbs-2&&game.save.inventory[3]==fish-1&&game.save.health==health&&game.save.energy==energy2,"Original road kitchen keeps meal cost and no immediate heal/refill");game.Close();game.save.health-=9;game.save.energy=5;stew=game.save.inventory[6];Key(app,KeyCode.F);Check(game.save.inventory[6]==stew-1&&game.save.energy>99&&game.save.foodUntil>game.save.totalTime,"Original actual F consumes one stored stew and retains original benefits");
        Prepare(app,6);game=app.Game;Stock(game,"tea",998);Open(app,"tea",true);herbs=game.save.inventory[2];int crystals=game.save.inventory[1];Activate(app,"tea",3);Check(game.save.tea==999&&game.save.inventory[2]==herbs-2&&game.save.inventory[1]==crystals-1,"Actual camp A brews original one tea for two herbs and one crystal");
        Console.WriteLine("PASS exact-capacity success for five services via numbered/mouse/Tab-Enter/controller A, original normal and reputation prices, live open-dialogue revalidation, preserved five-item batches/road kitchen/F consumption and original camp tea; NATIVE_UNVERIFIED");
    }
    static void Append(StringBuilder s,object value,HashSet<object> seen)
    {
        if(value==null){s.Append("null;");return;}Type type=value.GetType();if(type.IsPrimitive||type.IsEnum||value is string||value is decimal){s.Append(Convert.ToString(value,CultureInfo.InvariantCulture)).Append(';');return;}
        if(!type.IsValueType&&!seen.Add(value)){s.Append("ref;");return;}var seq=value as IEnumerable;if(seq!=null){foreach(object item in seq)Append(s,item,seen);s.Append("end;");return;}
        var fields=type.GetFields(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);Array.Sort(fields,(a,b)=>string.CompareOrdinal(a.Name,b.Name));foreach(var f in fields){s.Append(f.Name).Append('=');Append(s,f.GetValue(value),seen);}
    }
    static string State(GameCore g){var s=new StringBuilder();Append(s,g,new HashSet<object>());return s.ToString();}
    static string Files(){if(!Directory.Exists(Application.persistentDataPath))return "";var paths=Directory.GetFiles(Application.persistentDataPath,"*",SearchOption.AllDirectories);Array.Sort(paths,StringComparer.Ordinal);var s=new StringBuilder();foreach(string p in paths)s.Append(p).Append(Convert.ToBase64String(File.ReadAllBytes(p)));return s.ToString();}
    static object Quote(GameCore g,string action){return Call(g,"GetSupplyServiceQuote",action);}
    static bool Available(object q){return (bool)q.GetType().GetProperty("available").GetValue(q,null);}
    static void SavedContinuityAndPreparedPurity(FrontierRuntime app)
    {
        CapacityBaseline(app);
        foreach(int version in new[]{1,2})
        {
            Prepare(app);var g=app.Game;g.save.inventory[0]=9995;g.save.inventory[6]=9998;g.save.inventory[7]=9996;g.save.tea=998;
            foreach(string action in new[]{"buywood","buymeal","seeds","tea"}){Open(app,action);Activate(app,action,0);}
            g.PrepareSave();g.save.version=version;string before=JsonUtility.ToJson(g.save);Directory.CreateDirectory(Application.persistentDataPath);File.WriteAllText(SaveStore.PathFor(1),before);string warning;var old=SaveStore.Read(1,out warning);Check(old!=null&&warning==null,"Raw legacy v"+version+" accepts legitimately full stock");g.Load(old);g.Start();g.Close();
            Check(g.save.inventory[0]==9999&&g.save.inventory[6]==9999&&g.save.inventory[7]==9999&&g.save.tea==999&&g.Has("supply_runtime_mature_story")&&g.save.trackedQuest=="main","Legacy restore retains all legitimately paid stock, mature story and quest");int coins=g.save.coins;g.PrepareSave();Check(SaveStore.Write(g.save,0),"Real checksum writer accepts full paid inventory");g.Load(SaveStore.Read(0,out warning));g.Start();g.Close();Check(warning==null&&g.save.coins==coins&&g.save.inventory[0]==9999&&g.save.inventory[6]==9999&&g.save.inventory[7]==9999&&g.save.tea==999,"Checksum roundtrip cannot lose a legitimately paid item");
            string raw=JsonUtility.ToJson(g.save);Check(!raw.Contains("SupplyService")&&!raw.Contains("stockLimit"),"Derived quotes never expand original save schema");
        }
        Prepare(app);var game=app.Game;Host(app,"cook");game.save.weapon=1;game.save.flags.Add("weapon_1");Key(app,KeyCode.J);ActorAction actionOwner=game.playerAction;Check(actionOwner!=null&&!actionOwner.released,"Actual J prepares owned bow attack");Key(app,KeyCode.E);Pages(app);Check(game.mode=="dialog"&&ReferenceEquals(game.playerAction,actionOwner),"Real E dialogue preserves accepted bow action");game.PrepareSave();Check(SaveStore.Write(game.save,0),"Purity fixture protects actual checksum bytes");
        string state=State(game),files=Files();float clock=Get<float>(app,"clock");var matrix=Matrix4x4.TRS(new Vector3(13,21,0),Quaternion.Euler(0,0,11),new Vector3(1.25f,1.25f,1));var tint=new Color(.3f,.4f,.5f,.6f);
        foreach(EventType evt in new[]{EventType.Layout,EventType.Repaint})for(int n=0;n<5;n++)
        {
            Event.current.type=evt;GUI.matrix=matrix;GUI.color=tint;GUI.enabled=false;
            foreach(string action in Actions){object quote=Quote(game,action);Check(quote!=null&&Available(quote),"Fresh valid quote remains inspectable even away from its own host");Call(game,"SupplyServiceChoiceLabel",new Choice("原有","Original",action));}
            Check(Quote(game,"close")==null,"Unrelated original choices have no paid-service quote");Draw(app);Check(State(game)==state&&Files()==files&&Get<float>(app,"clock")==clock,"Repeated actual Layout/Repaint and quote/label reads preserve entire Core/RNG/action/save graph and disk");Check(GUI.matrix.Equals(matrix)&&GUI.color.Equals(tint)&&!GUI.enabled,"Original OnGUI restores caller matrix/color/enabled");
        }
        GUI.matrix=Matrix4x4.identity;GUI.color=Color.white;GUI.enabled=true;Event.current.type=EventType.Repaint;
        game.save.inventory[6]=9999;Draw(app);var q=Quote(game,"cook");Check(!Available(q)&&Get<int>(q,"stock")==9999&&Get<int>(q,"quantity")==1&&Get<int>(q,"limit")==9999,"Read-only quote follows live capacity without cached permission");float damage=actionOwner.damage;Denied(app,"cook");Check(ReferenceEquals(game.playerAction,actionOwner)&&actionOwner.damage==damage&&!actionOwner.released,"Rejected actual numbered/controller/click service cannot replace, release or modify captured attack");
        Console.WriteLine("PASS real raw v1/v2 plus checksum full-stock continuity without paid-item loss/schema additions, mature story/quest preservation, fresh uncached quotes and exact Core/RNG/owned-bow/save-byte purity across real Layout/Repaint and rejected inputs; NATIVE_UNVERIFIED");
    }
    static void BilingualLayoutsAndOriginalChoiceOrder(FrontierRuntime app)
    {
        CapacityBaseline(app);
        foreach(bool english in new[]{false,true})foreach(bool large in new[]{false,true})foreach(int width in new[]{960,1920})foreach(string host in new[]{"cook","garden","campfire"})foreach(int scenario in new[]{0,1,2})
        {
            Prepare(app,host=="campfire"?6:0);var g=app.Game;g.save.english=english;g.save.largeText=large;Screen.width=width;Screen.height=width==960?600:1080;Call(app,"Resize");
            if(scenario==1){g.save.inventory[0]=g.save.inventory[6]=g.save.inventory[7]=9999;g.save.tea=999;}if(scenario==2){g.save.inventory[1]=g.save.inventory[2]=g.save.inventory[3]=0;g.save.coins=0;}
            Open(app,host=="garden"?"seeds":"tea",host=="campfire");var conversation=g.conversation;int count=conversation.choices.Length;Check(count>=4&&count<=9,"Original full choice page retains reachable original actions");
            string suffix=(english?"en":"zh")+(large?"-large-":"-normal-")+width+"-"+host+"-"+scenario;
            Draw(app,suffix);foreach(Choice choice in conversation.choices)
            {
                Rect r=Slot(g,choice.action);int i=Array.IndexOf(conversation.choices,choice);string label=(string)Call(g,"SupplyServiceChoiceLabel",choice);string expected=(Get<bool>(app,"controllerUsed")?"":(i+1)+"  ")+label;
                Check(Labels().Exists(l=>l.text==expected&&Same(new Rect(l.r[0],l.r[1],l.r[2],l.r[3]),r)),"Every original action keeps localized full visible label, numbering and original control geometry");
                object quote=Quote(g,choice.action);if(quote!=null){Control(app,choice.action,Available(quote));Check(Get<string>(quote,"action")==choice.action&&Get<int>(quote,"quantity")==Quantity(choice.action),"Visible live paid label remains bound to exact original action and yield");}
            }
            if(host=="cook")Check(conversation.choices[0].action=="cook"&&conversation.choices[1].action=="buymeal"&&conversation.choices[2].action=="buywood"&&conversation.choices[3].action=="tea"&&Array.Exists(conversation.choices,c=>c.action=="kitchen")&&Array.Exists(conversation.choices,c=>c.action=="supply_prepare"),"Cook keeps four numbered services, Goodbye, road kitchen and batch preparation order");
            int leave=Array.FindIndex(conversation.choices,c=>c.action=="close");Check(leave>=0,"Original safe leave choice remains available");Activate(app,"close",3);Check(g.mode=="world","Controller A can always leave the original service dialogue safely");
        }
        Check(folder==null||captures==72,"Available/full/unaffordable cook/garden/camp dialogues captured in eight real text/screen policies");
        Console.WriteLine("PASS actual bilingual normal/large 960/1920 full dialogue choices, original geometry/order/numbers, live affordable/full/insufficient stock labels, untouched kitchen/batch/leave controls and controller A safe exit with 72 optional captures; NATIVE_UNVERIFIED");
    }
    public static int Main(string[] args)
    {
        string prior=Application.persistentDataPath,directory=Path.Combine(Path.GetTempPath(),"starfall-supply-service-"+Guid.NewGuid());Application.persistentDataPath=directory;folder=args.Length>0&&args[0]!="-"?args[0]:null;prefix=args.Length>1?args[1]:"qa-supply-service";var app=new FrontierRuntime();int failed=0;
        try{Call(app,"Awake");Check(app.Game!=null,"Actual Awake loads full campaign");foreach(Action<FrontierRuntime> test in new Action<FrontierRuntime>[]{CapacityAndFailureInputs,OriginalCostsBoundaryAndIndependentSystems,SavedContinuityAndPreparedPurity,BilingualLayoutsAndOriginalChoiceOrder})try{test(app);}catch(Exception error){failed++;Console.WriteLine("FAIL "+test.Method.Name+": "+(error.InnerException??error).Message);}app.EndVisualReview();Call(app,"OnDestroy");}
        finally{Application.persistentDataPath=prior;Input.Clear();GUI.queuedClick=null;GUI.color=Color.white;GUI.matrix=Matrix4x4.identity;GUI.enabled=true;if(Directory.Exists(directory))Directory.Delete(directory,true);}
        Console.WriteLine("Runtime supply services: "+(4-failed)+"/4 groups passed; NATIVE_UNVERIFIED");return failed==0?0:1;
    }
}
