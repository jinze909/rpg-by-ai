// Actual production E/Update, unchanged dialogue controls and original town handoffs.
// Staging and deterministic GUI declarations are offline: NATIVE_UNVERIFIED.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using Starfall;
using UnityEngine;

public static class RuntimeGardenHarvestChecks
{
    const BindingFlags All = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    
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
    static int Item(string action) { return action=="plant"?7:action=="ally1"?2:action=="ally2"?3:4; }
    static int Required(string action) { return action=="plant"?1:action=="ally1"?6:action=="ally2"||action=="engine"?3:5; }
    static string Kind(string action) { return action=="plant"||action=="ally1"||action=="harvest"?"garden":action=="ally2"?"fish":action=="ally3"?"forge":"engine"; }
    static string CompletedFlag(string action) { return action=="engine"?"gate_3":action=="plant"?null:"ally_"+action.Substring(4); }
    static void Prepare(FrontierRuntime app, int room = 0)
    {
        Input.Clear(); GUI.queuedClick=null; GUI.matrix=Matrix4x4.identity; GUI.color=Color.white; GUI.enabled=true;
        Screen.width=1280; Screen.height=800; Time.unscaledDeltaTime=.02f; Event.current.type=EventType.Repaint;
        app.BeginVisualReview(0,room); app.VisualReviewPaused=false; var g=app.Game;
        g.enemies.Clear(); g.projectiles.Clear(); g.hazards.Clear(); g.effects.Clear(); g.fallen.Clear();
        g.save.companion=-1; g.save.english=true; g.save.largeText=false; g.save.reducedMotion=true; g.save.effectsQuality=0;
        g.save.showGuidance=false; g.save.health=53; g.save.energy=61; g.save.totalTime=100; g.save.foodUntil=g.save.teaUntil=g.save.mealUntil=0;
        g.save.coins=313; for(int i=0;i<8;i++)g.save.inventory[i]=31; g.save.tea=17;
        g.save.reputation[1]=0; g.save.plantedAt=-1; g.save.trackedQuest="main"; g.save.flags.Add("handoff_runtime_mature_story"); g.toastTime=0;
        g.save.flags.RemoveAll(f=>f.StartsWith("firstharvest_")||f.StartsWith("watergarden_"));
        foreach(string flag in new[]{"ally_1","ally_3","watergarden_accept","watergarden_drawn","watergarden_watered","watergarden_complete","scout_saved"})if(!g.Has(flag))g.save.flags.Add(flag);
        g.save.stage=2; g.save.frontierEnding=0; g.save.projects[0]=0; g.gateOpeningTime=0;
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
    static void Open(FrontierRuntime app,string action,bool pad=false)
    {
        var g=app.Game; if(g.mode!="world")g.Close(); Host(app,Kind(action)); Key(app,pad?KeyCode.JoystickButton1:KeyCode.E);
        Check(g.mode=="dialog"&&g.conversation!=null,"Real E / B opens original "+action+" dialogue"); Pages(app);
        Check(Array.FindIndex(g.conversation.choices,c=>c.action==action)>=0,"The original handoff keeps its authored action "+action);
    }
    static void Pages(FrontierRuntime app, string capture = null)
    { int n=0; do { Draw(app,capture == null ? null : capture+"-page"+n); if((bool)Call(app,"DialogueChoicesVisible"))return; Key(app,KeyCode.Return); } while(n++<40); throw new Exception("Actual original dialogue pages reach choices"); }
    static Rect Slot(GameCore g, string action)
    { int i=Array.FindIndex(g.conversation.choices,c=>c.action==action),count=g.conversation.choices.Length,cols=count>2?2:count; Check(i>=0,"Original town action exists"); float w=(832-(cols-1)*14)/(float)Math.Max(1,cols); return new Rect(320+i%cols*(w+14),539+i/cols*45,w,37); }
    static GUI.ButtonCall Control(FrontierRuntime app, string action, bool enabled)
    { Draw(app); Rect r=Slot(app.Game,action);var b=GUI.buttonCalls.Find(c=>Same(c.rect,r));Check(b!=null&&b.enabled==enabled,"Actual "+action+" button must be "+(enabled?"enabled":"disabled")+" at live handoff balance");return b; }
    static void Number(FrontierRuntime app, string action)
    { int i=Array.FindIndex(app.Game.conversation.choices,c=>c.action==action);Check(i>=0&&i<9,"Original numbered handoff is reachable"); Key(app,(KeyCode)((int)KeyCode.Alpha1+i));Draw(app); }
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
        foreach(KeyCode key in new[]{KeyCode.Return,KeyCode.JoystickButton0}) { nav.focused=id;Key(app,key);Draw(app);Check(g.mode=="dialog"&&ReferenceEquals(g.conversation,conversation)&&JsonUtility.ToJson(g.save)==before,"Enter / controller A on stale disabled focus cannot complete or plant"); }
        g.Choose(Array.FindIndex(conversation.choices,c=>c.action==action));Check(g.mode=="dialog"&&ReferenceEquals(g.conversation,conversation)&&JsonUtility.ToJson(g.save)==before,"Direct original Choose revalidates stock and does not charge or close");
    }

    static bool Flag(GameCore g,string field) { return (bool)g.GetType().GetProperty("FirstHarvest"+field,All).GetValue(g,null); }
    static void Visit(FrontierRuntime app,string kind,string action,int input=0)
    { if(app.Game.mode!="world")app.Game.Close();Host(app,kind);Key(app,KeyCode.E);Pages(app);Activate(app,action,input); }
    static void Ripe(FrontierRuntime app)
    { Visit(app,"garden","plant");Check(app.Game.save.plantedAt>=0,"Real planting owns the crop");Visit(app,"rest","rest");Visit(app,"rest","rest"); }
    static void SeedReview(FrontierRuntime app,int input=0)
    { Open(app,"harvest");Activate(app,"garden_seed_menu",input);Pages(app);Check(app.Game.conversation.choices.Length==2&&app.Game.conversation.choices[0].action=="garden_back"&&app.Game.conversation.choices[1].action=="garden_return3","Seed return appends a separate explicit review with safe first choice"); }
    static void ActualGrowthAndAllInputPaths(FrontierRuntime app)
    {
        for(int input=0;input<4;input++)for(int canal=0;canal<3;canal++)foreach(int ending in new[]{0,1})
        {
            Prepare(app);var g=app.Game;g.save.projects[0]=canal;g.save.frontierEnding=ending;int seeds=g.save.inventory[7],herbs=g.save.inventory[2],coins=g.save.coins;Visit(app,"garden","plant",input);float planted=g.save.plantedAt;
            Check(planted>=100&&g.save.inventory[7]==seeds-1,"Actual input plants for precisely one original seed");Open(app,"harvest");Activate(app,"harvest",input);Check(g.save.plantedAt==planted&&g.save.inventory[2]==herbs&&g.save.coins==coins,"Immature check retains the exact crop and original resources");
            Visit(app,"rest","rest");Check(g.save.totalTime-planted<60,"One original 45-second inn rest is insufficient");Open(app,"harvest");Activate(app,"harvest",input);Check(g.save.plantedAt==planted,"One-rest check cannot harvest early");Visit(app,"rest","rest");Open(app,"harvest");
            int yield=5+2*canal+(ending==1?2:0);Check(g.conversation.textEn.Contains("herbs +"+yield+", seeds +2, coins +8")&&g.conversation.textEn.Contains("After harvest:"),"Original An dialogue displays exact live yield and balances");Activate(app,"harvest",input);
            Check(g.save.plantedAt==-1&&g.save.inventory[2]==herbs+yield&&g.save.inventory[7]==seeds+1&&g.save.coins==coins+8,"Numeric/mouse/Enter/pad pays exact original project and ending yield once");
            Visit(app,"garden","plant",input);Check(g.save.inventory[2]==herbs+yield&&g.save.coins==coins+8&&g.save.inventory[7]==seeds,"Next bed action is separate one-seed planting with no duplicate old reward");
        }
        Console.WriteLine("PASS real E, numeric/mouse/Enter/pad planting, original 60-second gate via inn rests, all canal/ending yields and exact once-only payout; NATIVE_UNVERIFIED");
    }
    static void FullStockRecoveryAndSeedConsent(FrontierRuntime app)
    {
        foreach(string blocker in new[]{"herbs","seeds","coins"})
        {
            Prepare(app);var g=app.Game;Ripe(app);float planted=g.save.plantedAt;if(blocker=="herbs")g.save.inventory[2]=9999;if(blocker=="seeds")g.save.inventory[7]=9998;if(blocker=="coins")g.save.coins=999999;
            Open(app,"harvest");string stock=JsonUtility.ToJson(g.save);int rev=g.revision;g.Choose(Array.FindIndex(g.conversation.choices,c=>c.action=="harvest"));
            Check(g.mode=="dialog"&&g.save.plantedAt==planted&&JsonUtility.ToJson(g.save)==stock&&g.revision==rev,"Actual ready harvest retains entire crop when "+blocker+" is full");Denied(app,"harvest");
            if(blocker=="herbs"){Visit(app,"cook","tea");Visit(app,"cook","tea");Visit(app,"cook","tea");}
            if(blocker=="coins"){Visit(app,"garden","seeds");Visit(app,"garden","seeds");}
            if(blocker=="seeds")
            {
                SeedReview(app);int seeds=g.save.inventory[7],gold=g.save.coins;float grow=g.save.plantedAt;
                // Default confirmation must be the non-destructive first control.
                Key(app,KeyCode.Return);Draw(app);Check(g.save.inventory[7]==seeds&&g.save.coins==gold&&g.save.plantedAt==grow,"Default Enter keeps all seeds and crop");
                SeedReview(app,1);Activate(app,"garden_back",1);Check(g.save.inventory[7]==seeds,"Explicit mouse cancellation returns to An without spending");
                SeedReview(app,2);Key(app,KeyCode.Escape);Check(g.save.inventory[7]==seeds&&!(bool)Call(g,"ReturnGardenSeeds"),"Escape closes the seed receipt; direct stale return cannot consume");
                SeedReview(app,3);Host(app,"cook");Check(!(bool)Call(g,"ReturnGardenSeeds")&&g.save.inventory[7]==seeds,"Seed confirmation rechecks actual An interaction authority");g.Close();
                SeedReview(app);Activate(app,"garden_return3",3);Check(g.save.inventory[7]==seeds-3&&g.save.coins==gold&&g.save.plantedAt==grow,"Deliberate pad-A confirmation returns exactly three seeds and grants no reward");
                Check(!(bool)Call(g,"ReturnGardenSeeds")&&g.save.inventory[7]==seeds-3,"Consumed seed review cannot be reused after reopening An");
            }
            int h=g.save.inventory[2],s=g.save.inventory[7],goldBefore=g.save.coins;Open(app,"harvest");Activate(app,"harvest",1);Check(g.save.plantedAt==-1&&g.save.inventory[2]==h+5&&g.save.inventory[7]==s+2&&g.save.coins==goldBefore+8,"Actual existing purchase/crafting or explicit seed return makes retained "+blocker+" crop fully payable");
        }
        Prepare(app);var game=app.Game;Ripe(app);Open(app,"harvest");game.save.coins=999999;Denied(app,"harvest");
        Console.WriteLine("PASS real disabled input paths and fresh stock checks, full herb/seed/coin recovery, default/cancel/Escape/stale/remote seed consent and no reward or repeated return; NATIVE_UNVERIFIED");
    }
    static void Reload(FrontierRuntime app,int slot,int version)
    {
        var g=app.Game;g.PrepareSave();g.save.version=version;Check(SaveStore.Write(g.save,slot),"Real envelope writes crop");string warning;SaveData data=SaveStore.Read(slot,out warning);Check(data!=null&&warning==null,"Real envelope reads crop");g.Load(data);g.Start();g.Close();g.enemies.Clear();Set(app,"camera",g.Position);
    }
    static void SavedCropsAndMatureFirstHarvest(FrontierRuntime app)
    {
        foreach(int version in new[]{1,2})foreach(bool ripe in new[]{false,true})
        {
            Prepare(app);var g=app.Game;Visit(app,"garden","plant");float planted=g.save.plantedAt;if(ripe){Visit(app,"rest","rest");Visit(app,"rest","rest");}g.save.inventory[2]=9999;Reload(app,0,version);Check(g.save.plantedAt==planted&&g.Has("handoff_runtime_mature_story"),"Legacy v"+version+" retains crop timestamp and unrelated story");
            Open(app,"harvest");if(ripe)Denied(app,"harvest");else{Activate(app,"harvest",0);Check(g.save.plantedAt==planted,"Loaded growing crop remains immature");Visit(app,"rest","rest");Visit(app,"rest","rest");}
            Visit(app,"cook","tea");Visit(app,"cook","tea");Visit(app,"cook","tea");int h=g.save.inventory[2],s=g.save.inventory[7],c=g.save.coins;Open(app,"harvest");Activate(app,"harvest",0);Reload(app,1,version);Check(g.save.plantedAt==-1&&g.save.inventory[2]==h+5&&g.save.inventory[7]==s+2&&g.save.coins==c+8,"Legacy retained crop pays once and reloads without clipping");
        }
        Prepare(app);var game=app.Game;Visit(app,"garden","firstharvest_menu");Activate(app,"firstharvest_accept",0);Check(Flag(game,"Accepted"),"Actual original story acceptance remains available");Ripe(app);game.save.inventory[2]=9999;Open(app,"harvest");Denied(app,"harvest");Check(!Flag(game,"Harvested"),"Rejected full crop cannot fabricate the First Harvest witness");Visit(app,"cook","tea");Visit(app,"cook","tea");Visit(app,"cook","tea");Open(app,"harvest");Activate(app,"harvest",0);Check(Flag(game,"Harvested")&&!Flag(game,"Packed"),"Only paid real crop earns original First Harvest stage");
        int coins=game.save.coins,tea=game.save.tea;Visit(app,"cook","firstharvest_menu");Activate(app,"firstharvest_pack",1);Visit(app,"returnscout","firstharvest_menu");Activate(app,"firstharvest_deliver",2);Visit(app,"garden","firstharvest_menu");Activate(app,"firstharvest_finish",3);Check(Flag(game,"Complete")&&game.save.coins==coins+25&&game.save.tea==tea+1,"Original packing, scout delivery and report pay unchanged once-only story reward");
        Visit(app,"garden","firstharvest_menu");Check(!Array.Exists(game.conversation.choices,x=>x.action=="firstharvest_finish"),"Completed story cannot report twice");
        Console.WriteLine("PASS real v1/v2 envelope crops before/after maturity and capacity recovery, once-only save payouts, rejected witness and complete mature First Harvest handoff story; NATIVE_UNVERIFIED");
    }
    static void BilingualLayoutAndReadonly(FrontierRuntime app)
    {
        foreach(bool english in new[]{false,true})foreach(bool large in new[]{false,true})foreach(int width in new[]{960,1280})foreach(string state in new[]{"empty","growing","blocked","seed-review"})
        {
            Prepare(app);var g=app.Game;g.save.english=english;g.save.largeText=large;Screen.width=width;Screen.height=width==960?600:800;Call(app,"Resize");g.save.projects[0]=2;g.save.frontierEnding=1;
            if(state!="empty")Ripe(app);if(state=="growing")g.save.totalTime=g.save.plantedAt+31;
            if(state=="blocked"||state=="seed-review"){g.save.inventory[2]=9999;g.save.inventory[7]=9999;g.save.coins=999999;}
            if(g.mode!="world")g.Close();Host(app,"garden");Key(app,KeyCode.E);if(state=="seed-review"){Pages(app);Activate(app,"garden_seed_menu",width==960?1:3);}
            string name=(english?"en":"zh")+(large?"-large":"-normal")+"-"+width+"-"+state;int pages=0;do{Draw(app,name+"-page"+pages);if((bool)Call(app,"DialogueChoicesVisible"))break;Key(app,KeyCode.Return);}while(++pages<40);Check(pages<40,"All localized dialogue pages are actually readable");
            int count=g.conversation.choices.Length;for(int i=0;i<count;i++){Rect r=Slot(g,g.conversation.choices[i].action);Check(r.y+r.height<=780&&GUI.buttonCalls.Exists(b=>Same(b.rect,r)),"All original and appended numbered choices remain on the unchanged card");}
            if(state!="seed-review")
            {
                Check(g.conversation.choices[0].action==(state=="empty"?"plant":"harvest")&&g.conversation.choices[1].action=="seeds"&&g.conversation.choices[2].action=="close"&&g.conversation.choices[count-1].action=="garden_seed_menu","Original first three services and mature story choices precede appended seed recovery");
                Check(g.conversation.textEn.Contains("herbs +11"),"Read-only dialogue reflects actual upgraded/ending yield");
                if(state=="empty") Check(g.conversation.textEn.Contains("Planting costs 1 seed.")&&!g.conversation.textEn.Contains("After harvest:"),"Empty-bed review explains its unpaid one-seed planting cost without promising a current-stock harvest balance");
                else Check(g.conversation.textEn.Contains("After harvest:")&&g.conversation.textEn.Contains(" → "),"Planted crops retain accurate before/after harvest balance review");
            }
            string before=JsonUtility.ToJson(g.save);int rev=g.revision;float clock=Get<float>(app,"clock");foreach(EventType type in new[]{EventType.Layout,EventType.Repaint}){Event.current.type=type;Draw(app);Check(JsonUtility.ToJson(g.save)==before&&g.revision==rev&&Get<float>(app,"clock")==clock,"Actual repeated OnGUI reads preserve save, simulation and crop");}Event.current.type=EventType.Repaint;
        }
        Console.WriteLine("PASS bilingual normal/large 960/1280 empty/growing/blocked/seed-review paginated captures, retained original choice geometry, exact eleven-herb quote and read-only Layout/Repaint; NATIVE_UNVERIFIED");
    }
    public static int Main(string[] args)
    {
        string previous=Application.persistentDataPath,temp=Path.Combine(Path.GetTempPath(),"starfall-garden-harvest-"+Guid.NewGuid());Application.persistentDataPath=temp;folder=args.Length>0&&args[0]!="-"?args[0]:null;prefix=args.Length>1?args[1]:"qa-garden-harvest";int failed=0;var app=new FrontierRuntime();
        try{Directory.CreateDirectory(temp);if(folder!=null)Directory.CreateDirectory(folder);Call(app,"Awake");foreach(Action<FrontierRuntime> test in new Action<FrontierRuntime>[]{ActualGrowthAndAllInputPaths,FullStockRecoveryAndSeedConsent,SavedCropsAndMatureFirstHarvest,BilingualLayoutAndReadonly})try{test(app);}catch(Exception error){failed++;Console.WriteLine("FAIL "+test.Method.Name+": "+(error.InnerException??error));}app.EndVisualReview();Call(app,"OnDestroy");}
        finally{Application.persistentDataPath=previous;Input.Clear();GUI.queuedClick=null;GUI.enabled=true;GUI.color=Color.white;GUI.matrix=Matrix4x4.identity;Event.current.type=EventType.Repaint;if(Directory.Exists(temp))Directory.Delete(temp,true);}
        Console.WriteLine("Runtime garden harvest: "+(4-failed)+"/4 groups passed; captures="+captures+"; NATIVE_UNVERIFIED");return failed==0?0:1;
    }
}
