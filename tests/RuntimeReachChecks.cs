// Exercise the shipped Update, OnGUI and SaveStore paths with offline Unity
// declarations. Import, native rendering, controller hardware and play feel are
// NATIVE_UNVERIFIED; these checks do not substitute for native Unity playback.
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Starfall;
using UnityEngine;

public static class RuntimeReachChecks
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static string captureFolder;
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static object Call(FrontierRuntime app, string method, params object[] args)
    { return typeof(FrontierRuntime).GetMethod(method, Private).Invoke(app, args); }
    private static void Set(FrontierRuntime app, string field, object value)
    { typeof(FrontierRuntime).GetField(field, Private).SetValue(app, value); }
    private static T Get<T>(FrontierRuntime app, string field)
    { return (T)typeof(FrontierRuntime).GetField(field, Private).GetValue(app); }
    private static void Draw(FrontierRuntime app, string capture = null)
    {
        GUI.commands.Clear(); GUI.textureCalls.Clear(); GUI.buttonCalls.Clear(); Call(app, "OnGUI");
        if (captureFolder != null && capture != null)
        {
            var commands = GUI.commands.ToArray(); var textures = GUI.textureCalls.ToArray(); var buttons = GUI.buttonCalls.ToArray();
            GUI.Export(captureFolder, "qa-reach-" + capture);
            GUI.commands.AddRange(commands); GUI.textureCalls.AddRange(textures); GUI.buttonCalls.AddRange(buttons);
        }
    }
    private static bool HasText(string value)
    { return GUI.commands.Exists(c => c.Contains("\"text\":" + GUI.S(value))); }
    private static void Update(FrontierRuntime app) { Call(app, "Update"); Input.keys.Clear(); }
    private static void Key(FrontierRuntime app, KeyCode key) { Input.keys.Add(key); Update(app); }
    private static void Click(FrontierRuntime app, Rect rect)
    { GUI.queuedClick = rect; Draw(app); Check(!GUI.queuedClick.HasValue, "The displayed production button accepts its click: " + rect.x + "," + rect.y + " mode=" + app.Game.mode + " recordsPage=" + Get<int>(app,"recordsPage")); }
    private static void Position(FrontierRuntime app, Point point)
    {
        Check(app.Game.CanStand(point), "The staged fixture uses exact, full-footprint walkable ground: " + point.x + "," + point.y);
        Check(app.SetVisualReviewPosition(point) && (app.Game.Position - point).Length < .001f, "Visual review must not silently repair staged feet");
        Set(app, "camera", app.Game.Position);
    }
    private static WorldObject Object(GameCore g, string kind)
    { var o = Array.Find(g.Map.objects, x => x.kind == kind); Check(o != null, "The shipped interaction exists: " + kind); return o; }
    private static void Interact(FrontierRuntime app, string kind, bool pad = false)
    {
        var o = Object(app.Game, kind); Position(app, new Point(o.x, o.y - .7f));
        Check(app.Game.Nearest() == o, "E/B targets the intended shipped interaction " + kind);
        Key(app, pad ? KeyCode.JoystickButton1 : KeyCode.E);
    }
    private static void Choose(FrontierRuntime app, string action, string capture = null)
    {
        Check(app.Game.mode == "dialog" && app.Game.conversation != null, "A numeric choice belongs to a displayed production dialogue");
        int index = Array.FindIndex(app.Game.conversation.choices, c => c.action == action);
        Check(index >= 0 && index < 9, "The displayed dialogue exposes the intended action " + action);
        var pages = DialogueLayout.Pages(app.Game.L(app.Game.conversation.text, app.Game.conversation.textEn), app.Game.save.largeText);
        int page = 0;
        do
        {
            Draw(app, capture == null ? null : capture + "-page" + page);
            Check(HasText(pages[page]), "Every authored narrative page is actually drawn before its numbered action");
            if ((bool)Call(app, "DialogueChoicesVisible")) break;
            Key(app, KeyCode.Return); page++;
        } while (page < pages.Count);
        Check(page == pages.Count - 1 && (bool)Call(app, "DialogueChoicesVisible"), "Choices wait for the final complete narrative page");
        Key(app, (KeyCode)((int)KeyCode.Alpha1 + index));
    }
    private static void Prepare(FrontierRuntime app, int room = 1, bool english = true, bool large = false)
    {
        Input.Clear(); GUI.queuedClick = null; Screen.width = 1280; Screen.height = 800; Time.unscaledDeltaTime = .02f;
        app.BeginVisualReview(0, room); app.VisualReviewPaused = false; var g = app.Game;
        if (room == 1) g.enemies.Clear(); // Only isolate the existing entrance chapter, never the authored Reach roster.
        g.save.companion = -1; g.save.english = english; g.save.largeText = large; g.save.showGuidance = true;
        g.save.reducedMotion = true; g.save.effectsQuality = 0; g.save.flags.Add("reach_runtime_mature_story");
        Set(app, "applicationPaused", false); Set(app, "pointerSeen", false); Set(app, "controllerUsed", false);
        Set(app, "combatHudTime", 0f); Set(app, "journalTab", 0); Set(app, "recordsPage", 0); Set(app, "selectedRegion", -1); Call(app, "Resize");
    }
    private static void Enter(FrontierRuntime app)
    {
        Interact(app, "portal11"); var g = app.Game;
        Check(g.save.room == GameCore.ReachRoom && g.ReachVisited && g.save.trackedQuest == "reach", "Real E enters, visits and tracks the appended riverbank");
        Check(g.Map.rows.Length == 48 && g.Map.rows[0].Length == 72 && (g.Position - new Point(6,39)).Length < .001f,
            "The actual entrance uses the larger authored map and its preserved spawn coordinates");
        Check(g.enemies.Count == 7 && g.enemies.TrueForAll(e => g.IsReachEnemyId(e.id) && !e.boss), "Entering starts exactly seven authored ordinary foes");
        int[] kinds = {0,1,2,0,1,2,0}; float[] health = {55,44,85,55,44,85,55};
        foreach (var e in g.enemies) Check(e.kind == kinds[e.id - 76000] && e.hp == health[e.id - 76000] && e.maxHp == health[e.id - 76000],
            "New foes preserve their canonical kind and actual combat health");
        Set(app, "camera", g.Position);
    }
    private static int Walk(FrontierRuntime app, Point destination, string label)
    {
        var g = app.Game; int frames = 0; float startTime = g.save.totalTime;
        while ((g.Position - destination).Length > .17f && frames++ < 5000)
        {
            Check(g.mode == "world" && !g.Dying && g.save.room == GameCore.ReachRoom, "Actual walking remains alive in the riverbank: " + label);
            Point movement = g.DirectionTo(g.Position, destination);
            Input.axes["Horizontal"] = movement.x; Input.axes["Vertical"] = -movement.y; Input.held.Clear();
            if (movement.x > .05f) Input.held.Add(KeyCode.D); if (movement.x < -.05f) Input.held.Add(KeyCode.A);
            if (movement.y > .05f) Input.held.Add(KeyCode.S); if (movement.y < -.05f) Input.held.Add(KeyCode.W);
            if (g.save.health < g.save.maxHealth * .4f && g.save.inventory[6] > 0) Input.keys.Add(KeyCode.F);
            Update(app); Check(g.CanStand(g.Position), "Actual movement preserves the complete hero footprint through " + label);
        }
        Input.axes.Clear(); Input.held.Clear();
        Check(frames < 5000 && (g.Position - destination).Length <= .17f && g.save.totalTime > startTime,
            "Held WASD, real movement collision and Update reach " + label + " without position writes or combat/stat tuning");
        return frames;
    }
    private static void Aim(FrontierRuntime app, Point point)
    {
        Set(app, "camera", app.Game.Position); Point screen = (Point)Call(app, "ScreenPoint", point);
        float scale = Get<float>(app, "scale"), x = Get<float>(app, "offsetX"), y = Get<float>(app, "offsetY");
        Input.mousePosition = new Vector3(x + screen.x * scale, Screen.height - y - screen.y * scale, 0);
    }
    private static bool ClearShot(GameCore g, Point destination)
    {
        Point delta = destination - g.Position; int samples = Math.Max(1, (int)Math.Ceiling(delta.Length / .12f));
        for (int i = 1; i <= samples; i++) if (g.Blocked((g.Position + delta * (i / (float)samples)).x, (g.Position + delta * (i / (float)samples)).y)) return false;
        return true;
    }
    private static void Fight(FrontierRuntime app)
    {
        var g = app.Game; int frames = 0, skills = 0, dodges = 0; Enemy target = null;
        var path = new List<Point>(); float routeClock = 0; Point planned = new Point();
        while (g.enemies.Count > 0 && frames++ < 18000)
        {
            Check(!g.Dying && g.save.room == GameCore.ReachRoom, "The unchanged starter survives actual riverbank combat");
            if (target == null || !g.enemies.Contains(target))
            {
                float nearest = float.MaxValue; target = null;
                foreach (var enemy in g.enemies) { float d = (enemy.Position - g.Position).Length; if (d < nearest) { nearest = d; target = enemy; } }
                path.Clear(); routeClock = 0;
            }
            float distance = (target.Position - g.Position).Length;
            bool contact = distance <= 1.65f && ClearShot(g, target.Position);
            Point movement = new Point();
            if (!contact)
            {
                routeClock -= .02f;
                if (routeClock <= 0 || (planned - target.Position).Length > 1.5f)
                {
                    planned = target.Position; path = g.NavigationPath(planned); routeClock = .6f;
                    Check(path.Count > 0 || distance < 1.75f, "A live riverbank foe has a real walking approach around the water");
                }
                while (path.Count > 0 && (path[0] - g.Position).Length < .16f) path.RemoveAt(0);
                movement = path.Count > 0 ? (path[0] - g.Position).Normalized : g.DirectionTo(g.Position, target.Position);
            }
            if (target.state == 1 && target.kind == 2 && (g.Position - target.target).Length < 2)
            { movement = (g.Position - target.target).Length > .05f ? (g.Position - target.target).Normalized : new Point(-1, 0); if (g.dashCooldown <= 0) { Input.keys.Add(KeyCode.Space); dodges++; } }
            Input.axes["Horizontal"] = movement.x; Input.axes["Vertical"] = -movement.y; Input.held.Clear();
            if (contact) Input.held.Add(KeyCode.J); Aim(app, target.Position);
            if (contact && g.skillCooldown <= 0 && g.save.energy >= 40) { Input.keys.Add(KeyCode.Q); skills++; }
            if (g.save.health < g.save.maxHealth * .6f && g.save.inventory[6] > 0) Input.keys.Add(KeyCode.F);
            Update(app);
        }
        Input.axes.Clear(); Input.held.Clear();
        Check(frames < 18000 && !g.Dying && g.enemies.Count == 0 && skills > 0 && dodges > 0,
            "Actual J/Q/Space clears all seven foes using normal resources, damage, AI and clocks");
        foreach (int id in new[]{76000,76001,76002,76003,76004,76005,76006}) Check(g.Has("reach_defeated_" + id), "Only genuine enemy deaths register the authored persistent defeat " + id);
        Check(g.save.kills[11] == 7 && !g.Has("boss_11"), "Actual ordinary kills credit the appended room and never fabricate a chapter Boss");
        Console.WriteLine("  Reach combat: " + frames + " Update frames; Q=" + skills + ", Space=" + dodges + ", health=" + g.save.health);
    }
    private static void Views(FrontierRuntime app, string suffix)
    {
        var g = app.Game; Update(app); QuestGuide q = g.GetQuest("reach"); Draw(app, "world" + suffix);
        Check(g.save.trackedQuest == "reach" && g.NavigationTarget().HasValue, "The actual optional survey has its current gold navigation objective");
        Point? target = Get<Point?>(app, "routeGoal"); Check(target.HasValue && (target.Value - g.NavigationTarget().Value).Length < .001f, "Production Update refreshes the route toward the nearest unsurveyed lamp");
        Check(Get<List<Point>>(app, "route").Count > 0, "The actual golden route carries reachable walking points around the river");
        Key(app, KeyCode.Tab); Draw(app); int quest = Array.IndexOf(GameCore.QuestIds, "reach");
        Check(quest == 13 && GameCore.QuestIds.Length >= 14, "The riverbank survey retains its fourteenth row; later quests append after the mature prefix");
        float spacing = Math.Min(46, 464f / 18); Click(app, new Rect(279,226 + quest * spacing,321,spacing - 7)); Draw(app, "journal" + suffix);
        Check(Get<int>(app, "selectedQuest") == quest && HasText(g.L("下一步：", "Next: ") + q.next), "The actual fourteenth journal row displays its current survey objective");
        Click(app, new Rect(927,647,263,31)); Draw(app, "map-local" + suffix);
        Check(g.mode == "map" && Get<int>(app, "selectedRegion") == 11 && Get<int>(app, "mapPage") == 1, "Actual destination opens riverbank on the second map page");
        Key(app, KeyCode.Escape); Check(g.mode == "world", "Escape closes the appended quest/map views and restores the real world");
    }
    private static void SourceArt(FrontierRuntime app)
    {
        Draw(app); var g = app.Game; Texture2D monster = Get<Texture2D>(app, "monsterSheet"), walk = Get<Texture2D>(app, "monsterWalkSheet"), vertical = Get<Texture2D>(app, "enemyArcherVertical");
        foreach (var e in g.enemies)
        {
            if ((e.Position - g.Position).Length > 8) continue;
            float row = (2 - e.kind) / 3f;
            bool body = GUI.textureCalls.Exists(c => c.texture == monster && Math.Abs(Math.Abs(c.uv.width)-1f/6) < .001f && Math.Abs(c.uv.y-row)<.001f)
                || GUI.textureCalls.Exists(c => c.texture == walk && Math.Abs(Math.Abs(c.uv.width)-.25f)<.001f && Math.Abs(c.uv.y-row)<.001f)
                || e.kind==1 && GUI.textureCalls.Exists(c => c.texture==vertical && Math.Abs(Math.Abs(c.uv.width)-1f/3)<.001f);
            Check(body, "Real new guards keep accepted hunter/heavy/archer source-art rows");
        }
    }
    public static void ReachEntranceAndContinuousMovement(FrontierRuntime app)
    {
        Prepare(app); var g = app.Game; g.save.flags.Remove("core_1"); Interact(app, "portal11");
        Check(g.save.room == 1 && !g.ReachVisited && g.GetQuest("reach").locked, "Real E before the first core leaves the existing forest intact and does not invent riverbank progress");
        g.save.flags.Add("core_1"); Enter(app); int frames = 0;
        foreach (var p in new[]{new Point(27,39),new Point(35,39),new Point(49,39),new Point(49,17),new Point(40,17),new Point(40,10),new Point(27,10),new Point(27,33)})
            frames += Walk(app,p,"both fixed crossings via " + p.x + "," + p.y);
        Check(!g.ReachBridgeOpen && !g.ReachRootsOpen && g.Blocked(31.5f,29.5f), "North and south routes work while the centre shortcut and secret remain genuinely closed");
        Key(app,KeyCode.E); Choose(app,"reach_bridge","bridge-real-walk"); Check(g.ReachBridgeOpen, "E and the actual numbered winch choice open the central shortcut at its authored location");
        frames += Walk(app,new Point(27,29),"western shortcut approach"); frames += Walk(app,new Point(35,29),"the newly opened centre bridge");
        Check(g.PresentationTile(31,29)=='=' && !g.Blocked(31.5f,29.5f), "Rendered shortcut and collision agree after its actual input action");
        Console.WriteLine("PASS actual gated E forest entry, canonical seven ordinary foes and " + frames + " held-WASD Update frames across fixed north/south routes and the repaired central shortcut; NATIVE_UNVERIFIED");
    }
    public static void ReachInteractionsActualCombatAndOnceEconomy(FrontierRuntime app)
    {
        Prepare(app); Enter(app); var g = app.Game; Views(app,"-entry");
        Interact(app,"reachBoard"); Choose(app,"reach_claim","board-incomplete"); Check(!g.ReachComplete,"A genuine premature board report cannot pay before the three surveys");
        Interact(app,"reachPost0"); Choose(app,"reach_post0","lamp-guarded"); Check(!g.ReachPostRecorded(0),"A real numeric survey action waits for actual nearby enemies to be cleared"); SourceArt(app);
        Interact(app,"reachBridge"); int coins=g.save.coins,crystals=g.save.inventory[1]; Choose(app,"reach_bridge");
        Check(g.ReachBridgeOpen && g.save.coins==coins && g.save.inventory[1]==crystals,"The real winch opens its shortcut without silently charging resources");
        Interact(app,"reachRoot"); Key(app,KeyCode.Escape); Check(g.mode=="world"&&!g.ReachRootsOpen,"Escape defers the actual root dialogue safely"); Key(app,KeyCode.R);
        Check(g.ReachRootsOpen,"Real R at the shipped root uses core one's existing exploration power");
        Fight(app);
        for(int i=0;i<3;i++){Interact(app,"reachPost"+i,i==1);Choose(app,"reach_post"+i,"lamp-clear"+i);Check(g.ReachPostRecorded(i),"Actual E/B and numeric choices record clear lamp "+i);}
        coins=g.save.coins;int shards=g.save.shards,stew=g.save.inventory[6];Interact(app,"reachCache");Choose(app,"reach_cache","cache-first");
        Check(g.ReachCacheRecovered&&g.save.coins==coins+20&&g.save.shards==shards+2&&g.save.inventory[6]==stew+1,"The real cleared hidden cache pays exactly20 coins/two shards/one stew once");
        Interact(app,"reachCache");Choose(app,"close");Check(g.save.coins==coins+20&&g.save.shards==shards+2&&g.save.inventory[6]==stew+1,"Actual revisiting cannot duplicate the cache payout");
        coins=g.save.coins;shards=g.save.shards;int points=g.save.skillPoints;Interact(app,"reachBoard");Choose(app,"reach_claim","report-first");
        Check(g.ReachComplete&&g.save.coins==coins+40&&g.save.shards==shards+4&&g.save.skillPoints==points+1&&g.Has("reach_runtime_mature_story")&&g.Has("core_1"),"The actual three-lamp report pays once and retains existing story/core identity");
        Interact(app,"reachBoard");Choose(app,"close","report-repeat");Check(g.save.coins==coins+40&&g.save.shards==shards+4&&g.save.skillPoints==points+1,"A completed board interaction cannot repeat its exact reward");
        Prepare(app);Enter(app);g=app.Game;Interact(app,"reachBridge");Position(app,new Point(6,39));Choose(app,"reach_bridge");Check(!g.ReachBridgeOpen,"A stale numeric winch action rechecks the actual player proximity");
        Interact(app,"reachRoot");g.save.flags.Remove("core_1");Choose(app,"reach_roots");Check(!g.ReachRootsOpen,"A stale root choice rechecks the original core power");
        g.save.flags.Add("core_1");Interact(app,"reachBridge");g.Enter(0);Key(app,KeyCode.Alpha1);Check(g.save.room==0&&!g.ReachBridgeOpen,"A numeric key after travel cannot operate a discarded foreign-map dialogue");
        Console.WriteLine("PASS guarded E/B/numeric post/board/bridge/cache, R root power and Escape defer, actual J/Q/Space ordinary kills and accepted source art, exact once-only survey/cache payouts and stale-location/core/travel protection; NATIVE_UNVERIFIED");
    }
    public static void ReachGuiMapsRecordsAndPresentationPolicies(FrontierRuntime app)
    {
        for(int language=0;language<2;language++)for(int large=0;large<2;large++)for(int policy=0;policy<2;policy++)
        {
            Prepare(app,1,language==1,large==1);var g=app.Game;Screen.width=policy==0?960:1920;Screen.height=policy==0?600:1080;
            g.save.effectsQuality=policy==0?0:2;g.save.reducedMotion=policy==0;Call(app,"Resize");
            string suffix=(language==1?"-en":"-zh")+(large==1?"-large":"")+(policy==0?"-960-minimal":"-1920-rich");
            g.TrackQuest("reach");Key(app,KeyCode.M);Draw(app,"map-first"+suffix);
            Check(Get<int>(app,"mapPage")==0&&GUI.buttonCalls.Exists(b=>b.rect.x==289&&b.rect.y==224),"The mature first-page region buttons retain their clickable geometry");
            Click(app,new Rect(685,633,149,24));Draw(app,"map-second"+suffix);
            foreach(int id in new[]{9,10,11})Check(HasText(g.L(g.data.maps[id].name,g.data.maps[id].en)),"Second page retains mature inn/archive and displays appended riverbank");
            Click(app,new Rect(667,224,156,80));Draw(app,"map-remote"+suffix);Check(Get<int>(app,"selectedRegion")==11,"The real appended map card selects the remote larger destination");
            Click(app,new Rect(888,616,306,47));Check(g.save.room==11&&g.ReachVisited,"Actual remote-card fast travel enters the optional map");Set(app,"camera",g.Position);Views(app,"-entry"+suffix);
            Interact(app,"reachBoard");Choose(app,"reach_track","board"+suffix);Check(g.save.trackedQuest=="reach","Actual optional board choice tracks its own survey");
            foreach(int lore in new[]{20,21}){Interact(app,"history"+lore);Choose(app,"close","record"+lore+suffix);Check(g.Has("history_"+lore),"Actual E records shipped lore "+lore);}
            Key(app,KeyCode.Tab);Draw(app);Click(app,new Rect(517,172,222,37));Draw(app,"records-first"+suffix);
            Check(Get<int>(app,"journalTab")==1,"The real records tab opens the mature codex");
            Click(app,new Rect(997,650,220,38));Draw(app,"records-second"+suffix);Click(app,new Rect(997,650,220,38));Draw(app,"records-third"+suffix);
            Check(Get<int>(app,"recordsPage")==2&&HasText(g.L(g.data.lore[20].title,g.data.lore[20].en))&&HasText(g.L(g.data.lore[21].title,g.data.lore[21].en)),"Actual records paging exposes both new notes after all twenty mature entries");
            Click(app,new Rect(341,243,382,44));Choose(app,"journal","codex20"+suffix);Check(g.mode=="journal","Closing an actual reread returns to its real journal parent");
            Click(app,new Rect(818,243,382,44));Choose(app,"journal","codex21"+suffix);Check(g.mode=="journal","The other new record also rereads through the mature codex UI");Key(app,KeyCode.Escape);
            Position(app,new Point(44,12));Draw(app,"roots-closed"+suffix);
            GUI.commands.Clear();GUI.textureCalls.Clear();GUI.buttonCalls.Clear();Call(app,"DrawReachWorld");
            Check(GUI.textureCalls.FindAll(c=>c.texture.name=="resource:Frontier/Art/FieldProps").Count==9,"Every closed root collision cell keeps its authored field-prop blocker at each graphics policy");
            Key(app,KeyCode.R);Draw(app,"roots-open"+suffix);Check(g.ReachRootsOpen&&!g.Blocked(46.5f,12.5f),"Actual R opens both visible secret path and collision under all policies");
            Position(app,new Point(27,33));Interact(app,"reachBridge");Choose(app,"reach_bridge","bridge"+suffix);
            GUI.commands.Clear();GUI.textureCalls.Clear();GUI.buttonCalls.Clear();Call(app,"DrawReachWorld");
            Check(GUI.textureCalls.FindAll(c=>c.texture.name=="resource:Frontier/Art/Terrain").Count==12,"The actual repaired bridge overlays exactly its twelve collision cells using accepted boardwalk art");
            Draw(app,"bridge-open"+suffix);Position(app,new Point(61,13));Draw(app,"east-warning"+suffix);SourceArt(app);
        }
        Console.WriteLine("PASS retained fourteenth journal destination/gold routes, mature and appended map-page clicks/remote travel, paged new lore20/21 and codex return, nine root blockers/twelve boardwalk cells and accepted guards across Chinese/English, normal/large text,960/1920 and effects/motion policies; NATIVE_UNVERIFIED");
    }
    private static SaveData RoundTrip(GameCore g,int slot)
    {
        g.PrepareSave();Check(SaveStore.Write(g.save,slot),"Production save envelope writes the actual current progress");string issue;SaveData value=SaveStore.Read(slot,out issue);
        Check(value!=null&&issue==null,"Production checksum validates real saved JSON");return value;
    }
    public static void ReachSaveStoreLegacyOwnershipAndReset(FrontierRuntime app)
    {
        Prepare(app);Enter(app);var g=app.Game;Enemy hunter=g.enemies.Find(e=>e.id==76000);float health=hunter.hp;
        Position(app,hunter.Position+new Point(-1.5f,0));Aim(app,hunter.Position);Input.held.Add(KeyCode.J);
        for(int frame=0;frame<80&&hunter.hp==health;frame++)Update(app);Input.held.Clear();Check(hunter.hp>0&&hunter.hp<health,"Real J contact creates the partial-HP save fixture without health writes");
        float remaining=hunter.hp;SaveData saved=RoundTrip(g,0);g.Load(saved);g.Start();Check(g.save.room==11&&g.enemies.Count==7&&g.enemies.Find(e=>e.id==76000).hp==remaining,"Actual same-room checksum roundtrip preserves canonical partial guard HP");
        int coins=g.save.coins;string story="reach_runtime_mature_story";saved=RoundTrip(g,1);
        saved.worldEnemies=new[]{new EnemyRecord{id=76000,kind=0,boss=true,x=17,y=20,hp=999,maxHp=999},new EnemyRecord{id=123,kind=3,boss=true,x=6,y=39,hp=999,maxHp=999},new EnemyRecord{id=76001,kind=2,boss=false,x=19,y=15,hp=999,maxHp=999}};
        Check(SaveStore.Write(saved,1),"Real JSON can carry a hostile snapshot fixture for ownership validation");string issue;saved=SaveStore.Read(1,out issue);Check(saved!=null&&issue==null,"The hostile snapshot still has a valid envelope checksum");g.Load(saved);g.Start();
        Check(g.enemies.Count==7&&g.enemies.TrueForAll(e=>g.IsReachEnemyId(e.id)&&!e.boss)&&g.enemies.Find(e=>e.id==76001).kind==1&&g.enemies.Find(e=>e.id==76000).maxHp==55,"Foreign Boss and mismatched kind/HP claims cannot replace the region's exact ordinary roster");
        saved=RoundTrip(g,2);saved.x=52;saved.y=7;saved.flags.Remove("reach_roots");Check(SaveStore.Write(saved,2),"Production envelope writes the stranded closed-secret fixture");saved=SaveStore.Read(2,out issue);Check(saved!=null&&issue==null,"Closed-secret fixture validates its real checksum");g.Load(saved);g.Start();
        Check((g.Position-new Point(6,39)).Length<.001f&&!g.ReachRootsOpen&&g.Has(story)&&g.save.coins==coins,"A closed-secret load resets isolated feet to the safe entrance without unlocking roots, dropping progress or changing resources");
        int generation=g.WorldGeneration;Key(app,KeyCode.M);Draw(app);Key(app,KeyCode.Escape);Check(g.WorldGeneration==generation,"Ordinary menu open/close cannot reset the encounter or refill existing guard HP");
        g.Enter(1);g.enemies.Clear();Enter(app);Check(g.enemies.Count==7&&!g.ReachComplete&&g.Has(story),"Travel re-enters the canonical region and retains existing optional story progress");
        Key(app,KeyCode.Escape);float time=g.save.totalTime;for(int frame=0;frame<20;frame++)Update(app);Check(g.mode=="pause"&&g.save.totalTime==time,"Real pause freezes the riverbank simulation");Key(app,KeyCode.Escape);
        Position(app,new Point(17,20));g.save.health=1;g.invulnerable=0;
        for(int frame=0;frame<1000&&!g.Dying;frame++)Update(app);Check(g.Dying&&g.Has("recovery_pending"),"Actual authored enemy AI damage enters the mature death/recovery path");saved=RoundTrip(g,0);g.Load(saved);g.Start();
        Check(g.save.room==0&&!g.Has("recovery_pending")&&g.Has(story)&&g.save.health==g.save.maxHealth,"Saving during the real death pose resumes safely in Town with existing story intact");
        for(int version=1;version<=2;version++)
        {
            var legacy=new SaveData{version=version,room=9,stage=1,x=11,y=16,coins=137,kills=new[]{1,2,3,4,5,6,7,8,9,10,11},explored=new[]{11,12,13,14,15,16,17,18,19,20,21},flags=new List<string>{"intro","core_1","watch_complete","signal_complete","sparring_complete","guild_letter_reviewed"}};
            File.WriteAllText(SaveStore.PathFor(2),JsonUtility.ToJson(legacy));saved=SaveStore.Read(2,out issue);Check(saved!=null&&issue==null,"Production legacy reader accepts eleven-slot v"+version+" JSON");g.Load(saved);g.Start();
            Check(g.save.room==9&&g.save.coins==137&&g.save.kills.Length==12&&g.save.explored.Length==12&&!g.ReachVisited&&!g.ReachComplete,"Legacy migration retains the mature inn and appends empty riverbank counters with no invented discoveries");
            for(int i=0;i<11;i++)Check(g.save.kills[i]==i+1&&g.save.explored[i]==i+11,"Every old eleven-map kill/exploration counter retains its exact prefix");
            Check(g.save.kills[11]==0&&g.save.explored[11]==0&&g.Has("watch_complete")&&g.Has("signal_complete")&&g.Has("sparring_complete")&&g.Has("guild_letter_reviewed"),"Both historical save versions preserve mature optional stories and clean appended state");
            Draw(app,"legacy-v"+version+"-inn");
        }
        Console.WriteLine("PASS actual SaveStore checksum/JSON, real-J partial guard HP, foreign-Boss/kind ownership, closed-secret safe-foot recovery, menu/travel/pause/death resets and exact eleven-map v1/v2 save-prefix compatibility; NATIVE_UNVERIFIED");
    }
    public static int Main(string[] args)
    {
        string previous=Application.persistentDataPath;string directory=Path.Combine(Path.GetTempPath(),"starfall-reach-check-"+Guid.NewGuid());
        Application.persistentDataPath=directory;captureFolder=args.Length>0?args[0]:null;if(captureFolder!=null)Directory.CreateDirectory(captureFolder);var app=new FrontierRuntime();int failed=0;
        try
        {
            Call(app,"Awake");Check(app.Game!=null,"Actual Awake loads the shipped full campaign");
            foreach(Action<FrontierRuntime> test in new Action<FrontierRuntime>[]{ReachEntranceAndContinuousMovement,ReachInteractionsActualCombatAndOnceEconomy,ReachGuiMapsRecordsAndPresentationPolicies,ReachSaveStoreLegacyOwnershipAndReset})
            {try{test(app);}catch(Exception error){failed++;Console.WriteLine("FAIL "+test.Method.Name+": "+(error.InnerException??error).Message);}}
            app.EndVisualReview();Call(app,"OnDestroy");
        }
        finally{Application.persistentDataPath=previous;Input.Clear();GUI.queuedClick=null;if(Directory.Exists(directory))Directory.Delete(directory,true);}
        Console.WriteLine("Runtime Reedwater Reach: "+(4-failed)+"/4 groups passed; NATIVE_UNVERIFIED");return failed==0?0:1;
    }
}
