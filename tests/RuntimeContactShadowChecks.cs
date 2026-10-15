// Real production actor submissions, OnGUI and GameCore against offline Unity
// declarations. This cannot certify native Editor compilation or GPU playback.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Starfall;
using UnityEngine;

public static class RuntimeContactShadowChecks
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private const string MatureActorDigest="84ae30c1321d4f183b660c276080d1c61a6b8e365646ecf6e98669d5043c9d4e";
    private static readonly Point[] Directions={new Point(1,0),new Point(-1,0),new Point(0,-1),new Point(0,1)};
    private static string captureFolder,capturePrefix;
    private static int captureCount;
    private static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    private static object Call(object target,string method,params object[] args)
    {
        MethodInfo found=target.GetType().GetMethod(method,Private|BindingFlags.Public);
        Check(found!=null,"Production behavior is missing: "+target.GetType().Name+"."+method);
        return found.Invoke(target,args);
    }
    private static T Get<T>(object target,string field){return (T)target.GetType().GetField(field,Private).GetValue(target);}
    private static void Set(object target,string field,object value){target.GetType().GetField(field,Private).SetValue(target,value);}
    private static bool Near(float a,float b){return Math.Abs(a-b)<.001f;}
    private static bool Same(Rect a,Rect b){return Near(a.x,b.x)&&Near(a.y,b.y)&&Near(a.width,b.width)&&Near(a.height,b.height);}
    private static bool Same(Color a,Color b){return Near(a.r,b.r)&&Near(a.g,b.g)&&Near(a.b,b.b)&&Near(a.a,b.a);}
    private static bool Same(Matrix4x4 a,Matrix4x4 b)
    {return Near(a.m00,b.m00)&&Near(a.m01,b.m01)&&Near(a.m03,b.m03)&&Near(a.m10,b.m10)&&Near(a.m11,b.m11)&&Near(a.m13,b.m13);}
    private static void Clear(){GUI.commands.Clear();GUI.textureCalls.Clear();GUI.buttonCalls.Clear();}
    private static bool Shadow(GUI.TextureCall c){return c.texture.name=="contact_shadow";}
    private static List<GUI.TextureCall> Shadows(){return GUI.textureCalls.FindAll(Shadow);}
    private static int ContactsAt(Point p,float width,float height,float top,Color tint)
    {return GUI.textureCalls.FindAll(c=>Shadow(c)&&Same(c.rect,new Rect(p.x-width/2,p.y+top,width,height))&&Same(c.color,tint)).Count;}
    private static Color Tint(float alpha){return new Color(.025f,.04f,.07f,alpha);}
    private static Point Foot(FrontierRuntime app,Point p){return (Point)Call(app,"ScreenPoint",p);}
    private static Dictionary<string,Texture2D> Cache(PixelArt art){return Get<Dictionary<string,Texture2D>>(art,"cache");}
    private static List<Texture2D> Owned(PixelArt art){return Get<List<Texture2D>>(art,"owned");}
    private static Texture2D Contact(PixelArt art){return (Texture2D)Call(art,"ContactShadow");}
    private static void Prepare(FrontierRuntime app,int room=1,int profession=0,bool encounter=false)
    {
        Input.Clear();GUI.queuedClick=null;GUI.matrix=Matrix4x4.identity;GUI.color=Color.white;GUI.enabled=true;
        Screen.width=1280;Screen.height=800;Time.unscaledDeltaTime=.02f;Event.current.type=EventType.Repaint;
        app.BeginVisualReview(profession,room,encounter);app.VisualReviewPaused=false;var g=app.Game;
        g.save.companion=-1;g.save.reducedMotion=true;g.save.effectsQuality=0;g.save.showGuidance=false;g.save.english=true;
        g.save.difficulty=1;g.save.talents[1]=0;g.save.relics=new[]{-1,-1};g.save.foodUntil=0;
        g.hazards.Clear();g.projectiles.Clear();g.effects.Clear();g.fallen.Clear();g.toastTime=0;g.invulnerable=0;
        Set(app,"camera",g.Position);Set(app,"clock",0f);Set(app,"combatHudTime",0f);Set(app,"pointerSeen",false);
        Set(app,"controllerUsed",false);Set(app,"applicationPaused",false);Set(app,"drawingOutline",false);Call(app,"Resize");
    }
    private static void Draw(FrontierRuntime app,string name=null)
    {
        Clear();Call(app,"OnGUI");
        if(captureFolder!=null&&name!=null)
        {
            var commands=GUI.commands.ToArray();var textures=GUI.textureCalls.ToArray();var buttons=GUI.buttonCalls.ToArray();
            GUI.Export(captureFolder,capturePrefix+"-"+name);captureCount++;
            GUI.commands.AddRange(commands);GUI.textureCalls.AddRange(textures);GUI.buttonCalls.AddRange(buttons);
        }
    }
    private static void Stamp(Point foot,float width,float height,float top,Color tint,string reason)
    {
        var shadows=Shadows();Check(shadows.Count==1,reason+": exactly one ellipse is submitted for the live actor");
        var c=shadows[0];Check(Same(c.rect,new Rect(foot.x-width/2,foot.y+top,width,height)),reason+": the original foot-relative footprint stays exact");
        Check(Same(c.uv,new Rect(0,0,1,1))&&Same(c.color,tint),reason+": whole ellipse and original tint/opacity");
        Check(GUI.textureCalls.IndexOf(c)==0,reason+": ground contact is drawn beneath the existing actor art");
    }
    private static void CachedEllipseAndOwnership(FrontierRuntime app)
    {
        var awake=Get<PixelArt>(app,"art");Texture2D t=Contact(awake);
        Check(t!=null&&Cache(awake).ContainsKey("contact_shadow")&&Owned(awake).Contains(t),"Awake prewarms the owned contact ellipse before any OnGUI");
        Check(t.width==64&&t.height==16&&t.filterMode==FilterMode.Point&&t.wrapMode==TextureWrapMode.Clamp&&t.mipmapCount==1,
            "The reusable RGBA stamp retains 64x16 Point/Clamp sampling without mipmaps");
        Check(t.pixels!=null&&t.pixels.Length==1024,"Offline texture upload captures the complete RGBA ellipse");
        int painted=0,previous=0;var widths=new int[16];var levels=new HashSet<float>();
        for(int y=0;y<16;y++)
        {
            bool entered=false,left=false;int count=0;
            for(int x=0;x<64;x++)
            {
                Color pixel=t.pixels[y*64+x];Check(pixel.a>=0&&pixel.a<=1,"Pixel ellipse alpha stays bounded");
                Check(Near(pixel.a,t.pixels[y*64+63-x].a)&&Near(pixel.a,t.pixels[(15-y)*64+x].a),"Ellipse alpha stays symmetric about both foot axes");
                if(pixel.a>0){Check(!left,"Every ellipse scanline is one connected span");entered=true;count++;painted++;levels.Add(pixel.a);Check(Near(pixel.r,1)&&Near(pixel.g,1)&&Near(pixel.b,1),"Tint-neutral visible pixels preserve actor color");}
                else if(entered)left=true;
                if(x<32&&x>0)Check(pixel.a>=t.pixels[y*64+x-1].a,"Pixel alpha rises monotonically toward the horizontal center");
                if(y<8&&y>0)Check(pixel.a>=t.pixels[(y-1)*64+x].a,"Pixel alpha rises monotonically toward the vertical center");
                if((x<4||x>59)&&(y<3||y>12))Check(Near(pixel.a,0),"The four rectangular corners are transparent");
                float nx=(x+.5f-32)/32,ny=(y+.5f-8)/8;
                if(nx*nx+ny*ny>1.02f)Check(Near(pixel.a,0),"No painted pixel extends outside the normalized ellipse");
            }
            widths[y]=count;if(y<8){Check(count>=previous,"Ellipse widens smoothly toward its center");previous=count;}
        }
        Check(painted>550&&painted<850&&levels.Count>=3&&levels.Count<=8&&widths[0]<widths[7]&&widths[7]>=56&&t.pixels[8*64+32].a==1,
            "Coverage is a broad centered ellipse rather than a box, diamond or thin line");
        int cache=Cache(awake).Count,owned=Owned(awake).Count;
        for(int i=0;i<40;i++){Call(awake,"PrewarmContactShadow");Check(ReferenceEquals(Contact(awake),t),"Prewarm and lookup reuse the same stamp");}
        Check(Cache(awake).Count==cache&&Owned(awake).Count==owned&&t.pixelReads==0,"Repeated prewarm/lookup performs no CPU readback or owned texture creation");
        var fresh=new PixelArt();Check(Contact(fresh)==null&&Cache(fresh).Count==0&&Owned(fresh).Count==0,"Cold lookup cannot allocate lazily during a draw");
        var source=Get<Texture2D[]>(app,"playerSheets")[0];var borrowed=fresh.FlashMask(source);
        Check(borrowed.name.StartsWith("resource:"),"Lifecycle fixture actually borrows the shipped prepared mask");
        Call(fresh,"PrewarmContactShadow");Texture2D first=Contact(fresh);Check(Owned(fresh).Count==1,"Prewarm owns only one additional stamp");
        var synthetic=new Texture2D(2,2,TextureFormat.RGBA32,false){name="contact-check-readable",pixels=new[]{Color.white,Color.white,Color.white,Color.white}};
        Texture2D generated=fresh.FlashMask(synthetic);Check(Owned(fresh).Contains(generated),"Lifecycle fixture includes an owned generated mask as well as a borrowed resource");
        fresh.Dispose();fresh.Dispose();Check(first.destroyed&&generated.destroyed&&!borrowed.destroyed&&!source.destroyed&&!synthetic.destroyed,
            "Dispose destroys owned stamps/masks and preserves borrowed authored textures");
        Check(Contact(fresh)==null&&Cache(fresh).Count==0&&Owned(fresh).Count==0,"Disposed lookup never returns a destroyed cached reference");
        Call(fresh,"PrewarmContactShadow");Check(Contact(fresh)!=null&&!ReferenceEquals(Contact(fresh),first)&&!Contact(fresh).destroyed,"Explicit re-prewarm rebuilds a live owned stamp after disposal");fresh.Dispose();
        Console.WriteLine("PASS centered pixel ellipse, Awake preload, cold lookup, repeated cache reuse, owned/borrowed disposal and explicit re-prewarm; NATIVE_UNVERIFIED");
    }
    // Frozen before the change against immutable 95eaa62. Exclude only the old
    // ground primitive/new stamp, then retain texture identity, full UV, density,
    // anchor, tint, matrix, draw order and cropped companion identity pieces.
    private static bool Ground(GUI.TextureCall c)
    {return Shadow(c)||c.texture==Texture2D.whiteTexture&&(Same(c.color,Tint(c.color.a))||Same(c.color,new Color(.04f,.1f,.15f,.3f)));}
    private static void N(StringBuilder text,float n){text.Append(n.ToString("R",CultureInfo.InvariantCulture)).Append(',');}
    private static void Record(StringBuilder text,string name)
    {
        text.Append(name).Append(':');
        foreach(var c in GUI.textureCalls)
        {
            if(Ground(c))continue;text.Append(c.texture.name).Append('|');
            foreach(float n in new[]{c.rect.x,c.rect.y,c.rect.width,c.rect.height,c.uv.x,c.uv.y,c.uv.width,c.uv.height,c.color.r,c.color.g,c.color.b,c.color.a,c.matrix.m00,c.matrix.m01,c.matrix.m03,c.matrix.m10,c.matrix.m11,c.matrix.m13})N(text,n);
        }
        text.AppendLine();
    }
    private static string Digest(string text)
    {using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-","").ToLowerInvariant();}
    private static void HeroState(GameCore g,int state,Point direction)
    {
        g.aim=direction;g.locomotionFacing=direction;g.movementDirection=new Point();g.playerAction=null;g.playerHitTime=g.playerFlash=g.deathTime=g.dashTime=0;
        if(state==1){g.movementDirection=direction*.1f;g.walkDistance=.8f;}
        if(state==2||state==3)g.playerAction=new ActorAction{kind="attack",weapon=g.save.weapon,profession=g.save.profession,direction=direction,duration=.7f,contact=.3f,elapsed=state==2?.1f:.31f,released=state==3};
        if(state==4){g.playerHitTime=.12f;g.playerFlash=.06f;}
        if(state==5){g.dashTime=.1f;Set(g,"dashDirection",direction);g.invulnerable=.2f;}
        if(state==6){g.deathTime=.7f;g.invulnerable=0;}
        if(state==7){g.playerAction=new ActorAction{kind="skill",weapon=g.save.profession,profession=g.save.profession,direction=direction,duration=.9f,contact=.4f,elapsed=.41f,released=true};}
    }
    private static void EnemyState(Enemy e,int state,Point direction)
    {
        e.state=0;e.moving=false;e.hitTime=e.flash=e.releaseTime=0;e.Face(direction);e.moveTime=.9f;
        if(state==1)e.moving=true;if(state==2){e.state=1;e.timer=.4f;e.windupDuration=.7f;}
        if(state==3)e.releaseTime=.2f;if(state==4){e.hitTime=.12f;e.flash=.06f;}if(state==5){e.moving=true;e.backpedaling=true;}
    }
    private static void MatureActorsAndFootprints(FrontierRuntime app)
    {
        ActorFixtures(app,true);
        for(int profession=0;profession<3;profession++)
        {
            Prepare(app,6,profession);var g=app.Game;g.enemies.Clear();g.racing=true;g.raceTime=7;
            Point foot=Foot(app,g.Position);string state=State(g);
            Clear();Call(app,"DrawPlayerActor");Stamp(foot,34,7,-3,Tint(.38f),"racing hero");
            Check(Skiffs()==1&&State(g)==state,"Normal racing retains one original skiff without advancing the race");
            var sheets=Get<Texture2D[]>(app,"playerSheets");int look=g.AppearanceWeapon;Texture2D saved=sheets[look];sheets[look]=null;
            try
            {
                Clear();Call(app,"DrawPlayerActor");Stamp(foot,30,7,-1,new Color(.04f,.1f,.15f,.3f),"racing legacy fallback");
                Check(Skiffs()==1&&State(g)==state,"A legacy fallback chooses its own skiff once instead of layering both presenter paths");
            }
            finally{sheets[look]=saved;}
        }
        Console.WriteLine("PASS three-class racing and missing-sheet fallback: one original boat and one contact footprint, unchanged race/save state; NATIVE_UNVERIFIED");
    }
    private static int Skiffs()
    {return GUI.textureCalls.FindAll(c=>c.texture.name=="resource:Frontier/Art/FieldProps"&&Same(c.uv,new Rect(.5f,.25f,.25f,.25f))).Count;}
    private static void ActorFixtures(FrontierRuntime app,bool validate)
    {
        var evidence=new StringBuilder();string failure=null;int fixtures=0;
        Action<Point,float,float,float,Color,string> verify=(p,w,h,o,t,name)=>{fixtures++;Record(evidence,name);if(validate&&failure==null)try{Stamp(p,w,h,o,t,name);}catch(Exception ex){failure=ex.Message;}};
        for(int profession=0;profession<3;profession++)for(int d=0;d<4;d++)
        {
            Prepare(app,1,profession);app.Game.enemies.Clear();
            for(int state=0;state<8;state++)
            {
                HeroState(app.Game,state,Directions[d]);Clear();Call(app,"DrawPlayerActor");verify(Foot(app,app.Game.Position),34,7,-3,Tint(.38f),"hero-"+profession+"-"+d+"-"+state);
            }
        }
        for(int kind=0;kind<3;kind++)for(int d=0;d<4;d++)
        {
            Prepare(app);var e=new Enemy(9000+kind,kind,app.Game.Position.x+2,app.Game.Position.y,100);app.Game.enemies.Clear();app.Game.enemies.Add(e);
            for(int state=0;state<6;state++)
            {
                EnemyState(e,state,Directions[d]);Clear();Call(app,"DrawEnemyActor",e);verify(Foot(app,e.Position),(kind==2?106:96)*.44f,7,-3,Tint(.35f),"foe-"+kind+"-"+d+"-"+state);
                Set(app,"drawingOutline",true);Clear();Call(app,"DrawEnemyActor",e);Record(evidence,"foe-outline-"+kind+"-"+d+"-"+state);if(validate)Check(Shadows().Count==0,"HUD outline has no additional enemy ground shadow");Set(app,"drawingOutline",false);
            }
            var fallen=new FallenActor{actor=e,room=1,life=.4f,duration=.65f};Clear();Call(app,"DrawFallen",fallen);Record(evidence,"fallen-"+kind+"-"+d);if(validate)Check(Shadows().Count==0,"Authored fallen poses receive no new ground stamp");
        }
        for(int room=1;room<=8;room++)for(int d=0;d<4;d++)
        {
            Prepare(app,room,0,true);Enemy boss=app.Game.enemies.Find(e=>e.boss);Check(boss!=null,"All eight actual mature encounter spawns exist");
            for(int state=0;state<6;state++)
            {
                EnemyState(boss,state,Directions[d]);Clear();Call(app,"DrawEnemyActor",boss);verify(Foot(app,boss.Position),(room==5?264:240)*.44f,7,-3,Tint(.35f),"boss-"+room+"-"+d+"-"+state);
            }
            Clear();Call(app,"DrawFallen",new FallenActor{actor=boss,room=room,life=.9f,duration=1.4f});Record(evidence,"boss-fallen-"+room+"-"+d);if(validate)Check(Shadows().Count==0,"Each authored fallen boss remains unchanged without a new stamp");
        }
        for(int id=0;id<3;id++)for(int d=0;d<4;d++)
        {
            Prepare(app);var g=app.Game;g.save.companion=id;g.companionPosition=g.Position+new Point(-2,.5f);g.companionAim=Directions[d];
            for(int state=0;state<5;state++)
            {
                g.companionMoving=state==1;g.companionMoveTime=.9f;g.companionAction=state<2?null:new ActorAction{kind=state==4?"command":"attack",direction=Directions[d],released=state>=3};
                Clear();Call(app,"DrawCompanionActor");verify(Foot(app,g.companionPosition),28,6,-3,Tint(.32f),"companion-"+id+"-"+d+"-"+state);
            }
        }
        Prepare(app,0);string[] npcs={"guild","garden","forge","cook","rest","fish","scout","returnscout","mentor","envoy","captain","tinker","keeper","council","castaway0","castaway1","innGuest","other"};
        foreach(string kind in npcs)
        {Point p=app.Game.Position+new Point(2,1);Clear();Call(app,"DrawNpc",kind,p,-1);verify(Foot(app,p),26,5,-2,Tint(.32f),"npc-"+kind);}
        app.Game.save.companion=0;var sheets=Get<Texture2D[]>(app,"companionSheets");Texture2D previous=sheets[0];sheets[0]=null;
        try{Clear();Call(app,"DrawCompanionActor");verify(Foot(app,app.Game.companionPosition),26,5,-2,Tint(.32f),"companion-fallback");}finally{sheets[0]=previous;}
        var heroes=Get<Texture2D[]>(app,"playerSheets");int look=app.Game.AppearanceWeapon;previous=heroes[look];heroes[look]=null;
        try{Clear();Call(app,"DrawPlayerActor");verify(Foot(app,app.Game.Position),30,7,-1,new Color(.04f,.1f,.15f,.3f),"legacy-fallback");}finally{heroes[look]=previous;}
        string digest=Digest(evidence.ToString());Console.WriteLine("Actor preservation witness: "+fixtures+" footprints; sha256="+digest);
        if(validate){Check(digest==MatureActorDigest,"Non-shadow atlas/UV/frame/density/anchor/flash/outline submissions differ from immutable 95eaa62");Check(failure==null,failure);}
        Console.WriteLine("PASS mature actor submission witness: three hero classes, three ordinary kinds, eight bosses, four directions, action/hit/death/walk states, all companions/NPCs and fallbacks; NATIVE_UNVERIFIED");
    }
    private static void AppendState(StringBuilder text,object value,HashSet<object> seen)
    {
        if(value==null){text.Append("null;");return;}Type type=value.GetType();
        if(type.IsPrimitive||type.IsEnum||value is string||value is decimal){text.Append(Convert.ToString(value,CultureInfo.InvariantCulture)).Append(';');return;}
        if(!type.IsValueType&&!seen.Add(value)){text.Append("ref;");return;}
        var list=value as System.Collections.IEnumerable;if(list!=null){foreach(object item in list)AppendState(text,item,seen);text.Append("end;");return;}
        var fields=type.GetFields(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);Array.Sort(fields,(a,b)=>string.CompareOrdinal(a.Name,b.Name));
        foreach(var field in fields){text.Append(field.Name).Append('=');AppendState(text,field.GetValue(value),seen);}
    }
    private static string State(GameCore g){var text=new StringBuilder();AppendState(text,g,new HashSet<object>());return text.ToString();}
    private static string Files()
    {
        var text=new StringBuilder();var files=Directory.GetFiles(Application.persistentDataPath,"*",SearchOption.AllDirectories);Array.Sort(files,StringComparer.Ordinal);
        foreach(string path in files)text.Append(path).Append(':').Append(Convert.ToBase64String(File.ReadAllBytes(path))).Append(';');return text.ToString();
    }
    private static void Place(FrontierRuntime app,Point world,Point pixel){Set(app,"camera",world-new Point((pixel.x-640)/36,(pixel.y-390)/36));}
    private static void GuiIsolationAndHud(FrontierRuntime app)
    {
        Prepare(app);var g=app.Game;g.enemies.Clear();var e=new Enemy(95001,2,g.Position.x+.5f,g.Position.y,100){state=1,timer=.4f,windupDuration=.7f,target=g.Position};g.enemies.Add(e);
        Place(app,g.Position,new Point(620,754));Set(app,"combatHudTime",3f);g.Attack(new Point(1,0));
        var art=Get<PixelArt>(app,"art");Call(art,"Glow");Draw(app); // Mature optional glow/world primitives are outside this cache contract.
        string before=State(g),files=Files();int owned=Owned(art).Count;Texture2D stamp=Contact(art);
        var matrix=Matrix4x4.TRS(new Vector3(19,31,0),Quaternion.Euler(0,0,17),new Vector3(1.4f,1.4f,1));var color=new Color(.3f,.5f,.7f,.8f);
        foreach(EventType evt in new[]{EventType.Layout,EventType.Repaint,EventType.Repaint,EventType.Layout,EventType.Repaint})
        {
            Event.current.type=evt;GUI.matrix=matrix;GUI.color=color;GUI.enabled=false;Draw(app);
            Check(State(g)==before&&ReferenceEquals(app.Game,g),"Actual Layout/Repaint leaves the complete GameCore and nested save/encounter state unchanged");
            Check(Same(GUI.matrix,matrix)&&Same(GUI.color,color)&&!GUI.enabled,"OnGUI restores caller matrix/color/enabled state");
            Check(ContactsAt(Foot(app,g.Position),34,7,-3,Tint(.38f))==1&&ContactsAt(Foot(app,e.Position),106*.44f,7,-3,Tint(.35f))==1,
                "HUD-overlapping player/heavy each draw ground contact once despite their foreground outline witness");
            Check(GUI.textureCalls.Exists(c=>c.texture.name.Contains("/ActorMasks/outline_")),"Isolation fixture actually submits a HUD actor outline");
            int panel=GUI.textureCalls.FindIndex(c=>c.texture==Texture2D.whiteTexture&&Same(c.rect,new Rect(426,730,428,54)));
            Check(panel>=0&&GUI.textureCalls.FindLastIndex(Shadow)<panel,"Ground contact stays under the hotbar; foreground outlines never duplicate it");
        }
        GUI.matrix=matrix;GUI.color=color;Clear();Call(app,"DrawContactShadow",new Point(45,60),30f,7f,-1f,new Color(.04f,.1f,.15f,.3f));
        Stamp(new Point(45,60),30,7,-1,new Color(.04f,.1f,.15f,.3f),"direct helper");Check(Same(GUI.matrix,matrix)&&Same(GUI.color,color),"Direct contact helper restores incoming GUI color and affine matrix");
        Set(app,"drawingOutline",true);Clear();Call(app,"DrawContactShadow",new Point(45,60),30f,7f,-1f,Tint(.3f));Check(GUI.textureCalls.Count==0,"Direct outline pass suppresses every ground stamp");Set(app,"drawingOutline",false);
        foreach(bool reduced in new[]{false,true})foreach(int fx in new[]{0,2})foreach(string mode in new[]{"world","pause"})
        {
            g.save.reducedMotion=reduced;g.save.effectsQuality=fx;g.mode=mode;string snapshot=State(g);Draw(app);
            Check(ContactsAt(Foot(app,g.Position),34,7,-3,Tint(.38f))==1&&ContactsAt(Foot(app,e.Position),106*.44f,7,-3,Tint(.35f))==1&&Shadows().TrueForAll(c=>ReferenceEquals(c.texture,stamp)),
                "Low/high FX, reduced motion and paused world retain the same cached actor contacts");
            Check(State(g)==snapshot,"Presentation policy changes do not advance simulation through OnGUI");
        }
        Check(Owned(art).Count==owned&&ReferenceEquals(Contact(art),stamp),"Repeated actual OnGUI never creates or replaces a contact texture");
        Check((bool)Call(app,"Save")&&Files()==files,"Visual review Save and all drawing leave existing save/backup/slot fixture bytes unchanged");
        GUI.matrix=Matrix4x4.identity;GUI.color=Color.white;GUI.enabled=true;Event.current.type=EventType.Repaint;
        Console.WriteLine("PASS complete GameCore/save and GUI-state isolation, actual HUD outline without duplicate ground, low/high FX/reduced motion/pause and read-only save boundary; NATIVE_UNVERIFIED");
    }
    private static void HeroContact(FrontierRuntime app,string reason)
    {Clear();Call(app,"DrawPlayerActor");Stamp(Foot(app,app.Game.Position),34,7,-3,Tint(.38f),reason);}
    private static void RealCombatContactsHitDeathAndDodge(FrontierRuntime app)
    {
        int contacts=0;
        foreach(bool render in new[]{false,true})foreach(int scenario in new[]{0,1,2})
        {
            Prepare(app);var g=app.Game;g.enemies.Clear();Point target=g.Position;
            var e=new Enemy(97001,2,target.x+.5f,target.y,100){state=1,timer=.01f,windupDuration=.65f,target=target,facing=new Point(-1,0)};g.enemies.Add(e);
            if(scenario==1)g.save.y=target.y+1.6f;if(scenario==2)g.Dash(new Point(0,1));float health=g.save.health;
            if(render){string state=State(g);Draw(app);Check(State(g)==state,"Rendering a committed heavy tell never resolves damage or advances the timer");
                Check(ContactsAt(Foot(app,g.Position),34,7,-3,Tint(.38f))==1&&ContactsAt(Foot(app,e.Position),106*.44f,7,-3,Tint(.35f))==1,"Heavy and player remain in ground contact during warning/dodge");}
            g.Tick(.02f,new Point());Check(e.state==0&&e.releaseTime>0,"Real Core releases the committed heavy warning at expiry");
            Check(Near(health-g.save.health,scenario==0?14:0),"Actual heavy damage retains inside/outside/dodge outcomes independently of rendering");
            Check(g.effects.Exists(f=>f.kind=="danger"&&Near(f.radius,GameCore.EnemyAreaRadius(e))),"Released attack effect preserves the authoritative ground radius");
            if(render){HeroContact(app,"heavy release player foot");Clear();Call(app,"DrawEnemyActor",e);Stamp(Foot(app,e.Position),106*.44f,7,-3,Tint(.35f),"heavy release foot");}contacts++;
        }
        for(int profession=0;profession<3;profession++)
        {
            Prepare(app,1,profession);var g=app.Game;g.enemies.Clear();g.save.x=12;g.save.y=14;Set(app,"camera",g.Position);
            g.Attack(new Point(1,0));ActorAction action=g.playerAction;Check(action!=null&&!action.released,"Production basic action starts its existing windup");HeroContact(app,"real basic windup");
            for(int frame=0;frame<60&&!action.released;frame++){g.Tick(.02f,new Point());HeroContact(app,"real contact transition");}
            Check(action.released&&g.PlayerPhase==ActionPhase.Active,"Real action timing reaches its visible contact without drawing changing release");
            Prepare(app,1,profession);g=app.Game;g.enemies.Clear();g.save.x=12;g.save.y=14;Set(app,"camera",g.Position);g.Dash(new Point(1,0));
            Check(g.dashTime>0&&g.invulnerable>0,"Production Dash establishes real movement and invulnerability");
            for(int frame=0;frame<20&&g.dashTime>0;frame++){g.Tick(.02f,new Point());HeroContact(app,"real moving dodge");}
            Check(g.dashTime<=0&&g.ActuallyMoving&&g.PlayerPose==8,"Final actual moving dodge keeps its captured ground footprint and pose");
            g.Tick(.02f,new Point());g.invulnerable=0;g.projectiles.Add(new Projectile{position=g.Position,damage=1,velocity=new Point(.01f,0),visual="enemyOrb",source="shot"});g.Tick(.02f,new Point());
            Check(g.playerHitTime>0&&g.playerFlash>0&&!g.Dying,"Actual hostile projectile enters hit/flash reaction");HeroContact(app,"real player hit");
            for(int frame=0;frame<25;frame++)g.Tick(.02f,new Point());g.invulnerable=0;g.save.health=1;
            g.projectiles.Add(new Projectile{position=g.Position,damage=999,velocity=new Point(.01f,0),visual="enemyOrb",source="shot"});g.Tick(.02f,new Point());
            Check(g.Dying,"Actual lethal projectile enters the authored player death");HeroContact(app,"real player death");
        }
        Prepare(app);var core=app.Game;core.enemies.Clear();core.save.x=12;core.save.y=14;core.save.weapon=0;
        var foe=new Enemy(98001,0,13,14,10);core.enemies.Add(foe);core.Attack(new Point(1,0));
        for(int i=0;i<60&&core.enemies.Contains(foe);i++)core.Tick(.02f,new Point());
        Check(!core.enemies.Contains(foe)&&core.fallen.Exists(f=>ReferenceEquals(f.actor,foe)),"Actual basic damage removes the dead foe into its existing fallen lifetime");
        Clear();Call(app,"DrawFallen",core.fallen.Find(f=>ReferenceEquals(f.actor,foe)));Check(Shadows().Count==0,"Real defeated foe gains no new fallen ground shadow");
        Check(contacts==6,"Real heavy expiry covers rendered/unrendered hit/miss/dodge pairs");
        Console.WriteLine("PASS real Core basic contacts, three-class moving/final dodge, hostile hit/flash/death, actual foe defeat/fallen and six warning expiry outcomes; NATIVE_UNVERIFIED");
    }
    private static void CaptureScenes(FrontierRuntime app)
    {
        for(int room=0;room<=8;room++)
        {
            Prepare(app,room,room%3,room>0);Enemy boss=app.Game.enemies.Find(e=>e.boss);
            if(boss!=null){app.Game.enemies.RemoveAll(e=>e!=boss);Set(app,"camera",boss.Position);boss.state=1;boss.timer=.4f;boss.windupDuration=.8f;boss.target=boss.Position;}
            Draw(app,"room-"+room);
        }
        for(int kind=0;kind<3;kind++)
        {
            Prepare(app);var g=app.Game;g.enemies.Clear();var e=new Enemy(99000+kind,kind,g.Position.x+2,g.Position.y,100){moving=true,moveTime=.8f};g.enemies.Add(e);Draw(app,"foe-walk-"+kind);
        }
        for(int profession=0;profession<3;profession++)foreach(int state in new[]{1,4})
        {Prepare(app,1,profession);app.Game.enemies.Clear();HeroState(app.Game,state,new Point(0,-1));Draw(app,"hero-"+profession+"-"+(state==1?"walk":"hit"));}
        for(int id=0;id<3;id++)
        {Prepare(app,0);app.Game.save.companion=id;app.Game.companionMoving=true;app.Game.companionMoveTime=.8f;app.Game.companionAim=new Point(0,1);Draw(app,"companion-"+id);}
        Prepare(app);app.Game.enemies.Clear();var heavy=new Enemy(99901,2,app.Game.Position.x+.5f,app.Game.Position.y,100);app.Game.enemies.Add(heavy);Place(app,app.Game.Position,new Point(620,754));Set(app,"combatHudTime",3f);Draw(app,"hud-foot-overlap");
        Prepare(app);app.Game.save.effectsQuality=0;app.Game.save.reducedMotion=true;app.Game.mode="pause";Draw(app,"paused-lowfx");
        Prepare(app);var heroes=Get<Texture2D[]>(app,"playerSheets");int look=app.Game.AppearanceWeapon;var previous=heroes[look];heroes[look]=null;try{Draw(app,"legacy-fallback");}finally{heroes[look]=previous;}
    }
    public static int Main(string[] args)
    {
        string previous=Application.persistentDataPath,directory=Path.Combine(Path.GetTempPath(),"starfall-contact-shadow-"+Guid.NewGuid());
        Application.persistentDataPath=directory;captureFolder=args.Length>0&&args[0]!="-"?args[0]:null;capturePrefix=args.Length>1?args[1]:"qa-contact";
        bool only=args.Length>2&&args[2]=="capture-only";var app=new FrontierRuntime();int failed=0;
        try
        {
            Directory.CreateDirectory(directory);File.WriteAllText(Path.Combine(directory,"starfall-v1.json"),"{\"version\":1,\"profession\":0}");
            File.WriteAllText(Path.Combine(directory,"starfall-slot-1.json.bak"),"existing-slot-backup-sentinel");Call(app,"Awake");Check(app.Game!=null,"Production Awake loads the shipped campaign");
            if(only)ActorFixtures(app,false);
            else foreach(Action<FrontierRuntime> test in new Action<FrontierRuntime>[]{CachedEllipseAndOwnership,MatureActorsAndFootprints,GuiIsolationAndHud,RealCombatContactsHitDeathAndDodge})
            {try{test(app);}catch(Exception ex){failed++;Console.WriteLine("FAIL "+test.Method.Name+": "+(ex.InnerException??ex).Message);}}
            if(captureFolder!=null){CaptureScenes(app);Check(captureCount==24,"Exactly 24 representative actual OnGUI records are captured");Console.WriteLine("Captured "+captureCount+" production OnGUI fixtures: "+capturePrefix+"-*");}
            app.EndVisualReview();Call(app,"OnDestroy");
        }
        finally{Application.persistentDataPath=previous;Input.Clear();GUI.queuedClick=null;GUI.matrix=Matrix4x4.identity;GUI.color=Color.white;GUI.enabled=true;Event.current.type=EventType.Repaint;if(Directory.Exists(directory))Directory.Delete(directory,true);}
        if(only){Console.WriteLine("Capture-only: semantic checks skipped; NATIVE_UNVERIFIED");return 0;}
        Console.WriteLine("Runtime contact shadows: "+(4-failed)+"/4 groups passed; NATIVE_UNVERIFIED");return failed==0?0:1;
    }
}
