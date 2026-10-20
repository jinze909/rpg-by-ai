// Real Update/OnGUI/SaveStore paths with offline Unity declarations.
// Synthetic outgoing labels are identified; native Player validation is pending.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using Starfall;
using UnityEngine;

public static class RuntimeHealingFeedbackChecks
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private static string captureFolder;
    private static int captures;
    private static readonly Color Green=PixelArt.C("#a8e8bc"),Rose=PixelArt.C("#f4aaa5"),Gold=PixelArt.C("#ebc58b"),Ink=PixelArt.C("#101a2b");
    public sealed class Label { public string k,text; public float[] r,color,m; public int size; public bool center; }
    private static void Check(bool ok,string why) { if(!ok)throw new Exception(why); }
    private static bool Near(float a,float b,float tolerance=.003f) { return Math.Abs(a-b)<tolerance; }
    private static object Call(object obj,string name,params object[] args)
    {var method=obj.GetType().GetMethod(name,Private|BindingFlags.Public);Check(method!=null,"Production method exists: "+name);return method.Invoke(obj,args);}
    private static void Set(object obj,string name,object value) {obj.GetType().GetField(name,Private).SetValue(obj,value);}
    private static T Get<T>(object obj,string name) {return (T)obj.GetType().GetField(name,Private).GetValue(obj);}
    private static void Update(FrontierRuntime app) {Call(app,"Update");Input.keys.Clear();}
    private static void Frames(FrontierRuntime app,int n) {for(int i=0;i<n;i++)Update(app);}
    private static void Key(FrontierRuntime app,KeyCode key) {Input.keys.Add(key);Update(app);}
    private static void Clear() {GUI.commands.Clear();GUI.textureCalls.Clear();GUI.buttonCalls.Clear();}
    private static void Draw(FrontierRuntime app,string capture=null)
    {
        Clear();Call(app,"OnGUI");
        if(captureFolder!=null&&capture!=null)
        {var commands=GUI.commands.ToArray();var textures=GUI.textureCalls.ToArray();var buttons=GUI.buttonCalls.ToArray();GUI.Export(captureFolder,"qa-healing-feedback-"+capture);captures++;GUI.commands.AddRange(commands);GUI.textureCalls.AddRange(textures);GUI.buttonCalls.AddRange(buttons);}
    }
    private static void Prepare(FrontierRuntime app)
    {
        Input.Clear();GUI.queuedClick=null;GUI.matrix=Matrix4x4.identity;GUI.color=Color.white;GUI.enabled=true;Screen.width=1280;Screen.height=800;Time.unscaledDeltaTime=.02f;Event.current.type=EventType.Repaint;
        app.BeginVisualReview(1,0);app.VisualReviewPaused=false;var g=app.Game;g.enemies.Clear();g.hazards.Clear();g.projectiles.Clear();g.effects.Clear();g.fallen.Clear();g.save.companion=-1;
        g.save.english=true;g.save.largeText=false;g.save.showGuidance=false;g.save.effectsQuality=0;g.save.reducedMotion=true;g.save.difficulty=1;g.save.talents=new int[3];g.save.relics=new[]{-1,-1};g.save.mealUntil=0;g.save.mealKind=0;g.save.totalTime=100;g.save.health=g.save.maxHealth;g.save.energy=100;g.save.inventory[6]=4;g.invulnerable=100;g.toastTime=0;g.companionAction=null;Set(g,"companionAttack",100f);g.companionCooldown=0;
        Check(g.CanStand(new Point(15,14))&&app.SetVisualReviewPosition(new Point(15,14)),"Actual town collision accepts clear test feet");
        Set(app,"camera",g.Position);Set(app,"clock",0f);Set(app,"combatHudTime",3f);Set(app,"applicationPaused",false);Set(app,"controllerUsed",false);Set(app,"pointerSeen",false);Set(app,"pointerAimActive",false);Set(app,"resumeTime",0f);Set(app,"arrivalTime",0f);Call(app,"Resize");
    }
    private static List<Effect> Gains(GameCore g) {return g.effects.FindAll(e=>e.kind=="healthGain"&&e.life>0);}
    private static string Expected(float amount) {return amount>0&&amount<.01f?"+<0.01":"+"+amount.ToString("0.##",CultureInfo.InvariantCulture);}
    private static Effect One(GameCore g,float before,bool fresh=true)
    {
        var found=Gains(g);float gain=g.save.health-before;Check(gain>0&&found.Count==1,"Actual positive HP gain emits exactly one healthGain label: actual="+found.Count+", gain="+gain);
        var effect=found[0];Check(effect.text==Expected(gain),"Exact clamped gain in invariant 0.## form: expected="+Expected(gain)+", actual="+effect.text);
        Check((effect.position-g.Position).Length<.001f&&Near(effect.maxLife,.7f)&&(!fresh||Near(effect.life,.7f)),"Fresh healing anchors to actual player feet with the existing .7s numeric lifetime");return effect;
    }
    private static void NoGain(GameCore g,string why) {Check(Gains(g).Count==0,"No fake HP amount at "+why);}
    private static void Stew(FrontierRuntime app,int input)
    {
        if(input<2)Key(app,input==0?KeyCode.F:KeyCode.JoystickButton4);
        else {Draw(app);Rect slot=new Rect(515,732,78,50);Check(GUI.buttonCalls.Exists(b=>b.enabled&&Same(b.rect,slot)),"Actual combat HUD stew button is enabled");GUI.queuedClick=slot;Draw(app);Check(!GUI.queuedClick.HasValue,"Actual stew control consumes click");}
    }
    private static void StewInputsAndActualAmounts(FrontierRuntime app)
    {
        for(int input=0;input<3;input++)foreach(float missing in new[]{80f,2.125f,0f})
        {
            Prepare(app);var g=app.Game;g.save.health=g.save.maxHealth-missing;g.save.energy=19;float before=g.save.health;int meals=g.save.inventory[6];Stew(app,input);
            Check(Near(g.save.health-before,Math.Min(55,missing))&&g.save.inventory[6]==meals-1&&g.save.energy==100&&Near(g.save.foodUntil-g.save.totalTime,90),"Real F/LB/HUD retains stew's one item, clamped 55 HP, full energy and 90s attack buff");
            if(missing>0)One(g,before);else NoGain(g,"full-health stew still grants mature energy/buff");
            Check(g.toast.Contains(Expected(Math.Min(55,missing))),"Stew toast states actual gained HP instead of promising nominal 55 at cap; input="+input+", missing="+missing+", actual="+g.toast);
        }
        CultureInfo previous=CultureInfo.CurrentCulture;
        try {CultureInfo.CurrentCulture=new CultureInfo("fr-FR");foreach(float missing in new[]{.125f,.001f}){Prepare(app);var g=app.Game;g.save.health-=missing;float before=g.save.health;Stew(app,0);var effect=One(g,before);Check(!effect.text.Contains(",")&&effect.text!="+0","Fractional/tiny positive heal stays visible under comma locale");}}
        finally {CultureInfo.CurrentCulture=previous;}
        foreach(string reason in new[]{"empty","dead","practice"})
        {Prepare(app);var g=app.Game;g.save.health-=30;if(reason=="empty")g.save.inventory[6]=0;else if(reason=="dead"){g.invulnerable=0;Call(g,"Hurt",999f,"environment",null);}else g.StartPractice();float hp=g.save.health;int items=g.save.inventory[6];Stew(app,0);Check(g.save.health==hp&&g.save.inventory[6]==items,"Rejected stew changes no health/inventory at "+reason);NoGain(g,reason);}
        Console.WriteLine("PASS actual F/LB/HUD stew: clamped 55 HP, full energy/90s buff, full-HP no label, actual toast, invariant fractional/tiny amounts and rejected items; NATIVE_UNVERIFIED");
    }
    private static void Mira(FrontierRuntime app)
    {var g=app.Game;g.save.companion=0;g.save.relationships[0]=3;g.save.flags.Add("companion_0");g.companionPosition=g.Position+new Point(-1,0);Set(g,"companionAttack",100f);g.companionCooldown=0;}
    private static void ContactsAndBufferedRelics(FrontierRuntime app)
    {
        foreach(bool racedFull in new[]{false,true})
        {
            Prepare(app);Mira(app);var g=app.Game;g.save.health=g.save.maxHealth*.5f;Set(g,"companionAttack",0f);float before=g.save.health;Update(app);
            Check(g.companionAction!=null&&g.companionAction.kind=="heal"&&Near(g.companionAction.contact,.16f),"Actual hurt-health auto AI prepares original .16s cast");Frames(app,7);Check(g.save.health==before&&!g.companionAction.released,"Automatic heal has no .14s early HP gain");NoGain(g,"auto preparation");if(racedFull)g.save.health=g.save.maxHealth;
            before=g.save.health;Frames(app,1);if(racedFull)NoGain(g,"automatic heal raced to full before contact");else {Check(Near(g.save.health-before,9),"Mira auto retains 6+bond HP at actual contact");One(g,before);}
            Check(!g.effects.Exists(e=>e.kind=="number"&&e.text=="+HP"),"Automatic cast no longer emits unquantified gold +HP");Frames(app,8);Check(Gains(g).Count==(racedFull?0:1),"Released auto action cannot echo its label on later frames");
        }
        foreach(bool racedFull in new[]{false,true})
        {
            Prepare(app);Mira(app);var g=app.Game;g.save.health-=3;g.save.energy=50;float before=g.save.health;Key(app,KeyCode.Z);Check(g.companionAction!=null&&Near(g.companionAction.contact,.2f),"Actual Z command retains .2s preparation");Frames(app,9);Check(g.save.health==before&&!g.companionAction.released,"Mira command has no early healing before .2s");NoGain(g,"command preparation");if(racedFull)g.save.health=g.save.maxHealth;before=g.save.health;Frames(app,1);
            Check(g.guardTime>2.9f&&g.save.energy>65&&Near(g.companionCooldown,17.8f),"Mature command energy, 3s guard and 18s cooldown unchanged");int ring=g.effects.FindIndex(e=>e.kind=="heal");Check(ring>=0,"Original .6s heal ring retained");
            if(racedFull)NoGain(g,"full health command contact still grants ring/guard/energy");else {var effect=One(g,before);Check(Near(g.save.health-before,3)&&g.effects.IndexOf(effect)>ring,"Clamped +3 label follows original ring to preserve variant ordering");}
            Frames(app,8);Check(Gains(g).Count==(racedFull?0:1),"Command contact emits its label once");
        }
        foreach(bool full in new[]{false,true})
        {
            Prepare(app);var g=app.Game;g.save.relics[0]=4;if(!full)g.save.health-=2.25f;Input.held.Add(KeyCode.J);Update(app);Input.held.Clear();Frames(app,9);ActorAction attack=g.playerAction;float before=g.save.health;Key(app,KeyCode.Q);
            Check(g.playerAction==attack&&g.skillCooldown<=0&&g.save.health==before,"Actual Q queued in attack preparation neither heals nor pays early");NoGain(g,"queued relic Q");
            int frames=0;while(g.playerAction==attack&&frames++<40)Update(app);Check(g.playerAction!=null&&g.playerAction.kind=="skill"&&g.skillCooldown>0&&g.save.energy<51,"Buffered Q eventually genuinely starts with original relic cost 50");
            if(full)NoGain(g,"full HP relic Q acceptance");else {Check(Near(g.save.health-before,2.25f),"Relic Q clamps original eight HP to real missing amount");One(g,before);}
            Frames(app,10);Check(Gains(g).Count==(full?0:1),"Later technique contact cannot duplicate acceptance heal");
            Prepare(app);g=app.Game;g.save.relics[0]=0;if(!full)g.save.health-=1.375f;before=g.save.health;g.dashCooldown=.10f;Input.axes["Horizontal"]=1;Key(app,KeyCode.Space);Input.axes["Horizontal"]=0;
            Check(g.dashTime<=0&&g.save.health==before,"Real buffered dodge does not heal early");NoGain(g,"queued relic dodge");frames=0;while(g.dashTime<=0&&frames++<8)Update(app);Check(g.dashTime>0,"Captured dodge genuinely starts after readiness");if(full)NoGain(g,"full HP started relic dodge");else {Check(Near(g.save.health-before,1.375f),"Original three HP dodge healing clamps to missing amount");One(g,before);}
            Frames(app,8);Check(Gains(g).Count==(full?0:1),"Dodge's later motion cannot echo acceptance heal");
        }
        Prepare(app);var rejected=app.Game;rejected.save.relics[0]=4;rejected.save.health-=30;rejected.save.energy=1;float rejectedHp=rejected.save.health;Key(app,KeyCode.Q);Check(rejected.save.health==rejectedHp,"Rejected unaffordable Q cannot heal");NoGain(rejected,"unaffordable Q");
        Console.WriteLine("PASS real .16s Mira auto/.20s Z command contacts, race-to-full/no early/no repeated gain, retained ring/guard/energy; actual queued Q and dodge heal only on acceptance/start, full HP and rejected costs; NATIVE_UNVERIFIED");
    }
    private static bool Same(Rect a,Rect b) {return Near(a.x,b.x)&&Near(a.y,b.y)&&Near(a.width,b.width)&&Near(a.height,b.height);}
    private static bool Same(Color a,Color b) {return Near(a.r,b.r)&&Near(a.g,b.g)&&Near(a.b,b.b)&&Near(a.a,b.a);}
    private static bool Overlap(Rect a,Rect b) {return a.x<b.x+b.width&&b.x<a.x+a.width&&a.y<b.y+b.height&&b.y<a.y+a.height;}
    private static bool IsText(Effect e) {return e.life>0&&(e.kind=="number"||e.kind=="break"||e.kind=="damageTaken"||e.kind=="healthGain");}
    private static List<Label> Labels(FrontierRuntime app,List<Effect> effects,bool separated=true)
    {
        var labels=new List<Label>();foreach(string command in GUI.commands){if(!command.StartsWith("{\"k\":\"text\"",StringComparison.Ordinal))continue;var label=JsonUtility.FromJson<Label>(command);if(label.r!=null&&label.r.Length==4&&Near(label.r[2],120)&&Near(label.r[3],32)&&label.size==(app.Game.save.largeText?20:19)&&effects.Exists(e=>e.text==label.text))labels.Add(label);}
        Check(labels.Count==effects.Count*2,"Each requested combat label has exact foreground/shadow pair: actual="+labels.Count+", expected="+effects.Count*2);var result=new List<Label>();
        for(int i=0;i<effects.Count;i++){var shadow=labels[i*2];var front=labels[i*2+1];Check(front.text==effects[i].text&&shadow.text==front.text&&front.center&&shadow.center,"Effect order and centered whole label preserved");Color ink=Ink,color=effects[i].kind=="healthGain"?Green:effects[i].kind=="damageTaken"?Rose:Gold;if(effects[i].kind=="healthGain"||effects[i].kind=="damageTaken"){float alpha=Math.Min(1,effects[i].life/.2f);ink.a*=alpha;color.a*=alpha;}Check(Same(new Color(front.color[0],front.color[1],front.color[2],front.color[3]),color)&&Same(new Color(shadow.color[0],shadow.color[1],shadow.color[2],shadow.color[3]),ink),"Actual green gain, rose loss, unchanged gold outgoing and last .2s foreground/shadow alpha");Check(Near(shadow.r[0],front.r[0]+1)&&Near(shadow.r[1],front.r[1]+1),"Original one-pixel ink shadow retained");result.Add(front);}
        if(separated)for(int a=0;a<result.Count;a++)for(int b=a+1;b<result.Count;b++){var x=result[a].r;var y=result[b].r;Check(!Overlap(new Rect(x[0],x[1],x[2]+1,x[3]+1),new Rect(y[0],y[1],y[2]+1,y[3]+1)),"Green/rose/gold mixed labels use nonoverlapping lifetime lanes");}return result;
    }
    private static Point Offset(FrontierRuntime app,Effect e,Label label)
    {Point p=(Point)Call(app,"ScreenPoint",e.position);return new Point(label.r[0]-(p.x-60),label.r[1]-(p.y-55-(1-e.life/e.maxLife)*22));}
    private static void MixedRenderingFadeAndHud(FrontierRuntime app)
    {
        foreach(bool english in new[]{false,true})foreach(bool large in new[]{false,true})foreach(int width in new[]{960,1920})
        {
            Prepare(app);var g=app.Game;g.save.english=english;g.save.largeText=large;Screen.width=width;Screen.height=width==960?640:1080;Call(app,"Resize");g.save.health-=30;float before=g.save.health;Stew(app,0);var actual=One(g,before);g.invulnerable=0;Call(g,"Hurt",12.5f,"environment",null);
            // Explicit outgoing/break fixture supplements genuine stew and Hurt.
            foreach(string text in new[]{"! 42","GUARDED","BREAK"})g.effects.Add(new Effect(text=="BREAK"?"break":"number",g.Position,.7f,text));var effects=g.effects.FindAll(IsText);
            Draw(app,(english?"en":"zh")+(large?"-large":"-normal")+"-"+width);var labels=Labels(app,effects);Point p=(Point)Call(app,"ScreenPoint",actual.position);Check(Near(labels[0].r[0],p.x-60)&&Near(labels[0].r[1],p.y-55),"Standalone first gain retains mature exact source anchor");var offsets=new List<Point>();for(int i=0;i<labels.Count;i++)offsets.Add(Offset(app,effects[i],labels[i]));
            string saved=JsonUtility.ToJson(g.save);float age=actual.life,clock=Get<float>(app,"clock");var art=Get<PixelArt>(app,"art");int owned=Get<List<Texture2D>>(art,"owned").Count,cache=Get<Dictionary<string,Texture2D>>(art,"cache").Count;for(int n=0;n<12;n++)Draw(app);Check(saved==JsonUtility.ToJson(g.save)&&actual.life==age&&clock==Get<float>(app,"clock")&&owned==Get<List<Texture2D>>(art,"owned").Count&&cache==Get<Dictionary<string,Texture2D>>(art,"cache").Count,"Repeated OnGUI does not mutate simulation/save/age or allocate texture/cache entries");
            Frames(app,12);Draw(app);labels=Labels(app,effects);for(int i=0;i<labels.Count;i++){Point offset=Offset(app,effects[i],labels[i]);Check(Near(offset.x,offsets[i].x)&&Near(offset.y,offsets[i].y),"Source-relative lanes remain stable through genuine recoil/camera movement and original 22px rise");}
        }
        Prepare(app);var underHud=app.Game;underHud.save.health-=12.5f;float underHp=underHud.save.health;Stew(app,0);var protectedGain=One(underHud,underHp);Point underSource=(Point)Call(app,"ScreenPoint",protectedGain.position),underEast=(Point)Call(app,"ScreenPoint",protectedGain.position+new Point(1,0));Set(app,"camera",Get<Point>(app,"camera")+(underSource-new Point(150,100))*(1/(underEast.x-underSource.x)));Draw(app);var protectedLabel=Labels(app,new List<Effect>{protectedGain})[0];Rect protectedRect=new Rect(protectedLabel.r[0],protectedLabel.r[1],121,33);Check(!((List<Rect>)Call(app,"HudRegions")).Exists(r=>Overlap(r,protectedRect)),"A newly born green label initially under resource HUD is packed into a visible safe lane");
        Prepare(app);var world=app.Game;world.save.health-=12.5f;float prior=world.save.health;Stew(app,0);var gain=One(world,prior);Draw(app);Point source=(Point)Call(app,"ScreenPoint",gain.position),east=(Point)Call(app,"ScreenPoint",gain.position+new Point(1,0));Set(app,"camera",Get<Point>(app,"camera")+(source-new Point(150,100))*(1/(east.x-source.x)));Draw(app,"hud-edge");Clear();Call(app,"DrawHudThreatWitness");Labels(app,new List<Effect>{gain});
        Prepare(app);world=app.Game;world.save.health-=12.5f;prior=world.save.health;Stew(app,0);gain=One(world,prior);Frames(app,30);Check(gain.life>0&&gain.life<.12f,"Real clock reaches healing fade interval before removal");Draw(app,"fade");var faded=Labels(app,new List<Effect>{gain});Check(faded[0].color[3]>0&&faded[0].color[3]<.6f,"Real green numeric label fades before expiration");
        Check(captureFolder==null||captures==10,"Exactly ten actual bilingual/large mixed, HUD and fade captures");Console.WriteLine("PASS real OnGUI green/rose/gold colors, whole-label shadow, mixed nonoverlap, stable camera lanes, read-only repeated draws, last .2s fade and actual post-HUD witness; captures="+captures+"; NATIVE_UNVERIFIED");
    }
    private static void LifecycleAndSaveBoundaries(FrontierRuntime app)
    {
        foreach(bool reduced in new[]{false,true})foreach(int quality in new[]{0,1,2})
        {
            Prepare(app);var g=app.Game;g.save.reducedMotion=reduced;g.save.effectsQuality=quality;g.save.health-=7;float before=g.save.health;Stew(app,0);var gain=One(g,before);Draw(app);Labels(app,new List<Effect>{gain});Key(app,KeyCode.Escape);Check(g.mode!="world","Real Escape pauses healing feedback simulation");float life=gain.life;Frames(app,12);Check(gain.life==life,"Pause freezes healing age");g.Close();Call(app,"OnApplicationPause",true);Frames(app,12);Check(gain.life==life,"Application pause freezes healing age");Call(app,"OnApplicationPause",false);Frames(app,50);Draw(app);Check(!g.effects.Contains(gain)&&Gains(g).Count==0,"Every FX/reduced-motion policy removes expired gains");Check(((IList)Get<object>(app,"combatTextPlacements")).Count==0,"Expired healing placements release cached references");
        }
        foreach(string boundary in new[]{"travel","load","review"})
        {
            Prepare(app);var g=app.Game;g.save.flags.Add("healing_story_sentinel");g.save.inventory[3]=7;g.save.coins=321;g.save.health-=7;float before=g.save.health;Stew(app,0);var gain=One(g,before);Draw(app);
            if(boundary=="travel")g.Enter(1);else if(boundary=="load"){g.PrepareSave();string json=JsonUtility.ToJson(g.save);Check(!json.Contains("healthGain"),"Healing labels add no persistent SaveData field");SaveData old=JsonUtility.FromJson<SaveData>(json);old.version=1;Check(SaveStore.Write(old,2),"Real checksummed SaveStore writes v1-compatible campaign");string warning;var restored=SaveStore.Read(2,out warning);Check(restored!=null&&warning==null,"Real checksum reader restores legacy slot");g.Load(restored);g.Close();}else {app.EndVisualReview();app.BeginVisualReview(1,0);app.VisualReviewPaused=false;}
            Check(!app.Game.effects.Contains(gain)&&Gains(app.Game).Count==0,"Actual "+boundary+" clears previous-world healing text");Draw(app);Check(!GUI.commands.Exists(c=>c.Contains("\"text\":\"+7\"")),"No stale label survives "+boundary);Check(((IList)Get<object>(app,"combatTextPlacements")).Count==0,"World transition releases healing placements");if(boundary!="review")Check(g.Has("healing_story_sentinel")&&g.save.inventory[3]==7&&g.save.coins==321,"Mature story/inventory/currency survive travel and checksummed legacy load");
        }
        Console.WriteLine("PASS all six FX/motion policies, real menu/application pause, .7s expiration/cache cleanup, travel/v1 checksummed load/review boundaries preserve story/inventory/currency and save schema; NATIVE_UNVERIFIED");
    }
    public static int Main(string[] args)
    {
        string previous=Application.persistentDataPath,directory=Path.Combine(Path.GetTempPath(),"starfall-healing-feedback-"+Guid.NewGuid());Application.persistentDataPath=directory;captureFolder=args.Length>0&&args[0]!="-"?args[0]:null;int failures=0;var app=new FrontierRuntime();
        try {Directory.CreateDirectory(directory);Call(app,"Awake");Check(app.Game!=null,"Actual Awake loads shipped campaign");foreach(Action<FrontierRuntime> test in new Action<FrontierRuntime>[]{StewInputsAndActualAmounts,ContactsAndBufferedRelics,MixedRenderingFadeAndHud,LifecycleAndSaveBoundaries})try{test(app);}catch(Exception ex){failures++;Console.WriteLine("FAIL "+test.Method.Name+": "+(ex.InnerException??ex));}app.EndVisualReview();Call(app,"OnDestroy");}
        finally {Application.persistentDataPath=previous;Input.Clear();GUI.queuedClick=null;GUI.matrix=Matrix4x4.identity;GUI.color=Color.white;GUI.enabled=true;Event.current.type=EventType.Repaint;if(Directory.Exists(directory))Directory.Delete(directory,true);}
        Console.WriteLine("Runtime healing feedback: "+(4-failures)+"/4 groups passed; NATIVE_UNVERIFIED");return failures==0?0:1;
    }
}
