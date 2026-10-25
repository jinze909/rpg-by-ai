using System;
using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Text;

namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        private static void PlanStatsNear(float actual, float expected, string label)
        { Check(!float.IsNaN(actual) && !float.IsInfinity(actual) && Math.Abs(actual - expected) < .0005f,
            label + ": expected " + expected + ", got " + actual); }

        private static void PlanStatsEqual(BattlePlanCombatStats actual, BattlePlanCombatStats expected, string label)
        {
            Check(actual != null && expected != null, label + " has two snapshots");
            PlanStatsNear(actual.attackDamage, expected.attackDamage, label + " attack damage");
            PlanStatsNear(actual.attackInterval, expected.attackInterval, label + " attack interval");
            PlanStatsNear(actual.attackEnergy, expected.attackEnergy, label + " attack energy");
            PlanStatsNear(actual.skillEnergy, expected.skillEnergy, label + " skill energy");
            PlanStatsNear(actual.dodgeEnergy, expected.dodgeEnergy, label + " dodge energy");
            PlanStatsNear(actual.walkSpeed, expected.walkSpeed, label + " walk speed");
        }

        private static void PlanStatsMatchEquipped(GameCore game, BattlePlanCombatStats stats, string label)
        {
            Check(stats != null, label + " exists");
            WeaponProfile profile = game.Profile(game.save.weapon, game.Style);
            PlanStatsNear(stats.attackDamage, profile.damage * (game.save.weapon == 1 && game.Style == 2 ? .65f : 1), label + " opening contact");
            PlanStatsNear(stats.attackInterval, profile.interval, label + " actual cadence");
            PlanStatsNear(stats.attackEnergy, profile.energy, label + " actual basic cost");
            PlanStatsNear(stats.skillEnergy, game.Relic(4) ? 50 : 40, label + " class Q cost");
            PlanStatsNear(stats.dodgeEnergy, game.Relic(2) ? 25 : 20, label + " dodge cost");
            PlanStatsNear(stats.walkSpeed, game.Speed, label + " live walk speed");
        }

        // Preserve every public value, including nested save records, active
        // contacts and queued sounds. Private intent references are checked too.
        private static void PlanStatsAppend(StringBuilder text, object value)
        {
            if (value == null) { text.Append("null;"); return; }
            if (value is string) { string s = (string)value; text.Append(s.Length).Append(':').Append(s).Append(';'); return; }
            if (value.GetType().IsPrimitive || value.GetType().IsEnum)
            { text.Append(Convert.ToString(value, CultureInfo.InvariantCulture)).Append(';'); return; }
            if (value is IEnumerable)
            { text.Append('['); foreach (object item in (IEnumerable)value) PlanStatsAppend(text, item); text.Append(']'); return; }
            text.Append('{');
            foreach (FieldInfo field in value.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
            { text.Append(field.Name).Append('='); PlanStatsAppend(text, field.GetValue(value)); }
            text.Append('}');
        }

        private static object PlanStatsIntent(GameCore game, string name)
        {
            FieldInfo field = typeof(GameCore).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
            Check(field != null, "The real existing buffered field is available: " + name);
            return field.GetValue(game);
        }

        private static string PlanStatsState(GameCore game)
        {
            var text = new StringBuilder(); PlanStatsAppend(text, game);
            text.Append('/').Append(game.mode).Append('/').Append(game.sound).Append('/').Append(game.WorldGeneration);
            foreach (string name in new[] { "bufferedAttack", "bufferedSkill", "bufferedDodge", "bufferedCompanionCommand" })
                PlanStatsAppend(text, PlanStatsIntent(game, name));
            return text.ToString();
        }

        private static void PlanStatsReadRepeatedly(GameCore game)
        {
            for (int repeat = 0; repeat < 12; repeat++)
            {
                Check(game.CurrentBattlePlanStats() != null, "The equipped valid build has a live snapshot");
                for (int slot = 0; slot < GameCore.BattlePlanSlots; slot++) game.BattlePlanStatsAt(slot);
                Check(game.BattlePlanStatsAt(-1) == null && game.BattlePlanStatsAt(3) == null, "Invalid read indices remain harmless");
            }
        }

        public static void BattlePlanStatsCurrentMatchOpeningContactsAndActualCosts(Campaign data)
        {
            for (int profession = 0; profession < 3; profession++) for (int weapon = 0; weapon < 3; weapon++)
            {
                var game = PlanCombatGame(data, profession); game.mode = "plans";
                int style = (profession + weapon) % 3;
                PlanGear(game, weapon, style, profession % 2, weapon == 0 ? 2 : 4, 5);
                BattlePlanCombatStats stats = game.CurrentBattlePlanStats();
                PlanStatsMatchEquipped(game, stats, "Profession " + profession + ", weapon " + weapon + ", style " + style);
                game.Close(); game.save.energy = 100;
                game.Attack(new Point(1, 0));
                Check(game.playerAction != null && !game.playerAction.released && game.playerAction.combo == 0,
                    "An actual opening basic preparation exists");
                PlanStatsNear(100 - game.save.energy, stats.attackEnergy, "Actual attack payment");
                PlanStatsNear(game.attackCooldown, stats.attackInterval, "Actual attack cooldown");
                PlanStatsNear(game.playerAction.damage * (weapon == 1 && style == 2 ? .65f : 1), stats.attackDamage,
                    "Accepted basic action carries the displayed contact damage");
                game.save.energy = 100; game.Dash(new Point(0, 1));
                Check(game.dashTime > 0 && game.playerAction == null, "A real dodge starts and cancels the pending basic action");
                PlanStatsNear(100 - game.save.energy, stats.dodgeEnergy, "Actual dodge payment");
                game.dashTime = 0; game.save.energy = 100; game.Skill(new Point(1, 0));
                Check(game.playerAction != null && game.playerAction.kind == "skill", "A real class Q starts");
                PlanStatsNear(100 - game.save.energy, stats.skillEnergy, "Actual class Q payment");
            }

            var flow = PlanCombatGame(data, 1); flow.mode = "plans"; PlanGear(flow, 1, 2, 0, -1, -1);
            BattlePlanCombatStats arrows = flow.CurrentBattlePlanStats(); flow.Close(); flow.Attack(new Point(1, 0));
            ActorAction action = flow.playerAction;
            Check(action != null && !action.released, "The Flow bow begins its real preparation");
            for (int frame = 0; frame < 60 && !action.released; frame++) flow.Tick(.01f, new Point());
            Check(action.released && flow.projectiles.Count == 3, "Flow releases the actual three-arrow spread");
            foreach (Projectile shot in flow.projectiles)
                PlanStatsNear(shot.damage, arrows.attackDamage, "The displayed number is each real Flow arrow, not its three-arrow total");

            var counter = PlanCombatGame(data, 0); counter.mode = "plans"; PlanGear(counter, 0, 1, 0, 5, -1);
            counter.combo = 1; counter.comboTimer = 1; counter.riposteTime = 1;
            BattlePlanCombatStats opening = counter.CurrentBattlePlanStats(); counter.Close(); counter.Attack(new Point(1, 0));
            Check(counter.playerAction != null && counter.playerAction.combo == 2 && counter.playerAction.impact.counter,
                "The fixture accepts a real third-hit counter with critical-capable equipment");
            PlanStatsNear(counter.playerAction.damage, opening.attackDamage * 1.25f * 1.5f,
                "The comparison remains an opening noncritical hit and excludes transient combo and counter multipliers");
        }

        public static void BattlePlanStatsCandidatesMatchAppliedBuildsAndLiveConditions(Campaign data)
        {
            int[,] pairs = { { -1, -1 }, { 1, 2 }, { 4, 5 }, { 0, 3 } };
            for (int profession = 0; profession < 3; profession++) for (int weapon = 0; weapon < 3; weapon++)
            {
                var game = PlanGame(data, profession); int index = (profession + weapon) % 4;
                int style = (profession + weapon) % 3;
                game.save.upgrades = 2; game.save.level = 7; game.save.talents = new[] { 2, 1, 3 };
                game.save.mastery = new[] { 0, 42, 100 }; game.save.relicRank = 3;
                game.save.maxHealth = 200; game.save.health = index % 2 == 0 ? 160 : 159;
                game.save.totalTime = 100; game.save.foodUntil = index % 2 == 0 ? 101 : 100;
                game.save.cold = index % 2 == 0 ? 71 : 70;
                game.save.mealKind = 3; game.save.mealUntil = index % 2 == 0 ? 101 : 100;
                game.racing = index == 3;
                if (index >= 2) { game.save.room = 8; game.save.abyssFloor = 2; game.save.boons[0] = 2; }
                PlanGear(game, weapon, style, index % 2, pairs[index, 0], pairs[index, 1]);
                Check(game.SaveBattlePlan(1), "The candidate is recorded through the actual plan action");
                PlanGear(game, (weapon + 1) % 3, (style + 1) % 3, 1 - index % 2, 3, -1);
                BattlePlanCombatStats candidate = game.BattlePlanStatsAt(1);
                Check(!game.BattlePlanIsCurrent(1) && game.ApplyBattlePlan(1), "The candidate must actually change the equipped build");
                PlanStatsMatchEquipped(game, candidate, "Applied candidate for profession " + profession + ", weapon " + weapon);
                PlanStatsEqual(candidate, game.CurrentBattlePlanStats(), "Candidate versus actually equipped snapshot");
            }

            var live = PlanGame(data); PlanGear(live, 0, 0, 0, 1, 2); Check(live.SaveBattlePlan(0), "The conditional reference plan exists");
            live.save.upgrades = 2; live.save.level = 7; live.save.talents = new[] { 2, 1, 3 };
            live.save.mastery = new[] { 20, 80, 100 }; live.save.relicRank = 3;
            live.save.maxHealth = 100; live.save.health = 80; live.save.totalTime = 100; live.save.foodUntil = 101;
            live.save.cold = 71; live.save.mealKind = 3; live.save.mealUntil = 101;
            BattlePlanCombatStats atThreshold = live.BattlePlanStatsAt(0);
            PlanStatsNear(atThreshold.attackDamage, 66, "Known paid growth, sword mastery, ranked relic, active food and inclusive 80% Amber threshold");
            PlanStatsNear(atThreshold.walkSpeed, 5.01f, "Known walk talent, Wind Compass, cold and active Starpath meal");
            live.save.health = 79.99f;
            PlanStatsNear(live.BattlePlanStatsAt(0).attackDamage, 60, "Amber damage ends below the health threshold");
            live.save.foodUntil = 100; live.save.mealUntil = 100;
            PlanStatsNear(live.BattlePlanStatsAt(0).attackDamage, 53, "Food expires at the current clock");
            PlanStatsNear(live.BattlePlanStatsAt(0).walkSpeed, 4.71f, "The walk meal expires at the current clock");
            live.save.room = 8; live.save.abyssFloor = 1; live.save.boons[0] = 2;
            PlanStatsNear(live.BattlePlanStatsAt(0).attackDamage, 68.9f, "Existing Abyss damage boons affect the candidate in the current world");
            live.save.room = 0; live.save.mealUntil = 101; live.racing = true;
            PlanStatsNear(live.BattlePlanStatsAt(0).walkSpeed, 5.36f, "Racing keeps its existing speed and suppresses the walk-only meal");
            PlanStatsNear(atThreshold.attackDamage, 66, "An earlier returned damage snapshot remains immutable after live conditions change");
            PlanStatsNear(atThreshold.walkSpeed, 5.01f, "An earlier returned speed snapshot remains immutable after live conditions change");
        }

        public static void BattlePlanStatsReadsPreserveSnapshotsCombatQueuesAndWholeState(Campaign data)
        {
            Check(typeof(BattlePlanCombatStats).IsSealed, "A comparison snapshot is sealed");
            FieldInfo[] fields = typeof(BattlePlanCombatStats).GetFields(BindingFlags.Public | BindingFlags.Instance);
            Check(fields.Length == 6, "Exactly the six requested combat values are exposed");
            foreach (FieldInfo field in fields)
                Check(field.IsInitOnly && field.FieldType == typeof(float), "Each combat value is a readonly float: " + field.Name);
            foreach (PropertyInfo property in typeof(BattlePlanCombatStats).GetProperties(BindingFlags.Public | BindingFlags.Instance))
                Check(!property.CanWrite, "Comparison snapshots expose no writable property");

            var game = PlanCombatGame(data, 1); game.mode = "plans"; PlanGear(game, 2, 2, 1, 2, 4);
            Check(game.SaveBattlePlan(0), "An alternative comparison exists while the current bow prepares");
            PlanGear(game, 1, 0, 0, 1, 5); game.Close(); game.Attack(new Point(1, 0));
            ActorAction prepared = game.playerAction; ImpactProfile impact = prepared.impact;
            SaveData owner = game.save; int[] styles = owner.weaponStyles, relics = owner.relics;
            BattlePlan[] plans = owner.battlePlans; BattlePlan stored = plans[0];
            string before = PlanStatsState(game); PlanStatsReadRepeatedly(game);
            Check(PlanStatsState(game) == before && ReferenceEquals(game.save, owner) && ReferenceEquals(owner.weaponStyles, styles)
                && ReferenceEquals(owner.relics, relics) && ReferenceEquals(owner.battlePlans, plans) && ReferenceEquals(plans[0], stored)
                && ReferenceEquals(game.playerAction, prepared) && ReferenceEquals(prepared.impact, impact),
                "Reading preserves the full save/economy/story/RNG, mode/revision/sound queue, array identity and genuine prepared action identity");
            for (int frame = 0; frame < 60 && !prepared.released; frame++) game.Tick(.01f, new Point());
            Check(prepared.released && game.projectiles.Count == 1, "The untouched preparation still releases its original real arrow");
            Projectile shot = game.projectiles[0]; before = PlanStatsState(game); PlanStatsReadRepeatedly(game);
            Check(PlanStatsState(game) == before && ReferenceEquals(game.projectiles[0], shot) && ReferenceEquals(shot.impact, impact),
                "Repeated comparisons do not advance, consume, recapture or replace an already launched projectile and its effects");

            string[] kinds = { "attack", "skill", "dodge", "companion" };
            string[] intentFields = { "bufferedAttack", "bufferedSkill", "bufferedDodge", "bufferedCompanionCommand" };
            for (int i = 0; i < kinds.Length; i++)
            {
                var queued = PlanCombatGame(data, 1); queued.mode = "plans";
                PlanGear(queued, 2, 1, 1, 2, 4); Check(queued.SaveBattlePlan(0), "The queued-action candidate exists");
                PlanGear(queued, 1, 0, 0, -1, -1); queued.Close();
                if (i == 0) { queued.attackCooldown = .08f; queued.PressAttack(new Point(0, 1)); }
                if (i == 1) { queued.skillCooldown = .08f; queued.Skill(new Point(0, 1)); }
                if (i == 2) { queued.dashCooldown = .08f; queued.Dash(new Point(0, 1)); }
                if (i == 3) { queued.save.flags.Add("companion_1"); queued.SelectCompanion(1); queued.companionCooldown = .08f; queued.CommandCompanion(); }
                object intent = PlanStatsIntent(queued, intentFields[i]);
                Check(intent != null, "A genuine pending " + kinds[i] + " intent exists before querying");
                before = PlanStatsState(queued); PlanStatsReadRepeatedly(queued);
                Check(PlanStatsState(queued) == before && ReferenceEquals(PlanStatsIntent(queued, intentFields[i]), intent),
                    "Comparison preserves the existing " + kinds[i] + " intent, its aim, owner, lifetime and unpaid energy");
                Advance(queued, .12f);
                Check(i == 0 ? queued.playerAction != null && queued.playerAction.kind == "attack" :
                    i == 1 ? queued.playerAction != null && queued.playerAction.kind == "skill" :
                    i == 2 ? queued.dashTime > 0 : queued.companionAction != null,
                    "The same pending " + kinds[i] + " still dispatches normally after the reads");
            }
            foreach (string mode in new[] { "plans", "world", "pause", "inventory", "title", "practice" })
            {
                game.mode = mode; game.deathTime = .3f; before = PlanStatsState(game); PlanStatsReadRepeatedly(game);
                Check(PlanStatsState(game) == before, "Read-only comparison also retains a defeated hero and mode " + mode);
            }
        }

        public static void BattlePlanStatsInvalidLockedAndLegacyRecordsRemainReadOnly(Campaign data)
        {
            for (int version = 1; version <= 2; version++)
            {
                var game = new GameCore(data, new SaveData { version = version, battlePlans = null, coins = 87, health = 62, energy = 44 });
                string before = PlanStatsState(game);
                Check(game.CurrentBattlePlanStats() != null, "A migrated v" + version + " starter has current stats");
                for (int slot = 0; slot < 3; slot++) Check(game.BattlePlanStatsAt(slot) == null, "Legacy empty plans do not acquire invented comparisons");
                foreach (int slot in new[] { -1, 3, int.MinValue, int.MaxValue })
                    Check(game.BattlePlanStatsAt(slot) == null, "An out-of-range slot has no comparison");
                Check(PlanStatsState(game) == before && game.save.coins == 87 && game.save.health == 62 && game.save.energy == 44,
                    "Reading the migrated v" + version + " save retains its paid progress and vitals");
                game.save.battlePlans = null; before = PlanStatsState(game);
                Check(game.BattlePlanStatsAt(0) == null && PlanStatsState(game) == before && game.save.battlePlans == null,
                    "A query tolerates a missing live array without normalizing or replacing it");
            }

            var locked = new BattlePlan { recorded = true, weapon = 2, style = 2, technique = 1, relicFirst = 2, relicSecond = 4 };
            var retained = new GameCore(data, new SaveData { coins = 93, battlePlans = new[] { locked } });
            retained.mode = "plans"; string lockedBefore = PlanStatsState(retained);
            BattlePlanCombatStats theoretical = retained.BattlePlanStatsAt(0);
            Check(theoretical != null && !retained.BattlePlanAvailable(0) && !retained.ApplyBattlePlan(0)
                && PlanStatsState(retained) == lockedBefore && retained.BattlePlanAt(0).weapon == 2,
                "A structurally valid locked plan has a theoretical comparison while actual apply stays rejected and atomic");
            Check(!retained.Has("weapon_2") && !retained.Has("technique_0") && !retained.Has("relic_2") && !retained.Has("relic_4"),
                "Comparison never grants any candidate unlock");
            foreach (string flag in new[] { "weapon_2", "core_1", "technique_0", "relic_2", "relic_4" }) retained.save.flags.Add(flag);
            Check(retained.ApplyBattlePlan(0), "The same plan becomes applicable only after its real unlocks exist");
            PlanStatsMatchEquipped(retained, theoretical, "Locked theoretical stats versus later genuinely applicable equipment");
            retained.PrepareSave(); var resumed = new GameCore(data, CloneSave(retained.save));
            PlanStatsEqual(resumed.BattlePlanStatsAt(0), theoretical, "Saved recipe comparison after the existing save reconstruction");

            var malformed = PlanGame(data);
            foreach (BattlePlan plan in new[] {
                new BattlePlan(), new BattlePlan { recorded = true, weapon = 3 },
                new BattlePlan { recorded = true, style = -1 }, new BattlePlan { recorded = true, technique = 2 },
                new BattlePlan { recorded = true, relicFirst = -2 }, new BattlePlan { recorded = true, relicSecond = 6 },
                new BattlePlan { recorded = true, relicFirst = 2, relicSecond = 2 } })
            {
                malformed.save.battlePlans = new[] { plan }; string before = PlanStatsState(malformed);
                Check(malformed.BattlePlanStatsAt(0) == null && malformed.BattlePlanStatsAt(1) == null && malformed.BattlePlanStatsAt(2) == null
                    && ReferenceEquals(malformed.save.battlePlans[0], plan) && PlanStatsState(malformed) == before,
                    "Malformed/unrecorded or short live arrays return no comparison without silently repairing the saved record");
            }
        }

        public static void BattlePlanStatsWeaponContactsMatchNineActualReleases(Campaign data)
        {
            for (int weapon = 0; weapon < 3; weapon++) for (int style = 0; style < 3; style++)
            {
                var game = PlanCombatGame(data, weapon); game.mode = "plans"; PlanGear(game, weapon, style, 0, -1, -1);
                Check(game.SaveBattlePlan(0), "The opening attack comparison is backed by a genuine saved equipment plan");
                WeaponProfile original = game.Profile(weapon, style);
                float displayed = game.WeaponContactDamage(weapon, style);
                PlanStatsNear(displayed, game.CurrentBattlePlanStats().attackDamage, "Weapon card versus current battle-plan opening contact");
                PlanStatsNear(displayed, game.BattlePlanStatsAt(0).attackDamage, "Weapon card versus stored battle-plan opening contact");
                PlanStatsNear(original.interval, (weapon == 0 ? .34f : weapon == 1 ? .44f : .62f) * (style == 1 ? 1.3f : style == 2 ? .8f : 1),
                    "The original weapon/style cadence remains authoritative");
                PlanStatsNear(original.energy, style == 1 ? 4 : style == 2 ? 3 : 0, "The original style energy price remains authoritative");
                Enemy target = null;
                if (weapon == 0) { target = new Enemy(99812 + style, 0, 13.1f, 14, 500) { stun = 99 }; game.enemies.Add(target); }
                game.Close(); game.save.energy = 100; game.Attack(new Point(1, 0)); ActorAction action = game.playerAction;
                Check(action != null && !action.released && action.combo == 0 && action.kind == "attack"
                    && action.weapon == weapon && action.style == style, "Every one of the nine original attacks genuinely prepares before its contact");
                PlanStatsNear(action.damage, original.damage, "The comparison does not replace or pre-scale captured combat damage");
                PlanStatsNear(action.duration, original.interval, "The unchanged original duration is captured");
                PlanStatsNear(action.contact, original.interval * (weapon == 1 ? .55f : .45f), "The unchanged original contact time is captured");
                PlanStatsNear(game.attackCooldown, original.interval, "An actual attack pays the original cooldown");
                PlanStatsNear(100 - game.save.energy, original.energy, "An actual attack pays the original energy cost");
                int frames = 0;
                while (action.elapsed + .0011f < action.contact && frames++ < 1000) game.Tick(.001f, new Point());
                Check(!action.released && game.projectiles.Count == 0 && (target == null || target.hp == target.maxHp),
                    "No damage or missile is released ahead of the original contact boundary");
                for (int step = 0; step < 4 && !action.released; step++) game.Tick(.001f, new Point());
                Check(action.released && action.elapsed + .00001f >= action.contact && action.elapsed <= action.contact + .0011f,
                    "Substep simulation releases the attack at its original contact boundary");
                if (weapon == 0)
                {
                    Check(target.hp < target.maxHp && game.projectiles.Count == 0, "The real sword release hits the actual unarmored nearby enemy");
                    PlanStatsNear(target.maxHp - target.hp, displayed, "The sword card matches real noncritical opening HP loss");
                }
                else
                {
                    Check(game.projectiles.Count == (weapon == 1 && style == 2 ? 3 : 1), "The original bow/starfire emission count is unchanged");
                    foreach (Projectile shot in game.projectiles)
                    {
                        PlanStatsNear(shot.damage, displayed, "The card matches each actual released projectile payload");
                        Check(ReferenceEquals(shot.impact, action.impact) && shot.friendly && shot.explosive == (weapon == 2)
                            && shot.piercing == (weapon == 1 && style == 1) && shot.slowing == (weapon == 2 && style == 2),
                            "Damage presentation preserves captured ownership, original piercing, splash and slowing roles");
                    }
                }
                PlanStatsNear(game.Profile(weapon, style).damage, original.damage, "Reading contact damage never modifies the original profile model");
            }
        }

        private static void WeaponContactsRead(GameCore game)
        {
            for (int weapon = 0; weapon < 3; weapon++) for (int style = 0; style < 3; style++)
            {
                float actual = game.WeaponContactDamage(weapon, style), raw = game.Profile(weapon, style).damage;
                PlanStatsNear(actual, raw * (weapon == 1 && style == 2 ? .65f : 1), "Only Flow bow's displayed per-arrow contact uses its original release fraction");
            }
            foreach (int invalid in new[] { -1, 3, int.MinValue, int.MaxValue }) for (int valid = 0; valid < 3; valid++)
            {
                Check(game.WeaponContactDamage(invalid, valid) == 0 && game.WeaponContactDamage(valid, invalid) == 0
                    && game.WeaponContactDamage(invalid, invalid) == 0, "Illegal weapon/style combinations return zero without silently clamping to another weapon");
            }
        }

        public static void BattlePlanStatsWeaponContactQueriesPreserveLiveAndLegacyState(Campaign data)
        {
            for (int condition = 0; condition < 7; condition++)
            {
                var game = PlanCombatGame(data, condition % 3); game.mode = "plans"; PlanGear(game, 1, 2, 0, condition >= 2 ? 1 : -1, condition >= 4 ? 4 : -1);
                game.save.level = 5; game.save.upgrades = 2; game.save.talents[0] = 2; game.save.mastery = new[] { 20, 80, 100 };
                game.save.totalTime = 100; game.save.maxHealth = 200; game.save.health = condition == 3 ? 159.99f : 160;
                game.save.relicRank = condition >= 4 ? 3 : 0; game.save.foodUntil = condition == 1 || condition == 4 ? 101 : 100;
                if (condition >= 5) { game.save.room = 8; game.save.abyssFloor = 2; game.save.boons[0] = condition == 5 ? 1 : 3; }
                string before = PlanStatsState(game); SaveData owner = game.save; int[] relics = owner.relics, mastery = owner.mastery;
                for (int read = 0; read < 8; read++) WeaponContactsRead(game);
                Check(PlanStatsState(game) == before && ReferenceEquals(game.save, owner) && ReferenceEquals(game.save.relics, relics)
                    && ReferenceEquals(game.save.mastery, mastery), "Active/expired food, exact Amber boundary, relic forging and Abyss boons are observed without changing any live state");
            }
            var pending = PlanCombatGame(data, 1); pending.mode = "plans"; PlanGear(pending, 1, 2, 0, 1, 4); pending.Close();
            pending.Attack(new Point(1, 0)); ActorAction action = pending.playerAction; ImpactProfile impact = action.impact;
            string prepared = PlanStatsState(pending);
            for (int read = 0; read < 12; read++) WeaponContactsRead(pending);
            Check(PlanStatsState(pending) == prepared && ReferenceEquals(pending.playerAction, action) && ReferenceEquals(action.impact, impact),
                "Inspecting any alternative contact keeps the prepared bow's exact aim, damage, owner, timing, energy and RNG");
            for (int frame = 0; frame < 600 && !action.released; frame++) pending.Tick(.001f, new Point());
            Check(action.released && pending.projectiles.Count == 3, "The original prepared Flow volley still releases after inspection");
            Projectile first = pending.projectiles[0]; string flying = PlanStatsState(pending);
            for (int read = 0; read < 12; read++) WeaponContactsRead(pending);
            Check(PlanStatsState(pending) == flying && ReferenceEquals(pending.projectiles[0], first) && ReferenceEquals(first.impact, impact),
                "Inspection cannot advance, redirect, reprice or replace an already flying volley");
            var queued = PlanCombatGame(data, 1); queued.Close(); queued.attackCooldown = .08f; queued.PressAttack(new Point(0, 1));
            object intent = PlanStatsIntent(queued, "bufferedAttack"); Check(intent != null, "Queue a real unpaid attack before contact inspection");
            string unpaid = PlanStatsState(queued); WeaponContactsRead(queued);
            Check(PlanStatsState(queued) == unpaid && ReferenceEquals(intent, PlanStatsIntent(queued, "bufferedAttack")),
                "A material-free inspection cannot consume, replace or alter an existing input buffer");
            for (int version = 1; version <= 2; version++)
            {
                var old = new GameCore(data, new SaveData { version = version, coins = 91, health = 51, energy = 37,
                    flags = new System.Collections.Generic.List<string> { "intro", "legacy_weapon_card" } });
                string migrated = PlanStatsState(old); WeaponContactsRead(old);
                Check(PlanStatsState(old) == migrated && old.Has("legacy_weapon_card") && !old.Has("weapon_1") && !old.Has("weapon_2"),
                    "The new contact query leaves v" + version + " migration, paid balances and locked equipment ownership untouched");
                old.PrepareSave(); var resumed = new GameCore(data, CloneSave(old.save)); string roundtrip = PlanStatsState(resumed);
                WeaponContactsRead(resumed); Check(PlanStatsState(resumed) == roundtrip && resumed.save.coins == 91 && resumed.Has("legacy_weapon_card"),
                    "Existing save preparation and reconstruction require no new contact-damage fields");
            }
        }
    }
}
