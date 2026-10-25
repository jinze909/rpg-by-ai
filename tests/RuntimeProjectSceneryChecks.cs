// Actual project payment / Update / OnGUI / SaveStore contracts using offline
// Unity declarations. Staged cameras and boundary witnesses are labelled.
// Native Editor import, GPU, hardware input and play feel are NATIVE_UNVERIFIED.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using Starfall;
using UnityEngine;

public static class RuntimeProjectSceneryChecks
{
    const BindingFlags All=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
    static string folder,prefix; static int captures;
    static void Check(bool ok,string why){if(!ok)throw new Exception(why);}
    static object Call(object obj,string name,params object[] args)
    {var method=obj.GetType().GetMethod(name,All);Check(method!=null,"Shipped method exists: "+name);return method.Invoke(obj,args);}
    static T Get<T>(object obj,string name)
    {var field=obj.GetType().GetField(name,All);Check(field!=null,"Shipped field exists: "+name);return(T)field.GetValue(obj);}
    static void Set(object obj,string name,object value){obj.GetType().GetField(name,All).SetValue(obj,value);}
    static bool Near(float a,float b){return Math.Abs(a-b)<.002f;}
    static bool Same(Rect a,Rect b){return Near(a.x,b.x)&&Near(a.y,b.y)&&Near(a.width,b.width)&&Near(a.height,b.height);}
    static bool Same(Color a,Color b){return Near(a.r,b.r)&&Near(a.g,b.g)&&Near(a.b,b.b)&&Near(a.a,b.a);}
    static bool SameCall(GUI.TextureCall a,GUI.TextureCall b)
    {return a.texture.name==b.texture.name&&Same(a.rect,b.rect)&&Same(a.uv,b.uv)&&Same(a.color,b.color)&&Near(a.matrix.m00,b.matrix.m00)&&Near(a.matrix.m01,b.matrix.m01)&&Near(a.matrix.m03,b.matrix.m03)&&Near(a.matrix.m10,b.matrix.m10)&&Near(a.matrix.m11,b.matrix.m11)&&Near(a.matrix.m13,b.matrix.m13);}
    static void Clear(){GUI.commands.Clear();GUI.textureCalls.Clear();GUI.buttonCalls.Clear();}
    static void Update(FrontierRuntime app){Call(app,"Update");Input.Clear();}
    static void Key(FrontierRuntime app,KeyCode key){Input.keys.Add(key);Update(app);}
    static void Draw(FrontierRuntime app,string name=null)
    {
        Clear();Call(app,"OnGUI");
        if(folder!=null&&name!=null){var commands=GUI.commands.ToArray();var textures=GUI.textureCalls.ToArray();var buttons=GUI.buttonCalls.ToArray();GUI.Export(folder,prefix+"-"+name);captures++;GUI.commands.AddRange(commands);GUI.textureCalls.AddRange(textures);GUI.buttonCalls.AddRange(buttons);}
    }
    static List<GUI.TextureCall> Ordered(FrontierRuntime app){Clear();Call(app,"DrawOrderedWorld");return new List<GUI.TextureCall>(GUI.textureCalls);}
    static string OrderedCommands(FrontierRuntime app){Ordered(app);return string.Join("\n",GUI.commands.ToArray());}
    static void Prepare(FrontierRuntime app,int room=0)
    {
        Input.Clear();GUI.queuedClick=null;GUI.matrix=Matrix4x4.identity;GUI.color=Color.white;GUI.enabled=true;
        Event.current.type=EventType.Repaint;Screen.width=1280;Screen.height=800;Time.unscaledDeltaTime=.02f;
        app.BeginVisualReview(1,room);app.VisualReviewPaused=false;var game=app.Game;
        Check(game.save.room==room,"Fixture enters the actual shipped map "+room);
        game.enemies.Clear();game.hazards.Clear();game.projectiles.Clear();game.effects.Clear();game.fallen.Clear();
        game.save.companion=-1;game.save.projects=new[]{0,0,0};game.save.flags.Remove("relic_1");
        game.save.inventory=new[]{100,100,100,100,100,1,2,3};game.save.coins=1000;game.save.shards=19;
        game.save.health=53;game.save.energy=61;game.save.english=true;game.save.largeText=false;game.save.showGuidance=false;
        game.save.reducedMotion=true;game.save.effectsQuality=0;game.toastTime=0;
        Set(app,"applicationPaused",false);Set(app,"controllerUsed",false);Set(app,"pointerSeen",false);Set(app,"resumeTime",0f);Set(app,"arrivalTime",0f);Set(app,"combatHudTime",0f);Set(app,"clock",0f);
        Set(app,"camera",room==0?new Point(14,11):game.Position);Set(app,"saveTimer",-1000f);Set(app,"lastRevision",game.revision);Call(app,"Resize");Draw(app);
    }
    static void ResetView(FrontierRuntime app,Point camera)
    {Set(app,"camera",camera);Set(app,"clock",0f);Set(app,"arrivalTime",0f);Set(app,"resumeTime",0f);app.Game.toastTime=0;}
    static void Click(FrontierRuntime app,Rect rect)
    {Draw(app);Check(GUI.buttonCalls.Exists(c=>Same(c.rect,rect)&&c.enabled),"Actual project control is enabled before clicking");GUI.queuedClick=rect;Draw(app);Check(!GUI.queuedClick.HasValue,"Actual project control consumes its click");}
    static void Build(FrontierRuntime app,int project)
    {
        var game=app.Game;if(game.mode!="world")game.Close();Key(app,KeyCode.B);Check(game.mode=="camp","Actual B opens town projects");
        float healthBefore=game.save.health,maxBefore=game.save.maxHealth,energyBefore=game.save.energy;
        SaveData before=JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(game.save));int tier=before.projects[project];
        Click(app,new Rect(296+318*project,570,268,42));Check(Get<object>(app,"townProjectConfirmation")!=null,"Original project plan opens the real confirmation");
        Check(game.save.coins==before.coins&&JsonUtility.ToJson(game.save.inventory)==JsonUtility.ToJson(before.inventory),"Reviewing the plan has not spent anything");
        Click(app,new Rect(654,626,288,46));
        Check(Get<object>(app,"townProjectConfirmation")==null&&game.save.projects[project]==tier+1,"Original explicit confirmation builds exactly one next tier");
        Check(game.save.coins==before.coins-(30+tier*20)&&game.save.inventory[0]==before.inventory[0]-(4+tier*2)&&game.save.inventory[1]==before.inventory[1]-(2+tier)&&game.save.inventory[4]==before.inventory[4]-(3+tier),"Original timber/crystal/part/coin costs are preserved");
        for(int index=0;index<8;index++)if(index!=0&&index!=1&&index!=4)Check(game.save.inventory[index]==before.inventory[index],"Construction does not consume another inventory category");
        for(int index=0;index<3;index++)Check(game.save.projects[index]==before.projects[index]+(index==project?1:0),"Only the paid facility changes tier");
        Check(game.save.reputation[2]==Math.Min(10,before.reputation[2]+1)&&game.save.reputation[0]==before.reputation[0]&&game.save.reputation[1]==before.reputation[1],"Original one Wild Assembly trust and cap are preserved");
        Check(game.Has("relic_1")==((project==0&&tier==1)||before.flags.Contains("relic_1")),"Only the original canal tier-II payment grants Amber Seed");
        Check(game.save.health==healthBefore&&game.save.maxHealth==maxBefore&&game.save.energy==energyBefore&&game.save.shards==before.shards&&game.save.xp==before.xp&&game.save.seed==before.seed,"Building neither heals nor changes energy, shards, XP or RNG: health="+before.health+"->"+game.save.health+", max="+before.maxHealth+"->"+game.save.maxHealth+", energy="+before.energy+"->"+game.save.energy+", shards="+before.shards+"->"+game.save.shards+", XP="+before.xp+"->"+game.save.xp+", seed="+before.seed+"->"+game.save.seed);
        Key(app,KeyCode.Escape);Check(game.mode=="world","Original Back returns from the project page");ResetView(app,new Point(14,11));
    }
    static void Fill(FrontierRuntime app,int tier){for(int next=1;next<=tier;next++)for(int project=0;project<3;project++)Build(app,project);}
    // No new types are referenced at compile time. Baseline RED must compile
    // successfully and fail on missing world visual changes before reflection.
    static IList Entries(FrontierRuntime app)
    {
        var list=Get<IList>(app,"worldDrawEntries");list.Clear();Call(app,"AddProjectSceneryEntries");
        return list;
    }
    static object Scene(object entry)
    {
        Check(Get<object>(entry,"kind").ToString()=="Project","Dedicated project scenery uses its own sorted entry kind");
        foreach(FieldInfo field in entry.GetType().GetFields(All))if(field.FieldType.Name=="ProjectScenery")return field.GetValue(entry);
        throw new Exception("Dedicated world entry owns its project-scene snapshot");
    }
    static List<object> Scenes(FrontierRuntime app)
    {var result=new List<object>();foreach(object entry in Entries(app)){object scene=Scene(entry);Check(Near(Get<float>(entry,"depth"),Get<Point>(scene,"position").y),"Each project sorts by its real ground anchor");result.Add(scene);}return result;}
    static object Project(FrontierRuntime app,int id,bool camp=false)
    {var found=Scenes(app).Find(s=>Get<int>(s,"project")==id&&Get<bool>(s,"camp")==camp);Check(found!=null,"Paid positive project has its own visible sorted scene: "+id);return found;}
    static List<GUI.TextureCall> ProjectCalls(FrontierRuntime app,object scene)
    {Clear();Call(app,"DrawProjectScenery",scene);return new List<GUI.TextureCall>(GUI.textureCalls);}
    static bool Contains(Rect outer,Rect inner)
    {return inner.x>=outer.x-.002f&&inner.y>=outer.y-.002f&&inner.x+inner.width<=outer.x+outer.width+.002f&&inner.y+inner.height<=outer.y+outer.height+.002f;}

    static void ActualPaymentsAndTierConsequences(FrontierRuntime app)
    {
        Prepare(app);Check(app.SetVisualReviewPosition(new Point(13,13)),"Town witness uses legal unchanged ground");ResetView(app,new Point(14,11));string before=OrderedCommands(app);
        for(int tier=1;tier<=2;tier++)for(int project=0;project<3;project++)
        {
            Build(app,project);string after=OrderedCommands(app);
            Check(after!=before,"A real paid facility tier must visibly change the ordered world, rather than only the project menu: project="+project+" tier="+tier);
            object scene=Project(app,project);Check(Get<int>(scene,"tier")==tier&&!Get<bool>(scene,"camp"),"Rendered project snapshot follows the exact paid tier");
            var direct=ProjectCalls(app,scene);Check(direct.Count>0,"Built facility submits real world artwork");Rect bounds=(Rect)Call(app,"ProjectSceneryBounds",scene);
            Check(direct.TrueForAll(c=>Contains(bounds,c.rect)),"Complete visual bounds enclose every actual atlas and pixel draw");
            if(project==0) Check(bounds.x+bounds.width<((Point)Call(app,"ScreenPoint",new Point(17,6))).x,"The canal stays west of the original north road rather than suggesting a blocking wall across it");
            var world=Ordered(app);Check(direct.TrueForAll(c=>world.Exists(w=>SameCall(c,w))),"Every paid scene draw is actually reached through the sorted production world path");
            before=after;
        }
        Check(app.Game.save.coins==760&&Scenes(app).Count==3,"All six real payments retain original total cost and exactly three tier-II town scenes");
        Console.WriteLine("PASS six actual B/Review/Confirm payments produce individual world-visible tier consequences with exact old material/coin/trust/Amber rewards, complete bounds and sorted actual submissions; NATIVE_UNVERIFIED");
    }

    static string Decor(FrontierRuntime app)
    {var text=new StringBuilder();foreach(Decoration prop in app.Game.Map.decor){Clear();Call(app,"DrawWorldDecoration",prop);text.Append(string.Join("\n",GUI.commands.ToArray()));}return text.ToString();}
    static string GroundCalls(FrontierRuntime app)
    {
        // The new tabletop legitimately reuses the Details atlas. Invoke the
        // unchanged ground pass itself instead of mistaking its furniture UV
        // for removed grass merely because both share one texture resource.
        Clear();var game=app.Game;
        for(int y=0;y<game.Map.rows.Length;y++)for(int x=0;x<game.Map.rows[y].Length;x++)
        {char tile=game.PresentationTile(x,y);Point p=(Point)Call(app,"ScreenPoint",new Point(x,y));Call(app,"DrawSurface",x,y,tile,p);Call(app,"DrawGroundDetail",x,y,tile,p);Call(app,"DrawTerrainEdge",x,y,tile,p);}
        return string.Join("\n",GUI.commands.ToArray());
    }
    static void ScreenAnchor(FrontierRuntime app,Point world,Point pixel){Set(app,"camera",world-new Point((pixel.x-640)/36,(pixel.y-390)/36));}
    static int HeroIndex(FrontierRuntime app,List<GUI.TextureCall> calls)
    {var sheets=Get<Texture2D[]>(app,"playerSheets");return calls.FindIndex(c=>Array.Exists(sheets,s=>ReferenceEquals(s,c.texture)));}
    static WorldObject Find(GameCore game,string kind){var found=Array.Find(game.Map.objects,o=>o.kind==kind);Check(found!=null,"Canonical interaction remains authored: "+kind);return found;}
    static void WalkTo(FrontierRuntime app,string kind)
    {
        var game=app.Game;WorldObject target=Find(game,kind);List<Point> route=game.InteractionPath(target);Check(route.Count>0,"Canonical interaction retains a real full-footprint route: "+kind);
        int frames=0;while(!ReferenceEquals(game.Nearest(),target)&&frames++<5000)
        {
            while(route.Count>0&&(route[0]-game.Position).Length<.14f)route.RemoveAt(0);Check(route.Count>0,"Walking retains an approach waypoint until the original E owns its target");
            Point move=(route[0]-game.Position).Normalized;Input.axes["Horizontal"]=move.x;Input.axes["Vertical"]=-move.y;
            if(move.x>.05f)Input.held.Add(KeyCode.D);if(move.x<-.05f)Input.held.Add(KeyCode.A);if(move.y>.05f)Input.held.Add(KeyCode.S);if(move.y<-.05f)Input.held.Add(KeyCode.W);
            Update(app);Check(game.CanStand(game.Position)&&game.mode=="world","Real held-WASD movement retains mature collision and world mode");
        }
        Check(frames<5000&&ReferenceEquals(game.Nearest(),target),"Real Update walking reaches the same original nearest E: "+kind);
        Key(app,KeyCode.E);Check(game.mode==(kind=="routes"?"map":kind=="townboard"?"camp":"dialog"),"Original E interaction still opens its mature destination: "+kind);game.Close();
    }
    static void DepthCullingWalkingAndMatureGeometry(FrontierRuntime app)
    {
        Prepare(app);var game=app.Game;string originalDecor=Decor(app),originalGround=GroundCalls(app);game.save.projects=new[]{2,2,2};
        Check(Decor(app)==originalDecor&&GroundCalls(app)==originalGround,"Project visuals neither replace authored decoration calls nor erase/rewrite original terrain and grass details");
        foreach(int id in new[]{0,1,2})
        {
            ResetView(app,new Point(14,11));object scene=Project(app,id);Point anchor=Get<Point>(scene,"position");
            foreach(bool inFront in new[]{false,true})
            {
                Point foot=anchor+new Point(0,inFront?.35f:-.35f);Check(game.CanStand(foot)&&app.SetVisualReviewPosition(foot)&&(game.Position-foot).Length<.001f,"Depth witness uses legal feet on each side of a facility");
                var direct=ProjectCalls(app,scene);var calls=Ordered(app);int project=calls.FindIndex(c=>SameCall(c,direct[0])),hero=HeroIndex(app,calls);
                Check(project>=0&&hero>=0&&(inFront?hero>project:hero<project),"Real hero and project draws sort in correct order across the ground line");
                Check(direct.TrueForAll(c=>c.color.a>=.399f&&c.color.a<=1.001f),"Project cover remains bounded and cannot make an actor vanish");
                if(!inFront)Check(direct.Exists(c=>c.color.a<.99f),"A real hero behind the project receives spatial cover relief");
            }
            ScreenAnchor(app,anchor,new Point(640,390));Rect bounds=(Rect)Call(app,"ProjectSceneryBounds",scene);Point point=(Point)Call(app,"ScreenPoint",anchor);
            float left=point.x-bounds.x,right=bounds.x+bounds.width-point.x,top=point.y-bounds.y,bottom=bounds.y+bounds.height-point.y;
            foreach(Point visible in new[]{new Point(-72-right+.1f,390),new Point(1280+72+left-.1f,390),new Point(640,-72-bottom+.1f),new Point(640,800+72+top-.1f)})
            {ScreenAnchor(app,anchor,visible);Check(Scenes(app).Exists(s=>Get<int>(s,"project")==id),"Complete project bounds remain inside the two-tile apron even with their anchor outside it");}
            Set(app,"camera",anchor+new Point(100,100));Check(!Scenes(app).Exists(s=>Get<int>(s,"project")==id),"Actual entry construction culls a completely far facility");
            var far=ProjectCalls(app,scene);var farWorld=Ordered(app);Check(!farWorld.Exists(c=>SameCall(c,far[0])),"Sorted production submission really omits the far project artwork");
        }
        ResetView(app,new Point(14,11));Check(app.SetVisualReviewPosition(new Point(18,15)),"Real town route starts at legal unchanged feet");
        foreach(string kind in new[]{"forge","garden","townboard","cook","routes"})WalkTo(app,kind);
        Console.WriteLine("PASS unchanged authored scenery/terrain/grass, legal front/behind hero depth and cover relief, full-bound four-edge apron retention/far culling, real held-WASD and original E at Noll/An/board/inn/wayfinder; NATIVE_UNVERIFIED");
    }

    static string CoreState(GameCore game)
    {return(string)typeof(RuntimeSceneryCoverChecks).GetMethod("State",All).Invoke(null,new object[]{game});}
    static string Files()
    {if(!Directory.Exists(Application.persistentDataPath))return "";var paths=Directory.GetFiles(Application.persistentDataPath,"*",SearchOption.AllDirectories);Array.Sort(paths,StringComparer.Ordinal);var text=new StringBuilder();foreach(string path in paths)text.Append(path).Append(':').Append(Convert.ToBase64String(File.ReadAllBytes(path))).Append(';');return text.ToString();}
    static string Physics(GameCore game)
    {var text=new StringBuilder();for(float y=2.25f;y<22;y+=.5f)for(float x=2.25f;x<34;x+=.5f)text.Append(game.CanStand(new Point(x,y))?'1':'0');return text.ToString();}
    static void AllCombinationsReadOnlyAndPreparedActions(FrontierRuntime app)
    {
        foreach(bool flying in new[]{false,true})
        {
            Prepare(app);var game=app.Game;Check(app.SetVisualReviewPosition(new Point(13,13)),"Combat witness starts at legal town feet");game.save.weapon=1;game.aim=new Point(1,0);Set(app,"camera",game.Position);
            Key(app,KeyCode.J);var action=game.playerAction;Check(action!=null&&!action.released,"Real J prepares an original bow attack");
            if(flying){int frames=0;while(!action.released&&frames++<50)Update(app);Check(action.released&&game.projectiles.Count>0,"Real Update releases a genuine in-flight bow shot");}
            Key(app,KeyCode.Escape);Check(game.mode=="pause","Actual Escape freezes the accepted attack or flying shot");
            GameCore review=Get<GameCore>(app,"reviewOriginal");Set(app,"reviewOriginal",null);
            try
            {
                Check(SaveStore.Write(game.save,0),"Pure-draw fixture protects actual checksummed save bytes");
                for(int a=0;a<=2;a++)for(int b=0;b<=2;b++)for(int c=0;c<=2;c++)
                {
                    game.save.projects=new[]{a,b,c};Draw(app);var art=Get<PixelArt>(app,"art");int owned=Get<List<Texture2D>>(art,"owned").Count,cache=Get<Dictionary<string,Texture2D>>(art,"cache").Count;
                    string state=CoreState(game),files=Files(),physics=Physics(game);float clock=Get<float>(app,"clock");var matrix=Matrix4x4.TRS(new Vector3(9,17,0),Quaternion.Euler(0,0,13),new Vector3(1.2f,1.2f,1));var tint=new Color(.3f,.4f,.5f,.6f);
                    foreach(EventType type in new[]{EventType.Layout,EventType.Repaint})
                    {Event.current.type=type;GUI.matrix=matrix;GUI.color=tint;GUI.enabled=false;Draw(app);Check(CoreState(game)==state&&Files()==files&&Physics(game)==physics&&ReferenceEquals(game.playerAction,action),"All27 tier tuples preserve the complete Core/save/files/physics/accepted action graph during actual OnGUI");Check(Near(Get<float>(app,"clock"),clock)&&Near(GUI.matrix.m03,matrix.m03)&&Near(GUI.matrix.m13,matrix.m13)&&Same(GUI.color,tint)&&!GUI.enabled,"OnGUI restores caller GUI state and freezes simulation/presentation clocks");}
                    Check(Get<List<Texture2D>>(art,"owned").Count==owned&&Get<Dictionary<string,Texture2D>>(art,"cache").Count==cache,"Repeated project draws allocate no additional owned textures/cache entries");
                    GUI.matrix=Matrix4x4.identity;GUI.color=Color.white;GUI.enabled=true;Event.current.type=EventType.Repaint;
                }
                game.save.projects=new[]{2,2,2};ResetView(app,new Point(14,11));game.mode="world";var scene=Project(app,2);var expected=ProjectCalls(app,scene);
                foreach(bool reduced in new[]{false,true})foreach(int fx in new[]{0,2})foreach(float clock in new[]{0f,7.7f})
                {game.save.reducedMotion=reduced;game.save.effectsQuality=fx;Set(app,"clock",clock);var actual=ProjectCalls(app,scene);Check(actual.Count==expected.Count&&actual.TrueForAll(call=>expected.Exists(e=>SameCall(e,call))),"Permanent project art and cover do not depend on FX, reduced motion, clock, or pause history");}
            }
            finally{Set(app,"reviewOriginal",review);GUI.matrix=Matrix4x4.identity;GUI.color=Color.white;GUI.enabled=true;Event.current.type=EventType.Repaint;}
        }
        Console.WriteLine("PASS all27 tier tuples x prepared/flying real bow actions x Layout/Repaint preserve full Core/save/physics/disk/GUI state, bounded caches, and static low/high FX/reduced-motion/clock-independent artwork; NATIVE_UNVERIFIED");
    }

    static void CaptureScenes(FrontierRuntime app)
    {
        if(folder==null)return;
        foreach(bool rich in new[]{false,true})foreach(int tier in new[]{0,1,2})
        {
            Prepare(app);Fill(app,tier);var game=app.Game;game.save.english=rich;game.save.largeText=true;game.save.effectsQuality=rich?2:0;game.save.reducedMotion=!rich;Screen.width=rich?1920:960;Screen.height=rich?1080:600;Call(app,"Resize");ResetView(app,new Point(14,11));
            string suffix=(rich?"en-rich-1920":"zh-low-960")+"-tier"+tier;Draw(app,"town-"+suffix);
            foreach(int project in new[]{0,1,2})
            {Point[] anchors={new Point(15.25f,6.45f),new Point(4.4f,17.45f),new Point(8.6f,8.65f)};ResetView(app,anchors[project]+new Point(0,-.6f));Draw(app,"facility-"+project+"-"+suffix);}
        }
        foreach(int room in new[]{5,6,7,GameCore.ReachRoom})foreach(int tier in new[]{0,1,2})foreach(bool rich in new[]{false,true})
        {
            Prepare(app,room);app.Game.save.projects[1]=tier;var fire=Find(app.Game,"campfire");Point anchor=new Point(fire.x+1.35f,fire.y+.5f);ResetView(app,anchor+new Point(0,-.6f));app.Game.save.english=rich;app.Game.save.effectsQuality=rich?2:0;app.Game.save.reducedMotion=!rich;Screen.width=rich?1920:960;Screen.height=rich?1080:600;Call(app,"Resize");Draw(app,"camp-"+room+"-tier"+tier+"-"+(rich?"en-rich-1920":"zh-low-960"));
        }
        Check(captures==48,"This execution records24 town/close-up and24 field-camp tier/policy command streams");
    }
    static void LegacyTransitionsCampsAndGarden(FrontierRuntime app)
    {
        foreach(int version in new[]{1,2})
        {
            Prepare(app);Fill(app,2);var game=app.Game;game.PrepareSave();game.save.version=version;Directory.CreateDirectory(Application.persistentDataPath);File.WriteAllText(SaveStore.PathFor(1),JsonUtility.ToJson(game.save));
            string warning;var old=SaveStore.Read(1,out warning);Check(old!=null&&warning==null,"Actual raw v"+version+" SaveStore reads existing project progress");game.Load(old);game.Start();game.enemies.Clear();ResetView(app,new Point(14,11));
            Check(game.save.version==2&&game.save.coins==760&&game.save.projects[0]==2&&game.save.projects[1]==2&&game.save.projects[2]==2&&Scenes(app).Count==3,"Actual migration retains paid project tiers and reconstructs three world consequences");
            game.PrepareSave();Check(SaveStore.Write(game.save,0),"Actual checksum writer accepts existing paid tiers");var saved=SaveStore.Read(0,out warning);Check(saved!=null&&warning==null&&saved.projects[0]==2&&saved.projects[1]==2&&saved.projects[2]==2,"Checksum roundtrip retains all three paid tiers");
            Check(!JsonUtility.ToJson(saved).Contains("projectScenery"),"Presentation adds no transient save fields");
        }
        foreach(int room in new[]{5,6,7,GameCore.ReachRoom})foreach(int tier in new[]{0,1,2})
        {
            Prepare(app,room);var game=app.Game;game.save.projects[1]=tier;var fire=Find(game,"campfire");ResetView(app,new Point(fire.x,fire.y));var scenes=Scenes(app);
            Check(scenes.Count==(tier==0?0:1),"Only an earned waystation tier appears at each present mature field camp");
            if(tier>0)Check(Get<bool>(scenes[0],"camp")&&Get<int>(scenes[0],"project")==1&&Get<int>(scenes[0],"tier")==tier,"Field camp uses the exact saved waystation tier, not another facility");
            string state=CoreState(game);Draw(app);Check(CoreState(game)==state,"Field scenery cannot grant daily tea/rest/heal or otherwise mutate the camp model");
        }
        foreach(int room in new[]{1,2,3,4,8,9,GameCore.ArchiveRoom})
        {Prepare(app,room);app.Game.save.projects=new[]{2,2,2};Check(Scenes(app).Count==0,"No Emberfall facilities or field-camp visuals leak into maps without their authored host");}
        Prepare(app);var town=app.Game;town.save.projects=new[]{2,2,2};town.save.flags.Add("ally_1");town.save.flags.Add("ally_3");
        foreach(string stage in new[]{"none","watergarden_accept","watergarden_drawn","watergarden_watered","watergarden_complete"})
        {
            if(stage!="none")town.save.flags.Add(stage);
            foreach(float growth in new[]{-1f,0f,60f})
            {
                town.save.totalTime=100;town.save.plantedAt=growth<0?-1:100-growth;ResetView(app,new Point(17,6));var garden=Array.Find(town.Map.decor,d=>d.kind=="garden");
                Clear();Call(app,"DrawWorldDecoration",garden);var mature=new List<GUI.TextureCall>(GUI.textureCalls);Draw(app);
                Check(mature.TrueForAll(c=>GUI.textureCalls.Exists(actual=>SameCall(c,actual))),"Original bare/growing/mature garden and optional water-delivery trial bed remain intact beneath new canal consequences");
                Check(Scenes(app).Count==3&&(!town.WaterGardenComplete||town.WaterGardenWatered),"Saved project scenery coexists with all five optional water quest stages");
            }
        }
        CaptureScenes(app);
        Console.WriteLine("PASS raw v1/v2 and checksum paid-tier continuity, original room transitions and all four present field camps/no-host maps, all15 crop/waterquest combinations plus48 actual tier/policy captures when requested; NATIVE_UNVERIFIED");
    }

    public static int Main(string[] args)
    {
        string previous=Application.persistentDataPath,directory=Path.Combine(Path.GetTempPath(),"starfall-project-scenery-"+Guid.NewGuid());Application.persistentDataPath=directory;
        folder=args.Length>0&&args[0]!="-"?args[0]:null;prefix=args.Length>1?args[1]:"qa-project-scenery";var app=new FrontierRuntime();int failed=0;
        try
        {
            Call(app,"Awake");Check(app.Game!=null,"Actual Awake loads the complete shipped campaign");
            foreach(Action<FrontierRuntime> test in new Action<FrontierRuntime>[]{ActualPaymentsAndTierConsequences,DepthCullingWalkingAndMatureGeometry,AllCombinationsReadOnlyAndPreparedActions,LegacyTransitionsCampsAndGarden})
                try{test(app);}catch(Exception error){failed++;Console.WriteLine("FAIL "+test.Method.Name+": "+(error.InnerException??error));}
            app.EndVisualReview();Call(app,"OnDestroy");
        }
        finally{Application.persistentDataPath=previous;Input.Clear();GUI.queuedClick=null;GUI.matrix=Matrix4x4.identity;GUI.color=Color.white;GUI.enabled=true;if(Directory.Exists(directory))Directory.Delete(directory,true);}
        Console.WriteLine("Runtime project scenery: "+(4-failed)+"/4 groups passed; NATIVE_UNVERIFIED");return failed==0?0:1;
    }
}
