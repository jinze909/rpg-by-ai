// Actual production OnGUI records and GameCore contacts against offline Unity
// declarations. This is not native Editor compilation, GPU playback or a playtest.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using Starfall;
using UnityEngine;

public static class RuntimeThreatContourChecks
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly Color Shadow = new Color(.025f,.045f,.06f,.95f);
    private static readonly Color EnemyInk = PixelArt.C("#df8a68");
    private static string captureFolder;
    private static void Check(bool value,string message){if(!value)throw new Exception(message);}
    private static object Call(object app,string name,params object[] args)
    {return app.GetType().GetMethod(name,Private).Invoke(app,args);}
    private static void Set(object app,string name,object value)
    {app.GetType().GetField(name,Private).SetValue(app,value);}
    private static bool Near(float a,float b,float tolerance=.035f){return Math.Abs(a-b)<tolerance;}
    private static bool Same(Color a,Color b)
    {return Near(a.r,b.r,.0001f)&&Near(a.g,b.g,.0001f)&&Near(a.b,b.b,.0001f)&&Near(a.a,b.a,.0001f);}
    private static bool Same(Rect a,Rect b)
    {return Near(a.x,b.x)&&Near(a.y,b.y)&&Near(a.width,b.width)&&Near(a.height,b.height);}
    private static bool Same(Matrix4x4 a,Matrix4x4 b)
    {return Near(a.m00,b.m00,.0001f)&&Near(a.m01,b.m01,.0001f)&&Near(a.m03,b.m03,.0001f)
        &&Near(a.m10,b.m10,.0001f)&&Near(a.m11,b.m11,.0001f)&&Near(a.m13,b.m13,.0001f);}
    private static void Clear(){GUI.commands.Clear();GUI.textureCalls.Clear();GUI.buttonCalls.Clear();}
    private static void Draw(FrontierRuntime app,string capture=null)
    {
        Clear();Call(app,"OnGUI");
        if(captureFolder!=null&&capture!=null)
        {
            var commands=GUI.commands.ToArray();var textures=GUI.textureCalls.ToArray();var buttons=GUI.buttonCalls.ToArray();
            GUI.Export(captureFolder,"qa-threat-"+capture);
            GUI.commands.AddRange(commands);GUI.textureCalls.AddRange(textures);GUI.buttonCalls.AddRange(buttons);
        }
    }
    private static void Prepare(FrontierRuntime app,int room=1,bool encounter=false)
    {
        Input.Clear();GUI.queuedClick=null;GUI.matrix=Matrix4x4.identity;GUI.color=Color.white;GUI.enabled=true;
        Screen.width=1280;Screen.height=800;Time.unscaledDeltaTime=.02f;Event.current.type=EventType.Repaint;
        app.BeginVisualReview(0,room,encounter);var g=app.Game;
        g.save.companion=-1;g.save.difficulty=1;g.save.talents[1]=0;g.save.foodUntil=0;
        g.save.relics=new[]{-1,-1};g.save.reducedMotion=true;g.save.effectsQuality=0;g.save.showGuidance=false;
        g.hazards.Clear();g.projectiles.Clear();g.effects.Clear();g.toastTime=0;g.invulnerable=0;
        Set(app,"camera",g.Position);Set(app,"clock",0f);Set(app,"combatHudTime",0f);
        Set(app,"pointerSeen",false);Set(app,"controllerUsed",false);Call(app,"Resize");
    }
    private static Enemy Warning(FrontierRuntime app,int kind=2)
    {
        var g=app.Game;g.enemies.Clear();var e=new Enemy(99001,kind,g.Position.x+2,g.Position.y,100)
        {state=1,timer=.4f,windupDuration=.65f,target=g.Position,facing=new Point(-1,0)};
        g.enemies.Add(e);return e;
    }
    private static void PlaceOnScreen(FrontierRuntime app,Point world,Point pixel)
    {Set(app,"camera",world-new Point((pixel.x-640)/36,(pixel.y-390)/36));}
    private static Point ScreenPoint(FrontierRuntime app,Point world){return (Point)Call(app,"ScreenPoint",world);}
    // Decode what Atlas actually submitted, including local rotation, canvas
    // scale and letterboxing. An unrotated horizontal rectangle cannot pass.
    private static Point Endpoint(GUI.TextureCall call,bool end)
    {
        Vector3 physical=call.matrix.MultiplyPoint3x4(new Vector3(call.rect.x+(end?call.rect.width:0),call.rect.y+call.rect.height/2,0));
        float scale=Math.Min(Screen.width/1280f,Screen.height/800f);
        return new Point((physical.x-(Screen.width-1280*scale)/2)/scale,(physical.y-(Screen.height-800*scale)/2)/scale);
    }
    private static List<GUI.TextureCall> Contour(float width,Color color)
    {return GUI.textureCalls.FindAll(c=>c.texture==Texture2D.whiteTexture&&Near(c.rect.height,width,.001f)&&Same(c.color,color));}
    private static bool SameSegment(GUI.TextureCall a,GUI.TextureCall b)
    {return (Endpoint(a,false)-Endpoint(b,false)).Length<.04f&&(Endpoint(a,true)-Endpoint(b,true)).Length<.04f;}
    private static List<GUI.TextureCall> PairedContour(Color color)
    {
        var dark=Contour(7,Shadow);var core=Contour(3,color);
        Check(dark.Count>0,"Actual OnGUI must submit a continuous 7px dark foreground boundary after world scenery");
        Check(core.Count==dark.Count,"Every dark boundary segment has an opaque 3px bright core");
        for(int i=0;i<dark.Count;i++)
        {
            Check(SameSegment(dark[i],core[i]),"The thick shadow and bright core share transformed center-line endpoints");
            Check(GUI.textureCalls.IndexOf(dark[i])<GUI.textureCalls.IndexOf(core[i]),"The dark backing is drawn before its bright core");
            Check(Near(dark[i].uv.x,0)&&Near(dark[i].uv.y,0)&&Near(dark[i].uv.width,1)&&Near(dark[i].uv.height,1),"Boundary segments use the whole plain white texture, without an alpha atlas");
        }
        return dark;
    }
    private static void Circle(List<GUI.TextureCall> lines,Point center,float radius)
    {
        Check(lines.Count>=12,"A circle is a connected boundary rather than isolated points");
        for(int i=0;i<lines.Count;i++)
        {
            Point from=Endpoint(lines[i],false),to=Endpoint(lines[i],true);
            Check(Near((from-center).Length,radius,.06f)&&Near((to-center).Length,radius,.06f),"Both submitted chord endpoints lie on the authoritative collision radius");
            Check((to-Endpoint(lines[(i+1)%lines.Count],false)).Length<.07f,"Adjacent actual draw segments meet without a gap, including circle closure");
        }
    }
    private static void SameGeometry(List<GUI.TextureCall> before,List<GUI.TextureCall> after,string why)
    {
        Check(before.Count==after.Count,why+" (segment count)");
        for(int i=0;i<before.Count;i++)Check(SameSegment(before[i],after[i]),why+" (rotated endpoints)");
    }
    private static bool Intersects(Rect a,Rect b)
    {return a.x+a.width>b.x&&a.x<b.x+b.width&&a.y+a.height>b.y&&a.y<b.y+b.height;}
    private static void ForegroundWorldAndHudPolicies(FrontierRuntime app)
    {
        foreach(int room in new[]{1,5,7,8})
        {
            Prepare(app,room,true);var g=app.Game;Enemy boss=g.enemies.Find(e=>e.boss);
            Check(boss!=null,"The shipped room "+room+" spawns its actual mature guardian, including final-floor abyss");
            g.enemies.RemoveAll(e=>e!=boss);boss.state=1;boss.attackPhase=1;boss.windupDuration=1.02f;boss.timer=.6f;
            boss.target=boss.Position;Set(app,"camera",boss.Position);
            if(room==1)
            {
                Decoration tree=Array.Find(g.Map.decor,d=>d.kind=="tree");
                Check(tree!=null,"The forest capture uses a shipped tree canopy");
                boss.x=tree.x;boss.y=tree.y-1;boss.target=boss.Position;Set(app,"camera",boss.Position);
            }
            Draw(app,room==1?"forest-canopy":room==5?"large-boss":room==7?"snow":"abyss");
            var boundary=PairedContour(EnemyInk);Circle(boundary,ScreenPoint(app,boss.target),GameCore.EnemyAreaRadius(boss)*36);
            int first=GUI.textureCalls.IndexOf(boundary[0]);
            int actor=GUI.textureCalls.FindLastIndex(c=>c.texture.name=="resource:Frontier/Art/Bosses");
            Check(actor>=0&&actor<first,"The actual large boss body is submitted before the foreground boundary");
            Rect warningBounds=(Rect)Call(app,"EnemyWarningBounds",boss);
            Check(GUI.textureCalls.Exists(c=>c.texture.name=="resource:Frontier/Art/Bosses"&&Intersects(c.rect,warningBounds)),"The large body really overlaps the warning footprint in this fixture");
            if(room==1)
            {
                int canopy=GUI.textureCalls.FindLastIndex(c=>c.texture.name=="resource:Frontier/Art/WorldProps"&&Intersects(c.rect,warningBounds));
                Check(canopy>=0&&canopy<first,"The overlapping real tree canopy is submitted before every new foreground segment");
            }
            foreach(bool reduced in new[]{false,true})foreach(int fx in new[]{0,2})
            {
                g.save.reducedMotion=reduced;g.save.effectsQuality=fx;Draw(app);
                SameGeometry(boundary,PairedContour(EnemyInk),"Low/high effects and reduced-motion cannot remove or move a committed boundary");
            }
            g.save.reducedMotion=true;g.save.effectsQuality=0;
            PlaceOnScreen(app,boss.target,new Point(620,754));Draw(app,"hud-"+room);
            var overHud=PairedContour(EnemyInk);
            Check(overHud.Count==boundary.Count*2,"A warning overlapping the actual hotbar is drawn once in world and once above HUD");
            int panel=GUI.textureCalls.FindLastIndex(c=>c.texture==Texture2D.whiteTexture&&Same(c.rect,new Rect(426,730,428,54)));
            Check(panel>=0&&GUI.textureCalls.IndexOf(overHud[0])<panel&&GUI.textureCalls.IndexOf(overHud[boundary.Count])>panel,
                "Actual HUD panel submission separates the world boundary from its foreground witness");
            SameGeometry(overHud.GetRange(0,boundary.Count),overHud.GetRange(boundary.Count,boundary.Count),"The HUD witness retains the same collision boundary");
        }
        Prepare(app);app.Game.enemies.Clear();Set(app,"combatHudTime",3f);
        var grazing=new Hazard{kind="tide",position=app.Game.Position,radius=1,timer=.8f,damage=17};app.Game.hazards.Add(grazing);
        app.Game.save.x=grazing.position.x+12; // Keep the player silhouette out of this status-panel overlap test.
        PlaceOnScreen(app,grazing.position,new Point(200,134));
        Rect status=new Rect(20,20,260,77);Point grazingCenter=ScreenPoint(app,grazing.position);
        Check(!Intersects(status,new Rect(grazingCenter.x-36,grazingCenter.y-36,72,72)),"The grazing regression separates the exact damage circle from the actual HUD panel");
        Check((bool)Call(app,"ThreatOver",status),"A 7px contour brushing the status panel must trigger HUD fading even when its exact damage bounds miss by one pixel");
        Draw(app,"stroke-grazes-status");var grazingLines=PairedContour(PixelArt.C("#d59583"));
        Check(grazingLines.Count==96,"A circle whose thick stroke alone touches the status HUD receives its full 48-segment post-HUD witness");
        int statusPanel=GUI.textureCalls.FindLastIndex(c=>c.texture==Texture2D.whiteTexture&&Same(c.rect,status));
        Check(statusPanel>=0&&GUI.textureCalls.IndexOf(grazingLines[48])>statusPanel,"The grazing contour is visibly restored after the actual status panel");
        Console.WriteLine("PASS actual OnGUI forest canopy, large boss, snow and abyss occlusion; 7px dark/3px opaque core; all FX/motion policies; identical post-HUD witness; NATIVE_UNVERIFIED");
    }
    private static void CollisionGeometryAndPreparedBossPhases(FrontierRuntime app)
    {
        Prepare(app);Enemy e=Warning(app,0);
        foreach(Point direction in new[]{new Point(1,0),new Point(-1,0),new Point(0,-1),new Point(0,1)})
        {
            e.Face(direction);Draw(app);var lines=PairedContour(EnemyInk);Point center=ScreenPoint(app,e.Position);int radial=0,arc=0;
            foreach(var line in lines)
            {
                Point a=Endpoint(line,false)-center,b=Endpoint(line,true)-center;
                if(a.Length<.05f||b.Length<.05f)
                {
                    Point edge=a.Length>b.Length?a:b;radial++;
                    Check(Near(edge.Length,GameCore.EnemyMeleeRadius*36,.06f)&&Near(edge.Normalized.x*direction.x+edge.Normalized.y*direction.y,GameCore.EnemyMeleeCosine,.001f),
                        "Claw radial edges end exactly at the real radius and angular damage cutoff");
                }
                else
                {
                    arc++;Check(Near(a.Length,GameCore.EnemyMeleeRadius*36,.06f)&&Near(b.Length,GameCore.EnemyMeleeRadius*36,.06f),"Claw arc endpoints follow the actual melee collision radius");
                    Check(a.Normalized.x*direction.x+a.Normalized.y*direction.y>=GameCore.EnemyMeleeCosine-.001f&&b.Normalized.x*direction.x+b.Normalized.y*direction.y>=GameCore.EnemyMeleeCosine-.001f,
                        "The open claw sector never advertises the safe rear half as a full circle");
                }
            }
            Check(radial==2&&arc>=12,"A real claw sector has two radius edges and a connected front arc");
        }
        e.kind=1;
        foreach(int width in new[]{960,1280,1920})foreach(Point direction in new[]{new Point(1,0),new Point(-1,0),new Point(0,-1),new Point(0,1)})
        {
            Screen.width=width;Screen.height=width==1920?1080:width==960?600:800;e.Face(direction);e.target=e.Position+direction*4;
            Draw(app,width==1920&&direction.y<0?"archer-north-letterbox":null);var lines=PairedContour(EnemyInk);
            Point lift=new Point(0,GameCore.WeaponHeight(1,direction)/36);
            Point from=ScreenPoint(app,GameCore.WeaponOrigin(e.Position,1,direction)-lift),to=ScreenPoint(app,e.target-lift);
            Check(lines.Count==1&&(Endpoint(lines[0],false)-from).Length<.05f&&(Endpoint(lines[0],true)-to).Length<.05f,
                "The actual rotated bow line starts at its locked directional muzzle on the elevated projectile plane at every viewport");
        }
        Screen.width=1280;Screen.height=800;e.kind=2;e.target=app.Game.Position;Draw(app,"heavy-circle");Circle(PairedContour(EnemyInk),ScreenPoint(app,e.target),GameCore.EnemyAreaRadius(e)*36);
        int bosses=0;
        for(int room=1;room<=8;room++)
        {
            Prepare(app,room,true);var g=app.Game;Enemy boss=g.enemies.Find(b=>b.boss);Check(boss!=null,"Every one of eight mature encounters supplies a real boss");
            g.enemies.RemoveAll(b=>b!=boss);Set(app,"camera",boss.Position);boss.state=1;boss.target=boss.Position;boss.windupDuration=1;boss.timer=.6f;
            foreach(int phase in new[]{1,2,3})
            {
                boss.attackPhase=phase;boss.hp=boss.maxHp;Draw(app);var prepared=PairedContour(EnemyInk);
                Circle(prepared,ScreenPoint(app,boss.target),GameCore.EnemyAreaRadius(boss)*36);
                Point center=ScreenPoint(app,boss.target);float radius=GameCore.EnemyAreaRadius(boss)*36;
                Rect exact=new Rect(center.x-radius,center.y-radius,radius*2,radius*2);
                Check(Same((Rect)Call(app,"EnemyWarningBounds",boss),exact),"The established EnemyWarningBounds contract stays at the exact collision radius rather than visual stroke padding");
                Check(GUI.textureCalls.Exists(c=>c.texture.name=="resource:Frontier/Art/Effects"&&Near(c.uv.x,.5f,.001f)&&Near(c.uv.y,0,.001f)&&Same(c.rect,exact)),
                    "The existing authored warning disc and the new line boundary retain the same prepared collision extent");
                boss.hp=boss.maxHp*.2f;Draw(app);
                Check(boss.BossPhase==3&&boss.AttackPhase==phase,"Live HP phase changes while the staged prepared phase stays committed");
                SameGeometry(prepared,PairedContour(EnemyInk),"Every mature boss keeps its prepared boundary across live HP thresholds");
            }
            bosses++;
        }
        Prepare(app);app.Game.enemies.Clear();
        string[] kinds={"tide","glass","rift","archiveTide"};string[] colors={"#d59583","#ebc58b","#b9a1d1","#fff1a6"};
        for(int i=0;i<kinds.Length;i++)
        {
            app.Game.hazards.Clear();var h=new Hazard{kind=kinds[i],position=app.Game.Position,radius=1.1f+i*.25f,timer=.8f,damage=17};app.Game.hazards.Add(h);
            Draw(app,"hazard-"+kinds[i]);var lines=PairedContour(PixelArt.C(colors[i]));Circle(lines,ScreenPoint(app,h.position),h.radius*36);
            PlaceOnScreen(app,h.position,new Point(620,754));Set(app,"combatHudTime",2f);Draw(app,"hazard-hud-"+kinds[i]);
            var hud=PairedContour(PixelArt.C(colors[i]));Check(hud.Count==lines.Count*2,"All four regional hazard geometries retain their full circle above actual HUD");
            SameGeometry(hud.GetRange(0,lines.Count),hud.GetRange(lines.Count,lines.Count),"Hazard HUD geometry equals its world collision radius");
            Set(app,"camera",app.Game.Position);
        }
        Check(bosses==8,"All eight mature bosses were exercised");
        Console.WriteLine("PASS actual claw angular cutoffs, 12 directional bow/canvas combinations, heavy circle, 8 mature bosses x 3 committed phases across HP changes, and four regional hazard radii/colors/HUD witnesses; NATIVE_UNVERIFIED");
    }
    // Include all core fields and nested model state, rather than a hand-picked
    // list that could miss rendering mutating timers, targets or save resources.
    private static void AppendState(StringBuilder output,object value,HashSet<object> visited)
    {
        if(value==null){output.Append("null;");return;}Type type=value.GetType();
        if(type.IsPrimitive||type.IsEnum||value is string||value is decimal)
        {output.Append(Convert.ToString(value,CultureInfo.InvariantCulture)).Append(';');return;}
        if(!type.IsValueType&&!visited.Add(value)){output.Append("ref;");return;}
        var items=value as System.Collections.IEnumerable;
        if(items!=null){foreach(object item in items)AppendState(output,item,visited);output.Append("end;");return;}
        var fields=type.GetFields(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);Array.Sort(fields,(a,b)=>string.CompareOrdinal(a.Name,b.Name));
        foreach(var field in fields){output.Append(field.Name).Append('=');AppendState(output,field.GetValue(value),visited);}
    }
    private static string State(GameCore game)
    {var result=new StringBuilder();AppendState(result,game,new HashSet<object>());return result.ToString();}
    private static void NoContour(FrontierRuntime app,string reason)
    {Draw(app);Check(Contour(7,Shadow).Count==0,reason);}
    private static void LifetimeVisibilityAndDrawIsolation(FrontierRuntime app)
    {
        Prepare(app);Enemy e=Warning(app);Draw(app);PairedContour(EnemyInk);
        foreach(int state in new[]{0,2}){e.state=state;NoContour(app,"Cancelled and released attack states emit no new danger boundary");}e.state=1;
        app.Game.enemies.Remove(e);NoContour(app,"Removing a dead/cleared enemy cannot leave a cached contour");app.Game.enemies.Add(e);
        foreach(string mode in new[]{"pause","inventory","map","dialog","fish","title"})
        {app.Game.mode=mode;NoContour(app,"Menus, dialogue and fishing must not emit a new active-world danger contour: "+mode);}app.Game.mode="world";
        app.Game.deathTime=.7f;NoContour(app,"The dying player view emits no new danger contour even with a remaining live warning");app.Game.deathTime=0;
        PlaceOnScreen(app,e.target,new Point(1900,1100));
        app.Game.hazards.Add(new Hazard{kind="tide",position=e.target,radius=1.35f,timer=.8f,damage=17});
        NoContour(app,"Entirely offscreen enemy and regional area warnings cannot emit foreground geometry");app.Game.hazards.Clear();
        Set(app,"camera",app.Game.Position);e.kind=1;e.x=app.Game.Position.x+35;e.y=app.Game.Position.y;e.target=app.Game.Position;e.Face(new Point(-1,0));Draw(app);
        Check(PairedContour(EnemyInk).Count>=1,"An offscreen archer whose incoming lane crosses the viewport retains its real danger line");
        e.target=e.Position+new Point(5,0);NoContour(app,"A distant archer with its entire bow lane outside view emits no foreground contour");
        Prepare(app,0);var g=app.Game;WorldObject mentor=Array.Find(g.Map.objects,o=>o.kind=="mentor");
        Check(mentor!=null,"The real town includes the mature sparring mentor");g.save.x=mentor.x;g.save.y=mentor.y;g.save.practiceBits=7;
        Check(g.StartSparring(),"Production StartSparring enters the actual tactical drills");Set(app,"camera",g.Position);
        for(int stage=0;stage<3;stage++)
        {
            g.enemies.Clear();g.Tick(.02f,new Point());Check(g.SparringAwaitingNext||g.SparringFinished,"Actual cleared drill enters paused tactical review");
            // Stale warning injection proves the paused review gate, rather
            // than obtaining a trivial empty-list absence assertion.
            Enemy stale=Warning(app);g.hazards.Add(new Hazard{kind="tide",position=g.Position,radius=1.35f,timer=.8f,damage=17});
            PlaceOnScreen(app,stale.target,new Point(620,754));NoContour(app,"Cleared and final sparring reviews suppress even stale warnings beneath HUD");
            g.enemies.Clear();g.hazards.Clear();if(stage<2)Check(g.ContinueSparring(),"The actual manual next-drill action remains usable");
        }
        Prepare(app);g=app.Game;e=Warning(app);g.hazards.Add(new Hazard{kind="tide",position=g.Position+new Point(4,0),radius=1.35f,timer=.8f,damage=17});
        Matrix4x4 matrix=Matrix4x4.TRS(new Vector3(21,39,0),Quaternion.Euler(0,0,17),new Vector3(1.4f,1.4f,1));Color color=new Color(.3f,.5f,.7f,.8f);
        string before=State(g);List<GUI.TextureCall> previous=null;
        foreach(EventType evt in new[]{EventType.Layout,EventType.Repaint,EventType.Repaint,EventType.Layout,EventType.Repaint})
        {
            Event.current.type=evt;GUI.matrix=matrix;GUI.color=color;GUI.enabled=false;Draw(app);
            Check(ReferenceEquals(app.Game,g)&&State(g)==before,"Repeated real OnGUI Layout/Repaint preserves every GameCore field and nested save/encounter state");
            Check(Same(GUI.matrix,matrix)&&Same(GUI.color,color)&&!GUI.enabled,"OnGUI restores the caller's color, matrix and enabled state with rotated danger geometry");
            var lines=Contour(7,Shadow);Check(lines.Count>0,"Isolation check actually exercises new foreground geometry");
            if(previous!=null)SameGeometry(previous,lines,"Repeated Layout/Repaint cannot jitter or advance warning geometry");previous=lines;
        }
        Event.current.type=EventType.Repaint;GUI.matrix=Matrix4x4.identity;GUI.color=Color.white;GUI.enabled=true;
        Console.WriteLine("PASS cancellation/removal, menus/death, full offscreen versus incoming bow lane, all real cleared sparring review stages, complete GameCore state and GUI state isolation across five OnGUI events; NATIVE_UNVERIFIED");
    }
    private static void RealExpiryContactsAndDodge(FrontierRuntime app)
    {
        int contacts=0;
        foreach(bool render in new[]{false,true})foreach(int scenario in new[]{0,1,2})
        {
            Prepare(app);var g=app.Game;Enemy e=Warning(app);Point target=g.Position;e.target=target;e.x=target.x+.5f;e.y=target.y;e.timer=.01f;
            Check(g.CanStand(target)&&g.CanStand(target+new Point(0,1.6f)),"The contact fixture uses real walkable ground on both sides of the radius");
            if(scenario==1){g.save.y=target.y+1.6f;}
            if(scenario==2){g.Dash(new Point(0,1));Check(g.dashTime>0&&g.invulnerable>0,"Production Dash starts its real movement/invulnerability window");}
            float health=g.save.health,energy=g.save.energy,timer=e.timer;
            if(render){Draw(app,"contact-heavy-"+scenario);PairedContour(EnemyInk);Check(g.save.health==health&&g.save.energy==energy&&e.timer==timer,"Rendering a warning never resolves damage, pays energy or advances expiry");}
            g.Tick(.02f,new Point());Check(e.state==0&&e.releaseTime>0,"The actual core tick releases the prepared heavy attack");
            Check(Near(health-g.save.health,scenario==0?14:0,.001f),"Actual expiry hits inside, misses outside and honors production dodge invulnerability independently of rendering");
            Check(g.effects.Exists(f=>f.kind=="danger"&&Near(f.radius,GameCore.EnemyAreaRadius(e),.001f)),"The actual release effect retains the same heavy area radius");
            if(render)NoContour(app,"A released attack has no stale foreground warning on the next actual draw");contacts++;
        }
        foreach(string kind in new[]{"tide","glass","rift","archiveTide"})foreach(bool render in new[]{false,true})foreach(int scenario in new[]{0,1,2})
        {
            Prepare(app);var g=app.Game;g.enemies.Clear();Point target=g.Position;
            var h=new Hazard{kind=kind,position=target,radius=1.35f,timer=.01f,damage=17};g.hazards.Add(h);
            if(scenario==1)g.save.y=target.y+1.45f;
            if(scenario==2)g.Dash(new Point(0,1));
            float health=g.save.health,timer=h.timer;
            if(render){Draw(app);Check(Contour(7,Shadow).Count>0&&g.save.health==health&&h.timer==timer,"Actual regional warning draw leaves the authoritative contact timer/health untouched");}
            g.Tick(.02f,new Point());Check(!g.hazards.Contains(h),"Actual expansion tick removes the expired "+kind+" hazard");
            Check(Near(health-g.save.health,scenario==0?17:0,.001f),"Real "+kind+" expiry preserves inside/outside/dodge outcomes whether its foreground warning was drawn");
            if(render)NoContour(app,"Expired regional hazards leave no cached foreground contour");contacts++;
        }
        Check(contacts==30,"Five real area attacks each cover rendered/unrendered inside, outside and dodge cases");
        Console.WriteLine("PASS 30 actual GameCore expiry contacts: heavy plus four regional hazards, inside/outside/real Dash, with and without OnGUI; identical damage and warning cleanup; NATIVE_UNVERIFIED");
    }
    public static int Main(string[] args)
    {
        string previous=Application.persistentDataPath;string directory=Path.Combine(Path.GetTempPath(),"starfall-threat-contours-"+Guid.NewGuid());
        Application.persistentDataPath=directory;captureFolder=args.Length>0?args[0]:null;var app=new FrontierRuntime();int failed=0;
        try
        {
            Directory.CreateDirectory(directory);Call(app,"Awake");Check(app.Game!=null,"Production Awake loads the shipped campaign");
            foreach(Action<FrontierRuntime> test in new Action<FrontierRuntime>[]
                {ForegroundWorldAndHudPolicies,CollisionGeometryAndPreparedBossPhases,LifetimeVisibilityAndDrawIsolation,RealExpiryContactsAndDodge})
            {try{test(app);}catch(Exception error){failed++;Console.WriteLine("FAIL "+test.Method.Name+": "+(error.InnerException??error).Message);}}
            app.EndVisualReview();Call(app,"OnDestroy");
        }
        finally{Application.persistentDataPath=previous;Input.Clear();GUI.queuedClick=null;GUI.matrix=Matrix4x4.identity;GUI.color=Color.white;GUI.enabled=true;Event.current.type=EventType.Repaint;if(Directory.Exists(directory))Directory.Delete(directory,true);}
        Console.WriteLine("Runtime threat contours: "+(4-failed)+"/4 groups passed; NATIVE_UNVERIFIED");return failed==0?0:1;
    }
}
