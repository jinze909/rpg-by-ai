using System;
using System.Reflection;

namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        private static GameCore SwordChainGame(Campaign data, int weapon = 0)
        {
            GameCore game = Quiet(data, weapon); game.SkipTutorial(); game.save.companion = -1;
            game.effects.Clear(); game.projectiles.Clear(); game.sounds.Clear(); return game;
        }
        private static ActorAction SwordChainStart(GameCore game)
        {
            for (int frame = 0; frame < 150 && (game.ActionTransitionBlocked || game.attackCooldown > 0); frame++)
                game.Tick(.01f, new Point());
            ActorAction previous = game.playerAction; game.Attack(new Point(1, 0));
            Check(game.playerAction != null && game.playerAction != previous && game.playerAction.kind == "attack",
                "The real mature Attack API accepts one fresh basic at its original readiness boundary");
            return game.playerAction;
        }
        private static void SwordChainRead(GameCore game)
        {
            for (int repeat = 0; repeat < 20; repeat++)
            {
                float remaining = game.SwordChainRemaining; int step = game.SwordChainStep;
                bool committed = game.SwordFinisherCommitted, released = game.SwordFinisherReleased;
                Check(!float.IsNaN(remaining) && !float.IsInfinity(remaining) && remaining >= 0 && step >= 0 && step <= 3
                    && (!released || committed), "Chain facts are finite, bounded and internally coherent");
            }
        }

        public static void SwordChainReportsAcceptedStartsIncludingMissesAndRejections(Campaign data)
        {
            Check(GameCore.ComboWindow == 1.1f && GameCore.SwordFinisherMultiplier == 1.25f,
                "The cue shares the unchanged one-point-one-second chain and original sword finisher multiplier");
            GameCore game = SwordChainGame(data);
            Check(game.SwordChainStep == 0 && game.SwordChainRemaining == 0 && !game.SwordFinisherCommitted,
                "An untouched sword has no started chain or invented committed finisher");
            for (int attack = 0; attack < 7; attack++)
            {
                ActorAction action = SwordChainStart(game); int expected = attack % 3;
                Check(game.enemies.Count == 0 && action.combo == expected && game.combo == expected
                    && game.SwordChainStep == expected + 1 && game.SwordChainRemaining == 1.1f,
                    "Every accepted empty-air attack advances and refreshes the mature chain, including its wrap: " + attack);
                Check(game.SwordFinisherCommitted == (expected == 2) && !game.SwordFinisherReleased,
                    "The third accepted sword preparation is committed before any enemy contact");
                float energy = game.save.energy, clock = game.comboTimer;
                game.Attack(new Point(0, -1));
                Check(ReferenceEquals(game.playerAction, action) && game.combo == expected && game.comboTimer == clock && game.save.energy == energy,
                    "Repeated blocked held input cannot invent a new paid attack or extra chain step");
            }
            GameCore rejected = SwordChainGame(data); rejected.save.weaponStyles[0] = 1; rejected.save.energy = 3.99f;
            rejected.Attack(new Point(1, 0));
            Check(rejected.playerAction == null && rejected.comboTimer == 0 && rejected.SwordChainStep == 0 && rejected.save.energy == 3.99f,
                "An unaffordable paid style cannot advance or advertise a chain");
            GameCore skill = SwordChainGame(data); SwordChainStart(skill); Advance(skill, .4f);
            int combo = skill.combo; float timer = skill.comboTimer; skill.Skill(new Point(1, 0));
            Check(skill.playerAction != null && skill.playerAction.kind == "skill" && skill.combo == combo
                && skill.comboTimer == timer && !skill.SwordFinisherCommitted,
                "The actual class skill does not increment or refresh the existing basic-attack chain");
        }

        public static void SwordChainRetainsAllWeaponCadencesCostsAndContactDamage(Campaign data)
        {
            for (int weapon = 0; weapon < 3; weapon++) for (int style = 0; style < 3; style++)
            {
                GameCore game = SwordChainGame(data, weapon); game.save.weaponStyles[weapon] = style;
                WeaponProfile profile = game.Profile(weapon, style);
                for (int index = 0; index < 4; index++)
                {
                    for (int frame = 0; frame < 150 && (game.ActionTransitionBlocked || game.attackCooldown > 0); frame++) game.Tick(.01f, new Point());
                    float before = game.save.energy; ActorAction action = SwordChainStart(game); int combo = index % 3;
                    float expected = profile.damage * (weapon == 0 && combo == 2 ? 1.25f : 1);
                    PlanStatsNear(action.damage, expected, "Accepted damage keeps the original weapon/style and third-sword multiplier");
                    PlanStatsNear(before - game.save.energy, profile.energy, "One accepted basic pays the original whole-attack energy");
                    PlanStatsNear(game.attackCooldown, profile.interval, "One accepted basic starts its complete original cooldown");
                    PlanStatsNear(action.duration, profile.interval, "Animation duration retains the real attack interval");
                    PlanStatsNear(action.contact, profile.interval * (weapon == 1 ? .55f : .45f), "The contact frame keeps its original weapon timing");
                    Check(action.combo == combo && game.SwordChainStep == (weapon == 0 ? combo + 1 : 0)
                        && game.SwordFinisherCommitted == (weapon == 0 && combo == 2),
                        "Global accepted combo still advances for every weapon while only sword facts are presented");
                    FocusRelease(game, action);
                    if (weapon != 0)
                    {
                        int count = weapon == 1 && style == 2 ? 3 : 1;
                        Check(game.projectiles.Count >= count, "The actual release retains every original arrow or spell");
                        for (int shot = game.projectiles.Count - count; shot < game.projectiles.Count; shot++)
                            PlanStatsNear(game.projectiles[shot].damage, expected * (weapon == 1 && style == 2 ? .65f : 1),
                                "Flying contact damage keeps each Flow arrow's existing coefficient");
                    }
                }
            }
            GameCore switched = SwordChainGame(data, 1); SwordChainStart(switched); Advance(switched, .5f);
            switched.Equip(0); ActorAction second = SwordChainStart(switched);
            Check(second.combo == 1 && switched.SwordChainStep == 2,
                "A real owned weapon swap retains the existing global combo rather than adding a new sword-only reset");
            Advance(switched, .4f); switched.riposteTime = .3f; ActorAction counter = SwordChainStart(switched);
            PlanStatsNear(counter.damage, switched.Profile(0, switched.Style).damage * 1.25f * 1.5f,
                "The actual third-sword counter retains both original multipliers");
            Check(counter.impact.counter && switched.riposteTime == 0 && switched.SwordFinisherCommitted,
                "Starting the finisher consumes counter readiness once and captures its original impact owner");
        }

        public static void SwordChainExpiryPausesAndCommittedContactKeepOriginalClocks(Campaign data)
        {
            GameCore expired = SwordChainGame(data); SwordChainStart(expired); Advance(expired, 1.11f);
            Check(expired.SwordChainRemaining == 0 && expired.SwordChainStep == 0, "The real simulation expiry hides a stale step");
            Check(SwordChainStart(expired).combo == 0, "The next accepted basic after expiry starts at the original first step");
            GameCore paused = SwordChainGame(data); SwordChainStart(paused); float timer = paused.comboTimer;
            foreach (string mode in new[] { "inventory", "journal", "pause", "dialog", "materials" })
            {
                paused.mode = mode; paused.Tick(.05f, new Point());
                Check(paused.comboTimer == timer && paused.SwordChainRemaining == timer,
                    "Raw chain facts use the original frozen menu clock without dispatching or clearing an accepted swing: " + mode);
            }
            paused.Close(); paused.hitStop = .1f; paused.Tick(.05f, new Point()); paused.Tick(.05f, new Point());
            Check(paused.comboTimer == timer, "Actual hit stop preserves the existing chain clock and prepared contact");
            paused.Tick(.02f, new Point()); PlanStatsNear(paused.SwordChainRemaining, timer - .02f, "The same clock resumes after hit stop");
            GameCore game = SwordChainGame(data); SwordChainStart(game); SwordChainStart(game); ActorAction third = SwordChainStart(game);
            Enemy target = FocusEnemy(game, 12901, game.Position.x + 1, game.Position.y, 9999);
            game.save.seed = 1; float hp = target.hp, damage = third.damage, elapsed = third.elapsed; ImpactProfile impact = third.impact;
            game.playerHitTime = 1.25f; game.playerRecoil = new Point();
            for (int frame = 0; frame < 115; frame++) game.Tick(.01f, new Point());
            Check(game.SwordChainRemaining == 0 && game.SwordChainStep == 0 && game.SwordFinisherCommitted
                && !game.SwordFinisherReleased && ReferenceEquals(game.playerAction, third) && third.elapsed == elapsed
                && target.hp == hp && third.damage == damage && ReferenceEquals(third.impact, impact),
                "Recoil can outlive the chain window but cannot erase the already paid third swing or release its contact early");
            FocusRelease(game, third);
            Check(game.SwordFinisherCommitted && game.SwordFinisherReleased && third.combo == 2 && third.damage == damage
                && ReferenceEquals(third.impact, impact), "The committed finisher becomes released on its real contact frame after recoil");
            PlanStatsNear(hp - target.hp, damage, "The actual resumed target hit uses captured finisher damage after the chain timer expires");
            Advance(game, .5f); Check(game.playerAction == null && !game.SwordFinisherCommitted && !game.SwordFinisherReleased,
                "The release receipt ends with the original actor action instead of creating a new persistent timer");
        }

        public static void SwordChainQueriesPreservePreparedQueuesProjectilesAndLegacyOwners(Campaign data)
        {
            GameCore prepared = SwordChainGame(data); SwordChainStart(prepared); SwordChainStart(prepared); ActorAction action = SwordChainStart(prepared);
            ImpactProfile impact = action.impact; SaveData owner = prepared.save; string before = PlanStatsState(prepared); SwordChainRead(prepared);
            Check(PlanStatsState(prepared) == before && ReferenceEquals(prepared.save, owner) && ReferenceEquals(prepared.playerAction, action)
                && ReferenceEquals(action.impact, impact), "Repeated chain queries do not recapture an accepted action, damage, aim, RNG, saved owner or effects");
            foreach (Action<GameCore> invalidate in new Action<GameCore>[] { g => g.save.weapon = 1, g => g.save.weaponStyles[0] = 1,
                g => g.save.profession = 1, g => g.save.technique = 1 })
            {
                GameCore stale = SwordChainGame(data); SwordChainStart(stale); SwordChainStart(stale); SwordChainStart(stale); invalidate(stale);
                string state = PlanStatsState(stale);
                Check(!stale.SwordFinisherCommitted && !stale.SwordFinisherReleased && PlanStatsState(stale) == state,
                    "A mismatched equipped action profile cannot advertise a committed finisher or be repaired by a read");
            }
            foreach (float timer in new[] { 0f, -1f, float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            {
                GameCore malformed = SwordChainGame(data); malformed.combo = 2; malformed.comboTimer = timer; before = PlanStatsState(malformed);
                Check(malformed.SwordChainRemaining == 0 && malformed.SwordChainStep == 0 && PlanStatsState(malformed) == before,
                    "Expired and nonfinite raw chain clocks show no step without mutating their owner");
            }
            foreach (int combo in new[] { -1, 3, int.MaxValue })
            {
                GameCore malformed = SwordChainGame(data); malformed.combo = combo; malformed.comboTimer = 1; before = PlanStatsState(malformed);
                Check(malformed.SwordChainRemaining == 0 && malformed.SwordChainStep == 0 && PlanStatsState(malformed) == before,
                    "Out-of-range raw combo values cannot invent progress or trigger a write-back");
            }
            foreach (string kind in new[] { "attack", "skill", "dodge", "companion" })
            {
                GameCore queued = SwordChainGame(data); string field;
                if (kind == "attack") { queued.attackCooldown = .08f; queued.PressAttack(new Point(0, -1)); field = "bufferedAttack"; }
                else if (kind == "skill") { queued.skillCooldown = .08f; queued.Skill(new Point(0, -1)); field = "bufferedSkill"; }
                else if (kind == "dodge") { queued.dashCooldown = .08f; queued.Dash(new Point(0, -1)); field = "bufferedDodge"; }
                else { queued.save.flags.Add("companion_1"); queued.SelectCompanion(1); queued.companionCooldown = .08f; queued.CommandCompanion(); field = "bufferedCompanionCommand"; }
                object intent = PlanStatsIntent(queued, field); Check(intent != null, "A genuine unpaid request exists before chain reads: " + kind);
                before = PlanStatsState(queued); SwordChainRead(queued);
                Check(PlanStatsState(queued) == before && ReferenceEquals(intent, PlanStatsIntent(queued, field)) && queued.SwordChainStep == 0,
                    "Reading chain facts preserves unpaid input ownership and cannot advance the accepted-start counter: " + kind);
            }
            GameCore archer = SwordChainGame(data, 1); ActorAction arrow = SwordChainStart(archer); FocusRelease(archer, arrow);
            Projectile shot = archer.projectiles[0]; before = PlanStatsState(archer); SwordChainRead(archer);
            Check(PlanStatsState(archer) == before && ReferenceEquals(shot, archer.projectiles[0]), "Sword queries cannot move or rewrite a flying projectile");
            foreach (int version in new[] { 1, 2 })
            {
                GameCore source = SwordChainGame(data); source.save.version = version; source.save.health = 62; source.save.energy = 44;
                source.save.coins = 91; source.save.flags.Add("legacy_sword_chain_keep"); SwordChainStart(source); SwordChainStart(source); SwordChainStart(source);
                source.PrepareSave(); GameCore loaded = new GameCore(data, CloneSave(source.save)); before = PlanStatsState(loaded); SwordChainRead(loaded);
                Check(loaded.SwordChainStep == 0 && loaded.SwordChainRemaining == 0 && !loaded.SwordFinisherCommitted && loaded.playerAction == null
                    && loaded.save.health == 62 && loaded.save.coins == 91 && loaded.Has("legacy_sword_chain_keep") && PlanStatsState(loaded) == before,
                    "The original v1/v2 prepare/load boundary restores saved progress and resets transient chain state without new saved fields");
            }
            Check(typeof(SaveData).GetField("swordChain") == null && typeof(SaveData).GetField("combo") == null
                && typeof(SaveData).GetField("comboTimer") == null, "The chain remains original unsaved combat state");
        }
    }
}
