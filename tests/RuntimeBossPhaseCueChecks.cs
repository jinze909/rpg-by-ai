// Real Update/OnGUI/input/save integration against offline Unity declarations.
// Encounter/HP staging and display-only camera fixtures are labelled below.
// This does not establish native Unity rendering, fonts or player acceptance.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using Starfall;
using UnityEngine;

public static class RuntimeBossPhaseCueChecks
{
    private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private const BindingFlags StaticPrivate = BindingFlags.Static | BindingFlags.NonPublic;
    private static string folder, prefix;
    private static int captures;
    private sealed class Label { public string text = null; public float[] r = null; public int size = 0; public bool center = false; }
    private static void Check(bool value, string why) { if (!value) throw new Exception(why); }
    private static bool Near(float a, float b, float epsilon = .003f) { return Math.Abs(a - b) <= epsilon; }
    private static bool Same(Rect a, Rect b) { return Near(a.x,b.x) && Near(a.y,b.y) && Near(a.width,b.width) && Near(a.height,b.height); }
    private static bool Overlap(Rect a, Rect b) { return a.x < b.x+b.width && b.x < a.x+a.width && a.y < b.y+b.height && b.y < a.y+a.height; }
    private static object Call(object target, string name, params object[] args)
    { MethodInfo method=target.GetType().GetMethod(name,Instance); Check(method!=null,"Production entry point exists: "+name); return method.Invoke(target,args); }
    private static void Set(object target, string name, object value) { target.GetType().GetField(name,Instance).SetValue(target,value); }
    private static T Get<T>(object target, string name) { return (T)target.GetType().GetField(name,Instance).GetValue(target); }
    private static T Field<T>(object target, string name) { return (T)target.GetType().GetField(name,Instance).GetValue(target); }
    private static bool Flag(object cue, string name) { return (bool)cue.GetType().GetProperty(name).GetValue(cue,null); }
    // Reflection keeps pre-feature production compilable for semantic RED.
    private static object Cue(FrontierRuntime app, Enemy boss) { return Call(app.Game,"GetBossPhaseCue",boss); }
    private static bool Visible(FrontierRuntime app)
    { var property=typeof(FrontierRuntime).GetProperty("BossPhaseCueVisible",Instance); Check(property!=null,"Boss cue visibility entry point exists"); return (bool)property.GetValue(app,null); }
    private static Rect Bounds(FrontierRuntime app) { return (Rect)Call(app,"BossPhaseCueBounds"); }
    private static void Update(FrontierRuntime app) { Call(app,"Update"); Input.keys.Clear(); }
    private static void Frames(FrontierRuntime app, int count) { for(int i=0;i<count;i++)Update(app); }
    private static void Key(FrontierRuntime app, KeyCode key) { Input.keys.Add(key); Update(app); }
    private static void Clear() { GUI.commands.Clear(); GUI.textureCalls.Clear(); GUI.buttonCalls.Clear(); }
    private static void Draw(FrontierRuntime app, string capture = null)
    {
        Clear(); Call(app,"OnGUI");
        if(folder!=null && capture!=null)
        {
            var commands=GUI.commands.ToArray(); var textures=GUI.textureCalls.ToArray(); var buttons=GUI.buttonCalls.ToArray();
            GUI.Export(folder,prefix+"-"+capture); captures++;
            GUI.commands.AddRange(commands); GUI.textureCalls.AddRange(textures); GUI.buttonCalls.AddRange(buttons);
        }
    }
    private static string Pending(GameCore g, int phase, int prepared)
    { return g.L("阶段 ","PHASE ")+phase+g.L(" · 当前预告仍为阶段 "," · current warning stays PHASE ")+prepared; }
    private static string Changed(GameCore g, int from, int phase)
    { return g.L("阶段 ","PHASE ")+from+" → "+phase+g.L(" · 注意后续攻击"," · watch the next attacks"); }
    private static bool Has(string text) { return GUI.commands.Exists(c=>c.Contains("\"text\":"+GUI.S(text))); }
    private static bool HasCue() { return GUI.commands.Exists(c=>c.Contains("\"k\":\"text\"") && (c.Contains("current warning stays PHASE") || c.Contains("watch the next attacks") || c.Contains("当前预告仍为阶段") || c.Contains("注意后续攻击"))); }
    private static Label RequireLabel(string text)
    {
        string command=GUI.commands.Find(c=>c.Contains("\"text\":"+GUI.S(text)));
        Check(command!=null,"Actual OnGUI must render the Boss phase chip: "+text);
        return JsonUtility.FromJson<Label>(command);
    }
    private static void RequireCounterCoexistence(FrontierRuntime app)
    {
        Check(Has(app.Game.L("闪避反击 1.2秒 · 下一次普通攻击 +50%","COUNTER 1.2s · next basic attack +50%")),"Actual mature counter text coexists with phase feedback");
        Check(!Overlap(Bounds(app),(Rect)Call(app,"FieldcraftCounterBounds")),"Phase chip and the active mature counter occupy separate rectangles");
    }
    private static void StageExistingBanners(FrontierRuntime app)
    { Set(app,"arrivalTime",2f);Set(app,"resumeTime",2f);Set(app,"resumeBrief",app.Game.L("原有返回提示","Existing return brief")); }
    private static void RequireBannerCoexistence(FrontierRuntime app)
    {
        foreach(Rect banner in new[]{new Rect(414,111,452,56),new Rect(352,205,576,98)})
        {
            Check(GUI.textureCalls.Exists(c=>c.texture==Texture2D.whiteTexture && Same(c.rect,banner)),"Actual mature arrival/resume panel is rendered beside the phase chip");
            Check(!Overlap(Bounds(app),banner),"Phase chip remains outside the actual arrival/resume panel bounds");
        }
        Check(Has(app.Game.L(app.Game.Map.name,app.Game.Map.en)) && Has(app.Game.L("欢迎回来 · 接下来","Welcome back · next step"))
            && Has(app.Game.L("原有返回提示","Existing return brief")),"Actual localized arrival and resume text coexist with phase feedback");
    }
    private static Enemy Prepare(FrontierRuntime app, int phase = 1, bool technique = false)
    {
        var method=typeof(RuntimeBossCommitmentChecks).GetMethod("Prepare",StaticPrivate);
        Enemy boss=(Enemy)method.Invoke(null,new object[]{app,technique?2:1,phase,2});
        var g=app.Game; g.save.english=true; g.save.largeText=false; g.save.showGuidance=false;
        g.save.effectsQuality=0; g.toastTime=0; g.save.health=g.save.maxHealth;
        GUI.color=Color.white; GUI.matrix=Matrix4x4.identity; GUI.enabled=true; Event.current.type=EventType.Repaint;
        return boss;
    }
    private static void Cross(FrontierRuntime app, Enemy boss, int phase, bool technique)
    {
        typeof(RuntimeBossCommitmentChecks).GetMethod("CrossByRealInput",StaticPrivate).Invoke(null,new object[]{app,boss,phase,technique});
        Draw(app); RequireLabel(Pending(app.Game,phase+1,phase));
        object cue=Cue(app,boss);
        Check(Flag(cue,"Changed") && Flag(cue,"Pending") && Field<int>(cue,"phase")==phase+1 && Field<int>(cue,"preparedPhase")==phase
            && Field<int>(cue,"fromPhase")==phase && Field<float>(cue,"remaining")>1.8f && Field<float>(cue,"remaining")<=2,
            "Real threshold contact owns a two-second receipt and independently truthful captured-warning phase");
    }
    private static void ActualPlayerAndAllyThresholdContacts(FrontierRuntime app)
    {
        foreach(int phase in new[]{1,2}) foreach(bool technique in new[]{false,true})
        {
            Enemy boss=Prepare(app,phase,technique); Cross(app,boss,phase,technique);
            Draw(app,"input-"+(technique?"q":"j")+"-"+phase+"-"+(phase+1)); RequireLabel(Pending(app.Game,phase+1,phase));
        }
        foreach(int phase in new[]{1,2})
        {
            Enemy boss=Prepare(app,phase); var g=app.Game; float hp=boss.hp;
            // Actual Sen command releases the existing persistent, ally-owned field.
            if(!g.Has("companion_2"))g.save.flags.Add("companion_2");
            g.save.companion=2; g.save.relationships[2]=0; g.companionCooldown=0;
            g.companionPosition=g.Position; g.companionAction=null; Set(g,"companionAttack",100f);
            Key(app,KeyCode.Z); ActorAction command=g.companionAction;
            Check(command!=null && !command.released && command.kind=="command","Actual Z prepares Sen without immediate HP loss");
            Check(boss.hp==hp,"Sen preparation cannot invent a phase transition");
            int frames=0; while(boss.hp==hp && frames++<90)Update(app);
            Check(frames<90 && command.released && boss.hp>0 && boss.hp<hp && boss.BossPhase==phase+1
                && command.impact!=null && !command.impact.playerOwned && g.playerAction==null,
                "A genuinely released non-player frost pulse crosses one living Boss threshold");
            Draw(app,"sen-frost-"+phase+"-"+(phase+1)); RequireLabel(Changed(g,phase,phase+1));
            object cue=Cue(app,boss); Check(Flag(cue,"Changed") && !Flag(cue,"Pending"),"Sen's original control cancels the old warning while its actual frost damage earns the change receipt");
        }
        Console.WriteLine("PASS actual J/Q and ally Sen-Z frost nonlethal contacts across both .66/.33 thresholds render authoritative pending/change text; NATIVE_UNVERIFIED");
    }
    private static void LockedAttackReleaseAndIndependentLifetime(FrontierRuntime app)
    {
        foreach(int phase in new[]{1,2})
        {
            Enemy boss=Prepare(app,phase); float duration=boss.windupDuration; Point target=boss.target; Cross(app,boss,phase,false); var g=app.Game;
            Check(Near(boss.windupDuration,duration) && (boss.target-target).Length<.001f && Near(GameCore.EnemyAreaRadius(boss),2.2f+phase*.2f),"Cue creation preserves the committed target, original duration and collision radius");
            float health=g.save.health; int cycle=boss.cycle, frames=0;
            while(boss.state==1 && frames++<150)Update(app);
            Check(frames<150 && boss.state==0 && boss.cycle==cycle+1 && boss.releaseTime>0,"Actual original prepared attack reaches one release and recovery");
            Check(Near(health-g.save.health,22+phase*3),"Accepted original slam contact keeps captured-phase damage");
            Effect danger=g.effects.Find(e=>e.kind=="danger"); Check(danger!=null && Near(danger.radius,2.2f+phase*.2f),"Released danger retains its prepared radius");
            int shots=0; foreach(Projectile shot in g.projectiles)if(!shot.friendly){shots++;Check(Near(shot.velocity.Length,3.5f+phase*.4f) && shot.damage==14,"Original radial speed and orb damage survive the cue");}
            Check(shots==8+phase*2,"Original radial spoke count survives the cue");
            Check(Near(boss.timer,2.2f-(phase+1)*.3f),"Recovery immediately uses the normal new HP phase");
            g.riposteTime=1.2f;StageExistingBanners(app);Draw(app,"release-"+phase);RequireLabel(Changed(g,phase,phase+1));RequireCounterCoexistence(app);RequireBannerCoexistence(app);
            object cue=Cue(app,boss); Check(Flag(cue,"Changed") && !Flag(cue,"Pending"),"Release retires pending immediately while the genuine short receipt remains");
            frames=0; while(boss.state!=1 && frames++<200)Update(app);
            Check(frames<200 && !g.Dying && boss.AttackPhase==phase+1 && Near(boss.windupDuration,1.1f-(phase+1)*.08f),"Next AI warning captures the new phase and its unmodified duration");
            Check(!Flag(Cue(app,boss),"Pending"),"The next prepared warning cannot retain the preceding phase mismatch");
        }
        Enemy held=Prepare(app); Cross(app,held,1,false); float locked=held.timer;
        // Labelled lifetime boundary fixture: existing stun holds the actual warning
        // long enough to distinguish its state-derived pending row from receipt TTL.
        held.stun=3; Frames(app,105); object expired=Cue(app,held);
        Check(held.state==1 && Near(held.timer,locked) && !Flag(expired,"Changed") && Flag(expired,"Pending") && Field<float>(expired,"remaining")==0,
            "Two seconds of simulation expires only the change receipt; a held old warning still reports pending");
        Draw(app,"pending-after-receipt-expiry"); RequireLabel(Pending(app.Game,2,1));
        held.stun=0; int limit=0; while(held.state==1 && limit++<120)Update(app);
        Draw(app,"expired-after-release"); Check(limit<120 && !HasCue() && !Visible(app) && !Flag(Cue(app,held),"Pending"),"Release removes the final pending row once its independent receipt already expired");
        Console.WriteLine("PASS original warning geometry/target/timing, accepted slam/radial release and next phase, independent two-second receipt expiry, and immediate pending retirement; NATIVE_UNVERIFIED");
    }
    private static void Append(StringBuilder text, object value, HashSet<object> seen)
    {
        if(value==null){text.Append("null;");return;} Type type=value.GetType();
        if(type.IsPrimitive || type.IsEnum || value is string || value is decimal){text.Append(Convert.ToString(value,CultureInfo.InvariantCulture)).Append(';');return;}
        if(!type.IsValueType && !seen.Add(value)){text.Append("ref;");return;} var sequence=value as IEnumerable;
        if(sequence!=null){foreach(object item in sequence)Append(text,item,seen);text.Append("end;");return;}
        var fields=type.GetFields(Instance); Array.Sort(fields,(a,b)=>string.CompareOrdinal(a.Name,b.Name));
        foreach(var field in fields){text.Append(field.Name).Append('=');Append(text,field.GetValue(value),seen);}
    }
    private static string State(GameCore g) { var text=new StringBuilder(); Append(text,g,new HashSet<object>()); return text.ToString(); }
    private static void Hidden(FrontierRuntime app, string why) { Draw(app); Check(!HasCue() && !Visible(app),why); }
    private static void PauseReadOnlySavesAndOwnership(FrontierRuntime app)
    {
        Enemy boss=Prepare(app); Cross(app,boss,1,false); var g=app.Game;
        Key(app,KeyCode.I); float remaining=Get<float>(g,"bossPhaseRemaining"), timer=boss.timer;
        Frames(app,12); Hidden(app,"Inventory hides phase feedback"); Key(app,KeyCode.Escape);
        Check(Near(Field<float>(Cue(app,boss),"remaining"),remaining) && Near(boss.timer,timer),"Inventory freezes the real receipt and warning clocks");
        Call(app,"OnApplicationPause",true); Frames(app,12); Hidden(app,"Application pause hides phase feedback"); Call(app,"OnApplicationPause",false);
        Check(Near(Field<float>(Cue(app,boss),"remaining"),remaining),"Application pause freezes the receipt");
        app.VisualReviewPaused=true; Frames(app,12); Hidden(app,"Review freeze hides phase feedback"); app.VisualReviewPaused=false;
        Check(Near(Field<float>(Cue(app,boss),"remaining"),remaining),"Review freeze retains simulation lifetime");
        g.hitStop=.10f; Frames(app,3); Draw(app); RequireLabel(Pending(g,2,1));
        Check(Near(Field<float>(Cue(app,boss),"remaining"),remaining),"Hit stop preserves lifetime while truthful feedback remains visible");
        string before=State(g); float clock=Get<float>(app,"clock");
        var matrix=Matrix4x4.TRS(new Vector3(7,11,0),Quaternion.Euler(0,0,9),new Vector3(1.2f,1.2f,1)); var tint=new Color(.2f,.3f,.4f,.5f);
        foreach(EventType type in new[]{EventType.Layout,EventType.Repaint})
        {
            Event.current.type=type; GUI.matrix=matrix; GUI.color=tint; GUI.enabled=false;
            for(int read=0;read<4;read++){Cue(app,boss);Draw(app);RequireLabel(Pending(g,2,1));}
            Check(State(g)==before && Get<float>(app,"clock")==clock,"Repeated query and Layout/Repaint mutate no Core/save/RNG/action or presentation clock");
            Check(GUI.matrix.Equals(matrix) && GUI.color.Equals(tint) && !GUI.enabled,"Actual OnGUI restores incoming matrix/color/enabled");
        }
        Event.current.type=EventType.Repaint; GUI.matrix=Matrix4x4.identity; GUI.color=Color.white; GUI.enabled=true;
        foreach(int version in new[]{1,2})
        {
            boss=Prepare(app);Cross(app,boss,1,false);g=app.Game;float hp=boss.hp;g.PrepareSave();g.save.version=version;string raw=JsonUtility.ToJson(g.save);
            Check(!raw.Contains("bossPhase") && !raw.Contains("BossPhaseCue") && !raw.Contains("preparedPhase") && !raw.Contains("attackPhase"),"Cue and captured attack add no save fields");
            File.WriteAllText(SaveStore.PathFor(1),raw);string issue;SaveData old=SaveStore.Read(1,out issue);
            Check(old!=null && issue==null,"Actual legacy raw save remains readable");Check(SaveStore.Write(old,2),"Actual checksum writer accepts unchanged schema");SaveData loaded=SaveStore.Read(2,out issue);
            Check(loaded!=null && issue==null,"Actual checksum reader restores legacy state");g.Load(loaded);g.Start();Enemy restored=g.enemies.Find(e=>e.boss);
            Check(restored!=null && Near(restored.hp,hp) && restored.BossPhase==2 && restored.state==0,"Load preserves post-hit HP and begins safe idle");
            Hidden(app,"Load cannot invent a historical phase receipt or warning");Check(!Flag(Cue(app,restored),"Changed") && !Flag(Cue(app,restored),"Pending"),"Fresh actor owns no old receipt");
        }
        boss=Prepare(app);Cross(app,boss,1,false);g=app.Game;g.enemies.Remove(boss);Draw(app);Check(!HasCue() && !Flag(Cue(app,boss),"Changed"),"Retired actor immediately loses visible/query ownership");Update(app);
        var replacement=new Enemy(boss.id,boss.kind,boss.x,boss.y,boss.maxHp,true){hp=boss.hp,timer=99};g.enemies.Add(replacement);Hidden(app,"Same-id replacement never inherits the previous actor receipt");
        Check(!Flag(Cue(app,replacement),"Changed"),"Receipt identity uses the actual actor reference");
        foreach(int id in new[]{GameCore.SignalWardenId,GameCore.CommissionGuardianId})
        {
            g.enemies.Clear();var special=new Enemy(id,3,boss.x,boss.y,300,true){hp=150,state=1,attackPhase=1,timer=.5f,windupDuration=1,target=g.Position};g.enemies.Add(special);
            Hidden(app,"Authored optional Bosses never acquire the canonical phase chip");Check(Field<int>(Cue(app,special),"phase")==0,"Optional Boss query is empty");
        }
        boss=Prepare(app);Cross(app,boss,1,false);g=app.Game;g.Enter(0);Hidden(app,"Travel clears prior phase feedback");Check(!Flag(Cue(app,boss),"Changed"),"Travel retires old receipt ownership");
        boss=Prepare(app);Cross(app,boss,1,false);g=app.Game;Call(g,"Hurt",10000f,"environment",null);Check(g.Dying,"Actual lethal damage enters death");Hidden(app,"Death hides phase feedback");
        Prepare(app);app.Game.StartPractice();Hidden(app,"Safe training has no world Boss phase chip");
        Console.WriteLine("PASS inventory/application/review/hit-stop clocks, full read-only query/GUI state, legacy raw/checksum loads, retirement/replacement, optional Bosses, travel/death/training; NATIVE_UNVERIFIED");
    }
    private static void Pointer(FrontierRuntime app, Point pixel)
    { float scale=Get<float>(app,"scale");Input.mousePosition=new Vector3(Get<float>(app,"offsetX")+pixel.x*scale,Screen.height-Get<float>(app,"offsetY")-pixel.y*scale,0); }
    private static void BilingualLayoutsTicksInputAndWitness(FrontierRuntime app)
    {
        // A living boss selects the mature combat HUD regardless of its linger
        // timer. Validate that reachable layout; do not claim exploration draws.
        bool combat=true;
        foreach(bool english in new[]{false,true}) foreach(bool large in new[]{false,true}) foreach(int width in new[]{960,1920})
        {
            Enemy boss=Prepare(app);Cross(app,boss,1,false);var g=app.Game;
            g.save.english=english;g.save.largeText=large;g.save.effectsQuality=0;g.save.reducedMotion=true;g.riposteTime=1.2f;
            Screen.width=width;Screen.height=width==1920?1080:600;Call(app,"Resize");Set(app,"combatHudTime",combat?3f:0f);StageExistingBanners(app);
            Draw(app,(combat?"combat":"exploration")+"-"+(english?"en":"zh")+(large?"-large":"-normal")+"-"+width);
            Label label=RequireLabel(Pending(g,2,1));RequireCounterCoexistence(app);RequireBannerCoexistence(app);Rect bounds=Bounds(app), expected=combat?new Rect(380,169,520,34):new Rect(383,573,514,34);
            Check(Same(bounds,expected) && label.center && label.size==(large?15:14),"Bilingual normal/large chip stays in its assigned readable row with actual larger-text sizing");
            Rect textRect=new Rect(label.r[0],label.r[1],label.r[2],label.r[3]);Check(textRect.x>=bounds.x && textRect.y>=bounds.y && textRect.x+textRect.width<=bounds.x+bounds.width && textRect.y+textRect.height<=bounds.y+bounds.height,"Localized text remains within the actual chip bounds");
            Check(((List<Rect>)Call(app,"HudRegions")).Exists(r=>Same(r,bounds)),"Actual chip is registered for shared foreground threat/actor witnesses");
            Check(!GUI.buttonCalls.Exists(c=>Overlap(c.rect,bounds)),"Phase feedback is noninteractive");
            Point pixel=new Point(bounds.x+bounds.width/2,bounds.y+bounds.height/2);
            Check(!(bool)Call(app,"PointerOverInterface",pixel),"Both HUD layouts pass world pointer input through the chip");
            Check(!Overlap(bounds,combat?new Rect(380,22,520,65):new Rect(383,612,514,64)) && !Overlap(bounds,(Rect)Call(app,"CombatFocusBounds")),"Chip remains separate from actual Boss HP and focus controls");
            float x=combat?399:403,y=combat?58:656,w=combat?482:474,h=combat?6:5;
            foreach(float threshold in new[]{.66f,.33f})
                Check(GUI.textureCalls.Exists(c=>c.texture==Texture2D.whiteTexture && c.rect.width<=3 && Math.Abs((c.rect.x+c.rect.width/2)-(x+w*threshold))<=2 && c.rect.y<=y && c.rect.y+c.rect.height>=y+h),"Actual existing Boss HP bar marks both real .66/.33 thresholds");
        }
        // The exploration bounds remain a safe fallback outside world mode;
        // an actual live canonical boss never renders this mature HUD branch.
        Enemy fallback=Prepare(app);Cross(app,fallback,1,false);app.Game.mode="inventory";
        Check(Same(Bounds(app),new Rect(383,573,514,34)),"Hidden exploration fallback retains its assigned bounds");Hidden(app,"Exploration fallback outside live world never renders stale phase feedback");app.Game.mode="world";
        {
            Enemy boss=Prepare(app);Cross(app,boss,1,false);var g=app.Game;g.playerAction=null;g.attackCooldown=0;Set(app,"combatHudTime",combat?3f:0f);Draw(app);Rect bounds=Bounds(app);
            Point pixel=new Point(bounds.x+bounds.width/2,bounds.y+bounds.height/2);Point playerPixel=(Point)Call(app,"ScreenPoint",g.Position);Point aim=(pixel-playerPixel).Normalized;
            Pointer(app,pixel);Input.buttons.Add(0);Update(app);Input.buttons.Clear();
            Check(g.playerAction!=null && g.playerAction.kind=="attack" && g.playerAction.direction.x*aim.x+g.playerAction.direction.y*aim.y>.99f,"Actual mouse hold over phase feedback starts and aims the ordinary world basic attack");
            boss=Prepare(app);Cross(app,boss,1,false);g=app.Game;g.playerAction=null;g.attackCooldown=0;Set(app,"combatHudTime",combat?3f:0f);Draw(app);
            Point slot=combat?new Point(440,740):new Point(364,710);Check((bool)Call(app,"PointerOverInterface",slot),"Mature Q slot retains pointer protection");Pointer(app,slot);Input.buttons.Add(0);Update(app);Input.buttons.Clear();Check(g.playerAction==null,"Protected Q slot never leaks an ordinary attack");
            // Display-only camera fixture puts the real guardian and its prepared
            // warning under the chip. No combat success is inferred from this draw.
            Set(app,"combatHudTime",combat?3f:0f);bounds=Bounds(app);Point target=new Point(bounds.x+bounds.width/2,bounds.y+28);
            Set(app,"camera",new Point(boss.x-(target.x-640)/36f,boss.y-(target.y-390)/36f));
            Draw(app,"overlap-"+(combat?"combat":"exploration"));RequireLabel(Pending(g,2,1));
            Check((bool)Call(app,"ThreatOver",bounds),"Prepared live guardian/warning overlaps the chip");
            Check(GUI.textureCalls.Exists(c=>c.texture==Texture2D.whiteTexture && Same(c.rect,bounds) && Near(c.color.a,.12f)),"Threat overlap uses the mature exactly .12 background fade");
            int fill=GUI.textureCalls.FindIndex(c=>c.texture==Texture2D.whiteTexture && Same(c.rect,bounds));
            Check(GUI.textureCalls.FindLastIndex(c=>c.texture.name.Contains("outline_Bosses"))>fill,"Actual guardian witness remains above the phase chip fill");
            Clear();Call(app,"DrawHudThreatWitness");
            Check(GUI.textureCalls.Exists(c=>c.texture.name.Contains("outline_Bosses")),"Registered chip participates in actual actor witness redraw");
            Check(GUI.textureCalls.FindAll(c=>c.texture==Texture2D.whiteTexture && Near(c.rect.width,4) && Near(c.rect.height,4)
                && Near(c.color.r,.87f) && Near(c.color.g,.54f) && Near(c.color.b,.41f) && Near(c.color.a,.8f)).Count==48,"All 48 prepared Boss warning points survive shared witness redraw");
        }
        Console.WriteLine("PASS eight actual bilingual normal/large 960/1920 low-effects/reduced-motion combat layouts, hidden exploration fallback, both real threshold ticks, chip click-through/protected slot and threat/actor/warning witnesses; captures="+captures+"; NATIVE_UNVERIFIED");
    }
    public static int Main(string[] args)
    {
        string previous=Application.persistentDataPath,temp=Path.Combine(Path.GetTempPath(),"starfall-boss-phase-"+Guid.NewGuid().ToString("N"));Application.persistentDataPath=temp;
        folder=args.Length>0 && args[0]!="-"?args[0]:null;prefix=args.Length>1?args[1]:"qa-boss-phase-cue";var app=new FrontierRuntime();int failed=0;
        try
        {
            Directory.CreateDirectory(temp);if(folder!=null)Directory.CreateDirectory(folder);Call(app,"Awake");
            foreach(Action<FrontierRuntime> test in new Action<FrontierRuntime>[]{ActualPlayerAndAllyThresholdContacts,LockedAttackReleaseAndIndependentLifetime,PauseReadOnlySavesAndOwnership,BilingualLayoutsTicksInputAndWitness})
                try{test(app);}catch(Exception error){failed++;Console.WriteLine("FAIL "+test.Method.Name+": "+(error.InnerException??error));}
        }
        finally{app.EndVisualReview();Call(app,"OnDestroy");Application.persistentDataPath=previous;Input.Clear();GUI.queuedClick=null;if(Directory.Exists(temp))Directory.Delete(temp,true);}
        Console.WriteLine("Runtime boss-phase-cue: "+(4-failed)+"/4 groups passed; captures="+captures+"; NATIVE_UNVERIFIED");return failed==0?0:1;
    }
}
