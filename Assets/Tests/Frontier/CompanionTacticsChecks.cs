using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        private static GameCore TacticsGame(Campaign data, int id, int bond = 0, int ending = 0)
        {
            GameCore game = Quiet(data); game.save.flags.Add("companion_" + id);
            game.save.relationships[id] = bond; game.save.frontierEnding = ending; game.SelectCompanion(id);
            game.save.health = 20; game.save.energy = 20; game.aim = new Point(0, 1);
            return game;
        }
        private static bool TacticsNear(float a, float b) { return Math.Abs(a - b) < .0002f; }
        private static object TacticsField(object value, string name)
        { return value.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(value); }
        private static void TacticsAppend(StringBuilder text, object value, HashSet<object> seen)
        {
            if (value == null) { text.Append("null;"); return; }
            Type type = value.GetType();
            if (type.IsPrimitive || type.IsEnum || value is string)
            { text.Append(value).Append(';'); return; }
            if (!type.IsValueType && !seen.Add(value)) { text.Append("ref;"); return; }
            IEnumerable values = value as IEnumerable;
            if (values != null) { foreach (object item in values) TacticsAppend(text, item, seen); return; }
            FieldInfo[] fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Array.Sort(fields, (a, b) => string.CompareOrdinal(a.Name, b.Name));
            foreach (FieldInfo field in fields)
            { if (field.Name == "data") continue; text.Append(field.Name).Append('='); TacticsAppend(text, field.GetValue(value), seen); }
        }
        private static string TacticsState(object value)
        { var text = new StringBuilder(); TacticsAppend(text, value, new HashSet<object>()); return text.ToString(); }
        private static CompanionCommandReceipt TacticsRelease(GameCore game)
        {
            game.CommandCompanion(); Check(game.companionAction != null && !game.companionAction.released && game.LastCompanionReceipt == null,
                "A real command prepares without an early receipt");
            Advance(game, .2f); Check(game.LastCompanionReceipt != null && game.companionAction.released,
                "The mature .2-second contact creates the real command receipt");
            return game.LastCompanionReceipt;
        }
        public static void CompanionTacticNotesAreReadOnlyAndShareActualFormulas(Campaign data)
        {
            foreach (int bond in new[] { 0, 5 }) foreach (int ending in new[] { 0, 3 })
            {
                GameCore game = TacticsGame(data, 0, bond, ending); game.save.upgrades = 2;
                for (int id = 0; id < 3; id++) game.save.relationships[id] = bond;
                string before = TacticsState(game);
                for (int read = 0; read < 5; read++) for (int id = 0; id < 3; id++)
                {
                    CompanionTacticInfo info = game.CompanionTactic(id);
                    Check(info.id == id && info.bond == bond && info.cooldown == (ending == 3 ? 12 : 18)
                        && info.contact == .2f && info.duration == .52f, "Every roster entry describes its own bond and the actual command timing");
                    if (id == 0) Check(info.heal == 18 + bond * 3 && info.energy == 15 && info.guardSeconds == 3 && info.incomingMultiplier == .45f
                        && info.arrows == 0 && info.frostPulseCount == 0, "Mira notes describe clamped recovery and 55 percent mitigation, not an absorption shield");
                    if (id == 1) Check(info.arrows == 3 && TacticsNear(info.arrowBaseDamage, game.Damage * (.8f + bond * .05f))
                        && info.heal == 0 && info.frostPulseCount == 0, "Ivo notes describe three launched piercing arrows and per-arrow base damage");
                    if (id == 2) Check(info.frostRadius == 4 && info.frostDuration == 4 && info.frostPulseCount == 5
                        && TacticsNear(info.frostPulseDamage, game.Damage * .4f) && info.controlRadius == 5 && info.controlSeconds == 1.4f,
                        "Sen notes distinguish the five-pulse field from the larger initial control radius");
                }
                Check(game.CompanionTactic(-1) == null && game.CompanionTactic(3) == null && TacticsState(game) == before,
                    "Reading descriptions and invalid entries never mutates simulation, save data, RNG, effects or narrative notifications");
            }
            foreach (Type type in new[] { typeof(CompanionTacticInfo), typeof(CompanionCommandReceipt) })
                foreach (FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
                    Check(field.IsInitOnly, "Tactics descriptions and receipts expose immutable fields");
            foreach (FieldInfo field in typeof(SaveData).GetFields())
                Check(field.FieldType != typeof(CompanionTacticInfo) && field.FieldType != typeof(CompanionCommandReceipt), "No tactics session object enters SaveData");
        }

        public static void CompanionReceiptsRequireActualContactAndKeepMatureCommands(Campaign data)
        {
            for (int id = 0; id < 3; id++) foreach (int bond in new[] { 0, 5 }) foreach (int ending in new[] { 0, 3 })
            {
                GameCore game = TacticsGame(data, id, bond, ending); game.Notify("原有剧情", "Original story");
                string toast = game.toast; game.CommandCompanion(); ActorAction action = game.companionAction;
                Check(action.kind == "command" && action.contact == .2f && action.duration == .52f && action.combo == bond
                    && action.damage == game.Damage && game.companionCooldown == (ending == 3 ? 12 : 18), "Accepting a command retains original action values and cooldown");
                Advance(game, .19f);
                Check(!action.released && game.LastCompanionReceipt == null && game.CompanionFeedbackTime == 0
                    && game.projectiles.Count == 0 && game.effects.Count == 0 && game.save.health == 20, "Preparation cannot fabricate release feedback or early effects");
                game.Tick(.01f, new Point()); CompanionCommandReceipt receipt = game.LastCompanionReceipt;
                Check(action.released && receipt != null && receipt.id == id && receipt.bond == bond && game.CompanionFeedbackTime == 2.2f
                    && TacticsNear(game.companionCooldown, (ending == 3 ? 12 : 18) - .2f), "Only the actual contact starts the full 2.2-second feedback witness");
                Check(game.toast == toast && game.toastTime > 0, "Command feedback does not overwrite the existing story notification");
                if (id == 0) Check(receipt.actualHeal == 18 + bond * 3 && receipt.actualEnergy == 15 && game.effects.Count == 2
                    && game.effects[0].kind == "heal" && game.effects[1].kind == "healthGain" && game.effects[1].text == "+" + receipt.actualHeal.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)
                    && game.projectiles.Count == 0 && game.guardTime == 3, "Mira keeps its original healing ring first and guard, followed by the exact actual recovery label");
                if (id == 1) Check(receipt.arrowCount == 3 && game.projectiles.Count == 3 && game.projectiles.TrueForAll(p => p.piercing && p.velocity.y > 0
                    && TacticsNear(p.damage, receipt.arrowBaseDamage)) && game.effects.Count == 1 && game.effects[0].kind == "release",
                    "Ivo records real launched arrows without claiming a hit or adding invented effects");
                if (id == 2) Check(receipt.controlCount == 0 && receipt.frostPulseDamage == action.damage * .4f
                    && game.projectiles.Count == 0 && game.effects.Count == 1 && game.effects[0].kind == "frost", "Sen records a real field even when no foe was controlled");
                game.CommandCompanion(); Check(game.LastCompanionReceipt == receipt && game.companionAction == action,
                    "A rejected command during cooldown keeps the actual receipt and active action");
                Advance(game, .4f); Check(game.LastCompanionReceipt == receipt && game.companionAction == null,
                    "The remaining action frames cannot issue a second receipt");
            }
        }

        public static void CompanionMiraReceiptReportsClampedRecoveryAndActualGuard(Campaign data)
        {
            foreach (int bond in new[] { 0, 5 }) foreach (bool full in new[] { false, true })
            {
                GameCore game = TacticsGame(data, 0, bond); game.save.health = full ? game.save.maxHealth : game.save.maxHealth - 2.25f;
                game.CommandCompanion(); Advance(game, .19f); game.save.energy = 99.25f;
                float health = game.save.health, energy = game.save.energy;
                game.Tick(.01f, new Point()); CompanionCommandReceipt receipt = game.LastCompanionReceipt;
                Check(receipt != null && TacticsNear(receipt.actualHeal, full ? 0 : 2.25f) && TacticsNear(receipt.actualEnergy, 100 - (energy + .14f))
                    && game.save.health == game.save.maxHealth && game.save.energy == 100,
                    "The release receipt reports actual capped health and energy gain, excluding ordinary pre-contact regeneration");
                Check(TacticsNear(game.save.health - health, receipt.actualHeal) && game.guardTime == 3, "A full-health release still grants the mature guard without inventing healing");
                game.invulnerable = 0; health = game.save.health;
                typeof(GameCore).GetMethod("Hurt", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(game, new object[] { 20f, "shot", (Point?)new Point(1, 0) });
                Check(TacticsNear(health - game.save.health, 20 * .45f), "The documented 55 percent mitigation equals real Hurt behavior");
                Check(game.LastCompanionReceipt == receipt && TacticsNear(receipt.actualHeal, full ? 0 : 2.25f), "Later damage cannot rewrite the immutable release receipt");
            }
            GameCore longer = TacticsGame(data, 0); longer.guardTime = 5; TacticsRelease(longer);
            Check(longer.guardTime > 4.79f, "Mira never shortens an already longer guard window");
        }

        public static void CompanionIvoAndSenReceiptsKeepCapturedDamageAndRealGeometry(Campaign data)
        {
            foreach (int bond in new[] { 0, 5 })
            {
                GameCore ivo = TacticsGame(data, 1, bond); ivo.CommandCompanion(); ActorAction arrowAction = ivo.companionAction;
                float damage = arrowAction.damage; ivo.save.upgrades = 5; ivo.save.relationships[1] = 5 - bond; ivo.SetAim(new Point(1, 0));
                Advance(ivo, .2f); CompanionCommandReceipt volley = ivo.LastCompanionReceipt;
                Check(volley.bond == bond && TacticsNear(volley.arrowBaseDamage, damage * (.8f + bond * .05f))
                    && ivo.projectiles.TrueForAll(p => p.velocity.y > 0 && TacticsNear(p.velocity.Length, 13) && p.piercing)
                    && (volley.center - ivo.companionPosition).Length < .0001f,
                    "Ivo retains starting bond, damage, direction, speed and real companion launch origin across equipment/aim changes");
            }
            GameCore sen = TacticsGame(data, 2); sen.save.x = 12; sen.save.y = 14;
            Enemy inner = new Enemy(981, 0, 13, 14, 9999) { stun = 999, timer = 999 };
            Enemy ring = new Enemy(982, 0, 16.5f, 14, 9999) { stun = 999, timer = 999 };
            Enemy outside = new Enemy(983, 0, 17.01f, 14, 9999) { stun = 999, timer = 999 };
            sen.enemies.Add(inner); sen.enemies.Add(ring); sen.enemies.Add(outside);
            Check(sen.CanStand(inner.Position) && sen.CanStand(ring.Position), "Actual production ground supplies the two separate Sen radii");
            sen.CommandCompanion(); ActorAction frostAction = sen.companionAction; Advance(sen, .2f);
            CompanionCommandReceipt fieldReceipt = sen.LastCompanionReceipt; object field = TacticsField(sen, "frost");
            Check(fieldReceipt.controlCount == 2 && (fieldReceipt.center - sen.Position).Length < .0001f
                && TacticsNear(fieldReceipt.frostPulseDamage, frostAction.damage * .4f) && TacticsNear((float)TacticsField(field, "damage"), fieldReceipt.frostPulseDamage),
                "Sen counts the actual initial two foes while retaining the immutable field center and pulse base damage");
            Check(inner.hp < inner.maxHp && inner.stun == .45f && ring.hp == ring.maxHp && ring.stun == 1.4f && outside.stun > 900,
                "The first real inner pulse retains its .45 stun overwrite; the outer control ring has no invented pulse damage");
            sen.save.x += 1; inner.stun = ring.stun = outside.stun = 999;
            Advance(sen, 3.3f); Check((int)TacticsField(field, "pulses") == 5,
                "The existing four-second field still schedules exactly five pulses");
            Check(((Point)TacticsField(field, "center") - fieldReceipt.center).Length < .0001f, "Walking after release does not move Sen's existing field or its receipt");
            Advance(sen, .8f); Check(TacticsField(sen, "frost") == null && sen.LastCompanionReceipt == fieldReceipt,
                "Field expiration does not erase the retained contact receipt");
            GameCore wall = TacticsGame(data, 2); Point stand = WallStand(wall); wall.save.x = stand.x; wall.save.y = stand.y;
            Enemy occluded = new Enemy(984, 0, (float)Math.Floor(stand.x) - .1f, stand.y, 9999) { stun = 999, timer = 999 };
            wall.enemies.Add(occluded); CompanionCommandReceipt blocked = TacticsRelease(wall);
            Check(blocked.controlCount == 0 && occluded.hp == occluded.maxHp && occluded.stun > 900,
                "Actual wall collision blocks both initial control and field contact instead of promising every nearby foe is frozen");
        }

        public static void CompanionReceiptClockPauseRetentionAndNewCommandsAreHonest(Campaign data)
        {
            GameCore game = TacticsGame(data, 1); CompanionCommandReceipt receipt = TacticsRelease(game);
            float clock = game.CompanionFeedbackTime, cooldown = game.companionCooldown;
            game.mode = "companions"; Advance(game, 1); Check(game.CompanionFeedbackTime == clock && game.companionCooldown == cooldown
                && game.LastCompanionReceipt == receipt, "Menus freeze the release witness on the same command simulation clock");
            game.Close(); game.hitStop = .1f; game.Tick(.05f, new Point()); game.Tick(.05f, new Point());
            Check(game.CompanionFeedbackTime == clock && game.companionCooldown == cooldown, "Mature hit stop freezes feedback and command cooldown together");
            game.hitStop = 0; Advance(game, 2.3f);
            Check(game.CompanionFeedbackTime == 0 && game.LastCompanionReceipt == receipt, "The HUD witness expires while notes retain the immutable last actual receipt");
            game.companionCooldown = 0; game.CommandCompanion(); Check(game.LastCompanionReceipt == null && game.CompanionFeedbackTime == 0,
                "Accepting the next real command removes the old receipt before its new preparation");
            game.mode = "pause"; Advance(game, 1); Check(game.LastCompanionReceipt == null && !game.companionAction.released,
                "Paused preparation cannot create a receipt based on wall-clock time");
            game.Close(); Advance(game, .2f); Check(game.LastCompanionReceipt != null && game.LastCompanionReceipt != receipt,
                "The resumed next contact creates exactly its own new receipt");
        }

        public static void CompanionReceiptsCancelAtLifecycleBoundariesWithoutSaveFields(Campaign data)
        {
            for (int boundary = 0; boundary < 9; boundary++) foreach (bool released in new[] { false, true })
            {
                GameCore game = TacticsGame(data, 0);
                if (boundary >= 6)
                {
                    game.Enter(0); game.enemies.Clear(); game.save.practiceBits = 7;
                    foreach (WorldObject mentor in game.Map.objects) if (mentor.kind == "mentor") { game.save.x = mentor.x; game.save.y = mentor.y; break; }
                }
                if (released) TacticsRelease(game); else game.CommandCompanion();
                if (boundary == 0) game.Enter(0);
                if (boundary == 1) game.Load(game.save);
                if (boundary == 2) { game.NewGame(0); game.Close(); }
                if (boundary == 3) game.SelectCompanion(-1);
                if (boundary == 4) { game.save.flags.Add("companion_1"); game.SelectCompanion(1); }
                if (boundary == 5) { game.invulnerable = 0; game.save.health = 1; typeof(GameCore).GetMethod("Hurt", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(game, new object[] { 999f, "shot", (Point?)new Point(1, 0) }); }
                if (boundary >= 6)
                {
                    if (boundary == 6) game.StartPractice();
                    if (boundary == 7) Check(game.StartSparring(), "Actual mature sparring entry");
                    if (boundary == 8) Check(game.StartRange(0), "Actual mature freeform range entry");
                    game.CommandCompanion(); game.Tick(.02f, new Point());
                    Check(game.companionAction == null && game.LastCompanionReceipt == null,
                        "Training still disables companions on its simulation tick, without a fabricated receipt"); game.EndPractice();
                }
                Check(game.LastCompanionReceipt == null && game.CompanionFeedbackTime == 0,
                    "No command receipt crosses lifecycle boundary " + boundary + ", released=" + released);
            }
            GameCore saveGame = TacticsGame(data, 1); CompanionCommandReceipt old = TacticsRelease(saveGame); saveGame.PrepareSave();
            string before = TacticsState(saveGame.save); saveGame.CompanionTactic(0); saveGame.CompanionTactic(1); saveGame.CompanionTactic(2);
            Check(TacticsState(saveGame.save) == before && saveGame.LastCompanionReceipt == old,
                "Reading a prepared save leaves its full persisted state and actual receipt unchanged");
            foreach (int version in new[] { 1, 2 })
            {
                var legacy = new SaveData { version = version }; var loaded = new GameCore(data, legacy);
                Check(loaded.LastCompanionReceipt == null && loaded.CompanionFeedbackTime == 0 && loaded.save.version == 2,
                    "Version one and two saves need no receipt fields or migration additions");
            }
        }
    }
}
