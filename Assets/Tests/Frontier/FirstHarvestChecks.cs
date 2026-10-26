using System;
using System.Collections.Generic;
using System.Reflection;

namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        private static readonly string[] FirstHarvestOldQuestIds = { "main", "shore", "ally1", "ally2", "ally3", "personal0", "personal1", "personal2", "bounty", "secrets", "watch", "signal", "archive", "reach", "homeward", "commission", "shelter", "fieldcraft", "supper", "returnletter", "riverlesson", "watergarden" };
        private static void FirstHarvestFeature()
        {
            Check(Array.IndexOf(GameCore.QuestIds, "firstharvest") >= 0,
                "A saved optional first-harvest quest must be offered after the existing garden-water story");
        }
        private static bool FirstHarvestRead(GameCore g, string property)
        {
            PropertyInfo info = typeof(GameCore).GetProperty("FirstHarvest" + property);
            Check(info != null, "The first-harvest state is available: " + property);
            return (bool)info.GetValue(g, null);
        }
        private static bool FirstHarvestCall(GameCore g, string action)
        {
            MethodInfo info = typeof(GameCore).GetMethod(action);
            Check(info != null, "The physical first-harvest action is available: " + action);
            return (bool)info.Invoke(g, null);
        }
        private static GameCore FirstHarvestGame(Campaign data, int profession = 0)
        {
            GameCore g = WaterGardenGame(data, profession); WaterGardenStage(g, 4);
            // This is a prior mature-story fixture, not a new quest shortcut.
            // The water-garden prerequisites above were earned through real menus.
            g.save.flags.Add("scout_saved"); g.Close(); return g;
        }
        private static void FirstHarvestVisit(GameCore g, string id)
        {
            int room = id == "innCook" ? 9 : 0;
            if (g.save.room != room) g.Enter(room);
            Check(g.save.room == room, "The original local route can reach the first-harvest resident " + id);
            WaterGardenVisit(g, id);
        }
        private static void FirstHarvestTopic(GameCore g, string id)
        { FirstHarvestVisit(g, id); WaterGardenChoose(g, "firstharvest_menu"); }
        private static void FirstHarvestAccept(GameCore g)
        { FirstHarvestTopic(g, "gardener"); WaterGardenChoose(g, "firstharvest_accept"); }
        private static void FirstHarvestCrop(GameCore g)
        {
            FirstHarvestVisit(g, "gardener");
            if (g.save.plantedAt < 0) WaterGardenChoose(g, "plant"); else g.Close();
            Advance(g, Math.Max(0, 60.1f - (g.save.totalTime - g.save.plantedAt)));
            FirstHarvestVisit(g, "gardener"); WaterGardenChoose(g, "harvest");
        }
        private static void FirstHarvestPack(GameCore g, string cook = "chef")
        { FirstHarvestTopic(g, cook); WaterGardenChoose(g, "firstharvest_pack"); }
        private static void FirstHarvestDeliver(GameCore g)
        { FirstHarvestTopic(g, "returned_scout"); WaterGardenChoose(g, "firstharvest_deliver"); }
        private static void FirstHarvestFinish(GameCore g)
        { FirstHarvestTopic(g, "gardener"); WaterGardenChoose(g, "firstharvest_finish"); }
        private static void FirstHarvestStage(GameCore g, int stage)
        {
            if (stage >= 1 && !FirstHarvestRead(g, "Accepted")) FirstHarvestAccept(g);
            if (stage >= 2 && !FirstHarvestRead(g, "Harvested")) FirstHarvestCrop(g);
            if (stage >= 3 && !FirstHarvestRead(g, "Packed")) FirstHarvestPack(g);
            if (stage >= 4 && !FirstHarvestRead(g, "Delivered")) FirstHarvestDeliver(g);
            if (stage >= 5 && !FirstHarvestRead(g, "Complete")) FirstHarvestFinish(g);
            g.Close();
        }
        private static string FirstHarvestCheckpoint(GameCore g)
        {
            string result = "";
            foreach (string property in new[] { "Ready", "Accepted", "Harvested", "Packed", "Delivered", "Complete" })
                result += FirstHarvestRead(g, property) ? "1" : "0";
            return result;
        }

        public static void FirstHarvestRequiresSavedTownPrerequisitesAndPhysicalHandoffs(Campaign data)
        {
            FirstHarvestFeature();
            foreach (string missing in new[] { "ally_1", "ally_3", "watergarden_accept", "watergarden_drawn", "watergarden_watered", "watergarden_complete", "scout_saved" })
            {
                var g = FirstHarvestGame(data); g.save.flags.Remove(missing); WaterGardenStand(g, "gardener");
                string before = WaterGardenEconomy(g); int flags = g.save.flags.Count;
                Check(!FirstHarvestRead(g, "Ready") && !FirstHarvestCall(g, "AcceptFirstHarvest")
                    && WaterGardenEconomy(g) == before && flags == g.save.flags.Count,
                    "The new story cannot bypass its saved original prerequisite: " + missing);
                Check(g.GetQuest("firstharvest").locked, "The journal reports the unavailable prerequisite without accepting it: " + missing);
            }
            int[] stages = { 0, 2, 3, 4 };
            string[] ids = { "gardener", "chef", "returned_scout", "gardener" };
            string[] methods = { "AcceptFirstHarvest", "PackFirstHarvest", "DeliverFirstHarvest", "FinishFirstHarvest" };
            for (int action = 0; action < stages.Length; action++)
                foreach (string boundary in new[] { "remote", "menu", "practice", "health", "nan", "wall", "enemy", "projectile", "hazard", "canonical", "blocked-host-ground" })
                {
                    var g = FirstHarvestGame(data); FirstHarvestStage(g, stages[action]); WorldObject host = WaterGardenStand(g, ids[action]);
                    WorldObject[] authored = g.Map.objects; string[] rows = (string[])g.Map.rows.Clone();
                    if (boundary == "remote") { g.save.x = 28; g.save.y = 14; }
                    if (boundary == "menu") g.mode = "inventory";
                    if (boundary == "practice") g.practicing = true;
                    if (boundary == "health") g.save.health = 0;
                    if (boundary == "nan") g.save.x = float.NaN;
                    if (boundary == "wall") { g.save.x = 0; g.save.y = 0; }
                    if (boundary == "enemy") g.enemies.Add(new Enemy(974131, 0, g.save.x, g.save.y, 30));
                    if (boundary == "projectile") g.projectiles.Add(new Projectile { friendly = false, position = g.Position, life = 1 });
                    if (boundary == "hazard") g.hazards.Add(new Hazard { position = g.Position, timer = 1, radius = 1 });
                    try
                    {
                        if (boundary == "canonical") g.Map.objects = Array.ConvertAll(authored, o => ReferenceEquals(o, host)
                            ? new WorldObject { id = "foreign-" + o.id, kind = o.kind, x = o.x, y = o.y } : o);
                        if (boundary == "blocked-host-ground")
                        {
                            g.save.x = host.x; g.save.y = host.y + 1.25f;
                            char[] line = g.Map.rows[(int)host.y].ToCharArray(); line[(int)host.x] = '#'; g.Map.rows[(int)host.y] = new string(line);
                            Check(g.CanStand(g.Position), "Blocked-host-ground rejection keeps the actor on legal footing");
                        }
                        string before = WaterGardenEconomy(g), progress = FirstHarvestCheckpoint(g); int flags = g.save.flags.Count;
                        Check(!FirstHarvestCall(g, methods[action]) && WaterGardenEconomy(g) == before
                            && progress == FirstHarvestCheckpoint(g) && flags == g.save.flags.Count,
                            "Each earned handoff requires its actual safe, living, authored current resident: " + methods[action] + " / " + boundary);
                    }
                    finally { g.Map.objects = authored; g.Map.rows = rows; }
                }
            var early = FirstHarvestGame(data); FirstHarvestAccept(early); WaterGardenStand(early, "chef");
            early.save.inventory[2] = 200;
            Check(!FirstHarvestCall(early, "PackFirstHarvest") && !FirstHarvestRead(early, "Harvested"),
                "Having or buying herbs does not substitute for an actual accepted crop harvest");
            WaterGardenStand(early, "returned_scout"); Check(!FirstHarvestCall(early, "DeliverFirstHarvest"), "A scout cannot receive a packet which Ari has not packed");
            WaterGardenStand(early, "gardener"); Check(!FirstHarvestCall(early, "FinishFirstHarvest"), "An cannot pay before the scout receives the packet");
        }

        public static void FirstHarvestUsesRealCropAndPreservesTownYields(Campaign data)
        {
            FirstHarvestFeature();
            var previous = FirstHarvestGame(data); FirstHarvestCrop(previous);
            Check(!FirstHarvestRead(previous, "Accepted") && !FirstHarvestRead(previous, "Harvested"), "A harvest before accepting does not retroactively complete the new request");
            FirstHarvestAccept(previous);
            Check(!FirstHarvestRead(previous, "Harvested"), "Accepting does not count an earlier crop, existing herbs or the old trial-bed watering");
            for (int profession = 0; profession < 3; profession++)
                for (int canal = 0; canal <= 2; canal++)
                    foreach (int ending in new[] { 0, 1 })
                    {
                        var g = FirstHarvestGame(data, profession); g.save.projects[0] = canal; g.save.frontierEnding = ending;
                        bool existing = (profession + canal + ending) % 2 == 0;
                        if (existing) { FirstHarvestVisit(g, "gardener"); WaterGardenChoose(g, "plant"); Advance(g, 12); }
                        int seedsBefore = g.save.inventory[7]; FirstHarvestAccept(g);
                        Check(g.save.inventory[7] == seedsBefore && !FirstHarvestRead(g, "Harvested"), "Acceptance does not replant, accelerate or charge an existing crop");
                        FirstHarvestVisit(g, "gardener");
                        if (!existing) WaterGardenChoose(g, "plant"); else g.Close();
                        float planted = g.save.plantedAt; int seeds = g.save.inventory[7], herbs = g.save.inventory[2], coins = g.save.coins;
                        FirstHarvestVisit(g, "gardener"); WaterGardenChoose(g, "harvest");
                        Check(g.save.plantedAt == planted && g.save.inventory[2] == herbs && !FirstHarvestRead(g, "Harvested"),
                            "The existing sixty-second maturity rule still rejects an early harvest");
                        g.Close(); Advance(g, 60.1f - (g.save.totalTime - planted));
                        FirstHarvestVisit(g, "gardener"); WaterGardenChoose(g, "harvest");
                        int yield = 5 + 2 * canal + (ending == 1 ? 2 : 0);
                        Check(FirstHarvestRead(g, "Harvested") && g.save.plantedAt == -1 && g.save.inventory[2] == herbs + yield
                            && g.save.inventory[7] == seeds + 2 && g.save.coins == coins + 8,
                            "The real menu records a fresh harvest while preserving every class, canal and shared-water yield: " + profession + "/" + canal + "/" + ending);
                        Check(g.save.profession == profession && g.save.weapon == profession && g.save.projects[0] == canal && g.save.frontierEnding == ending,
                            "The optional crop witness never changes class, equipment, canal progress or ending");
                        string paidCrop = WaterGardenEconomy(g); FirstHarvestVisit(g, "gardener");
                        Check(!Array.Exists(g.conversation.choices, c => c.action == "harvest"), "Harvest completion returns the original garden to its planting menu");
                        g.Close(); Check(WaterGardenEconomy(g) == paidCrop, "Merely revisiting An cannot duplicate crop proceeds");
                    }
        }

        public static void FirstHarvestPaysExactRouteAndRejectsStaleOrFullRewards(Campaign data)
        {
            FirstHarvestFeature();
            var corner = FirstHarvestGame(data); FirstHarvestStage(corner, 2);
            WorldObject cornerCook = WaterGardenObject(corner, "chef"); corner.save.x = 9.5f; corner.save.y = 16.5f;
            MethodInfo attackRay = typeof(GameCore).GetMethod("ClearAttackPath", BindingFlags.Instance | BindingFlags.NonPublic);
            Check(attackRay != null && corner.CanStand(corner.Position) && ReferenceEquals(corner.Nearest(), cornerCook)
                && !(bool)attackRay.Invoke(corner, new object[] { corner.Position, new Point(cornerCook.x, cornerCook.y) }),
                "The real original cook approach is legal and E-selectable even where a house corner blocks a combat ray");
            int cornerHerbs = corner.save.inventory[2]; corner.Interact(); WaterGardenChoose(corner, "firstharvest_menu");
            WaterGardenChoose(corner, "firstharvest_pack");
            Check(FirstHarvestRead(corner, "Packed") && corner.save.inventory[2] == cornerHerbs - 2,
                "A real E conversation at the mature cook's corner approach can pack the quest herbs without imposing combat-line-of-sight rules");
            for (int profession = 0; profession < 3; profession++)
                foreach (string cook in new[] { "chef", "innCook" })
                {
                    var g = FirstHarvestGame(data, profession); FirstHarvestStage(g, 2);
                    FirstHarvestTopic(g, cook); g.save.inventory[2] = 1; int index = WaterGardenChoice(g, "firstharvest_pack");
                    string shortBefore = WaterGardenEconomy(g); g.Choose(index);
                    Check(!FirstHarvestRead(g, "Packed") && WaterGardenEconomy(g) == shortBefore,
                        "Ari never removes a partial herb payment or invents a packet when materials run short");
                    g.Close(); g.save.inventory[2] = 2; g.save.health = 41; g.save.energy = 37;
                    g.save.foodUntil = g.save.totalTime + 12; g.save.teaUntil = g.save.totalTime + 14;
                    SaveData before = CloneSave(g.save); FirstHarvestPack(g, cook);
                    Check(FirstHarvestRead(g, "Packed") && g.save.inventory[2] == 0 && g.save.health == before.health && g.save.energy == before.energy
                        && g.save.foodUntil == before.foodUntil && g.save.teaUntil == before.teaUntil && g.save.mealKind == before.mealKind && g.save.mealUntil == before.mealUntil,
                        "Either physical Ari packs exactly two herbs without healing, refilling, eating or replacing a timed meal");
                    for (int item = 0; item < 8; item++) if (item != 2) Check(g.save.inventory[item] == before.inventory[item], "A quest packet is not an inventory item or a charge to unrelated supplies");
                    if (g.save.room != 0) g.Enter(0); WaterGardenStand(g, "chef");
                    string packed = WaterGardenEconomy(g); Check(!FirstHarvestCall(g, "PackFirstHarvest") && WaterGardenEconomy(g) == packed, "A packed packet cannot charge twice");
                    FirstHarvestDeliver(g); Check(FirstHarvestRead(g, "Delivered") && !FirstHarvestRead(g, "Complete"), "The returned scout acknowledges physical delivery before An pays");
                    string delivered = WaterGardenEconomy(g); WaterGardenStand(g, "returned_scout");
                    Check(!FirstHarvestCall(g, "DeliverFirstHarvest") && WaterGardenEconomy(g) == delivered, "A received packet cannot be delivered twice");
                    int coins = g.save.coins, tea = g.save.tea; FirstHarvestFinish(g);
                    Check(FirstHarvestRead(g, "Complete") && g.save.coins == coins + 25 && g.save.tea == tea + 1,
                        "The final real An conversation pays exactly 25 coins and one tea, once");
                    string paid = WaterGardenEconomy(g); WaterGardenStand(g, "gardener");
                    Check(!FirstHarvestCall(g, "FinishFirstHarvest") && WaterGardenEconomy(g) == paid, "A repeated completion cannot mint more rewards");
                    WaterGardenReplay(g, "firstharvest_finish"); Check(WaterGardenEconomy(g) == paid, "An obsolete final callback cannot duplicate the reward");
                }
            int[] stages = { 0, 2, 3, 4 }; string[] hosts = { "gardener", "chef", "returned_scout", "gardener" };
            string[] actions = { "firstharvest_accept", "firstharvest_pack", "firstharvest_deliver", "firstharvest_finish" };
            for (int i = 0; i < stages.Length; i++)
            {
                var stale = FirstHarvestGame(data); FirstHarvestStage(stale, stages[i]); FirstHarvestTopic(stale, hosts[i]);
                int index = WaterGardenChoice(stale, actions[i]); WorldObject host = WaterGardenObject(stale, hosts[i]); int flags = stale.save.flags.Count;
                stale.save.x = 28; stale.save.y = 14; string afterMove = WaterGardenEconomy(stale); stale.Choose(index);
                Check(WaterGardenEconomy(stale) == afterMove && flags == stale.save.flags.Count,
                    "An offered handoff cannot progress after leaving its original resident: " + actions[i]);
                Check((stale.Position - new Point(host.x, host.y)).Length > 1.75f, "The stale fixture really moved away from the offered resident");
            }
            foreach (string cap in new[] { "coins", "tea", "both" })
            {
                var g = FirstHarvestGame(data); FirstHarvestStage(g, 4); WaterGardenStand(g, "gardener");
                g.save.coins = cap == "tea" ? 20 : 999975; g.save.tea = cap == "coins" ? 4 : 999;
                string before = WaterGardenEconomy(g); int flags = g.save.flags.Count;
                Check(!FirstHarvestCall(g, "FinishFirstHarvest") && !FirstHarvestRead(g, "Complete") && flags == g.save.flags.Count && WaterGardenEconomy(g) == before,
                    "A capped reward remains wholly unpaid and retryable: " + cap);
                g.save.coins = 999974; g.save.tea = 998;
                Check(FirstHarvestCall(g, "FinishFirstHarvest") && g.save.coins == 999999 && g.save.tea == 999 && FirstHarvestRead(g, "Complete"),
                    "Exact coin and tea capacity boundaries pay the full original new-quest reward atomically");
            }
        }

        public static void FirstHarvestRestoresEachPhaseAndKeepsGuidanceReadOnly(Campaign data)
        {
            FirstHarvestFeature();
            Check(GameCore.QuestIds.Length >= FirstHarvestOldQuestIds.Length + 1, "The new optional journal row is additive");
            for (int row = 0; row < FirstHarvestOldQuestIds.Length; row++)
                Check(GameCore.QuestIds[row] == FirstHarvestOldQuestIds[row], "Every mature journal row keeps its original index: " + row);
            Check(GameCore.QuestIds[FirstHarvestOldQuestIds.Length] == "firstharvest", "The new quest appends after all mature rows");
            foreach (int version in new[] { 1, 2 })
                for (int stage = 0; stage <= 5; stage++)
                {
                    var g = FirstHarvestGame(data, 2); g.save.projects[0] = 2; g.save.mastery[2] = 17;
                    g.save.flags.Add("homeward_public"); g.save.flags.Add("supper_complete"); FirstHarvestStage(g, stage);
                    string state = FirstHarvestCheckpoint(g); g.PrepareSave(); string economy = WaterGardenEconomy(g);
                    SaveData saved = CloneSave(g.save); saved.version = version;
                    g = new GameCore(data, saved); g.Start(); g.Close();
                    Check(FirstHarvestCheckpoint(g) == state && WaterGardenEconomy(g) == economy,
                        "Every ordered checkpoint survives actual v" + version + " reconstruction without replaying costs or rewards: " + stage);
                    Check(g.save.profession == 2 && g.save.weapon == 2 && g.save.projects[0] == 2 && g.save.mastery[2] == 17 && g.Has("homeward_public") && g.Has("supper_complete"),
                        "The additive story preserves class, equipment, mature plot and town progress on load");
                    int revision = g.revision, flags = g.save.flags.Count;
                    QuestGuide guide = g.GetQuest("firstharvest"); string beforeRead = WaterGardenEconomy(g);
                    for (int read = 0; read < 3; read++) guide = g.GetQuest("firstharvest");
                    Check(guide.id == "firstharvest" && !guide.locked && guide.complete == (stage == 5)
                        && WaterGardenEconomy(g) == beforeRead && revision == g.revision && flags == g.save.flags.Count,
                        "Reading current quest guidance is side-effect free at every saved phase");
                    if (stage < 5)
                    {
                        string hostId = stage < 2 || stage == 4 ? "gardener" : stage == 2 ? "chef" : "returned_scout";
                        WorldObject host = WaterGardenObject(g, hostId);
                        Check(guide.room == 0 && (guide.target - new Point(host.x, host.y)).Length < .001f,
                            "The saved quest points to its real next resident or crop rather than another quest's target: " + stage);
                        g.save.x = 18; g.save.y = 15; List<Point> route = g.InteractionPath(host);
                        Check(route.Count > 0, "The suggested resident has a real reachable interaction route: " + hostId);
                        Point foot = route[route.Count - 1]; g.save.x = foot.x; g.save.y = foot.y;
                        Check(g.CanStand(foot) && ReferenceEquals(g.Nearest(), host), "The suggested route ends where real E chooses the promised resident");
                    }
                    if (stage < 5) { FirstHarvestStage(g, 5); Check(FirstHarvestRead(g, "Complete"), "Restored progress remains finishable through original menus"); }
                }
            foreach (string orphan in new[] { "firstharvest_harvested", "firstharvest_packed", "firstharvest_delivered", "firstharvest_complete" })
            {
                var g = FirstHarvestGame(data); g.save.flags.Add(orphan);
                var loaded = new GameCore(data, CloneSave(g.save)); loaded.Start(); loaded.Close();
                Check(!FirstHarvestRead(loaded, "Accepted") && !FirstHarvestRead(loaded, "Harvested") && !FirstHarvestRead(loaded, "Packed")
                    && !FirstHarvestRead(loaded, "Delivered") && !FirstHarvestRead(loaded, "Complete"), "An orphan later flag cannot forge ordered quest completion: " + orphan);
            }
        }
    }
}
