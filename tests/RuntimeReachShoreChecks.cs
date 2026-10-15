// Exercise production OnGUI, actual keyboard input and SaveStore against offline
// Unity declarations. Texture pixel snapshots are supplied by the stubs; no GPU,
// native import, player performance or Unity licence validation is implied.
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Starfall;
using UnityEngine;

public static class RuntimeReachShoreChecks
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static string captureFolder, capturePrefix;
    private static Texture2D[] awakeShores;
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private static object Call(FrontierRuntime app, string method, params object[] args)
    { return typeof(FrontierRuntime).GetMethod(method, Private).Invoke(app, args); }
    private static T Get<T>(FrontierRuntime app, string field)
    { return (T)typeof(FrontierRuntime).GetField(field, Private).GetValue(app); }
    private static void Set(FrontierRuntime app, string field, object value)
    { typeof(FrontierRuntime).GetField(field, Private).SetValue(app, value); }
    private static void Clear() { GUI.commands.Clear(); GUI.textureCalls.Clear(); GUI.buttonCalls.Clear(); }
    private static bool Near(float a, float b) { return Math.Abs(a - b) < .001f; }
    private static bool Same(Rect a, Rect b)
    { return Near(a.x,b.x) && Near(a.y,b.y) && Near(a.width,b.width) && Near(a.height,b.height); }
    private static bool Shore(GUI.TextureCall c) { return c.texture.name.StartsWith("reach_shore_", StringComparison.Ordinal); }
    private static List<GUI.TextureCall> Shores() { return GUI.textureCalls.FindAll(Shore); }
    private static Dictionary<string,Texture2D> Cache(PixelArt art)
    { return (Dictionary<string,Texture2D>)typeof(PixelArt).GetField("cache",Private).GetValue(art); }
    private static List<Texture2D> Owned(PixelArt art)
    { return (List<Texture2D>)typeof(PixelArt).GetField("owned",Private).GetValue(art); }
    private static Texture2D[] CachedShores(PixelArt art)
    { var result=new List<Texture2D>(); foreach(var pair in Cache(art)) if(pair.Key.StartsWith("reach_shore_",StringComparison.Ordinal))result.Add(pair.Value); return result.ToArray(); }
    private static void Draw(FrontierRuntime app, string capture = null)
    {
        Clear(); Call(app,"OnGUI");
        if(captureFolder != null && capture != null)
        {
            var commands=GUI.commands.ToArray();var textures=GUI.textureCalls.ToArray();var buttons=GUI.buttonCalls.ToArray();
            GUI.Export(captureFolder,capturePrefix+"-"+capture);
            GUI.commands.AddRange(commands);GUI.textureCalls.AddRange(textures);GUI.buttonCalls.AddRange(buttons);
        }
    }
    private static void Prepare(FrontierRuntime app, int room = GameCore.ReachRoom)
    {
        Input.Clear(); GUI.queuedClick=null; Screen.width=1280; Screen.height=800; Time.unscaledDeltaTime=.02f;
        app.BeginVisualReview(0,room); app.VisualReviewPaused=false; var g=app.Game;
        g.enemies.Clear();g.save.companion=-1;g.save.reducedMotion=true;g.save.effectsQuality=0;g.save.showGuidance=true;g.save.english=true;
        g.save.flags.Add("shore_mature_story");g.TrackQuest("reach");g.toastTime=0;
        Set(app,"applicationPaused",false);Set(app,"pointerSeen",false);Set(app,"controllerUsed",false);Set(app,"clock",0f);
        Set(app,"camera",g.Position);Call(app,"Resize");
    }
    private static void Position(FrontierRuntime app, Point p)
    {
        Check(app.Game.CanStand(p),"Capture/input feet use the complete walkable footprint at "+p.x+","+p.y);
        Check(app.SetVisualReviewPosition(p)&&(app.Game.Position-p).Length<.001f,"Visual review never repairs a staged shoreline position");
        Set(app,"camera",p);
    }
    private static void Key(FrontierRuntime app, KeyCode key)
    { Input.keys.Add(key);Call(app,"Update");Input.keys.Clear(); }
    private static void Interact(FrontierRuntime app, string kind)
    {
        var o=Array.Find(app.Game.Map.objects,x=>x.kind==kind);Check(o!=null,"Actual shipped shoreline interaction exists: "+kind);
        Position(app,new Point(o.x,o.y-.7f));Check(app.Game.Nearest()==o,"Real E targets the shipped "+kind);Key(app,KeyCode.E);
    }
    private static void Choose(FrontierRuntime app, string action)
    {
        var g=app.Game;Check(g.mode=="dialog"&&g.conversation!=null,"Actual E displays its production dialogue");
        int choice=Array.FindIndex(g.conversation.choices,c=>c.action==action);Check(choice>=0&&choice<9,"Actual dialogue exposes "+action);
        var pages=DialogueLayout.Pages(g.L(g.conversation.text,g.conversation.textEn),g.save.largeText);
        for(int page=0;page<pages.Count;page++) { Draw(app);if(page+1<pages.Count)Key(app,KeyCode.Return); }
        Key(app,(KeyCode)((int)KeyCode.Alpha1+choice));
    }
    private static bool Water(GameCore g, int x, int y)
    { return y>=0&&y<g.Map.rows.Length&&x>=0&&x<g.Map.rows[y].Length&&g.PresentationTile(x,y)=='~'; }
    // Independent blob convention: N,E,S,W then NE,SE,SW,NW. A diagonal bank
    // owns an inner corner only when neither adjacent cardinal already owns it.
    private static int Topology(GameCore g, int x, int y)
    {
        int mask=(!Water(g,x,y-1)?1:0)|(!Water(g,x+1,y)?2:0)|(!Water(g,x,y+1)?4:0)|(!Water(g,x-1,y)?8:0);
        if((mask&3)==0&&!Water(g,x+1,y-1))mask|=16;
        if((mask&6)==0&&!Water(g,x+1,y+1))mask|=32;
        if((mask&12)==0&&!Water(g,x-1,y+1))mask|=64;
        if((mask&9)==0&&!Water(g,x-1,y-1))mask|=128;
        return mask;
    }
    private static int Normalize(int mask)
    {
        int c=mask&15;
        if((c&3)==0)c|=mask&16;if((c&6)==0)c|=mask&32;if((c&12)==0)c|=mask&64;if((c&9)==0)c|=mask&128;
        return c;
    }
    private static Color Pixel(Texture2D t,int x,int y) { return t.pixels[(47-y)*48+x]; }
    private static int Painted(Texture2D t,int x0,int y0,int x1,int y1)
    { int n=0;for(int y=y0;y<y1;y++)for(int x=x0;x<x1;x++)if(Pixel(t,x,y).a>.001f)n++;return n; }
    private static void ValidatePixels(Texture2D t,int topology)
    {
        Check(t.width==48&&t.height==48&&t.filterMode==FilterMode.Point&&t.wrapMode==TextureWrapMode.Clamp&&t.mipmapCount==1,
            "Cached bank masks retain 48x48 Point/clamped, unmipped sampling");
        Check(t.pixels!=null&&t.pixels.Length==2304,"Offline texture snapshots contain all prepared pixels");
        for(int y=17;y<31;y++)for(int x=17;x<31;x++)
            Check(Pixel(t,x,y).a<=.001f,"Every topology leaves a transparent deep-water core rather than filling the tile: "+t.name+" topology="+topology+" pixel="+x+","+y+" alpha="+Pixel(t,x,y).a);
        var colors=new HashSet<string>();var alpha=new HashSet<int>();int visible=0,bright=0;
        foreach(Color c in t.pixels)
        {
            Check(c.a>=0&&c.a<=1,"Every stored bank alpha is bounded");if(c.a<=.001f)continue;visible++;
            colors.Add(Math.Round(c.r,3)+","+Math.Round(c.g,3)+","+Math.Round(c.b,3));alpha.Add((int)Math.Round(c.a*1000));
            if(c.r+c.g+c.b>1.95f)bright++;
        }
        Check(visible>0&&visible<2100&&colors.Count>=3&&alpha.Count>=3,"A bank contains distinct lip/shallow/falloff layers and transparent water");
        Check(bright<300,"Foam remains a sparse accent rather than occupying the river centre");
        if((topology&1)==0)Check(Painted(t,18,0,30,2)==0,"Water-water north joins have no internal edge strip");
        if((topology&2)==0)Check(Painted(t,46,18,48,30)==0,"Water-water east joins have no internal edge strip");
        if((topology&4)==0)Check(Painted(t,18,46,30,48)==0,"Water-water south joins have no internal edge strip");
        if((topology&8)==0)Check(Painted(t,0,18,2,30)==0,"Water-water west joins have no internal edge strip");
        if((topology&15)==0)
        {
            int corners=0;for(int bit=16;bit<=128;bit<<=1)if((topology&bit)!=0)corners++;
            Check(visible<=256*corners,"Diagonal-only banks fit their active 16px corner footprints: "+t.name+" corners="+corners+" visible="+visible);
            for(int y=0;y<48;y++)for(int x=0;x<48;x++)
            {
                if(Pixel(t,x,y).a<=.001f)continue;
                int sx=x==47?48:x,sy=y==47?48:y,nearest=int.MaxValue;
                if((topology&16)!=0)nearest=Math.Min(nearest,(48-sx)*(48-sx)+sy*sy);
                if((topology&32)!=0)nearest=Math.Min(nearest,(48-sx)*(48-sx)+(48-sy)*(48-sy));
                if((topology&64)!=0)nearest=Math.Min(nearest,sx*sx+(48-sy)*(48-sy));
                if((topology&128)!=0)nearest=Math.Min(nearest,sx*sx+sy*sy);
                Check(nearest<256,"Every diagonal-only bank pixel stays within 16px of an actual land corner: "+t.name+" topology="+topology+" pixel="+x+","+y+" squared distance="+nearest);
            }
        }
    }
    private static string Cell(FrontierRuntime app,GUI.TextureCall c)
    {
        Point o=(Point)Call(app,"ScreenPoint",new Point(0,0));float x=(c.rect.x-o.x)/36,y=(c.rect.y-o.y)/36;
        Check(Near(x,(float)Math.Round(x))&&Near(y,(float)Math.Round(y)),"Bank masks anchor to exact world cells after rounded camera projection");
        return ((int)Math.Round(x))+","+((int)Math.Round(y));
    }
    private static Dictionary<string,Texture2D> ShoreCells(FrontierRuntime app)
    {
        var cells=new Dictionary<string,Texture2D>();
        foreach(var c in Shores())
        {
            string key=Cell(app,c);Check(!cells.ContainsKey(key),"OnGUI submits at most one complete shore mask per water tile");cells[key]=c.texture;
            string[] xy=key.Split(',');int x=int.Parse(xy[0]),y=int.Parse(xy[1]);
            Check(Water(app.Game,x,y),"No bank mask covers walkable land, boardwalk or a root blocker");
            Point p=(Point)Call(app,"ScreenPoint",new Point(x,y));
            Check(Same(c.rect,new Rect(p.x,p.y,36,36))&&Same(c.uv,new Rect(0,0,1,1)),"All bank pixels stay inside the exact water cell without UV stretching");
            Check(Near(c.color.r,1)&&Near(c.color.g,1)&&Near(c.color.b,1)&&Near(c.color.a,1),"Bank colour/alpha come from the mask rather than a policy tint");
        }
        return cells;
    }
    private static void CaptureScenarios(FrontierRuntime app)
    {
        if(captureFolder==null)return;
        for(int policy=0;policy<2;policy++)for(int language=0;language<2;language++)
        {
            Prepare(app);var g=app.Game;Screen.width=policy==0?960:1920;Screen.height=policy==0?600:1080;
            g.save.effectsQuality=policy==0?0:2;g.save.reducedMotion=policy==0;g.save.english=language==1;g.save.largeText=true;
            string suffix=(language==1?"-en":"-zh")+(policy==0?"-960-minimal":"-1920-rich");Call(app,"Resize");
            Position(app,new Point(27,39));Draw(app,"entrance"+suffix);
            Position(app,new Point(27,32.3f));Set(app,"camera",new Point(31.5f,29.5f));Draw(app,"bridge-closed"+suffix);
            Interact(app,"reachBridge");Choose(app,"reach_bridge");g.toastTime=0;Set(app,"camera",new Point(31.5f,29.5f));Draw(app,"bridge-open"+suffix);
            Position(app,new Point(44,11.3f));Set(app,"camera",new Point(46.5f,12.5f));Draw(app,"roots-corner"+suffix);
            Key(app,KeyCode.R);g.toastTime=0;Set(app,"camera",new Point(46.5f,12.5f));Draw(app,"roots-open"+suffix);
            Position(app,new Point(27,29));Set(app,"camera",new Point(30.123f,27.987f));Draw(app,"continuous-bank"+suffix);
        }
        Console.WriteLine("CAPTURE 24 actual OnGUI views: entrance, bridge closed/open, roots corner/open and fractional continuous bank; Chinese/English,960 minimal/reduced motion and1920 rich; NATIVE_UNVERIFIED");
    }
    private static void LayeredTopologyStaysInsideWater(FrontierRuntime app)
    {
        Prepare(app);var g=app.Game;Set(app,"camera",new Point(31,26));Draw(app);
        Check(Shores().Count>0,"Actual room11 OnGUI must draw layered transparent shoreline masks; the old flat Box edges draw none");
        ShoreCells(app);int water=0,deep=0,diagonal=0,bank=0;var shapes=new HashSet<int>();
        for(int y=0;y<g.Map.rows.Length;y++)for(int x=0;x<g.Map.rows[y].Length;x++)
        {
            if(!Water(g,x,y))continue;water++;int topology=Topology(g,x,y);Point p=(Point)Call(app,"ScreenPoint",new Point(x,y));
            Clear();Call(app,"DrawTerrainEdge",x,y,'~',p);var calls=Shores();
            if(topology==0){Check(GUI.textureCalls.Count==0,"Eight-neighbour deep water emits no internal outlines or transparent placeholders");deep++;continue;}
            Check(calls.Count==1&&GUI.textureCalls.Count==1,"A real water bank uses exactly one prepared layered mask");
            Check(Same(calls[0].rect,new Rect(p.x,p.y,36,36)),"The complete layered mask cannot spill into an adjacent land/road cell");
            ValidatePixels(calls[0].texture,topology);shapes.Add(topology);bank++;if((topology&15)==0)diagonal++;
        }
        Check(water==459&&bank>240&&deep>100&&diagonal>0&&shapes.Count>12,"The actual72x48 map exercises straight banks, outer bends, diagonal-only inner corners and clear river centres");
        foreach(int room in new[]{0,1,5,10})
        {
            Prepare(app,room);Clear();Call(app,"DrawTerrainEdge",12,12,'~',new Point(200,200));
            Check(Shores().Count==0,"Mature room"+room+" keeps its existing water-edge path");
        }
        Console.WriteLine("PASS actual OnGUI + "+water+" authored water cells: layered banks="+bank+", clear deep water="+deep+", diagonal-only corners="+diagonal+"; exact tile masks and preserved mature-room path; NATIVE_UNVERIFIED");
    }
    private static void WarmedBoundedCacheAndDisposal(FrontierRuntime app)
    {
        Check(awakeShores.Length==92,"Actual Awake must prewarm the46 nonempty eight-neighbour topologies in two variants (92 textures, below94); observed "+awakeShores.Length);
        var art=Get<PixelArt>(app,"art");var reference=new HashSet<Texture2D>(awakeShores);
        Check(reference.Count==92&&CachedShores(art).Length==92,"Every prewarmed mask has one stable, owned cache entry");
        MethodInfo mask=typeof(PixelArt).GetMethod("ReachShore"),normal=typeof(PixelArt).GetMethod("NormalizeReachShoreMask");
        Check(mask!=null&&normal!=null,"Prepared masks expose bounded topology lookup");var canonical=new HashSet<int>();
        for(int raw=0;raw<256;raw++)
        {
            int n=Normalize(raw);canonical.Add(n);Check((int)normal.Invoke(null,new object[]{raw})==n,"Normalization preserves only geometrically relevant inner corners");
            for(int variant=0;variant<2;variant++)
            {
                var t=(Texture2D)mask.Invoke(art,new object[]{raw,variant});
                if(n==0){Check(t==null,"Open river centres do not allocate or draw a shoreline texture");continue;}
                Check(t!=null&&reference.Contains(t)&&Owned(art).Contains(t),"Every raw adjacency selects an already prepared, owned bank");
                Check(t==(Texture2D)mask.Invoke(art,new object[]{n,variant+2}),"Variants are bounded by world-seed parity");ValidatePixels(t,n);
            }
        }
        Check(canonical.Count==47,"Independent eight-neighbour normalization has exactly47 valid topologies");
        Prepare(app);Draw(app);int owned=Owned(art).Count,cache=Cache(art).Count;
        for(int step=0;step<24;step++)
        {
            Set(app,"camera",new Point(26+step*.31f,25.123f));app.Game.save.effectsQuality=step%3;app.Game.save.reducedMotion=step%2==0;
            Draw(app);Check(Owned(art).Count==owned&&Cache(art).Count==cache,"Repeated actual world draws/camera changes never allocate more textures");
        }
        Check(CachedShores(art).Length==92,"Drawing every policy leaves the bounded bank cache unchanged");
        var disposable=new PixelArt();MethodInfo warm=typeof(PixelArt).GetMethod("PrewarmReachShores");warm.Invoke(disposable,null);
        var ownedMasks=CachedShores(disposable);Check(ownedMasks.Length==92,"The isolated lifecycle uses the same full prewarm");
        warm.Invoke(disposable,null);Check(CachedShores(disposable).Length==92&&Owned(disposable).Count==92,"Repeated prewarm is idempotent");
        disposable.Dispose();foreach(var t in ownedMasks)Check(t.destroyed,"PixelArt.Dispose destroys every generated bank texture");
        Check(Cache(disposable).Count==0&&Owned(disposable).Count==0&&(Texture2D)mask.Invoke(disposable,new object[]{1,0})==null,"Dispose clears ownership and cached lookup without lazy reconstruction");
        Console.WriteLine("PASS Awake prewarm, all256 raw/47 canonical adjacency patterns x2 variants,92 owned masks, repeated actual draws and idempotent prewarm/Dispose; NATIVE_UNVERIFIED");
    }
    private static int FixtureTopology(bool[] land,int columns,int x,int y)
    {
        int mask=0;
        int[] dx={0,1,0,-1,1,1,-1,-1},dy={-1,0,1,0,-1,1,1,-1};
        for(int i=0;i<8;i++)if(land[(y+dy[i])*columns+x+dx[i]])mask|=1<<i;
        return Normalize(mask);
    }
    private static Color EdgePixel(Texture2D texture,int x,int y)
    { return texture==null?new Color(0,0,0,0):Pixel(texture,x,y); }
    private static string Rgba(Color c) { return c.r+","+c.g+","+c.b+","+c.a; }
    private static void CompatibleTopologyEdgesMatch(FrontierRuntime app)
    {
        var art=Get<PixelArt>(app,"art");MethodInfo lookup=typeof(PixelArt).GetMethod("ReachShore");
        Check(lookup!=null,"Prepared shoreline lookup exists for exact RGBA seam checks");
        int fixtures=0,comparisons=0;var pairs=new HashSet<string>();
        for(int direction=0;direction<2;direction++)
        {
            bool horizontal=direction==0;int columns=horizontal?4:3,rows=horizontal?3:4;
            int ax=1,ay=1,bx=horizontal?2:1,by=horizontal?1:2;
            // The union of the two eight-neighbour footprints has ten free
            // cells. Exhaust every assignment with both centre cells water,
            // so the compared topology pairs can actually share an edge.
            for(int assignment=0;assignment<1024;assignment++)
            {
                var land=new bool[columns*rows];int bit=0;
                for(int y=0;y<rows;y++)for(int x=0;x<columns;x++)
                    if(!(x==ax&&y==ay)&&!(x==bx&&y==by))land[y*columns+x]=(assignment&(1<<bit++))!=0;
                int a=FixtureTopology(land,columns,ax,ay),b=FixtureTopology(land,columns,bx,by);fixtures++;
                if(!pairs.Add(direction+":"+a+":"+b))continue;
                for(int av=0;av<2;av++)for(int bv=0;bv<2;bv++)
                {
                    var at=(Texture2D)lookup.Invoke(art,new object[]{a,av});var bt=(Texture2D)lookup.Invoke(art,new object[]{b,bv});
                    Check((a==0||at!=null)&&(b==0||bt!=null),"Compatible nonempty shore topologies have prepared texture pixels");
                    for(int offset=0;offset<48;offset++)
                    {
                        Color ac=EdgePixel(at,horizontal?47:offset,horizontal?offset:47),bc=EdgePixel(bt,horizontal?0:offset,horizontal?offset:0);
                        Check(ac.r==bc.r&&ac.g==bc.g&&ac.b==bc.b&&ac.a==bc.a,
                            "Shared water edge RGBA differs: "+(horizontal?"east/west":"south/north")+" topology="+a+"/"+b+" variant="+av+"/"+bv+" offset="+offset+" rgba="+Rgba(ac)+"/"+Rgba(bc));comparisons++;
                    }
                }
            }
        }
        Check(fixtures==2048&&comparisons>0,"Both shared-edge directions exhaust all compatible local land/water assignments");
        Console.WriteLine("PASS "+fixtures+" local land/water assignments, "+pairs.Count+" compatible directed topology pairs x4 variant pairs: "+comparisons+" exact RGBA edge comparisons; NATIVE_UNVERIFIED");
    }
    private static int Seed(int x,int y)
    { unchecked { return (x*73856093^y*19349663^12*83492791)&0x7fffffff; } }
    private static void CheckOpaqueWater(FrontierRuntime app)
    {
        var g=app.Game;
        for(int y=0;y<48;y++)for(int x=0;x<72;x++)
        {
            if(!Water(g,x,y))continue;Point p=(Point)Call(app,"ScreenPoint",new Point(x,y));if(p.x<=-36||p.x>=1280||p.y<=-36||p.y>=800)continue;
            int seed=Seed(x,y),column=seed%4;bool flip=(seed&4)==0;float tint=.97f+seed%5*.014f;
            Rect rect=new Rect(p.x,p.y,36,36),uv=new Rect((column+(flip?1:0))/4f,0,(flip?-1:1)/4f,.25f);
            var matches=GUI.textureCalls.FindAll(c=>c.texture.name=="resource:Frontier/Art/TerrainVariants"&&Same(c.rect,rect)&&Same(c.uv,uv));
            Check(matches.Count==1&&Near(matches[0].color.a,1)&&Near(matches[0].color.r,tint)&&Near(matches[0].color.g,tint)&&Near(matches[0].color.b,tint),
                "Original full opaque water source/UV/seed tint stay unchanged at "+x+","+y);
            var overlay=GUI.textureCalls.Find(c=>Shore(c)&&Same(c.rect,rect));
            if(overlay!=null)Check(GUI.textureCalls.IndexOf(matches[0])<GUI.textureCalls.IndexOf(overlay),"Layered banks are drawn after the original complete water bottom");
        }
    }
    private static void StableWorldSeedPoliciesAndOpaqueBottom(FrontierRuntime app)
    {
        Prepare(app);var g=app.Game;var seen=new Dictionary<string,Texture2D>();int frames=0;
        foreach(int width in new[]{960,1920})foreach(bool english in new[]{false,true})foreach(bool reduced in new[]{false,true})
        {
            Screen.width=width;Screen.height=width==960?600:1080;g.save.english=english;g.save.largeText=true;g.save.effectsQuality=reduced?0:2;g.save.reducedMotion=reduced;
            for(int step=0;step<=40;step++)
            {
                // Traverse both axes so the same 41 fractional camera steps
                // cover the long river and its northern/southern bends.
                Set(app,"camera",new Point(25+step*.6f,10.123f+step*.7f));Set(app,"clock",step*.37f);Draw(app);var cells=ShoreCells(app);
                Check(cells.Count>0,"Static bank information remains present in low effects, rich effects and both HUD languages");
                foreach(var c in cells){Texture2D prior;if(seen.TryGetValue(c.Key,out prior))Check(prior==c.Value,"The same world-coordinate bank keeps its topology/variant across fractional camera, clock, language and window policies");else seen[c.Key]=c.Value;}
                CheckOpaqueWater(app);frames++;
            }
        }
        Check(frames==328&&seen.Count>200,"The actual continuous camera route compares a substantial connected stretch under all eight display policies; frames="+frames+", unique bank cells="+seen.Count);
        Console.WriteLine("PASS328 actual OnGUI fractional-camera frames across960/1920,Chinese/English and low/reduced/rich policies; stable world-seeded masks and original opaque water UV/tint/order; NATIVE_UNVERIFIED");
    }
    private static string Collision(GameCore g)
    { var value=new System.Text.StringBuilder();for(int y=0;y<g.Map.rows.Length;y++)for(int x=0;x<g.Map.rows[y].Length;x++)value.Append(g.Blocked(x+.5f,y+.5f)?'1':'0');return value.ToString(); }
    private static void DrawIsReadOnly(FrontierRuntime app)
    {
        string save=JsonUtility.ToJson(app.Game.save),collision=Collision(app.Game),rows=string.Join("\n",app.Game.Map.rows);
        for(int frame=0;frame<3;frame++)Draw(app);
        Check(JsonUtility.ToJson(app.Game.save)==save&&Collision(app.Game)==collision&&string.Join("\n",app.Game.Map.rows)==rows,
            "Actual repeated OnGUI leaves saved progression, collision and authored map rows unchanged");
    }
    private static void ActualRoutesBridgeRootAndLegacyState(FrontierRuntime app)
    {
        // Reuse the mature real-E/held-WASD route test, rather than inventing a
        // new shortcut fixture or changing combat stats to claim continuity.
        RuntimeReachChecks.ReachEntranceAndContinuousMovement(app);
        Prepare(app);var g=app.Game;Set(app,"camera",new Point(31.5f,29.5f));Draw(app);var before=ShoreCells(app);
        for(int y=28;y<=30;y++)for(int x=30;x<=33;x++)Check(g.PresentationTile(x,y)=='~'&&g.Blocked(x+.5f,y+.5f),"Actual closed shortcut remains water with its mature collision");
        DrawIsReadOnly(app);int coins=g.save.coins,crystals=g.save.inventory[1];
        Interact(app,"reachBridge");Choose(app,"reach_bridge");Set(app,"camera",new Point(31.5f,29.5f));Draw(app);var after=ShoreCells(app);int changed=0;
        for(int y=28;y<=30;y++)for(int x=30;x<=33;x++)
        {
            Check(g.PresentationTile(x,y)=='='&&!g.Blocked(x+.5f,y+.5f),"Real E/numeric winch action preserves all12 authored bridge cells and collision");
            Check(!after.ContainsKey(x+","+y),"No stale bank mask covers the newly walkable bridge");
        }
        foreach(int y in new[]{27,31})for(int x=30;x<=33;x++)
        { string key=x+","+y;Texture2D old;Check(after.ContainsKey(key),"Adjacent water gains its new north/south bank after the actual bridge opens");if(!before.TryGetValue(key,out old)||old!=after[key])changed++; }
        Check(changed==8&&g.save.coins==coins&&g.save.inventory[1]==crystals,"All eight bridge perimeter water masks refresh without spending mature resources");DrawIsReadOnly(app);
        Position(app,new Point(44,11.3f));Draw(app);before=ShoreCells(app);Check(!g.ReachRootsOpen&&g.PresentationTile(46,12)=='v'&&g.Blocked(46.5f,12.5f),"The closed root route preserves its exact dry blocker");
        Key(app,KeyCode.R);Draw(app);after=ShoreCells(app);
        Check(g.ReachRootsOpen&&g.PresentationTile(46,12)=='='&&!g.Blocked(46.5f,12.5f),"Actual R uses existing core power to open the unchanged root path");
        foreach(var pair in before)if(after.ContainsKey(pair.Key))Check(after[pair.Key]==pair.Value,"Changing a dry root blocker into a dry path does not invent or remove water banks");DrawIsReadOnly(app);
        for(int version=1;version<=2;version++)
        {
            var legacy=new SaveData{version=version,room=9,stage=1,x=11,y=16,coins=137,kills=new[]{1,2,3,4,5,6,7,8,9,10,11},explored=new[]{11,12,13,14,15,16,17,18,19,20,21},flags=new List<string>{"intro","core_1","watch_complete","signal_complete","sparring_complete","guild_letter_reviewed"}};
            File.WriteAllText(SaveStore.PathFor(2),JsonUtility.ToJson(legacy));string issue;SaveData loaded=SaveStore.Read(2,out issue);
            Check(loaded!=null&&issue==null,"The actual legacy JSON reader accepts historical v"+version);g.Load(loaded);g.Start();
            Check(g.save.room==9&&g.save.coins==137&&g.save.kills.Length==12&&g.save.explored.Length==12&&!g.ReachVisited&&!g.ReachComplete&&!g.ReachBridgeOpen&&!g.ReachRootsOpen,"Legacy migration preserves mature ownership and appends no visual or route progress");
            for(int i=0;i<11;i++)Check(g.save.kills[i]==i+1&&g.save.explored[i]==i+11,"Every old room counter retains its exact prefix");
            Draw(app);Check(Shores().Count==0,"The warmed room11 bank cache never changes a historical inn scene");DrawIsReadOnly(app);
            g.Enter(GameCore.ReachRoom);g.enemies.Clear();Set(app,"camera",new Point(31,29));Draw(app);
            Check(g.Has("watch_complete")&&g.Has("signal_complete")&&g.Has("sparring_complete")&&g.Has("guild_letter_reviewed")&&g.Blocked(31.5f,29.5f)&&g.Blocked(46.5f,12.5f),"Entering after a legacy load retains mature stories and closed bridge/root collision");
        }
        Console.WriteLine("PASS mature actual-E/WASD crossings, real numeric bridge and R roots, eight refreshed water neighbours, draw-only state/collision, and exact v1/v2 SaveStore compatibility; NATIVE_UNVERIFIED");
    }
    public static int Main(string[] args)
    {
        string previous=Application.persistentDataPath,directory=Path.Combine(Path.GetTempPath(),"starfall-reach-shore-"+Guid.NewGuid());
        Directory.CreateDirectory(directory);Application.persistentDataPath=directory;captureFolder=args.Length>0&&args[0]!="-"?args[0]:null;capturePrefix=args.Length>1?args[1]:"qa-shore";
        var app=new FrontierRuntime();int failed=0;bool captureOnly=args.Length>2&&args[2]=="capture-only";
        try
        {
            Call(app,"Awake");Check(app.Game!=null,"Actual Awake loads the full shipped campaign");awakeShores=CachedShores(Get<PixelArt>(app,"art"));
            CaptureScenarios(app);
            if(!captureOnly)foreach(Action<FrontierRuntime> test in new Action<FrontierRuntime>[]{LayeredTopologyStaysInsideWater,WarmedBoundedCacheAndDisposal,CompatibleTopologyEdgesMatch,StableWorldSeedPoliciesAndOpaqueBottom,ActualRoutesBridgeRootAndLegacyState})
            { try{test(app);}catch(Exception error){failed++;Console.WriteLine("FAIL "+test.Method.Name+": "+(error.InnerException??error).Message);} }
            app.EndVisualReview();Call(app,"OnDestroy");
            foreach(var texture in awakeShores)Check(texture.destroyed,"Actual runtime OnDestroy releases all prewarmed bank textures");
        }
        finally{Application.persistentDataPath=previous;Input.Clear();GUI.queuedClick=null;if(Directory.Exists(directory))Directory.Delete(directory,true);}
        if(captureOnly){Console.WriteLine("Runtime Reach shoreline captures only; assertions were not run; NATIVE_UNVERIFIED");return 0;}
        Console.WriteLine("Runtime Reach shoreline: "+(5-failed)+"/5 groups passed; NATIVE_UNVERIFIED");return failed==0?0:1;
    }
}
