// Production Hurt / hostile flight / Update / OnGUI, using offline Unity declarations.
// Synthetic effect labels are explicitly identified; native rendering and feel remain unverified.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using Starfall;
using UnityEngine;

public static class RuntimeDamageTakenChecks
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static string captureFolder;
    private static readonly Color Rose=PixelArt.C("#f4aaa5"), Gold=PixelArt.C("#ebc58b"), Ink=PixelArt.C("#101a2b");
    public sealed class Label { public string k,text; public float[] r,color,m; public int size; public bool center; }
    private static void Check(bool ok,string message) { if(!ok)throw new Exception(message); }
    private static bool Near(float a,float b,float tolerance=.002f) { return Math.Abs(a-b)<=tolerance; }
    private static bool Same(Color a,Color b) { return Near(a.r,b.r)&&Near(a.g,b.g)&&Near(a.b,b.b)&&Near(a.a,b.a); }
    private static object Call(object obj,string name,params object[] args)
    { var method=obj.GetType().GetMethod(name,Private|BindingFlags.Public);Check(method!=null,"Existing production method: "+name);return method.Invoke(obj,args); }
    private static void Set(object obj,string name,object value) { obj.GetType().GetField(name,Private).SetValue(obj,value); }
    private static T Get<T>(object obj,string name) { return (T)obj.GetType().GetField(name,Private).GetValue(obj); }
    private static void Update(FrontierRuntime app) { Call(app,"Update");Input.keys.Clear(); }
    private static void Frames(FrontierRuntime app,int count) { for(int i=0;i<count;i++)Update(app); }
    private static void Clear() { GUI.commands.Clear();GUI.textureCalls.Clear();GUI.buttonCalls.Clear(); }
    private static void Draw(FrontierRuntime app,string capture=null)
    {
        Clear();Call(app,"OnGUI");
        if(captureFolder!=null&&capture!=null)
        { var c=GUI.commands.ToArray();var t=GUI.textureCalls.ToArray();var b=GUI.buttonCalls.ToArray();GUI.Export(captureFolder,"qa-damage-taken-"+capture);GUI.commands.AddRange(c);GUI.textureCalls.AddRange(t);GUI.buttonCalls.AddRange(b); }
    }
    private static void Prepare(FrontierRuntime app)
    {
        Input.Clear();GUI.queuedClick=null;GUI.matrix=Matrix4x4.identity;GUI.color=Color.white;GUI.enabled=true;Screen.width=1280;Screen.height=800;Time.unscaledDeltaTime=.02f;Event.current.type=EventType.Repaint;
        app.BeginVisualReview(1,0);app.VisualReviewPaused=false;var g=app.Game;g.enemies.Clear();g.hazards.Clear();g.projectiles.Clear();g.effects.Clear();g.fallen.Clear();g.save.companion=-1;
        g.save.english=true;g.save.largeText=false;g.save.showGuidance=false;g.save.effectsQuality=0;g.save.reducedMotion=true;g.save.difficulty=1;g.save.talents=new int[3];g.save.relics=new[]{-1,-1};g.save.mealUntil=0;g.save.mealKind=0;g.save.totalTime=100;g.save.health=g.save.maxHealth;g.invulnerable=0;g.toastTime=0;
        Check(g.CanStand(new Point(15,14))&&app.SetVisualReviewPosition(new Point(15,14)),"Actual town collision accepts clear test feet");
        Set(app,"camera",g.Position);Set(app,"clock",0f);Set(app,"combatHudTime",3f);Set(app,"applicationPaused",false);Set(app,"controllerUsed",false);Set(app,"pointerSeen",false);Set(app,"pointerAimActive",false);Set(app,"resumeTime",0f);Set(app,"arrivalTime",0f);Call(app,"Resize");
    }
    private static List<Effect> Damage(GameCore game) { return game.effects.FindAll(e=>e.kind=="damageTaken"&&e.life>0); }
    private static bool TextEffect(Effect e) { return e.life>0&&(e.kind=="number"||e.kind=="break"||e.kind=="damageTaken"); }
    private static string Text(float loss) { return loss<.01f?"-<0.01":"-"+loss.ToString("0.##",CultureInfo.InvariantCulture); }
    private static Effect One(GameCore game,float before)
    {
        var found=Damage(game);float loss=before-Math.Max(0,game.save.health);Check(loss>0&&found.Count==1,"One real positive HP loss must emit exactly one incoming damage label (actual="+found.Count+")");var effect=found[0];
        Check(effect.text==Text(loss),"Text reports actual clamped HP delta in invariant decimal form: expected="+Text(loss)+", actual="+effect.text);
        Check((effect.position-game.Position).Length<.001f&&Near(effect.life,.7f)&&Near(effect.maxLife,.7f),"Incoming label has actual player feet and original .7 second numeric lifetime");return effect;
    }
    private static void Hurt(GameCore g,float amount,string source="environment") { Call(g,"Hurt",amount,source,(Point?)new Point(-1,0)); }
    private static Projectile Hostile(FrontierRuntime app,float damage)
    { var g=app.Game;var p=new Projectile{position=g.Position,velocity=new Point(.01f,0),damage=damage,source="shot",visual="enemyArrow",life=1};g.projectiles.Add(p);return p; }
    private static void MitigatedContactsUseActualDelta(FrontierRuntime app)
    {
        Prepare(app);var g=app.Game;float before=g.save.health;var shot=Hostile(app,17);Update(app);Check(!g.projectiles.Contains(shot)&&Near(before-g.save.health,17),"Actual hostile projectile collision retains original 17 HP damage");One(g,before);Check(g.effects.Exists(e=>e.kind=="hurt"),"Existing hurt ring is preserved alongside new text");Draw(app,"actual-hostile-arrow");
        for(int difficulty=0;difficulty<3;difficulty++)foreach(bool guarded in new[]{false,true})
        {
            Prepare(app);g=app.Game;g.save.difficulty=difficulty;g.save.talents[1]=4;g.guardTime=guarded?2:0;g.save.relics[0]=5;g.save.mealKind=2;g.save.mealUntil=g.save.totalTime+60;
            before=g.save.health;float expected=19.75f*(difficulty==0?.45f:difficulty==2?1.4f:1)*(guarded?.45f:1)*(1-4*.035f)*1.15f*.9f;
            Hostile(app,19.75f);Update(app);Check(Near(before-g.save.health,expected),"Shipped difficulty, guard, armor talent, risk relic and broth formula is unchanged");One(g,before);
        }
        Prepare(app);g=app.Game;g.save.room=8;g.save.abyssFloor=3;g.save.boons[2]=3;before=g.save.health;Hurt(g,17,"area");Check(Near(before-g.save.health,17*(1-3*.12f)),"Abyss guard boon keeps its original incoming multiplier");One(g,before);
        CultureInfo previous=CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture=new CultureInfo("fr-FR");
            foreach(float amount in new[]{8.5f,.01f,.001f}){Prepare(app);g=app.Game;before=g.save.health;Hurt(g,amount);var effect=One(g,before);Check(!effect.text.Contains(",")&&effect.text!="-0","French locale cannot produce comma damage or hide tiny positive health loss as zero");}
        }
        finally { CultureInfo.CurrentCulture=previous; }
        Console.WriteLine("PASS actual hostile arrow contact, all difficulty/guard/talent/relic/broth reductions, abyss boon and invariant fractional/tiny actual HP loss; NATIVE_UNVERIFIED");
    }
    private static void LethalAndProtectedContacts(FrontierRuntime app)
    {
        Prepare(app);var g=app.Game;g.save.health=3.375f;float before=g.save.health;int deaths=g.save.deaths;Hostile(app,999);Update(app);
        Check(g.Dying&&g.save.health==0&&g.save.deaths==deaths+1,"Lethal real projectile keeps mature zero-HP death transaction");var lethal=One(g,before);Check(lethal.text=="-3.38","Lethal text reports remaining 3.375 HP, not 999 overkill");Draw(app,"lethal-cap");Hurt(g,999);Check(Damage(g).Count==1,"Already dying cannot duplicate incoming labels");
        foreach(string boundary in new[]{"invulnerable","perfect-dodge","practice","sparring","range","zero"})
        {
            Prepare(app);g=app.Game;
            if(boundary=="invulnerable")g.invulnerable=1;
            else if(boundary=="perfect-dodge"){g.invulnerable=1;g.dashTime=.18f;g.perfectDodgeArmed=true;}
            else if(boundary=="practice")g.StartPractice();
            else if(boundary=="sparring"||boundary=="range")
            {g.save.practiceBits=7;var mentor=Array.Find(g.Map.objects,o=>o.kind=="mentor");Check(mentor!=null,"Existing Ada remains");g.save.x=mentor.x;g.save.y=mentor.y-.7f;Check(boundary=="sparring"?g.StartSparring():g.StartRange(0),"Actual safe training begins: "+boundary);}
            before=g.save.health;Hurt(g,boundary=="zero"?0:17,"shot");
            Check(g.save.health==before&&Damage(g).Count==0,"No actual loss means no incoming damage label at "+boundary);
            if(boundary=="perfect-dodge")Check(g.effects.Exists(e=>e.kind=="number"&&e.text=="COUNTER"),"Original perfect-dodge counter feedback survives");
            if(boundary=="sparring"||boundary=="range")Check(g.SafeTrainingActive&&!g.Dying,"Safe training damage accounting remains mature");
            if(g.practicing)g.EndPractice();
        }
        Console.WriteLine("PASS lethal overkill caps to remaining HP; already-dying, invulnerability, perfect dodge, original lesson, three-drill sparring, loadout range and zero loss emit no false incoming label; NATIVE_UNVERIFIED");
    }
    private static Rect RectOf(Label l) { return new Rect(l.r[0],l.r[1],l.r[2],l.r[3]); }
    private static Color ColorOf(Label l) { return new Color(l.color[0],l.color[1],l.color[2],l.color[3]); }
    private static bool Overlap(Rect a,Rect b) {return a.x<b.x+b.width&&b.x<a.x+a.width&&a.y<b.y+b.height&&b.y<a.y+a.height;}
    private static List<Label> Labels(FrontierRuntime app,List<Effect> effects,bool separated=true)
    {
        var labels=new List<Label>();
        foreach(string command in GUI.commands)
        {if(!command.StartsWith("{\"k\":\"text\"",StringComparison.Ordinal))continue;var label=JsonUtility.FromJson<Label>(command);if(label.r!=null&&label.r.Length==4&&Near(label.r[2],120)&&Near(label.r[3],32)&&label.size==(app.Game.save.largeText?20:19)&&effects.Exists(e=>e.text==label.text))labels.Add(label);}
        Check(labels.Count==effects.Count*2,"Each actual requested combat label has exactly one shadow/foreground pair (actual="+labels.Count+", expected="+(effects.Count*2)+")");var result=new List<Label>();
        for(int i=0;i<effects.Count;i++)
        {var shadow=labels[i*2];var front=labels[i*2+1];Check(front.text==effects[i].text&&shadow.text==effects[i].text&&front.center&&shadow.center,"Whole numeric labels retain effect order and middle-center alignment");Color expectedInk=Ink,expectedFront=effects[i].kind=="damageTaken"?Rose:Gold;if(effects[i].kind=="damageTaken"){float alpha=Math.Min(1,effects[i].life/.2f);expectedInk.a*=alpha;expectedFront.a*=alpha;}Check(Same(ColorOf(shadow),expectedInk)&&Same(ColorOf(front),expectedFront),"Incoming damage is rose and fades with its shadow; old outgoing/heal/break text remains exact gold");Check(Near(shadow.r[0],front.r[0]+1)&&Near(shadow.r[1],front.r[1]+1),"One-pixel ink shadow is preserved");result.Add(front);}
        if(separated)for(int a=0;a<result.Count;a++)for(int b=a+1;b<result.Count;b++){var first=RectOf(result[a]);var second=RectOf(result[b]);first.width++;first.height++;second.width++;second.height++;Check(!Overlap(first,second),"Incoming/outgoing/heal/break labels share non-overlapping lifetime lanes");}
        return result;
    }
    private static Point Offset(FrontierRuntime app,Effect effect,Label label)
    {Point screen=(Point)Call(app,"ScreenPoint",effect.position);float rise=(1-effect.life/effect.maxLife)*22;return new Point(label.r[0]-(screen.x-60),label.r[1]-(screen.y-55-rise));}
    private static void MixedRenderingAndHudWitness(FrontierRuntime app)
    {
        foreach(bool large in new[]{false,true})foreach(int width in new[]{960,1280,1920})
        {
            Prepare(app);var g=app.Game;g.save.largeText=large;Screen.width=width;Screen.height=width==960?640:width==1920?1080:800;Call(app,"Resize");float before=g.save.health;Hostile(app,12.5f);Update(app);var incoming=One(g,before);
            // Explicit mixed-message layout fixture supplements one genuine hostile contact.
            foreach(string text in new[]{"! 42","+HP","GUARDED","BREAK"})g.effects.Add(new Effect(text=="BREAK"?"break":"number",g.Position,.7f,text));
            var effects=g.effects.FindAll(TextEffect);Draw(app,large?"mixed-large-"+width:"mixed-"+width);var labels=Labels(app,effects);Point p=(Point)Call(app,"ScreenPoint",incoming.position);Check(Near(labels[0].r[0],p.x-60)&&Near(labels[0].r[1],p.y-55),"Isolated first incoming label preserves mature source anchor");
            string saved=JsonUtility.ToJson(g.save);float age=incoming.life,clock=Get<float>(app,"clock");var art=Get<PixelArt>(app,"art");int owned=Get<List<Texture2D>>(art,"owned").Count,cache=Get<Dictionary<string,Texture2D>>(art,"cache").Count;for(int draw=0;draw<12;draw++)Draw(app);Check(saved==JsonUtility.ToJson(g.save)&&incoming.life==age&&clock==Get<float>(app,"clock"),"Repeated presentation does not advance combat/save/effect clock");Check(owned==Get<List<Texture2D>>(art,"owned").Count&&cache==Get<Dictionary<string,Texture2D>>(art,"cache").Count,"Drawing damage text adds no texture or PixelArt cache allocation");
            var offsets=new List<Point>();for(int i=0;i<labels.Count;i++)offsets.Add(Offset(app,effects[i],labels[i]));Frames(app,12);Draw(app);labels=Labels(app,effects);
            for(int i=0;i<labels.Count;i++){Point offset=Offset(app,effects[i],labels[i]);Check(Near(offset.x,offsets[i].x)&&Near(offset.y,offsets[i].y),"Every mixed label retains its fixed source-relative lane and original 22px age lift through real recoil and camera movement");}
        }
        foreach(bool lethal in new[]{false,true})
        {
            Prepare(app);var game=app.Game;if(lethal)game.save.health=3;float hp=game.save.health;Hostile(app,17);Update(app);var actual=One(game,hp);
            // First draw allocates a genuine center-world slot. Camera movement
            // then puts that already allocated source under the real resource HUD.
            Draw(app);Point source=(Point)Call(app,"ScreenPoint",actual.position);Point east=(Point)Call(app,"ScreenPoint",actual.position+new Point(1,0));Set(app,"camera",Get<Point>(app,"camera")+(source-new Point(150,100))*(1/(east.x-source.x)));
            Draw(app,lethal?"lethal-hud-edge":"hud-edge");Clear();Call(app,"DrawHudThreatWitness");Labels(app,new List<Effect>{actual});
        }
        Prepare(app);var fadedGame=app.Game;float prior=fadedGame.save.health;Hostile(app,17);Update(app);var faded=One(fadedGame,prior);Frames(app,30);Check(faded.life>0&&faded.life<.12f,"Real Update reaches the final fade window before expiry");Draw(app,"fading");var fadeLabels=Labels(app,new List<Effect>{faded});Check(ColorOf(fadeLabels[0]).a<.6f&&ColorOf(fadeLabels[0]).a>0,"Incoming foreground and shadow visibly fade before removal");
        Console.WriteLine("PASS actual OnGUI rose incoming text plus unchanged gold outgoing/heal/guard/break labels, normal/large 960/1280/1920 lanes and exact age rise; actual HUD threat witness retains incoming damage; NATIVE_UNVERIFIED");
    }
    private static void LifecyclePauseAndSaveBoundaries(FrontierRuntime app)
    {
        foreach(bool reduced in new[]{false,true})foreach(int quality in new[]{0,1,2})
        {
            Prepare(app);var g=app.Game;g.save.reducedMotion=reduced;g.save.effectsQuality=quality;float before=g.save.health;Hostile(app,17);Update(app);Effect effect=One(g,before);Draw(app);Labels(app,new List<Effect>{effect});float life=effect.life;
            Input.keys.Add(KeyCode.Escape);Update(app);Check(g.mode!="world","Real Escape pauses the combat simulation");float paused=effect.life;Frames(app,12);Check(effect.life==paused&&paused<=life,"Pause does not age damage feedback");g.Close();Frames(app,50);Draw(app);Check(!g.effects.Contains(effect)&&Damage(g).Count==0&&!GUI.commands.Exists(c=>c.Contains("\"text\":\"-17\"")),"Actual clock expires/removes incoming labels in every effects/reduced-motion policy");
            FieldInfo placements=app.GetType().GetField("combatTextPlacements",Private);if(placements!=null)Check(((IList)placements.GetValue(app)).Count==0,"Expired labels release cached effect references");
        }
        foreach(string boundary in new[]{"travel","load","review"})
        {
            Prepare(app);var g=app.Game;g.save.flags.Add("damage_taken_story_sentinel");g.save.inventory[3]=7;g.save.coins=321;float before=g.save.health;Hostile(app,17);Update(app);Effect effect=One(g,before);Draw(app);
            if(boundary=="travel")g.Enter(1);
            else if(boundary=="load")
            {g.PrepareSave();string json=JsonUtility.ToJson(g.save);Check(!json.Contains("damageTaken"),"Transient text does not extend the persistent save schema");SaveData old=JsonUtility.FromJson<SaveData>(json);old.version=1;Check(SaveStore.Write(old,2),"Actual checksummed SaveStore writes the legacy-compatible campaign");string warning;SaveData restored=SaveStore.Read(2,out warning);Check(restored!=null&&warning==null,"Actual SaveStore verifies checksum and restores the selected slot");g.Load(restored);g.Close();}
            else {app.EndVisualReview();app.BeginVisualReview(1,0);app.VisualReviewPaused=false;}
            Check(!app.Game.effects.Contains(effect)&&Damage(app.Game).Count==0,"Actual "+boundary+" drops old-world incoming feedback");Draw(app);Check(!GUI.commands.Exists(c=>c.Contains("\"text\":\"-17\"")),"Actual "+boundary+" cannot draw stale health loss");
            if(boundary!="review")Check(g.Has("damage_taken_story_sentinel")&&g.save.inventory[3]==7&&g.save.coins==321,"Travel/legacy load preserve story, inventory and currency");
        }
        Console.WriteLine("PASS incoming lifetime/expiry/cache cleanup, real pause and all six FX/motion settings, travel/v1 load/review boundaries with original story/inventory/currency and unchanged save schema; NATIVE_UNVERIFIED");
    }
    public static int Main(string[] args)
    {
        string previous=Application.persistentDataPath,directory=Path.Combine(Path.GetTempPath(),"starfall-damage-taken-"+Guid.NewGuid());Application.persistentDataPath=directory;captureFolder=args.Length>0&&args[0]!="-"?args[0]:null;int failed=0;var app=new FrontierRuntime();
        try
        {Directory.CreateDirectory(directory);Call(app,"Awake");Check(app.Game!=null,"Actual production Awake loads campaign");foreach(Action<FrontierRuntime> test in new Action<FrontierRuntime>[]{MitigatedContactsUseActualDelta,LethalAndProtectedContacts,MixedRenderingAndHudWitness,LifecyclePauseAndSaveBoundaries})try{test(app);}catch(Exception ex){failed++;Console.WriteLine("FAIL "+test.Method.Name+": "+(ex.InnerException??ex));}app.EndVisualReview();Call(app,"OnDestroy");}
        finally {Application.persistentDataPath=previous;Input.Clear();GUI.queuedClick=null;GUI.matrix=Matrix4x4.identity;GUI.color=Color.white;GUI.enabled=true;Event.current.type=EventType.Repaint;if(Directory.Exists(directory))Directory.Delete(directory,true);}
        Console.WriteLine("Runtime damage taken: "+(4-failed)+"/4 groups passed; NATIVE_UNVERIFIED");return failed==0?0:1;
    }
}
