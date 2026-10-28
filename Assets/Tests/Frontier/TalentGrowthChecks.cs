using System;
using System.Reflection;

namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        private static readonly BindingFlags talentGrowthFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        private static object TalentGrowthRead(GameCore game, int talent)
        {
            MethodInfo method = typeof(GameCore).GetMethod("GetTalentGrowthPreview", new[] { typeof(int) });
            Check(method != null, "The actual read-only talent growth preview API exists");
            return method.Invoke(game, new object[] { talent });
        }
        private static T TalentGrowthField<T>(object owner, string name)
        {
            Check(owner != null, "A valid live talent has a growth preview");
            FieldInfo field = owner.GetType().GetField(name, BindingFlags.Public | BindingFlags.Instance);
            Check(field != null && field.IsInitOnly, "Growth snapshot field is public and immutable: " + name);
            return (T)field.GetValue(owner);
        }
        private static object TalentGrowthValue(object preview, string name, string stat)
        {
            object value = TalentGrowthField<object>(preview, name);
            Check(value.GetType().IsSealed && TalentGrowthField<object>(value, "stat").ToString() == stat,
                "The immutable growth value identifies the actual statistic " + stat);
            return value;
        }
        private static float TalentGrowthNumber(object preview, string value, string stat, string phase)
        { return TalentGrowthField<float>(TalentGrowthValue(preview, value, stat), phase); }
        private static void TalentGrowthNear(float actual, float expected, string reason)
        { Check(!float.IsNaN(actual) && !float.IsInfinity(actual) && Math.Abs(actual - expected) < .001f,
            reason + ": expected " + expected + ", got " + actual); }
        private static void TalentGrowthIdentity(object preview, int talent, int rank, bool canLearn)
        {
            Check(preview != null && preview.GetType().IsSealed && TalentGrowthField<int>(preview, "talent") == talent &&
                TalentGrowthField<int>(preview, "rank") == rank && TalentGrowthField<int>(preview, "nextRank") == Math.Min(5, rank + 1) &&
                TalentGrowthField<bool>(preview, "canLearn") == canLearn,
                "An immutable snapshot describes the actual rank, earned next rank and original Learn availability");
            foreach (FieldInfo field in preview.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
                Check(field.IsInitOnly, "No public preview field can mutate a later Learn operation: " + field.Name);
        }
        private static void TalentGrowthCall(GameCore game, string method, params object[] args)
        { typeof(GameCore).GetMethod(method, talentGrowthFlags).Invoke(game, args); }
        private static GameCore TalentGrowthGame(Campaign data, int profession, int weapon, int style, int context, int talent, int rank)
        {
            var game = PlanCombatGame(data, profession);
            PlanGear(game, weapon, style, 0, context == 0 ? -1 : 1, context > 1 ? 5 : -1);
            game.save.skillPoints = 40;
            for (int paid = 0; paid < rank; paid++) game.Learn(talent);
            game.save.upgrades = context == 0 ? 0 : 2; game.save.level = context == 0 ? 1 : 7;
            game.save.mastery = new[] { 0, 42, 100 }; game.save.relicRank = 3;
            game.save.maxHealth = 240; game.save.health = context == 2 ? 191 : 192;
            game.save.totalTime = 100; game.save.foodUntil = context == 1 || context == 3 ? 1000 : 100;
            game.save.cold = context == 1 ? 71 : 70; game.save.seed = 1;
            if (context == 3)
            {
                game.save.room = 8; game.save.abyssFloor = 2; game.save.abyssSeed = 701;
                game.save.boons[0] = 2; game.save.boons[2] = 2;
                TalentGrowthCall(game, "GenerateAbyss"); game.save.x = 5; game.save.y = 14;
            }
            game.enemies.Clear(); game.projectiles.Clear(); game.effects.Clear(); game.Close();
            Check(game.CanStand(game.Position), "Talent contacts use legal authored or generated ground");
            return game;
        }
        private static ActorAction TalentGrowthAttack(GameCore game, object preview, string phase)
        {
            game.attackCooldown = 0; game.combo = 0; game.comboTimer = 0; game.save.energy = 100;
            game.Attack(new Point(1, 0)); ActorAction action = game.playerAction;
            Check(action != null && !action.released && action.kind == "attack" && action.combo == 0, "A real paid opening attack begins");
            float contact = action.damage * (action.weapon == 1 && action.style == 2 ? .65f : 1);
            TalentGrowthNear(TalentGrowthNumber(preview, "first", "ContactDamage", phase), contact,
                "Edge's " + phase + " value equals the actual captured opening contact or each Flow arrow");
            TalentGrowthNear(TalentGrowthNumber(preview, "second", "CriticalChance", phase), action.impact.criticalChance,
                "Edge's " + phase + " chance equals the actual immutable contact profile");
            int shots = game.projectiles.Count;
            for (int frame = 0; frame < 100 && !action.released; frame++) TalentGrowthCall(game, "TickAction", .01f);
            Check(action.released, "The real action's authored contact clock reaches release");
            if (action.weapon > 0)
            {
                int count = action.weapon == 1 && action.style == 2 ? 3 : 1;
                Check(game.projectiles.Count == shots + count, "The existing weapon releases its original projectile count");
                for (int shot = shots; shot < game.projectiles.Count; shot++)
                {
                    TalentGrowthNear(game.projectiles[shot].damage, contact, "Every actually released arrow/starfire carries displayed contact damage");
                    Check(ReferenceEquals(game.projectiles[shot].impact, action.impact), "Released projectiles retain the original captured critical profile");
                }
            }
            TalentGrowthCall(game, "TickAction", action.duration);
            return action;
        }
        private static void TalentGrowthReadAll(GameCore game)
        {
            for (int repeat = 0; repeat < 8; repeat++)
            {
                for (int talent = 0; talent < 3; talent++) Check(TalentGrowthRead(game, talent) != null, "Each original talent has a live preview");
                foreach (int invalid in new[] { -1, 3, int.MinValue, int.MaxValue })
                    Check(TalentGrowthRead(game, invalid) == null, "Invalid talent read is harmless");
            }
        }
        private static string TalentGrowthState(GameCore game)
        {
            var text = new System.Text.StringBuilder(PlanStatsState(game));
            PlanStatsAppend(text, typeof(GameCore).GetField("frost", talentGrowthFlags).GetValue(game));
            text.Append('/').Append(typeof(GameCore).GetField("frostGeneration", talentGrowthFlags).GetValue(game));
            return text.ToString();
        }

        public static void TalentGrowthEdgeMatchesRealLearnContactsAndCapturedCriticals(Campaign data)
        {
            foreach (int rank in new[] { 0, 2, 4 }) for (int profession = 0; profession < 3; profession++)
                for (int weapon = 0; weapon < 3; weapon++) for (int style = 0; style < 3; style++)
            {
                int context = (rank + profession + weapon + style) % 4;
                var game = TalentGrowthGame(data, profession, weapon, style, context, 0, rank);
                object preview = TalentGrowthRead(game, 0); TalentGrowthIdentity(preview, 0, rank, true);
                TalentGrowthAttack(game, preview, "before"); int points = game.save.skillPoints; float health = game.save.health;
                game.Learn(0);
                Check(game.save.skillPoints == points - 1 && game.save.talents[0] == rank + 1 && game.save.health == health,
                    "Original Edge Learn spends exactly one point without healing or replacing equipment");
                TalentGrowthAttack(game, preview, "after");
                object next = TalentGrowthRead(game, 0);
                TalentGrowthNear(TalentGrowthNumber(next, "first", "ContactDamage", "before"), TalentGrowthNumber(preview, "first", "ContactDamage", "after"),
                    "The next live preview begins at the actually learned contact value");
            }
            var combo = TalentGrowthGame(data, 0, 0, 1, 2, 0, 2); object opening = TalentGrowthRead(combo, 0);
            combo.combo = 1; combo.comboTimer = 1; combo.riposteTime = 1; combo.Attack(new Point(1, 0));
            Check(combo.playerAction != null && combo.playerAction.combo == 2 && combo.playerAction.impact.counter, "The original third-hit earned counter fixture is real");
            TalentGrowthNear(combo.playerAction.damage, TalentGrowthNumber(opening, "first", "ContactDamage", "before") * 1.25f * 1.5f,
                "The talent figure stays an opening noncritical contact and does not claim combo or counter damage");
        }

        public static void TalentGrowthResolveAndAgilityMatchActualHealthMovementAndDodge(Campaign data)
        {
            foreach (int rank in new[] { 0, 2, 4 }) for (int profession = 0; profession < 3; profession++) for (int context = 0; context < 4; context++)
            {
                var game = TalentGrowthGame(data, profession, 0, 0, context, 1, rank);
                game.save.difficulty = context % 3; game.guardTime = context % 2 == 1 ? 3 : 0;
                game.save.mealKind = 2; game.save.mealUntil = context == 2 ? 1000 : 100;
                object preview = TalentGrowthRead(game, 1); TalentGrowthIdentity(preview, 1, rank, true);
                TalentGrowthNear(TalentGrowthNumber(preview, "first", "MaxHealth", "before"), game.save.maxHealth, "Resolve uses actual earned maximum health");
                TalentGrowthNear(TalentGrowthNumber(preview, "first", "MaxHealth", "after"), game.save.maxHealth + 12, "Resolve retains its original twelve HP purchase");
                float health = game.save.health; game.invulnerable = 0;
                TalentGrowthCall(game, "Hurt", 20f, "shot", (Point?)new Point(1, 0)); float lossBefore = health - game.save.health;
                float other = (game.save.difficulty == 0 ? .45f : game.save.difficulty == 2 ? 1.4f : 1) *
                    (game.guardTime > 0 ? GameCore.GuardIncomingMultiplier : 1) * (game.InAbyss ? 1 - game.save.boons[2] * .12f : 1) *
                    (game.Relic(5) ? 1.15f : 1) * game.RoadMealDamageMultiplier("shot");
                TalentGrowthNear(lossBefore, 20 * other * TalentGrowthNumber(preview, "second", "IncomingTalentFactor", "before"),
                    "Resolve labels only its own mitigation while actual Hurt retains guard, difficulty, relic, abyss and meal rules");
                float wounded = game.save.health, maximum = game.save.maxHealth; int points = game.save.skillPoints;
                game.Learn(1);
                Check(game.save.skillPoints == points - 1 && game.save.talents[1] == rank + 1 && game.save.maxHealth == maximum + 12 &&
                    game.save.health == Math.Min(game.save.maxHealth, wounded + 12), "Real Resolve still spends one point and restores its original twelve HP");
                health = game.save.health; game.invulnerable = 0;
                TalentGrowthCall(game, "Hurt", 20f, "shot", (Point?)new Point(1, 0));
                TalentGrowthNear(health - game.save.health, 20 * other * TalentGrowthNumber(preview, "second", "IncomingTalentFactor", "after"),
                    "The displayed next Resolve factor matches the actual post-Learn damage transaction");

                var agile = TalentGrowthGame(data, profession, 0, 0, context, 2, rank);
                if (context == 2) { agile.save.relics[0] = 2; agile.save.mealKind = 3; agile.save.mealUntil = 1000; }
                object movement = TalentGrowthRead(agile, 2); TalentGrowthIdentity(movement, 2, rank, true);
                float speed = agile.Speed; TalentGrowthNear(TalentGrowthNumber(movement, "first", "WalkSpeed", "before"), speed, "Agility shows actual current profession, relic, cold and meal speed");
                Point start = agile.Position; agile.Tick(.02f, new Point(1, 0));
                TalentGrowthNear((agile.Position - start).Length / .02f, speed, "Actual walking translates at the displayed current speed");
                agile.save.energy = 100; float cost = agile.CurrentDodgeEnergyCost; agile.Dash(new Point(0, 1));
                TalentGrowthNear(agile.dashCooldown, TalentGrowthNumber(movement, "second", "DodgeCooldown", "before"), "Actual Dash starts at the displayed current cooldown");
                Check(agile.dashTime == .18f && agile.invulnerable == .32f && 100 - agile.save.energy == cost, "Agility preview does not alter original dodge duration, immunity or energy cost");
                agile.dashTime = agile.dashCooldown = agile.invulnerable = 0; agile.save.energy = 100; agile.Learn(2);
                TalentGrowthNear(agile.Speed, TalentGrowthNumber(movement, "first", "WalkSpeed", "after"), "Real Agility Learn reaches the displayed next live speed");
                agile.Dash(new Point(0, 1)); TalentGrowthNear(agile.dashCooldown, TalentGrowthNumber(movement, "second", "DodgeCooldown", "after"), "The next real Dash retains the displayed rank cooldown");
                Check(100 - agile.save.energy == cost && agile.dashTime == .18f && agile.invulnerable == .32f, "Learning Agility leaves original dodge payment and protection intact");
                agile.racing = true; object race = TalentGrowthRead(agile, 2);
                TalentGrowthNear(TalentGrowthNumber(race, "first", "WalkSpeed", "before"), agile.Speed, "Racing uses its actual existing speed and meal exclusion");
            }
        }

        public static void TalentGrowthQueriesPreservePaidActionsFieldsQueuesAndSimulation(Campaign data)
        {
            var game = TalentGrowthGame(data, 1, 1, 2, 1, 0, 2); game.Attack(new Point(1, 0)); ActorAction action = game.playerAction;
            Check(action != null && !action.released, "A genuine paid Flow preparation exists before reading growth");
            ImpactProfile impact = action.impact; string before = TalentGrowthState(game); TalentGrowthReadAll(game);
            Check(TalentGrowthState(game) == before && ReferenceEquals(action, game.playerAction) && ReferenceEquals(impact, action.impact),
                "Reading every talent preserves full save/RNG/clock/paid action profile and queued presentation");
            float capturedDamage = action.damage, capturedChance = impact.criticalChance, elapsed = action.elapsed, cooldown = game.attackCooldown, energy = game.save.energy;
            game.Learn(0);
            Check(ReferenceEquals(action, game.playerAction) && ReferenceEquals(action.impact, impact) && action.damage == capturedDamage && impact.criticalChance == capturedChance &&
                action.elapsed == elapsed && game.attackCooldown == cooldown && game.save.energy == energy && !action.released,
                "The original real Learn operation does not cancel or recapture an already paid attack");
            for (int frame = 0; frame < 100 && !action.released; frame++) TalentGrowthCall(game, "TickAction", .01f);
            Check(action.released && game.projectiles.Count == 3, "The original captured Flow action releases its unchanged three arrows after reads");
            Projectile shot = game.projectiles[0]; before = TalentGrowthState(game); TalentGrowthReadAll(game);
            Check(TalentGrowthState(game) == before && ReferenceEquals(shot, game.projectiles[0]) && ReferenceEquals(shot.impact, impact),
                "Airborne contacts retain their immutable profile, age, muzzle progress and ownership");
            foreach (Projectile originalShot in game.projectiles)
                TalentGrowthNear(originalShot.damage, capturedDamage * .65f, "Learning Edge during preparation cannot raise a paid Flow arrow's old damage");

            var mage = FrostOverlapGame(data); ActorAction frostAction; object field = FrostOverlapRelease(mage, true, out frostAction);
            before = TalentGrowthState(mage); TalentGrowthReadAll(mage);
            Check(TalentGrowthState(mage) == before && ReferenceEquals(field, FrostOverlapHead(mage)),
                "A genuinely released frost field retains its own center, pulse count, age and captured criticals");
            foreach (string kind in new[] { "attack", "skill", "dodge", "companion" })
            {
                var queued = Quiet(data); string name;
                if (kind == "attack") { queued.attackCooldown = .08f; queued.PressAttack(new Point(0, 1)); name = "bufferedAttack"; }
                else if (kind == "skill") { queued.skillCooldown = .08f; queued.Skill(new Point(0, 1)); name = "bufferedSkill"; }
                else if (kind == "dodge") { queued.dashCooldown = .08f; queued.Dash(new Point(0, 1)); name = "bufferedDodge"; }
                else { queued.save.flags.Add("companion_1"); queued.SelectCompanion(1); queued.companionCooldown = .08f; queued.CommandCompanion(); name = "bufferedCompanionCommand"; }
                object intent = PlanStatsIntent(queued, name); Check(intent != null, "An actual unpaid intent is queued: " + kind);
                before = TalentGrowthState(queued); TalentGrowthReadAll(queued);
                Check(TalentGrowthState(queued) == before && ReferenceEquals(intent, PlanStatsIntent(queued, name)), "Preview reads retain the actual aim, owner, remaining life and unpaid cost: " + kind);
                Advance(queued, .12f);
                Check(kind == "dodge" ? queued.dashTime > 0 : kind == "companion" ? queued.companionAction != null : queued.playerAction != null,
                    "The same original queued action still dispatches after all previews: " + kind);
            }
            foreach (string mode in new[] { "world", "talents", "inventory", "plans", "journal", "map", "dialog", "pause", "title" })
            {
                game.mode = mode; game.deathTime = .3f; before = TalentGrowthState(game); TalentGrowthReadAll(game);
                Check(TalentGrowthState(game) == before, "Growth previews are read-only even while defeated or in another mode: " + mode);
            }
        }

        public static void TalentGrowthRetainsLegacySavesCapsAndHarmlessRejections(Campaign data)
        {
            var original = ResetBuild(data, 1); original.save.flags.Add("talent_growth_mature_unknown"); original.save.flags.Add("weapon_2");
            original.save.mastery = new[] { 23, 64, 87 }; original.save.weaponStyles = new[] { 2, 1, 2 }; original.save.projects = new[] { 2, 1, 2 };
            original.save.trackedQuest = "riverlesson"; original.save.tea = 7; original.PrepareSave();
            foreach (int version in new[] { 1, 2 })
            {
                SaveData old = CloneSave(original.save); old.version = version; var loaded = new GameCore(data, old); loaded.Start();
                string before = TalentGrowthState(loaded); TalentGrowthReadAll(loaded);
                Check(TalentGrowthState(loaded) == before && loaded.Has("talent_growth_mature_unknown") && loaded.save.trackedQuest == "riverlesson" &&
                    loaded.save.coins == 73 && loaded.save.tea == 7 && loaded.save.mastery[2] == 87 && loaded.save.projects[2] == 2,
                    "Existing v" + version + " journey, equipment mastery, economy and unknown story flags survive previews");
                for (int talent = 0; talent < 3; talent++) TalentGrowthIdentity(TalentGrowthRead(loaded, talent), talent, original.save.talents[talent], true);
                loaded.PrepareSave(); var again = new GameCore(data, CloneSave(loaded.save)); again.Start();
                for (int talent = 0; talent < 3; talent++) TalentGrowthIdentity(TalentGrowthRead(again, talent), talent, original.save.talents[talent], true);
            }
            for (int talent = 0; talent < 3; talent++)
            {
                var game = Fresh(data); game.save.skillPoints = 0; string before = TalentGrowthState(game);
                object preview = TalentGrowthRead(game, talent); TalentGrowthIdentity(preview, talent, 0, false);
                Check(TalentGrowthNumber(preview, "first", talent == 0 ? "ContactDamage" : talent == 1 ? "MaxHealth" : "WalkSpeed", "after") >
                    TalentGrowthNumber(preview, "first", talent == 0 ? "ContactDamage" : talent == 1 ? "MaxHealth" : "WalkSpeed", "before"),
                    "An empty point balance still discloses the earned next-rank benefit without pretending it is buyable");
                game.Learn(talent); Check(TalentGrowthState(game) == before, "Original insufficient-point Learn remains atomic");
                game.save.skillPoints = 10; for (int rank = 0; rank < 5; rank++) game.Learn(talent);
                preview = TalentGrowthRead(game, talent); TalentGrowthIdentity(preview, talent, 5, false); before = TalentGrowthState(game);
                foreach (string field in new[] { "first", "second" })
                {
                    object value = TalentGrowthField<object>(preview, field);
                    TalentGrowthNear(TalentGrowthField<float>(value, "after"), TalentGrowthField<float>(value, "before"), "A capped talent cannot advertise a nonexistent sixth benefit");
                }
                game.Learn(talent); Check(TalentGrowthState(game) == before, "Original cap keeps points, vitals and rank unchanged");
            }
            foreach (int[] malformed in new int[][] { null, new int[0], new int[2], new int[4], new[] { -1, 0, 0 }, new[] { 0, 6, 0 }, new[] { 0, 0, int.MaxValue } })
            {
                var game = Fresh(data); game.save.talents = malformed; string before = TalentGrowthState(game);
                foreach (int talent in new[] { -1, 0, 1, 2, 3 }) Check(TalentGrowthRead(game, talent) == null, "Malformed live talent state has no misleading growth preview");
                Check(TalentGrowthState(game) == before && ReferenceEquals(malformed, game.save.talents), "A malformed-state query never repairs, replaces or normalizes the player's data");
            }
            foreach (FieldInfo field in typeof(SaveData).GetFields(BindingFlags.Public | BindingFlags.Instance))
                Check(field.Name.IndexOf("talentgrowth", StringComparison.OrdinalIgnoreCase) < 0 && field.Name.IndexOf("talentpreview", StringComparison.OrdinalIgnoreCase) < 0,
                    "Live comparison snapshots add no persisted save field");
        }
    }
}
