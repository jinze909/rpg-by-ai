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

public static class RuntimeTownHandoffChecks
{
    const BindingFlags All = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static readonly string[] Actions = { "plant", "ally1", "ally2", "ally3", "engine" };
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
        g.save.flags.RemoveAll(f=>f=="ally_1"||f=="ally_2"||f=="ally_3"||f=="gate_3");
        g.save.stage=2; g.save.frontierEnding=0; g.gateOpeningTime=0;
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
    static void Pages(FrontierRuntime app)
    { int n=0; do { Draw(app); if((bool)Call(app,"DialogueChoicesVisible"))return; Key(app,KeyCode.Return); } while(n++<40); throw new Exception("Actual original dialogue pages reach choices"); }
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
    static void Baseline(FrontierRuntime app)
    { Prepare(app);app.Game.save.inventory[7]=0;Open(app,"plant");Control(app,"plant",false); }
    static object Quote(GameCore g,string action) { return Call(g,"GetTownHandoffQuote",action); }
    static bool Available(object quote) { return (bool)quote.GetType().GetProperty("available").GetValue(quote,null); }
    static string ChoiceLabel(GameCore g,Choice choice) { return (string)Call(g,"DialogueChoiceLabel",choice); }
    static void ExactPayment(FrontierRuntime app,string action,int input)
    {
        var g=app.Game;SaveData expected=JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(g.save));
        expected.inventory[Item(action)]-=Required(action);
        if(action=="plant")expected.plantedAt=expected.totalTime;
        else if(action=="engine")expected.flags.Add("gate_3");
        else { expected.flags.Add(CompletedFlag(action));expected.coins+=40;expected.inventory[6]++;expected.skillPoints++; }
        Activate(app,action,input);
        Check(g.mode=="world"&&g.conversation==null&&JsonUtility.ToJson(g.save)==JsonUtility.ToJson(expected),
            "Actual original "+action+" changes only its exact old cost, timestamp/flag and 40 coin / stew / skill-point reward");
        string once=JsonUtility.ToJson(g.save);int revision=g.revision;g.Choose(0);
        Check(JsonUtility.ToJson(g.save)==once&&g.revision==revision,"A repeated stale Choose outside dialogue never pays or rewards twice");
    }
    static void MissingMaterialsRetainAndRefillOriginalRows(FrontierRuntime app)
    {
        foreach(string action in Actions)foreach(bool pad in new[]{false,true})
        {
            Prepare(app,action=="engine"?3:0);var g=app.Game;g.save.inventory[Item(action)]=0;Open(app,action,pad);var retained=g.conversation;
            foreach(int stock in new[]{0,Required(action)-1})
            {
                g.save.inventory[Item(action)]=stock;Denied(app,action);object quote=Quote(g,action);
                Check(Get<string>(quote,"action")==action&&Get<int>(quote,"item")==Item(action)
                    &&Get<int>(quote,"required")==Required(action)&&Get<int>(quote,"stock")==stock
                    &&Get<int>(quote,"missing")==Required(action)-stock&&!Available(quote),
                    "The real disabled row reports its original material, full need, current stock and exact shortfall");
            }
            g.save.inventory[Item(action)]=Required(action);Control(app,action,true);
            Check(ReferenceEquals(g.conversation,retained)&&Available(Quote(g,action)),"Depositing the last missing item reenables the same existing numbered row without reopening dialogue");
            ExactPayment(app,action,pad?3:0);
        }
        Console.WriteLine("PASS actual E / B opens all five original handoffs; missing and one-short numeric/mouse/Enter/controller/direct selections keep dialogue/resources; refilling reenables the same row and pays once; NATIVE_UNVERIFIED");
    }
    static string ChoiceId(FrontierRuntime app,string action)
    { Rect r=Slot(app.Game,action);return (string)Call(app,"ControlId",r.x,r.y,r.width,r.height); }
    static void SafeUnavailableDefaultAndDeliberateShop(FrontierRuntime app)
    {
        foreach(string action in new[]{"plant","engine"})foreach(KeyCode key in new[]{KeyCode.Return,KeyCode.JoystickButton0})
        {
            Prepare(app,action=="engine"?3:0);var g=app.Game;for(int i=0;i<8;i++)g.save.inventory[i]=0;g.save.coins=25;
            Open(app,action);var nav=Get<MenuNavigation>(app,"navigation");
            Check(nav.focused==ChoiceId(app,"close"),"An unavailable authored first handoff defaults to the existing leave control, never an unrelated paid shop");
            string before=JsonUtility.ToJson(g.save);Key(app,key);Draw(app);
            Check(g.mode=="world"&&g.conversation==null&&JsonUtility.ToJson(g.save)==before,"Unassisted Enter / controller A closes a shortage dialogue safely without spending coins, planting or delivering");
        }
        foreach(int input in new[]{0,1,2,3})
        {
            Prepare(app);var g=app.Game;for(int i=0;i<8;i++)g.save.inventory[i]=0;g.save.coins=25;Open(app,"plant");
            Activate(app,"seeds",input);
            Check(g.mode=="world"&&g.save.coins==20&&g.save.inventory[7]==3&&!g.Has("ally_1")&&g.save.plantedAt<0,
                "The original seed shop remains deliberately reachable by number, mouse, Tab + Enter and controller A at its exact old five-coin / three-seed price");
        }
        foreach(KeyCode key in new[]{KeyCode.Return,KeyCode.JoystickButton0})
        {
            Prepare(app);var g=app.Game;Open(app,"ally1");
            Check(Get<MenuNavigation>(app,"navigation").focused==ChoiceId(app,"ally1"),"A payable authored first handoff keeps its original default selection");
            int herbs=g.save.inventory[2],coins=g.save.coins,stew=g.save.inventory[6],points=g.save.skillPoints;Key(app,key);Draw(app);
            Check(g.mode=="world"&&g.Has("ally_1")&&g.save.inventory[2]==herbs-6&&g.save.coins==coins+40&&g.save.inventory[6]==stew+1&&g.save.skillPoints==points+1,
                "Unassisted Enter / controller A still completes the ready original first handoff exactly once");
            Prepare(app);g=app.Game;g.save.inventory[3]=0;Open(app,"ally2");
            Check(Get<MenuNavigation>(app,"navigation").focused==ChoiceId(app,"fish"),"A free authored first fishing action keeps its original default despite a later unavailable handoff");
            string before=JsonUtility.ToJson(g.save);Key(app,key);Draw(app);
            Check(g.mode=="fish"&&JsonUtility.ToJson(g.save)==before,"Unassisted Enter / controller A preserves the original free-fishing default and resources");
        }
    }
    static void OriginalTransactionsAndStaleCompletionStayIntact(FrontierRuntime app)
    {
        Baseline(app);
        SafeUnavailableDefaultAndDeliberateShop(app);
        foreach(string action in Actions)foreach(int input in new[]{0,1,2,3})
        {
            Prepare(app,action=="engine"?3:0);var g=app.Game;g.save.inventory[Item(action)]=Required(action);Open(app,action);
            ExactPayment(app,action,input);Check(!Available(Quote(g,action)),"A successful original one-time handoff or occupied crop is no longer payable");
            if(action.StartsWith("ally",StringComparison.Ordinal))
            { Check(g.GetQuest(action).complete,"The mature journal commission resolves from its original ally flag");g.Close();Host(app,Kind(action));Key(app,KeyCode.E);Pages(app);Check(!Array.Exists(g.conversation.choices,c=>c.action==action),"A completed resident request is removed by its original dialogue branch"); }
        }
        foreach(string action in Actions)
        {
            Prepare(app,action=="engine"?3:0);var g=app.Game;Open(app,action);Check(Available(Quote(g,action)),"A displayed eligible original offer initially has its supplies");
            if(action=="plant")g.save.plantedAt=g.save.totalTime;else g.save.flags.Add(CompletedFlag(action));
            Denied(app,action);Check(!Available(Quote(g,action)),"The same displayed offer immediately refuses a newly completed flag or occupied crop");
        }
        // All mature free and related actions retain their own independent rules.
        Prepare(app);var game=app.Game;Open(app,"plant");ExactPayment(app,"plant",1);Open(app,"harvest");
        float planted=game.save.plantedAt;int herbs=game.save.inventory[2],seeds=game.save.inventory[7],coins=game.save.coins;
        Activate(app,"harvest",0);Check(game.mode=="world"&&game.save.plantedAt==planted&&game.save.inventory[2]==herbs,"The existing early harvest/check-growth action stays selectable and does not fabricate a harvest");
        game.save.totalTime+=60;Open(app,"harvest");Activate(app,"harvest",1);
        Check(game.save.plantedAt<0&&game.save.inventory[2]==herbs+5+game.save.projects[0]*2&&game.save.inventory[7]==seeds+2&&game.save.coins==coins+8,
            "The original sixty-second crop keeps project-scaled herbs, two seeds and eight coins");
        Prepare(app);game=app.Game;Open(app,"ally2");string before=JsonUtility.ToJson(game.save);Activate(app,"fish",3);
        Check(game.mode=="fish"&&JsonUtility.ToJson(game.save)==before,"Original free lake fishing remains reachable through controller A without charging bait or handoff materials");
        Prepare(app);game=app.Game;foreach(string flag in new[]{"ally_1","ally_2","ally_3"})game.save.flags.Add(flag);
        Host(app,"garden");Key(app,KeyCode.E);Pages(app);
        Check(Array.Exists(game.conversation.choices,c=>c.action=="supper_prep_0")&&game.DialogueChoiceEnabled("supper_prep_0")
            &&Array.Exists(game.conversation.choices,c=>c.action=="plant")&&Array.Exists(game.conversation.choices,c=>c.action=="seeds"),
            "All three town contributions retain the mature shared-supper narrative, planting and seed shop controls");
        Activate(app,"close",3);Check(game.mode=="world","The original leave control stays safe after town requests are complete");
        Console.WriteLine("PASS exact original payments/rewards through numeric/mouse/Enter/controller, live completion/crop revalidation and one-time journal flags; safe unavailable default and deliberate seed purchase with original ready/free defaults; unchanged growth/harvest/fishing/seeds/shared-supper/leave routes; NATIVE_UNVERIFIED");
    }
    static void Append(StringBuilder s,object value,HashSet<object> seen)
    {
        if(value==null){s.Append("null;");return;}Type type=value.GetType();if(type.IsPrimitive||type.IsEnum||value is string||value is decimal){s.Append(Convert.ToString(value,CultureInfo.InvariantCulture)).Append(';');return;}
        if(!type.IsValueType&&!seen.Add(value)){s.Append("ref;");return;}var seq=value as IEnumerable;if(seq!=null){foreach(object item in seq)Append(s,item,seen);s.Append("end;");return;}
        var fields=type.GetFields(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);Array.Sort(fields,(a,b)=>string.CompareOrdinal(a.Name,b.Name));foreach(var f in fields){s.Append(f.Name).Append('=');Append(s,f.GetValue(value),seen);}
    }
    static string State(GameCore g){var s=new StringBuilder();Append(s,g,new HashSet<object>());return s.ToString();}
    static string Files(){if(!Directory.Exists(Application.persistentDataPath))return "";var paths=Directory.GetFiles(Application.persistentDataPath,"*",SearchOption.AllDirectories);Array.Sort(paths,StringComparer.Ordinal);var s=new StringBuilder();foreach(string p in paths)s.Append(p).Append(Convert.ToBase64String(File.ReadAllBytes(p)));return s.ToString();}
    static void SavedQuestsAndPreparedReadsArePure(FrontierRuntime app)
    {
        Baseline(app);
        foreach(int version in new[]{1,2})
        {
            Prepare(app);var g=app.Game;g.TrackQuest("ally1");Open(app,"ally1");ExactPayment(app,"ally1",0);Open(app,"plant");ExactPayment(app,"plant",1);
            float planted=g.save.plantedAt;int herbs=g.save.inventory[2],seeds=g.save.inventory[7],coins=g.save.coins;
            g.PrepareSave();g.save.version=version;Directory.CreateDirectory(Application.persistentDataPath);File.WriteAllText(SaveStore.PathFor(1),JsonUtility.ToJson(g.save));
            string warning;var saved=SaveStore.Read(1,out warning);Check(saved!=null&&warning==null,"Real reader accepts original raw v"+version+" town progress");
            g.Load(saved);g.Close();g.enemies.Clear();
            Check(g.Has("ally_1")&&g.GetQuest("ally1").complete&&g.save.trackedQuest=="ally1"&&g.Has("handoff_runtime_mature_story")
                &&Near(g.save.plantedAt,planted)&&g.save.inventory[2]==herbs&&g.save.inventory[7]==seeds&&g.save.coins==coins,
                "Legacy restoration retains planted growth, consumed material, once-only reward and the exact tracked mature quest; v="+version+" ally="+g.Has("ally_1")+" complete="+g.GetQuest("ally1").complete+" tracked="+g.save.trackedQuest+" story="+g.Has("handoff_runtime_mature_story")+" planted="+g.save.plantedAt+"/"+planted+" herbs="+g.save.inventory[2]+"/"+herbs+" seeds="+g.save.inventory[7]+"/"+seeds+" coins="+g.save.coins+"/"+coins);
            Check(SaveStore.Write(g.save,0),"Actual checksum writer accepts legitimate original handoff progress");g.Load(SaveStore.Read(0,out warning));g.Close();g.enemies.Clear();
            Check(warning==null&&g.Has("ally_1")&&Near(g.save.plantedAt,planted)&&g.save.inventory[7]==seeds&&g.save.coins==coins,
                "Checksum restoration cannot replay a completed delivery or reset the original crop clock");
            g.Enter(3);g.enemies.Clear();Open(app,"engine");ExactPayment(app,"engine",2);g.PrepareSave();int parts=g.save.inventory[4];
            Check(SaveStore.Write(g.save,0),"Actual checksum writer persists the original repaired elevator");g.Load(SaveStore.Read(0,out warning));g.Close();g.enemies.Clear();
            Check(warning==null&&g.Has("gate_3")&&g.save.inventory[4]==parts&&g.save.trackedQuest=="ally1","Repaired main-story gate and paid parts survive restoring the actual mine snapshot");
            string raw=JsonUtility.ToJson(g.save);Check(!raw.Contains("TownHandoff")&&!raw.Contains("missing")&&!raw.Contains("required"),"Read-only live handoff requirements add no fields to the existing save schema");
        }
        Prepare(app);var game=app.Game;Host(app,"garden");game.save.weapon=1;game.save.weaponStyles[1]=1;Key(app,KeyCode.J);ActorAction owned=game.playerAction;
        Check(owned!=null&&!owned.released,"Actual J accepts an owned paid bow preparation before dialogue");Key(app,KeyCode.E);Pages(app);
        Check(game.mode=="dialog"&&ReferenceEquals(game.playerAction,owned),"Actual E preserves the already paid bow while opening the original resident");
        game.save.inventory[7]=0;game.PrepareSave();Check(SaveStore.Write(game.save,0),"Purity fixture protects real checksum bytes");
        string state=State(game),files=Files();float clock=Get<float>(app,"clock");
        foreach(EventType evt in new[]{EventType.Layout,EventType.Repaint})for(int n=0;n<4;n++)
        {
            Event.current.type=evt;foreach(string action in Actions){Check(Quote(game,action)!=null,"Every original handoff has a pure readable live quote");ChoiceLabel(game,new Choice("原选项","Original choice",action));}
            Check(Quote(game,"close")==null,"An unrelated free story choice acquires no handoff quote");Draw(app);
            Check(State(game)==state&&Files()==files&&Get<float>(app,"clock")==clock,"Repeated actual layout/repaint and live labels leave Core/RNG/prepared action/disk/world time unchanged");
        }
        Event.current.type=EventType.Repaint;Denied(app,"plant");
        Check(ReferenceEquals(game.playerAction,owned)&&!owned.released,"Rejected disabled handoffs preserve the paid bow and never release a hidden attack");
        Console.WriteLine("PASS real raw v1/v2 plus checksum crop/commission/elevator continuity, tracked mature quest and old schema; pure repeated real OnGUI and quote reads preserve saved bytes, RNG/world/action clocks and owned paid bow; NATIVE_UNVERIFIED");
    }
    static void BilingualOriginalLayoutsAndChoiceOrder(FrontierRuntime app)
    {
        Baseline(app);
        foreach(bool english in new[]{false,true})foreach(bool large in new[]{false,true})foreach(int width in new[]{960,1280})
        foreach(string action in new[]{"plant","ally2","ally3","engine"})foreach(int balance in new[]{0,1,2})
        {
            Prepare(app,action=="engine"?3:0);var g=app.Game;g.save.english=english;g.save.largeText=large;
            Screen.width=width;Screen.height=width==960?600:800;Call(app,"Resize");
            foreach(int item in new[]{2,3,4,7})g.save.inventory[item]=balance==0?0:balance==1?31:9999;
            Open(app,action);var c=g.conversation;Check(c.choices.Length>=2&&c.choices.Length<=9,"Every original resident choice retains a reachable unchanged page");
            string suffix=(english?"en":"zh")+(large?"-large-":"-normal-")+width+"-"+Kind(action)+"-"+balance;Draw(app,suffix);
            var labels=Labels();foreach(Choice choice in c.choices)
            {
                int i=Array.IndexOf(c.choices,choice);Rect r=Slot(g,choice.action);string text=(i+1)+"  "+ChoiceLabel(g,choice);
                Check(labels.Exists(l=>l.text==text&&Same(new Rect(l.r[0],l.r[1],l.r[2],l.r[3]),r)),"Every numbered original choice displays its full localized label in its unchanged geometry");
                object quote=Quote(g,choice.action);
                if(quote!=null)
                {
                    var button=GUI.buttonCalls.Find(b=>Same(b.rect,r));Check(button!=null&&button.enabled==Available(quote),"Each captured live handoff control reflects actual availability");
                    Check(text.Contains(Get<int>(quote,"stock")+"/"+Get<int>(quote,"required")),"Changed original label exposes current stock and exact full need");
                }
                else Check(ChoiceLabel(g,choice)==g.SupplyServiceChoiceLabel(choice),"Unrelated story/shop/forge labels retain their mature formatter exactly");
            }
            string[] original=Kind(action)=="garden"?new[]{"ally1","plant","seeds","close"}:Kind(action)=="fish"?new[]{"fish","ally2","close"}:Kind(action)=="forge"?new[]{"upgrade","bow","staff","ally3","close"}:new[]{"engine","close"};
            for(int i=0;i<original.Length;i++)Check(c.choices[i].action==original[i],"Original town handoff/control number stays in its authored position: "+Kind(action));
            Check(g.DialogueChoiceEnabled("close"),"Original free leave choice is always available regardless of shortage");
        }
        Check(folder==null||captures==96,"Shortage/ready/9999 stock across four original hosts has all 96 actual bilingual normal/large 960/1280 captures");
        Console.WriteLine("PASS original numbered plant/three-delivery/elevator geometry/order, original free/story/shop labels and live missing/ready/9999 stock in bilingual normal/large 960/1280 OnGUI; 96 optional full dialogue captures; NATIVE_UNVERIFIED");
    }
    public static int Main(string[] args)
    {
        string prior=Application.persistentDataPath,directory=Path.Combine(Path.GetTempPath(),"starfall-town-handoff-"+Guid.NewGuid());Application.persistentDataPath=directory;
        folder=args.Length>0&&args[0]!="-"?args[0]:null;prefix=args.Length>1?args[1]:"qa-town-handoff";var app=new FrontierRuntime();int failed=0;
        try
        {
            Call(app,"Awake");Check(app.Game!=null,"Actual Awake loads the complete shipped campaign");
            foreach(Action<FrontierRuntime> test in new Action<FrontierRuntime>[]{MissingMaterialsRetainAndRefillOriginalRows,OriginalTransactionsAndStaleCompletionStayIntact,SavedQuestsAndPreparedReadsArePure,BilingualOriginalLayoutsAndChoiceOrder})
                try{test(app);}catch(Exception error){failed++;Console.WriteLine("FAIL "+test.Method.Name+": "+(error.InnerException??error).Message);}
        }
        finally {app.EndVisualReview();Call(app,"OnDestroy");Application.persistentDataPath=prior;Input.Clear();GUI.queuedClick=null;GUI.color=Color.white;GUI.matrix=Matrix4x4.identity;GUI.enabled=true;if(Directory.Exists(directory))Directory.Delete(directory,true);}
        Console.WriteLine("Runtime town handoffs: "+(4-failed)+"/4 groups passed; NATIVE_UNVERIFIED");return failed==0?0:1;
    }
}
