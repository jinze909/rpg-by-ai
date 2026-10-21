// Real production Update, OnGUI, skill/command release and SaveStore paths.
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

public static class RuntimeGuardStatusChecks
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static string captureFolder;
    private static int captures;
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
    { var property=typeof(FrontierRuntime).GetProperty("GuardStatusVisible",Private); Check(property!=null,"Live Guard presentation exists"); return (bool)property.GetValue(app,null); }
    private static Rect Bounds(FrontierRuntime app) { return (Rect)Call(app,"GuardStatusBounds"); }
    private static void Update(FrontierRuntime app) { Call(app,"Update"); Input.keys.Clear(); }
    private static void Frames(FrontierRuntime app, int count) { for(int i=0;i<count;i++)Update(app); }
    private static void Key(FrontierRuntime app, KeyCode key) { Input.keys.Add(key); Update(app); }
    private static void Draw(FrontierRuntime app, string capture = null)
    {
        GUI.commands.Clear(); GUI.textureCalls.Clear(); GUI.buttonCalls.Clear(); Call(app,"OnGUI");
        if(captureFolder!=null && capture!=null)
        {
            var commands=GUI.commands.ToArray(); var textures=GUI.textureCalls.ToArray(); var buttons=GUI.buttonCalls.ToArray();
            GUI.Export(captureFolder,"qa-guard-status-"+capture); captures++;
            GUI.commands.AddRange(commands); GUI.textureCalls.AddRange(textures); GUI.buttonCalls.AddRange(buttons);
        }
    }
    private static bool Has(string text) { return GUI.commands.Exists(c=>c.Contains("\"text\":"+GUI.S(text))); }
    private static bool HasPart(string text) { return GUI.commands.Exists(c=>c.Contains("\"k\":\"text\"") && c.Contains(text)); }
    private static bool HasGuard(FrontierRuntime app) { return HasPart(app.Game.L("防护 ","GUARD ")); }
    private static string Expected(GameCore g)
    { return g.L("防护 ","GUARD ")+(Math.Ceiling(g.guardTime*10)/10).ToString("0.0",CultureInfo.InvariantCulture)+g.L("秒 · 受到伤害 ×","s · incoming damage ×")+GameCore.GuardIncomingMultiplier.ToString("0.##",CultureInfo.InvariantCulture); }
    private static Label GuardLabel(FrontierRuntime app)
    {
        string text=Expected(app.Game); string command=GUI.commands.Find(c=>c.Contains("\"text\":"+GUI.S(text)));
        Check(command!=null,"Actual OnGUI reports the authoritative Guard clock and multiplier: "+text); return JsonUtility.FromJson<Label>(command);
    }
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
        g.save.technique=0; g.guardTime=0; g.invulnerable=0; g.toastTime=0;
        Set(app,"applicationPaused",false); Set(app,"pointerSeen",false); Set(app,"pointerAimActive",false); Set(app,"controllerUsed",false);
        Set(app,"combatHudTime",0f); Set(app,"resumeTime",0f); Set(app,"arrivalTime",0f); Set(app,"camera",g.Position); Call(app,"Resize"); Pointer(app,new Point(900,390));
    }
    private static ActorAction ReleaseSkill(FrontierRuntime app, int technique)
    {
        var g=app.Game; g.save.technique=technique; if(technique==1 && !g.Has("technique_0"))g.save.flags.Add("technique_0");
        Key(app,KeyCode.Q); ActorAction cast=g.playerAction;
        Check(cast!=null && cast.kind=="skill" && !cast.released && g.guardTime<=0,"Real Q preparation grants no early protection");
        Check(Near(g.save.energy,60) && Near(g.skillCooldown,3.5f),"Accepted Q keeps original 40 energy and 3.5-second cooldown");
        Draw(app); Check(!Visible(app) && !HasGuard(app),"Preparing Q cannot advertise unreleased Guard");
        int frame=0; while(!cast.released && frame++<40){Check(g.guardTime<=0,"Protection remains absent before real contact"); Update(app);}
        Check(cast.released && Near(g.guardTime,technique==0?3:4),"Only the real Q release grants its mature three/four seconds");
        Draw(app); GuardLabel(app); return cast;
    }
    private static void AttachMira(GameCore g)
    { if(!g.Has("companion_0"))g.save.flags.Add("companion_0"); g.save.companion=0; g.companionCooldown=0; g.companionPosition=g.Position+new Point(-1,0); }
    private static void ReleaseMira(FrontierRuntime app)
    {
        Key(app,KeyCode.Z); var action=app.Game.companionAction;
        Check(action!=null && action.kind=="command" && !action.released,"Real Z starts Mira's original preparation");
        int frame=0; while(!action.released && frame++<30)Update(app);
        Check(action.released && app.Game.LastCompanionReceipt!=null && app.Game.LastCompanionReceipt.id==0,"Actual Mira release produces the existing recovery receipt");
    }
    private static void ActualReleasesCountdownAndSharedProtection(FrontierRuntime app)
    {
        Prepare(app); ReleaseSkill(app,0); Draw(app,"q-three-seconds"); float start=app.Game.guardTime;
        Frames(app,25); Check(Near(app.Game.guardTime,start-.5f),"Guard follows exactly 25 real world ticks"); Draw(app); GuardLabel(app);
        int frame=0; while(app.Game.guardTime>0 && frame++<180)Update(app); Draw(app,"expired");
        Check(app.Game.guardTime<=0 && !Visible(app) && !HasGuard(app),"Natural expiry removes the card without lingering protection");
        Prepare(app); ReleaseSkill(app,1); AttachMira(app.Game); float before=app.Game.guardTime; ReleaseMira(app);
        Check(app.Game.guardTime>3 && app.Game.guardTime<before && app.Game.companionCooldown>17.7f,"Mira preserves naturally remaining longer alternate-Q protection without renewing it");
        Draw(app,"q-four-plus-mira"); GuardLabel(app);
        Prepare(app,1); AttachMira(app.Game); Key(app,KeyCode.Z); Check(app.Game.guardTime<=0,"Mira preparation alone grants no Guard");
        var command=app.Game.companionAction; int ticks=0; while(!command.released && ticks++<30)Update(app);
        Check(command.released && app.Game.guardTime>2.9f && app.Game.guardTime<=3,"Real Mira release grants Guard to a Ranger as well");
        Draw(app,"ranger-mira"); GuardLabel(app);
        Console.WriteLine("PASS actual Vanguard Q 3s/4s release, original cost/cooldown, natural countdown/expiry and real Mira Z preserving longer shared protection or granting Ranger protection; NATIVE_UNVERIFIED");
    }
    private static void Append(StringBuilder text, object value, HashSet<object> seen)
    {
        if(value==null){text.Append("null;");return;} Type type=value.GetType();
        if(type.IsPrimitive || type.IsEnum || value is string || value is decimal){text.Append(Convert.ToString(value,CultureInfo.InvariantCulture)).Append(';');return;}
        if(!type.IsValueType && !seen.Add(value)){text.Append("ref;");return;} var sequence=value as IEnumerable;
        if(sequence!=null){foreach(object item in sequence)Append(text,item,seen);text.Append("end;");return;}
        var fields=type.GetFields(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic); Array.Sort(fields,(a,b)=>string.CompareOrdinal(a.Name,b.Name));
        foreach(var field in fields){text.Append(field.Name).Append('=');Append(text,field.GetValue(value),seen);}
    }
    private static string State(GameCore g) { var text=new StringBuilder(); Append(text,g,new HashSet<object>()); return text.ToString(); }
    private static void Hidden(FrontierRuntime app, string reason) { Draw(app); Check(!Visible(app) && !HasGuard(app),reason); }
    private static void PauseBoundariesReadOnlyAndLegacySave(FrontierRuntime app)
    {
        Prepare(app); ReleaseSkill(app,0); var g=app.Game; Draw(app);
        string state=State(g); float clock=Get<float>(app,"clock"); var matrix=Matrix4x4.TRS(new Vector3(13,21,0),Quaternion.Euler(0,0,11),new Vector3(1.25f,1.25f,1)); var tint=new Color(.3f,.4f,.5f,.6f);
        foreach(EventType evt in new[]{EventType.Layout,EventType.Repaint})
        {
            Event.current.type=evt; GUI.matrix=matrix; GUI.color=tint; GUI.enabled=false; Draw(app); GuardLabel(app);
            Check(State(g)==state && Get<float>(app,"clock")==clock,"Repeated Layout/Repaint preserves all Core/save fields and presentation clock");
            Check(GUI.matrix.Equals(matrix) && GUI.color.Equals(tint) && !GUI.enabled,"Actual OnGUI restores incoming matrix, tint and enabled state");
        }
        Event.current.type=EventType.Repaint; GUI.matrix=Matrix4x4.identity; GUI.color=Color.white; GUI.enabled=true;
        Key(app,KeyCode.I); float remaining=g.guardTime; Frames(app,20); Hidden(app,"Inventory hides live Guard while the mature timer pauses"); Check(g.guardTime==remaining,"Inventory never advances protection"); Key(app,KeyCode.Escape);
        Call(app,"OnApplicationPause",true); remaining=g.guardTime; Frames(app,10); Hidden(app,"Application pause hides the live card"); Check(g.guardTime==remaining,"Application pause freezes Guard"); Call(app,"OnApplicationPause",false);
        app.VisualReviewPaused=true; Frames(app,10); Hidden(app,"Paused review hides the live card"); Check(g.guardTime==remaining,"Paused review freezes Guard"); app.VisualReviewPaused=false;
        g.hitStop=.1f; remaining=g.guardTime; Frames(app,3); Draw(app); GuardLabel(app); Check(g.guardTime==remaining,"Hit stop preserves the real Guard clock while its truthful card remains visible");
        foreach(float invalid in new[]{0,-1,float.NaN,float.PositiveInfinity,float.NegativeInfinity}){g.guardTime=invalid;Hidden(app,"Zero, negative and nonfinite timers never advertise active protection");}
        g.guardTime=1.234f; CultureInfo previous=CultureInfo.CurrentCulture;
        try{CultureInfo.CurrentCulture=new CultureInfo("fr-FR");Draw(app);Check(Has("GUARD 1.3s · incoming damage ×0.45"),"Foreign locale retains invariant visible timer and multiplier");}finally{CultureInfo.CurrentCulture=previous;}
        g.PrepareSave(); Check(SaveStore.Write(g.save,0),"Actual checksum writer accepts the unchanged save schema"); string warning; SaveData saved=SaveStore.Read(0,out warning);
        Check(saved!=null && warning==null,"Actual checksum reader restores the unchanged save"); string raw=JsonUtility.ToJson(saved);
        Check(!raw.Contains("guardTime") && !raw.Contains("GuardStatus"),"Live protection and presentation do not enter SaveData");
        foreach(int version in new[]{1,2})
        {
            SaveData old=JsonUtility.FromJson<SaveData>(raw); old.version=version; File.WriteAllText(SaveStore.PathFor(1),JsonUtility.ToJson(old));
            SaveData loaded=SaveStore.Read(1,out warning); Check(loaded!=null && warning==null,"Actual raw legacy v"+version+" reader succeeds"); g.Load(loaded); g.Start();
            Check(g.guardTime==0 && g.save.coins==old.coins && g.save.profession==old.profession,"Legacy load clears transient Guard and preserves mature economy/class"); Hidden(app,"Legacy saves invent no active protection");
        }
        Prepare(app); ReleaseSkill(app,0); g=app.Game; g.Enter(1); Check(g.save.room==1 && g.guardTime==0,"Actual travel uses the mature transient reset"); Hidden(app,"Travel does not retain a stale Guard display");
        Prepare(app); ReleaseSkill(app,0); g=app.Game; Call(g,"Hurt",10000f,"environment",null); Check(g.Dying,"Real lethal incoming damage starts death"); Hidden(app,"Death suppresses remaining protection");
        Prepare(app); app.Game.StartPractice(); app.Game.guardTime=2; Check(app.Game.practicing,"Actual original practice starts"); Hidden(app,"Safe training never advertises world Guard");
        Console.WriteLine("PASS full Core/save/GUI read-only draws, inventory/application/review pause, hit stop, nonfinite timers, invariant locale, actual checksum/legacy loads, travel/death and training boundaries; NATIVE_UNVERIFIED");
    }
    private static void ClickThroughThreatFadeAndWitness(FrontierRuntime app)
    {
        foreach(bool combat in new[]{false,true})
        {
            Prepare(app); ReleaseSkill(app,0); Frames(app,30); var g=app.Game; Set(app,"combatHudTime",combat?3f:0f); Draw(app); Rect bounds=Bounds(app);
            Check(Same(bounds,new Rect(24,386,298,24)),"Actual Guard bounds use the side status area in both HUD layouts");
            Check(((List<Rect>)Call(app,"HudRegions")).Exists(r=>Same(r,bounds)),"Guard participates in actual shared HUD witness regions");
            Point pixel=new Point(bounds.x+30,bounds.y+12); Check(!(bool)Call(app,"PointerOverInterface",pixel),"Read-only Guard passes world mouse input through");
            Point expectedAim=(pixel-(Point)Call(app,"ScreenPoint",g.Position)).Normalized;
            Pointer(app,pixel); Input.buttons.Add(0); Update(app); Input.buttons.Clear();
            Check(g.playerAction!=null && g.playerAction.kind=="attack" && g.playerAction.direction.x*expectedAim.x+g.playerAction.direction.y*expectedAim.y>.99f,"Actual mouse hold over Guard launches and aims a real basic attack toward that pointer");
            Prepare(app); ReleaseSkill(app,0); Frames(app,30); g=app.Game; Set(app,"combatHudTime",combat?3f:0f); Draw(app);
            Point slot=combat?new Point(440,740):new Point(364,710); Check((bool)Call(app,"PointerOverInterface",slot),"Mature Q slot retains pointer protection");
            Pointer(app,slot); Input.buttons.Add(0); Update(app); Input.buttons.Clear(); Check(g.playerAction==null,"Holding the protected Q slot never leaks a basic attack");
            // Display-only distant enemy/camera fixture: no combat success is inferred.
            var enemy=new Enemy(59301,0,22,14,80){state=1,timer=.3f,windupDuration=.65f,target=new Point(22,14)}; enemy.Face(new Point(0,-1)); g.enemies.Add(enemy);
            Set(app,"combatHudTime",combat?3f:0f); bounds=Bounds(app); Point targetPixel=new Point(bounds.x+bounds.width/2,bounds.y+30);
            Set(app,"camera",new Point(enemy.x-(targetPixel.x-640)/36f,enemy.y-(targetPixel.y-390)/36f)); Draw(app,"threat-"+(combat?"combat":"exploration"));
            Check((bool)Call(app,"ThreatOver",bounds),"The real enemy/warning envelope overlaps Guard");
            Check(GUI.textureCalls.Exists(c=>c.texture==Texture2D.whiteTexture && Same(c.rect,bounds) && Near(c.color.a,.12f,.001f)),"Overlapping Guard fill uses exactly .12 opacity");
            int fill=GUI.textureCalls.FindIndex(c=>c.texture==Texture2D.whiteTexture && Same(c.rect,bounds));
            Check(GUI.textureCalls.FindLastIndex(c=>c.texture.name.Contains("outline_Monsters"))>fill,"Actual enemy outline remains visible after the Guard fill");
            GUI.commands.Clear(); GUI.textureCalls.Clear(); GUI.buttonCalls.Clear(); Call(app,"DrawHudThreatWitness");
            Check(GUI.textureCalls.Exists(c=>c.texture.name.Contains("outline_Monsters")),"Shared witness redraw includes the enemy under Guard");
            Check(GUI.textureCalls.FindAll(c=>c.rect.width==2).Count==33,"The complete mature thirty-three-point melee warning survives Guard overlap");
        }
        Console.WriteLine("PASS both HUD bounds/regions, actual click-through basic attack and aim, protected Q slot, exact .12 threat fade and post-HUD enemy/warning witness; NATIVE_UNVERIFIED");
    }
    private static void BilingualLayoutsAndMatureHudCoexistence(FrontierRuntime app)
    {
        foreach(bool english in new[]{false,true}) foreach(bool large in new[]{false,true}) foreach(bool combat in new[]{false,true})
        {
            Prepare(app,0,6); var g=app.Game; g.save.english=english; g.save.largeText=large; Screen.width=large?960:1280; Screen.height=large?600:800; Call(app,"Resize");
            // Display-only fractional timer/counter fixture covers exact text and coexistence.
            g.guardTime=1.234f; g.riposteTime=1.234f; g.save.mealKind=2; g.save.mealUntil=g.save.totalTime+100;
            g.Notify("原有提示保留","Existing notification retained"); Set(app,"combatHudTime",combat?3f:0f);
            Draw(app,"layout-"+(english?"en":"zh")+(large?"-large960":"-normal1280")+(combat?"-combat":"-exploration"));
            Label label=GuardLabel(app); Rect bounds=Bounds(app);
            Check(Same(new Rect(label.r[0],label.r[1],label.r[2],label.r[3]),new Rect(bounds.x+8,bounds.y,bounds.width-16,bounds.height)) && label.center && label.size==(large?13:12),"Localized Guard has one centered readable row with actual large-text sizing");
            Check(HasPart(g.L("闪避反击 1.3","COUNTER 1.3")) && Has(g.RoadMealStatus) && Has(g.L("原有提示保留","Existing notification retained")),"Mature counter, active road meal and original notification remain visible together");
            Check(!Overlap(bounds,(Rect)Call(app,"FieldcraftCounterBounds")) && !Overlap(bounds,(Rect)Call(app,"RoadMealHudBounds",combat)),"Guard does not overlap counter or road-meal text regions");
            Check(!Overlap(bounds,combat?new Rect(20,103,260,26):new Rect(24,145,298,60)),"Guard remains outside climate status");
            Check(GUI.textureCalls.Exists(c=>c.texture==Texture2D.whiteTexture && Same(c.rect,bounds) && Near(c.color.a,.92f)),"Unobstructed Guard retains its normal .92 fill");
            Rect toastBounds=combat?new Rect(20,135,260,62):new Rect(382,132,516,67);
            var toastFill=GUI.textureCalls.Find(c=>c.texture==Texture2D.whiteTexture && Same(c.rect,toastBounds));
            Check(toastFill!=null,"The actual active toast panel is drawn at its mature HUD position");
            Check(!Overlap(bounds,toastFill.rect),"Guard must remain outside the actual toast fill, which is painted after the HUD");
            if(!combat)
            {
                Check(Same(bounds,new Rect(24,386,298,24)),"Guard stays in the side status area outside the whole exploration toast panel");
                Check(((List<Rect>)Call(app,"HudRegions")).Exists(r=>Same(r,bounds)) && !(bool)Call(app,"PointerOverInterface",new Point(bounds.x+10,bounds.y+12)),"The Guard row retains its live witness bounds and click-through input beside a toast");
                // Display-only imminent-toast fixture; expiry still uses real Update.
                g.toastTime=.04f; Frames(app,3); Check(g.toastTime<=0 && g.guardTime>0,"Real Update expires the toast while Guard remains active");
                Draw(app,"toast-expired-"+(english?"en":"zh")+(large?"-large960":"-normal1280")); GuardLabel(app); Rect restored=Bounds(app);
                Check(Same(restored,new Rect(24,386,298,24)) && Same(restored,bounds),"Guard retains its stable side status row after toast expiry");
                var regions=(List<Rect>)Call(app,"HudRegions");
                Check(regions.Exists(r=>Same(r,restored)),"HUD witnesses retain the actual stable Guard rectangle after toast expiry");
                Check(!GUI.textureCalls.Exists(c=>c.texture==Texture2D.whiteTexture && Same(c.rect,toastBounds)),"Expired toast has no remaining opaque panel over the restored Guard row");
            }
        }
        // Separate display-only fixtures retain the actual mature hint geometry.
        foreach(int overlay in new[]{0,1,2,3})
        {
            Prepare(app,0,overlay==0?6:overlay==1?5:1); var g=app.Game; g.guardTime=2; Rect matureBounds;
            if(overlay==0)
            {
                foreach(string flag in new[]{"desert_report","personal_1","companion_1","commission_accepted"})if(!g.Has(flag))g.save.flags.Add(flag);
                g.enemies.Add(new Enemy(GameCore.CommissionGuardianId,3,29,13,260,true){timer=99});
                Check(g.CommissionAccepted,"Commission hint fixture retains the mature acceptance prerequisites"); matureBounds=new Rect(383,113,514,65);
            }
            else if(overlay==1){g.racing=true;g.raceCheckpoint=0;g.raceTime=5;matureBounds=new Rect(430,112,420,48);}
            else if(overlay==2){Set(app,"arrivalTime",2f);matureBounds=new Rect(414,111,452,56);}
            else{Set(app,"resumeTime",2f);Set(app,"resumeBrief","Existing return brief");matureBounds=new Rect(352,205,576,98);}
            Draw(app,"central-hint-"+overlay); GuardLabel(app);
            var actual=GUI.textureCalls.Find(c=>c.texture==Texture2D.whiteTexture && Same(c.rect,matureBounds));
            Check(actual!=null,"The actual mature Commission, race, arrival or welcome panel remains present");
            Check(!Overlap(Bounds(app),actual.rect),"Guard stays separate from actual Commission, race, arrival and welcome panels");
        }
        Prepare(app,0,1); ReleaseSkill(app,1); var game=app.Game; AttachMira(game); ReleaseMira(app); game.riposteTime=1.2f;
        var boss=new Enemy(59302,0,game.Position.x+4,game.Position.y,250,true){timer=99}; game.enemies.Add(boss); Draw(app,"boss-counter-focus-receipt"); Rect guard=Bounds(app);
        Check(HasGuard(app) && HasPart("COUNTER") && HasPart("Mira") && game.LastCompanionReceipt!=null,"Guard coexists with live counter and actual Mira recovery receipt");
        Check((bool)typeof(FrontierRuntime).GetProperty("CombatFocusVisible",Private).GetValue(app,null),"Mature focus control remains available beside a real living target");
        Check(!Overlap(guard,new Rect(380,22,520,65)) && !Overlap(guard,(Rect)Call(app,"CombatFocusBounds")) && !Overlap(guard,(Rect)Call(app,"CompanionFeedbackBounds")),"Guard remains separate from Boss health, focus and companion receipt");
        Console.WriteLine("PASS zh/en normal1280/large960 in both HUDs, exact fractional text and normal fill, actual toast-fill separation and stable row through expiry, retained counter/climate/meal plus Boss health/focus/actual Mira receipt coexistence; captures="+captures+"; NATIVE_UNVERIFIED");
    }
    public static int Main(string[] args)
    {
        string previous=Application.persistentDataPath, directory=Path.Combine(Path.GetTempPath(),"starfall-guard-status-"+Guid.NewGuid()); Application.persistentDataPath=directory;
        captureFolder=args.Length>0 && args[0]!="-"?args[0]:null; if(captureFolder!=null)Directory.CreateDirectory(captureFolder); var app=new FrontierRuntime(); int failed=0;
        try
        {
            Call(app,"Awake"); Check(app.Game!=null,"Actual Awake loads the shipped campaign");
            foreach(Action<FrontierRuntime> test in new Action<FrontierRuntime>[]{ActualReleasesCountdownAndSharedProtection,PauseBoundariesReadOnlyAndLegacySave,ClickThroughThreatFadeAndWitness,BilingualLayoutsAndMatureHudCoexistence})
                try{test(app);}catch(Exception error){failed++;Console.WriteLine("FAIL "+test.Method.Name+": "+(error.InnerException??error).Message);}
        }
        finally{app.EndVisualReview();Call(app,"OnDestroy");Application.persistentDataPath=previous;Input.Clear();GUI.queuedClick=null;if(Directory.Exists(directory))Directory.Delete(directory,true);}
        Console.WriteLine("Runtime Guard status: "+(4-failed)+"/4 groups passed; captures="+captures+"; NATIVE_UNVERIFIED"); return failed==0?0:1;
    }
}
