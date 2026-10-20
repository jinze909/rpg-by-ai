// Actual Hit / accepted J / Update / OnGUI against offline Unity declarations.
// Named effects in layout stress fixtures are explicitly synthetic; no native GPU
// rendering, font rasterization or play feel is certified by this harness.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using Starfall;
using UnityEngine;

public static class RuntimeCombatTextChecks
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static string captureFolder, capturePrefix;
    private static int captures;
    private static readonly Color Gold = PixelArt.C("#ebc58b"), Ink = PixelArt.C("#101a2b");
    public sealed class Label { public string k, text; public float[] r, color, m; public int size; public bool center; }
    private static void Check(bool ok,string text) { if(!ok)throw new Exception(text); }
    private static object Call(object app,string name,params object[] args)
    { var method=app.GetType().GetMethod(name,Private|BindingFlags.Public|BindingFlags.Static);Check(method!=null,"Existing production entry point: "+name);return method.Invoke(app,args); }
    private static void Set(object app,string name,object value) { app.GetType().GetField(name,Private).SetValue(app,value); }
    private static T Get<T>(object app,string name) { return (T)app.GetType().GetField(name,Private).GetValue(app); }
    private static bool Near(float a,float b,float tolerance=.002f) { return Math.Abs(a-b)<tolerance; }
    private static bool Same(Rect a,Rect b) { return Near(a.x,b.x)&&Near(a.y,b.y)&&Near(a.width,b.width)&&Near(a.height,b.height); }
    private static bool Same(Color a,Color b) { return Near(a.r,b.r)&&Near(a.g,b.g)&&Near(a.b,b.b)&&Near(a.a,b.a); }
    private static bool Same(Matrix4x4 a,Matrix4x4 b) { return Near(a.m00,b.m00)&&Near(a.m01,b.m01)&&Near(a.m03,b.m03)&&Near(a.m10,b.m10)&&Near(a.m11,b.m11)&&Near(a.m13,b.m13); }
    private static bool Overlap(Rect a,Rect b) { return a.x<b.x+b.width-.001f&&b.x<a.x+a.width-.001f&&a.y<b.y+b.height-.001f&&b.y<a.y+a.height-.001f; }
    private static Rect RectOf(Label value) { return new Rect(value.r[0],value.r[1],value.r[2],value.r[3]); }
    private static Color ColorOf(Label value) { return new Color(value.color[0],value.color[1],value.color[2],value.color[3]); }
    private static bool Critical(Effect effect) { return (effect.kind=="number"||effect.kind=="break"||effect.kind=="healthGain")&&effect.life>0; }
    private static void Clear() { GUI.commands.Clear();GUI.textureCalls.Clear();GUI.buttonCalls.Clear(); }
    private static void Draw(FrontierRuntime app,string capture=null)
    {
        Clear();Call(app,"OnGUI");
        if(captureFolder!=null&&capture!=null)
        {var commands=GUI.commands.ToArray();var textures=GUI.textureCalls.ToArray();var buttons=GUI.buttonCalls.ToArray();GUI.Export(captureFolder,capturePrefix+"-"+capture);captures++;GUI.commands.AddRange(commands);GUI.textureCalls.AddRange(textures);GUI.buttonCalls.AddRange(buttons);}
    }
    private static void Update(FrontierRuntime app) { Call(app,"Update");Input.keys.Clear(); }
    private static void Prepare(FrontierRuntime app,int profession=0)
    {
        Input.Clear();GUI.queuedClick=null;GUI.matrix=Matrix4x4.identity;GUI.color=Color.white;GUI.enabled=true;Screen.width=1280;Screen.height=800;Time.unscaledDeltaTime=.02f;Event.current.type=EventType.Repaint;
        app.BeginVisualReview(profession,0);app.VisualReviewPaused=false;var g=app.Game;g.enemies.Clear();g.hazards.Clear();g.projectiles.Clear();g.effects.Clear();g.fallen.Clear();g.save.companion=-1;g.save.reducedMotion=true;g.save.effectsQuality=0;g.save.showGuidance=false;g.save.english=true;g.save.largeText=false;g.save.health=g.save.maxHealth;g.toastTime=0;g.invulnerable=100;g.playerFlash=0;
        Point foot=new Point(15,14);Check(g.CanStand(foot)&&app.SetVisualReviewPosition(foot),"Shipped town accepts exact clear combat fixture feet");
        Set(app,"camera",g.Position);Set(app,"clock",0f);Set(app,"combatHudTime",3f);Set(app,"applicationPaused",false);Set(app,"controllerUsed",false);Set(app,"pointerSeen",false);Set(app,"pointerAimActive",false);Set(app,"resumeTime",0f);Call(app,"Resize");
    }
    private static Enemy EnemyAt(FrontierRuntime app,int id,int kind,Point position)
    {
        Check(app.Game.CanStand(position),"Genuine collision permits combat fixture enemy feet");var e=new Enemy(id,kind,position.x,position.y,999);e.timer=1000;e.facing=new Point(-1,0);app.Game.enemies.Add(e);return e;
    }
    private static void Hit(GameCore game,Enemy enemy,float amount=1) { Call(game,"Hit",enemy,amount,false,(Point?)null,null); }
    private static List<Label> Labels(FrontierRuntime app,bool nonoverlap=true)
    {
        var effects=app.Game.effects.FindAll(Critical);var all=new List<Label>();
        // Optional reflection keeps immutable pre-change source compilable. The
        // fixed baseline must fail semantic overlap rather than a missing API.
        FieldInfo placementField=app.GetType().GetField("combatTextPlacements",Private);
        if(placementField!=null)
        {
            var slots=(IList)placementField.GetValue(app);Check(slots.Count==effects.Count,"Prepared placement storage is bounded by exactly the currently live labels");
            foreach(object slot in slots){FieldInfo field=slot.GetType().GetField("effect",Private|BindingFlags.Public);Check(field!=null&&effects.Contains((Effect)field.GetValue(slot)),"Every cached offset belongs to an actual current live effect, never stale history");}
        }
        foreach(string command in GUI.commands)
        {
            if(!command.StartsWith("{\"k\":\"text\"",StringComparison.Ordinal))continue;
            Label label=JsonUtility.FromJson<Label>(command);
            if(label.r!=null&&label.r.Length==4&&Near(label.r[2],120)&&Near(label.r[3],32)&&label.size==(app.Game.save.largeText?20:19)&&effects.Exists(e=>e.text==label.text))all.Add(label);
        }
        Check(all.Count==effects.Count*2,"Every actual live number/break is drawn exactly once with its original shadow (actual="+all.Count+", expected="+(effects.Count*2)+")");
        var result=new List<Label>();
        for(int i=0;i<effects.Count;i++)
        {
            Label shadow=all[i*2],foreground=all[i*2+1];
            Check(shadow.text==effects[i].text&&foreground.text==effects[i].text&&shadow.center&&foreground.center,"Original complete effect texts, list order and middle-center alignment are retained");
            Color expectedShadow=Ink,expectedFront=effects[i].kind=="healthGain"?PixelArt.C("#a8e8bc"):Gold;
            if(effects[i].kind=="healthGain"){float alpha=Math.Min(1,effects[i].life/.2f);expectedShadow.a*=alpha;expectedFront.a*=alpha;}
            Check(Same(ColorOf(shadow),expectedShadow)&&Same(ColorOf(foreground),expectedFront),"Original gold/ink remain exact; real healing uses its separate green tint and fade");
            Check(Near(shadow.r[0],foreground.r[0]+1)&&Near(shadow.r[1],foreground.r[1]+1),"Original one-pixel shadow remains offset from its own foreground");
            result.Add(foreground);
        }
        if(nonoverlap)for(int a=0;a<result.Count;a++)for(int b=a+1;b<result.Count;b++)
        {Rect first=RectOf(result[a]),second=RectOf(result[b]);first.width++;first.height++;second.width++;second.height++;Check(!Overlap(first,second),"Actual combat text rectangles (including shadows) must not overlap: "+result[a].text+" / "+result[b].text);}
        return result;
    }
    private static Point Offset(FrontierRuntime app,Effect effect,Label label)
    {Point p=(Point)Call(app,"ScreenPoint",effect.position);float progress=1-effect.life/effect.maxLife;return new Point(label.r[0]-(p.x-60),label.r[1]-(p.y-55-progress*22));}
    private static void SingleOriginal(FrontierRuntime app,Effect effect)
    {Draw(app);var labels=Labels(app);Check(labels.Count==1,"A single live label remains a single pair");Point offset=Offset(app,effect,labels[0]);Check(Near(offset.x,0)&&Near(offset.y,0),"Isolated label keeps exact original 120x32 anchor and 22px age lift");}
    private static List<Effect> GuardBreak(FrontierRuntime app,bool actualJ=false)
    {
        Prepare(app);var g=app.Game;Enemy heavy=EnemyAt(app,53100,2,g.Position+new Point(.9f,0));heavy.stagger=29.99f;float hp=heavy.hp;
        if(actualJ)
        {
            Point screen=(Point)Call(app,"ScreenPoint",heavy.Position);Input.mousePosition=new Vector3(screen.x,Screen.height-screen.y,0);Input.held.Add(KeyCode.J);Update(app);Input.Clear();
            Check(g.playerAction!=null&&!g.playerAction.released&&heavy.hp==hp,"Actual J preserves the ordinary preparation before real contact");int frames=0;while(heavy.hp==hp&&frames++<60)Update(app);Check(frames<60,"Actual prepared J reaches the heavy");
        }
        else Hit(g,heavy);
        Check(heavy.hp<hp&&heavy.stun>0&&g.effects.Exists(e=>e.kind=="number"&&e.text=="GUARDED")&&g.effects.Exists(e=>e.kind=="break"&&e.text=="BREAK"),"One real front-armored Hit produces guard, break and numeric damage without gameplay changes");
        var texts=g.effects.FindAll(Critical);Check(texts.Count==3,"That real contact retains all three distinct critical effects");return texts;
    }
    private static List<Effect> StaffBlast(FrontierRuntime app)
    {
        Prepare(app,2);var g=app.Game;float[] dy={0,-.6f,.6f};var foes=new List<Enemy>();for(int i=0;i<3;i++){var e=EnemyAt(app,53110+i,0,g.Position+new Point(2.6f,dy[i]));e.stagger=29.99f;foes.Add(e);}
        Point screen=(Point)Call(app,"ScreenPoint",foes[0].Position);Input.mousePosition=new Vector3(screen.x,Screen.height-screen.y,0);Input.held.Add(KeyCode.J);Update(app);Input.Clear();Check(g.playerAction!=null&&g.playerAction.weapon==2&&!g.playerAction.released,"Actual staff J first prepares its mature action");
        int frames=0;while(foes.Exists(e=>e.hp==e.maxHp)&&frames++<100)Update(app);
        Check(frames<100&&foes.TrueForAll(e=>e.hp<e.maxHp)&&g.effects.FindAll(Critical).Count==6,"A real launched staff blast damages and breaks its direct foe and both splash neighbors");return g.effects.FindAll(Critical);
    }
    private static Effect MiraAutomaticHeal(FrontierRuntime app)
    {
        Prepare(app);var g=app.Game;if(!g.Has("companion_0"))g.save.flags.Add("companion_0");g.SelectCompanion(0);Check(g.save.companion==0,"Existing safe-travel selection accepts unlocked Mira");g.companionPosition=g.Position;Set(g,"companionAttack",0f);g.save.health=g.save.maxHealth*.5f;float before=g.save.health;
        Update(app);Check(g.companionAction!=null&&g.companionAction.kind=="heal"&&!g.companionAction.released&&Near(g.save.health,before)&&!g.effects.Exists(Critical),"Actual automatic Mira heal prepares before its contact and creates no early healing label");
        int frames=0;while(Near(g.save.health,before)&&frames++<30)Update(app);
        var labels=g.effects.FindAll(Critical);Check(frames<30&&g.save.health>before&&g.save.health<=g.save.maxHealth&&labels.Count==1&&labels[0].kind=="healthGain"&&labels[0].text=="+"+(g.save.health-before).ToString("0.##",CultureInfo.InvariantCulture), "Actual automatic healing contact raises HP and emits its true received amount once");
        Check((labels[0].position-g.Position).Length<.001f&&labels[0].life>0&&labels[0].life<=labels[0].maxLife,"Real heal feedback is anchored to the healed player's current position with its mature effect lifetime");return labels[0];
    }
    private static void RealContactsAndSinglePreservation(FrontierRuntime app)
    {
        GuardBreak(app);Draw(app);Labels(app);GuardBreak(app,true);Draw(app);Labels(app);
        StaffBlast(app);Draw(app);Labels(app);
        Effect heal=MiraAutomaticHeal(app);SingleOriginal(app,heal);for(int frame=0;frame<40;frame++)Update(app);Draw(app);Labels(app);Check(!app.Game.effects.Contains(heal)&&!GUI.commands.Exists(c=>{if(!c.StartsWith("{\"k\":\"text\"",StringComparison.Ordinal))return false;var label=JsonUtility.FromJson<Label>(c);return label.text==heal.text&&Near(label.r[2],120)&&Near(label.r[3],32);}),"Actual Update expires and removes the once-emitted healing label without stale draw or cache history");
        foreach(bool large in new[]{false,true})foreach(string kind in new[]{"number","break"})foreach(float progress in new[]{0f,.3f,.9f})
        {Prepare(app);app.Game.save.largeText=large;var effect=new Effect(kind,app.Game.Position,.7f,"QA solo");effect.life=effect.maxLife*(1-progress);app.Game.effects.Add(effect);SingleOriginal(app,effect);}
        Prepare(app);foreach(string text in new[]{"! 42","+HP","GUARDED","BREAK"})app.Game.effects.Add(new Effect(text=="BREAK"?"break":"number",app.Game.Position,.7f,text));Draw(app);Labels(app);
        Console.WriteLine("PASS genuine guarded+break+damage Hit and accepted J, real three-target staff blast, actual Mira healing preparation/contact/expiry, retained gold labels/shadow/font and exact isolated age/anchor; NATIVE_UNVERIFIED");
    }
    private static void StableExpiryPoliciesAndWorldBoundary(FrontierRuntime app)
    {
        Prepare(app);var g=app.Game;var oldest=new Effect("number",g.Position,.1f,"fixture-oldest");var second=new Effect("break",g.Position,.7f,"fixture-survivor");var third=new Effect("number",g.Position+new Point(.2f,0),.7f,"fixture-third");g.effects.AddRange(new[]{oldest,second,third});
        Draw(app);var original=Labels(app);var offsetSecond=Offset(app,second,original[1]);var offsetThird=Offset(app,third,original[2]);
        for(int frame=0;frame<8;frame++)
        {
            Update(app);Draw(app);var now=Labels(app);var live=g.effects.FindAll(Critical);int ix=live.IndexOf(second),iy=live.IndexOf(third);Check(ix>=0&&iy>=0,"Real updates retain the two longer effects");Point a=Offset(app,second,now[ix]),b=Offset(app,third,now[iy]);Check(Near(a.x,offsetSecond.x)&&Near(a.y,offsetSecond.y)&&Near(b.x,offsetThird.x)&&Near(b.y,offsetThird.y),"Older expiry cannot reflow surviving labels into vacated slots");
        }
        Check(!g.effects.Contains(oldest),"Actual Update removes the expired predecessor");g.effects.Add(new Effect("number",g.Position,.7f,"fixture-new-arrival"));Draw(app);Labels(app);
        for(int frame=0;frame<18;frame++){Update(app);Draw(app);Labels(app);}
        foreach(bool large in new[]{false,true})foreach(bool reduced in new[]{false,true})foreach(int fx in new[]{0,1,2})
        {g.save.largeText=large;g.save.reducedMotion=reduced;g.save.effectsQuality=fx;Draw(app);Labels(app);}
        Prepare(app);g=app.Game;var predecessor=new Effect("number",g.Position,.7f,"boundary-fixture-first");var reused=new Effect("break",g.Position,.7f,"boundary-fixture-reused");g.effects.AddRange(new[]{predecessor,reused});Draw(app);var boundary=Labels(app);Point previousOffset=Offset(app,reused,boundary[1]);Check(!Near(previousOffset.x,0)||!Near(previousOffset.y,0),"Explicit boundary fixture first owns a displaced slot");
        int generation=g.WorldGeneration;g.Enter(1);Check(g.WorldGeneration!=generation,"Existing Enter creates a real world generation boundary");g.effects.Clear();g.enemies.Clear();g.hazards.Clear();g.projectiles.Clear();Set(app,"camera",g.Position);
        // Intentionally reuse the old effect object only to expose stale layout
        // references; normal Enter correctly clears its transient effect list.
        reused.position=g.Position;reused.life=reused.maxLife=.7f;g.effects.Add(reused);SingleOriginal(app,reused);
        Prepare(app);var single=new Effect("number",app.Game.Position,.7f,"new-world-single");app.Game.effects.Add(single);SingleOriginal(app,single);
        app.EndVisualReview();FieldInfo placements=app.GetType().GetField("combatTextPlacements",Private);if(placements!=null){Check(((IList)placements.GetValue(app)).Count==0&&Get<GameCore>(app,"combatTextGame")==null,"EndVisualReview immediately releases every cached effect and review GameCore reference");}
        Console.WriteLine("PASS real age/expiry and new arrivals stay separated without survivor reflow; low/rich-FX, reduced motion, large text, fresh-world reset and review cleanup; NATIVE_UNVERIFIED");
    }
    private static void DenseLayoutAndNoStaleLabels(FrontierRuntime app)
    {
        foreach(int width in new[]{960,1280,1920})foreach(bool large in new[]{false,true})
        {
            Prepare(app);Screen.width=width;Screen.height=width==960?640:width==1920?1080:800;app.Game.save.largeText=large;Call(app,"Resize");
            // Explicit layout stress fixture: distinct text effects at nearby real
            // world points. These are not presented as extra natural combat hits.
            for(int i=0;i<12;i++)app.Game.effects.Add(new Effect(i%3==0?"break":"number",app.Game.Position+new Point((i%3)*.15f,(i%2)*.1f),.7f,"fixture-"+i));
            Draw(app);var initial=Labels(app);var offsets=new List<Point>();for(int i=0;i<initial.Count;i++)offsets.Add(Offset(app,app.Game.effects[i],initial[i]));
            Set(app,"camera",app.Game.Position+new Point(.7f,-.4f));Draw(app);var moved=Labels(app);for(int i=0;i<moved.Count;i++){Point value=Offset(app,app.Game.effects[i],moved[i]);Check(Near(value.x,offsets[i].x)&&Near(value.y,offsets[i].y),"Camera movement cannot rebuild fixed per-effect slot offsets");}
            for(int frame=0;frame<38;frame++){Update(app);Draw(app);if(app.Game.effects.Exists(Critical))Labels(app);}
            Check(!app.Game.effects.Exists(Critical),"All stress labels expire through unchanged real effect clock");Draw(app);Labels(app);Check(!GUI.commands.Exists(c=>c.Contains("fixture-")),"No expired label survives any world draw");
            var isolated=new Effect("number",app.Game.Position,.7f,"post-expiry-single");app.Game.effects.Add(isolated);SingleOriginal(app,isolated);
        }
        Console.WriteLine("PASS 12-label explicit stress across normal/large 960/1280/1920, deterministic camera movement and full natural expiry with no stale slots; NATIVE_UNVERIFIED");
    }
    private static Point AtScreen(FrontierRuntime app,Point screen)
    { return (Point)Call(app,"WorldAtPointer",screen); }
    private static Rect Envelope(FrontierRuntime app,Effect effect,Label label)
    {Point p=(Point)Call(app,"ScreenPoint",effect.position)+Offset(app,effect,label);return new Rect(p.x-60,p.y-77,121,55);}
    private static bool OverlapsAny(Rect rect,List<Rect> regions)
    {foreach(Rect region in regions)if(Overlap(rect,region))return true;return false;}
    private static void OffCanvasLabelsStayClipped(FrontierRuntime app)
    {
        foreach(Point screen in new[]{new Point(-80,390),new Point(1360,390),new Point(640,-40),new Point(640,940)})
        {
            Prepare(app);Point world=AtScreen(app,screen);
            // Deliberate off-canvas fixture, not a claim that unseen real hits
            // naturally emit these QA tokens. Both source rectangles are clipped.
            var first=new Effect("number",world,.7f,"QA off A");var second=new Effect("break",world,.7f,"QA off B");app.Game.effects.AddRange(new[]{first,second});Draw(app);
            var labels=Labels(app,false);
            for(int i=0;i<labels.Count;i++)
            {
                Point offset=Offset(app,app.Game.effects[i],labels[i]);Rect rect=RectOf(labels[i]);
                Check(Near(offset.x,0)&&Near(offset.y,0),"A wholly off-canvas original combat label must remain source-relative instead of being pulled into view by collision search (side="+screen.x+","+screen.y+", label="+i+")");
                Check(!Overlap(new Rect(0,0,1280,800),new Rect(rect.x,rect.y,rect.width+1,rect.height+1)),"Both foreground and shadow of deliberately clipped QA labels remain outside the canvas");
            }
        }
        Console.WriteLine("PASS simultaneous wholly clipped labels retain exact original source anchors on all four canvas edges; clipped labels may overlap outside view; NATIVE_UNVERIFIED");
    }
    private static List<Effect> GuardBreakAtHud(FrontierRuntime app,Point destination)
    {
        var effects=GuardBreak(app);Point source=(Point)Call(app,"ScreenPoint",effects[0].position);Point unit=(Point)Call(app,"ScreenPoint",effects[0].position+new Point(1,0));
        // Actual mature Hit supplies all three labels; only the review camera
        // is staged so the collision is adjacent to real status/actions.
        Set(app,"camera",Get<Point>(app,"camera")+(source-destination)*(1f/(unit.x-source.x)));return effects;
    }
    private static void RelocatedLanesRespectActualHud(FrontierRuntime app)
    {
        foreach(Point destination in new[]{new Point(150,150),new Point(640,760)})
        {
            var effects=GuardBreakAtHud(app,destination);Draw(app);
            var labels=Labels(app);var regions=(List<Rect>)Call(app,"HudRegions");Point original=Offset(app,effects[0],labels[0]);
            Check(Near(original.x,0)&&Near(original.y,0),"Original singleton policy is retained exactly even beside HUD; only later colliding labels relocate");
            Check(OverlapsAny(Envelope(app,effects[0],labels[0]),regions),"The real-Hit camera fixture deliberately exercises an original HUD-edge envelope");
            for(int i=1;i<labels.Count;i++)
            {
                Point offset=Offset(app,effects[i],labels[i]);Check(!Near(offset.x,0)||!Near(offset.y,0),"Each colliding real combat message keeps a separate relocated lane");
                Check(!OverlapsAny(Envelope(app,effects[i],labels[i]),regions),"A relocated combat lane must avoid all actual HudRegions for its complete 22px rise and shadow (label="+i+", edge="+destination.x+","+destination.y+")");
            }
        }
        Console.WriteLine("PASS real guarded+break+damage collision lanes avoid actual status/action HUD regions for their whole rise while the original singleton anchor stays exact; NATIVE_UNVERIFIED");
    }
    private static void Append(StringBuilder text,object value,HashSet<object> seen)
    {
        if(value==null){text.Append("null;");return;}Type type=value.GetType();if(type.IsPrimitive||type.IsEnum||value is string||value is decimal){text.Append(Convert.ToString(value,CultureInfo.InvariantCulture)).Append(';');return;}if(!type.IsValueType&&!seen.Add(value)){text.Append("ref;");return;}var seq=value as IEnumerable;if(seq!=null){foreach(object item in seq)Append(text,item,seen);text.Append("end;");return;}var fields=type.GetFields(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);Array.Sort(fields,(a,b)=>string.CompareOrdinal(a.Name,b.Name));foreach(var f in fields){text.Append(f.Name).Append('=');Append(text,f.GetValue(value),seen);}
    }
    private static string State(GameCore game) {var text=new StringBuilder();Append(text,game,new HashSet<object>());return text.ToString();}
    private static string Files(){var text=new StringBuilder();var paths=Directory.GetFiles(Application.persistentDataPath,"*",SearchOption.AllDirectories);Array.Sort(paths,StringComparer.Ordinal);foreach(string path in paths)text.Append(path).Append(':').Append(Convert.ToBase64String(File.ReadAllBytes(path))).Append(';');return text.ToString();}
    private static void IsolationAndActualClock(FrontierRuntime app)
    {
        GuardBreak(app);var art=Get<PixelArt>(app,"art");Call(art,"Glow");Draw(app);var owned=Get<List<Texture2D>>(art,"owned");var cache=Get<Dictionary<string,Texture2D>>(art,"cache");int owns=owned.Count,caches=cache.Count;string files=Files();float clock=Get<float>(app,"clock");var matrix=Matrix4x4.TRS(new Vector3(19,31,0),Quaternion.Euler(0,0,17),new Vector3(1.4f,1.4f,1));var tint=new Color(.3f,.5f,.7f,.8f);
        foreach(EventType evt in new[]{EventType.Layout,EventType.Repaint}){Event.current.type=evt;string state=State(app.Game);GUI.matrix=matrix;GUI.color=tint;GUI.enabled=false;Draw(app);Labels(app);Check(State(app.Game)==state&&Near(Get<float>(app,"clock"),clock),"Actual layout/repaint preserves every Core/save field and presentation clock");Check(Same(GUI.matrix,matrix)&&Same(GUI.color,tint)&&!GUI.enabled,"Actual OnGUI restores incoming GUI matrix/color/enabled");}
        Check(owned.Count==owns&&cache.Count==caches,"Text layout allocates no PixelArt texture or cache entry");Check((bool)Call(app,"Save")&&Files()==files,"Review draw and Save leave existing save/slot/backup files byte-identical");GUI.matrix=Matrix4x4.identity;GUI.color=Color.white;GUI.enabled=true;Event.current.type=EventType.Repaint;
        var control=new FrontierRuntime();Call(control,"Awake");try{GuardBreak(app);GuardBreak(control);for(int frame=0;frame<65;frame++){Input.Clear();Update(app);Update(control);string state=State(app.Game);Check(State(control.Game)==state,"Rendered/unrendered complete Core state matches at real frame "+frame);Draw(app);Check(State(app.Game)==state,"OnGUI cannot change any subsequent combat clock or effect lifetime");}}finally{control.EndVisualReview();Call(control,"OnDestroy");}
        Console.WriteLine("PASS complete Core/save/GUI/clock/texture-cache isolation and 65 actual rendered-versus-unrendered Update frames through effect expiry; NATIVE_UNVERIFIED");
    }
    private static void Captures(FrontierRuntime app)
    {
        GuardBreak(app);Draw(app,"guard-break-damage");GuardBreak(app,true);app.Game.save.english=false;Draw(app,"actual-j");StaffBlast(app);Draw(app,"staff-three-targets");
        GuardBreak(app);app.Game.save.largeText=true;Draw(app,"large");GuardBreak(app);Screen.width=960;Screen.height=640;Call(app,"Resize");Draw(app,"small-lowfx");GuardBreak(app);Screen.width=1920;Screen.height=1080;app.Game.save.effectsQuality=2;app.Game.save.reducedMotion=false;Call(app,"Resize");Draw(app,"wide-richfx");
        Prepare(app);for(int i=0;i<8;i++)app.Game.effects.Add(new Effect(i%2==0?"number":"break",app.Game.Position+new Point((i%3)*.15f,0),.7f,"fixture-"+i));Draw(app,"dense-fixture");
        GuardBreak(app);for(int i=0;i<18;i++)Update(app);Draw(app,"aged");for(int i=0;i<22;i++)Update(app);Draw(app,"expired");Prepare(app);app.Game.effects.Add(new Effect("number",app.Game.Position,.7f,"QA solo"));Draw(app,"isolated-original");
        MiraAutomaticHeal(app);Draw(app,"actual-mira-heal");
        Prepare(app);Point clipped=AtScreen(app,new Point(-80,390));app.Game.effects.Add(new Effect("number",clipped,.7f,"QA off A"));app.Game.effects.Add(new Effect("break",clipped,.7f,"QA off B"));Draw(app,"clipped-edge-fixture");
        GuardBreakAtHud(app,new Point(150,150));Draw(app,"status-edge-real-hit");GuardBreakAtHud(app,new Point(640,760));Draw(app,"action-edge-real-hit");
        Check(captures==14,"Fourteen representative actual OnGUI captures");Console.WriteLine("CAPTURE 14 actual OnGUI combat text fixtures; NATIVE_UNVERIFIED");
    }
    public static int Main(string[] args)
    {
        string previous=Application.persistentDataPath,directory=Path.Combine(Path.GetTempPath(),"starfall-combat-text-"+Guid.NewGuid());Application.persistentDataPath=directory;captureFolder=args.Length>0&&args[0]!="-"?args[0]:null;capturePrefix=args.Length>1?args[1]:"qa-combat-text";bool only=args.Length>2&&args[2]=="capture-only";int failed=0;var app=new FrontierRuntime();
        try{Directory.CreateDirectory(directory);File.WriteAllText(Path.Combine(directory,"starfall-v1.json"),"{\"version\":1,\"profession\":0}");File.WriteAllText(Path.Combine(directory,"starfall-slot-1.json.bak"),"existing-slot-backup-sentinel");Call(app,"Awake");Check(app.Game!=null,"Actual production Awake loads mature campaign");if(!only)foreach(Action<FrontierRuntime> test in new Action<FrontierRuntime>[]{RealContactsAndSinglePreservation,StableExpiryPoliciesAndWorldBoundary,DenseLayoutAndNoStaleLabels,IsolationAndActualClock,OffCanvasLabelsStayClipped,RelocatedLanesRespectActualHud})try{test(app);}catch(Exception ex){failed++;Console.WriteLine("FAIL "+test.Method.Name+": "+(ex.InnerException??ex).ToString());}if(captureFolder!=null)Captures(app);app.EndVisualReview();Call(app,"OnDestroy");}
        finally{Application.persistentDataPath=previous;Input.Clear();GUI.queuedClick=null;GUI.matrix=Matrix4x4.identity;GUI.color=Color.white;GUI.enabled=true;Event.current.type=EventType.Repaint;if(Directory.Exists(directory))Directory.Delete(directory,true);}if(only){Console.WriteLine("Capture-only: semantic checks skipped; NATIVE_UNVERIFIED");return 0;}Console.WriteLine("Runtime combat text: "+(6-failed)+"/6 groups passed; NATIVE_UNVERIFIED");return failed==0?0:1;
    }
}
