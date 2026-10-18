using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;

namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        private static GameCore LandmarkGame(Campaign data, int profession = 0)
        { var g = Fresh(data, profession); g.SkipTutorial(); g.save.explored[0] = 16777215; g.mode = "landmarks"; return g; }
        private static int LandmarkFlags(GameCore g)
        { return g.save.flags.FindAll(f => f.StartsWith("landmark_pin:", StringComparison.Ordinal)).Count; }
        private static bool LandmarkTarget(GameCore g, Point target)
        { Point? current = g.NavigationTarget(); return current.HasValue && (current.Value-target).Length < .001f; }
        private static string LandmarkDescribe(object value)
        {
            if (value == null) return "null";
            if (value is string) return "s:" + value;
            Type type = value.GetType();
            if (type.IsPrimitive || type.IsEnum) return Convert.ToString(value, CultureInfo.InvariantCulture);
            var builder = new StringBuilder(); IEnumerable collection = value as IEnumerable;
            if (collection != null) { foreach (object item in collection) builder.Append("[").Append(LandmarkDescribe(item)).Append("]"); }
            else foreach (FieldInfo field in type.GetFields(BindingFlags.Public|BindingFlags.Instance))
                builder.Append(field.Name).Append("=").Append(LandmarkDescribe(field.GetValue(value))).Append(";");
            return builder.ToString();
        }

        public static void LandmarksProjectOnlyExploredPresentNamedNonresourceObjectsInAuthoredOrder(Campaign data)
        {
            var g = LandmarkGame(data); g.save.explored[0] = 0;
            Check(g.DiscoveredLandmarks().Count == 0, "An unexplored town cannot reveal residents or services through the landmark list");
            WorldObject cook = Object(g,"cook");
            int bit = (int)(cook.x*6/g.Map.rows[0].Length) + (int)(cook.y*4/g.Map.rows.Length)*6;
            g.save.explored[0] = 1 << bit;
            Check(g.DiscoveredLandmarks().Contains(cook), "A discovered sector exposes its actual named kitchen");
            Check(!g.DiscoveredLandmarks().Contains(Object(g,"forge")), "Knowing a kitchen sector does not disclose the distant forge sector");
            g.save.explored[0] = 16777215; List<WorldObject> found = g.DiscoveredLandmarks(); int cursor = 0;
            foreach (WorldObject obj in g.Map.objects)
                if (obj.kind != "wood" && obj.kind != "herb" && obj.kind != "ore" && g.IsObjectPresent(obj))
                    Check(cursor < found.Count && ReferenceEquals(found[cursor++],obj), "Landmarks retain canonical references and original authored ordering");
            Check(cursor == found.Count && !found.Contains(Object(g,"returnscout")), "A scout who has not returned cannot leak into the discovered list");
            g.Enter(1); g.save.explored[1] = 16777215;
            foreach (WorldObject obj in g.DiscoveredLandmarks())
                Check(obj.kind != "wood" && obj.kind != "herb" && obj.kind != "ore", "Gathering nodes remain outside the useful landmark list");
        }

        public static void LandmarksRejectCopiedForeignMalformedAndStalePinCallbacks(Campaign data)
        {
            var g = LandmarkGame(data); WorldObject cook = Object(g,"cook");
            var copy = new WorldObject { id=cook.id,kind=cook.kind,name=cook.name,en=cook.en,x=cook.x,y=cook.y };
            Check(!g.PinLandmark(copy) && !g.PinLandmark(null) && !g.PinLandmark(data.maps[9].objects[1]), "Only a real current-map object can authorize a pin");
            Check(g.PinLandmark(cook) && ReferenceEquals(g.PinnedLandmark,cook) && LandmarkFlags(g) == 1, "An explicit valid choice records exactly one canonical destination");
            string flags = string.Join("|",g.save.flags); int revision = g.revision;
            Check(!g.PinLandmark(cook) && g.revision == revision && string.Join("|",g.save.flags) == flags, "Choosing the same pin is a genuine no-op");
            foreach (string mode in new[]{"world","map","journal","dialog","pause","inventory","plans","title"})
            {
                g.mode = mode; Check(!g.PinLandmark(Object(g,"forge")) && !g.ClearLandmarkPin() && string.Join("|",g.save.flags) == flags,
                    "Stale landmark callbacks cannot mutate from unrelated mode " + mode);
            }
            g.mode = "landmarks"; g.practicing = true;
            Check(!g.PinLandmark(Object(g,"forge")) && !g.ClearLandmarkPin() && g.PinnedLandmark == null && g.HasLandmarkPin, "Training suppresses, rather than deletes, a real-world pin");
            g.practicing = false; g.deathTime = .3f;
            Check(!g.PinLandmark(Object(g,"forge")) && !g.ClearLandmarkPin() && g.PinnedLandmark == null && g.HasLandmarkPin, "Defeat cannot edit a saved pin");
            g.deathTime = 0; g.save.explored[0] = 0;
            Check(!g.PinLandmark(Object(g,"forge")) && g.PinnedLandmark == null, "A stale object cannot bypass the exploration requirement");
        }

        public static void LandmarksValidateCanonicalMetadataAndUnambiguousSavedIds(Campaign data)
        {
            var g = LandmarkGame(data); WorldObject[] original = g.Map.objects;
            try
            {
                foreach (WorldObject invalid in new[]{
                    new WorldObject{id="",kind="cook",name="Named",x=8,y=17},
                    new WorldObject{id="bad:id",kind="cook",name="Named",x=8,y=17},
                    new WorldObject{id="bad",kind="cook",x=8,y=17},
                    new WorldObject{id="bad",kind="",name="Named",x=8,y=17},
                    new WorldObject{id="bad",kind="cook",name="Named",x=float.NaN,y=17},
                    new WorldObject{id="bad",kind="cook",name="Named",x=8,y=float.PositiveInfinity},
                    new WorldObject{id="bad",kind="cook",name="Named",x=-1,y=17},
                    new WorldObject{id="bad",kind="cook",name="Named",x=36,y=17},
                    new WorldObject{id="bad",kind="cook",name="Named",x=8,y=24}})
                {
                    var objects = new List<WorldObject>(original); objects.Add(invalid); g.Map.objects = objects.ToArray();
                    Check(!g.DiscoveredLandmarks().Contains(invalid) && !g.PinLandmark(invalid), "Malformed named map metadata cannot become a destination: " + invalid.id);
                }
                WorldObject actual = original[0]; var duplicate = new WorldObject{id=actual.id,kind=actual.kind,name=actual.name,en=actual.en,x=actual.x+1,y=actual.y};
                var both = new List<WorldObject>(original); both.Add(duplicate); g.Map.objects=both.ToArray();
                Check(!g.PinLandmark(actual) && !g.PinLandmark(duplicate) && !g.DiscoveredLandmarks().Contains(actual), "An ambiguous stable ID cannot select or persist either object");
            }
            finally { g.Map.objects = original; }
        }

        public static void LandmarksKeepQuestTutorialPracticeAndAbyssNavigationOwnership(Campaign data)
        {
            var g = LandmarkGame(data); WorldObject cook = Object(g,"cook"); string tracked = g.save.trackedQuest;
            Point? original = g.NavigationTarget(); Check(original.HasValue, "The mature town quest provides its original route");
            Check(g.PinLandmark(cook) && LandmarkTarget(g,new Point(cook.x,cook.y)) && g.save.trackedQuest == tracked, "A personal route never swaps the real tracked quest");
            g.save.tutorialBits = 7;
            Check(g.TutorialStep == 3 && LandmarkTarget(g,new Point(cook.x,cook.y)), "An explicit pin can guide an already-informed player without completing the tutorial");
            g.practicing = true; g.enemies.Clear(); var dummy = new Enemy(73,0,12,14,50); g.enemies.Add(dummy);
            Check(LandmarkTarget(g,dummy.Position), "The original practice target retains absolute route ownership");
            g.practicing=false; g.enemies.Clear(); Check(g.ClearLandmarkPin() && !g.HasLandmarkPin && LandmarkTarget(g,new Point(11,14)), "Clearing returns control to the unchanged mentor tutorial");
            g.SkipTutorial(); Check(LandmarkTarget(g,original.Value) && !g.ClearLandmarkPin(), "Clearing an absent pin is a no-op and leaves original quest navigation intact");
            g.PinLandmark(cook); g.save.frontierEnding=1; g.Close(); g.StartAbyss(0); g.mode="landmarks";
            Check(g.InAbyss && g.HasLandmarkPin && g.PinnedLandmark==null && g.DiscoveredLandmarks().Count==0 && LandmarkTarget(g,new Point(31,14)), "Seeded abyss objectives cannot be replaced by a static-map pin");
            Check(!g.PinLandmark(g.Map.objects[0]) && !g.ClearLandmarkPin(), "Abyss menus cannot replace or erase the stored surface destination");
        }

        public static void LandmarksPersistThroughTravelDefeatLegacyLoadsAndResetForNewJourneys(Campaign data)
        {
            for(int version=1;version<=2;version++) for(int profession=0;profession<3;profession++)
            {
                var g=LandmarkGame(data,profession); WorldObject cook=Object(g,"cook"); Check(g.PinLandmark(cook),"Each starter may explicitly choose the kitchen");
                string tracked=g.save.trackedQuest; g.Close(); g.Enter(1);
                Check(g.HasLandmarkPin&&g.PinnedLandmark==null&&g.save.trackedQuest==tracked,"Travelling keeps the reference dormant without redirecting a foreign map");
                SaveData checkpoint=CloneSave(g.save);checkpoint.version=version;
                var loaded=new GameCore(data,checkpoint);loaded.Start();loaded.Close();
                Check(loaded.HasLandmarkPin&&loaded.PinnedLandmark==null&&loaded.save.profession==profession,"A v"+version+" checkpoint retains only the earned dormant pin");
                loaded.Enter(0);Check(ReferenceEquals(loaded.PinnedLandmark,Object(loaded,"cook")),"Returning resolves the saved ID to this map's original canonical object");
                loaded.invulnerable=0;loaded.save.health=1;loaded.projectiles.Add(new Projectile{position=loaded.Position,damage=999,source="shot"});loaded.Tick(.02f,new Point());
                Check(loaded.Dying&&loaded.HasLandmarkPin&&loaded.PinnedLandmark==null,"A real lethal contact temporarily suppresses the pin without deleting it");
                Advance(loaded,1.2f);Check(!loaded.Dying&&loaded.HasLandmarkPin&&ReferenceEquals(loaded.PinnedLandmark,Object(loaded,"cook")),"The original inn recovery restores local navigation without a new reward");
                loaded.Load(new SaveData{version=version});loaded.Start();loaded.Close();
                Check(!loaded.HasLandmarkPin&&loaded.PinnedLandmark==null,"Loading a different old save invalidates the previous world's private pin cache");
                loaded.save.explored[0]=16777215;loaded.mode="landmarks";loaded.PinLandmark(Object(loaded,"forge"));loaded.NewGame(profession);loaded.Close();
                Check(!loaded.HasLandmarkPin&&LandmarkFlags(loaded)==0,"Starting a fresh journey never inherits another journey's pin");
            }
        }

        public static void LandmarksNormalizeOnlyTheirNamespaceAndSuppressHiddenStoryObjectsReadOnly(Campaign data)
        {
            var g=LandmarkGame(data);SaveData saved=CloneSave(g.save);
            saved.flags.AddRange(new[]{"mature_unknown_value","landmark_pinball:keep","landmark_pin:garbage","landmark_pin:-1:chef","landmark_pin:8:anything","landmark_pin:999:chef","landmark_pin:0:missing","landmark_pin:00:chef","landmark_pin:0:smith","mature_unknown_value"});
            var loaded=new GameCore(data,saved);loaded.Start();loaded.Close();
            Check(LandmarkFlags(loaded)==1&&loaded.Has("landmark_pin:0:chef")&&ReferenceEquals(loaded.PinnedLandmark,Object(loaded,"cook")),"Malformed references and duplicate pins normalize to the first valid stable object");
            Check(loaded.save.flags.FindAll(f=>f=="mature_unknown_value").Count==2&&loaded.Has("landmark_pinball:keep"),"Unrelated mature, unknown and similarly named namespaces remain intact");
            loaded.mode="landmarks";loaded.ClearLandmarkPin();loaded.Enter(1);loaded.save.explored[1]=16777215;loaded.mode="landmarks";
            WorldObject scout=Object(loaded,"scout");Check(loaded.PinLandmark(scout),"The actual present scout can be deliberately marked");
            loaded.save.flags.Add("scout_saved");string before=LandmarkDescribe(loaded.save);int revision=loaded.revision;
            Check(loaded.PinnedLandmark==null&&!loaded.DiscoveredLandmarks().Contains(scout)&&loaded.HasLandmarkPin,"An earned story disappearance suppresses the local mark without a stale interaction target");
            Check(before==LandmarkDescribe(loaded.save)&&revision==loaded.revision,"Hidden-pin inspection cannot silently rewrite saved flags or progress");
            loaded.Enter(9);loaded.save.explored[9]=16777215;WorldObject bowl=Object(loaded,"supperBowl0");
            Check(!loaded.DiscoveredLandmarks().Contains(bowl),"Explored inn sectors cannot reveal uncooked supper story objects");
            loaded.save.flags.AddRange(new[]{"ally_1","ally_2","ally_3","supper_cooked"});
            Check(loaded.DiscoveredLandmarks().Contains(bowl),"A legitimately present cooked place joins the list using its real story presence predicate");
        }

        public static void LandmarksUseLiveBridgeRootAndTidePathsWithoutOpeningGates(Campaign data)
        {
            var g=ReachGame(data);g.enemies.Clear();g.save.explored[11]=16777215;g.mode="landmarks";WorldObject cache=Object(g,"reachCache");
            Check(g.PinLandmark(cache)&&g.NavigationPath(g.NavigationTarget().Value).Count==0&&!g.ReachRootsOpen,"An explored but blocked object may be marked without granting its exploration ability or opening its gate");
            g.Close();ReachStand(g,"reachRoot");Check(g.ClearReachRoots(),"The real nearby exploration ability still owns root opening");
            g.save.x=g.Map.spawnX;g.save.y=g.Map.spawnY;
            Check(g.PinnedLandmark==cache&&g.NavigationPath(g.NavigationTarget().Value).Count>0,"The same stable pin automatically uses the newly opened root topology");
            g.save.x=27.5f;g.save.y=32.5f;g.mode="landmarks";WorldObject island=Object(g,"reachPost1");g.PinLandmark(island);
            List<Point> beforeBridge=g.NavigationPath(new Point(island.x,island.y));g.Close();ReachStand(g,"reachBridge");Check(g.OpenReachBridge(),"Only the authored winch opens the shortcut");
            g.save.x=27.5f;g.save.y=32.5f;List<Point> afterBridge=g.NavigationPath(new Point(island.x,island.y));
            Check(beforeBridge.Count>afterBridge.Count&&afterBridge.Count>0&&g.PinnedLandmark==island,"An existing pinned destination uses the shorter physical bridge route after repair");
            var port=Unlocked(data);port.Enter(5);port.enemies.Clear();port.save.explored[5]=16777215;port.mode="landmarks";WorldObject tideTarget=Object(port,"manifest");port.PinLandmark(tideTarget);
            port.save.x=5.5f;port.save.y=8.5f;port.save.totalTime=0;port.save.tideHeldUntil=0;List<Point> low=port.NavigationPath(port.NavigationTarget().Value);
            port.save.totalTime=24;List<Point> high=port.NavigationPath(port.NavigationTarget().Value);
            Check(port.PinnedLandmark==tideTarget&&low.Count>0,"A personal pin keeps the actual harbor object across the real tide cycle");
            foreach(Point p in high)Check(port.CanStand(p),"High-tide navigation cannot send the player through newly flooded cells");
            Check(high.Count==0||LandmarkDescribe(low)!=LandmarkDescribe(high),"A changed tide recalculates a different route or honestly reports the destination blocked");
        }

        public static void LandmarksReadQueriesAndRoutesNeverAwardInteractOrAdvanceWorldState(Campaign data)
        {
            var g=LandmarkGame(data);WorldObject cook=Object(g,"cook");g.PinLandmark(cook);g.Close();
            g.attackCooldown=.4f;g.skillCooldown=3;g.riposteTime=.8f;g.save.foodUntil=80;g.save.teaUntil=90;g.save.mealKind=2;g.save.mealUntil=70;
            string saved=LandmarkDescribe(g.save), enemies=LandmarkDescribe(g.enemies), projectiles=LandmarkDescribe(g.projectiles), effects=LandmarkDescribe(g.effects);
            int revision=g.revision;float toast=g.toastTime;int sounds=g.sounds.Count;string mode=g.mode;
            for(int pass=0;pass<30;pass++)
            {var list=g.DiscoveredLandmarks();WorldObject pin=g.PinnedLandmark;Check(g.HasLandmarkPin&&list.Contains(pin),"Repeated map reads retain the same actual destination");g.NavigationTarget();g.NavigationPath(new Point(pin.x,pin.y));}
            Check(saved==LandmarkDescribe(g.save)&&enemies==LandmarkDescribe(g.enemies)&&projectiles==LandmarkDescribe(g.projectiles)&&effects==LandmarkDescribe(g.effects),"Discovery, pin lookup and routing cannot change any saved or combat entity field");
            Check(g.revision==revision&&g.toastTime==toast&&g.sounds.Count==sounds&&g.mode==mode&&g.conversation==null&&g.attackCooldown==.4f&&g.skillCooldown==3&&g.riposteTime==.8f,
                "Read-only map queries cannot award, interact, make noise, consume a timer or open a dialogue");
        }
    }
}
