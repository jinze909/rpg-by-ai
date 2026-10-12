using System;
using System.Collections.Generic;

namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        private static void WatchChoice(GameCore g, string action)
        {
            Check(g.mode == "dialog" && g.conversation != null, "The watch choice belongs to a real conversation");
            int index = Array.FindIndex(g.conversation.choices, c => c.action == action);
            Check(index >= 0, "Missing watch dialogue choice " + action); g.Choose(index);
        }
        private static GameCore WatchAcceptedGame(Campaign data, int profession = 0, bool record = true)
        {
            var g = Fresh(data, profession); g.SkipTutorial();
            g.save.flags.Add("scout_saved"); g.save.flags.Add("core_1");
            WorldObject scout = Object(g, "returnscout"); g.save.x = scout.x; g.save.y = scout.y - 1;
            g.Interact(); WatchChoice(g, "watch_commission"); WatchChoice(g, "watch_accept");
            Check(g.WatchAccepted && g.save.trackedQuest == "watch", "The returned scout accepts and tracks the optional watch commission");
            g.Enter(1); g.enemies.Clear();
            if (record)
            {
                g.save.x = 7.5f; g.save.y = 19.5f; g.UseExplorationAbility();
                g.save.x = 4.5f; g.save.y = 19.5f; g.Talk(Object(g, "cache1")); g.Close();
                Check(g.Has("cache_1"), "The existing sealed cache supplies the patrol record through its original exploration interaction");
            }
            return g;
        }
        private static WorldObject WatchStart(GameCore g, int stage)
        {
            WorldObject signal = Object(g, "watchSignal" + stage);
            g.save.x = signal.x; g.save.y = signal.y - 1; g.Interact();
            WatchChoice(g, "watch_start_" + stage);
            Check(g.WatchActive(stage), "The ordinary beacon conversation starts the selected watch stage");
            return signal;
        }
        private static void WatchStand(GameCore g, WorldObject signal)
        { g.save.x = signal.x; g.save.y = signal.y - 1; }
        private static void WatchLightWithoutThreats(GameCore g, WorldObject signal)
        {
            WatchStand(g, signal); int stage = g.WatchStage;
            for (int frame = 0; frame < 1000 && g.WatchStage == stage; frame++) g.Tick(.02f, new Point());
            Check(g.WatchStage == stage + 1 && g.WatchLampLit(stage), "Twelve real uncontested seconds light exactly one beacon");
        }
        public static void LastWatchFullQuestAndOriginalStory(Campaign data)
        {
            for (int profession = 0; profession < 3; profession++)
            {
                var g = WatchAcceptedGame(data, profession); int originalStage = g.save.stage;
                for (int stage = 0; stage < 3; stage++)
                {
                    WorldObject signal = WatchStart(g, stage);
                    int expected = stage == 0 ? 2 : 3;
                    Check(g.enemies.FindAll(e => g.IsWatchEnemy(e)).Count == expected,
                        "Each watch stage starts its authored mixed patrol wave");
                    foreach (Enemy e in g.enemies) Check(!e.boss && !g.Blocked(e.x, e.y), "Optional patrol enemies have valid footing and no main Boss identity");
                    Combat(g, false); Check(!g.Dying && !g.Has("boss_1"), "Each starter class can win through actual attacks, techniques and dodges without main Boss credit");
                    WatchLightWithoutThreats(g, signal);
                    g.PrepareSave(); g = new GameCore(data, CloneSave(g.save)); g.Start();
                    Check(g.WatchStage == stage + 1 && g.WatchLampLit(stage), "Each beacon checkpoint survives reconstruction");
                }
                Check(!g.Has("watch_complete") && g.GetQuest("watch").room == 0,
                    "All three lamps lead back to the scout for the actual story handoff");
                g.Enter(0); WorldObject scout = Object(g, "returnscout"); g.save.x = scout.x; g.save.y = scout.y - 1;
                g.Interact(); WatchChoice(g, "watch_commission"); int coins = g.save.coins, shards = g.save.shards, points = g.save.skillPoints;
                WatchChoice(g, "watch_claim");
                Check(g.Has("watch_complete") && g.GetQuest("watch").complete && g.save.coins == coins + 40 && g.save.shards == shards + 4 && g.save.skillPoints == points + 1,
                    "The scout pays the complete optional patrol once: forty coins, four shards and one point");
                Check(g.save.stage == originalStage && g.Has("core_1") && g.Has("scout_saved") && !g.Has("boss_1"),
                    "The optional story preserves the campaign stage, original scout and core flags");
                g.PrepareSave(); var loaded = new GameCore(data, CloneSave(g.save)); loaded.Start();
                loaded.save.x = scout.x; loaded.save.y = scout.y - 1; loaded.Interact(); WatchChoice(loaded, "watch_commission");
                Check(Array.FindIndex(loaded.conversation.choices, c => c.action == "watch_claim") < 0 && loaded.save.coins == coins + 40 && loaded.save.shards == shards + 4,
                    "A saved completed story cannot offer or duplicate its final payment");
            }
        }
        public static void LastWatchClockPressureAndStageBoundaries(Campaign data)
        {
            var g = WatchAcceptedGame(data); WorldObject signal = WatchStart(g, 0);
            var owned = new List<Enemy>(g.enemies); foreach (Enemy e in owned) { e.x = 23; e.y = 16; e.timer = 9999; }
            WatchStand(g, signal); Advance(g, 3);
            Check(g.WatchProgress > .2f && g.WatchProgress < .3f, "The lantern charges from actual uncontested world time");
            float progress = g.WatchProgress, time = g.save.totalTime;
            foreach (string mode in new[] { "pause", "inventory", "journal", "dialog" })
            { g.mode = mode; g.Tick(.05f, new Point(1, 0)); Check(g.WatchProgress == progress && g.save.totalTime == time, "Menu modes freeze the patrol channel"); }
            g.Close(); g.hitStop = .1f; g.Tick(.05f, new Point());
            Check(g.WatchProgress == progress, "Hit stop freezes rather than advances or discards the current channel");
            g.hitStop = 0; g.Tick(0, new Point()); Check(g.WatchProgress == progress, "A zero-duration tick cannot alter watch progress");
            g.save.x = signal.x + 2; g.save.y = signal.y; g.Tick(.02f, new Point());
            Check(g.WatchProgress == 0, "Leaving the lantern's real channel radius discards accumulated progress");
            WatchStand(g, signal); Advance(g, 1); Check(g.WatchProgress > 0, "Returning restarts the channel");
            var ordinary = new Enemy(71, 1, signal.x + 2, signal.y - 1, 32) { timer = 9999 }; g.enemies.Add(ordinary); g.Tick(.02f, new Point());
            Check(g.WatchProgress == 0, "An ordinary world enemy also disrupts the lantern within three tiles");
            ordinary.x = 23; ordinary.y = 16; Advance(g, 1);
            g.invulnerable = 0; g.projectiles.Add(new Projectile { position = g.Position, damage = 1, source = "shot" }); g.Tick(.02f, new Point());
            Check(g.playerHitTime > 0 && g.WatchProgress == 0, "A real hostile contact interrupts the current channel in the same simulation step");
            Advance(g, .4f); WatchStand(g, signal); WatchLightWithoutThreats(g, signal);
            Check(g.enemies.Contains(ordinary) && !g.enemies.Exists(e => g.IsWatchEnemy(e)) && !g.Has("watch_defeated_72000"),
                "Area control retires only the owned wave, without inventing kills or deleting mature world encounters");
            int count = g.enemies.Count; g.Talk(signal); g.Close(); g.Tick(.02f, new Point());
            Check(g.WatchStage == 1 && g.enemies.Count == count, "A lit lantern cannot replay its wave or silently advance the next checkpoint");
        }
        public static void LastWatchSnapshotsTravelAndDefeatRecords(Campaign data)
        {
            var g = WatchAcceptedGame(data); WatchStart(g, 0);
            Enemy first = g.enemies.Find(e => e.id == 72000), survivor = g.enemies.Find(e => e.id == 72001);
            Check(first != null && survivor != null, "The first patrol uses its reserved stable enemy IDs");
            foreach (Enemy e in g.enemies) e.timer = 9999;
            g.save.x = first.x - 1; g.save.y = first.y; survivor.x = 20; survivor.y = 16;
            for (int frame = 0; frame < 200 && g.enemies.Contains(first); frame++)
            { g.Tick(.02f, new Point()); g.Attack(first.Position - g.Position); }
            Check(!g.enemies.Contains(first) && g.Has("watch_defeated_72000") && g.enemies.Contains(survivor),
                "Only actual lethal attack contact persists the patrol enemy's defeat");
            survivor.hp -= 3; float hp = survivor.hp; int coins = g.save.coins, kills = g.save.kills[1];
            g.PrepareSave(); var loaded = new GameCore(data, CloneSave(g.save)); loaded.Start();
            Check(loaded.WatchActive(0) && loaded.WatchStage == 0 && loaded.WatchProgress == 0,
                "Loading an unfinished patrol keeps its checkpoint but starts a fresh channel");
            Check(!loaded.enemies.Exists(e => e.id == 72000) && Math.Abs(loaded.enemies.Find(e => e.id == 72001).hp - hp) < .001f,
                "A same-room save retains surviving health and does not respawn a defeated patrol enemy");
            Enemy restored = loaded.enemies.Find(e => e.id == 72001);
            Check(restored.timer <= 2, "Reserved snapshot IDs cannot turn the restored patrol attack timer into hours of inactivity");
            loaded.save.x = restored.x - 1.5f; loaded.save.y = restored.y; bool released = false;
            for (int frame = 0; frame < 100 && !released; frame++)
            { loaded.Tick(.02f, new Point()); released = restored.releaseTime > 0; }
            Check(released, "A surviving loaded patrol hunter releases a real telegraphed attack within two seconds");
            var bossLoad = new GameCore(data, CloneSave(g.save)); bossLoad.Start(); bossLoad.save.x = 28; bossLoad.save.y = 10;
            Action(bossLoad, "shrine", "boss"); Enemy guardian = bossLoad.enemies.Find(e => e.boss);
            Check(guardian != null && !bossLoad.IsWatchEnemyId(guardian.id), "Loading reserved patrol IDs cannot push the main guardian allocator into the patrol ID range");
            loaded.Enter(0); loaded.Enter(1);
            Check(loaded.WatchActive(0) && !loaded.enemies.Exists(e => e.id == 72000) && loaded.enemies.Exists(e => e.id == 72001),
                "Travel reconstructs living patrol members only and never counts an empty old room as completion");
            Check(loaded.save.coins == coins && loaded.save.kills[1] == kills && !loaded.Has("watch_signal_0"),
                "Encounter reconstruction neither farms rewards nor advances the unfinished lamp");
            // Old snapshots can be empty because a player had cleared the forest before accepting this new story.
            var oldEmpty = CloneSave(loaded.save); oldEmpty.snapshotRoom = 1; oldEmpty.worldEnemies = new EnemyRecord[0];
            var fromEmpty = new GameCore(data, oldEmpty); fromEmpty.Start();
            Check(fromEmpty.enemies.Exists(e => e.id == 72001) && fromEmpty.WatchStage == 0,
                "An existing empty world snapshot cannot erase a newly active patrol or light its checkpoint");
            foreach (Enemy e in fromEmpty.enemies) Check(e.id != 72000, "Persisted defeat is respected after an empty snapshot as well");
        }
        public static void LastWatchCompatibilityReadinessAndReachability(Campaign data)
        {
            foreach (int version in new[] { 1, 2 })
            {
                var saved = new SaveData { version = version, stage = 1, coins = 101, flags = new List<string> { "intro", "scout_saved", "core_1", "boss_1", "cache_1", "scout_gift" } };
                var g = new GameCore(data, saved); g.Start();
                Check(!g.WatchAccepted && !g.Has("watch_complete") && g.save.coins == 101 && g.Has("boss_1") && g.Has("cache_1") && g.Has("scout_gift"),
                    "Old saves retain all mature progress and never autoaccept or autopay the new commission");
                g.Enter(1); Check(!g.enemies.Exists(e => g.IsWatchEnemy(e)), "Merely loading or visiting an old forest does not start a patrol");
                foreach (WorldObject o in g.Map.objects) if (o.kind.StartsWith("watchSignal"))
                {
                    Check(g.IsObjectPresent(o), "Authored watch lanterns remain real world objects");
                    g.save.x = g.Map.spawnX; g.save.y = g.Map.spawnY;
                    var route = g.NavigationPath(new Point(o.x, o.y));
                    Check(route.Count > 0 && (route[route.Count - 1] - new Point(o.x, o.y)).Length <= 1.8f,
                        "Every lantern is reachable by the hero footprint from the real forest entrance");
                    foreach (Point p in route) Check(!g.Blocked(p.x - .22f, p.y - .18f) && !g.Blocked(p.x + .22f, p.y + .18f), "The optional patrol route respects existing forest collision");
                }
            }
            var locked = Fresh(data); Check(locked.GetQuest("watch").locked && !locked.WatchAccepted,
                "A new character must first resolve the original scout and first-core story");
            locked.save.flags.Add("scout_saved"); locked.Enter(0); WorldObject scout = Object(locked, "returnscout"); locked.Talk(scout);
            Check(locked.conversation.choices[0].action == "scoutgift", "The original scout gift remains the first interaction");
            int stew = locked.save.inventory[6]; locked.Choose(0); Check(locked.save.inventory[6] == stew + 1 && locked.Has("scout_gift"), "The original homecoming gift is intact");
            locked.Talk(scout); Check(Array.FindIndex(locked.conversation.choices, c => c.action == "watch_commission") < 0,
                "The new commission does not bypass the first-core prerequisite");
            var noRecord = WatchAcceptedGame(data, 0, false); WorldObject lantern = Object(noRecord, "watchSignal0"); noRecord.save.x = lantern.x; noRecord.save.y = lantern.y - 1; noRecord.Interact();
            Check(noRecord.conversation == null || Array.FindIndex(noRecord.conversation.choices, c => c.action == "watch_start_0") < 0,
                "A patrol cannot skip recovering the original sealed road record");
            var ready = WatchAcceptedGame(data); WorldObject second = Object(ready, "watchSignal1"); ready.save.x = second.x; ready.save.y = second.y - 1; ready.Interact();
            Check(ready.conversation == null || Array.FindIndex(ready.conversation.choices, c => c.action == "watch_start_1") < 0,
                "Lanterns must be relit in the authored order rather than skipping checkpoints");
        }
        public static void LastWatchDeathCannotCompleteOrPay(Campaign data)
        {
            var g = WatchAcceptedGame(data); WorldObject signal = WatchStart(g, 0);
            foreach (Enemy e in g.enemies) { e.x = 23; e.y = 16; e.timer = 9999; }
            WatchStand(g, signal); Advance(g, 11.98f);
            Check(g.WatchProgress > .99f && g.WatchStage == 0, "The lethal-contact fixture reaches the last unfinished channel step");
            g.invulnerable = 0; g.save.health = 1; int shards = g.save.shards, points = g.save.skillPoints;
            g.projectiles.Add(new Projectile { position = g.Position, damage = 999, source = "shot" }); g.Tick(.05f, new Point());
            Check(g.Dying && !g.Has("watch_signal_0") && !g.Has("watch_complete"),
                "Lethal contact wins the last channel step and cannot award a lantern or quest reward");
            g.PrepareSave(); var pending = new GameCore(data, CloneSave(g.save)); pending.Start();
            Check(pending.save.room == 0 && pending.WatchStage == 0 && pending.WatchAccepted && !pending.Has("watch_complete") && pending.save.shards == shards && pending.save.skillPoints == points,
                "A save during the fallen pose recovers safely without discarding patrol acceptance or granting completion");
            Advance(g, 1); Check(g.save.room == 0 && g.WatchStage == 0 && !g.Has("watch_signal_0"), "Normal death recovery retains the unfinished patrol checkpoint");
            g.Enter(1); Check(g.enemies.FindAll(e => g.IsWatchEnemy(e)).Count == 2 && g.WatchProgress == 0,
                "Returning after death restores the uncompleted patrol with a fresh channel");
        }
    }
}
