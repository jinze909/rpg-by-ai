// Real production Update, OnGUI, original provision transactions and SaveStore paths.
// Display-only camera/timer fixtures are labelled; native Unity remains unverified.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using Starfall;
using UnityEngine;

public static class RuntimeProvisionStatusChecks
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static string captureFolder;
    private static int captures;
    private static string capturePrefix="qa-provision-status";
    private sealed class Label { public string text = null; public float[] r = null, color = null; public int size = 0; public bool center = false; }
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private static bool Near(float a, float b, float tolerance = .003f) { return Math.Abs(a - b) <= tolerance; }
    private static bool Same(Rect a, Rect b) { return Near(a.x,b.x) && Near(a.y,b.y) && Near(a.width,b.width) && Near(a.height,b.height); }
    private static bool Overlap(Rect a, Rect b) { return a.x < b.x+b.width && b.x < a.x+a.width && a.y < b.y+b.height && b.y < a.y+a.height; }
    private static object Call(object target, string name, params object[] args)
    { var method=target.GetType().GetMethod(name,Private|BindingFlags.Public); Check(method!=null,"Production entry point exists: "+name); return method.Invoke(target,args); }
    private static void Set(FrontierRuntime app, string name, object value) { typeof(FrontierRuntime).GetField(name,Private).SetValue(app,value); }
    private static T Get<T>(FrontierRuntime app, string name) { return (T)typeof(FrontierRuntime).GetField(name,Private).GetValue(app); }
    private static bool Visible(FrontierRuntime app)
    { var property=typeof(FrontierRuntime).GetProperty("ProvisionStatusVisible",Private); Check(property!=null,"Live provision presentation exists"); return (bool)property.GetValue(app,null); }
    private static Rect Bounds(FrontierRuntime app) { return (Rect)Call(app,"ProvisionStatusBounds"); }
    private static void Update(FrontierRuntime app) { Call(app,"Update"); Input.keys.Clear(); }
    private static void Frames(FrontierRuntime app, int count) { for(int i=0;i<count;i++)Update(app); }
    private static void Key(FrontierRuntime app, KeyCode key) { Input.keys.Add(key); Update(app); }
    private static void Draw(FrontierRuntime app, string capture = null)
    {
        GUI.commands.Clear(); GUI.textureCalls.Clear(); GUI.buttonCalls.Clear(); Call(app,"OnGUI");
        if(captureFolder!=null && capture!=null)
        {
            var commands=GUI.commands.ToArray(); var textures=GUI.textureCalls.ToArray(); var buttons=GUI.buttonCalls.ToArray();
            GUI.Export(captureFolder,capturePrefix+"-"+capture); captures++;
            GUI.commands.AddRange(commands); GUI.textureCalls.AddRange(textures); GUI.buttonCalls.AddRange(buttons);
        }
    }
    private static bool Has(string text) { return GUI.commands.Exists(c=>c.Contains("\"text\":"+GUI.S(text))); }
    private static bool HasPart(string text) { return GUI.commands.Exists(c=>c.Contains("\"k\":\"text\"") && c.Contains(text)); }
    private static string Heading(GameCore g,bool tea)
    { float remaining=Math.Max(0,(tea?g.save.teaUntil:g.save.foodUntil)-g.save.totalTime);return g.L(tea?"凉茶保护 ":"炖汤强化 ",tea?"TEA WARD ":"STEW BOOST ")+Math.Ceiling(remaining).ToString("0",CultureInfo.InvariantCulture)+g.L("秒","s"); }
    private static Label LabelFor(string text)
    { string command=GUI.commands.Find(c=>c.Contains("\"text\":"+GUI.S(text))); Check(command!=null,"Actual OnGUI reports truthful provision effect: "+text);return JsonUtility.FromJson<Label>(command); }
    private static void AssertStew(FrontierRuntime app)
    { Check(Has(Heading(app.Game,false)),"Actual OnGUI exposes consumed stew's remaining attack boost"); Check(Has(app.Game.L("基础攻击 +7","Base attack +7")),"Stew card states the original base damage bonus, not a multiplied hit promise"); }
    private static void AssertTea(FrontierRuntime app)
    { Check(Has(Heading(app.Game,true)),"Actual OnGUI exposes consumed tea's remaining climate/recovery protection"); Check(Has(app.Game.L("耐力回复 +6/秒 · 抗暑寒","EN +6/s · heat/cold ward")),"Tea card states the original recovery and climate benefits"); }
    private static void Activate(FrontierRuntime app,bool tea=true)
    { Key(app,KeyCode.F);if(tea)Key(app,KeyCode.T);Draw(app);AssertStew(app);if(tea)AssertTea(app); }
    private static void Pointer(FrontierRuntime app, Point pixel)
    { float scale=Get<float>(app,"scale"); Input.mousePosition=new Vector3(Get<float>(app,"offsetX")+pixel.x*scale,Screen.height-Get<float>(app,"offsetY")-pixel.y*scale,0); }
    private static void Prepare(FrontierRuntime app, int profession=0, int room=0)
    {
        Input.Clear(); GUI.queuedClick=null; GUI.matrix=Matrix4x4.identity; GUI.color=Color.white; GUI.enabled=true;
        Screen.width=1280; Screen.height=800; Time.unscaledDeltaTime=.02f; Event.current.type=EventType.Repaint;
        app.BeginVisualReview(profession,room); app.VisualReviewPaused=false; var g=app.Game;
        g.enemies.Clear(); g.projectiles.Clear(); g.hazards.Clear(); g.effects.Clear(); g.fallen.Clear();
        g.save.companion=-1; g.save.english=true; g.save.largeText=false; g.save.showGuidance=false;
        g.save.reducedMotion=true; g.save.effectsQuality=0; g.save.energy=100; g.save.health=g.save.maxHealth;
        g.save.relics=new[]{-1,-1}; g.save.talents=new int[3]; g.save.totalTime=100; g.save.foodUntil=g.save.teaUntil=g.save.mealUntil=0;
        g.save.inventory[6]=8; g.save.tea=8; g.save.technique=0; g.guardTime=0; g.invulnerable=0; g.toastTime=0;
        Set(app,"applicationPaused",false); Set(app,"pointerSeen",false); Set(app,"pointerAimActive",false); Set(app,"controllerUsed",false);
        Check(app.SetVisualReviewPosition(new Point(15,14)),"Fixture uses actual valid town footing");
        Set(app,"combatHudTime",0f); Set(app,"resumeTime",0f); Set(app,"arrivalTime",0f); Set(app,"camera",g.Position); Call(app,"Resize"); Pointer(app,new Point(900,390));
    }
    private static void Click(FrontierRuntime app,Rect rect)
    { Draw(app);Check(GUI.buttonCalls.Exists(b=>b.enabled&&Same(b.rect,rect)),"Actual original provision control is enabled");GUI.queuedClick=rect;Draw(app);Check(!GUI.queuedClick.HasValue,"Original provision button consumes the intended click"); }
    private static void ActualConsumptionAndBenefits(FrontierRuntime app)
    {
        Prepare(app);var g=app.Game;float unboosted=g.Damage;g.save.health-=9;g.save.energy=12;int stock=g.save.inventory[6];
        Key(app,KeyCode.F);Draw(app,"real-f");AssertStew(app);
        Check(g.save.inventory[6]==stock-1&&Near(g.save.health,g.save.maxHealth)&&Near(g.save.energy,100)&&Near(g.save.foodUntil-g.save.totalTime,90),"Real F keeps one stew, actual capped healing, full energy and original 90 seconds");
        Check(Near(g.Damage-unboosted,7),"The advertised +7 is the unchanged live base attack bonus");
        g.save.energy=12;g.save.heat=55;g.save.cold=57;stock=g.save.tea;Key(app,KeyCode.T);Draw(app,"real-t");AssertStew(app);AssertTea(app);
        Check(g.save.tea==stock-1&&g.save.energy==100&&g.save.heat==0&&g.save.cold==0&&Near(g.save.teaUntil-g.save.totalTime,180),"Real T preserves one tea, full energy, climate reset and original 180 seconds");
        g.save.energy=10;float before=g.save.energy;Frames(app,10);Check(Near(g.save.energy-before,4),"Ranger's actual ten updates retain original 14+6 energy per second");
        float stewRemaining=g.save.foodUntil-g.save.totalTime,teaRemaining=g.save.teaUntil-g.save.totalTime;Frames(app,25);Draw(app);AssertStew(app);AssertTea(app);
        Check(Near(g.save.foodUntil-g.save.totalTime,stewRemaining-.5f)&&Near(g.save.teaUntil-g.save.totalTime,teaRemaining-.5f),"Both visible effects follow only the shared real world clock");
        Prepare(app);g=app.Game;Key(app,KeyCode.JoystickButton4);Draw(app,"real-pad-stew");AssertStew(app);Check(g.save.inventory[6]==7,"Original controller LB uses exactly one stew");
        foreach(bool combat in new[]{false,true})
        {
            Prepare(app);g=app.Game;Set(app,"combatHudTime",combat?3f:0f);
            Click(app,combat?new Rect(515,732,78,50):new Rect(470,699,109,75));Draw(app);AssertStew(app);
            Set(app,"combatHudTime",combat?3f:0f);Click(app,combat?new Rect(600,732,78,50):new Rect(586,699,109,75));Draw(app);AssertTea(app);
            Check(g.save.inventory[6]==7&&g.save.tea==7,"Original HUD buttons consume one item each in both layouts");
        }
        Prepare(app);g=app.Game;Key(app,KeyCode.I);Click(app,new Rect(947,546,246,35));Click(app,new Rect(947,590,246,35));
        Check(g.mode=="inventory"&&g.save.inventory[6]==7&&g.save.tea==7,"Inventory clicks preserve the original paused item transactions");
        Draw(app);Check(!HasPart("STEW BOOST ")&&!HasPart("TEA WARD "),"A paused inventory does not advertise world-active HUD cards");Key(app,KeyCode.Escape);Draw(app,"inventory-return");AssertStew(app);AssertTea(app);
        Prepare(app);g=app.Game;Key(app,KeyCode.J);ActorAction action=g.playerAction;Check(action!=null&&!action.released,"Actual J prepares an original attack");float captured=action.damage;Key(app,KeyCode.F);Draw(app);AssertStew(app);
        Check(ReferenceEquals(action,g.playerAction)&&action.damage==captured,"Consuming stew cannot rewrite a prepared attack's captured damage or ownership");
        Prepare(app);g=app.Game;g.save.inventory[6]=g.save.tea=0;Key(app,KeyCode.F);Key(app,KeyCode.T);Draw(app);Check(!HasPart("STEW BOOST ")&&!HasPart("TEA WARD "),"Rejected empty consumption cannot invent an active effect");
        Console.WriteLine("PASS real F/T, controller LB, both original HUDs and inventory clicks: original costs/healing/energy/climate/90s/180s, live +7 base damage and +6/s recovery, clock countdown, captured attack unchanged, empty stock rejected; NATIVE_UNVERIFIED");
    }
    private static void Append(StringBuilder text,object value,HashSet<object> seen)
    {
        if(value==null){text.Append("null;");return;}Type type=value.GetType();
        if(type.IsPrimitive||type.IsEnum||value is string||value is decimal){text.Append(Convert.ToString(value,CultureInfo.InvariantCulture)).Append(';');return;}
        if(!type.IsValueType&&!seen.Add(value)){text.Append("ref;");return;}var sequence=value as IEnumerable;
        if(sequence!=null){foreach(object item in sequence)Append(text,item,seen);text.Append("end;");return;}
        var fields=type.GetFields(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);Array.Sort(fields,(a,b)=>string.CompareOrdinal(a.Name,b.Name));
        foreach(var field in fields){text.Append(field.Name).Append('=');Append(text,field.GetValue(value),seen);}
    }
    private static string State(GameCore g){var text=new StringBuilder();Append(text,g,new HashSet<object>());return text.ToString();}
    private static void Hidden(FrontierRuntime app,string message)
    { Draw(app);Check(!Visible(app)&&!HasPart("STEW BOOST ")&&!HasPart("TEA WARD "),message); }
    private static void PauseReadOnlyAndExpiry(FrontierRuntime app)
    {
        Prepare(app);Activate(app);var g=app.Game;g.skillCooldown=.12f;g.Skill(new Point(1,0));
        Check(typeof(GameCore).GetField("bufferedSkill",Private).GetValue(g)!=null,"Fixture has a real accepted queued skill");
        string state=State(g);float clock=Get<float>(app,"clock");var matrix=Matrix4x4.TRS(new Vector3(13,21,0),Quaternion.Euler(0,0,11),new Vector3(1.25f,1.25f,1));var tint=new Color(.3f,.4f,.5f,.6f);
        foreach(EventType evt in new[]{EventType.Layout,EventType.Repaint})
        {Event.current.type=evt;GUI.matrix=matrix;GUI.color=tint;GUI.enabled=false;Draw(app);AssertStew(app);AssertTea(app);Check(State(g)==state&&Get<float>(app,"clock")==clock,"Repeated actual Layout/Repaint preserves every Core/save/queue field and presentation clock");Check(GUI.matrix.Equals(matrix)&&GUI.color.Equals(tint)&&!GUI.enabled,"Actual OnGUI restores caller matrix/tint/enabled state");}
        Event.current.type=EventType.Repaint;GUI.matrix=Matrix4x4.identity;GUI.color=Color.white;GUI.enabled=true;
        Key(app,KeyCode.I);float time=g.save.totalTime;Frames(app,20);Hidden(app,"Inventory hides provision HUD while world time pauses");Check(g.save.totalTime==time,"Inventory cannot spend buff duration");Key(app,KeyCode.Escape);
        Call(app,"OnApplicationPause",true);time=g.save.totalTime;Frames(app,10);Hidden(app,"Application pause hides provision cards");Check(g.save.totalTime==time,"Application pause freezes buffs");Call(app,"OnApplicationPause",false);
        app.VisualReviewPaused=true;Frames(app,10);Hidden(app,"Paused review hides provision cards");Check(g.save.totalTime==time,"Paused review freezes buffs");app.VisualReviewPaused=false;
        g.hitStop=.1f;time=g.save.totalTime;Frames(app,3);Draw(app);AssertStew(app);AssertTea(app);Check(g.save.totalTime==time,"Hit stop leaves authoritative effect durations frozen and truthfully visible");
        g.hitStop=0;g.ClearBufferedCombatInputs();g.save.foodUntil=g.save.totalTime+.04f;g.save.teaUntil=g.save.totalTime+.08f;Draw(app);Check(Has("STEW BOOST 1s")&&Has("TEA WARD 1s"),"Positive fractional durations round up without showing an active zero");
        Frames(app,3);Draw(app,"staggered-expiry");Check(!HasPart("STEW BOOST ")&&Has("TEA WARD 1s")&&Near(Bounds(app).height,44),"Actual expiry removes only stew and compacts the remaining tea row");
        Frames(app,2);Hidden(app,"Natural expiry removes both rows without a lingering zero-second card");
        foreach(float invalid in new[]{0,-1,float.NaN,float.PositiveInfinity,float.NegativeInfinity}){g.save.foodUntil=g.save.teaUntil=invalid;Hidden(app,"Expired/nonfinite deadlines never advertise a status");}
        Prepare(app);Activate(app);g=app.Game;g.invulnerable=0;Call(g,"Hurt",10000f,"environment",null);Check(g.Dying,"Actual lethal damage starts death");Hidden(app,"Death never advertises active provision combat benefit");
        Prepare(app);Activate(app);g=app.Game;g.StartPractice();Check(g.practicing,"Actual original training starts");Hidden(app,"Training never advertises world provision buffs");
        Console.WriteLine("PASS actual read-only Layout/Repaint with queued skill and all Core fields, restored GUI state, inventory/application/review pause, hit stop, ceiling text, natural independent expiry, invalid deadlines, death and real training boundaries; NATIVE_UNVERIFIED");
    }
    private static void Interact(FrontierRuntime app,string kind)
    {WorldObject target=Array.Find(app.Game.Map.objects,o=>o.kind==kind);Check(target!=null&&app.SetVisualReviewPosition(new Point(target.x,target.y-1)),"Original rest interaction has valid reachable footing");Key(app,KeyCode.E);Check(app.Game.mode=="dialog"&&app.Game.conversation!=null,"Real E opens the original rest dialogue");}
    private static void Choose(FrontierRuntime app,string action)
    {
        int index=Array.FindIndex(app.Game.conversation.choices,c=>c.action==action);Check(index>=0&&index<9,"Original numbered dialogue exposes "+action);int page=0;
        do{Draw(app);if((bool)Call(app,"DialogueChoicesVisible"))break;Key(app,KeyCode.Return);}while(page++<20);
        Check((bool)Call(app,"DialogueChoicesVisible"),"Original dialogue pages complete before real numbered choice");Key(app,(KeyCode)((int)KeyCode.Alpha1+index));
    }
    private static void RestAndSavedContinuity(FrontierRuntime app)
    {
        Prepare(app);Activate(app);var g=app.Game;g.save.coins=73;g.save.inventory[0]=11;float food=g.save.foodUntil,tea=g.save.teaUntil;g.PrepareSave();
        Check(SaveStore.Write(g.save,0),"Actual checksum save writer accepts original timed provisions");string warning;SaveData saved=SaveStore.Read(0,out warning);Check(saved!=null&&warning==null,"Actual checksum reader restores provisions");string raw=JsonUtility.ToJson(saved);
        Check(!raw.Contains("ProvisionStatus")&&!raw.Contains("StewBoostRemaining")&&!raw.Contains("TeaBoostRemaining"),"HUD state and derived remaining values never expand the save schema");
        g.Load(saved);g.Start();Draw(app,"checksum-load");AssertStew(app);AssertTea(app);Check(Near(g.save.foodUntil,food)&&Near(g.save.teaUntil,tea)&&g.save.coins==73&&g.save.inventory[0]==11,"Checksum load preserves original deadlines, economy and materials: food="+g.save.foodUntil+"/"+food+", tea="+g.save.teaUntil+"/"+tea+", coins="+g.save.coins+", wood="+g.save.inventory[0]);
        foreach(int version in new[]{1,2})
        {
            SaveData old=JsonUtility.FromJson<SaveData>(raw);old.version=version;old.foodUntil=old.totalTime+110;old.teaUntil=old.totalTime+170;
            File.WriteAllText(SaveStore.PathFor(1),JsonUtility.ToJson(old));SaveData loaded=SaveStore.Read(1,out warning);Check(loaded!=null&&warning==null,"Raw legacy v"+version+" load succeeds");g.Load(loaded);g.Start();Draw(app,"legacy-v"+version);AssertStew(app);AssertTea(app);
            Check(Has("STEW BOOST 110s")&&Has("TEA WARD 170s")&&g.save.coins==73&&g.save.inventory[0]==11,"Legacy allowed 110-second stew is displayed truthfully without a new 90-second clamp; original balances remain");
        }
        Prepare(app);Activate(app);g=app.Game;food=g.save.foodUntil;tea=g.save.teaUntil;Interact(app,"rest");float before=g.save.totalTime;Choose(app,"rest");
        Check(Near(g.save.totalTime-before,45)&&g.save.foodUntil==food&&g.save.teaUntil==tea,"Actual original rest advances 45 seconds without renewing or rewriting either deadline");Draw(app,"real-rest");AssertStew(app);AssertTea(app);
        g.Enter(1);g.enemies.Clear();g.hazards.Clear();g.projectiles.Clear();Draw(app,"travel");AssertStew(app);AssertTea(app);Check(g.save.foodUntil==food&&g.save.teaUntil==tea,"Original region travel retains persisted provision deadlines");
        Console.WriteLine("PASS actual checksum SaveStore and raw legacy v1/v2 continuity, unchanged schema/economy/materials, truthful legacy 110s stew, actual E/numbered inn rest spending original 45s, and original travel preserving deadlines; NATIVE_UNVERIFIED");
    }
    private static void LayoutsThreatsAndPointer(FrontierRuntime app)
    {
        Prepare(app);Activate(app); // semantic baseline must fail on missing visible behavior before new private presentation reflection.
        foreach(bool english in new[]{false,true})foreach(bool large in new[]{false,true})foreach(bool combat in new[]{false,true})foreach(int count in new[]{0,1,2})
        {
            Prepare(app,1,6);Activate(app);var g=app.Game;g.save.english=english;g.save.largeText=large;g.save.foodUntil=count==0?0:g.save.totalTime+1.234f;g.save.teaUntil=count==1?0:g.save.totalTime+81.234f;
            Screen.width=large?960:1920;Screen.height=large?600:1080;Call(app,"Resize");g.guardTime=1.234f;g.riposteTime=1.234f;g.save.mealKind=2;g.save.mealUntil=g.save.totalTime+100;g.RestartTutorial();
            // Display-only receipt fixture observes already owned balances; no loot transaction is inferred from presentation setup.
            Call(g,"RecordCombatLoot",7,5,1,3,0,false);g.Notify("原有提示保留","Existing notification retained");Set(app,"combatHudTime",combat?3f:0f);
            Draw(app,"layout-"+(english?"en":"zh")+(large?"-large960":"-normal1920")+(combat?"-combat":"-exploration")+"-"+count);if(count!=0)AssertStew(app);if(count!=1)AssertTea(app);
            Rect bounds=Bounds(app);Check(Same(bounds,new Rect(combat?1030:1008,combat?184:568,combat?230:248,count==2?94:44)),"Both actual HUDs use stable provision rows outside original status lanes");
            Check(((List<Rect>)Call(app,"HudRegions")).Exists(r=>Same(r,bounds)),"Whole live provision bounds participate in shared HUD witness geometry");
            Label stew=LabelFor(Heading(g,count==0));Check(Same(new Rect(stew.r[0],stew.r[1],stew.r[2],stew.r[3]),new Rect(bounds.x+41,bounds.y+3,bounds.width-49,20)),"Actual header follows the declared icon/text geometry");
            Check(Has(g.RoadMealStatus)&&HasPart(g.L("防护 ","GUARD "))&&HasPart(g.L("战利品","LOOT"))&&Has(g.L("原有提示保留","Existing notification retained")),"Existing meal, Guard, loot and notification remain present beside the new cards");
            foreach(Rect mature in new[]{(Rect)Call(app,"RoadMealHudBounds",combat),(Rect)Call(app,"GuardStatusBounds"),(Rect)Call(app,"CombatLootBounds"),new Rect(24,474,298,204),combat?new Rect(20,103,260,26):new Rect(24,145,298,60),combat?new Rect(20,135,260,62):new Rect(382,132,516,67)})Check(!Overlap(bounds,mature),"Provision rows remain outside original meal/Guard/loot/tutorial/climate/toast bounds");
            Check(GUI.textureCalls.Exists(c=>c.texture==Texture2D.whiteTexture&&Same(c.rect,new Rect(bounds.x,bounds.y,bounds.width,44))&&Near(c.color.a,.85f)),"Unobstructed card preserves normal readable fill");
        }
        foreach(bool combat in new[]{false,true})
        {
            Prepare(app);Activate(app);var g=app.Game;Frames(app,30);Set(app,"combatHudTime",combat?3f:0f);Draw(app);Rect bounds=Bounds(app);Point pointer=new Point(bounds.x+30,bounds.y+12);
            Check(!(bool)Call(app,"PointerOverInterface",pointer),"Read-only provision cards pass world mouse input through");Point direction=(pointer-(Point)Call(app,"ScreenPoint",g.Position)).Normalized;Pointer(app,pointer);Input.buttons.Add(0);Update(app);Input.buttons.Clear();
            Check(g.playerAction!=null&&g.playerAction.kind=="attack"&&g.playerAction.direction.x*direction.x+g.playerAction.direction.y*direction.y>.99f,"Real mouse over provision status starts and aims a basic attack");
            Prepare(app);Activate(app);g=app.Game;Frames(app,30);Set(app,"combatHudTime",combat?3f:0f);Draw(app);Rect protectedSlot=combat?new Rect(515,732,78,50):new Rect(470,699,109,75);Point protectedPixel=new Point(protectedSlot.x+10,protectedSlot.y+10);Check((bool)Call(app,"PointerOverInterface",protectedPixel),"Original stew action retains pointer protection");Pointer(app,protectedPixel);Input.buttons.Add(0);Update(app);Input.buttons.Clear();Check(g.playerAction==null,"Original HUD action area cannot leak a held basic attack");
            // Display-only camera/threat fixture: used only to witness rendering order and opacity.
            var enemy=new Enemy(59401,0,24,14,80){state=1,timer=.3f,windupDuration=.65f,target=new Point(24,14)};enemy.Face(new Point(0,-1));g.enemies.Add(enemy);Set(app,"combatHudTime",combat?3f:0f);bounds=Bounds(app);Point targetPixel=new Point(bounds.x+bounds.width/2,bounds.y+30);Set(app,"camera",new Point(enemy.x-(targetPixel.x-640)/36f,enemy.y-(targetPixel.y-390)/36f));Draw(app,"threat-"+(combat?"combat":"exploration"));
            bounds=Bounds(app);Rect row=new Rect(bounds.x,bounds.y,bounds.width,44);Check((bool)Call(app,"ThreatOver",row),"Real enemy/warning envelope overlaps provision row");Check(GUI.textureCalls.Exists(c=>c.texture==Texture2D.whiteTexture&&Same(c.rect,row)&&Near(c.color.a,.12f)),"Overlapping provision background fades to actual .12 opacity");
            int fill=GUI.textureCalls.FindIndex(c=>c.texture==Texture2D.whiteTexture&&Same(c.rect,row));Check(GUI.textureCalls.FindLastIndex(c=>c.texture.name.Contains("outline_Monsters"))>fill,"Actual enemy outline remains visible after provision fill");GUI.commands.Clear();GUI.textureCalls.Clear();GUI.buttonCalls.Clear();Call(app,"DrawHudThreatWitness");Check(GUI.textureCalls.Exists(c=>c.texture.name.Contains("outline_Monsters"))&&GUI.textureCalls.FindAll(c=>c.rect.width==2).Count==33,"Shared witness redraw retains enemy and mature thirty-three-point melee warning");
        }
        // Display-only committed windups verify that the existing explanatory panel is never covered.
        foreach(int kind in new[]{0,1,2,3,4})
        {
            Prepare(app);Activate(app);var g=app.Game;
            int id=kind==3?GameCore.CommissionGuardianId:kind==4?GameCore.SignalWardenId:59402+kind;
            var tell=new Enemy(id,kind>=3?3:kind,g.Position.x+2,g.Position.y,260,kind>=3){state=1,timer=.3f,windupDuration=.65f,target=g.Position,cycle=1};tell.Face(new Point(-1,0));g.enemies.Add(tell);
            Draw(app,"mature-tell-"+kind);AssertStew(app);AssertTea(app);Rect panel=new Rect(24,228,298,90);
            string expected=kind==3?(string)Call(app,"CommissionMoveHint",tell):kind==4?(string)Call(app,"SignalMoveHint",tell):g.EnemyHint(tell);
            Label label=LabelFor(expected);Check(Same(new Rect(label.r[0],label.r[1],label.r[2],label.r[3]),new Rect(40,241,266,68)),"Original hunter/archer/heavy/guardian/warden tell keeps its complete text geometry");
            Check(GUI.textureCalls.Exists(c=>c.texture==Texture2D.whiteTexture&&Same(c.rect,panel))&&!Overlap(Bounds(app),panel),"New provision rows never cover the actual mature enemy-tell panel");
        }
        foreach(bool reduced in new[]{false,true})foreach(int quality in new[]{0,1,2})
        {Prepare(app);Activate(app);var g=app.Game;g.save.reducedMotion=reduced;g.save.effectsQuality=quality;Draw(app,"policy-"+reduced+"-"+quality);AssertStew(app);AssertTea(app);}
        Prepare(app);Activate(app);var game=app.Game;CultureInfo culture=CultureInfo.CurrentCulture;try{CultureInfo.CurrentCulture=new CultureInfo("fr-FR");game.save.foodUntil=game.save.totalTime+1.01f;game.save.teaUntil=game.save.totalTime+.01f;Draw(app,"comma-locale");Check(Has("STEW BOOST 2s")&&Has("TEA WARD 1s"),"Foreign locale keeps invariant ceiling countdowns");}finally{CultureInfo.CurrentCulture=culture;}
        Console.WriteLine("PASS zh/en normal1920/large960 stew-only/tea-only/two rows in both HUDs, original meal/Guard/loot/tutorial/climate/toast coexistence, exact bounds/shared witnesses, actual click-through attack and protected controls, threat opacity/redraw, locale and every reduced-motion/effects policy; NATIVE_UNVERIFIED");
    }
    public static int Main(string[] args)
    {
        string previous=Application.persistentDataPath,directory=Path.Combine(Path.GetTempPath(),"starfall-provision-status-"+Guid.NewGuid());Application.persistentDataPath=directory;
        captureFolder=args.Length>0&&args[0]!="-"?args[0]:null;if(args.Length>1)capturePrefix=args[1];if(captureFolder!=null)Directory.CreateDirectory(captureFolder);var app=new FrontierRuntime();int failed=0;
        try
        {Call(app,"Awake");Check(app.Game!=null,"Actual Awake loads shipped campaign");foreach(Action<FrontierRuntime> test in new Action<FrontierRuntime>[]{ActualConsumptionAndBenefits,PauseReadOnlyAndExpiry,RestAndSavedContinuity,LayoutsThreatsAndPointer})try{test(app);}catch(Exception error){failed++;Console.WriteLine("FAIL "+test.Method.Name+": "+(error.InnerException??error).Message);}}
        finally{app.EndVisualReview();Call(app,"OnDestroy");Application.persistentDataPath=previous;Input.Clear();GUI.queuedClick=null;if(Directory.Exists(directory))Directory.Delete(directory,true);}
        Console.WriteLine("Runtime provision status: "+(4-failed)+"/4 groups passed; NATIVE_UNVERIFIED");return failed==0?0:1;
    }
}
