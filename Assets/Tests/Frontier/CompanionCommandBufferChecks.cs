using System;
using System.Reflection;

namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        // Use only the established public command entry point so the original
        // production baseline compiles and fails on the missing behavior.
        private static GameCore CompanionBufferGame(Campaign data, int companion = 1, int ending = 0)
        {
            GameCore game = Quiet(data, 1);
            for (int id = 0; id < 3; id++) game.save.flags.Add("companion_" + id);
            game.save.relationships[companion] = 3; game.save.frontierEnding = ending;
            game.SelectCompanion(companion); game.save.health = 20; game.save.energy = 30;
            typeof(GameCore).GetField("companionAttack", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(game, 1000f);
            game.SetAim(new Point(1, 0)); return game;
        }
        private static bool CompanionBufferQueued(GameCore game)
        {
            PropertyInfo property = typeof(GameCore).GetProperty("CompanionCommandQueued");
            return property != null && property.GetValue(game, null) is bool && (bool)property.GetValue(game, null);
        }
        private static ActorAction CompanionBufferAwait(GameCore game, float step = .02f)
        {
            for (int frame = 0; frame < 30; frame++)
            {
                game.Tick(step, new Point());
                if (game.companionAction != null && game.companionAction.kind == "command") return game.companionAction;
            }
            return null;
        }
        private static void CompanionBufferNoLaterCommand(GameCore game, string boundary)
        {
            typeof(GameCore).GetField("companionAttack", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(game, 1000f);
            for (int frame = 0; frame < 30; frame++)
            {
                game.Tick(.02f, new Point());
                Check(game.companionAction == null || game.companionAction.kind != "command",
                    "No abandoned companion request crosses " + boundary);
            }
            Check(!CompanionBufferQueued(game) && game.LastCompanionReceipt == null,
                "A canceled request cannot fabricate a pending state or contact receipt after " + boundary);
        }

        public static void CompanionCommandBufferThresholdCadencesAndSingleDispatch(Campaign data)
        {
            foreach (float step in new[] { 1f / 60, 1f / 30, .05f })
            for (int id = 0; id < 3; id++) foreach (int ending in new[] { 0, 3 })
            foreach (float delay in new[] { .12f, .121f })
            {
                GameCore game = CompanionBufferGame(data, id, ending); game.companionCooldown = delay;
                float health = game.save.health, energy = game.save.energy; game.CommandCompanion();
                Check(game.companionAction == null && game.companionCooldown == delay && game.save.health == health
                    && game.save.energy == energy && game.guardTime == 0 && game.effects.Count == 0
                    && game.projectiles.Count == 0 && game.LastCompanionReceipt == null,
                    "An early companion press grants no action, cooldown, recovery, guard, projectile, effect or receipt");
                ActorAction action = CompanionBufferAwait(game, step);
                string context = " companion=" + id + " ending=" + ending + " step=" + step + " delay=" + delay;
                Check((action != null) == (delay == .12f), "Accept exactly .120 and reject .121 at actual readiness:" + context);
                if (action == null) continue;
                Check(action.elapsed == 0 && !action.released && action.contact == .2f && action.duration == .52f
                    && game.companionCooldown == (ending == 3 ? 12 : 18) && game.LastCompanionReceipt == null,
                    "The final valid tick starts one original full preparation without backdating elapsed time:" + context);
                Advance(game, .2f); CompanionCommandReceipt receipt = game.LastCompanionReceipt;
                Check(action.released && receipt != null && receipt.id == id, "A queued command reaches its genuine contact:" + context);
                Advance(game, .5f);
                Check(game.companionAction == null && ReferenceEquals(game.LastCompanionReceipt, receipt)
                    && !CompanionBufferQueued(game) && game.companionCooldown < (ending == 3 ? 12 : 18),
                    "One released request cannot repeat or renew its cooldown:" + context);
            }
            FieldInfo window = typeof(GameCore).GetField("CompanionCommandBufferWindow");
            PropertyInfo queued = typeof(GameCore).GetProperty("CompanionCommandQueued");
            Check(window != null && window.IsLiteral && (float)window.GetRawConstantValue() == .12f
                && queued != null && queued.PropertyType == typeof(bool) && queued.GetSetMethod(true) == null,
                "The authored .12s window and truthful queued observation are public read-only API");
            GameCore immediate = CompanionBufferGame(data); immediate.CommandCompanion();
            Check(immediate.companionAction != null && immediate.companionAction.elapsed == 0
                && immediate.companionCooldown == 18 && !CompanionBufferQueued(immediate),
                "A ready public command keeps its immediate mature behavior");
            GameCore extended = CompanionBufferGame(data); extended.companionCooldown = .12f; extended.CommandCompanion();
            extended.companionCooldown = .15f; Advance(extended, .4f);
            Check(extended.companionAction == null && extended.LastCompanionReceipt == null && !CompanionBufferQueued(extended),
                "A later readiness boundary cannot extend a one-shot request beyond its original lifetime");
        }

        public static void CompanionCommandBufferCapturesAimAndOriginalContactValues(Campaign data)
        {
            for (int id = 0; id < 3; id++)
            {
                GameCore game = CompanionBufferGame(data, id); Point target = new Point(21, 12);
                Point direction = (target - game.companionPosition).Normalized; game.SetAimTarget(target);
                game.companionCooldown = .08f; float queuedDamage = game.Damage; game.CommandCompanion();
                game.SetAimTarget(new Point(5, 18)); game.SetAim(new Point(-1, 0));
                game.save.talents[0] = 2; game.save.relationships[id] = 5;
                ActorAction action = CompanionBufferAwait(game);
                Check(action != null, "The real cooldown press survives until companion preparation");
                Check((action.direction - direction).Length < .0001f && action.target.HasValue
                    && (action.target.Value - target).Length < .0001f && action.combo == 5 && action.damage == game.Damage
                    && action.damage > queuedDamage && Math.Abs(action.impact.criticalChance - .11f) < .0001f,
                    "Intent retains pressed aim while bond, damage and immutable impact capture at actual start for companion " + id);
                float damage = action.damage; ImpactProfile impact = action.impact;
                game.save.talents[0] = 3; game.save.relationships[id] = 1;
                Advance(game, .19f);
                Check(!action.released && game.save.health == 20 && game.guardTime <= 0 && game.projectiles.Count == 0
                    && game.effects.Count == 0 && game.LastCompanionReceipt == null,
                    "The entire original preparation remains free of early support and invented feedback");
                float energy = game.save.energy; game.Tick(.01f, new Point()); CompanionCommandReceipt receipt = game.LastCompanionReceipt;
                Check(action.released && receipt != null && receipt.id == id && receipt.bond == 5
                    && action.damage == damage && ReferenceEquals(action.impact, impact) && TacticsNear(game.companionCooldown, 17.8f),
                    "Only the .2s contact releases the originally prepared command snapshot");
                if (id == 0) Check(TacticsNear(game.save.health, 53) && TacticsNear(receipt.actualHeal, 33)
                    && TacticsNear(receipt.actualEnergy, 15) && TacticsNear(game.save.energy, energy + .14f + 15)
                    && TacticsNear(game.guardTime, 3) && game.effects.Exists(effect => effect.kind == "heal"),
                    "Mira keeps 18 + bond*3 healing, +15 energy and three-second original guard");
                if (id == 1)
                {
                    Check(receipt.arrowCount == 3 && game.projectiles.Count == 3 && game.projectiles.TrueForAll(shot =>
                        shot.friendly && shot.piercing && TacticsNear(shot.damage, damage * 1.05f) && ReferenceEquals(shot.impact, impact)),
                        "Ivo retains exactly three real piercing arrows and original .8 + bond*.05 damage");
                    Point origin = GameCore.WeaponOrigin(game.companionPosition, 1, direction);
                    Check((game.projectiles[1].velocity.Normalized - (target - origin).Normalized).Length < .001f,
                        "The actual center arrow flies from its original muzzle toward the captured target");
                }
                if (id == 2) Check(receipt.controlCount == 0 && TacticsNear(receipt.frostPulseDamage, damage * .4f)
                    && game.projectiles.Count == 0 && game.effects.Exists(effect => effect.kind == "frost")
                    && (receipt.center - game.Position).Length < .001f,
                    "Sen retains the original player-centered field and .4 pulse damage without inventing a controlled enemy");
            }
        }

        public static void CompanionCommandBufferKeepsHeroQueuesLatestPressAndHitStop(Campaign data)
        {
            for (int hero = 0; hero < 3; hero++) foreach (bool companionFirst in new[] { false, true })
            {
                GameCore game = CompanionBufferGame(data); game.save.energy = 100;
                game.companionCooldown = .08f;
                if (companionFirst) game.CommandCompanion();
                if (hero == 0) { game.skillCooldown = .08f; game.Skill(new Point(0, -1)); }
                if (hero == 1) { game.dashCooldown = .08f; game.Dash(new Point(0, -1)); }
                if (hero == 2) { game.attackCooldown = .08f; game.PressAttack(new Point(0, -1)); }
                if (!companionFirst) game.CommandCompanion();
                ActorAction command = CompanionBufferAwait(game);
                Check(command != null && command.elapsed == 0 && !command.released,
                    "Hero input cannot swallow an independent queued companion command: hero=" + hero + " companionFirst=" + companionFirst);
                Check(hero == 1 ? game.dashTime > 0 : game.playerAction != null && game.playerAction.kind == (hero == 0 ? "skill" : "attack"),
                    "Companion input cannot clear an already accepted hero skill, dodge or basic press");
            }
            GameCore latest = CompanionBufferGame(data); latest.companionCooldown = .12f; latest.CommandCompanion();
            latest.Tick(.02f, new Point()); Point target = new Point(12, 20); Point direction = (target - latest.companionPosition).Normalized;
            latest.SetAimTarget(target); latest.CommandCompanion(); latest.SetAimTarget(null); latest.SetAim(new Point(-1, 0));
            ActorAction replacement = CompanionBufferAwait(latest);
            Check(replacement != null && (replacement.direction - direction).Length < .0001f && replacement.target.HasValue
                && (replacement.target.Value - target).Length < .0001f,
                "A fresh accepted press replaces the single intent with its own direction and target");
            GameCore frozen = CompanionBufferGame(data); frozen.companionCooldown = .08f; frozen.CommandCompanion(); frozen.hitStop = .1f;
            float clock = frozen.save.totalTime; frozen.Tick(.05f, new Point()); frozen.Tick(.05f, new Point());
            Check(frozen.companionCooldown == .08f && frozen.save.totalTime == clock && frozen.companionAction == null
                && CompanionBufferQueued(frozen) && frozen.save.energy == 30,
                "Hit stop freezes companion readiness and request lifetime on the same simulation clock");
            Check(CompanionBufferAwait(frozen) != null, "The frozen request starts once after hit stop resumes");
            GameCore automatic = CompanionBufferGame(data);
            FocusEnemy(automatic, 98297, automatic.companionPosition.x + 3, automatic.companionPosition.y);
            typeof(GameCore).GetField("companionAttack", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(automatic, .01f);
            automatic.Tick(.01f, new Point()); ActorAction support = automatic.companionAction;
            Check(support != null && support.kind == "attack" && !support.released,
                "The actual companion AI prepares automatic support before the deliberate request");
            Advance(automatic, .10f); automatic.companionCooldown = .12f; automatic.CommandCompanion();
            Check(ReferenceEquals(automatic.companionAction, support) && !support.released && CompanionBufferQueued(automatic),
                "Queuing a deliberate command preserves automatic preparation and pending contact");
            automatic.Tick(.05f, new Point()); automatic.Tick(.02f, new Point());
            Check(ReferenceEquals(automatic.companionAction, support) && support.released && automatic.projectiles.Count == 1
                && automatic.companionCooldown > 0 && CompanionBufferQueued(automatic) && automatic.LastCompanionReceipt == null,
                "Existing automatic support releases its real arrow during the waiting window without a command receipt");
            ActorAction deliberate = CompanionBufferAwait(automatic, .01f);
            Check(deliberate != null && deliberate != support && deliberate.elapsed == 0 && !deliberate.released,
                "Only true cooldown readiness replaces the automatic support with fresh command preparation");
        }

        public static void CompanionCommandBufferClearsBoundariesAndStaysOutOfSaves(Campaign data)
        {
            GameCore saved = CompanionBufferGame(data); saved.companionCooldown = .08f; saved.CommandCompanion(); saved.PrepareSave();
            SaveData checkpoint = CloneSave(saved.save);
            Check(saved.companionAction == null && CompanionBufferAwait(saved) != null,
                "Preparing a real save neither executes nor discards the still-local cooldown request");
            foreach (int version in new[] { 1, 2 })
            {
                SaveData old = CloneSave(checkpoint); old.version = version;
                GameCore restored = new GameCore(data, old); restored.Start(); restored.Close(); restored.enemies.Clear();
                CompanionBufferNoLaterCommand(restored, "legacy v" + version + " save reconstruction");
                Check(restored.save.companion == (version == 1 ? -1 : 1) && restored.Has("companion_1")
                    && restored.save.relationships[1] == 3,
                    "Reconstruction preserves earned progress and the original v1 unselected/v2 selected companion rule");
            }
            foreach (FieldInfo field in typeof(SaveData).GetFields())
                Check(field.Name.IndexOf("companionCooldown", StringComparison.OrdinalIgnoreCase) < 0
                    && field.Name.IndexOf("commandQueued", StringComparison.OrdinalIgnoreCase) < 0
                    && field.FieldType.Name.IndexOf("CompanionCommandIntent", StringComparison.OrdinalIgnoreCase) < 0,
                    "The command cooldown and input intent never become persisted progress");
            GameCore reprise = RepriseGame(data, 0); reprise.save.flags.Add("companion_1"); reprise.SelectCompanion(1);
            Check(reprise.save.companion == 1, "The earned companion is deliberately selected before the reprise");
            typeof(GameCore).GetField("companionAttack", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(reprise, 1000f);
            reprise.invulnerable = 1000; Enemy warden = RepriseStart(reprise); warden.hp = 1; warden.timer = 1000;
            reprise.save.x = warden.x - 1.5f; reprise.save.y = warden.y;
            Check(reprise.CanStand(reprise.Position), "The final real sword contact begins on authored walkable ground");
            reprise.SetAimTarget(null); reprise.Attack(new Point(1, 0));
            Check(reprise.playerAction != null && reprise.playerAction.kind == "attack", "A public Attack starts the actual sword preparation");
            for (int frame = 0; frame < 7; frame++) reprise.Tick(.02f, new Point());
            Check(reprise.playerAction != null && !reprise.playerAction.released && reprise.SignalRepriseActive,
                "Seven genuine frames preserve the pending final sword contact");
            reprise.companionCooldown = .02f; reprise.CommandCompanion();
            Check(CompanionBufferQueued(reprise) && reprise.companionAction == null,
                "A deliberate cooldown request is pending immediately before the lethal reprise contact");
            reprise.Tick(.02f, new Point());
            Check(!reprise.SignalRepriseActive && reprise.LastSignalReprise != null && reprise.LastSignalReprise.won
                && !reprise.enemies.Contains(warden), "The eighth real sword contact wins and retires the actual reprise");
            Check(reprise.playerAction == null && reprise.companionAction == null && !CompanionBufferQueued(reprise)
                && reprise.companionCooldown <= .000001f && reprise.LastCompanionReceipt == null,
                "Reprise victory retires pending companion intent before the same tick can restart a command or renew cooldown");
            CompanionBufferNoLaterCommand(reprise, "the completed reprise's same-frame retirement");
            Action<GameCore>[] boundaries = {
                game => { game.mode = "pause"; game.Close(); }, game => { game.mode = "inventory"; game.Close(); },
                game => { game.mode = "dialog"; game.Close(); }, game => { game.SelectCompanion(2); game.SelectCompanion(1); },
                game => { game.SelectCompanion(-1); game.SelectCompanion(1); }, game => game.Enter(1), game => game.Enter(0),
                game => { game.PrepareSave(); game.Load(CloneSave(game.save)); game.Close(); },
                game => game.save = CloneSave(game.save), game => game.save.room = 0,
                game => game.save.weapon = 0, game => game.save.profession = 2,
                game => game.save.weaponStyles[1] = 1, game => game.save.technique = 1,
                game => { game.StartPractice(); Check(game.practicing, "The real town practice yard starts before its exit boundary"); game.EndPractice(); },
                game => { game.NewGame(1); game.Close(); },
                game => { game.invulnerable = 0; game.save.health = 1; game.projectiles.Add(new Projectile { position = game.Position, damage = 999, source = "shot" }); game.Tick(.01f, new Point()); Check(game.Dying, "Actual lethal projectile starts death before queued support"); }
            };
            for (int boundary = 0; boundary < boundaries.Length; boundary++)
            {
                GameCore game = CompanionBufferGame(data);
                if (boundary == 14) { game.Enter(0); game.enemies.Clear(); }
                game.companionCooldown = .08f; game.CommandCompanion(); boundaries[boundary](game);
                game.enemies.Clear(); CompanionBufferNoLaterCommand(game, "boundary " + boundary);
            }
        }
    }
}
