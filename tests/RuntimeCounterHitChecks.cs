// Offline actual Update / OnGUI / SaveStore evidence. Scene/GPU/player feel is
// NATIVE_UNVERIFIED. Dense-layout effects below are explicit stress fixtures.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using Starfall;
using UnityEngine;

public static class RuntimeCounterHitChecks
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private static string captureFolder,capturePrefix;
    private static int nextId=67400,captures;
    private static Effect earned;
    public sealed class Label { public string k,text;public float[] r,color,m;public int size;public int[] padding;public bool center; }
    private static void Check(bool value,string message){if(!value)throw new Exception(message);}
    private static object Call(object app,string name,params object[] args){return app.GetType().GetMethod(name,Private|BindingFlags.Public).Invoke(app,args);}
    private static void Set(object app,string name,object value){app.GetType().GetField(name,Private).SetValue(app,value);}
    private static T Get<T>(object app,string name){return(T)app.GetType().GetField(name,Private).GetValue(app);}
    private static bool Near(float a,float b){return Math.Abs(a-b)<.003f;}
    private static bool Same(Rect a,Rect b){return Near(a.x,b.x)&&Near(a.y,b.y)&&Near(a.width,b.width)&&Near(a.height,b.height);}
    private static bool Same(Color a,Color b){return Near(a.r,b.r)&&Near(a.g,b.g)&&Near(a.b,b.b)&&Near(a.a,b.a);}
    private static bool Overlap(Rect a,Rect b){return a.x<b.x+b.width-.001f&&b.x<a.x+a.width-.001f&&a.y<b.y+b.height-.001f&&b.y<a.y+a.height-.001f;}
    private static Rect RectOf(Label label){return new Rect(label.r[0],label.r[1],label.r[2],label.r[3]);}
    private static Color ColorOf(Label label){return new Color(label.color[0],label.color[1],label.color[2],label.color[3]);}
    private static void Update(FrontierRuntime app){Call(app,"Update");Input.keys.Clear();}
    private static void Frames(FrontierRuntime app,int n){for(int i=0;i<n;i++)Update(app);}
    private static void Key(FrontierRuntime app,KeyCode key){Input.keys.Add(key);Update(app);}
    private static void Draw(FrontierRuntime app,string name=null)
    {
        GUI.commands.Clear();GUI.textureCalls.Clear();GUI.buttonCalls.Clear();Call(app,"OnGUI");
        if(captureFolder!=null&&name!=null){var commands=GUI.commands.ToArray();var textures=GUI.textureCalls.ToArray();var buttons=GUI.buttonCalls.ToArray();GUI.Export(captureFolder,capturePrefix+"-"+name);captures++;GUI.commands.AddRange(commands);GUI.textureCalls.AddRange(textures);GUI.buttonCalls.AddRange(buttons);}
    }
    private static List<Label> Texts(string text,float height)
    {
        var result=new List<Label>();foreach(string command in GUI.commands)if(command.StartsWith("{\"k\":\"text\"",StringComparison.Ordinal)){var value=JsonUtility.FromJson<Label>(command);if(value.text==text&&value.r!=null&&Near(value.r[2],120)&&Near(value.r[3],height))result.Add(value);}return result;
    }
    private static int Badges(FrontierRuntime app){return Texts(app.Game.L("反击","COUNTER"),16).Count;}
    private static void Position(FrontierRuntime app,Point foot){Check(app.Game.CanStand(foot)&&app.SetVisualReviewPosition(foot)&&(app.Game.Position-foot).Length<.001f,"Exact legal fixture feet");Set(app,"camera",app.Game.Position);}
    private static void Prepare(FrontierRuntime app,int profession=0)
    {
        Input.Clear();GUI.queuedClick=null;GUI.matrix=Matrix4x4.identity;GUI.color=Color.white;GUI.enabled=true;Screen.width=1280;Screen.height=800;Time.unscaledDeltaTime=.02f;Event.current.type=EventType.Repaint;
        app.BeginVisualReview(profession,0);app.VisualReviewPaused=false;var g=app.Game;g.Enter(1);g.Close();g.enemies.Clear();g.hazards.Clear();g.projectiles.Clear();g.effects.Clear();g.fallen.Clear();g.save.companion=-1;g.save.english=true;g.save.largeText=false;g.save.reducedMotion=true;g.save.effectsQuality=0;g.save.showGuidance=false;g.save.weapon=0;g.save.weaponStyles=new int[3];g.save.relics=new[]{-1,-1};g.save.health=g.save.maxHealth;g.save.energy=100;g.invulnerable=0;g.toastTime=0;
        if(!g.Has("weapon_1"))g.save.flags.Add("weapon_1");if(!g.Has("weapon_2"))g.save.flags.Add("weapon_2");Position(app,new Point(12,14));Set(app,"applicationPaused",false);Set(app,"resumeTime",0f);Set(app,"pointerSeen",false);Set(app,"pointerAimActive",false);Set(app,"controllerUsed",false);Set(app,"combatHudTime",3f);Call(app,"Resize");
    }
    private static Enemy Foe(FrontierRuntime app,Point position,int kind=0){Check(app.Game.CanStand(position),"Real footprint accepts victim");var e=new Enemy(++nextId,kind,position.x,position.y,999){timer=1000};e.Face(new Point(-1,0));app.Game.enemies.Add(e);return e;}
    private static void Aim(FrontierRuntime app,Point target){Set(app,"camera",app.Game.Position);Point p=(Point)Call(app,"ScreenPoint",target);float scale=Get<float>(app,"scale");Input.mousePosition=new Vector3(Get<float>(app,"offsetX")+p.x*scale,Screen.height-Get<float>(app,"offsetY")-p.y*scale,0);}
    private static ActorAction Attack(FrontierRuntime app){Input.held.Add(KeyCode.J);Update(app);Input.held.Clear();var a=app.Game.playerAction;Check(a!=null&&a.kind=="attack"&&!a.released,"Actual J first prepares original attack");return a;}
    private static void Click(FrontierRuntime app,string text){Draw(app);foreach(string command in GUI.commands)if(command.Contains("\"text\":"+GUI.S(text))){var value=JsonUtility.FromJson<Label>(command);Rect r=RectOf(value);if(!GUI.buttonCalls.Exists(b=>Same(b.rect,r)&&b.enabled))continue;GUI.queuedClick=r;Draw(app);Check(!GUI.queuedClick.HasValue,"Actual equipment button consumes click");return;}throw new Exception("Missing real button: "+text);}
    private static void Equip(FrontierRuntime app,int weapon){Key(app,KeyCode.I);Check(app.Game.mode=="inventory","Actual I opens equipment");Click(app,app.Game.L(new[]{"短剑","猎弓","星火杖"}[weapon],new[]{"Sword","Bow","Starfire staff"}[weapon]));Click(app,app.Game.L("装备此打法","Equip style"));Key(app,KeyCode.Escape);Check(app.Game.save.weapon==weapon&&app.Game.mode=="world","Actual weapon selection and Escape resume");}
    private static void EarnCounter(FrontierRuntime app)
    {
        var g=app.Game;var e=Foe(app,g.Position+new Point(1.1f,0));e.timer=.15f;int frames=0;while(!(e.state==1&&e.timer<=.055f)&&frames++<250)Update(app);Check(frames<250,"A real hunter reaches its natural melee windup");float hp=g.save.health;
        Input.axes["Vertical"]=1;Input.held.Add(KeyCode.W);Key(app,KeyCode.Space);Input.Clear();while(g.riposteTime<=0&&frames++<300)Update(app);Check(g.riposteTime>0&&g.save.health==hp,"Actual Space earns counter from genuine incoming melee without artificial invulnerability");while(g.dashTime>0)Update(app);g.enemies.Clear();g.effects.Clear();
    }
    private static Effect Contact(FrontierRuntime app,Enemy e,ActorAction action)
    {
        float before=e.hp;int frames=0;while(e.hp==before&&frames++<150)Update(app);Check(frames<150&&e.hp<before,"Real prepared J physically contacts its living victim");var effects=app.Game.effects.FindAll(x=>x.kind=="number"&&x.text!="GUARDED");Check(effects.Count==1,"Exactly one real numeric receipt for one contact");Effect label=effects[0];string expected=(label.text.StartsWith("! ",StringComparison.Ordinal)?"! ":"")+((int)(before-e.hp));Check(label.text==expected,"Full original critical prefix and integer damage match real health subtraction");Check(Near(label.maxLife,.7f)&&label.variant==1,"Only actual counter contact marks its unchanged seven-tenths-second damage number");Check(action.impact.counter&&action.released,"Released action retains captured counter identity");Draw(app);Check(Badges(app)==2,"Actual OnGUI renders one counter caption with shadow after contact");return label;
    }
    public static void GenuineDodgeJAndFlyingSnapshots(FrontierRuntime app)
    {
        for(int weapon=0;weapon<3;weapon++)
        {
            Prepare(app);EarnCounter(app);if(weapon>0)Equip(app,weapon);var g=app.Game;Enemy e=Foe(app,g.Position+new Point(weapon==0?1.1f:4.2f,0));Aim(app,e.Position);g.save.seed=0;ActorAction a=Attack(app);Check(a.impact.counter&&g.riposteTime==0,"Actual J consumes earned counter only at action start");Draw(app);Check(Badges(app)==0&&e.hp==999,"Preparation has neither damage nor premature counter badge");
            if(weapon>0){int frames=0;while(!a.released&&frames++<60)Update(app);Check(frames<60&&g.projectiles.Count>0&&e.hp==999,"Original ranged contact is still flying after visible release");Equip(app,weapon==1?2:0);Check(a.weapon==weapon&&a.impact.counter,"Real inventory swap cannot retag an already flying impact");}
            earned=Contact(app,e,a);Draw(app,"actual-"+new[]{"sword","bow-after-swap","staff-after-swap"}[weapon]);Check(earned.text.StartsWith("! ",StringComparison.Ordinal),"Deterministic genuine contact also retains critical prefix");
        }
        Prepare(app);EarnCounter(app);var heavy=Foe(app,app.Game.Position+new Point(1.1f,0),2);heavy.stagger=29.99f;Aim(app,heavy.Position);Contact(app,heavy,Attack(app));Draw(app,"real-counter-guard-break");Check(app.Game.effects.Exists(x=>x.text=="GUARDED")&&app.Game.effects.Exists(x=>x.text=="BREAK"),"A real counter contact retains both mature front-guard and stagger-break feedback");CheckLayout(app);
        Prepare(app);EarnCounter(app);var miss=Foe(app,app.Game.Position+new Point(5,0));Aim(app,miss.Position);Attack(app);Frames(app,25);Draw(app);Check(miss.hp==999&&Badges(app)==0,"An actual consumed sword counter that misses never advertises a contact");
        Prepare(app);var ordinary=Foe(app,app.Game.Position+new Point(1,0));Aim(app,ordinary.Position);Attack(app);Frames(app,16);Draw(app);Check(ordinary.hp<999&&Badges(app)==0&&app.Game.effects.TrueForAll(e=>e.kind!="number"||e.variant==0),"Actual ordinary J damage keeps original unmarked numbers");
        Prepare(app,2);EarnCounter(app);var skill=Foe(app,app.Game.Position+new Point(2,0));Aim(app,skill.Position);Key(app,KeyCode.Q);Check(app.Game.playerAction!=null&&app.Game.playerAction.kind=="skill","Real Q is the original class technique");Frames(app,32);Draw(app);Check(skill.hp<999&&Badges(app)==0,"Real damaging class Q never borrows counter readiness for its own badge");
        Prepare(app);var game=app.Game;if(!game.Has("companion_1"))game.save.flags.Add("companion_1");game.SelectCompanion(1);Check(game.save.companion==1,"Mature safe-travel companion selection is actually accepted before adding foe");var ally=Foe(app,game.Position+new Point(3,0));game.companionPosition=game.Position;Set(game,"companionAttack",0f);game.riposteTime=1.5f;int wait=0;while(ally.hp==999&&wait++<150)Update(app);Draw(app);Check(ally.hp<999&&Badges(app)==0,"Actual companion projectile contact cannot present the player's unused counter as its own");
        Console.WriteLine("PASS genuine Space-earned counter J contact/preparation/miss, original integer and critical damage, real delayed bow/staff contacts after inventory swaps, ordinary J, class Q and autonomous companion exclusions; NATIVE_UNVERIFIED");
    }
    private static Point Offset(FrontierRuntime app,Effect effect,Label numeric){Point p=(Point)Call(app,"ScreenPoint",effect.position);float rise=(1-effect.life/effect.maxLife)*22;return new Point(numeric.r[0]-(p.x-60),numeric.r[1]-(p.y-55-rise));}
    private static void CheckLayout(FrontierRuntime app,bool lanes=true)
    {
        var g=app.Game;var effects=g.effects.FindAll(e=>e.life>0&&(e.kind=="number"||e.kind=="break"||e.kind=="healthGain"||e.kind=="damageTaken"));int count=0;var envelopes=new List<Rect>();
        foreach(Effect e in effects)
        {
            var numeric=Texts(e.text,32);Check(numeric.Count==2,"Every complete numeric/guard/break token survives with exactly one shadow");Label front=numeric[1],shadow=numeric[0];Check(front.size==(g.save.largeText?20:19)&&front.center&&Near(shadow.r[0],front.r[0]+1)&&Near(shadow.r[1],front.r[1]+1),"Original numeric size, centering and one-pixel shadow remain");bool counter=e.kind=="number"&&e.variant==1;float alpha=counter?Math.Min(1,e.life/.2f):1;Color gold=PixelArt.C("#ebc58b"),ink=PixelArt.C("#101a2b");gold.a*=alpha;ink.a*=alpha;Check(Same(ColorOf(front),gold)&&Same(ColorOf(shadow),ink),"Original gold/ink exact, only counter fades in last .2s");Point offset=Offset(app,e,front),p=(Point)Call(app,"ScreenPoint",e.position)+offset;var envelope=new Rect(p.x-60,p.y-(counter?95:77),121,counter?73:55);envelopes.Add(envelope);
            if(counter){var captions=Texts(g.L("反击","COUNTER"),16);Label title=captions.Find(x=>Near(x.r[0],front.r[0])&&Near(x.r[1],front.r[1]-18));Check(title!=null&&title.size==(g.save.largeText?12:11)&&title.center,"Badge has independent bilingual row, full original number below it");Color mint=PixelArt.C("#7acabb");mint.a*=alpha;Check(Same(ColorOf(title),mint)&&title.r[3]-title.padding[2]-title.padding[3]>=title.size,"Caption mint/fade and actual vertical glyph room fit");Check(title.text.Length*title.size<=title.r[2]-title.padding[0]-title.padding[1],"Complete unshortened caption fits conservative full-em glyph width");count++;}
        }
        Check(Badges(app)==count*2,"Only marked damage receipts own bilingual badge/shadow");if(lanes)for(int a=0;a<envelopes.Count;a++)for(int b=a+1;b<envelopes.Count;b++)Check(!Overlap(envelopes[a],envelopes[b]),"Whole badge/number trajectories and shadows never share lanes");
    }
    public static void BilingualDenseLanesFadeAndExpiry(FrontierRuntime app)
    {
        foreach(int width in new[]{960,1280,1920})foreach(bool english in new[]{false,true})foreach(bool large in new[]{false,true})
        {
            Prepare(app);var g=app.Game;g.save.english=english;g.save.largeText=large;g.save.effectsQuality=width==960?0:width==1280?1:2;g.save.reducedMotion=width!=1920;Screen.width=width;Screen.height=width==960?600:width==1280?800:1080;Call(app,"Resize");
            // Explicit mixed feedback stress, distinct from accepted-contact proof.
            for(int i=0;i<8;i++)g.effects.Add(new Effect(i%3==1?"break":"number",g.Position+new Point((i%3)*.1f,(i%2)*.1f),.7f,"fixture-"+i){variant=i%3==0?1:0});Draw(app,"dense-"+(english?"en":"zh")+(large?"-large":"-normal")+"-"+width);CheckLayout(app);var selected=g.effects[3];var initial=Texts(selected.text,32)[1];Point originalOffset=Offset(app,selected,initial);
            g.effects[0].life=.01f;Frames(app,2);Draw(app);CheckLayout(app);Point survivor=Offset(app,selected,Texts(selected.text,32)[1]);Check(Near(survivor.x,originalOffset.x)&&Near(survivor.y,originalOffset.y),"Earlier expiry does not reflow a surviving counter lane");
            int age=0;while(selected.life>.12f&&age++<60)Update(app);Draw(app);CheckLayout(app);Check(selected.life>0&&selected.life<.2f,"Real Update reaches final counter fade interval");for(int n=0;n<60&&g.effects.Count>0;n++)Update(app);Draw(app);Check(Badges(app)==0&&!GUI.commands.Exists(c=>c.Contains("fixture-")),"Real Update removes both final counter rows and all mature stress tokens");Check(((IList)Get<object>(app,"combatTextPlacements")).Count==0,"Expired labels retain no placement/cache history");
        }
        // One isolated counter retains the original numeric anchor and age lift.
        Prepare(app);var single=new Effect("number",app.Game.Position,.7f,"! 42"){variant=1};app.Game.effects.Add(single);Draw(app,"isolated-critical");CheckLayout(app);Point offsetSingle=Offset(app,single,Texts(single.text,32)[1]);Check(Near(offsetSingle.x,0)&&Near(offsetSingle.y,0),"Counter caption does not move its original singleton numeric anchor");
        Point source=(Point)Call(app,"ScreenPoint",single.position),unit=(Point)Call(app,"ScreenPoint",single.position+new Point(1,0));float density=unit.x-source.x;Point desired=new Point(150,165);Set(app,"camera",Get<Point>(app,"camera")+(source-desired)*(1/density));string state=State(app.Game);Draw(app,"caption-only-hud-mirror");var duplicateNumbers=Texts(single.text,32);var duplicateCaptions=Texts(app.Game.L("反击","COUNTER"),16);Check(duplicateNumbers.Count==4&&duplicateCaptions.Count==4,"Camera-carried caption-only HUD overlap repeats complete counter feedback above the actual HUD");Rect number=RectOf(duplicateNumbers[1]),caption=RectOf(duplicateCaptions[1]);var status=new Rect(20,20,260,77);Check(!Overlap(number,status)&&Overlap(caption,status)&&State(app.Game)==state,"The caption alone overlaps status; repeated render is simulation read-only");
        GUI.commands.Clear();GUI.textureCalls.Clear();GUI.buttonCalls.Clear();Call(app,"DrawHudThreatWitness");Check(Texts(single.text,32).Count==2&&Badges(app)==2,"Actual post-HUD witness emits the counter number and caption pair above status");
        Prepare(app);single=new Effect("number",app.Game.Position,.7f,"HUD-new-counter"){variant=1};app.Game.effects.Add(single);source=(Point)Call(app,"ScreenPoint",single.position);Set(app,"camera",Get<Point>(app,"camera")+(source-desired)*(1/density));Draw(app,"new-counter-avoids-hud");var relocated=Texts(single.text,32);Check(relocated.Count==2,"Freshly placed counter avoids HUD without needing mirrored duplication");Point moved=Offset(app,single,relocated[1]),placed=(Point)Call(app,"ScreenPoint",single.position)+moved;Rect envelope=new Rect(placed.x-60,placed.y-95,121,73);Check(!Near(moved.x,0)||!Near(moved.y,0),"Caption-only HUD conflict relocates new counter from original source lane");foreach(Rect region in (List<Rect>)Call(app,"HudRegions"))Check(!Overlap(envelope,region),"Whole fresh counter rise and caption avoids every actual HUD region");
        Prepare(app);Point remote=(Point)Call(app,"WorldAtPointer",new Point(-100,390));single=new Effect("number",remote,.7f,"remote-counter-fixture"){variant=1};app.Game.effects.Add(single);Draw(app,"remote-counter-clipped");var clipped=Texts(single.text,32);Check(clipped.Count==2&&Badges(app)==2,"Remote source still submits its complete clipped pair");Point off=Offset(app,single,clipped[1]);Check(Near(off.x,0)&&Near(off.y,0)&&!Overlap(new Rect(0,0,1280,800),RectOf(clipped[1])),"Packing never pulls an unseen counter contact into the visible canvas");
        Console.WriteLine("PASS bilingual normal/large 960/1280/1920, low/medium/full FX and reduced/full motion, full badge glyph room, mixed guarded/break trajectories, stable lanes, real final-.2 fade/expiry, caption-only HUD mirror/new-lane avoidance and remote clipping; NATIVE_UNVERIFIED");
    }
    private static void Append(StringBuilder text,object value,HashSet<object> seen)
    {
        if(value==null){text.Append("null;");return;}Type type=value.GetType();if(type.IsPrimitive||type.IsEnum||value is string||value is decimal){text.Append(Convert.ToString(value,CultureInfo.InvariantCulture)).Append(';');return;}if(!type.IsValueType&&!seen.Add(value)){text.Append("ref;");return;}var seq=value as IEnumerable;if(seq!=null){foreach(object item in seq)Append(text,item,seen);text.Append("end;");return;}var fields=type.GetFields(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);Array.Sort(fields,(a,b)=>string.CompareOrdinal(a.Name,b.Name));foreach(var f in fields){text.Append(f.Name).Append('=');Append(text,f.GetValue(value),seen);}
    }
    private static string State(GameCore g){var text=new StringBuilder();Append(text,g,new HashSet<object>());return text.ToString();}
    private static string Files(){var text=new StringBuilder();var paths=Directory.GetFiles(Application.persistentDataPath,"*",SearchOption.AllDirectories);Array.Sort(paths,StringComparer.Ordinal);foreach(string path in paths)text.Append(path).Append(':').Append(Convert.ToBase64String(File.ReadAllBytes(path)));return text.ToString();}
    public static void RealPauseSaveDeathTravelAndDrawIsolation(FrontierRuntime app)
    {
        Prepare(app);EarnCounter(app);var g=app.Game;var foe=Foe(app,g.Position+new Point(1.1f,0));Aim(app,foe.Position);Effect e=Contact(app,foe,Attack(app));Draw(app);var art=Get<PixelArt>(app,"art");Call(art,"Glow");Draw(app);int textures=Get<List<Texture2D>>(art,"owned").Count,cache=Get<Dictionary<string,Texture2D>>(art,"cache").Count;string files=Files();float clock=Get<float>(app,"clock");
        var tint=new Color(.2f,.3f,.4f,.5f);var matrix=Matrix4x4.TRS(new Vector3(4,7,0),Quaternion.Euler(0,0,11),new Vector3(1.3f,1.3f,1));foreach(EventType evt in new[]{EventType.Layout,EventType.Repaint}){Event.current.type=evt;GUI.color=tint;GUI.matrix=matrix;GUI.enabled=false;string before=State(g);Draw(app);Check(State(g)==before&&Get<float>(app,"clock")==clock,"Actual Layout/Repaint cannot alter any Core/save field or presentation clock");Check(Same(GUI.color,tint)&&Near(GUI.matrix.m00,matrix.m00)&&Near(GUI.matrix.m03,matrix.m03)&&!GUI.enabled,"OnGUI restores incoming color, matrix and enabled");}
        Check(Get<List<Texture2D>>(art,"owned").Count==textures&&Get<Dictionary<string,Texture2D>>(art,"cache").Count==cache&&Files()==files,"Read-only feedback makes no texture/cache/save-file mutation");GUI.color=Color.white;GUI.matrix=Matrix4x4.identity;GUI.enabled=true;Event.current.type=EventType.Repaint;
        Key(app,KeyCode.I);float life=e.life,total=g.save.totalTime;Frames(app,20);Check(g.mode=="inventory"&&e.life==life&&g.save.totalTime==total,"Actual inventory freezes existing receipt rather than fast-forwarding it");Key(app,KeyCode.Escape);Call(app,"OnApplicationPause",true);life=e.life;Frames(app,20);Check(e.life==life,"Actual application pause callback freezes both rows' shared effect clock");Call(app,"OnApplicationPause",false);app.VisualReviewPaused=true;Frames(app,20);Check(e.life==life,"Review pause freezes the same shared lifetime");app.VisualReviewPaused=false;
        g.PrepareSave();Check(SaveStore.Write(g.save,0),"Production checksum writer accepts exact live combat checkpoint");string warning;var saved=SaveStore.Read(0,out warning);Check(saved!=null&&warning==null,"Production checksum reader restores checkpoint");int coins=saved.coins;g.Load(saved);g.Start();Draw(app);Check(!g.effects.Exists(x=>x.kind=="number"&&x.variant==1)&&Badges(app)==0&&((IList)Get<object>(app,"combatTextPlacements")).Count==0&&g.save.coins==coins,"Load retires transient damage captions/cache while preserving real economy");
        foreach(int version in new[]{1,2}){saved.version=version;File.WriteAllText(SaveStore.PathFor(1),JsonUtility.ToJson(saved));var old=SaveStore.Read(1,out warning);Check(old!=null&&warning==null,"Original raw v1/v2 saves remain readable");g.Load(old);g.Start();Draw(app);Check(g.save.coins==coins&&Badges(app)==0,"Legacy read introduces no counter feedback or currency change");}
        Prepare(app);g=app.Game;g.effects.Add(new Effect("number",g.Position,.7f,"travel-only-fixture"){variant=1});Draw(app);Check(Badges(app)==2,"Explicit old-world receipt exists before actual travel");g.Enter(0);g.Close();Draw(app);Check(Badges(app)==0&&((IList)Get<object>(app,"combatTextPlacements")).Count==0,"Actual world-generation travel retires the old receipt and placement");
        Prepare(app);g=app.Game;g.effects.Add(new Effect("number",g.Position,.7f,"death-only-fixture"){variant=1});g.save.health=1;g.invulnerable=0;Call(g,"Hurt",999f,"environment",(Point?)null);Check(g.Dying,"Accepted real damage begins mature death sequence");Frames(app,70);Draw(app);Check(!g.Dying&&g.save.room==0&&Badges(app)==0,"Actual death/recovery leaves no stale counter caption in town");app.EndVisualReview();Check(((IList)Get<object>(app,"combatTextPlacements")).Count==0&&Get<GameCore>(app,"combatTextGame")==null,"EndVisualReview drops all receipt and GameCore cache references");
        Console.WriteLine("PASS complete Core/save/GUI/clock/texture-cache read-only Layout/Repaint, actual inventory/app/review pause, SaveStore checksum and raw v1/v2, actual travel/death/recovery and review cleanup; NATIVE_UNVERIFIED");
    }
    public static int Main(string[] args)
    {
        string previous=Application.persistentDataPath,directory=Path.Combine(Path.GetTempPath(),"starfall-counter-hit-"+Guid.NewGuid());Application.persistentDataPath=directory;captureFolder=args.Length>0&&args[0]!="-"?args[0]:null;capturePrefix=args.Length>1?args[1]:"qa-counter-hit";int failed=0;var app=new FrontierRuntime();
        try{Directory.CreateDirectory(directory);if(captureFolder!=null)Directory.CreateDirectory(captureFolder);Call(app,"Awake");foreach(Action<FrontierRuntime> test in new Action<FrontierRuntime>[]{GenuineDodgeJAndFlyingSnapshots,BilingualDenseLanesFadeAndExpiry,RealPauseSaveDeathTravelAndDrawIsolation})try{test(app);}catch(Exception error){failed++;Console.WriteLine("FAIL "+test.Method.Name+": "+(error.InnerException??error));}app.EndVisualReview();Call(app,"OnDestroy");}
        finally{Application.persistentDataPath=previous;Input.Clear();GUI.queuedClick=null;GUI.matrix=Matrix4x4.identity;GUI.color=Color.white;GUI.enabled=true;if(Directory.Exists(directory))Directory.Delete(directory,true);}
        Console.WriteLine("Runtime counter-hit: "+(3-failed)+"/3 groups passed; captures="+captures+"; NATIVE_UNVERIFIED");return failed==0?0:1;
    }
}
