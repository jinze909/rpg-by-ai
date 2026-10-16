// Actual production DrawWorldDecoration, OnGUI and keyboard Update against
// offline Unity declarations. Native import, GPU playback and feel are unverified.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Starfall;
using UnityEngine;

public static class RuntimeSceneryCoverChecks
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    // Recorded from immutable 6ab2cfa; only cover alpha is omitted. The witness
    // retains every scenery UV/rect/RGB/order and static ground call/alpha.
    private const string GeometryDigest="60716cd4bb3f1c97ee4fb8bf23e20e5df937cb0e9f66a7a67acaaec6da5fbc5a";
    private static readonly int[] Rooms={0,1,2,3,4,6,7,10,GameCore.ReachRoom};
    private static string captureFolder,capturePrefix;
    private static int captures;
    private static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    private static object Call(object target,string method,params object[] args)
    {
        var found=target.GetType().GetMethod(method,Private|BindingFlags.Public);
        Check(found!=null,"Existing production entry point is missing: "+method);return found.Invoke(target,args);
    }
    private static T Get<T>(object target,string field){return (T)target.GetType().GetField(field,Private).GetValue(target);}
    private static void Set(object target,string field,object value){target.GetType().GetField(field,Private).SetValue(target,value);}
    private static bool Near(float a,float b,float tolerance=.001f){return Math.Abs(a-b)<tolerance;}
    private static bool Same(Rect a,Rect b){return Near(a.x,b.x)&&Near(a.y,b.y)&&Near(a.width,b.width)&&Near(a.height,b.height);}
    private static bool Same(Color a,Color b){return Near(a.r,b.r)&&Near(a.g,b.g)&&Near(a.b,b.b)&&Near(a.a,b.a);}
    private static bool Same(Matrix4x4 a,Matrix4x4 b)
    {return Near(a.m00,b.m00)&&Near(a.m01,b.m01)&&Near(a.m03,b.m03)&&Near(a.m10,b.m10)&&Near(a.m11,b.m11)&&Near(a.m13,b.m13);}
    private static bool Cover(Decoration d){return d.kind=="tree"||d.kind=="palm"||d.kind=="snowpine"||d.kind=="ruin";}
    private static void Clear(){GUI.commands.Clear();GUI.textureCalls.Clear();GUI.buttonCalls.Clear();}
    private static void Prepare(FrontierRuntime app,int room=1)
    {
        Input.Clear();GUI.queuedClick=null;GUI.matrix=Matrix4x4.identity;GUI.color=Color.white;GUI.enabled=true;
        Screen.width=1280;Screen.height=800;Time.unscaledDeltaTime=.02f;Event.current.type=EventType.Repaint;
        app.BeginVisualReview(0,room);app.VisualReviewPaused=false;var g=app.Game;
        Check(g.save.room==room,"Fixture enters the shipped map "+room);g.enemies.Clear();g.save.companion=-1;
        g.save.reducedMotion=true;g.save.effectsQuality=0;g.save.showGuidance=false;g.save.english=true;
        g.hazards.Clear();g.projectiles.Clear();g.effects.Clear();g.fallen.Clear();g.toastTime=0;
        Set(app,"camera",g.Position);Set(app,"clock",0f);Set(app,"combatHudTime",0f);Set(app,"pointerSeen",false);
        Set(app,"controllerUsed",false);Set(app,"applicationPaused",false);Set(app,"drawingOutline",false);Call(app,"Resize");
    }
    private static void Draw(FrontierRuntime app,string capture=null)
    {
        Clear();Call(app,"OnGUI");
        if(captureFolder!=null&&capture!=null)
        {
            var commands=GUI.commands.ToArray();var textures=GUI.textureCalls.ToArray();var buttons=GUI.buttonCalls.ToArray();
            GUI.Export(captureFolder,capturePrefix+"-"+capture);captures++;
            GUI.commands.AddRange(commands);GUI.textureCalls.AddRange(textures);GUI.buttonCalls.AddRange(buttons);
        }
    }
    // Deliberately synthetic boundary samples test only presentation. Actual
    // walking below uses Update and collision, with no position assignments.
    private static void Actor(FrontierRuntime app,Point point,Enemy enemy=null)
    {if(enemy==null){app.Game.save.x=point.x;app.Game.save.y=point.y;}else{enemy.x=point.x;enemy.y=point.y;}}
    private static List<GUI.TextureCall> Decoration(FrontierRuntime app,Decoration prop)
    {Clear();Call(app,"DrawWorldDecoration",prop);return new List<GUI.TextureCall>(GUI.textureCalls);}
    private static float Alpha(FrontierRuntime app,Decoration prop)
    {
        var calls=Decoration(app,prop);Check(calls.Count>0,"The actual authored cover submits art: "+prop.kind);
        float alpha=calls[0].color.a;
        foreach(var c in calls)Check(Near(c.color.a,alpha)&&Near(c.color.r,1)&&Near(c.color.g,1)&&Near(c.color.b,1),"Every canopy strip/ruin uses one neutral bounded cover tint");
        Check(alpha>=.3999f&&alpha<=1.0001f,"Cover alpha remains bounded at .4..1");return alpha;
    }
    private static float At(FrontierRuntime app,Decoration prop,Point relative,Enemy enemy=null)
    {Actor(app,new Point(prop.x+relative.x,prop.y+relative.y),enemy);return Alpha(app,prop);}
    private static void Boundary(FrontierRuntime app,Decoration d,Point inside,Point outside,Enemy e,string label)
    {
        float a=At(app,d,inside,e),b=At(app,d,outside,e);
        Check(Math.Abs(a-b)<.006f,"A .002-world-unit "+label+" step cannot pop the whole "+d.kind+" in room "+app.Game.save.room+" ("+a+" -> "+b+")");
    }
    private static void AuthoredPropsAndContinuousBoundaries(FrontierRuntime app)
    {
        int mature=0,reach=0,maps=0;var kinds=new HashSet<string>();
        foreach(int room in Rooms)
        {
            Prepare(app,room);int count=0;
            foreach(var d in app.Game.Map.decor)
            {
                if(!Cover(d))continue;count++;kinds.Add(d.kind);Set(app,"camera",new Point(d.x,d.y-1));
                Check(Near(At(app,d,new Point(0,-1)),.4f),"A fully covered hero retains the established .4 minimum");
                Boundary(app,d,new Point(1.399f,-1),new Point(1.401f,-1),null,"horizontal core-boundary");
                Boundary(app,d,new Point(0,-.001f),new Point(0,.001f),null,"lower core-boundary");
                Boundary(app,d,new Point(0,-2.699f),new Point(0,-2.701f),null,"upper core-boundary");
                Boundary(app,d,new Point(1.3992929f,-.0007071f),new Point(1.4007071f,.0007071f),null,"corner core-boundary");
                Boundary(app,d,new Point(1.799f,-1),new Point(1.801f,-1),null,"horizontal feather-midpoint");
                Check(Near(At(app,d,new Point(1.6f,-1)),.49375f,.0002f)&&Near(At(app,d,new Point(1.8f,-1)),.7f,.0002f)&&Near(At(app,d,new Point(2,-1)),.90625f,.0002f),
                    "Submitted alpha has the retained quarter/half/three-quarter cubic feather rather than a binary or linear fade");
                float diagonal=.4f/(float)Math.Sqrt(2);
                Check(Near(At(app,d,new Point(1.4f+diagonal,diagonal)),.7f,.0002f),"The outer corner uses radial .8-world support without a square feather");
                Check(Near(At(app,d,new Point(2.201f,-1)),1)&&Near(At(app,d,new Point(0,.801f)),1)&&Near(At(app,d,new Point(0,-3.501f)),1),"Lateral and both vertical support endpoints restore full opacity");
            }
            if(room==GameCore.ReachRoom)reach=count;else{mature+=count;if(count>0)maps++;}
        }
        Check(mature==47&&maps==8&&reach==67&&kinds.Count==4,"All 47 mature cover props in eight maps and all67 existing Reach covers are exercised");
        Console.WriteLine("PASS47 authored mature covers /8 maps +67 Reach: actual tint, .002 lateral/vertical/corner continuity, .4 minimum, cubic intermediate alpha and radial .8 support; NATIVE_UNVERIFIED");
    }
    private static Decoration Forest(FrontierRuntime app)
    {var d=Array.Find(app.Game.Map.decor,p=>p.kind=="tree"&&Near(p.x,16)&&Near(p.y,10));Check(d!=null,"The fixture uses the shipped forest tree at16,10");return d;}
    private static void WitnessesAndPresentationPolicies(FrontierRuntime app)
    {
        foreach(int room in new[]{1,6,7,2,GameCore.ReachRoom})
        {
            Prepare(app,room);var g=app.Game;var d=Array.Find(g.Map.decor,Cover);Set(app,"camera",new Point(d.x,d.y-1));
            Actor(app,new Point(d.x+20,d.y+20));var e=new Enemy(94001,2,d.x,d.y-1,100);g.enemies.Add(e);
            Boundary(app,d,new Point(1.599f,-1),new Point(1.601f,-1),e,"enemy horizontal core-boundary");
            Boundary(app,d,new Point(0,-2.899f),new Point(0,-2.901f),e,"enemy upper core-boundary");
            Check(Near(At(app,d,new Point(2,-1),e),.7f,.0002f),"Enemy retains its wider1.6 by2.9 foot-relative cover and actual intermediate tint");
            float enemyOnly=Alpha(app,d);Actor(app,new Point(d.x+1.8f,d.y-1));
            Check(Near(Alpha(app,d),enemyOnly),"Two half-strength actors choose maximum cover instead of stacking transparency");
            e.x=d.x;e.y=d.y-1;Check(Near(Alpha(app,d),.4f),"The strongest actor wins without pushing below .4");g.enemies.Clear();
            Actor(app,new Point(d.x+20,d.y+20));g.companionPosition=new Point(d.x,d.y-1);g.save.companion=-1;
            Check(Near(Alpha(app,d),1),"An inactive companion's stale position cannot reveal scenery");
            g.save.companion=0;g.companionPosition=new Point(d.x,d.y-3.1f);
            Check(Near(Alpha(app,d),.7f,.0002f),"An active companion retains the player2.7-height upper support");
            g.companionPosition=new Point(d.x,d.y+.4f);Check(Near(Alpha(app,d),.7f,.0002f),"Active companion support also feathers beyond the prop ground line");
            g.companionPosition=new Point(d.x+1.8f,d.y-1);
            Check(Near(Alpha(app,d),.7f,.0002f),"An active companion retains the player1.4 half-width support");
            Actor(app,new Point(d.x+1.8f,d.y-1));Check(Near(Alpha(app,d),.7f,.0002f),"Equal player/companion weights do not stack transparency");
            g.companionPosition=new Point(d.x,d.y-1);Check(Near(Alpha(app,d),.4f),"A stronger active companion wins at the same .4 minimum");
            g.save.companion=-1;float reference=Alpha(app,d);
            foreach(bool reduced in new[]{false,true})foreach(int fx in new[]{0,2})foreach(string mode in new[]{"world","pause"})foreach(float time in new[]{0f,9.7f})
            {g.save.reducedMotion=reduced;g.save.effectsQuality=fx;g.mode=mode;Set(app,"clock",time);Check(Near(Alpha(app,d),reference),"Cover tint is spatial and independent of clock, FX, reduced-motion and pause");}
            Actor(app,new Point(d.x+20,d.y+20));Check(Near(Alpha(app,d),1),"Moving away restores opacity immediately without cached reveal history");
            Actor(app,new Point(d.x+1.8f,d.y-1));Check(Near(Alpha(app,d),reference),"Re-entering the same point reproduces the same tint without a warmup");
        }
        foreach(int room in new[]{6,2})
        {
            Prepare(app,room);var g=app.Game;var d=Array.Find(g.Map.decor,p=>p.kind==(room==6?"palm":"ruin"));
            Point feet=room==6?new Point(10.9f,15.75f):new Point(31.2f,4.8f);
            Check(g.CanStand(feet)&&app.SetVisualReviewPosition(feet)&&(g.Position-feet).Length<.001f,"Companion-only fixture starts at exact legal feet");
            g.SelectCompanion(0);Check(g.save.companion==0&&g.CanStand(g.companionPosition)&&g.companionPosition.y<d.y,"Core selects a real companion behind the foreground cover");
            Point companionStart=g.companionPosition;float started=g.save.totalTime;
            for(int frame=0;frame<10;frame++)
            {
                g.save.companion=-1;float playerOnly=Alpha(app,d);g.save.companion=0;
                Check(Near(playerOnly,1)&&Alpha(app,d)<.53f,"The real selected/following companion alone reveals the palm/ruin while its hero is outside support");
                Set(app,"camera",new Point(d.x,d.y-1));var submitted=Decoration(app,d);Draw(app);
                Check(submitted.TrueForAll(c=>GUI.textureCalls.Exists(a=>SameCall(a,c,true))),"Ordered OnGUI retains companion-only cover tint");
                Input.axes["Vertical"]=1;Input.held.Add(KeyCode.W);Update(app);Input.Clear();
                Check(g.CanStand(g.Position)&&g.CanStand(g.companionPosition),"Real held W/Update preserves hero and following-companion collision footprints");
            }
            Check((g.companionPosition-companionStart).Length>.1f&&g.save.totalTime>started,"The companion witness follows through real Core movement, including its existing follow deadband");
        }
        Console.WriteLine("PASS player/enemy/active-companion footprints, maximum nonstacking witnesses, inactive companion exclusion, real selected/following companion-only palm/ruin and clock/FX/motion/pause/history independence; NATIVE_UNVERIFIED");
    }
    private static void Update(FrontierRuntime app){Call(app,"Update");Input.keys.Clear();}
    private static int Walk(FrontierRuntime app,Point destination,Decoration watched,bool validate,ref bool intermediate,ref float maxJump)
    {
        var g=app.Game;int frames=0;float previous=-1,startTime=g.save.totalTime;Point start=g.Position;
        Check(g.CanStand(destination),"Actual walking destination uses the complete collision footprint");
        while((g.Position-destination).Length>.13f&&frames++<3500)
        {
            Point direction=g.DirectionTo(g.Position,destination);Input.axes["Horizontal"]=direction.x;Input.axes["Vertical"]=-direction.y;Input.held.Clear();
            if(direction.x>.05f)Input.held.Add(KeyCode.D);if(direction.x<-.05f)Input.held.Add(KeyCode.A);
            if(direction.y>.05f)Input.held.Add(KeyCode.S);if(direction.y<-.05f)Input.held.Add(KeyCode.W);
            Point before=g.Position;Update(app);Check(g.CanStand(g.Position)&&g.mode=="world"&&!g.Dying,"Held WASD follows actual valid collision during scenery crossing");
            Check((g.Position-before).Length<.2f,"A walking frame uses the unchanged ordinary movement speed");
            if(watched!=null)
            {
                Set(app,"camera",new Point(watched.x,watched.y-1));float alpha=Alpha(app,watched);var submitted=Decoration(app,watched);Draw(app);
                Check(submitted.TrueForAll(c=>GUI.textureCalls.Exists(a=>SameCall(a,c,true))),"Actual OnGUI submits the exact watched decoration tint and geometry during held movement");
                intermediate|=alpha>.43f&&alpha<.97f;if(previous>=0)maxJump=Math.Max(maxJump,Math.Abs(alpha-previous));previous=alpha;
            }
        }
        Input.Clear();Check(frames<3500&&(g.Position-destination).Length<=.13f&&g.save.totalTime>startTime&&(g.Position-start).Length>.2f,"Actual Update reaches the route waypoint without position writes");
        if(validate&&watched!=null)Check(maxJump<.16f,"Ordinary walking must not produce the old .6 whole-canopy jump");return frames;
    }
    private static void ActualInputRoute(FrontierRuntime app,bool validate,bool capture)
    {
        Prepare(app,0);var g=app.Game;bool intermediate=false;float jump=0;
        var portal=Array.Find(g.Map.objects,o=>o.kind=="portal1");Check(portal!=null,"Town exposes the shipped forest portal");
        int frames=Walk(app,new Point(portal.x,portal.y+.7f),null,false,ref intermediate,ref jump);
        Check(g.Nearest()==portal,"Real E targets the shipped forest portal after actual town walking");Input.keys.Add(KeyCode.E);Update(app);g=app.Game;
        Check(g.save.room==1,"Actual keyboard E enters the mature forest");g.enemies.Clear(); // Isolate scenery; do not alter stats/collision/speed.
        var tree=Forest(app);frames+=Walk(app,new Point(18.5f,9),null,false,ref intermediate,ref jump);
        Set(app,"camera",new Point(tree.x,tree.y-1));if(capture)Draw(app,"walk-before");
        frames+=Walk(app,new Point(16,9),tree,validate,ref intermediate,ref jump);
        Set(app,"camera",new Point(tree.x,tree.y-1));if(capture)Draw(app,"walk-covered");
        if(validate)Check(intermediate&&Near(Alpha(app,tree),.4f),"Real E plus held WASD crosses intermediate production OnGUI tints and reaches the covered core");
        Console.WriteLine((validate?"PASS":"CAPTURE")+" actual E/held WASD town-to-forest route: "+frames+" collision-valid Update frames, intermediate="+intermediate+", largest adjacent alpha step="+jump+"; NATIVE_UNVERIFIED");
    }
    private static void ActualKeyboardAndWalk(FrontierRuntime app){ActualInputRoute(app,true,false);}
    private static bool SameCall(GUI.TextureCall a,GUI.TextureCall b,bool alpha)
    {return a.texture.name==b.texture.name&&Same(a.rect,b.rect)&&Same(a.uv,b.uv)&&Same(a.matrix,b.matrix)&&Near(a.color.r,b.color.r)&&Near(a.color.g,b.color.g)&&Near(a.color.b,b.color.b)&&(!alpha||Near(a.color.a,b.color.a));}
    private static bool Ground(GUI.TextureCall c)
    {return c.texture.name=="resource:Frontier/Art/Terrain"||c.texture.name=="resource:Frontier/Art/TerrainVariants"||c.texture.name=="resource:Frontier/Art/Details";}
    private static void Number(StringBuilder text,float value){text.Append(value.ToString("R",CultureInfo.InvariantCulture)).Append(',');}
    private static void Record(StringBuilder text,List<GUI.TextureCall> calls,bool alpha)
    {
        foreach(var c in calls)
        {
            text.Append(c.texture.name).Append('|');foreach(float n in new[]{c.rect.x,c.rect.y,c.rect.width,c.rect.height,c.uv.x,c.uv.y,c.uv.width,c.uv.height,c.color.r,c.color.g,c.color.b,c.matrix.m00,c.matrix.m01,c.matrix.m03,c.matrix.m10,c.matrix.m11,c.matrix.m13})Number(text,n);
            if(alpha)Number(text,c.color.a);text.Append(';');
        }
    }
    private static string Digest(string text){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-","").ToLowerInvariant();}
    private static void PreservationWitness(FrontierRuntime app,bool validate)
    {
        var witness=new StringBuilder();int decor=0;
        foreach(int room in Rooms)
        {
            Prepare(app,room);var g=app.Game;Set(app,"camera",new Point(18.123f,12.987f));Actor(app,new Point(18,13));
            g.save.reducedMotion=false;g.save.effectsQuality=2;Set(app,"clock",2.345f);
            foreach(var d in g.Map.decor)
            {
                witness.Append(room).Append('/').Append(d.kind).Append('/').Append(decor++).Append(':');Record(witness,Decoration(app,d),!Cover(d));
                if(Cover(d))
                {
                    Actor(app,new Point(d.x,d.y-1));var covered=Decoration(app,d);Actor(app,new Point(d.x+20,d.y+20));var distant=Decoration(app,d);
                    Check(covered.Count==distant.Count,"Cover does not replace authored scenery with different art");
                    for(int i=0;i<covered.Count;i++)Check(SameCall(covered[i],distant[i],false),"Cover changes alpha only; rooted strips, UVs, anchors, wind and geometry remain identical");
                }
            }
            Draw(app);witness.Append("ground/").Append(room).Append(':');Record(witness,GUI.textureCalls.FindAll(Ground),true);
        }
        string digest=Digest(witness.ToString());Console.WriteLine("Scenery/static-ground preservation witness: "+decor+" decorations; sha256="+digest);
        if(validate)Check(digest==GeometryDigest,"Authored scenery geometry/UV/wind/RGB/order, noncover alpha or static ground differs from immutable6ab2cfa");
    }
    private static void AppendState(StringBuilder text,object value,HashSet<object> seen)
    {
        if(value==null){text.Append("null;");return;}Type type=value.GetType();
        if(type.IsPrimitive||type.IsEnum||value is string||value is decimal){text.Append(Convert.ToString(value,CultureInfo.InvariantCulture)).Append(';');return;}
        if(!type.IsValueType&&!seen.Add(value)){text.Append("ref;");return;}var list=value as System.Collections.IEnumerable;
        if(list!=null){foreach(object item in list)AppendState(text,item,seen);text.Append("end;");return;}
        var fields=type.GetFields(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);Array.Sort(fields,(a,b)=>string.CompareOrdinal(a.Name,b.Name));
        foreach(var field in fields){text.Append(field.Name).Append('=');AppendState(text,field.GetValue(value),seen);}
    }
    private static string State(GameCore game){var text=new StringBuilder();AppendState(text,game,new HashSet<object>());return text.ToString();}
    private static string Files()
    {var text=new StringBuilder();var paths=Directory.GetFiles(Application.persistentDataPath,"*",SearchOption.AllDirectories);Array.Sort(paths,StringComparer.Ordinal);foreach(string p in paths)text.Append(p).Append(':').Append(Convert.ToBase64String(File.ReadAllBytes(p))).Append(';');return text.ToString();}
    private static void Place(FrontierRuntime app,Point world,Point pixel){Set(app,"camera",world-new Point((pixel.x-640)/36,(pixel.y-390)/36));}
    private static void HudFixture(FrontierRuntime app,string capture=null,bool validate=true)
    {
        Prepare(app);var g=app.Game;var d=Forest(app);Actor(app,new Point(d.x,d.y-1));
        var enemy=new Enemy(95001,2,d.x+.3f,d.y-1,100){state=1,timer=.4f,windupDuration=.7f,target=g.Position};g.enemies.Add(enemy);
        Place(app,g.Position,new Point(620,754));Set(app,"combatHudTime",3f);var expected=Decoration(app,d);Draw(app,capture);if(!validate)return;
        int first=GUI.textureCalls.FindIndex(c=>SameCall(c,expected[0],true));
        int hero=GUI.textureCalls.FindIndex(c=>c.texture.name.StartsWith("resource:Frontier/Art/Hero",StringComparison.Ordinal));
        if(hero<0)hero=GUI.textureCalls.FindIndex(c=>Array.Exists(Get<Texture2D[]>(app,"playerSheets"),t=>ReferenceEquals(c.texture,t)));
        Check(hero>=0&&first>hero,"Depth sorting still submits the hero behind the foreground canopy");
        int panel=GUI.textureCalls.FindIndex(c=>c.texture==Texture2D.whiteTexture&&Same(c.rect,new Rect(426,730,428,54)));
        int outline=GUI.textureCalls.FindLastIndex(c=>c.texture.name.Contains("/ActorMasks/outline_"));
        var dark=GUI.textureCalls.FindAll(c=>c.texture==Texture2D.whiteTexture&&Near(c.rect.height,7,.0001f)&&Same(c.color,new Color(.025f,.045f,.06f,.95f)));
        Check(panel>first&&outline>panel&&dark.Count==96&&GUI.textureCalls.IndexOf(dark[0])>first&&GUI.textureCalls.IndexOf(dark[48])>panel,
            "Canopy keeps world depth while real actor silhouettes and the two48-segment danger contours survive above the HUD");
    }
    private static void IsolationGeometryOrderAndCulling(FrontierRuntime app)
    {
        PreservationWitness(app,true);HudFixture(app);var g=app.Game;var d=Forest(app);var art=Get<PixelArt>(app,"art");Call(art,"Glow");Draw(app);
        string before=State(g),files=Files();int owned=Get<List<Texture2D>>(art,"owned").Count,cache=Get<Dictionary<string,Texture2D>>(art,"cache").Count;
        float clock=Get<float>(app,"clock");var matrix=Matrix4x4.TRS(new Vector3(19,31,0),Quaternion.Euler(0,0,17),new Vector3(1.4f,1.4f,1));var color=new Color(.3f,.5f,.7f,.8f);
        foreach(EventType evt in new[]{EventType.Layout,EventType.Repaint,EventType.Layout,EventType.Repaint})
        {
            Event.current.type=evt;GUI.matrix=matrix;GUI.color=color;GUI.enabled=false;Draw(app);
            Check(State(g)==before&&ReferenceEquals(app.Game,g)&&Near(Get<float>(app,"clock"),clock),"OnGUI leaves the complete Core/save/encounter model and presentation clock unchanged");
            Check(Same(GUI.matrix,matrix)&&Same(GUI.color,color)&&!GUI.enabled,"OnGUI restores incoming matrix, color and enabled state");
        }
        Check(Get<List<Texture2D>>(art,"owned").Count==owned&&Get<Dictionary<string,Texture2D>>(art,"cache").Count==cache,"Repeated scenery OnGUI creates no owned textures/cache entries");
        Check((bool)Call(app,"Save")&&Files()==files,"Drawing and visual-review Save preserve all save,slot and backup bytes");
        GUI.matrix=Matrix4x4.identity;GUI.color=Color.white;GUI.enabled=true;Event.current.type=EventType.Repaint;
        Set(app,"camera",new Point(d.x,d.y-13));Rect bounds=(Rect)Call(app,"WorldDecorationBounds",d);
        Check((bool)Call(app,"WorldSceneryVisible",d),"A crown inside the existing two-tile apron is retained when its ground anchor is outside the screen");
        Check(bounds.width>=100&&bounds.height>100,"Full rooted plant bounds retain crown,root and wind excursion");
        var visible=Decoration(app,d);Draw(app);Check(visible.TrueForAll(c=>GUI.textureCalls.Exists(a=>SameCall(a,c,true))),"Culling keeps every strip of the complete near-edge canopy");
        Set(app,"camera",new Point(d.x+100,d.y+100));Check(!(bool)Call(app,"WorldSceneryVisible",d),"Far scenery retains the existing culling policy");
        var far=Decoration(app,d);Draw(app);Check(!GUI.textureCalls.Exists(c=>SameCall(c,far[0],true)),"Actual ordered-world submission omits the far authored cover");
        Console.WriteLine("PASS immutable scenery/static-ground witness, alpha-only rooted geometry, complete Core/save/GUI/cache isolation, depth sorting, actual post-HUD actor/threat contours and complete-bounds culling; NATIVE_UNVERIFIED");
    }
    private static void CaptureScenes(FrontierRuntime app)
    {
        int[] rooms={1,6,7,2,GameCore.ReachRoom};string[] names={"forest","palm","snowpine","ruin","reach"};
        for(int scenario=0;scenario<rooms.Length;scenario++)for(int stage=0;stage<4;stage++)
        {
            if(rooms[scenario]==GameCore.ReachRoom&&(stage==1||stage==3))continue;
            Prepare(app,rooms[scenario]);var g=app.Game;var d=rooms[scenario]==1?Forest(app):rooms[scenario]==GameCore.ReachRoom?Array.Find(g.Map.decor,p=>p.kind=="tree"&&Near(p.x,14)&&Near(p.y,4)):Array.Find(g.Map.decor,Cover);
            float[] offset={1.2f,1.401f,1.8f,2.3f};Point foot=new Point(d.x+offset[stage],d.y-(rooms[scenario]==6||rooms[scenario]==7||rooms[scenario]==2?2.2f:1));
            Check(g.CanStand(foot)&&app.SetVisualReviewPosition(foot)&&(g.Position-foot).Length<.001f,"Staged cover capture uses exact collision-valid feet: room="+rooms[scenario]+" stage="+stage+" point="+foot.x+","+foot.y);
            bool rich=scenario%2==1;Screen.width=rich?1920:960;Screen.height=rich?1080:600;g.save.effectsQuality=rich?2:0;g.save.reducedMotion=!rich;
            Set(app,"camera",new Point(d.x+1.4f,d.y-1));Set(app,"clock",2.345f);Draw(app,names[scenario]+"-"+new[]{"core","edge","feather","clear"}[stage]);
        }
        foreach(int room in new[]{6,2})
        {
            Prepare(app,room);var g=app.Game;var d=Array.Find(g.Map.decor,p=>p.kind==(room==6?"palm":"ruin"));
            Point feet=room==6?new Point(10.9f,15.75f):new Point(31.2f,4.8f);
            Check(g.CanStand(feet)&&app.SetVisualReviewPosition(feet)&&(g.Position-feet).Length<.001f,"Companion capture uses exact legal player feet");
            g.SelectCompanion(0);Check(g.save.companion==0&&g.CanStand(g.companionPosition),"The capture uses Core's selected companion on real ground");
            Screen.width=1920;Screen.height=1080;g.save.reducedMotion=false;g.save.effectsQuality=2;Set(app,"clock",2.345f);Set(app,"camera",new Point(d.x+1.5f,d.y-1));
            Draw(app,room==6?"companion-only-palm":"companion-only-ruin");
        }
        ActualInputRoute(app,false,true);HudFixture(app,"hud-lowfx",false);app.Game.save.effectsQuality=2;app.Game.save.reducedMotion=false;Screen.width=1920;Screen.height=1080;Draw(app,"hud-rich");
        Check(captures==24,"Exactly24 comparable production OnGUI captures are recorded");Console.WriteLine("CAPTURE24 qa-cover fixtures: four mature regions x4 stages, two Reach controls, two real selected companion-only views, actual E/WASD route and two HUD policies; NATIVE_UNVERIFIED");
    }
    public static int Main(string[] args)
    {
        string previous=Application.persistentDataPath,directory=Path.Combine(Path.GetTempPath(),"starfall-scenery-cover-"+Guid.NewGuid());
        Application.persistentDataPath=directory;captureFolder=args.Length>0&&args[0]!="-"?args[0]:null;capturePrefix=args.Length>1?args[1]:"qa-cover";
        bool only=args.Length>2&&args[2]=="capture-only";var app=new FrontierRuntime();int failed=0;
        try
        {
            Directory.CreateDirectory(directory);File.WriteAllText(Path.Combine(directory,"starfall-v1.json"),"{\"version\":1,\"profession\":0}");File.WriteAllText(Path.Combine(directory,"starfall-slot-1.json.bak"),"existing-slot-backup-sentinel");Call(app,"Awake");Check(app.Game!=null,"Production Awake loads the shipped campaign");
            if(only)PreservationWitness(app,false);
            else foreach(Action<FrontierRuntime> test in new Action<FrontierRuntime>[]{AuthoredPropsAndContinuousBoundaries,WitnessesAndPresentationPolicies,ActualKeyboardAndWalk,IsolationGeometryOrderAndCulling})
            {try{test(app);}catch(Exception ex){failed++;Console.WriteLine("FAIL "+test.Method.Name+": "+(ex.InnerException??ex).Message);}}
            if(captureFolder!=null)CaptureScenes(app);app.EndVisualReview();Call(app,"OnDestroy");
        }
        finally{Application.persistentDataPath=previous;Input.Clear();GUI.queuedClick=null;GUI.matrix=Matrix4x4.identity;GUI.color=Color.white;GUI.enabled=true;Event.current.type=EventType.Repaint;if(Directory.Exists(directory))Directory.Delete(directory,true);}
        if(only){Console.WriteLine("Capture-only: semantic checks skipped; NATIVE_UNVERIFIED");return 0;}
        Console.WriteLine("Runtime scenery cover: "+(4-failed)+"/4 groups passed; NATIVE_UNVERIFIED");return failed==0?0:1;
    }
}
