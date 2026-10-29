using System;
using System.Reflection;

namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        private static GameCore FishingRetryLake(Campaign data, bool success = false)
        {
            var game = Fresh(data); game.SkipTutorial();
            if (success) RiverLakeCatch(game);
            else
            {
                RiverVisit(game, "fish"); RiverChoose(game, "fish");
                game.Notify("原有剧情", "Original story");
                for (int pull = 0; pull < 3; pull++)
                {
                    game.FishHit();
                    if (pull < 2) Check(game.RecentFishingRetry == null && !game.CanRetryFishing && game.toast == game.L("原有剧情", "Original story"),
                        "A nonterminal lake miss preserves the story and cannot advertise a retry");
                }
            }
            FishingRetryCheckOffer(game, false, success, -1); return game;
        }
        private static FishingRetryOffer FishingRetryCheckOffer(GameCore game, bool river, bool success, int site)
        {
            FishingRetryOffer offer = game.RecentFishingRetry;
            Check(game.mode == "world" && offer != null && offer.river == river && offer.success == success && offer.site == site && game.CanRetryFishing,
                "Only the already-settled local attempt exposes its actual water, outcome and site");
            Check(game.RecentFishingPull == null && !game.RiverFishingActive,
                "A retry offer follows the original Close and clears the completed cast's pull feedback");
            return offer;
        }
        private static void FishingRetryRestart(GameCore game, bool river, int site)
        {
            string saved = TacticsState(game.save), toast = game.toast; int revision = game.revision;
            Check(game.TryRetryFishing() && game.mode == "fish" && game.fishTime == 0 && game.fishCursor == 0
                && game.fishHits == 0 && game.fishMisses == 0 && game.RecentFishingRetry == null && !game.CanRetryFishing
                && game.RecentFishingPull == null && game.RiverFishingActive == river,
                "One accepted retry consumes its offer before a clean zeroed cast begins");
            if (river) Check(game.RiverFishingSite == site && game.RiverPhase == RiverFishingPhase.Waiting
                && game.RiverPhaseRemaining == GameCore.RiverFishingWaitSeconds,
                "A river retry returns to its captured bank with the complete original wait");
            Check(TacticsState(game.save) == saved && game.toast == toast && game.revision == revision,
                "Restarting spends no supplies, advances no story and cannot replay the settled reward or overwrite its toast");
            string started = TacticsState(game);
            Check(!game.TryRetryFishing() && TacticsState(game) == started,
                "Repeated activation cannot restart the same consumed offer or disturb the new cast");
        }
        private static void FishingRetryRiverMiss(GameCore game, bool timeout)
        {
            RiverAwaitReeling(game); int misses = game.fishMisses;
            if (!timeout) game.FishHit();
            else
                for (int frame = 0; frame < 200 && game.mode == "fish" && game.fishMisses == misses; frame++) game.Tick(.01f, new Point());
            Check(game.fishMisses == misses + 1, "A real river pull or unpulled window accepts exactly one miss");
        }
        // Include every paid action and unpaid queue when checking that a
        // retry refusal cannot steal the existing player's next action.
        private static string FishingRetryProtectedState(GameCore game)
        {
            return TacticsState(new object[] { game.save, game.WorldGeneration, game.mode, game.revision,
                game.playerAction, game.companionAction, game.aim, game.aimTarget, game.companionAim,
                game.attackCooldown, game.skillCooldown, game.dashCooldown, game.dashTime, game.companionCooldown,
                game.playerHitTime, game.hitStop, game.deathTime, game.guardTime, game.invulnerable,
                game.fishTime, game.fishCursor, game.fishHits, game.fishMisses,
                game.conversation, game.toast, game.toastTime, game.sound, game.sounds,
                game.enemies, game.projectiles, game.effects, game.hazards,
                TacticsField(game, "bufferedAttack"), TacticsField(game, "bufferedSkill"),
                TacticsField(game, "bufferedDodge"), TacticsField(game, "bufferedCompanionCommand") });
        }

        public static void FishingRetryTerminalOutcomesPreserveRewardsAndRestartZeroed(Campaign data)
        {
            var lake = Fresh(data); lake.SkipTutorial(); lake.AcceptBounty(2);
            RiverVisit(lake, "fish"); RiverChoose(lake, "fish"); lake.Notify("原有剧情", "Original story");
            string unpaid = TacticsState(lake.save), story = lake.toast; int coins = lake.save.coins;
            for (int pull = 0; pull < 3; pull++)
            {
                FishingFeedbackLakeGood(lake);
                if (pull < 2) Check(lake.RecentFishingRetry == null && !lake.CanRetryFishing && lake.toast == story && TacticsState(lake.save) == unpaid,
                    "The first two real good lake pulls preserve the original story and award nothing");
            }
            FishingRetryOffer first = FishingRetryCheckOffer(lake, false, true, -1);
            Check(lake.save.inventory[3] == 1 && lake.save.coins == coins + 4 && lake.save.bountyProgress == 1
                && lake.toast == lake.L("钓到银鳍鱼 ×1！", "Caught a Silverfin!"),
                "The third original lake pull pays one fish, four coins and one bounty event before offering retry");
            FishingRetryRestart(lake, false, -1);
            for (int pull = 0; pull < 2; pull++) FishingFeedbackLakeGood(lake);
            Check(lake.save.inventory[3] == 1 && lake.save.coins == coins + 4 && lake.save.bountyProgress == 1,
                "A repeated cast still needs its own third good pull before any second payment");
            FishingFeedbackLakeGood(lake); FishingRetryCheckOffer(lake, false, true, -1);
            Check(lake.save.inventory[3] == 2 && lake.save.coins == coins + 8 && lake.save.bountyProgress == 2
                && !ReferenceEquals(first, lake.RecentFishingRetry), "A fresh completed retry pays exactly one fresh catch and creates a new immutable offer");
            lake.FishHit(); Check(lake.save.inventory[3] == 2, "A terminal extra pull cannot duplicate the paid catch");
            var failedLake = FishingRetryLake(data);
            Check(failedLake.save.inventory[3] == 0 && failedLake.fishMisses == 3
                && failedLake.toast == failedLake.L("鱼游走了。没有损失，再试一次吧。", "The fish slipped away. Nothing lost; try again."),
                "Three actual lake misses retain the original no-loss notification before retry");
            FishingRetryRestart(failedLake, false, -1);

            for (int site = 0; site < 2; site++) foreach (string outcome in new[] { "good", "miss", "timeout" })
            {
                var river = RiverGame(data, true); RiverEnter(river); river.AcceptBounty(2);
                int fish = river.save.inventory[3]; coins = river.save.coins; RiverBegin(river, site);
                river.Notify("原有河岸剧情", "Original river story"); unpaid = TacticsState(river.save); story = river.toast;
                for (int pull = 0; pull < 3; pull++)
                {
                    if (outcome == "good") RiverGoodPull(river); else FishingRetryRiverMiss(river, outcome == "timeout");
                    if (pull < 2) Check(river.RecentFishingRetry == null && river.toast == story && TacticsState(river.save) == unpaid,
                        "Nonterminal river pulls/timeouts preserve original story, supplies and notes");
                }
                bool success = outcome == "good"; FishingRetryCheckOffer(river, true, success, site);
                Check(river.save.inventory[3] == fish + (success ? 1 : 0) && river.save.coins == coins + (success ? 4 : 0)
                    && river.save.bountyProgress == (success ? 1 : 0) && river.RiverLessonRecorded(site) == success && !river.RiverLessonRecorded(1 - site)
                    && river.toast == (success ? river.L("河岸银鳍鱼 ×1！这一处已记录。", "River Silverfin x1! This site is recorded.")
                        : river.L("鱼游走了。记录与物资没有损失，再试一次吧。", "The fish slipped away. Notes and supplies are safe; try again.")),
                    "Each actual bank result retains its original reward, bounty, own-site note and final notification: " + outcome);
                FishingRetryRestart(river, true, site);
            }
        }

        public static void FishingRetryWorldLifetimeAndCanonicalLocalityRejectStaleOffers(Campaign data)
        {
            Check(GameCore.FishingRetryDuration == 8f, "A settled attempt offers exactly the authored eight-second world lifetime");
            var clock = FishingRetryLake(data); FishingRetryOffer offer = clock.RecentFishingRetry; string before = TacticsState(clock);
            foreach (float dt in new[] { 0f, -1f, float.NaN, float.PositiveInfinity, float.NegativeInfinity }) clock.Tick(dt, new Point());
            Check(TacticsState(clock) == before, "Invalid frame times neither age nor consume a valid retry offer");
            float time = clock.save.totalTime; clock.Tick(5, new Point()); Advance(clock, 7.90f);
            Check(ReferenceEquals(clock.RecentFishingRetry, offer) && clock.CanRetryFishing && Math.Abs(clock.save.totalTime - time - 7.95f) < .0003f,
                "The existing .05 frame clamp controls the retry lifetime while normal world time continues");
            Advance(clock, .06f);
            Check(clock.RecentFishingRetry == null && !clock.CanRetryFishing && !clock.TryRetryFishing() && clock.mode == "world" && clock.save.inventory[3] == 0,
                "Eight valid world seconds expire a retry without silently starting or paying another cast");
            var near = FishingRetryLake(data); near.save.x += .19f;
            Check(near.CanStand(near.Position) && near.CanRetryFishing && near.TryRetryFishing(), "The same legal approach may shift within the authored .2 origin radius");

            foreach (string boundary in new[] { "menu", "newcast", "load", "travel", "same-room", "newgame", "owner", "room", "generation", "remote", "nan", "wall", "health", "practice", "dying", "enemy", "projectile", "hazard", "removed", "replaced", "moved", "nearest", "path" })
            {
                var game = FishingRetryLake(data); WorldObject host = Object(game, "fish"); WorldObject[] authored = game.Map.objects;
                float hostX = host.x; string[] rows = game.Map.rows; int generation = game.WorldGeneration;
                try
                {
                    if (boundary == "menu") { game.mode = "inventory"; game.Close(); }
                    if (boundary == "newcast") { RiverVisit(game, "fish"); RiverChoose(game, "fish"); }
                    if (boundary == "load") game.Load(RiverSave(game.save));
                    if (boundary == "travel") { game.Enter(1); game.Enter(0); }
                    if (boundary == "same-room") game.Enter(0);
                    if (boundary == "newgame") { game.NewGame(0); game.Close(); }
                    if (boundary == "owner") game.save = RiverSave(game.save);
                    if (boundary == "room") game.save.room = 1;
                    if (boundary == "generation") typeof(GameCore).GetProperty("WorldGeneration").GetSetMethod(true).Invoke(game, new object[] { generation + 1 });
                    if (boundary == "remote") game.save.x += .21f;
                    if (boundary == "nan") game.save.x = float.NaN;
                    if (boundary == "wall") { game.save.x = 0; game.save.y = 0; }
                    if (boundary == "health") game.save.health = 0;
                    if (boundary == "practice") game.practicing = true;
                    if (boundary == "dying") game.deathTime = .5f;
                    if (boundary == "enemy") game.enemies.Add(new Enemy(99871, 0, game.save.x, game.save.y, 20));
                    if (boundary == "projectile") game.projectiles.Add(new Projectile { position = game.Position, friendly = false });
                    if (boundary == "hazard") game.hazards.Add(new Hazard { position = game.Position, timer = 1, radius = 1 });
                    if (boundary == "removed") game.Map.objects = Array.FindAll(authored, item => !ReferenceEquals(item, host));
                    if (boundary == "replaced") game.Map.objects = Array.ConvertAll(authored, item => ReferenceEquals(item, host) ? new WorldObject { id = item.id, kind = item.kind, x = item.x, y = item.y } : item);
                    if (boundary == "moved") host.x += .01f;
                    if (boundary == "nearest")
                    {
                        var closer = new WorldObject { id = "retry-nearer", kind = "rest", x = game.save.x, y = game.save.y };
                        var objects = new WorldObject[authored.Length + 1]; Array.Copy(authored, objects, authored.Length); objects[authored.Length] = closer; game.Map.objects = objects;
                        Check(ReferenceEquals(game.Nearest(), closer), "The nearest-object fixture genuinely changes E's owner");
                    }
                    if (boundary == "path")
                    {
                        // Recast from the west dock so the blocked endpoint is
                        // outside the actor's own footprint and .2 origin.
                        game.save.x = host.x - .7f; game.save.y = host.y + .3f;
                        Check(game.CanStand(game.Position) && ReferenceEquals(game.Nearest(), host), "The west-dock path fixture is a real legal canonical approach");
                        game.Interact(); RiverChoose(game, "fish"); game.FishHit(); game.FishHit(); game.FishHit();
                        FishingRetryCheckOffer(game, false, false, -1);
                        string[] blocked = (string[])rows.Clone(); int x = (int)Math.Floor(host.x), y = (int)Math.Floor(host.y);
                        char[] line = blocked[y].ToCharArray(); line[x] = '#'; blocked[y] = new string(line); game.Map.rows = blocked;
                        Check(game.CanStand(game.Position), "The blocked-host fixture leaves the player's own feet legal");
                    }
                    string protectedState = FishingRetryProtectedState(game), allState = TacticsState(game);
                    Check(game.RecentFishingRetry == null && !game.CanRetryFishing && !game.TryRetryFishing(),
                        "A retry rechecks its actual owner, world, canonical host, legal nearby approach and quiet living context: " + boundary);
                    Check(FishingRetryProtectedState(game) == protectedState && TacticsState(game) == allState,
                        "Rejected stale retry and invalid reads preserve every public/private simulation field and story state: " + boundary);
                }
                finally { host.x = hostX; data.maps[0].objects = authored; data.maps[0].rows = rows; }
            }
            var walked = FishingRetryLake(data); Point origin = walked.Position; walked.save.x += .21f; walked.Tick(.02f, new Point());
            walked.save.x = origin.x; walked.save.y = origin.y;
            Check(walked.RecentFishingRetry == null && !walked.TryRetryFishing(), "A frame spent outside the origin radius permanently retires the offer even after returning");
            var lockedRiver = RiverGame(data, true); RiverEnter(lockedRiver); RiverCatch(lockedRiver, 0); lockedRiver.save.flags.Remove("riverlesson_accept");
            Check(lockedRiver.RecentFishingRetry == null && !lockedRiver.TryRetryFishing(), "A river retry rechecks the lesson's live access rather than treating a saved bank as authority");
        }

        public static void FishingRetryRefusalsPreservePaidActionsAndQueuedRequests(Campaign data)
        {
            foreach (string context in new[] { "windup", "active", "recovery", "dash", "hitstop", "hitstun", "attack-queue", "skill-queue", "dodge-queue", "companion-queue", "stock" })
            {
                var game = FishingRetryLake(data); game.save.energy = 100;
                if (context == "windup" || context == "active" || context == "recovery")
                {
                    game.Attack(new Point(1, 0)); Check(game.playerAction != null, "The paid-action fixture really uses the original accepted basic attack");
                    if (context != "windup") { game.playerAction.released = true; game.playerAction.elapsed = game.playerAction.contact + (context == "recovery" ? .11f : 0); }
                    Check(game.PlayerPhase == (context == "windup" ? ActionPhase.Windup : context == "active" ? ActionPhase.Active : ActionPhase.Recovery),
                        "The retry refusal fixture covers the actual requested action phase: " + context);
                }
                if (context == "dash") { game.Dash(new Point(1, 0)); Check(game.dashTime > 0, "The existing dodge starts before retry is refused"); }
                if (context == "hitstop") game.hitStop = .1f;
                if (context == "hitstun") game.playerHitTime = .1f;
                if (context == "attack-queue") { game.attackCooldown = .08f; game.PressAttack(new Point(1, 0)); }
                if (context == "skill-queue") { game.skillCooldown = .08f; game.Skill(new Point(1, 0)); }
                if (context == "dodge-queue") { game.dashCooldown = .08f; game.Dash(new Point(1, 0)); }
                if (context == "companion-queue") { game.save.flags.Add("companion_1"); game.SelectCompanion(1); game.companionCooldown = .08f; game.CommandCompanion(); }
                string queued = context == "attack-queue" ? "bufferedAttack" : context == "skill-queue" ? "bufferedSkill" : context == "dodge-queue" ? "bufferedDodge" : context == "companion-queue" ? "bufferedCompanionCommand" : null;
                if (queued != null) Check(TacticsField(game, queued) != null && game.playerAction == null && game.dashTime == 0,
                    "A real public press captured only the unpaid request before the retry check: " + context);
                if (context == "stock") game.save.inventory[3] = 9999;
                string before = FishingRetryProtectedState(game), allState = TacticsState(game);
                for (int read = 0; read < 5; read++) Check(!game.CanRetryFishing, "An occupied action or full fish inventory never advertises retry: " + context);
                Check(!game.TryRetryFishing() && FishingRetryProtectedState(game) == before && TacticsState(game) == allState,
                    "A rejected retry does not spend energy, clear a request, cancel paid Recovery or alter its clock: " + context);
            }
            var friendly = FishingRetryLake(data);
            friendly.projectiles.Add(new Projectile { position = friendly.Position, friendly = true, life = 2 });
            friendly.effects.Add(new Effect("retry-existing-effect", friendly.Position, 1));
            string prepared = TacticsState(friendly.projectiles), effects = TacticsState(friendly.effects);
            Check(friendly.CanRetryFishing && friendly.TryRetryFishing() && TacticsState(friendly.projectiles) == prepared && TacticsState(friendly.effects) == effects,
                "An existing friendly missile and presentation effect neither block a safe retry nor get consumed by it");
        }

        public static void FishingRetryReadsAndLegacySavesRemainImmutableAndTransient(Campaign data)
        {
            Check(typeof(FishingRetryOffer).IsSealed, "A retry offer is a sealed immutable snapshot");
            foreach (FieldInfo field in typeof(FishingRetryOffer).GetFields(BindingFlags.Public | BindingFlags.Instance))
                Check(field.IsInitOnly, "Every public retry offer field is immutable: " + field.Name);
            foreach (FieldInfo field in typeof(SaveData).GetFields(BindingFlags.Public | BindingFlags.Instance))
                Check(field.FieldType != typeof(FishingRetryOffer) && field.Name.IndexOf("fishingretry", StringComparison.OrdinalIgnoreCase) < 0,
                    "The optional retry adds no durable field or schema requirement");
            var game = FishingRetryLake(data, true); FishingRetryOffer offer = game.RecentFishingRetry; string before = TacticsState(game);
            for (int read = 0; read < 30; read++) Check(ReferenceEquals(game.RecentFishingRetry, offer) && game.CanRetryFishing,
                "Repeated offer and availability reads do not consume or renew a valid attempt");
            Check(TacticsState(game) == before, "Retry reads preserve public/private simulation state, save data, RNG, queues, rewards and toast");
            game.PrepareSave(); SaveData checkpoint = RiverSave(game.save);
            Check(ReferenceEquals(game.RecentFishingRetry, offer) && game.CanRetryFishing,
                "The normal autosave snapshot retains the current local session without persisting its offer");
            foreach (int version in new[] { 1, 2 })
            {
                SaveData saved = RiverSave(checkpoint); saved.version = version; var loaded = new GameCore(data, saved); loaded.Start(); loaded.Close();
                Check(loaded.RecentFishingRetry == null && !loaded.CanRetryFishing && !loaded.TryRetryFishing()
                    && loaded.save.inventory[3] == checkpoint.inventory[3] && loaded.save.coins == checkpoint.coins,
                    "Legacy/current reconstruction retains only the already-paid catch and never restores or mints a retry: v" + version);
                loaded.PrepareSave(); string durable = TacticsState(loaded.save);
                var resumed = new GameCore(data, RiverSave(loaded.save)); resumed.Start(); resumed.Close(); resumed.PrepareSave();
                Check(TacticsState(resumed.save) == durable && resumed.RecentFishingRetry == null,
                    "A second round trip retains the existing save representation without transient retry fields: v" + version);
            }
            FishingRetryRestart(game, false, -1);
            Check(!offer.river && offer.success && offer.site == -1,
                "A previously returned offer keeps its immutable settled outcome after the live session restarts");
            var empty = Fresh(data); empty.SkipTutorial(); RiverStand(empty, "fish"); before = TacticsState(empty);
            Check(empty.RecentFishingRetry == null && !empty.CanRetryFishing && !empty.TryRetryFishing() && TacticsState(empty) == before,
                "Mere proximity to Old Tide never manufactures an offer or an automatic first cast");
        }
    }
}
