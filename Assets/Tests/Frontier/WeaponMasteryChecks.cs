using System;
using System.Reflection;

namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        // Keep the new contract behind reflection so the same harness compiles
        // against an isolated pre-feature checkout and fails for a missing API.
        private static object MasteryCall(GameCore game, string name, int weapon)
        {
            MethodInfo method = typeof(GameCore).GetMethod(name, BindingFlags.Public | BindingFlags.Instance,
                null, new[] { typeof(int) }, null);
            Check(method != null, "Production must expose " + name + "(int)");
            return method.Invoke(game, new object[] { weapon });
        }
        private static object MasteryProgress(GameCore game, int weapon)
        { return MasteryCall(game, "GetWeaponMasteryProgress", weapon); }
        private static string MasteryText(GameCore game, int weapon)
        { return (string)MasteryCall(game, "WeaponMasteryProgressText", weapon); }
        private static T MasteryField<T>(object progress, string name)
        {
            Check(progress != null, "A valid weapon has a mastery snapshot");
            FieldInfo field = progress.GetType().GetField(name, BindingFlags.Public | BindingFlags.Instance);
            Check(field != null && field.FieldType == typeof(T) && field.IsInitOnly,
                "Mastery snapshots expose readonly " + typeof(T).Name + " " + name);
            return (T)field.GetValue(progress);
        }
        private static void MasteryMatches(object progress, int weapon, int points, bool core)
        {
            int rank = points / 20, threshold = points == 100 ? 100 : (rank + 1) * 20;
            Check(progress != null && progress.GetType().Name == "WeaponMasteryProgress" && progress.GetType().IsSealed,
                "The mastery contract is a sealed snapshot type");
            foreach (FieldInfo field in progress.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
                Check(field.IsInitOnly, "Every public mastery field is immutable: " + field.Name);
            foreach (PropertyInfo property in progress.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
                Check(!property.CanWrite, "A mastery snapshot has no mutable public property: " + property.Name);
            Check(MasteryField<int>(progress, "weapon") == weapon && MasteryField<int>(progress, "points") == points
                && MasteryField<int>(progress, "rank") == rank && MasteryField<int>(progress, "nextThreshold") == threshold
                && MasteryField<int>(progress, "remaining") == (points == 100 ? 0 : threshold - points)
                && MasteryField<int>(progress, "baseDamageBonus") == rank * 2
                && MasteryField<bool>(progress, "maximum") == (points == 100)
                && MasteryField<bool>(progress, "stylesUnlocked") == (core || rank >= 1)
                && Math.Abs(MasteryField<float>(progress, "fraction") - (points == 100 ? 1 : (points % 20) / 20f)) < .00001f,
                "Mastery snapshot reports exact points, bounded rank, next milestone, remaining points, base bonus and segment fill");
        }
        private static string MasteryReadState(GameCore game)
        { return LandmarkDescribe(game) + "/sound=" + game.sound; }
        private static string MasteryCompact(string text)
        { return text.Replace(" ", "").Replace("\u00a0", "").Replace("\n", "").Replace("\r", ""); }
        private static void MasteryLocalized(GameCore game, int weapon, int points)
        {
            string text = MasteryText(game, weapon), compact = MasteryCompact(text);
            Check(!String.IsNullOrEmpty(text) && text.Length < 180 && compact.Contains((points / 20) + "/5")
                && compact.Contains(points + "/100"), "A concise progress line includes rank/5 and points/100");
            if (game.save.english)
                Check(points == 100 ? text.ToLowerInvariant().Contains("max")
                    : (text.ToLowerInvariant().Contains("next") || text.ToLowerInvariant().Contains("remaining"))
                        && text.Contains(((points / 20 + 1) * 20 - points).ToString()),
                    "English progress explains the next remaining amount, or maximum mastery");
            else
                Check((points == 100 ? text.Contains("满") || text.Contains("最高")
                    : text.Contains("还") || text.Contains("距") || text.Contains("剩"))
                    && (points == 100 || text.Contains(((points / 20 + 1) * 20 - points).ToString())),
                    "Chinese progress explains the remaining amount, or maximum mastery");
        }

        public static void WeaponMasteryThresholdsAndLocalizedReadsArePure(Campaign data)
        {
            foreach (bool english in new[] { false, true }) for (int weapon = 0; weapon < 3; weapon++)
                foreach (int points in new[] { 0, 18, 19, 20, 21, 98, 99, 100 })
                    foreach (string mode in new[] { "world", "dialog" })
                    {
                        var game = Fresh(data); game.save.english = english; game.mode = mode;
                        game.save.weapon = (weapon + 1) % 3; game.save.mastery[weapon] = points;
                        string before = MasteryReadState(game);
                        MasteryMatches(MasteryProgress(game, weapon), weapon, points, false);
                        MasteryLocalized(game, weapon, points);
                        Check(MasteryReadState(game) == before,
                            "Reading any weapon in either language or menu cannot mutate save, revision, feedback, conversation or combat state");
                    }
        }

        public static void WeaponMasteryInvalidClampedReadsAndSnapshotsStayIndependent(Campaign data)
        {
            var game = Fresh(data); game.save.mastery = new[] { 19, 20, 99 };
            object held = MasteryProgress(game, 0); game.save.mastery[0] = 100;
            game.save.flags.Add("core_1"); game.save.english = !game.save.english;
            MasteryMatches(held, 0, 19, false); MasteryMatches(MasteryProgress(game, 0), 0, 100, true);
            for (int weapon = 0; weapon < 3; weapon++)
            {
                game.save.mastery[weapon] = 0;
                MasteryMatches(MasteryProgress(game, weapon), weapon, 0, true);
            }
            game.save.flags.Remove("core_1"); game.save.weapon = 0; game.save.mastery[1] = 20;
            Check(!game.Has("weapon_1"), "The ownership check starts with an uncrafted bow");
            MasteryMatches(MasteryProgress(game, 1), 1, 20, false); game.Equip(1);
            Check(game.save.weapon == 0, "A mastered preview never grants ownership or bypasses the existing equip gate");
            foreach (int weapon in new[] { Int32.MinValue, -1, 3, Int32.MaxValue })
            {
                string before = MasteryReadState(game);
                Check(MasteryProgress(game, weapon) == null && MasteryText(game, weapon) == String.Empty
                    && before == MasteryReadState(game), "Invalid weapon reads return no quote or text without touching state");
            }
            foreach (int points in new[] { Int32.MinValue, -40, -1, 101, 140, Int32.MaxValue })
                for (int weapon = 0; weapon < 3; weapon++)
                {
                    game.save.mastery[weapon] = points; string before = MasteryReadState(game);
                    int bounded = Math.Max(0, Math.Min(100, points));
                    MasteryMatches(MasteryProgress(game, weapon), weapon, bounded, false);
                    MasteryLocalized(game, weapon, bounded);
                    Check(before == MasteryReadState(game) && game.save.mastery[weapon] == points,
                        "Malformed numeric progress is clamped only in the read snapshot, with no save repair side effect");
                }
            foreach (int[] malformed in new int[][] { null, new int[0], new[] { 19 } })
            {
                game.save.mastery = malformed;
                for (int weapon = 0; weapon < 3; weapon++)
                {
                    string before = MasteryReadState(game); int points = malformed != null && weapon < malformed.Length ? malformed[weapon] : 0;
                    MasteryMatches(MasteryProgress(game, weapon), weapon, points, false); MasteryLocalized(game, weapon, points);
                    Check(before == MasteryReadState(game) && System.Object.ReferenceEquals(game.save.mastery, malformed),
                        "A null or short mastery array reads missing entries as zero without allocating or repairing the save");
                }
            }
            MasteryMatches(held, 0, 19, false);
        }

        public static void WeaponMasteryBaseBonusMatchesExistingDamageAndEveryProfileMultiplier(Campaign data)
        {
            foreach (bool abyss in new[] { false, true }) for (int equipped = 0; equipped < 3; equipped++)
                for (int weapon = 0; weapon < 3; weapon++) foreach (int points in new[] { 0, 19, 20, 99, 100 })
                {
                    var game = Unlocked(data);
                    if (abyss) { game.StartAbyss(0); Check(game.InAbyss, "Damage comparison uses a real Abyss run"); game.save.boons[0] = 2; }
                    game.save.weapon = equipped; game.save.mastery = new[] { 40, 60, 80 }; game.save.mastery[weapon] = 0;
                    float damage = game.Damage; float[] profiles = { game.Profile(weapon, 0).damage,
                        game.Profile(weapon, 1).damage, game.Profile(weapon, 2).damage };
                    game.save.mastery[weapon] = points; object progress = MasteryProgress(game, weapon);
                    int bonus = MasteryField<int>(progress, "baseDamageBonus"); float boon = abyss ? 1.3f : 1;
                    Check(bonus == points / 20 * 2
                        && Math.Abs(game.Damage - damage - (equipped == weapon ? bonus * boon : 0)) < .0001f,
                        "The reported bonus equals the existing equipped base-damage contribution before Abyss scaling");
                    for (int style = 0; style < 3; style++)
                    {
                        float factor = (weapon == 1 ? .9f : weapon == 2 ? 1.25f : 1) * (style == 1 ? 1.4f : style == 2 ? .8f : 1);
                        Check(Math.Abs(game.Profile(weapon, style).damage - profiles[style] - bonus * boon * factor) < .0001f,
                            "Every inspected weapon profile applies its existing weapon, style and Abyss multipliers exactly once");
                    }
                }
        }

        public static void WeaponMasteryRealRewardsRefreshMilestonesRetainReleasedCreditAndExcludeTraining(Campaign data)
        {
            for (int weapon = 0; weapon < 3; weapon++) foreach (int initial in new[] { 18, 98 })
            {
                var game = Quiet(data, weapon); game.save.mastery[weapon] = initial; game.save.seed = 1;
                object before = MasteryProgress(game, weapon); string text = MasteryText(game, weapon);
                game.SetStyle(1); Check(game.Style == (initial < 20 ? 0 : 1), "The mature style gate agrees with the pre-kill mastery rank");
                if (game.Style != 0) game.SetStyle(0);
                int coins = game.save.coins, xp = game.save.xp, kills = game.save.kills[1], herbs = game.save.inventory[2];
                var target = new Enemy(9700 + weapon, 0, game.save.x + (weapon == 0 ? 1.2f : 4), game.save.y, 1) { stun = 999 };
                game.enemies.Add(target); game.Attack(new Point(1, 0));
                for (int frame = 0; frame < 150 && game.enemies.Contains(target); frame++) game.Tick(.02f, new Point());
                Check(!game.enemies.Contains(target) && game.save.mastery[weapon] == initial + 2
                    && game.save.coins == coins + 6 && game.save.xp == xp + 12 && game.save.kills[1] == kills + 1
                    && game.save.inventory[2] == herbs + 1, "An actual weapon-owned kill retains its existing mastery and combat rewards");
                MasteryMatches(MasteryProgress(game, weapon), weapon, initial + 2, false);
                MasteryMatches(before, weapon, initial, false);
                Check(MasteryText(game, weapon) != text && game.StylesUnlocked, "The live line and style availability reflect the earned milestone immediately");
                game.SetStyle(1); Check(game.Style == 1, "The original style action accepts an earned milestone");
            }
            var cache = Quiet(data); cache.save.flags.Add("seal_1"); cache.save.mastery[0] = 12;
            object oldCache = MasteryProgress(cache, 0); int cacheCoins = cache.save.coins, shards = cache.save.shards, stew = cache.save.inventory[6];
            cache.Talk(Object(cache, "cache1"));
            Check(cache.save.mastery[0] == 20 && cache.save.coins == cacheCoins + 30 && cache.save.shards == shards + 3
                && cache.save.inventory[6] == stew + 1 && cache.StylesUnlocked, "The real sealed-road cache preserves its eight-point reward and other loot");
            MasteryMatches(oldCache, 0, 12, false); MasteryMatches(MasteryProgress(cache, 0), 0, 20, false);
            cache.Close(); cache.Talk(Object(cache, "cache1")); Check(cache.save.mastery[0] == 20, "Reopening the actual cache cannot farm mastery");

            var arrow = Quiet(data, 1); arrow.save.mastery[1] = 18; arrow.save.mastery[0] = 80;
            var distant = new Enemy(9705, 0, 18, 14, 1) { stun = 999 }; arrow.enemies.Add(distant);
            arrow.Attack(new Point(1, 0)); for (int frame = 0; frame < 30 && arrow.projectiles.Count == 0; frame++) arrow.Tick(.02f, new Point());
            Check(arrow.projectiles.Count == 1, "The delayed-credit case launches the real bow action");
            float releasedDamage = arrow.projectiles[0].damage; arrow.Equip(0); arrow.SetStyle(2);
            Check(arrow.projectiles[0].damage == releasedDamage, "The progress feature leaves prepared projectile damage intact after an equipment change");
            for (int frame = 0; frame < 100 && arrow.enemies.Contains(distant); frame++) arrow.Tick(.02f, new Point());
            Check(!arrow.enemies.Contains(distant) && arrow.save.weapon == 0 && arrow.save.mastery[0] == 80,
                "A delayed bow kill cannot credit or alter the later equipped sword");
            MasteryMatches(MasteryProgress(arrow, 1), 1, 20, false);
            var prepared = Quiet(data, 1); prepared.save.seed = 1; prepared.save.mastery[1] = 20;
            var survivor = new Enemy(9706, 0, 18, 14, 999) { stun = 999 }; prepared.enemies.Add(survivor);
            prepared.Attack(new Point(1, 0));
            for (int frame = 0; frame < 30 && prepared.projectiles.Count == 0; frame++) prepared.Tick(.02f, new Point());
            Check(prepared.projectiles.Count == 1, "The retained-stat case launches a real arrow");
            float preparedDamage = prepared.projectiles[0].damage; prepared.Equip(0); prepared.save.mastery[0] = 100;
            prepared.save.talents[0] = 5; prepared.SetStyle(2);
            for (int frame = 0; frame < 100 && survivor.hp == 999; frame++) prepared.Tick(.02f, new Point());
            Check(Math.Abs(999 - survivor.hp - preparedDamage) < .001f,
                "An actual delayed contact retains its released damage despite later mastery, talent and style changes");
            for (int weapon = 0; weapon < 3; weapon++)
            {
                var range = RangeGame(data, weapon); range.save.mastery[weapon] = 18;
                Check(range.StartRange(0), "The mastery exclusion check enters the actual training range");
                Enemy dummy = range.enemies[0]; dummy.hp = 1; int coins = range.save.coins, xp = range.save.xp;
                RangeRealAttack(range, dummy);
                Check(!range.enemies.Contains(dummy) && range.save.mastery[weapon] == 18
                    && range.save.coins == coins && range.save.xp == xp, "Actual lethal training contacts cannot farm progress or combat rewards");
                MasteryMatches(MasteryProgress(range, weapon), weapon, 18, true); range.EndPractice();
            }
        }

        public static void WeaponMasteryV1V2ResumptionPreservesSavedProgressAndOtherFields(Campaign data)
        {
            foreach (int version in new[] { 1, 2 }) foreach (bool english in new[] { false, true })
            {
                var original = Quiet(data); original.save.version = version; original.save.english = english;
                original.save.mastery = new[] { 19, 20, 100 }; original.save.weaponStyles = new[] { 0, 2, 1 };
                original.save.coins = 173; original.save.shards = 23; original.save.kills[1] = 7;
                original.save.projects[1] = 2; original.save.flags.Add("weapon_1"); original.save.flags.Add("mastery_legacy_story");
                original.save.inventory[3] = 11; original.PrepareSave(); SaveData checkpoint = CloneSave(original.save);
                // Compare with a control reconstruction to isolate this read-only
                // feature from the established v1 migration and transient reset.
                var restored = new GameCore(data, CloneSave(checkpoint)); var control = new GameCore(data, CloneSave(checkpoint));
                Check(restored.save.version == 2 && restored.save.mastery[0] == 19 && restored.save.mastery[1] == 20
                    && restored.save.mastery[2] == 100 && restored.save.weaponStyles[1] == 2
                    && restored.save.weaponStyles[2] == 1 && restored.save.coins == 173 && restored.save.shards == 23
                    && restored.save.kills[1] == 7 && restored.save.projects[1] == 2 && restored.save.inventory[3] == 11
                    && restored.Has("mastery_legacy_story"), "Both save versions retain mastery, styles, ownership, story, rewards and projects without a new schema");
                string before = MasteryReadState(restored);
                for (int weapon = 0; weapon < 3; weapon++)
                { MasteryMatches(MasteryProgress(restored, weapon), weapon, checkpoint.mastery[weapon], false); MasteryLocalized(restored, weapon, checkpoint.mastery[weapon]); }
                Check(before == MasteryReadState(restored) && LandmarkDescribe(restored.save) == LandmarkDescribe(control.save),
                    "Resumed progress and localized text leave every reconstructed save field identical to its untouched control");
            }
        }
    }
}
