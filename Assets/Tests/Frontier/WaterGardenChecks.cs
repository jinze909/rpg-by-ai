using System;
using System.Collections.Generic;

namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        private static GameCore WaterGardenGame(Campaign data, int profession = 0)
        {
            var g = Fresh(data, profession); g.SkipTutorial();
            // Earn both original requests through their existing physical menus.
            foreach (int request in new[] { 1, 3 })
            {
                g.save.inventory[request == 1 ? 2 : 4] = request == 1 ? 6 : 5;
                int coins = g.save.coins, points = g.save.skillPoints, stew = g.save.inventory[6];
                WaterGardenVisit(g, request == 1 ? "gardener" : "smith"); WaterGardenChoose(g, "ally" + request);
                Check(g.Has("ally_" + request) && g.save.coins == coins + 40 && g.save.skillPoints == points + 1
                    && g.save.inventory[6] == stew + 1 && g.save.inventory[request == 1 ? 2 : 4] == 0,
                    "Water-garden entry preserves the full original resident request reward and exact material payment: " + request);
            }
            g.Close(); Check(g.WaterGardenReady && !g.Has("ally_2"), "The water-garden story needs An and Noll, without making Old Tide's request a new prerequisite");
            return g;
        }
        private static WorldObject WaterGardenObject(GameCore g, string id)
        {
            WorldObject result = Array.Find(g.Map.objects, o => o.id == id);
            Check(result != null, "The shipped town contains water-garden station " + id); return result;
        }
        private static WorldObject WaterGardenStand(GameCore g, string id)
        {
            g.Close(); WorldObject source = WaterGardenObject(g, id);
            foreach (Point offset in new[] { new Point(0, .7f), new Point(.7f, 0), new Point(-.7f, 0), new Point(0, -.7f), new Point() })
            {
                Point p = new Point(source.x, source.y) + offset;
                if (!g.CanStand(p)) continue;
                g.save.x = p.x; g.save.y = p.y;
                if (ReferenceEquals(g.Nearest(), source)) return source;
            }
            throw new Exception("No real unobstructed nearest-object approach exists for " + id);
        }
        private static void WaterGardenVisit(GameCore g, string id)
        { WaterGardenStand(g, id); g.Interact(); Check(g.mode == "dialog" && g.conversation != null, "A real E opens the local station: " + id); }
        private static int WaterGardenChoice(GameCore g, string action)
        {
            Check(g.mode == "dialog" && g.conversation != null, "The water-garden action is offered by real dialogue");
            int found = Array.FindIndex(g.conversation.choices, c => c.action == action);
            Check(found >= 0, "The real water-garden conversation offers " + action); return found;
        }
        private static void WaterGardenChoose(GameCore g, string action) { g.Choose(WaterGardenChoice(g, action)); }
        private static void WaterGardenAccept(GameCore g)
        { WaterGardenVisit(g, "smith"); WaterGardenChoose(g, "watergarden_offer"); WaterGardenChoose(g, "watergarden_accept"); }
        private static void WaterGardenDraw(GameCore g)
        { WaterGardenVisit(g, "watergarden_draw"); WaterGardenChoose(g, "watergarden_draw"); }
        private static void WaterGardenWater(GameCore g)
        { WaterGardenVisit(g, "watergarden_bed"); WaterGardenChoose(g, "watergarden_water"); }
        private static void WaterGardenFinish(GameCore g)
        { WaterGardenVisit(g, "gardener"); WaterGardenChoose(g, "watergarden_offer"); WaterGardenChoose(g, "watergarden_finish"); }
        private static void WaterGardenStage(GameCore g, int stage)
        {
            if (stage >= 1) WaterGardenAccept(g);
            if (stage >= 2) WaterGardenDraw(g);
            if (stage >= 3) WaterGardenWater(g);
            if (stage >= 4) WaterGardenFinish(g);
            g.Close();
        }
        private static string WaterGardenEconomy(GameCore g)
        {
            SaveData s = g.save;
            return string.Join(",", s.inventory) + "|" + s.coins + "|" + s.tea + "|" + s.skillPoints + "|" + s.health
                + "|" + s.maxHealth + "|" + s.energy + "|" + s.plantedAt + "|" + s.totalTime + "|" + s.foodUntil + "|" + s.teaUntil
                + "|" + s.shards + "|" + s.xp + "|" + string.Join(",", s.projects) + "|" + string.Join(",", s.talents)
                + "|" + string.Join(",", s.mastery) + "|" + string.Join(",", s.reputation) + "|" + string.Join(",", s.relationships);
        }
        private static void WaterGardenReplay(GameCore g, string action)
        {
            g.conversation = new Conversation("stale", "stale", "stale", "stale", new Choice("stale", "stale", action));
            g.mode = "dialog"; g.Choose(0);
        }

        public static void WaterGardenOrderedPhysicalHandoffs(Campaign data)
        {
            for (int profession = 0; profession < 3; profession++)
            {
                var g = WaterGardenGame(data, profession);
                WorldObject draw = WaterGardenObject(g, "watergarden_draw"), bed = WaterGardenObject(g, "watergarden_bed");
                Check(draw.kind == "waterDraw" && draw.x == 20 && draw.y == 11 && bed.kind == "waterBed" && bed.x == 19 && bed.y == 6,
                    "The two new handoffs use distinct authored water and garden stations");
                Check(!g.IsObjectPresent(draw) && !g.IsObjectPresent(bed), "Optional task stations do not take nearby E actions before acceptance");
                string economy = WaterGardenEconomy(g);
                WaterGardenAccept(g);
                Check(g.mode == "world" && g.WaterGardenAccepted && !g.WaterGardenDrawn && !g.WaterGardenWatered && !g.WaterGardenComplete
                    && g.save.trackedQuest == "watergarden" && WaterGardenEconomy(g) == economy,
                    "The real earned smith topic accepts and tracks the separate story without consuming or repaying original supplies");
                Check(g.IsObjectPresent(draw) && !g.IsObjectPresent(bed), "Only the first physical handoff appears after acceptance");
                WaterGardenStand(g, "gardener");
                Check(!g.WaterGardenBed() && !g.FinishWaterGarden(), "An cannot accept an empty bucket or finish before the first watering");
                WaterGardenDraw(g);
                Check(g.mode == "world" && g.WaterGardenDrawn && !g.WaterGardenWatered && !g.IsObjectPresent(draw) && g.IsObjectPresent(bed)
                    && WaterGardenEconomy(g) == economy, "Drawing one bucket changes the owned checkpoint and station presence without minting an inventory item");
                Check(!g.DrawGardenWater(), "A consumed draw cannot record a second bucket");
                WaterGardenWater(g);
                Check(g.mode == "world" && g.WaterGardenWatered && !g.WaterGardenComplete && !g.IsObjectPresent(bed)
                    && WaterGardenEconomy(g) == economy, "Watering consumes the story bucket without replacing the mature crop or production timer");
                Check(!g.WaterGardenBed(), "The same garden handoff cannot be consumed twice");
                int seeds = g.save.inventory[7], tea = g.save.tea;
                WaterGardenFinish(g);
                Check(g.mode == "world" && g.WaterGardenComplete && g.save.inventory[7] == seeds + 3 && g.save.tea == tea + 1,
                    "Reporting to the actual herbalist pays exactly three seeds and one tea for every profession");
                string paid = WaterGardenEconomy(g); WaterGardenStand(g, "gardener");
                Check(!g.FinishWaterGarden() && WaterGardenEconomy(g) == paid, "Repeated direct completion cannot pay a second reward");
                WaterGardenReplay(g, "watergarden_finish");
                Check(WaterGardenEconomy(g) == paid && g.WaterGardenComplete, "An obsolete finish callback cannot pay twice either");
            }
        }

        public static void WaterGardenRejectsInvalidAndStaleActions(Campaign data)
        {
            foreach (string prerequisite in new[] { "", "ally_1", "ally_3" })
            {
                var locked = Fresh(data); locked.SkipTutorial(); if (prerequisite.Length > 0) locked.save.flags.Add(prerequisite);
                WaterGardenStand(locked, "smith"); string before = WaterGardenEconomy(locked);
                Check(!locked.WaterGardenReady && !locked.AcceptWaterGarden() && !locked.DrawGardenWater()
                    && !locked.WaterGardenBed() && !locked.FinishWaterGarden() && WaterGardenEconomy(locked) == before,
                    "Missing original commissions cannot be bypassed by direct water-garden APIs: " + prerequisite);
            }
            string[] ids = { "smith", "watergarden_draw", "watergarden_bed", "gardener" };
            string[] actions = { "watergarden_accept", "watergarden_draw", "watergarden_water", "watergarden_finish" };
            for (int stage = 0; stage < 4; stage++)
            {
                foreach (string boundary in new[] { "remote", "menu", "practice", "health", "nan", "wall", "enemy", "projectile", "hazard", "canonical", "path" })
                {
                    var g = WaterGardenGame(data); WaterGardenStage(g, stage); WorldObject source = WaterGardenStand(g, ids[stage]);
                    WorldObject[] authored = g.Map.objects; string[] rows = (string[])g.Map.rows.Clone();
                    if (boundary == "remote") { g.save.x = 28; g.save.y = 14; }
                    if (boundary == "menu") g.mode = "inventory";
                    if (boundary == "practice") g.practicing = true;
                    if (boundary == "health") g.save.health = 0;
                    if (boundary == "nan") g.save.x = float.NaN;
                    if (boundary == "wall") { g.save.x = 0; g.save.y = 0; }
                    if (boundary == "enemy") g.enemies.Add(new Enemy(91231, 0, g.save.x, g.save.y, 20));
                    if (boundary == "projectile") g.projectiles.Add(new Projectile { position = g.Position, friendly = false, life = 2 });
                    if (boundary == "hazard") g.hazards.Add(new Hazard { position = g.Position, timer = 1, radius = 1 });
                    string before = WaterGardenEconomy(g); int flags = g.save.flags.Count;
                    try
                    {
                        if (boundary == "canonical") g.Map.objects = Array.ConvertAll(authored, o => ReferenceEquals(o, source)
                            ? new WorldObject { id = "foreign-" + o.id, kind = o.kind, x = o.x, y = o.y } : o);
                        if (boundary == "path")
                        {
                            // Keep legal player footing but place a wall at the station endpoint.
                            g.save.x = source.x; g.save.y = source.y + 1.25f;
                            char[] line = g.Map.rows[(int)source.y].ToCharArray(); line[(int)source.x] = '#'; g.Map.rows[(int)source.y] = new string(line);
                            Check(g.CanStand(g.Position), "The path rejection uses live legal player footing, not a player inside a wall");
                        }
                        bool result = stage == 0 ? g.AcceptWaterGarden() : stage == 1 ? g.DrawGardenWater()
                            : stage == 2 ? g.WaterGardenBed() : g.FinishWaterGarden();
                        Check(!result && g.save.flags.Count == flags && WaterGardenEconomy(g) == before,
                            "Each direct handoff rechecks its physical, living, quiet current station atomically: stage " + stage + " / " + boundary);
                    }
                    finally { g.Map.objects = authored; g.Map.rows = rows; }
                }
                var stale = WaterGardenGame(data); WaterGardenStage(stale, stage); WaterGardenVisit(stale, ids[stage]);
                if (stage == 0 || stage == 3) WaterGardenChoose(stale, "watergarden_offer");
                int index = WaterGardenChoice(stale, actions[stage]); string unchanged = WaterGardenEconomy(stale);
                stale.save.x = 28; stale.save.y = 14; stale.Choose(index);
                Check(WaterGardenEconomy(stale) == unchanged && !stale.Has(new[] { "watergarden_accept", "watergarden_drawn", "watergarden_watered", "watergarden_complete" }[stage]),
                    "A real stale station callback cannot advance or pay after walking away: " + stage);
            }
            var replaced = WaterGardenGame(data); WaterGardenAccept(replaced); WaterGardenVisit(replaced, "watergarden_draw");
            int oldIndex = WaterGardenChoice(replaced, "watergarden_draw");
            var selected = Fresh(data, 2); selected.SkipTutorial(); selected.save.flags.Add("ally_1"); selected.save.flags.Add("ally_3");
            replaced.Load(CloneSave(selected.save)); replaced.Start(); replaced.Close(); string selectedEconomy = WaterGardenEconomy(replaced);
            replaced.Choose(oldIndex);
            Check(!replaced.WaterGardenAccepted && !replaced.WaterGardenDrawn && WaterGardenEconomy(replaced) == selectedEconomy,
                "A pre-load conversation index cannot draw water into another selected legacy-compatible save");
        }

        public static void WaterGardenRewardsAndOldSaves(Campaign data)
        {
            foreach (int version in new[] { 1, 2 })
            {
                var g = WaterGardenGame(data, 2); g.save.flags.Add("homeward_public"); g.save.flags.Add("supper_complete"); g.save.flags.Add("core_1");
                g.save.projects[0] = 2; g.save.mastery[2] = 17;
                for (int checkpoint = 0; checkpoint <= 4; checkpoint++)
                {
                    if (checkpoint == 1) WaterGardenAccept(g);
                    if (checkpoint == 2) WaterGardenDraw(g);
                    if (checkpoint == 3) WaterGardenWater(g);
                    if (checkpoint == 4) WaterGardenFinish(g);
                    bool accepted = g.WaterGardenAccepted, drawn = g.WaterGardenDrawn, watered = g.WaterGardenWatered, complete = g.WaterGardenComplete;
                    string before = WaterGardenEconomy(g); g.PrepareSave(); SaveData saved = CloneSave(g.save); saved.version = version;
                    g = new GameCore(data, saved); g.Start(); g.Close();
                    Check(g.WaterGardenAccepted == accepted && g.WaterGardenDrawn == drawn && g.WaterGardenWatered == watered
                        && g.WaterGardenComplete == complete && WaterGardenEconomy(g) == before,
                        "Every finite water-garden checkpoint survives real v" + version + " reconstruction without invented payment: " + checkpoint);
                    Check(g.save.profession == 2 && g.save.weapon == 2 && g.Has("homeward_public") && g.Has("supper_complete") && g.Has("core_1")
                        && g.save.projects[0] == 2 && g.save.mastery[2] == 17, "Identity, mature story flags, workshop growth and equipment survive the additive quest");
                }
                foreach (string orphan in new[] { "watergarden_drawn", "watergarden_watered", "watergarden_complete" })
                {
                    var original = WaterGardenGame(data); original.save.flags.Add(orphan); original.save.version = version;
                    var loaded = new GameCore(data, CloneSave(original.save)); loaded.Start(); loaded.Close();
                    Check(!loaded.WaterGardenAccepted && !loaded.WaterGardenDrawn && !loaded.WaterGardenWatered && !loaded.WaterGardenComplete,
                        "An orphan later flag cannot masquerade as earned ordered progress after migration: " + orphan);
                }
            }
            foreach (string full in new[] { "seeds", "tea", "both" })
            {
                var capped = WaterGardenGame(data); WaterGardenStage(capped, 3); WaterGardenStand(capped, "gardener");
                capped.save.inventory[7] = full == "tea" ? 41 : 9997; capped.save.tea = full == "seeds" ? 7 : 999;
                string before = WaterGardenEconomy(capped); int flags = capped.save.flags.Count;
                Check(!capped.FinishWaterGarden() && !capped.WaterGardenComplete && capped.save.flags.Count == flags && WaterGardenEconomy(capped) == before,
                    "A capped reward remains wholly unpaid and retryable: " + full);
                capped.save.inventory[7] = 9996; capped.save.tea = 998;
                Check(capped.FinishWaterGarden() && capped.WaterGardenComplete && capped.save.inventory[7] == 9999 && capped.save.tea == 999,
                    "The exact two reward ceilings are legal and finish atomically on retry");
                string paid = WaterGardenEconomy(capped); capped.PrepareSave(); var loaded = new GameCore(data, CloneSave(capped.save)); loaded.Start(); loaded.Close();
                WaterGardenStand(loaded, "gardener"); Check(!loaded.FinishWaterGarden() && WaterGardenEconomy(loaded) == paid,
                    "A completed reward remains single-payment after reconstruction at both exact caps");
            }
        }

        private static void WaterGardenGuide(GameCore g, string id)
        {
            g.Close(); g.save.x = 18; g.save.y = 15;
            string before = WaterGardenEconomy(g); int revision = g.revision; QuestGuide q = g.GetQuest("watergarden");
            WorldObject target = WaterGardenObject(g, id); List<Point> path = g.InteractionPath(target);
            Check(q.id == "watergarden" && !q.locked && !q.complete && q.room == 0 && (q.target - new Point(target.x, target.y)).Length < .001f,
                "The quest names its real next handoff station: " + id);
            Check(path.Count > 0 && WaterGardenEconomy(g) == before && g.revision == revision,
                "Reading guidance and computing a route are side-effect free and find actual town footing");
            foreach (Point step in path) Check(g.CanStand(step), "Each water-garden route step remains legal under town topology");
            Point end = path[path.Count - 1]; g.save.x = end.x; g.save.y = end.y;
            Check(ReferenceEquals(g.Nearest(), target), "The route ends where real E selects its promised station: " + id);
        }
        public static void WaterGardenPreservesTownAndNavigation(Campaign data)
        {
            var g = WaterGardenGame(data); int oldQuests = Array.IndexOf(GameCore.QuestIds, "riverlesson");
            Check(Array.IndexOf(GameCore.QuestIds, "watergarden") == 21 && oldQuests == 20,
                "The water-garden and river-lesson journal identities keep their original indices as later stories append");
            WaterGardenGuide(g, "smith"); WaterGardenAccept(g); WaterGardenGuide(g, "watergarden_draw");
            WaterGardenStand(g, "routes"); g.Interact(); Check(g.mode == "map", "The original center-town wayfinder remains the actual E target while the nearby pump is active");
            WaterGardenDraw(g); WaterGardenGuide(g, "watergarden_bed");
            WaterGardenVisit(g, "gardener");
            string[] actions = { "plant", "seeds", "close" };
            for (int i = 0; i < actions.Length; i++) Check(g.conversation.choices[i].action == actions[i], "An keeps every original post-commission gardening menu position");
            WaterGardenChoose(g, "plant"); float planted = g.save.plantedAt; int seeds = g.save.inventory[7], herbs = g.save.inventory[2], coins = g.save.coins;
            Check(planted == g.save.totalTime, "The mature crop starts through An's actual menu");
            WaterGardenWater(g); Check(g.save.plantedAt == planted && g.save.inventory[7] == seeds && g.save.inventory[2] == herbs,
                "Story watering neither consumes extra seed nor accelerates, harvests, or replaces the mature sixty-second crop");
            WaterGardenGuide(g, "gardener");
            WaterGardenVisit(g, "gardener"); WaterGardenChoose(g, "harvest"); Check(g.save.plantedAt == planted && g.save.inventory[2] == herbs,
                "The new water story does not bypass the original immature-harvest gate");
            g.Close(); Advance(g, 60.1f); WaterGardenVisit(g, "gardener"); WaterGardenChoose(g, "harvest");
            Check(g.save.plantedAt == -1 && g.save.inventory[2] == herbs + 5 && g.save.inventory[7] == seeds + 2 && g.save.coins == coins + 8,
                "A mature crop still pays the exact original five herbs, two seeds and eight coins");
            WaterGardenFinish(g); Check(g.GetQuest("watergarden").complete, "The appended guide recognizes the paid ending");
            WaterGardenVisit(g, "smith");
            foreach (string action in new[] { "upgrade", "bow", "staff", "close", "forge_quotes" })
                Check(Array.Exists(g.conversation.choices, c => c.action == action), "The new town story preserves the blacksmith's mature action " + action);
            Check(!g.Has("ally_2") && !g.SupperReady && !g.RiverLessonReady,
                "The new handoff never silently completes another resident's commission or its downstream story");
        }
    }
}
