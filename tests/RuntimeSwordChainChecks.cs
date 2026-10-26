// Executes shipped Update/OnGUI and original SaveStore. The sword-chain HUD is
// presentation of accepted attack starts, including misses; NATIVE_UNVERIFIED.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using Starfall;
using UnityEngine;

public static class RuntimeSwordChainChecks
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private static string captureFolder,capturePrefix="qa-sword-chain";
    private static int captures;
    private sealed class Label { public string text=null; public float[] r=null; public int size=0; }
    private static void Check(bool value,string message){if(!value)throw new Exception(message);}
    private static bool Near(float a,float b,float tolerance=.003f){return Math.Abs(a-b)<=tolerance;}
    private static bool Same(Rect a,Rect b){return Near(a.x,b.x)&&Near(a.y,b.y)&&Near(a.width,b.width)&&Near(a.height,b.height);}
    private static bool Overlap(Rect a,Rect b){return a.x<b.x+b.width&&b.x<a.x+a.width&&a.y<b.y+b.height&&b.y<a.y+a.height;}
    private static object Call(object target,string name,params object[] args)
    {var method=target.GetType().GetMethod(name,Private|BindingFlags.Public);Check(method!=null,"Production entry point exists: "+name);return method.Invoke(target,args);}
    private static void Set(FrontierRuntime app,string name,object value){typeof(FrontierRuntime).GetField(name,Private).SetValue(app,value);}
    private static T Get<T>(FrontierRuntime app,string name){return (T)typeof(FrontierRuntime).GetField(name,Private).GetValue(app);}
    private static bool Visible(FrontierRuntime app)
    {var property=typeof(FrontierRuntime).GetProperty("SwordChainVisible",Private);Check(property!=null,"Live sword-chain presentation exists");return (bool)property.GetValue(app,null);}
    private static Rect Bounds(FrontierRuntime app){return (Rect)Call(app,"SwordChainBounds");}
    private static void Update(FrontierRuntime app){Call(app,"Update");Input.keys.Clear();Input.buttonDown.Clear();}
    private static void Frames(FrontierRuntime app,int count){for(int i=0;i<count;i++)Update(app);}
    private static void Key(FrontierRuntime app,KeyCode key){Input.keys.Add(key);Update(app);}
    private static void Pointer(FrontierRuntime app,Point pixel)
    {float scale=Get<float>(app,"scale");Input.mousePosition=new Vector3(Get<float>(app,"offsetX")+pixel.x*scale,Screen.height-Get<float>(app,"offsetY")-pixel.y*scale,0);}
    private static void Press(FrontierRuntime app,string device)
    {
        if(device=="mouse"){Input.buttons.Add(0);Input.buttonDown.Add(0);}
        else{KeyCode key=device=="pad"?KeyCode.JoystickButton2:KeyCode.J;Input.keys.Add(key);Input.held.Add(key);}
        Update(app);Input.held.Clear();Input.buttons.Clear();
    }
    private static void Draw(FrontierRuntime app,string capture=null)
    {
        GUI.commands.Clear();GUI.textureCalls.Clear();GUI.buttonCalls.Clear();Call(app,"OnGUI");
        if(captureFolder!=null&&capture!=null){var commands=GUI.commands.ToArray();var textures=GUI.textureCalls.ToArray();var buttons=GUI.buttonCalls.ToArray();GUI.Export(captureFolder,capturePrefix+"-"+capture);captures++;GUI.commands.AddRange(commands);GUI.textureCalls.AddRange(textures);GUI.buttonCalls.AddRange(buttons);}
    }
    private static bool Has(string text){return GUI.commands.Exists(c=>c.Contains("\"text\":"+GUI.S(text)));}
    private static bool HasPart(string text){return GUI.commands.Exists(c=>c.Contains("\"k\":\"text\"")&&c.Contains(text));}
    private static string Regular(GameCore g,int step)
    {int next=step%3+1;return g.L("连段 ","CHAIN ")+step+"/3 · "+g.L("下一招","next ")+next+(next==3?" ×1.25":"")+" · "+(Math.Ceiling(Math.Max(0,g.comboTimer)*10)/10).ToString("0.0",CultureInfo.InvariantCulture)+g.L("秒","s");}
    private static string Finisher(GameCore g,bool released)
    {return g.L(released?"重斩已释放 · 招式 ×1.25":"重斩起手 · 招式 ×1.25",released?"HEAVY RELEASED · move ×1.25":"HEAVY WINDUP · move ×1.25");}
    private static void AssertRegular(FrontierRuntime app,int step)
    {Check(Has(Regular(app.Game,step)),"Actual OnGUI exposes accepted sword start "+step+" and its next move/window: "+Regular(app.Game,step));}
    private static void AssertFinisher(FrontierRuntime app,bool released)
    {Check(Has(Finisher(app.Game,released)),"Actual OnGUI prioritizes the captured third move's "+(released?"release":"windup"));}
    private static void Prepare(FrontierRuntime app,int room=0)
    {
        Input.Clear();GUI.queuedClick=null;GUI.matrix=Matrix4x4.identity;GUI.color=Color.white;GUI.enabled=true;Event.current.type=EventType.Repaint;
        Screen.width=1280;Screen.height=800;Time.unscaledDeltaTime=.02f;app.BeginVisualReview(0,room);app.VisualReviewPaused=false;var g=app.Game;
        g.enemies.Clear();g.projectiles.Clear();g.hazards.Clear();g.effects.Clear();g.fallen.Clear();g.save.companion=-1;g.save.english=true;g.save.largeText=false;g.save.showGuidance=false;
        g.save.reducedMotion=true;g.save.effectsQuality=0;g.save.health=g.save.maxHealth;g.save.energy=100;g.save.relics=new[]{-1,-1};g.save.talents=new int[3];g.save.totalTime=100;g.save.weapon=0;g.save.technique=0;
        g.save.foodUntil=g.save.teaUntil=g.save.mealUntil=0;g.guardTime=g.riposteTime=g.invulnerable=g.toastTime=0;
        Check(app.SetVisualReviewPosition(new Point(15,14)),"Fixture uses valid shipped town footing");Set(app,"applicationPaused",false);Set(app,"pointerSeen",false);Set(app,"pointerAimActive",false);Set(app,"controllerUsed",false);
        Set(app,"combatHudTime",0f);Set(app,"resumeTime",0f);Set(app,"arrivalTime",0f);Set(app,"camera",g.Position);Call(app,"Resize");Call(app,"ResetControllerAim",false);Pointer(app,new Point(900,390));
    }
    private static void Ready(FrontierRuntime app)
    {for(int i=0;i<100&&(app.Game.attackCooldown>0||app.Game.playerAction!=null);i++)Update(app);Check(app.Game.attackCooldown<=0&&app.Game.playerAction==null,"Original attack interval/action naturally finishes");}
    private static ActorAction Start(FrontierRuntime app,string device="keyboard")
    {Ready(app);Press(app,device);var a=app.Game.playerAction;Check(a!=null&&a.kind=="attack"&&!a.released,"Real press accepts an original sword attack");return a;}
    private static ActorAction Third(FrontierRuntime app,string device="keyboard")
    {Start(app,device);Draw(app);AssertRegular(app,1);Start(app,device);Draw(app);AssertRegular(app,2);var third=Start(app,device);Draw(app);AssertFinisher(app,false);return third;}
    private static void RealInputSequenceAndWindow(FrontierRuntime app)
    {
        foreach(string device in new[]{"keyboard","pad","mouse"})foreach(int style in new[]{0,1,2})
        {
            Prepare(app);var g=app.Game;g.save.weaponStyles[0]=style;if(device=="pad")Input.axes["Aim Horizontal"]=1;
            float baseDamage=g.Profile(0,style).damage;ActorAction first=Start(app,device);Draw(app,"real-"+device+"-style"+style+"-first");AssertRegular(app,1);
            Check(first.combo==0&&Near(first.damage,baseDamage)&&Near(g.attackCooldown,g.Profile(0,style).interval),"First real start keeps original damage, interval and combo index");
            ActorAction second=Start(app,device);Draw(app);AssertRegular(app,2);Check(second.combo==1&&Near(second.damage,baseDamage),"Second accepted miss keeps original normal damage; hits are not required");
            ActorAction third=Start(app,device);Draw(app,"real-"+device+"-style"+style+"-third");AssertFinisher(app,false);
            Check(third.combo==2&&Near(third.damage,baseDamage*1.25f),"Captured third retains original 1.25 sword move multiplier");
            while(!third.released)Update(app);Draw(app);AssertFinisher(app,true);Ready(app);Draw(app);AssertRegular(app,3);
            ActorAction fourth=Start(app,device);Draw(app);AssertRegular(app,1);Check(fourth.combo==0&&Near(fourth.damage,baseDamage),"Fourth start naturally wraps to one, without a new damage rule");
            Frames(app,60);Draw(app);Check(!HasPart("CHAIN ")&&!HasPart("HEAVY ")&&!Visible(app),"True 1.1-second continuation expiry hides completed misses");
            ActorAction restarted=Start(app,device);Draw(app);AssertRegular(app,1);Check(restarted.combo==0,"New press after actual expiry restarts original chain");
        }
        Prepare(app);var game=app.Game;Start(app);Draw(app);AssertRegular(app,1);game.attackCooldown=.5f;Press(app,"keyboard");Draw(app);AssertRegular(app,1);Check(game.combo==0,"Rejected early input does not invent a chain step");
        Ready(app);game.save.weaponStyles[0]=1;game.save.energy=0;Press(app,"keyboard");Draw(app);Check(game.combo==0,"Rejected paid attack cannot advance combo state");
        foreach(int weapon in new[]{1,2}){Prepare(app);game=app.Game;game.save.weapon=weapon;Start(app);Draw(app);Check(!HasPart("CHAIN ")&&!HasPart("HEAVY ")&&!Visible(app),"Bow/staff starts do not advertise sword finisher feedback");}
        Console.WriteLine("PASS real J/controller X/mouse starts across three sword styles: accepted misses, unchanged captured first/second/third damage and interval, third contact/release, fourth wrap, true 1.1s expiry/restart, rejected inputs and non-sword gating; NATIVE_UNVERIFIED");
    }
    private static void CommittedThirdAndCapturedOwnership(FrontierRuntime app)
    {
        Prepare(app);var g=app.Game;ActorAction a=Third(app);float damage=a.damage,elapsed=a.elapsed;g.playerHitTime=1.6f;Frames(app,60);Draw(app,"recoil-beyond-window");AssertFinisher(app,false);
        Check(g.comboTimer<=0&&ReferenceEquals(g.playerAction,a)&&a.elapsed==elapsed&&!a.released&&a.damage==damage,"Long recoil expires continuation without losing captured third action/damage/contact ownership");
        for(int i=0;i<90&&!a.released;i++)Update(app);Draw(app,"release-after-recoil");AssertFinisher(app,true);Check(a.released&&a.damage==damage&&g.effects.Exists(e=>e.kind=="slash"&&e.variant==2),"Original resumed contact releases the same third slash exactly on its action clock");
        Ready(app);Draw(app);Check(!Visible(app)&&!HasPart("HEAVY "),"Committed marker ends with its captured action even when continuation was already expired");
        Prepare(app);g=app.Game;Third(app);g.save.weapon=1;Draw(app);Check(!HasPart("HEAVY ")&&!Visible(app),"Changed equipped weapon never advertises stale captured sword ownership before next Tick");
        Prepare(app);g=app.Game;Third(app);g.save.weaponStyles[0]=1;Draw(app);Check(!HasPart("HEAVY "),"Changed style never labels the invalid old finisher as committed");Update(app);Check(g.playerAction==null,"Original next Tick cancels mismatched captured action");
        Prepare(app);g=app.Game;Start(app);Draw(app);AssertRegular(app,1);Ready(app);g.riposteTime=.5f;ActorAction second=Start(app);Draw(app);AssertRegular(app,2);Check(second.combo==1&&Near(second.damage,g.Profile(0,g.Style).damage*1.5f),"Counter multiplier retains original captured behavior without claiming a third move");
        Console.WriteLine("PASS captured third windup survives recoil longer than continuation window, original delayed contact/released marker and action expiry, changed weapon/style invalidation, and original independent counter damage ownership; NATIVE_UNVERIFIED");
    }
    private static void Append(StringBuilder text,object value,HashSet<object> seen)
    {
        if(value==null){text.Append("null;");return;}Type type=value.GetType();if(type.IsPrimitive||type.IsEnum||value is string||value is decimal){text.Append(Convert.ToString(value,CultureInfo.InvariantCulture)).Append(';');return;}
        if(!type.IsValueType&&!seen.Add(value)){text.Append("ref;");return;}var sequence=value as IEnumerable;if(sequence!=null){foreach(object item in sequence)Append(text,item,seen);text.Append("end;");return;}
        var fields=type.GetFields(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);Array.Sort(fields,(a,b)=>string.CompareOrdinal(a.Name,b.Name));foreach(var field in fields){text.Append(field.Name).Append('=');Append(text,field.GetValue(value),seen);}
    }
    private static string State(GameCore g){var text=new StringBuilder();Append(text,g,new HashSet<object>());return text.ToString();}
    private static void Hidden(FrontierRuntime app,string message){Draw(app);Check(!Visible(app)&&!HasPart("CHAIN ")&&!HasPart("HEAVY "),message);}
    private static void ReadOnlyPauseAndLegacySave(FrontierRuntime app)
    {
        Prepare(app);var g=app.Game;Start(app);Draw(app);AssertRegular(app,1);g.skillCooldown=.12f;g.Skill(new Point(1,0));Check(typeof(GameCore).GetField("bufferedSkill",Private).GetValue(g)!=null,"Fixture holds original accepted queued skill");
        string state=State(g);float clock=Get<float>(app,"clock");var matrix=Matrix4x4.TRS(new Vector3(13,21,0),Quaternion.Euler(0,0,11),new Vector3(1.25f,1.25f,1));var tint=new Color(.3f,.4f,.5f,.6f);
        foreach(EventType evt in new[]{EventType.Layout,EventType.Repaint}){Event.current.type=evt;GUI.matrix=matrix;GUI.color=tint;GUI.enabled=false;Draw(app);AssertRegular(app,1);Check(State(g)==state&&Get<float>(app,"clock")==clock,"Repeated actual OnGUI leaves all Core/save/queues and clock byte-for-field unchanged");Check(GUI.matrix.Equals(matrix)&&GUI.color.Equals(tint)&&!GUI.enabled,"OnGUI restores caller matrix/tint/enabled");}
        Event.current.type=EventType.Repaint;GUI.matrix=Matrix4x4.identity;GUI.color=Color.white;GUI.enabled=true;g.ClearBufferedCombatInputs();Key(app,KeyCode.I);float remaining=g.comboTimer;Frames(app,20);Hidden(app,"Inventory pauses and hides chain feedback");Check(g.comboTimer==remaining,"Inventory spends no continuation time");Key(app,KeyCode.Escape);Draw(app);AssertRegular(app,1);
        Call(app,"OnApplicationPause",true);remaining=g.comboTimer;Frames(app,10);Hidden(app,"Application pause hides chain feedback");Check(g.comboTimer==remaining,"Application pause freezes real chain time");Call(app,"OnApplicationPause",false);app.VisualReviewPaused=true;Frames(app,10);Hidden(app,"Paused review hides chain feedback");Check(g.comboTimer==remaining,"Review pause freezes real chain");app.VisualReviewPaused=false;
        g.hitStop=.1f;remaining=g.comboTimer;Frames(app,3);Draw(app);AssertRegular(app,1);Check(g.comboTimer==remaining,"Hit stop leaves actual continuation clock frozen");g.hitStop=0;
        g.save.coins=73;g.save.inventory[0]=11;g.PrepareSave();string raw=JsonUtility.ToJson(g.save);Check(!raw.Contains("comboTimer")&&!raw.Contains("SwordChain")&&!raw.Contains("SwordFinisher")&&!raw.Contains("playerAction"),"No transient action or new presentation field enters save schema");
        Check(SaveStore.Write(g.save,0),"Actual checksum writer succeeds");string warning;SaveData saved=SaveStore.Read(0,out warning);Check(saved!=null&&warning==null,"Actual checksum read succeeds");g.Load(saved);g.Start();Hidden(app,"Loaded checksum save does not restore an unowned transient chain");Check(g.save.coins==73&&g.save.inventory[0]==11,"Checksum load keeps economy/materials");
        foreach(int version in new[]{1,2}){SaveData old=JsonUtility.FromJson<SaveData>(raw);old.version=version;File.WriteAllText(SaveStore.PathFor(1),JsonUtility.ToJson(old));SaveData loaded=SaveStore.Read(1,out warning);Check(loaded!=null&&warning==null,"Raw legacy v"+version+" load succeeds");g.Load(loaded);g.Start();Hidden(app,"Legacy saves do not invent a sword chain");Check(g.save.coins==73&&g.save.inventory[0]==11,"Legacy load preserves original balances");Start(app);Draw(app,"legacy-v"+version);AssertRegular(app,1);}
        Prepare(app);g=app.Game;Start(app);Draw(app);AssertRegular(app,1);g.invulnerable=0;Call(g,"Hurt",10000f,"environment",null);Check(g.Dying,"Actual lethal damage starts death");Hidden(app,"Death hides chain feedback");
        Prepare(app);Start(app);Draw(app);AssertRegular(app,1);app.Game.StartPractice();Hidden(app,"Real training hides world chain feedback");
        foreach(float invalid in new[]{0,-1,float.NaN,float.PositiveInfinity,float.NegativeInfinity}){Prepare(app);Start(app);Draw(app);AssertRegular(app,1);Ready(app);app.Game.comboTimer=invalid;Hidden(app,"Expired/nonfinite continuation cannot invent a live row");}
        Console.WriteLine("PASS exact read-only Layout/Repaint with queued skill/all Core state and caller GUI restoration, actual inventory/application/review/hit-stop pause, checksum/raw legacy v1/v2 save continuity without schema expansion, death/training and invalid timer boundaries; NATIVE_UNVERIFIED");
    }
    private static void LayoutThreatsAndPointer(FrontierRuntime app)
    {
        Prepare(app);Start(app);Draw(app);AssertRegular(app,1); // baseline RED is missing visible behavior before new private reflection.
        foreach(bool english in new[]{false,true})foreach(bool large in new[]{false,true})foreach(bool compact in new[]{false,true})foreach(int phase in new[]{1,2,3,4})
        {
            Prepare(app);var g=app.Game;g.save.english=english;g.save.largeText=large;Screen.width=large?960:1920;Screen.height=large?600:1080;Call(app,"Resize");
            ActorAction a=null;if(phase>=3)a=Third(app);else{Start(app);if(phase==2)Start(app);}if(phase==4){while(!a.released)Update(app);}
            g.save.foodUntil=g.save.totalTime+90;g.save.teaUntil=g.save.totalTime+180;g.guardTime=g.riposteTime=1.234f;g.save.mealKind=2;g.save.mealUntil=g.save.totalTime+100;g.RestartTutorial();Call(g,"RecordCombatLoot",7,5,1,3,0,false);g.Notify("原有提示保留","Existing notification retained");Set(app,"combatHudTime",compact?3f:0f);
            Draw(app,"layout-"+(english?"en":"zh")+(large?"-large960":"-normal1920")+(compact?"-compact":"-normal")+"-phase"+phase);if(phase<3)AssertRegular(app,phase);else AssertFinisher(app,phase==4);
            Rect bounds=Bounds(app);Check(Same(bounds,new Rect(compact?20:24,207,compact?260:298,20)),"Both HUDs use the narrow original free gap above enemy hint");
            Check(((List<Rect>)Call(app,"HudRegions")).Exists(r=>Same(r,bounds)),"Live sword row participates in shared threat geometry");
            string text=phase<3?Regular(g,phase):Finisher(g,phase==4);string command=GUI.commands.Find(c=>c.Contains("\"text\":"+GUI.S(text)));Check(command!=null,"Actual new label exists");Label label=JsonUtility.FromJson<Label>(command);
            Check(Same(new Rect(label.r[0],label.r[1],label.r[2],label.r[3]),new Rect(bounds.x+6,bounds.y,bounds.width-12,18))&&label.size==(large?12:11),"New label keeps declared exact typography and large-text scaling");
            Check(Has(g.RoadMealStatus)&&HasPart(g.L("防护 ","GUARD "))&&HasPart(g.L("战利品","LOOT"))&&HasPart(g.L("炖汤强化 ","STEW BOOST "))&&HasPart(g.L("凉茶保护 ","TEA WARD "))&&Has(g.L("原有提示保留","Existing notification retained")),"Original meal/guard/loot/provisions/toast remain visible beside sword feedback");
            foreach(Rect mature in new[]{new Rect(24,228,298,90),new Rect(24,145,298,60),new Rect(20,135,260,62),(Rect)Call(app,"RoadMealHudBounds",compact),(Rect)Call(app,"GuardStatusBounds"),(Rect)Call(app,"CombatLootBounds"),(Rect)Call(app,"ProvisionStatusBounds"),new Rect(24,474,298,204),new Rect(380,22,520,65)})Check(!Overlap(bounds,mature),"Sword row cannot cover mature hint/climate/toast/meal/guard/reward/provision/tutorial/boss lanes");
        }
        foreach(bool compact in new[]{false,true})
        {
            Prepare(app);Start(app);Ready(app);Set(app,"combatHudTime",compact?3f:0f);Draw(app);AssertRegular(app,1);Rect bounds=Bounds(app);Point pointer=new Point(bounds.x+30,bounds.y+9);Check(!(bool)Call(app,"PointerOverInterface",pointer),"Read-only sword row passes world mouse input through");Point direction=(pointer-(Point)Call(app,"ScreenPoint",app.Game.Position)).Normalized;Pointer(app,pointer);Input.buttons.Add(0);Input.buttonDown.Add(0);Update(app);Input.buttons.Clear();Check(app.Game.playerAction!=null&&app.Game.playerAction.direction.x*direction.x+app.Game.playerAction.direction.y*direction.y>.99f,"Real mouse on sword row starts and aims the original next attack");
            Prepare(app);Start(app);Ready(app);var g=app.Game;Set(app,"combatHudTime",compact?3f:0f);Rect slot=compact?new Rect(515,732,78,50):new Rect(470,699,109,75);Pointer(app,new Point(slot.x+10,slot.y+10));Check((bool)Call(app,"PointerOverInterface",new Point(slot.x+10,slot.y+10)),"Original item action remains pointer protected");Input.buttons.Add(0);Input.buttonDown.Add(0);Update(app);Input.buttons.Clear();Check(g.playerAction==null,"Original action area cannot leak a new sword start");
            // Display-only camera/threat fixture: observes shared warning redraw and opacity.
            Prepare(app);Start(app);g=app.Game;var enemy=new Enemy(59910,0,24,14,80){state=1,timer=.3f,windupDuration=.65f,target=new Point(24,14)};enemy.Face(new Point(0,-1));g.enemies.Add(enemy);Set(app,"combatHudTime",compact?3f:0f);bounds=Bounds(app);Point pixel=new Point(bounds.x+bounds.width/2,bounds.y+10);Set(app,"camera",new Point(enemy.x-(pixel.x-640)/36f,enemy.y-(pixel.y-390)/36f));Draw(app,"threat-"+compact);bounds=Bounds(app);Check((bool)Call(app,"ThreatOver",bounds),"Actual threat envelope overlaps sword row");Check(GUI.textureCalls.Exists(c=>c.texture==Texture2D.whiteTexture&&Same(c.rect,bounds)&&Near(c.color.a,.12f)),"Sword background fades to .12 for actual threat overlap");GUI.commands.Clear();GUI.textureCalls.Clear();Call(app,"DrawHudThreatWitness");Check(GUI.textureCalls.Exists(c=>c.texture.name.Contains("outline_Monsters")),"Shared witness redraw preserves the overlapping actual enemy");
        }
        foreach(int kind in new[]{0,1,2,3,4})
        {
            Prepare(app);Start(app);var g=app.Game;int id=kind==3?GameCore.CommissionGuardianId:kind==4?GameCore.SignalWardenId:59920+kind;var enemy=new Enemy(id,kind>=3?3:kind,g.Position.x+2,g.Position.y,260,kind>=3){state=1,timer=.3f,windupDuration=.65f,target=g.Position,cycle=1};enemy.Face(new Point(-1,0));g.enemies.Add(enemy);Draw(app,"mature-tell-"+kind);AssertRegular(app,1);string expected=kind==3?(string)Call(app,"CommissionMoveHint",enemy):kind==4?(string)Call(app,"SignalMoveHint",enemy):g.EnemyHint(enemy);string command=GUI.commands.Find(c=>c.Contains("\"text\":"+GUI.S(expected)));Check(command!=null,"Original enemy hint text remains");Label label=JsonUtility.FromJson<Label>(command);Check(Same(new Rect(label.r[0],label.r[1],label.r[2],label.r[3]),new Rect(40,241,266,68))&&GUI.textureCalls.Exists(c=>c.texture==Texture2D.whiteTexture&&Same(c.rect,new Rect(24,228,298,90))),"All original hunter/archer/heavy/guardian/warden hints retain exact complete bounds");
        }
        foreach(bool reduced in new[]{false,true})foreach(int quality in new[]{0,1,2}){Prepare(app);Start(app);app.Game.save.reducedMotion=reduced;app.Game.save.effectsQuality=quality;Draw(app,"policy-"+reduced+"-"+quality);AssertRegular(app,1);}
        Prepare(app);Start(app);var game=app.Game;CultureInfo culture=CultureInfo.CurrentCulture;try{CultureInfo.CurrentCulture=new CultureInfo("fr-FR");game.comboTimer=1.01f;Draw(app,"comma-locale");Check(Has("CHAIN 1/3 · next 2 · 1.1s"),"Foreign locale retains invariant ceiling-tenth text");}finally{CultureInfo.CurrentCulture=culture;}
        Console.WriteLine("PASS zh/en normal1920/large960 four phases/both HUDs, exact typography/shared bounds, original climate/tea/provisions/meal/guard/reward/toast/tutorial/boss lanes and all five mature enemy hints, real mouse click-through/protected controls, actual threat fade/redraw, locale and effects/motion policies; NATIVE_UNVERIFIED");
    }
    public static int Main(string[] args)
    {
        string previous=Application.persistentDataPath,directory=Path.Combine(Path.GetTempPath(),"starfall-sword-chain-"+Guid.NewGuid());Application.persistentDataPath=directory;captureFolder=args.Length>0&&args[0]!="-"?args[0]:null;if(args.Length>1)capturePrefix=args[1];if(captureFolder!=null)Directory.CreateDirectory(captureFolder);var app=new FrontierRuntime();int failed=0;
        try{Call(app,"Awake");Check(app.Game!=null,"Actual Awake loads shipped campaign");foreach(Action<FrontierRuntime> test in new Action<FrontierRuntime>[]{RealInputSequenceAndWindow,CommittedThirdAndCapturedOwnership,ReadOnlyPauseAndLegacySave,LayoutThreatsAndPointer})try{test(app);}catch(Exception error){failed++;Console.WriteLine("FAIL "+test.Method.Name+": "+(error.InnerException??error).Message);}}
        finally{app.EndVisualReview();Call(app,"OnDestroy");Application.persistentDataPath=previous;Input.Clear();GUI.queuedClick=null;if(Directory.Exists(directory))Directory.Delete(directory,true);}
        Console.WriteLine("Runtime sword chain: "+(4-failed)+"/4 groups passed; NATIVE_UNVERIFIED");return failed==0?0:1;
    }
}
