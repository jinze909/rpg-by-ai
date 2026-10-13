// Production OnGUI geometry against offline Unity declarations. Staged renderer
// evidence does not constitute native Unity import, GPU playback or player feel.
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Starfall;
using UnityEngine;

public static class RuntimeMapVisibilityChecks
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static string captureFolder;
    private static void Check(bool value,string message){if(!value)throw new Exception(message);}
    private static object Call(FrontierRuntime app,string method,params object[] args)
    {return typeof(FrontierRuntime).GetMethod(method,Private).Invoke(app,args);}
    private static void Set(FrontierRuntime app,string field,object value)
    {typeof(FrontierRuntime).GetField(field,Private).SetValue(app,value);}
    private static void Clear(){GUI.commands.Clear();GUI.textureCalls.Clear();GUI.buttonCalls.Clear();}
    private static void Draw(FrontierRuntime app,string capture=null)
    {
        Clear();Call(app,"OnGUI");
        if(captureFolder!=null&&capture!=null)
        {
            var commands=GUI.commands.ToArray();var textures=GUI.textureCalls.ToArray();var buttons=GUI.buttonCalls.ToArray();
            GUI.Export(captureFolder,"qa-map-visibility-"+capture);
            GUI.commands.AddRange(commands);GUI.textureCalls.AddRange(textures);GUI.buttonCalls.AddRange(buttons);
        }
    }
    private static void Prepare(FrontierRuntime app,int room)
    {
        Input.Clear();GUI.queuedClick=null;Screen.width=1280;Screen.height=800;
        app.BeginVisualReview(0,room);var g=app.Game;
        Check(g.save.room==room,"The renderer fixture enters actual shipped room "+room);
        g.enemies.Clear();g.save.companion=-1;g.save.reducedMotion=true;g.save.effectsQuality=0;g.save.showGuidance=false;g.toastTime=0;
        Set(app,"camera",g.Position);Set(app,"clock",0f);Draw(app);
    }
    private static bool Intersects(Rect a,Rect b)
    {return a.x+a.width>b.x&&a.x<b.x+b.width&&a.y+a.height>b.y&&a.y<b.y+b.height;}
    private static bool Contains(Rect outer,Rect inner)
    {return inner.x>=outer.x-.002f&&inner.y>=outer.y-.002f&&inner.x+inner.width<=outer.x+outer.width+.002f&&inner.y+inner.height<=outer.y+outer.height+.002f;}
    private static bool Near(float a,float b){return Math.Abs(a-b)<.001f;}
    private static bool SameRect(Rect a,Rect b)
    {return Near(a.x,b.x)&&Near(a.y,b.y)&&Near(a.width,b.width)&&Near(a.height,b.height);}
    private static bool SameColor(Color a,Color b)
    {return Near(a.r,b.r)&&Near(a.g,b.g)&&Near(a.b,b.b)&&Near(a.a,b.a);}
    private static bool SameCall(GUI.TextureCall a,GUI.TextureCall b)
    {return a.texture.name==b.texture.name&&SameRect(a.rect,b.rect)&&SameRect(a.uv,b.uv)&&SameColor(a.color,b.color);}
    private static bool Terrain(GUI.TextureCall call)
    {return (call.texture.name=="resource:Frontier/Art/Terrain"||call.texture.name=="resource:Frontier/Art/TerrainVariants")&&Near(call.rect.width,36)&&Near(call.rect.height,36);}
    private static List<GUI.TextureCall> VisibleTerrain()
    {return GUI.textureCalls.FindAll(c=>Terrain(c)&&Intersects(c.rect,new Rect(0,0,1280,800)));}
    private static int Range(object range,string field)
    {return (int)range.GetType().GetField(field).GetValue(range);}
    private static char LegacyTile(GameCore g,int x,int y)
    {
        char tile=g.Map.rows[y][x];
        if(g.save.room==GameCore.ArchiveRoom)
        {
            if(!g.ArchiveDrained&&x>=9&&x<=12&&y>=13&&y<=14)tile='~';
            if(!g.ArchiveVented&&x==23&&y>=6&&y<=7)tile='#';
        }
        if(tile=='%'&&!g.LowTide)tile='~';
        if((tile=='v'||tile=='w'||tile=='b')&&g.Has("seal_"+g.save.room))tile='=';
        return tile;
    }
    private static void LegacyVisibleGroundEqualsBruteForce(FrontierRuntime app)
    {
        int compared=0;
        for(int room=0;room<11;room++)
        {
            Prepare(app,room);var g=app.Game;
            foreach(Point camera in new[]{new Point(12.123f,14.123f),new Point(18,12),new Point(27.99f,7.5f),new Point(9.01f,18.25f)})
            {
                Set(app,"camera",camera);Draw(app,room==1&&compared==4?"legacy-forest":null);var actual=VisibleTerrain();
                Clear();GUI.matrix=Matrix4x4.identity;GUI.color=Color.white;
                // Reproduce the former full-map scan independently of the new
                // range helper; use the old authoritative dynamic tile rules.
                for(int y=0;y<g.Map.rows.Length;y++)for(int x=0;x<g.Map.rows[y].Length;x++)
                {
                    Point p=(Point)Call(app,"ScreenPoint",new Point(x,y));
                    if(p.x< -36||p.y< -36||p.x>1280||p.y>800)continue;
                    Call(app,"DrawSurface",x,y,LegacyTile(g,x,y),p);
                }
                var reference=VisibleTerrain();
                Check(actual.Count==reference.Count,"All mature visible ground cells remain drawn in room"+room);
                for(int i=0;i<actual.Count;i++)Check(SameCall(actual[i],reference[i]),"Mature terrain source, UV, position and opaque tint stay identical in room"+room);
                compared++;
            }
        }
        Check(compared==44,"All11 mature maps, including generated abyss and dynamic archive, were compared at four camera phases");
        Console.WriteLine("PASS44 actual OnGUI mature-map ground comparisons against legacy whole-map enumeration, including fractional cameras, generated abyss and archive; NATIVE_UNVERIFIED");
    }
    private static void FullSpriteScenerySeamsAndUnconditionalActors(FrontierRuntime app)
    {
        Prepare(app,1);var g=app.Game;Point camera=new Point(20,15);Set(app,"camera",camera);
        var kinds=new HashSet<string>{"counter","tree","palm","snowpine","house","ruin","garden"};
        foreach(MapData map in g.data.maps)foreach(Decoration d in map.decor)kinds.Add(d.kind);
        foreach(string kind in kinds)foreach(bool motion in new[]{false,true})
        {
            g.save.reducedMotion=!motion;g.save.effectsQuality=motion?2:0;
            var prop=new Decoration{kind=kind,x=20,y=15,variant=2};
            Rect bounds=(Rect)Call(app,"WorldDecorationBounds",prop);Clear();Call(app,"DrawWorldDecoration",prop);
            Check(GUI.textureCalls.Count>0,"Each actual scenery kind is drawn for its extent check");
            foreach(var texture in GUI.textureCalls)Check(Contains(bounds,texture.rect),"Complete bounds include every actual "+kind+" sprite/wind strip/counter wing");
        }
        g.save.reducedMotion=true;g.save.effectsQuality=0;
        var counter=new Decoration{kind="counter",x=camera.x+(1435-640)/36f,y=camera.y,variant=0};
        Check((bool)Call(app,"WorldSceneryVisible",counter),"A counter wing enters the prewarm apron before its ground anchor");
        var tree=new Decoration{kind="tree",x=camera.x,y=camera.y+(875-390)/36f};
        Check((bool)Call(app,"WorldSceneryVisible",tree),"A tree crown is ready while its ground anchor is below the screen");
        var distant=new Decoration{kind="house",x=camera.x+60,y=camera.y};
        Check(!(bool)Call(app,"WorldSceneryVisible",distant),"Truly distant complete static sprites are culled");
        Decoration[] original=g.Map.decor;WorldObject[] objects=g.Map.objects;
        try
        {
            g.Map.decor=new[]{counter,tree,distant};g.Map.objects=new WorldObject[0];g.enemies.Clear();
            Clear();Call(app,"DrawWorldDecoration",counter);var counterCalls=GUI.textureCalls.ToArray();
            Clear();Call(app,"DrawWorldDecoration",tree);var treeCalls=GUI.textureCalls.ToArray();
            Clear();Call(app,"DrawOrderedWorld");
            foreach(var expected in counterCalls)Check(GUI.textureCalls.Exists(c=>SameCall(c,expected)),"Ordered scenery retains all prewarmed counter wings without fade");
            foreach(var expected in treeCalls)Check(GUI.textureCalls.Exists(c=>SameCall(c,expected)),"Ordered scenery retains the full canopy before its anchor reaches view");
            var archer=new Enemy(99123,1,camera.x+23,camera.y,45){state=1,timer=.5f,windupDuration=.65f,target=g.Position,facing=new Point(-1,0)};
            g.enemies.Add(archer);Clear();Call(app,"DrawEnemyWarning",archer);Call(app,"DrawEnemyWithBars",archer);
            string[] dynamicCalls=GUI.commands.ToArray();Check(dynamicCalls.Length>4,"The offscreen actor emits its real sprite and incoming-shot warning");
            Clear();Call(app,"DrawOrderedWorld");
            foreach(string expected in dynamicCalls)Check(GUI.commands.Contains(expected),"Scenery visibility never culls or fades an active actor, warning lane or HP bar");
        }
        finally{g.Map.decor=original;g.Map.objects=objects;g.enemies.Clear();}
        Prepare(app,11);g=app.Game;Set(app,"camera",new Point(24,24));
        var canopy=new Decoration{kind="tree",x=24,y=24};
        float prior=-1;
        foreach(float distance in new[]{0,1.4f,1.5f,1.6f,1.8f,2,2.2f,2.6f})
        {
            g.save.x=24+distance;g.save.y=23;g.save.reducedMotion=true;g.save.effectsQuality=0;
            Clear();Call(app,"DrawWorldDecoration",canopy);float opacity=GUI.textureCalls[0].color.a;
            Check(opacity>=prior&&opacity>=.3999f&&opacity<=1,"New-map canopy cover changes monotonically through its feather without hiding actors or roots");
            if(distance==0)Check(Near(opacity,.4f),"Fully covered actors retain the original readable minimum opacity");
            if(distance==1.8f)Check(opacity>.4f&&opacity<1,"The old abrupt edge becomes a genuine intermediate spatial fade");
            if(distance>=2.2f)Check(Near(opacity,1),"Scenery outside the cover feather stays fully opaque");
            g.save.reducedMotion=false;g.save.effectsQuality=2;
            Clear();Call(app,"DrawWorldDecoration",canopy);
            Check(Near(GUI.textureCalls[0].color.a,opacity),"Scenery cover opacity is deterministic across motion/effect policies");prior=opacity;
        }
        g.save.x=30;g.save.y=24;g.enemies.Add(new Enemy(99124,0,24,23,40));
        Clear();Call(app,"DrawWorldDecoration",canopy);Check(Near(GUI.textureCalls[0].color.a,.4f),"Nearby enemies receive the same readable continuous canopy cover as the protagonist");
        Console.WriteLine("PASS actual complete scenery extents, canopy wind/counter wings, prewarm seam inclusion, distant static culling, unconditional active actor/warning geometry and gradual new-map canopy cover; NATIVE_UNVERIFIED");
    }
    private static void LargerMapApronAndPreviewGeometry(FrontierRuntime app)
    {
        Prepare(app,11);var g=app.Game;
        Check(g.Map.rows.Length==48&&g.Map.rows[0].Length==72,"The actual optional exploration map is72x48");
        for(int step=0;step<=40;step++)
        {
            Point camera=new Point(27+step*.25f,24.123f);Set(app,"camera",camera);Draw(app,step==20?"large-route-seam":null);
            object range=Call(app,"VisibleWorldTiles");int left=Range(range,"left"),right=Range(range,"right"),top=Range(range,"top"),bottom=Range(range,"bottom");
            Check((right-left)*(bottom-top)<=1148&&right-left<72&&bottom-top<48,"Large-map ground work stays bounded to viewport/apron rather than map area");
            Point origin=(Point)Call(app,"ScreenPoint",new Point(0,0));
            Check(left*36+origin.x<=-72&&right*36+origin.x>=1280+72&&top*36+origin.y<=-72&&bottom*36+origin.y>=800+72,
                "Both neighboring ground tiles are submitted before crossing any screen edge");
            var actual=VisibleTerrain();
            for(int y=0;y<48;y++)for(int x=0;x<72;x++)
            {
                Point p=(Point)Call(app,"ScreenPoint",new Point(x,y));Rect rect=new Rect(p.x,p.y,36,36);
                if(Intersects(rect,new Rect(0,0,1280,800)))
                    Check(actual.Exists(c=>SameRect(c.rect,rect)&&Near(c.color.a,1)),"Every visible cell is fully opaque and ready at fractional/seam camera phases");
            }
        }
        foreach(int width in new[]{960,1920})foreach(bool english in new[]{false,true})foreach(bool large in new[]{false,true})
        {
            Screen.width=width;Screen.height=width==960?600:1080;g.save.english=english;g.save.largeText=large;
            g.save.showGuidance=true;g.TrackQuest("reach");Point? goal=g.NavigationTarget();
            Check(goal.HasValue&&g.CurrentGuide.id=="reach","The fitted-map objective belongs to the actual optional survey");
            Set(app,"camera",g.Position);Draw(app,"large-hud-"+width+(english?"-en":"-zh")+(large?"-large":""));
            Rect mini=new Rect(1024,42,216,144);
            List<GUI.TextureCall> minimap=GUI.textureCalls.FindAll(c=>c.texture.name=="white"&&Near(c.rect.width,3)&&Near(c.rect.height,3)&&Contains(mini,c.rect));
            Check(minimap.Count>=72*48,"Large-map minimap fits every actual terrain cell inside its original panel");
            Point point=g.Position;Check(GUI.textureCalls.Exists(c=>SameRect(c.rect,new Rect(1024+point.x*3-2,42+point.y*3-2,5,5))&&SameColor(c.color,PixelArt.C("#f2ead7"))),
                "The current-player minimap marker uses the fitted scale rather than old coordinates");
            Check(GUI.textureCalls.Exists(c=>SameRect(c.rect,new Rect(1024+goal.Value.x*3-3,42+goal.Value.y*3-3,7,7))&&SameColor(c.color,PixelArt.C("#ebc58b"))),
                "The actual quest minimap marker follows the same fitted terrain coordinates");
            Set(app,"selectedRegion",11);g.mode="map";Draw(app,"large-preview-"+width+(english?"-en":"-zh")+(large?"-large":""));
            Rect preview=new Rect(888,264,302.4f,201.6f);
            List<GUI.TextureCall> cells=GUI.textureCalls.FindAll(c=>c.texture.name=="white"&&Near(c.rect.width,4.2f)&&Near(c.rect.height,4.2f)&&Contains(preview,c.rect));
            Check(cells.Count==72*48,"All current large-map cells fit the original preview footprint");
            Check(GUI.textureCalls.Exists(c=>SameRect(c.rect,new Rect(888+point.x*4.2f-2,264+point.y*4.2f-2,5,5))&&SameColor(c.color,PixelArt.C("#f2ead7"))),
                "The full-map current-player marker follows its fitted terrain scale");
            Check(GUI.textureCalls.Exists(c=>SameRect(c.rect,new Rect(888+goal.Value.x*4.2f-3,264+goal.Value.y*4.2f-3,7,7))&&SameColor(c.color,PixelArt.C("#ebc58b"))),
                "The actual local quest preview marker follows fitted terrain coordinates");
            g.mode="world";
        }
        Prepare(app,0);var legacy=app.Game;Draw(app);
        Check(GUI.textureCalls.Exists(c=>SameRect(c.rect,new Rect(1024,42,6,6))),"Original-map minimap cell geometry stays exactly6 pixels");
        legacy.TrackQuest("reach");Set(app,"selectedRegion",11);legacy.mode="map";Draw(app,"large-remote-preview");
        Check(GUI.textureCalls.FindAll(c=>c.texture.name=="white"&&Near(c.rect.width,4.2f)&&Near(c.rect.height,4.2f)&&Contains(new Rect(888,264,302.4f,201.6f),c.rect)).Count==72*48,
            "Remote large-map previews also fit without changing the current room");
        Check(legacy.save.room==0,"Inspecting a remote optional map never changes gameplay room/collision");
        QuestGuide quest=legacy.CurrentGuide;
        Check(quest.id=="reach"&&quest.room==11&&GUI.textureCalls.Exists(c=>SameRect(c.rect,new Rect(888+quest.target.x*4.2f-3,264+quest.target.y*4.2f-3,7,7))&&SameColor(c.color,PixelArt.C("#ebc58b"))),
            "The remote quest preview also retains the actual larger-map objective coordinates");
        Rect bridge=new Rect(888+30*4.2f,264+28*4.2f,4.2f,4.2f),roots=new Rect(888+45*4.2f,264+11*4.2f,4.2f,4.2f);
        Check(GUI.textureCalls.Exists(c=>SameRect(c.rect,bridge)&&SameColor(c.color,PixelArt.C("#365f78"))),
            "A remote closed bridge presents the authoritative water cells");
        legacy.save.flags.Add("reach_bridge");legacy.save.flags.Add("reach_roots");Draw(app,"large-remote-open-routes");
        Check(GUI.textureCalls.Exists(c=>SameRect(c.rect,bridge)&&SameColor(c.color,PixelArt.C("#aaa17e")))&&
              GUI.textureCalls.Exists(c=>SameRect(c.rect,roots)&&SameColor(c.color,PixelArt.C("#aaa17e")))&&legacy.save.room==0,
            "Persisted bridge/root state updates both remote route previews without altering current-room gameplay");
        Prepare(app,11);g=app.Game;
        Check(app.SetVisualReviewPosition(new Point(44,12)),"The root-boundary capture uses actual walkable footing beside the blocked route");
        Set(app,"camera",g.Position);Draw(app,"large-closed-roots");Clear();Call(app,"DrawReachWorld");
        var barriers=GUI.textureCalls.FindAll(c=>c.texture.name=="resource:Frontier/Art/FieldProps"&&Near(c.rect.width,40)&&Near(c.rect.height,48));
        Check(barriers.Count==9,"Every actual blocked3x3root cell has an accepted complete blocker sprite");
        for(int y=11;y<=13;y++)for(int x=45;x<=47;x++)
        {
            Point p=(Point)Call(app,"ScreenPoint",new Point(x,y));
            Check(GUI.textureCalls.Exists(c=>SameRect(c.rect,new Rect(p.x,p.y,36,36))&&Near(c.color.a,.94f))&&
                  barriers.Exists(c=>SameRect(c.rect,new Rect(p.x-2,p.y-9,40,48))&&Near(c.color.a,1)),
                "Ground blocking cells and their opaque root sprites share the authoritative closed route");
        }
        g.save.flags.Add("reach_roots");g.save.flags.Add("reach_bridge");Draw(app,"large-open-roots");Clear();Call(app,"DrawReachWorld");
        Check(!GUI.textureCalls.Exists(c=>c.texture.name=="resource:Frontier/Art/FieldProps"&&Near(c.rect.width,40)&&Near(c.rect.height,48)),
            "Opening the real root route removes every blocker sprite with its collision");
        Check(GUI.textureCalls.FindAll(c=>c.texture.name=="resource:Frontier/Art/Terrain"&&Near(c.rect.width,36)&&Near(c.rect.height,36)&&Near(c.color.a,1)).Count==12,
            "All twelve opened bridge cells display accepted opaque wooden terrain");
        Console.WriteLine("PASS41 large-map fractional camera seams with opaque two-tile aprons and bounded work, fitted local/remote map+minimap markers across8 display policies, and exact legacy6px geometry; NATIVE_UNVERIFIED");
    }
    public static int Main(string[] args)
    {
        string previous=Application.persistentDataPath;string directory=Path.Combine(Path.GetTempPath(),"starfall-map-visibility-"+Guid.NewGuid());
        Application.persistentDataPath=directory;captureFolder=args.Length>0?args[0]:null;var app=new FrontierRuntime();int failed=0;
        try
        {
            Call(app,"Awake");Check(app.Game!=null,"Production Awake loads the full campaign");
            foreach(Action<FrontierRuntime> test in new Action<FrontierRuntime>[]{LegacyVisibleGroundEqualsBruteForce,FullSpriteScenerySeamsAndUnconditionalActors,LargerMapApronAndPreviewGeometry})
            {try{test(app);}catch(Exception error){failed++;Console.WriteLine("FAIL "+test.Method.Name+": "+(error.InnerException??error).Message);}}
            app.EndVisualReview();Call(app,"OnDestroy");
        }
        finally{Application.persistentDataPath=previous;Input.Clear();GUI.queuedClick=null;if(Directory.Exists(directory))Directory.Delete(directory,true);}
        Console.WriteLine("Runtime map visibility: "+(3-failed)+"/3 groups passed; NATIVE_UNVERIFIED");return failed==0?0:1;
    }
}
