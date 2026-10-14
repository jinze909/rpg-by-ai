using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        private static GameCore PlanGame(Campaign data, int profession = 0)
        {
            var g = Fresh(data, profession); g.SkipTutorial();
            // An earned mid-game inventory fixture isolates preparation from
            // crafting payouts. These flags are never awarded by a plan action.
            foreach (string flag in new[] { "weapon_1", "weapon_2", "core_1", "technique_" + profession,
                "relic_0", "relic_1", "relic_2", "relic_3", "relic_4", "relic_5" })
                if (!g.Has(flag)) g.save.flags.Add(flag);
            g.mode = "plans"; return g;
        }
        private static void PlanGear(GameCore g, int weapon, int style, int technique, int first, int second)
        {
            g.Equip(weapon); g.SetStyle(style);
            if (g.save.technique != technique) g.LearnTechnique();
            g.EquipRelic(0, first); g.EquipRelic(1, second);
            Check(g.save.weapon == weapon && g.Style == style && g.save.technique == technique &&
                g.save.relics[0] == first && g.save.relics[1] == second, "The owned equipment fixture uses the existing public equip controls");
        }
        private static bool SamePlan(BattlePlan p, int weapon, int style, int technique, int first, int second)
        { return p != null && p.recorded && p.weapon == weapon && p.style == style && p.technique == technique && p.relicFirst == first && p.relicSecond == second; }
        private static string PlanProtectedSave(SaveData save)
        {
            var output = new StringBuilder();
            foreach (FieldInfo field in typeof(SaveData).GetFields())
            {
                if (field.Name == "weapon" || field.Name == "weaponStyles" || field.Name == "technique" || field.Name == "relics" || field.Name == "battlePlans") continue;
                output.Append(field.Name).Append('='); object value = field.GetValue(save);
                var values = value as System.Collections.IEnumerable;
                if (values != null && !(value is string)) foreach (object item in values) output.Append(item).Append(',');
                else output.Append(value);
                output.Append(';');
            }
            return output.ToString();
        }
        private static GameCore PlanCombatGame(Campaign data, int profession)
        {
            var g = PlanGame(data, profession); g.Enter(1); g.enemies.Clear();
            g.save.x = 12; g.save.y = 14; g.save.reducedMotion = true; g.save.seed = 1;
            Check(g.CanStand(g.Position), "The combat preparation check uses legal mature-map ground");
            return g;
        }

        public static void BattlePlansCaptureThreeOwnedBuildsAndReturnCopies(Campaign data)
        {
            Check(GameCore.BattlePlanSlots == 3, "Three independent preparation slots are available");
            for (int profession = 0; profession < 3; profession++)
            {
                var g = PlanGame(data, profession);
                for (int slot = 0; slot < 3; slot++)
                {
                    Check(g.BattlePlanAt(slot) == null && !g.BattlePlanAvailable(slot) && !g.BattlePlanIsCurrent(slot), "A fresh slot contains no invented build");
                    PlanGear(g, slot, slot, slot % 2, slot, slot + 3);
                    Check(g.SaveBattlePlan(slot) && SamePlan(g.BattlePlanAt(slot), slot, slot, slot % 2, slot, slot + 3), "Saving captures each current owned weapon, its stance, class Q and two relics");
                    BattlePlan observed = g.BattlePlanAt(slot); observed.weapon = 99; observed.style = 99; observed.relicFirst = 99;
                    Check(SamePlan(g.BattlePlanAt(slot), slot, slot, slot % 2, slot, slot + 3), "Changing a returned DTO cannot alter its saved slot");
                }
                PlanGear(g, 0, 2, 1, -1, -1); Check(g.SaveBattlePlan(1), "Explicit overwrite records the current owned build");
                Check(SamePlan(g.BattlePlanAt(1), 0, 2, 1, -1, -1) && SamePlan(g.BattlePlanAt(0), 0, 0, 0, 0, 3) &&
                    SamePlan(g.BattlePlanAt(2), 2, 2, 0, 2, 5), "Overwriting one slot retains its two neighbors and allows empty relic slots");
            }
        }

        public static void BattlePlansApplyOnlySelectedWeaponStanceAndOwnedChoices(Campaign data)
        {
            for (int profession = 0; profession < 3; profession++)
            {
                var g = PlanGame(data, profession);
                PlanGear(g, 2, 1, 1, 4, 5); Check(g.SaveBattlePlan(0), "An advanced owned setup can be recorded");
                PlanGear(g, 1, 2, 0, 0, 3); g.save.weaponStyles[0] = 1; int swordStyle = g.save.weaponStyles[0], bowStyle = g.save.weaponStyles[1];
                Check(g.BattlePlanAvailable(0) && !g.BattlePlanIsCurrent(0) && g.ApplyBattlePlan(0), "One deliberate apply equips a different legal setup");
                Check(g.save.weapon == 2 && g.Style == 1 && g.save.technique == 1 && g.save.relics[0] == 4 && g.save.relics[1] == 5 &&
                    g.save.weaponStyles[0] == swordStyle && g.save.weaponStyles[1] == bowStyle && g.save.profession == profession,
                    "Applying changes only the destination weapon's stance, current class Q and the two relic slots; class identity and other stances remain");
                Check(g.BattlePlanIsCurrent(0) && !g.ApplyBattlePlan(0), "The equipped build is recognized and an equal apply is a no-op");
                PlanGear(g, 0, 0, 0, -1, -1); Check(g.SaveBattlePlan(2), "A light setup with the primary Q can be saved");
                Check(g.ApplyBattlePlan(0) && g.ApplyBattlePlan(2) && g.save.weapon == 0 && g.Style == 0 && g.save.technique == 0 &&
                    g.save.relics[0] == -1 && g.save.relics[1] == -1, "Returning to a saved plain setup explicitly unequips both relics");
            }
        }

        public static void BattlePlansRecheckAllUnlocksBeforeAtomicApply(Campaign data)
        {
            foreach (string missing in new[] { "weapon_1", "core_1", "technique_0", "relic_2", "relic_5" })
            {
                var g = PlanGame(data); PlanGear(g, 1, 1, 1, 2, 5); Check(g.SaveBattlePlan(0), "The initially owned proposed setup exists");
                PlanGear(g, 0, 0, 0, -1, -1); g.save.flags.Remove(missing);
                int weapon = g.save.weapon, style = g.Style, technique = g.save.technique, first = g.save.relics[0], second = g.save.relics[1];
                string protectedSave = PlanProtectedSave(g.save);
                Check(!g.BattlePlanAvailable(0) && !string.IsNullOrEmpty(g.BattlePlanUnavailableReason(0)) && !g.ApplyBattlePlan(0),
                    "A stale saved setup explains and rejects its now missing unlock: " + missing);
                Check(g.save.weapon == weapon && g.Style == style && g.save.technique == technique && g.save.relics[0] == first && g.save.relics[1] == second &&
                    PlanProtectedSave(g.save) == protectedSave && SamePlan(g.BattlePlanAt(0), 1, 1, 1, 2, 5), "Unavailable apply is atomic and retains the plan without buying its missing progress");
                g.save.flags.Add(missing); Check(g.BattlePlanAvailable(0) && g.ApplyBattlePlan(0), "Restoring the actual earned unlock makes the original saved choice usable");
            }
            var byMastery = PlanGame(data); PlanGear(byMastery, 1, 2, 0, -1, -1); Check(byMastery.SaveBattlePlan(1), "The bow stance can be saved");
            PlanGear(byMastery, 0, 0, 0, -1, -1); byMastery.save.flags.Remove("core_1"); byMastery.save.mastery[0] = 100;
            Check(!byMastery.BattlePlanAvailable(1) && !byMastery.ApplyBattlePlan(1), "Current sword mastery cannot unlock the candidate bow's stance");
            byMastery.save.mastery[1] = 20;
            Check(byMastery.BattlePlanAvailable(1) && byMastery.ApplyBattlePlan(1) && byMastery.Style == 2, "The candidate weapon's own first mastery rank opens its existing stance gate");
        }

        public static void BattlePlanTransactionsRequireLivePreparationAndValidSlots(Campaign data)
        {
            var g = PlanGame(data); Check(g.SaveBattlePlan(0), "A slot is populated for boundary checks");
            foreach (string mode in new[] { "world", "pause", "inventory", "talents", "dialog", "title", "settings", "practice" })
            {
                g.mode = mode; int revision = g.revision; string protectedSave = PlanProtectedSave(g.save);
                Check(!g.SaveBattlePlan(1) && !g.RemoveBattlePlan(0) && !g.ApplyBattlePlan(0) && g.BattlePlanAt(0) != null && g.BattlePlanAt(1) == null &&
                    g.revision == revision && PlanProtectedSave(g.save) == protectedSave, "Stale preparation callbacks cannot mutate from mode " + mode);
            }
            g.mode = "plans"; g.deathTime = .3f;
            Check(!g.SaveBattlePlan(1) && !g.RemoveBattlePlan(0) && !g.ApplyBattlePlan(0) && g.BattlePlanAt(0) != null, "A defeated hero cannot manage or apply preparation");
            g.deathTime = 0;
            foreach (int slot in new[] { -1, 3, int.MinValue, int.MaxValue })
                Check(g.BattlePlanAt(slot) == null && !g.BattlePlanAvailable(slot) && !g.BattlePlanIsCurrent(slot) && !g.SaveBattlePlan(slot) && !g.ApplyBattlePlan(slot) && !g.RemoveBattlePlan(slot),
                    "All public slot operations reject an out-of-range index: " + slot);
            Check(!g.RemoveBattlePlan(2) && g.RemoveBattlePlan(0) && g.BattlePlanAt(0) == null && !g.RemoveBattlePlan(0), "Remove clears only an occupied selected slot and does not fabricate a second mutation");
        }

        public static void BattlePlansPreservePaidGrowthStoryEconomyAndTransientVitals(Campaign data)
        {
            var g = PlanGame(data, 2); PlanGear(g, 0, 2, 0, 5, 2); Check(g.SaveBattlePlan(0), "A contrasting owned build is recorded");
            PlanGear(g, 2, 0, 1, 4, 0);
            g.save.coins = 197; g.save.shards = 43; g.save.skillPoints = 7; g.save.level = 8; g.save.xp = 17;
            g.save.upgrades = 2; g.save.talents = new[] { 3, 2, 1 }; g.save.maxHealth = 164; g.save.health = 61; g.save.energy = 42;
            g.save.mastery = new[] { 23, 64, 87 }; g.save.projects = new[] { 2, 1, 2 }; g.save.relationships = new[] { 3, 4, 5 };
            g.save.stage = 3; g.save.harborPledge = 2; g.save.desertChoice = 1; g.save.mountainChoice = 2; g.save.frontierEnding = 3;
            g.save.flags.Add("homeward_complete"); g.save.flags.Add("archive_complete"); g.save.flags.Add("scout_saved");
            g.save.totalTime = 197; g.save.plantedAt = 155; g.save.foodUntil = 250; g.save.teaUntil = 300;
            g.save.mealKind = 2; g.save.mealUntil = 280; g.save.controllerAimProfile = 1; g.save.controllerAimInvertY = true;
            g.save.bounty = 4; g.save.bountyProgress = 2; g.save.bountyCycle = 6;
            g.attackCooldown = .27f; g.skillCooldown = 2.6f; g.dashCooldown = .64f; g.dashTime = .08f; g.invulnerable = .2f; g.guardTime = 1.7f;
            g.save.flags.Add("companion_1"); g.SelectCompanion(1); g.companionCooldown = 3.1f;
            Point follower = g.companionPosition; string protectedSave = PlanProtectedSave(g.save);
            Check(g.ApplyBattlePlan(0) && g.SaveBattlePlan(2) && g.RemoveBattlePlan(2), "Preparation apply, copy and remove all work without touching paid progress");
            Check(PlanProtectedSave(g.save) == protectedSave && g.save.health == 61 && g.save.energy == 42 && g.save.maxHealth == 164 &&
                g.attackCooldown == .27f && g.skillCooldown == 2.6f && g.dashCooldown == .64f && g.dashTime == .08f && g.invulnerable == .2f && g.guardTime == 1.7f &&
                g.companionCooldown == 3.1f && (g.companionPosition - follower).Length == 0,
                "Preparation preserves every non-loadout save field, paid talents/mastery/technique ownership, stories, growth, economy, climate/meal clocks, vitals, cooldowns and selected follower");
        }

        public static void BattlePlansNormalizeLegacyRecordsWithoutInventingUnlocks(Campaign data)
        {
            // Unity's inline class serialization may materialize an empty
            // array entry instead of retaining null. Both plausible default
            // shapes must remain empty unless an actual save action marked it.
            foreach (BattlePlan ghost in new[] { new BattlePlan(),
                new BattlePlan { weapon = 0, style = 0, technique = 0, relicFirst = 0, relicSecond = 0 },
                new BattlePlan { weapon = 0, style = 0, technique = 0, relicFirst = -1, relicSecond = -1 } })
            {
                var g = new GameCore(data, new SaveData { battlePlans = new[] { ghost } });
                Check(g.BattlePlanAt(0) == null && !g.BattlePlanAvailable(0) && !g.BattlePlanIsCurrent(0),
                    "An unrecorded inline-object placeholder must not become a phantom saved sword build after reconstruction");
            }
            foreach (int version in new[] { 1, 2 })
            {
                var legacy = new SaveData { version = version, coins = 87, health = 62, energy = 44, battlePlans = null };
                var g = new GameCore(data, legacy);
                Check(g.save.battlePlans != null && g.save.battlePlans.Length == 3 && g.save.coins == 87 && g.save.health == 62 && g.save.energy == 44,
                    "A version " + version + " save without plans grows empty slots without paying or restoring vitals");
                for (int slot = 0; slot < 3; slot++) Check(g.BattlePlanAt(slot) == null, "Migration does not invent a saved or unlocked build");
            }
            var locked = new BattlePlan { recorded = true, weapon = 2, style = 2, technique = 1, relicFirst = 4, relicSecond = 5 };
            var shortSource = new[] { locked }; var shortSave = new SaveData { battlePlans = shortSource };
            var shortGame = new GameCore(data, shortSave); locked.weapon = 0;
            Check(shortGame.save.battlePlans.Length == 3 && SamePlan(shortGame.BattlePlanAt(0), 2, 2, 1, 4, 5) && shortGame.BattlePlanAt(1) == null &&
                shortGame.BattlePlanAt(2) == null && !shortGame.BattlePlanAvailable(0) && !shortGame.Has("weapon_2") && !shortGame.Has("technique_0") && !shortGame.Has("relic_4"),
                "A short save is deep-normalized, retains a structurally valid locked record, and never grants its gear");
            foreach (BattlePlan malformed in new[] {
                new BattlePlan { recorded = true, weapon = -1 }, new BattlePlan { recorded = true, weapon = 3 }, new BattlePlan { recorded = true, style = -1 }, new BattlePlan { recorded = true, style = 3 },
                new BattlePlan { recorded = true, technique = -1 }, new BattlePlan { recorded = true, technique = 2 }, new BattlePlan { recorded = true, relicFirst = -2 }, new BattlePlan { recorded = true, relicSecond = 6 },
                new BattlePlan { recorded = true, relicFirst = 2, relicSecond = 2 } })
            {
                var g = new GameCore(data, new SaveData { battlePlans = new[] { malformed, new BattlePlan { recorded = true, weapon = 0 }, null, new BattlePlan { recorded = true, weapon = 1 } } });
                Check(g.save.battlePlans.Length == 3 && g.BattlePlanAt(0) == null && SamePlan(g.BattlePlanAt(1), 0, 0, 0, -1, -1) && g.BattlePlanAt(2) == null,
                    "A malformed record is discarded independently, preserves valid neighbors and truncates an excessive slot array");
            }
            for (int profession = 0; profession < 3; profession++)
            {
                var plain = Fresh(data, profession); plain.mode = "plans";
                Check(plain.SaveBattlePlan(2) && SamePlan(plain.BattlePlanAt(2), profession, 0, 0, -1, -1),
                    "A real capture explicitly marks a legitimate plain starter build, including two empty relics");
                plain.PrepareSave(); plain.Load(CloneSave(plain.save)); plain.Start();
                Check(plain.BattlePlanAt(0) == null && plain.BattlePlanAt(1) == null && SamePlan(plain.BattlePlanAt(2), profession, 0, 0, -1, -1) &&
                    plain.BattlePlanAvailable(2) && plain.BattlePlanIsCurrent(2),
                    "Actual recorded plain builds survive reconstruction while adjacent unrecorded slots remain empty");
            }
            var earned = PlanGame(data); PlanGear(earned, 2, 1, 1, 1, 4); Check(earned.SaveBattlePlan(2), "The earned high-index slot exists before a load");
            var record = earned.BattlePlanAt(2); earned.PrepareSave(); earned.Load(CloneSave(earned.save)); earned.Start(); earned.mode = "plans";
            Check(SamePlan(earned.BattlePlanAt(2), record.weapon, record.style, record.technique, record.relicFirst, record.relicSecond) && earned.BattlePlanAvailable(2),
                "Preparation survives ordinary save reconstruction alongside the player's actual earned unlocks");
        }

        public static void BattlePlansChangedApplyCancelsPreparationWithoutRefundingContacts(Campaign data)
        {
            foreach (string action in new[] { "attack", "skill" })
            {
                var g = PlanCombatGame(data, 0); g.mode = "plans"; PlanGear(g, 1, 1, 1, -1, -1); Check(g.SaveBattlePlan(0), "The alternate preparation is recorded");
                PlanGear(g, 0, 0, 0, -1, -1); Check(g.SaveBattlePlan(1), "The current preparation is recorded for a no-op apply"); g.Close();
                var target = new Enemy(83000, 0, 13.3f, 14, 999) { stun = 99 }; g.enemies.Add(target);
                if (action == "attack") g.Attack(new Point(1, 0)); else g.Skill(new Point(1, 0));
                ActorAction prepared = g.playerAction; float energy = g.save.energy, skill = g.skillCooldown, attack = g.attackCooldown;
                Check(prepared != null && !prepared.released, "A real " + action + " is in preparation before opening the menu");
                g.mode = "plans"; int revision = g.revision;
                Check(!g.ApplyBattlePlan(1) && g.playerAction == prepared && g.revision == revision && g.save.energy == energy,
                    "Applying an equal current setup does not cancel its genuine prepared action or refund its energy");
                Check(g.ApplyBattlePlan(0) && g.playerAction == null && g.save.energy == energy && g.skillCooldown == skill && g.attackCooldown == attack,
                    "An actual equipment change cancels the prepared contact while retaining already paid energy and attack/Q cooldowns");
                g.Close(); Advance(g, .8f);
                Check(target.hp == 999 && g.projectiles.Count == 0, "The canceled prepared " + action + " cannot release a stale hit after returning to the world");
            }
        }

        public static void BattlePlansRetainLaunchedMissilesAndPersistentFrostProfiles(Campaign data)
        {
            var bow = PlanCombatGame(data, 1); bow.mode = "plans"; PlanGear(bow, 0, 2, 0, 5, -1); Check(bow.SaveBattlePlan(0), "A contrasting sword and critical relic setup exists");
            PlanGear(bow, 1, 1, 0, -1, -1); bow.Close(); var target = new Enemy(83001, 0, 17, 14, 999) { stun = 99 }; bow.enemies.Add(target);
            bow.Attack(new Point(1, 0)); ActorAction action = bow.playerAction;
            for (int frame = 0; frame < 50 && !action.released; frame++) bow.Tick(.01f, new Point());
            Check(action.released && bow.projectiles.Count == 1 && target.hp == 999, "A real committed bow action launches before its distant target is hit");
            Projectile shot = bow.projectiles[0]; float damage = shot.damage; ImpactProfile identity = shot.impact;
            Check(shot.piercing && identity.weapon == 1 && identity.style == 1 && identity.masteryWeapon == 1, "The launched missile carries the original bow stance and source");
            bow.mode = "plans"; Check(bow.ApplyBattlePlan(0) && bow.projectiles.Contains(shot) && shot.impact == identity && shot.damage == damage,
                "Applying another legal setup leaves a launched missile, its damage and immutable impact identity intact"); bow.Close();
            for (int frame = 0; frame < 100 && target.hp == 999; frame++) { target.stun = 99; bow.Tick(.01f, new Point()); }
            Check(Math.Abs(999 - target.hp - damage) < .002f, "The actual old arrow cannot borrow the new sword stance or critical relic on contact");

            var mage = PlanCombatGame(data, 2); mage.mode = "plans"; PlanGear(mage, 0, 2, 0, 5, -1); Check(mage.SaveBattlePlan(0), "A different setup exists for a running frost field");
            PlanGear(mage, 2, 0, 1, -1, -1); mage.Close(); var frostTarget = new Enemy(83002, 0, 14, 14, 999) { stun = 99 }; mage.enemies.Add(frostTarget);
            mage.Skill(new Point(1, 0)); float castDamage = mage.playerAction.damage;
            for (int frame = 0; frame < 50 && frostTarget.hp == 999; frame++) { frostTarget.stun = 99; mage.Tick(.01f, new Point()); }
            Check(frostTarget.hp < 999 && mage.effects.Exists(e => e.kind == "frost"), "The actual alternate Arcanist Q produces its first frost contact");
            float firstHp = frostTarget.hp; mage.mode = "plans"; Check(mage.ApplyBattlePlan(0), "A legal equipment change can be applied while the committed field remains"); mage.Close();
            for (int frame = 0; frame < 100 && frostTarget.hp == firstHp; frame++) { frostTarget.stun = 99; mage.Tick(.01f, new Point()); }
            Check(Math.Abs(firstHp - frostTarget.hp - castDamage * .4f) < .002f && mage.save.weapon == 0 && mage.save.technique == 0,
                "The next real frost pulse persists with its original caster damage instead of adopting the applied weapon, Q or relics");
            for (int profession = 0; profession < 3; profession++)
            {
                var g = PlanGame(data, profession);
                foreach (bool english in new[] { false, true }) { g.save.english = english;
                    Check(!string.IsNullOrEmpty(g.ClassTechniqueName(0)) && !string.IsNullOrEmpty(g.ClassTechniqueDescription(0)) &&
                        !string.IsNullOrEmpty(g.ClassTechniqueName(1)) && !string.IsNullOrEmpty(g.ClassTechniqueDescription(1)) && g.ClassTechniqueName(0) != g.ClassTechniqueName(1),
                        "Each actual profession has distinct primary/alternate Q labels and readable notes in both languages"); }
            }
        }
    }
}
