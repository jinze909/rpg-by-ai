using System;
using System.Reflection;

namespace Starfall.Tests
{
    // These checks use the actual E owner and existing dialogue transactions.
    // A hint is presentation only: it cannot gather, plant, open or pay out.
    public static partial class CoreChecks
    {
        private static void HintText(GameCore game, WorldObject target, string zh, string en)
        {
            string before = LandmarkDescribe(game); string mode = game.mode; int generation = game.WorldGeneration;
            Check(game.InteractionHint(target, false) == zh, "Chinese interaction hint matches the live state: " + zh);
            Check(game.InteractionHint(target, true) == en, "English interaction hint matches the live state: " + en);
            Check(LandmarkDescribe(game) == before && game.mode == mode && game.WorldGeneration == generation,
                "Both localized hint queries leave all public saved, simulation and authored-object state unchanged");
        }

        private static void HintStand(GameCore game, WorldObject target)
        {
            game.Close();
            var route = game.InteractionPath(target);
            if (route.Count > 0)
            { Point end = route[route.Count - 1]; game.save.x = end.x; game.save.y = end.y; }
            Check(game.CanStand(game.Position) && ReferenceEquals(game.Nearest(), target),
                "The actual E owner at the legal approach must be " + target.id);
        }

        private static void HintOpen(GameCore game, WorldObject target)
        { HintStand(game, target); game.Interact(); }

        private static void HintChoose(GameCore game, string action)
        {
            Check(game.mode == "dialog" && game.conversation != null, "A real dialogue owns " + action);
            for (int index = 0; index < game.conversation.choices.Length; index++)
                if (game.conversation.choices[index].action == action) { game.Choose(index); return; }
            throw new Exception("Missing original dialogue action " + action);
        }

        private static void HintGather(GameCore game, WorldObject node)
        {
            HintText(game, node, "可采集", "Gather supplies");
            int[] before = (int[])game.save.inventory.Clone(); int coins = game.save.coins, shards = game.save.shards;
            HintOpen(game, node);
            int item = node.kind == "herb" ? 2 : node.kind == "wood" ? 0 : 1;
            for (int index = 0; index < before.Length; index++)
                Check(game.save.inventory[index] - before[index] == (index == item || node.kind == "ore" && index == 4 ? 3 : 0),
                    "Actual E preserves the three-item resource yield and ore's three parts at item " + index);
            Check(game.Has("node_" + node.id) && game.save.coins == coins && game.save.shards == shards,
                "Gathering marks only its existing node flag and keeps currency unchanged");
            HintText(game, node, "已采集 · 回镇休息后更新", "Gathered · rest in town to renew");
            string depleted = LandmarkDescribe(game.save);
            game.Interact(); game.Interact();
            Check(LandmarkDescribe(game.save) == depleted, "Repeated real E on a depleted node cannot duplicate supplies");
        }

        public static void InteractionHintResourcesFollowRealGatherAndInnRest(Campaign data)
        {
            var game = Fresh(data); game.SkipTutorial(); game.Enter(1); game.enemies.Clear();
            string[] kinds = { "herb", "wood", "ore" };
            foreach (string kind in kinds) HintGather(game, Object(game, kind));
            int[] gathered = (int[])game.save.inventory.Clone();
            game.Enter(0); HintOpen(game, Object(game, "portal9"));
            Check(game.save.room == 9, "The actual town door reaches the existing inn interior");
            game.save.health = 10; game.save.energy = 12; float time = game.save.totalTime;
            HintOpen(game, Object(game, "rest")); HintChoose(game, "rest");
            Check(game.save.totalTime == time + 45 && game.save.health == game.save.maxHealth && game.save.energy == 100,
                "The real inn choice retains its 45-second full recovery");
            foreach (string flag in game.save.flags) Check(!flag.StartsWith("node_", StringComparison.Ordinal), "Actual rest renews node flags");
            for (int index = 0; index < gathered.Length; index++)
                Check(game.save.inventory[index] == gathered[index], "Rest itself never grants gathering rewards");
            HintOpen(game, Object(game, "portal0")); game.Enter(1); game.enemies.Clear();
            foreach (string kind in kinds) HintGather(game, Object(game, kind));
            Check(game.save.inventory[0] == gathered[0] + 3 && game.save.inventory[1] == gathered[1] + 3
                && game.save.inventory[2] == gathered[2] + 3 && game.save.inventory[4] == gathered[4] + 3,
                "The renewed actual nodes pay the same three supplies and three ore parts a second time");
        }

        public static void InteractionHintGardenKeepsDialogueGrowthHarvestAndRecipes(Campaign data)
        {
            var game = Fresh(data); game.SkipTutorial(); WorldObject garden = Object(game, "garden");
            HintText(game, garden, "交谈 · 田地空置", "Talk · plot empty");
            int seeds = game.save.inventory[7], herbs = game.save.inventory[2], coins = game.save.coins;
            HintOpen(game, garden);
            Check(game.mode == "dialog" && game.save.plantedAt < 0 && game.save.inventory[7] == seeds,
                "E talks to An without instantly planting the advertised empty plot");
            HintChoose(game, "plant"); float planted = game.save.plantedAt;
            Check(planted == game.save.totalTime && game.save.inventory[7] == seeds - 1
                && game.save.inventory[2] == herbs && game.save.coins == coins, "The original plant choice costs exactly one seed");
            HintText(game, garden, "交谈 · 作物还需 60秒", "Talk · crops ready in 60s");
            Advance(game, .02f);
            HintText(game, garden, "交谈 · 作物还需 60秒", "Talk · crops ready in 60s");
            HintOpen(game, garden); float paused = game.save.totalTime;
            Advance(game, 10);
            Check(game.save.totalTime == paused && game.save.plantedAt == planted, "An's actual dialogue pauses crop growth");
            HintText(game, garden, "交谈 · 作物还需 60秒", "Talk · crops ready in 60s");
            HintChoose(game, "harvest");
            Check(game.save.plantedAt == planted && game.save.inventory[2] == herbs && game.save.inventory[7] == seeds - 1,
                "The original early harvest choice pays nothing");
            Advance(game, 59.96f);
            float boundary = planted + 59.99f;
            game.Tick(boundary - game.save.totalTime, new Point());
            Check(game.save.totalTime == boundary && game.save.totalTime - planted < 60, "Active simulation reaches the explicit 59.99-second boundary");
            HintText(game, garden, "交谈 · 作物还需 1秒", "Talk · crops ready in 1s");
            HintOpen(game, garden); HintChoose(game, "harvest");
            Check(game.save.plantedAt == planted && game.save.inventory[2] == herbs && game.save.coins == coins,
                "A 59.99-second crop remains unharvestable through the real choice");
            game.Tick(.01f, new Point());
            Check(game.save.totalTime - planted == 60, "The next active tick reaches the exact sixty-second boundary");
            HintText(game, garden, "交谈 · 作物成熟", "Talk · crops ready");
            HintOpen(game, garden);
            Check(game.save.plantedAt == planted && game.save.inventory[2] == herbs,
                "E still opens An's dialogue for mature crops without instantly harvesting");
            HintChoose(game, "harvest");
            Check(game.save.plantedAt < 0 && game.save.inventory[2] == herbs + 5 && game.save.inventory[7] == seeds + 1
                && game.save.coins == coins + 8, "The real base harvest keeps five herbs, two seeds and eight coins");
            HintText(game, garden, "交谈 · 田地空置", "Talk · plot empty");
            int bought = game.save.inventory[7], money = game.save.coins;
            HintOpen(game, garden); HintChoose(game, "seeds");
            Check(game.save.inventory[7] == bought + 3 && game.save.coins == money - 5, "An still sells three seeds for five coins");
            game.save.inventory[3] = 1; int meal = game.save.inventory[6], herbStock = game.save.inventory[2], crystal = game.save.inventory[1];
            HintOpen(game, Object(game, "cook")); HintChoose(game, "cook");
            Check(game.save.inventory[3] == 0 && game.save.inventory[2] == herbStock - 2 && game.save.inventory[6] == meal + 1
                && game.save.inventory[1] == crystal, "The existing stew recipe still consumes one fish and two herbs");
            game.save.inventory[7] = 0; HintOpen(game, garden); HintChoose(game, "plant");
            Check(game.save.plantedAt < 0 && game.save.inventory[7] == 0, "The real no-seed plant rejection keeps an empty plot");
        }

        public static void InteractionHintSealsAndCachesMatchActualPowersAndOnceRewards(Campaign data)
        {
            var game = Unlocked(data); game.SkipTutorial();
            game.save.flags.RemoveAll(flag => flag.StartsWith("core_", StringComparison.Ordinal)
                || flag.StartsWith("seal_", StringComparison.Ordinal) || flag.StartsWith("cache_", StringComparison.Ordinal));
            int[] rooms = { 1, 2, 3, 5, 6, 7 }, powers = { 1, 2, 3, 2, 1, 3 };
            for (int index = 0; index < rooms.Length; index++)
            {
                int room = rooms[index], power = powers[index]; game.Enter(room); game.enemies.Clear();
                WorldObject seal = Object(game, "seal" + power), cache = Object(game, "cache" + power);
                game.save.flags.Remove("core_" + power);
                HintText(game, seal, "需要第 " + power + " 枚星核能力", "Requires exploration power from core " + power);
                HintText(game, cache, "先解除入口封印", "Release the entrance seal first");
                Check(game.Blocked(6.5f, 19.5f), "The actual entrance is still blocked before its power transaction");
                HintOpen(game, seal);
                Check(!game.Has("seal_" + room), "Actual E cannot release a seal without the matching core power");
                game.save.flags.Add("core_" + power);
                HintText(game, seal, "可解除封印", "Release the seal");
                int coins = game.save.coins, shards = game.save.shards, meals = game.save.inventory[6], mastery = game.save.mastery[game.save.weapon];
                game.Interact();
                Check(game.Has("seal_" + room) && !game.Blocked(6.5f, 19.5f), "Actual E with the earned power opens the real entrance");
                Check(game.save.coins == coins && game.save.shards == shards && game.save.inventory[6] == meals
                    && game.save.mastery[game.save.weapon] == mastery, "Releasing the entrance does not pay the cache early");
                HintText(game, seal, "旧路已开启", "Route open");
                HintText(game, cache, "可取补给", "Open the supply cache");
                HintOpen(game, cache);
                Check(game.mode == "dialog" && game.Has("cache_" + room) && game.Has("history_" + cache.lore)
                    && game.save.coins == coins + 30 && game.save.shards == shards + 3 && game.save.inventory[6] == meals + 1
                    && game.save.mastery[game.save.weapon] == Math.Min(100, mastery + 8),
                    "The actual opened cache pays the unchanged once-only currencies, stew, mastery and regional record");
                Check(game.conversation.text.StartsWith(data.lore[cache.lore].zh, StringComparison.Ordinal), "The actual first cache dialogue includes its authored lore");
                HintText(game, cache, "补给已取 · 可重读记录", "Supplies claimed · reread record");
                string awarded = LandmarkDescribe(game.save);
                HintOpen(game, cache);
                Check(LandmarkDescribe(game.save) == awarded && game.conversation.text == data.lore[cache.lore].zh,
                    "Repeating actual E rereads the original lore without another payout");
                game.Close(); game.OpenCodex(cache.lore);
                Check(game.conversation.text == data.lore[cache.lore].zh, "Claimed cache records remain readable in the original codex");
                game.Close();
            }
            Check(game.GetQuest("secrets").complete, "All six actual discoveries still complete the mature secret-route quest");
        }

        public static void InteractionHintPreservesNearestAndRejectsForeignOrUnsupportedTargets(Campaign data)
        {
            var game = Fresh(data); game.SkipTutorial(); WorldObject[] original = game.Map.objects;
            try
            {
                var herb = new WorldObject { id = "hint_overlap_herb", kind = "herb", x = 14, y = 14 };
                var ore = new WorldObject { id = "hint_overlap_ore", kind = "ore", x = 14, y = 14 };
                var garden = new WorldObject { id = "hint_overlap_garden", kind = "garden", x = 14, y = 14 };
                game.Map.objects = new[] { herb, ore, garden }; game.save.x = 14; game.save.y = 14;
                Check(ReferenceEquals(game.Nearest(), herb), "Equal-distance real E targets retain authored first-object precedence");
                for (int read = 0; read < 20; read++)
                {
                    HintText(game, ore, "可采集", "Gather supplies");
                    HintText(game, garden, "交谈 · 田地空置", "Talk · plot empty");
                    Check(ReferenceEquals(game.Nearest(), herb), "Querying a different owned target cannot change the real nearest owner");
                }
                int herbs = game.save.inventory[2], crystals = game.save.inventory[1]; game.Interact();
                Check(game.save.inventory[2] == herbs + 3 && game.save.inventory[1] == crystals
                    && game.Has("node_" + herb.id) && !game.Has("node_" + ore.id), "Actual overlap E still gathers only the first object");
                game.Map.objects = new[] { ore, herb, garden };
                HintText(game, herb, "已采集 · 回镇休息后更新", "Gathered · rest in town to renew");
                Check(ReferenceEquals(game.Nearest(), ore), "An authored order change remains the only change to the equal-distance E owner");
                game.Interact(); Check(game.save.inventory[1] == crystals + 3 && game.Has("node_" + ore.id), "The reordered real E transaction owns the ore");
                game.save.x = 15.75f;
                Check(game.Nearest() == null, "The original E radius excludes an object at exactly 1.75 units");
                HintText(game, herb, "已采集 · 回镇休息后更新", "Gathered · rest in town to renew");
                var copy = new WorldObject { id = herb.id, kind = herb.kind, x = herb.x, y = herb.y };
                HintText(game, copy, "", ""); HintText(game, null, "", "");
                HintText(game, data.maps[1].objects[0], "", "");
                foreach (string kind in new[] { null, "", "cook", "unknown", "seal0", "seal4", "seal1x" })
                {
                    var unsupported = new WorldObject { id = "hint_unknown", kind = kind, x = 14, y = 14 };
                    game.Map.objects = new[] { unsupported }; HintText(game, unsupported, "", "");
                }
            }
            finally { game.Map.objects = original; }
            HintText(game, Object(game, "returnscout"), "", "");
            WorldObject townGarden = Object(game, "garden"); game.StartPractice();
            Check(game.practicing, "The suppression fixture enters actual practice"); HintText(game, townGarden, "", ""); game.EndPractice();
            game.invulnerable = 0; game.save.health = 1;
            game.projectiles.Add(new Projectile { position = game.Position, damage = 999, source = "shot" }); game.Tick(.01f, new Point());
            Check(game.Dying, "An actual lethal collision starts the recovery state"); HintText(game, townGarden, "", "");
        }

        public static void InteractionHintQueriesAndLegacyReloadsKeepAllStateReadOnly(Campaign data)
        {
            foreach (FieldInfo field in typeof(SaveData).GetFields(BindingFlags.Public | BindingFlags.Instance))
                Check(field.Name.IndexOf("hint", StringComparison.OrdinalIgnoreCase) < 0, "Interaction hints add no persistent hint field: " + field.Name);
            foreach (int version in new[] { 1, 2 })
            {
                var original = Fresh(data); original.SkipTutorial(); original.save.totalTime = 27; original.save.plantedAt = 9;
                original.save.coins = 73; original.save.inventory[7] = 5; original.save.english = version == 1;
                original.save.flags.AddRange(new[] { "core_1", "seal_1", "cache_1", "node_herb1", "history_0" });
                original.PrepareSave(); SaveData checkpoint = CloneSave(original.save); checkpoint.version = version;
                var game = new GameCore(data, CloneSave(checkpoint)); var control = new GameCore(data, CloneSave(checkpoint));
                game.Start(); game.Close(); control.Start(); control.Close();
                Check(game.save.version == 2 && game.save.coins == 73 && game.save.plantedAt == 9 && game.save.inventory[7] == 5,
                    "Legacy migration keeps existing garden, supply and economic fields without a schema increase");
                WorldObject garden = Object(game, "garden"); WorldObject nearest = game.Nearest();
                string saved = LandmarkDescribe(game.save), simulation = LandmarkDescribe(game); string mode = game.mode; int generation = game.WorldGeneration;
                for (int read = 0; read < 30; read++)
                {
                    foreach (WorldObject target in game.Map.objects)
                    { game.InteractionHint(target, false); game.InteractionHint(target, true); }
                    HintText(game, garden, "交谈 · 作物还需 42秒", "Talk · crops ready in 42s");
                    Check(ReferenceEquals(game.Nearest(), nearest), "Repeated localized reads never replace Nearest");
                }
                Check(LandmarkDescribe(game.save) == saved && LandmarkDescribe(game) == simulation && game.mode == mode && game.WorldGeneration == generation,
                    "Hint queries preserve every public save and simulation field, effects, sound queue, actors and campaign object");
                game.PrepareSave(); control.PrepareSave();
                Check(LandmarkDescribe(game.save) == LandmarkDescribe(control.save), "Querying hints cannot alter the subsequent real save payload");
                game.Load(CloneSave(game.save)); game.Start(); game.Close();
                HintText(game, Object(game, "garden"), "交谈 · 作物还需 42秒", "Talk · crops ready in 42s");
                game.Enter(1); game.enemies.Clear();
                HintText(game, Object(game, "herb"), "已采集 · 回镇休息后更新", "Gathered · rest in town to renew");
                HintText(game, Object(game, "seal1"), "旧路已开启", "Route open");
                HintText(game, Object(game, "cache1"), "补给已取 · 可重读记录", "Supplies claimed · reread record");
                HintText(game, garden, "", "");
                Check(game.Has("node_herb1") && game.Has("cache_1") && game.Has("seal_1") && game.save.coins == 73,
                    "Existing depletion, route and claim flags survive old-version load and a later save/load without renewal or duplicate rewards");
            }
        }
    }
}
