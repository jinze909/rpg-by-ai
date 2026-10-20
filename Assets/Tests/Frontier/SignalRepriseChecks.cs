using System;
using System.Collections.Generic;
using System.Reflection;

namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        private static readonly BindingFlags reprisePrivate = BindingFlags.Instance | BindingFlags.NonPublic;
        private static GameCore RepriseGame(Campaign data, int profession = 0)
        {
            GameCore g = SignalReadyGame(data, profession);
            g.save.flags.Add("signal_started"); g.save.flags.Add("signal_complete");
            g.save.reducedMotion = true; g.invulnerable = 0; g.save.companion = -1;
            return g;
        }
        private static void RepriseLamp(GameCore g)
        { g.save.x = g.SignalPoint.x; g.save.y = g.SignalPoint.y - 1; }
        private static Enemy RepriseStart(GameCore g)
        {
            Check(g.StartSignalReprise(), "A deliberate completed-story reprise starts at the actual third forest signal");
            Enemy e = g.enemies.Find(GameCore.IsSignalWarden);
            Check(e != null && e.hp == 300 && e.maxHp == 300 && g.SignalRepriseActive && g.IsSignalRepriseActor(e),
                "Every reprise owns one new full 300-HP Warden using the existing reserved identity");
            return e;
        }
        private static void RepriseHit(GameCore g, Enemy e, float damage, bool owned)
        {
            typeof(GameCore).GetMethod("Hit", reprisePrivate).Invoke(g, new object[] {
                e, damage, false, (Point?)new Point(1, 0), new ImpactProfile(0, 0, 0, 0, 0, owned) });
        }
        private static void RepriseHurt(GameCore g, float damage)
        { typeof(GameCore).GetMethod("Hurt", reprisePrivate).Invoke(g, new object[] { damage, "shot", (Point?)new Point(1, 0) }); }
        private static object RepriseField(GameCore g, string name)
        { return typeof(GameCore).GetField(name, reprisePrivate).GetValue(g); }
        private static void RepriseNoReward(SaveData before, SaveData after)
        {
            Check(after.coins == before.coins && after.xp == before.xp && after.level == before.level &&
                after.shards == before.shards && after.skillPoints == before.skillPoints && after.stage == before.stage &&
                after.bounty == before.bounty && after.bountyProgress == before.bountyProgress &&
                TacticsState(after.mastery) == TacticsState(before.mastery) && TacticsState(after.kills) == TacticsState(before.kills) &&
                TacticsState(after.flags) == TacticsState(before.flags),
                "Reprise combat never repeats rewards, XP, mastery, bounty, kill credit or any story flag");
            for (int item = 0; item < before.inventory.Length; item++) if (item != 6)
                Check(after.inventory[item] == before.inventory[item], "Reprise adds no enemy loot or materials");
        }
        private static void RepriseFight(GameCore g)
        {
            int frames = 0, attacks = 0, techniques = 0, dodges = 0, meals = 0;
            int stew = g.save.inventory[6]; g.Eat(); meals++;
            Check(g.save.inventory[6] == stew - 1 && g.save.foodUntil > g.save.totalTime,
                "The real Eat action consumes the one starter stew and applies its normal food duration");
            while (g.SignalRepriseActive && frames++ < 16000)
            {
                Check(g.save.room == 1 && !g.Dying, "A starter must survive the authored reprise without forced health or invulnerability");
                Enemy target = g.enemies.Find(GameCore.IsSignalWarden);
                float distance = (target.Position - g.Position).Length;
                Point movement = distance > 1.4f ? g.DirectionTo(g.Position, target.Position) : new Point();
                if (target.state == 1 && (g.Position - target.target).Length < 3.4f)
                {
                    movement = (g.Position - target.target).Length > .05f ? (g.Position - target.target).Normalized : new Point(-1, 0);
                    if (g.dashCooldown <= 0) { float old = g.dashTime; g.Dash(movement); if (g.dashTime > old) dodges++; }
                }
                // Read and evade the first warning before attacking, so even
                // the Ranger's short fight exercises the real dodge cost.
                if (dodges == 0) { g.Tick(.02f, movement); continue; }
                ActorAction previous = g.playerAction; float skillBefore = g.skillCooldown;
                if (distance < 3 && g.skillCooldown <= 0 && g.save.energy >= 40)
                    g.Skill(target.Position - g.Position);
                else g.Attack(target.Position - g.Position);
                if (g.playerAction != previous && g.playerAction != null && g.playerAction.kind == "attack") attacks++;
                if (g.save.health < g.save.maxHealth * .6f && g.save.inventory[6] > 0) { g.Eat(); meals++; }
                g.Tick(.02f, movement);
                // A real buffered Q can begin inside Tick after dodge recovery.
                if (g.skillCooldown > skillBefore + .1f) techniques++;
            }
            Console.WriteLine("  Reprise starter " + g.save.profession + ": " + frames + " frames; attacks=" + attacks + ", Q=" + techniques +
                ", dodges=" + dodges + ", meals=" + meals + "; health=" + g.save.health + "; active=" + g.SignalRepriseActive + "; won=" +
                (g.LastSignalReprise != null && g.LastSignalReprise.won));
            Check(frames < 16000 && !g.Dying && !g.SignalRepriseActive && g.LastSignalReprise != null && g.LastSignalReprise.won &&
                attacks > 0 && techniques > 0 && dodges > 0 && meals > 0,
                "Every starter wins using real Attack, Q, Dash and Eat while the original AI and costs run");
        }

        public static void SignalRepriseRequiresCompletedStoryAndPhysicalThirdLamp(Campaign data)
        {
            var locked = Fresh(data); locked.SkipTutorial(); locked.Enter(1); locked.enemies.Clear(); RepriseLamp(locked);
            Check(!locked.StartSignalReprise(), "Neither story completion can be skipped");
            locked.save.flags.Add("signal_complete"); Check(!locked.StartSignalReprise(), "Signal completion alone cannot skip the completed watch");
            var first = SignalReadyGame(data); Check(!first.StartSignalReprise() && first.StartSignalEncounter(),
                "The first Warden remains the original reward-bearing story trial");
            var g = RepriseGame(data); RepriseLamp(g);
            foreach (string mode in new[] { "title", "dialog", "inventory", "pause", "journal", "map", "plans", "fish" })
            { g.mode = mode; Check(!g.StartSignalReprise(), "Only the active world can start a reprise: " + mode); }
            g.Close(); g.Enter(0); g.enemies.Clear();
            Check(!g.StartSignalReprise(), "The completed scout conversation in town cannot remotely spawn a forest Warden");
            g.Enter(1); g.enemies.Clear(); g.save.x = 7; g.save.y = 11;
            Check(!g.StartSignalReprise(), "The first lamp is not the third lamp");
            RepriseLamp(g); WorldObject[] objects = g.Map.objects;
            try
            {
                g.Map.objects = Array.FindAll(objects, o => o.kind != "watchSignal2");
                Check(!g.StartSignalReprise(), "A coordinate alone cannot substitute for a present physical third signal");
            }
            finally { g.Map.objects = objects; }
            g.save.flags.Remove("watch_signal_2"); Check(!g.StartSignalReprise(), "The actual third lamp must remain lit and present");
            g.save.flags.Add("watch_signal_2");
            string[] rows = g.Map.rows;
            try
            {
                g.Map.rows = (string[])rows.Clone(); char[] row = g.Map.rows[9].ToCharArray(); row[23] = '#'; g.Map.rows[9] = new string(row);
                Check(!g.StartSignalReprise(), "The physical lamp interaction also requires an unobstructed contact path");
            }
            finally { g.Map.rows = rows; }
            g.Attack(new Point(-1, 0)); Check(!g.StartSignalReprise(), "An unfinished attack cannot carry into a new attempt");
            Advance(g, 1); g.Dash(new Point(-1, 0)); Check(!g.StartSignalReprise(), "An active dodge cannot carry into a new attempt");
            Advance(g, .4f); RepriseLamp(g); g.invulnerable = 0; RepriseHurt(g, 1);
            Check(!g.StartSignalReprise(), "A hit reaction must finish before beginning"); Advance(g, .3f); RepriseLamp(g);
            var blocker = new Enemy(610, 3, 28, 8, 100, true); g.enemies.Add(blocker);
            Check(!g.StartSignalReprise(), "A chapter Boss cannot be stacked with the reprise"); g.enemies.Remove(blocker);
            var impostor = new Enemy(GameCore.SignalWardenId, 0, 28, 8, 10); g.enemies.Add(impostor);
            Check(!g.StartSignalReprise(), "A stale reserved-ID actor blocks duplicate ownership even with a wrong role"); g.enemies.Remove(impostor);
            g.save.health = 73; g.save.energy = 51; SaveData before = CloneSave(g.save);
            Enemy e = RepriseStart(g);
            Check(g.save.health == 73 && g.save.energy == 51 && g.save.totalTime == before.totalTime && g.save.inventory[6] == before.inventory[6],
                "Beginning has no healing, energy refill, resource cost or artificial time advance");
            RepriseNoReward(before, g.save);
            Check(!g.StartSignalReprise() && g.enemies.FindAll(GameCore.IsSignalWarden).Count == 1 && g.IsSignalRepriseActor(e),
                "Repeated callbacks cannot stack or replace the owned attempt");
            var stale = RepriseGame(data); stale.ShowSignalReprise(); int choice = Array.FindIndex(stale.conversation.choices, c => c.action == "signal_reprise_start");
            stale.save.x = 7; stale.save.y = 11; stale.Choose(choice);
            Check(!stale.SignalRepriseActive, "A stale start menu rechecks physical proximity when its callback runs");
        }

        public static void SignalRepriseThreeStartersWinThroughNormalPublicActions(Campaign data)
        {
            for (int profession = 0; profession < 3; profession++)
            {
                var g = RepriseGame(data, profession); g.save.bounty = 3; g.save.bountyProgress = 0;
                Check(g.save.level == 1 && g.save.upgrades == 0 && g.save.weapon == profession && g.save.inventory[6] == 1 && g.invulnerable == 0,
                    "Positive victories use starter equipment, one starter meal and no patched invulnerability");
                SaveData before = CloneSave(g.save); Enemy e = RepriseStart(g); RepriseFight(g);
                Console.WriteLine("  Recorded: damage=" + g.LastSignalReprise.damage.ToString("R") + ", contacts=" + g.LastSignalReprise.contacts +
                    ", breaks=" + g.LastSignalReprise.breaks + ", seconds=" + g.LastSignalReprise.seconds.ToString("R"));
                Check(!g.enemies.Contains(e) && Math.Abs(g.LastSignalReprise.damage - 300) < .001f && g.LastSignalReprise.contacts > 0 && g.LastSignalReprise.breaks > 0 &&
                    g.LastSignalReprise.seconds > 0 && g.LastSignalReprise.hitsTaken >= 0,
                    "A full 300-HP win records actual capped player damage, real contacts, breaks and elapsed simulation time");
                RepriseNoReward(before, g.save);
                Check(g.save.inventory[6] == before.inventory[6] - 1 && g.save.totalTime > before.totalTime && g.SignalComplete && g.SignalReady,
                    "Normal supplies and world time apply while original completion stays earned");
            }
        }

        public static void SignalRepriseClockCountsOnlyActiveSimulation(Campaign data)
        {
            var g = RepriseGame(data); Enemy e = RepriseStart(g); e.timer = 1000;
            FieldInfo clock = typeof(GameCore).GetField("signalRepriseClock", reprisePrivate);
            Check(clock != null && clock.IsPrivate && clock.FieldType == typeof(float), "Elapsed time has a private attempt-owned simulation clock");
            g.save.totalTime = 16777216f; float world = g.save.totalTime;
            g.Tick(.02f, new Point());
            Check(g.SignalRepriseTime > .0199f && g.SignalRepriseTime < .0201f && g.save.totalTime == world,
                "Huge accumulated world time cannot erase small reprise intervals through float subtraction");
            float time = g.SignalRepriseTime;
            foreach (string mode in new[] { "dialog", "inventory", "journal", "map", "pause", "plans", "fish" })
            { g.mode = mode; g.Tick(.05f, new Point(1, 0)); Check(g.SignalRepriseTime == time, "Menu pauses reprise time: " + mode); }
            g.Close(); g.hitStop = .04f; g.Tick(.02f, new Point()); g.Tick(.02f, new Point());
            Check(g.SignalRepriseTime == time, "Hitstop pauses the same clock as the real combat simulation");
            g.Tick(.02f, new Point()); Check(Math.Abs(g.SignalRepriseTime - time - .02f) < .0001f, "The first live frame after hitstop advances exactly once");
            time = g.SignalRepriseTime;
            foreach (float dt in new[] { -1f, 0f, float.NaN, float.PositiveInfinity }) g.Tick(dt, new Point());
            Check(g.SignalRepriseTime == time, "Invalid and nonpositive Tick inputs cannot invent attempt time");
            g.Tick(5, new Point()); Check(Math.Abs(g.SignalRepriseTime - time - .05f) < .0001f, "Long frames share the production .05-second simulation clamp");
            time = g.SignalRepriseTime; RepriseLamp(g); Check(g.StopSignalReprise() && Math.Abs(g.LastSignalReprise.seconds - time) < .0001f,
                "Stopping freezes the exact active clock into the immutable result");
            Advance(g, 1); Check(g.SignalRepriseTime == time && g.LastSignalReprise.seconds == time, "World time after an attempt cannot alter its result");
            RepriseStart(g); Check(g.SignalRepriseTime == 0 && g.LastSignalReprise.seconds == time, "Retry starts a fresh clock while retaining the last comparison");
        }

        public static void SignalRepriseRecordsReadOnlyCappedPlayerDamageAndHealthLoss(Campaign data)
        {
            var g = RepriseGame(data); Enemy e = RepriseStart(g); e.timer = 1000;
            foreach (FieldInfo field in typeof(SignalRepriseRecord).GetFields()) Check(field.IsInitOnly, "Result fields are immutable: " + field.Name);
            foreach (PropertyInfo property in new[] { typeof(GameCore).GetProperty("LastSignalReprise"), typeof(GameCore).GetProperty("PreviousSignalReprise") })
                Check(property.GetSetMethod() == null && property.GetSetMethod(true).IsPrivate, "Consumers cannot replace comparison records");
            string before = TacticsState(g);
            for (int i = 0; i < 100; i++)
                Check(g.SignalRepriseActive && g.SignalRepriseTime == 0 && g.SignalRepriseDamage == 0 && g.SignalRepriseContacts == 0 &&
                    g.SignalRepriseBreaks == 0 && g.SignalRepriseHitsTaken == 0 && g.LastSignalReprise == null && g.PreviousSignalReprise == null &&
                    g.IsSignalRepriseActor(e) && !g.IsSignalRepriseActor(new Enemy(GameCore.SignalWardenId, 3, e.x, e.y, 300, true)),
                    "Live HUD queries report owned actor identity without dispatching combat");
            Check(TacticsState(g) == before, "Repeated reprise reads mutate no public or private simulation or save state");
            RepriseHit(g, e, 10, false);
            Check(e.hp == 290 && g.SignalRepriseDamage == 0 && g.SignalRepriseContacts == 0, "A real non-player impact damages the Boss without inflating player statistics");
            RepriseHit(g, e, 17, true); Check(g.SignalRepriseDamage == 17 && g.SignalRepriseContacts == 1, "Only actual player-owned health loss becomes a contact");
            RepriseHit(g, e, 0, true); Check(g.SignalRepriseContacts == 1, "Zero damage is not a recorded successful contact");
            e.stagger = 94; e.staggerRecovery = 0; RepriseHit(g, e, 10, true);
            Check(g.SignalRepriseDamage == 27 && g.SignalRepriseContacts == 2 && g.SignalRepriseBreaks == 1,
                "A break is counted on the actual player impact that crosses the authored stagger threshold");
            var ordinary = new Enemy(620, 0, 30, 16, 100); g.enemies.Add(ordinary); RepriseHit(g, ordinary, 12, true);
            Check(g.SignalRepriseDamage == 27 && g.SignalRepriseContacts == 2, "Player hits on unrelated forest actors are excluded");
            g.invulnerable = 0; float health = g.save.health; RepriseHurt(g, 0);
            Check(g.save.health == health && g.SignalRepriseHitsTaken == 0, "A zero-health-loss hurt is excluded");
            g.invulnerable = 0; g.projectiles.Add(new Projectile { position = g.Position, damage = 7, source = "shot", ownerId = 621 }); g.Tick(.02f, new Point());
            Check(g.save.health < health && g.SignalRepriseHitsTaken == 1, "A real hostile projectile health loss records one hit taken");
            health = g.save.health; RepriseHurt(g, 7);
            Check(g.save.health == health && g.SignalRepriseHitsTaken == 1, "An invulnerable repeat contact cannot inflate hits taken");
            RepriseHit(g, e, 10000, true); SignalRepriseRecord won = g.LastSignalReprise;
            Check(won != null && won.won && won.damage == 290 && won.contacts == 3 && won.breaks == 1 && won.hitsTaken == 1,
                "A lethal overkill adds only remaining health, excludes prior non-player damage and freezes the real counters");
            RepriseLamp(g); Advance(g, .3f); RepriseStart(g);
            Check(ReferenceEquals(g.LastSignalReprise, won) && g.PreviousSignalReprise == null && g.SignalRepriseDamage == 0 && g.SignalRepriseContacts == 0,
                "Starting a comparison clears current counters and preserves the completed record");
            g.Tick(.02f, new Point()); Check(g.StopSignalReprise(), "A second attempt can be voluntarily recorded at the lamp");
            SignalRepriseRecord stopped = g.LastSignalReprise;
            Check(!stopped.won && ReferenceEquals(g.PreviousSignalReprise, won) && won.damage == 290 && won.contacts == 3,
                "Stopping shifts the previous immutable record once without mutating it");
            Check(!g.StopSignalReprise() && ReferenceEquals(g.LastSignalReprise, stopped) && ReferenceEquals(g.PreviousSignalReprise, won),
                "Repeated stop callbacks cannot replace the record or shift history twice");
            RepriseStart(g); Check(g.StopSignalReprise() && ReferenceEquals(g.PreviousSignalReprise, stopped), "Only the two most recent session attempts remain for comparison");
        }

        public static void SignalRepriseStopRetryRetiresOnlyAttemptActions(Campaign data)
        {
            var g = RepriseGame(data, 2); g.save.flags.Add("technique_2"); g.save.technique = 1;
            var forest = new Enemy(630, 0, 30, 16, 80) { stun = 1000 }; g.enemies.Add(forest);
            g.Skill(new Point(-1, 0)); Advance(g, .7f);
            Check(g.playerAction == null && RepriseField(g, "frost") != null, "The real alternate Q leaves a persistent frost field after its action finishes");
            var friendly = new Projectile { position = new Point(30, 15), life = 100, friendly = true };
            var owned = new Projectile { position = new Point(30, 17), life = 100, ownerId = GameCore.SignalWardenId };
            var hostile = new Projectile { position = new Point(30, 14), life = 100, ownerId = 631, source = "shot" };
            g.projectiles.Add(friendly); g.projectiles.Add(owned); g.projectiles.Add(hostile); RepriseLamp(g);
            Enemy e = RepriseStart(g);
            Check(RepriseField(g, "frost") == null && !g.projectiles.Contains(friendly) && !g.projectiles.Contains(owned) && g.projectiles.Contains(hostile) && g.enemies.Contains(forest),
                "Starting clears stale frost, friendly shots and owned Warden shots while preserving old forest actors and unrelated hostile shots");
            g.save.x = 7; g.save.y = 11; Check(!g.StopSignalReprise() && g.SignalRepriseActive, "Stop requires returning to the actual third lamp"); RepriseLamp(g);
            foreach (string mode in new[] { "dialog", "pause", "inventory" }) { g.mode = mode; Check(!g.StopSignalReprise(), "Paused callbacks cannot stop an attempt"); } g.Close();
            g.skillCooldown = 0; g.save.energy = 100; g.Skill(new Point(-1, 0)); ActorAction oldQ = g.playerAction;
            Check(oldQ != null && oldQ.kind == "skill" && !oldQ.released, "A real delayed Q is pending before stop");
            float health = g.save.health, energy = g.save.energy, time = g.save.totalTime;
            Check(g.StopSignalReprise() && !g.enemies.Contains(e) && g.playerAction == null && RepriseField(g, "frost") == null,
                "Stop removes only the reprise actor and cancels the old delayed Q and persistent field");
            Check(g.save.health == health && g.save.energy == energy && g.save.totalTime == time && g.enemies.Contains(forest) && g.projectiles.Contains(hostile),
                "Voluntary stop neither refunds resources nor rewinds world time or the surrounding forest");
            Enemy retry = RepriseStart(g); Advance(g, .7f);
            Check(retry.hp == 300 && !oldQ.released && g.SignalRepriseDamage == 0 && g.enemies.Contains(forest),
                "A cancelled old Q cannot release into the new actor that reuses ID 73000");
            g.skillCooldown = 0; g.save.energy = 100; g.Skill(new Point(-1, 0)); Advance(g, .35f);
            Check(RepriseField(g, "frost") != null, "The new attempt can create real fresh frost normally");
            RepriseLamp(g); g.projectiles.Add(friendly); g.projectiles.Add(owned);
            Check(g.StopSignalReprise() && RepriseField(g, "frost") == null && !g.projectiles.Contains(friendly) && !g.projectiles.Contains(owned) &&
                g.projectiles.Contains(hostile) && g.enemies.Contains(forest), "Stop retires the active field and attack missiles with the same limited cleanup as retry");
            RepriseStart(g); Advance(g, .8f); Check(g.SignalRepriseDamage == 0, "The previous attempt's remaining frost pulses cannot score in a retry");
        }

        public static void SignalRepriseClonesAndLegacySavesNeverResumeOrRepay(Campaign data)
        {
            var g = RepriseGame(data); Enemy e = RepriseStart(g); RepriseHit(g, e, 33, true); g.Tick(.02f, new Point()); g.PrepareSave();
            SaveData checkpoint = CloneSave(g.save);
            Check(checkpoint.worldEnemies != null && Array.Exists(checkpoint.worldEnemies, r => r.id == GameCore.SignalWardenId),
                "The existing world snapshot format is exercised rather than replaced by a reprise save schema");
            foreach (FieldInfo field in typeof(SaveData).GetFields())
                Check(field.Name.IndexOf("reprise", StringComparison.OrdinalIgnoreCase) < 0 && field.FieldType != typeof(SignalRepriseRecord),
                    "Session comparison adds no persistence fields: " + field.Name);
            foreach (int version in new[] { 1, 2 })
            {
                SaveData saved = CloneSave(checkpoint); saved.version = version;
                saved.worldEnemies = new[] {
                    new EnemyRecord { id = GameCore.SignalWardenId, kind = 3, boss = true, x = 20, y = 9, hp = 267, maxHp = 300 },
                    new EnemyRecord { id = GameCore.SignalWardenId, kind = 0, boss = false, x = 20, y = 9, hp = 50, maxHp = 50 },
                    new EnemyRecord { id = 641, kind = 1, x = 30, y = 16, hp = 31, maxHp = 80 } };
                SaveData before = CloneSave(saved); var loaded = new GameCore(data, saved); loaded.Start();
                Check(loaded.save.version == 2 && loaded.SignalReady && loaded.SignalComplete && !loaded.SignalActive && !loaded.SignalRepriseActive &&
                    loaded.enemies.TrueForAll(x => x.id != GameCore.SignalWardenId) && loaded.LastSignalReprise == null && loaded.PreviousSignalReprise == null &&
                    loaded.enemies.Exists(x => x.id == 641 && x.hp == 31),
                    "Both save versions filter completed reserved-ID actors, retain ordinary live forest HP and begin with no resumed reprise or record");
                RepriseNoReward(before, loaded.save); RepriseLamp(loaded); Enemy retry = RepriseStart(loaded);
                Check(retry.hp == 300 && loaded.SignalRepriseDamage == 0 && loaded.SignalRepriseTime == 0, "Reload requires a deliberate full-health retry with fresh session counters");
                loaded.PrepareSave(); loaded.Load(CloneSave(loaded.save)); loaded.Start();
                Check(!loaded.SignalRepriseActive && !loaded.SignalActive && loaded.LastSignalReprise == null && loaded.PreviousSignalReprise == null,
                    "Loading into the same GameCore also clears attempt ownership and records");
                RepriseNoReward(before, loaded.save);
            }
            var original = SignalReadyGame(data); Enemy first = SignalStart(original); first.hp = 143; original.PrepareSave();
            var resumed = new GameCore(data, CloneSave(original.save)); resumed.Start();
            Check(resumed.SignalActive && !resumed.SignalRepriseActive && resumed.enemies.Find(GameCore.IsSignalWarden).hp == 143 && !resumed.SignalComplete,
                "The first unfinished story Warden still resumes its original saved health normally");
        }

        public static void SignalRepriseDeathTravelNewAndTrainingClearSession(Campaign data)
        {
            foreach (string boundary in new[] { "travel", "reload", "new", "death", "training" })
            {
                var g = RepriseGame(data); RepriseStart(g); g.Tick(.02f, new Point()); Check(g.StopSignalReprise(), "Create a prior local comparison before the lifecycle boundary");
                RepriseStart(g); g.Tick(.02f, new Point()); Check(g.StopSignalReprise(), "Create the second comparison before the lifecycle boundary");
                Check(g.LastSignalReprise != null && g.PreviousSignalReprise != null, "Both session records exist before reset"); RepriseStart(g);
                if (boundary == "travel") { g.Enter(0); g.Enter(1); }
                else if (boundary == "reload") { g.PrepareSave(); g.Load(CloneSave(g.save)); g.Start(); }
                else if (boundary == "new") { g.NewGame(1); g.Close(); }
                else if (boundary == "training") { g.Enter(0); g.StartPractice(); Check(g.practicing, "Actual basic training starts normally"); }
                else
                {
                    g.invulnerable = 0; g.save.health = 1;
                    g.projectiles.Add(new Projectile { position = g.Position, damage = 999, source = "shot" }); g.Tick(.02f, new Point());
                    Check(g.Dying && !g.SignalRepriseActive && g.LastSignalReprise == null && g.PreviousSignalReprise == null,
                        "Lethal real contact immediately abandons the attempt and clears local comparison history");
                    Advance(g, 1); Check(g.save.room == 0 && !g.SignalActive, "Ordinary death recovery returns home without a reprise ambush");
                }
                Check(!g.SignalRepriseActive && g.LastSignalReprise == null && g.PreviousSignalReprise == null && g.SignalRepriseTime == 0 &&
                    g.SignalRepriseDamage == 0 && g.SignalRepriseContacts == 0 && g.SignalRepriseBreaks == 0 && g.SignalRepriseHitsTaken == 0,
                    "Lifecycle reset clears all transient ownership, counters and comparison records: " + boundary);
                if (boundary != "new") Check(g.SignalReady && g.SignalComplete, "Lifecycle reset preserves earned original story completion: " + boundary);
            }
        }

        public static void SignalRepriseKeepsOriginalPatternsCompletedStoryAndFirstReward(Campaign data)
        {
            var first = SignalReadyGame(data); SaveData original = CloneSave(first.save); Enemy originalEnemy = SignalStart(first);
            SignalAttackUntilDefeated(first, originalEnemy);
            Check(first.SignalComplete && first.save.shards == original.shards + 6 && first.save.skillPoints == original.skillPoints + 1 &&
                first.LastSignalReprise == null && first.PreviousSignalReprise == null, "First story victory keeps exactly its original one-time reward and creates no reprise record");
            RepriseLamp(first); first.ShowSignalTrial();
            Check(first.conversation.textEn == "The warden in the old light lowers its weapon. It guards a path between the roots, rather than a claim over the forest. The patrol is gone, but travelers still see the light it left behind." &&
                first.conversation.choices[0].action == "close" && first.conversation.choices[0].en == "Keep the signal burning" &&
                first.conversation.choices.Length == 2 && first.conversation.choices[1].action == "signal_reprise_menu",
                "The completed story text and first close choice stay exact while the optional reprise menu is appended");
            first.Close();
            for (int pattern = 0; pattern < 3; pattern++)
            {
                var normal = SignalReadyGame(data); SignalStart(normal);
                var reprise = RepriseGame(data); RepriseStart(reprise);
                Enemy a = SignalWindup(normal, pattern), b = SignalWindup(reprise, pattern);
                Check(a.windupDuration == b.windupDuration && a.windupDuration == (pattern == 0 ? .95f : pattern == 1 ? 1.15f : 1.1f) &&
                    (a.attackOrigin - b.attackOrigin).Length < .0001f && (a.attackDestination - b.attackDestination).Length < .0001f &&
                    (a.target - b.target).Length < .0001f && (a.facing - b.facing).Length < .0001f && a.BossPhase == b.BossPhase && a.AttackPhase == b.AttackPhase,
                    "Reprise starts the original geometry, health phase and telegraph duration for pattern " + pattern);
                Point origin = b.attackOrigin, target = b.target, facing = b.facing, destination = b.attackDestination; float windup = b.windupDuration;
                // Move along the advertised safe central lane, not into a fan
                // lane that would consume a shot before its count is inspected.
                a.hp = b.hp = 80; normal.save.x += 1; reprise.save.x += 1;
                normal.Tick(.02f, new Point()); reprise.Tick(.02f, new Point());
                Check(b.BossPhase == 3 && b.windupDuration == windup && (b.attackOrigin - origin).Length < .0001f &&
                    (b.attackDestination - destination).Length < .0001f && (b.target - target).Length < .0001f && (b.facing - facing).Length < .0001f,
                    "Original phase changes cannot reshape a captured reprise warning");
                SignalRelease(normal, a); SignalRelease(reprise, b);
                float recovery = pattern == 0 ? .9f : pattern == 1 ? 1.1f : 1.2f;
                Console.WriteLine("  Pattern " + pattern + ": recovery " + a.timer + "/" + b.timer + ", cycle " + a.cycle + "/" + b.cycle +
                    ", shots " + normal.projectiles.Count + "/" + reprise.projectiles.Count + ", actors " + reprise.enemies.Count);
                Check(Math.Abs(a.timer - recovery) < .001f && Math.Abs(b.timer - recovery) < .001f && a.cycle == b.cycle &&
                    normal.projectiles.Count == reprise.projectiles.Count && reprise.enemies.Count == 1,
                    "All three original recovery clocks, cycle order and finite shot counts are unchanged in reprise");
                if (pattern == 1) Check(GameCore.SignalMarks(b).Length == 2 && GameCore.SignalMarkRadius == .9f, "Paired marks retain their original safe centre geometry");
                if (pattern == 2) Check(reprise.projectiles.Count == 4 && GameCore.SignalFanStart == 1.7f && GameCore.SignalFanLength == 4.5f,
                    "The original four finite fan lanes keep their start and travel range");
            }
        }
    }
}
