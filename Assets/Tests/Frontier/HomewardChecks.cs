using System;
using System.Collections.Generic;

namespace Starfall.Tests
{
    // These checks exercise shipped Core actions and simulation offline.
    // They do not certify native Unity playback or companion game feel.
    public static partial class CoreChecks
    {
        private static GameCore HomewardGame(Campaign data, int profession = 0, bool accepted = true)
        {
            var g = Fresh(data, profession); g.SkipTutorial(); g.save.stage = 1;
            foreach (string flag in new[] { "core_1", "exp_intro", "harbor_report", "boss_5", "gate_5", "companion_0", "companion_1", "personal_0", "manifest_found", "reach_complete", "reach_post_0", "reach_post_1", "reach_post_2" })
                g.save.flags.Add(flag);
            // A completed survey is the mature prerequisite, not a new-wave
            // defeat. Isolate its cleared old patrols from the new encounters.
            for (int i = 0; i < 7; i++) g.save.flags.Add("reach_defeated_" + (76000 + i));
            g.save.relationships[0] = 3; g.Enter(5); g.enemies.Clear();
            g.SelectCompanion(1); ReachStand(g, "captain");
            if (accepted)
            {
                Check(g.AcceptHomewardRoute(), "The earned captain conversation can accept Mira's next route");
                Check(g.save.companion == 1, "Acceptance never silently replaces the selected companion");
                g.SelectCompanion(0); g.Enter(GameCore.ReachRoom);
            }
            return g;
        }
        private static void HomewardWaitForMira(GameCore g, WorldObject obj)
        {
            int frames = 0;
            // The witness stands near Viola, as the follower always has. The
            // hero may be 1.6 tiles from a lamp while Mira trails another 1.9;
            // requiring Mira to stand on the prop would never converge there.
            while ((g.companionPosition - g.Position).Length > 2.85f && frames++ < 12000)
                g.Tick(.02f, new Point());
            Check(g.save.companion == 0 && frames < 12000 && g.CanStand(g.companionPosition) &&
                (g.companionPosition-g.Position).Length <= 3 && (g.Position-new Point(obj.x,obj.y)).Length < 1.75f,
                "The real Mira follower reaches the route object on valid ground: object="+obj.kind+" @"+obj.x+","+obj.y+
                " player="+g.save.x+","+g.save.y+" Mira="+g.companionPosition.x+","+g.companionPosition.y+
                " playerGap="+(g.companionPosition-g.Position).Length+" objectGap="+(g.companionPosition-new Point(obj.x,obj.y)).Length+" frames="+frames);
        }
        private static WorldObject HomewardStand(GameCore g, string kind)
        {
            WorldObject obj = ReachStand(g, kind); HomewardWaitForMira(g, obj); return obj;
        }
        private static int HomewardWalkTo(GameCore g, string kind)
        {
            g.Close(); WorldObject obj = Object(g, kind); Point target = new Point(obj.x, obj.y);
            List<Point> path = g.NavigationPath(target); int frames = 0;
            Check(path.Count > 0 || (g.Position-target).Length < 1.75f, "The route stop has a genuine navigable approach");
            while ((g.Position-target).Length >= 1.6f && frames++ < 15000)
            {
                while (path.Count > 0 && (path[0]-g.Position).Length < .15f) path.RemoveAt(0);
                Point movement = path.Count > 0 ? (path[0]-g.Position).Normalized : g.DirectionTo(g.Position,target);
                Point before = g.Position; g.Tick(.02f,movement);
                Check(g.save.room == GameCore.ReachRoom && !g.Dying && g.CanStand(g.Position),
                    "Actual walking remains in the owned region and never clips its terrain");
                Check((g.Position-before).Length < .8f, "The journey uses continuous movement, not a catch-up teleport");
            }
            Check(frames < 15000 && (g.Position-target).Length < 1.75f, "Actual walking reaches the selected local object");
            HomewardWaitForMira(g,obj); return frames;
        }
        private static void HomewardWave(GameCore g, int stage)
        {
            HomewardStand(g,"reachPost" + (stage+1));
            Check(g.StartHomewardStop(stage), "A nearby, selected Mira starts the next deliberate encounter");
            Check(g.enemies.Count == 2 && !g.HomewardStopComplete(stage), "Starting a wave neither completes a stop nor deletes its two real foes");
            ReachCombatRoutes(g); Advance(g,.6f); HomewardStand(g,"reachPost" + (stage+1));
            Check(g.RecordHomewardStop(stage), "The two real defeats permit a manual route observation with Mira");
        }
        private static void HomewardCompletedFixture(GameCore g)
        {
            // Transaction controls intentionally start at an already earned
            // two-stop checkpoint; genuine wave defeats are checked separately.
            foreach (string flag in new[] { "homeward_wave_0", "homeward_wave_1", "homeward_stop_0", "homeward_stop_1", "homeward_defeated_77000", "homeward_defeated_77001", "homeward_defeated_77002", "homeward_defeated_77003", "history_20", "history_21" })
                if (!g.Has(flag)) g.save.flags.Add(flag);
            g.enemies.RemoveAll(e => e.id >= 77000 && e.id <= 77003);
        }

        public static void HomewardEarnedAcceptanceKeepsMaturePersonalStoryAndSelection(Campaign data)
        {
            var fresh = Fresh(data); Check(!fresh.HomewardReady && !fresh.HomewardAccepted && fresh.GetQuest("homeward").locked,
                "A new hero cannot begin a follow-up to unearned companion and survey stories");
            foreach (string missing in new[] { "personal_0", "companion_0", "reach_complete", "core_1" })
            {
                var locked = HomewardGame(data,0,false); locked.save.flags.Remove(missing);
                int coins = locked.save.coins, shards = locked.save.shards, relationship = locked.save.relationships[0];
                Check(!locked.HomewardReady && !locked.AcceptHomewardRoute() && !locked.HomewardAccepted &&
                    locked.save.coins == coins && locked.save.shards == shards && locked.save.relationships[0] == relationship && locked.save.companion == 1,
                    "Missing earned prerequisite rejects acceptance atomically: " + missing);
            }
            var g = HomewardGame(data,0,false); int money = g.save.coins, fragments = g.save.shards, points = g.save.skillPoints;
            g.mode="pause"; Check(!g.AcceptHomewardRoute(), "Paused acceptance cannot start a world quest"); g.Close();
            g.practicing=true; Check(!g.AcceptHomewardRoute(), "Training cannot accept a real-world companion route"); g.practicing=false;
            g.deathTime=.3f; Check(!g.AcceptHomewardRoute(), "A defeated hero cannot accept a future route"); g.deathTime=0;
            g.save.x=5; g.save.y=14; Check(!g.AcceptHomewardRoute(), "An old offer cannot be accepted after leaving the actual captain");
            ReachStand(g,"captain"); g.CompanionStory(0);
            Check(g.conversation != null && Array.FindIndex(g.conversation.choices,c => c.action == "homeward_offer" || c.action == "homeward_accept") < 0,
                "The synthetic anywhere companion story does not expose a remote captain acceptance"); g.Close();
            g.Interact();
            Check(g.conversation != null && g.conversation.choices.Length >= 4 &&
                g.conversation.choices[0].action == "gift0" && g.conversation.choices[1].action == "join0" && g.conversation.choices[2].action == "close",
                "Completed Mira gift, explicit invitation and farewell retain their mature positions before the new route");
            Check(g.conversation.textEn.Contains("new route") && !g.conversation.textEn.Contains("brother returned"),
                "The continuation retains Mira's truthful earned personal-story ending");
            g.Close(); Check(g.AcceptHomewardRoute() && g.HomewardAccepted && g.HomewardReady && g.save.companion == 1,
                "Accepting at the real captain starts only the new optional quest and leaves Ivo selected");
            Check(!g.AcceptHomewardRoute() && g.save.coins == money && g.save.shards == fragments && g.save.skillPoints == points && g.Has("personal_0") && g.Has("reach_complete"),
                "Repeated acceptance neither grants old/new rewards nor rewrites mature story completion");
            g.SelectCompanion(0); Check(g.save.companion == 0, "The existing explicit invitation still selects Mira after acceptance");
        }

        public static void HomewardLocalStartsRequireOrderedStopsAndActualMira(Campaign data)
        {
            var g = HomewardGame(data); int coins = g.save.coins, shards = g.save.shards;
            Check(!g.StartHomewardStop(-1) && !g.StartHomewardStop(2) && !g.RecordHomewardStop(-1) && !g.RecordHomewardStop(2) && !g.FinishHomewardRoute(true),
                "Invalid indices and premature handoffs cannot advance the route");
            HomewardStand(g,"reachPost2"); Check(!g.StartHomewardStop(1), "The eastern route cannot bypass the first river-island observation");
            HomewardStand(g,"reachPost1"); g.SelectCompanion(1);
            Check(!g.StartHomewardStop(0), "A selected different companion cannot impersonate Mira");
            g.SelectCompanion(-1); Check(!g.StartHomewardStop(0), "A dismissed companion cannot witness the route stop");
            g.SelectCompanion(0); Point actual = g.companionPosition; g.companionPosition = new Point(6,39);
            Check(!g.StartHomewardStop(0), "A selected but physically distant Mira cannot start the encounter"); g.companionPosition = actual;
            g.mode = "pause"; Check(!g.StartHomewardStop(0), "A paused callback cannot start a world encounter"); g.Close();
            g.practicing = true; Check(!g.StartHomewardStop(0), "Training isolation rejects new world encounters"); g.practicing = false;
            g.deathTime = .4f; Check(!g.StartHomewardStop(0), "Defeat cannot start a new optional encounter"); g.deathTime = 0;
            g.enemies.Add(new Enemy(18,0,39,29,40)); Check(!g.StartHomewardStop(0), "An unrelated nearby threat must clear before the new encounter"); g.enemies.Clear();
            g.save.x = 8; g.save.y = 38; Check(!g.StartHomewardStop(0), "A callback captured at a stop rechecks the current hero location");
            HomewardStand(g,"reachPost1"); Check(g.StartHomewardStop(0), "The ready nearby duo starts the authored first wave");
            Check(!g.StartHomewardStop(0) && g.enemies.Count == 2 && !g.StartHomewardStop(1),
                "Repeated and out-of-order start calls do not duplicate actors or skip the unresolved wave");
            Check(g.save.coins == coins && g.save.shards == shards && !g.HomewardStopComplete(0), "All start/guard checks preserve the economy and manual progress");
        }

        public static void HomewardObservationsRequireRealDefeatsAndWitnessedHandoff(Campaign data)
        {
            var g = HomewardGame(data); HomewardStand(g,"reachPost1");
            Check(!g.RecordHomewardStop(0), "A quiet old surveyed lamp is not an unstarted new route observation");
            Check(g.StartHomewardStop(0) && !g.RecordHomewardStop(0), "Beginning the new wave cannot bypass its living foes");
            g.enemies.Clear(); Check(!g.RecordHomewardStop(0), "An empty enemy list without genuine defeat records cannot grant the stop");
            g.PrepareSave(); g.Load(CloneSave(g.save)); g.Start();
            Check(g.enemies.Count == 2, "A missing unresolved wave rebuilds its canonical foes instead of granting an empty victory");
            ReachCombatRoutes(g); Advance(g,.6f); HomewardStand(g,"reachPost1");
            Check(g.Has("homeward_defeated_77000") && g.Has("homeward_defeated_77001") && !g.HomewardStopComplete(0),
                "Actual ordinary combat records both defeats without auto-completing the story beat");
            g.SelectCompanion(1); Check(!g.RecordHomewardStop(0), "Defeated foes do not waive Mira's handoff presence");
            g.SelectCompanion(0); Point witness = g.companionPosition; g.companionPosition = new Point(6,39);
            Check(!g.RecordHomewardStop(0), "A stale completion cannot use a companion who has left the lamp"); g.companionPosition = witness;
            g.save.x = 8; g.save.y = 38; Check(!g.RecordHomewardStop(0), "A stale completion cannot be confirmed from the board");
            HomewardStand(g,"reachPost1"); Check(g.RecordHomewardStop(0) && !g.RecordHomewardStop(0) && g.HomewardStopComplete(0),
                "The actual quiet duo manually records the earned first stop once");
            g.Enter(0); Check(!g.RecordHomewardStop(1) && !g.StartHomewardStop(1) && !g.FinishHomewardRoute(false),
                "Distant room callbacks cannot operate the riverbank story");
        }

        public static void HomewardEveryStarterWalksWithMiraAndWinsOrdinaryEncounters(Campaign data)
        {
            for (int profession = 0; profession < 3; profession++)
            {
                var g = HomewardGame(data,profession); g.AcceptBounty(0);
                int coins = g.save.coins, shards = g.save.shards, kills = g.save.kills[11], bond = g.save.relationships[0];
                int[] inventory = (int[])g.save.inventory.Clone(); float clock = g.save.totalTime; Point companion = g.companionPosition;
                int frames = HomewardWalkTo(g,"reachPost1");
                Check((g.companionPosition-companion).Length > 10 && g.companionMoveTime > 0,
                    "Mira actually walks the long route through the mature follower, not a staged extra actor");
                for (int stage = 0; stage < 2; stage++)
                {
                    if (stage == 1) frames += HomewardWalkTo(g,"reachPost2");
                    Check(g.StartHomewardStop(stage), "Actual arrival with Mira starts the next required wave");
                    int first = 77000+stage*2; int secondKind = stage == 0 ? 1 : 2;
                    Check(g.enemies.Count == 2 && g.enemies.Exists(e => e.id == first && e.kind == 0 && e.maxHp == 55 && !e.boss && g.CanStand(e.Position)) &&
                        g.enemies.Exists(e => e.id == first+1 && e.kind == secondKind && e.maxHp == (stage == 0 ? 44 : 85) && !e.boss && g.CanStand(e.Position)),
                        "Each starter fights the same canonical normal hunter/support pair on valid map ground");
                    frames += ReachCombatRoutes(g); Advance(g,.6f); HomewardWalkTo(g,"reachPost"+(stage+1));
                    Check(g.RecordHomewardStop(stage), "The surviving real follower witnesses each manual clear observation");
                }
                Check(g.save.kills[11] == kills+4 && g.save.coins == coins+24 && g.save.shards == shards && g.save.bountyProgress == 4,
                    "Actual normal wave combat keeps its four existing coin payouts and contract counts, before any record or story reward");
                Check(!g.HomewardRecordsFound && g.HomewardRecordCount == 0, "Completed new encounters never invent discoveries of the two old records");
                for (int note = 20; note <= 21; note++)
                {
                    frames += HomewardWalkTo(g,"history"+note); int beforeCoins=g.save.coins, beforeShards=g.save.shards;
                    g.Talk(Object(g,"history"+note));
                    Check(g.mode == "dialog" && g.Has("history_"+note) && g.save.coins == beforeCoins+10 && g.save.shards == beforeShards+2,
                        "Actual local old-record discovery keeps its mature ten-coin/two-shard payout independently of the new copies");
                    g.Close(); g.Talk(Object(g,"history"+note)); g.Close();
                    Check(g.save.coins == beforeCoins+10 && g.save.shards == beforeShards+2, "Rereading an old record never repeats its discovery reward");
                }
                Check(g.HomewardRecordsFound && g.HomewardRecordCount == 2, "Both actual discoveries satisfy the truthful archive-copy requirement");
                frames += HomewardWalkTo(g,"campfire");
                Check(g.save.room == 11 && !g.Dying && g.save.deaths == 0 && g.save.kills[11] == kills+4 && g.save.coins == coins+44 && g.save.shards == shards+4 && g.save.bountyProgress == 4,
                    "Real Attack/Q/dodge/meal and Mira support retain four normal rewards plus both mature record discoveries");
                Check(g.save.inventory[1] == inventory[1]+1 && g.save.inventory[2] == inventory[2]+2 && g.save.inventory[4] == inventory[4]+1 && g.save.totalTime > clock,
                    "Ordinary wave kills keep their material loot and the journey advances the normal world clock");
                bool publish = profession != 1;
                Check(g.FinishHomewardRoute(publish) && g.HomewardComplete && g.HomewardPublicChart == publish &&
                    g.save.coins == coins+74 && g.save.shards == shards+7 && g.save.relationships[0] == Math.Min(5,bond+1),
                    "The actual two-stop journey ends with the advertised independent once-only reward and chosen outcome");
                Check(!g.Has("boss_11") && !g.Has("core_2") && g.save.stage == 1 && g.Has("personal_0") && g.Has("reach_complete"),
                    "The follow-up cannot impersonate a Boss, deliver the next starcore or replace old story completion");
                g.Enter(1); g.Enter(11);
                Check(g.enemies.Count == 0 && g.save.coins == coins+74 && g.save.shards == shards+7 && g.save.kills[11] == kills+4 && !g.FinishHomewardRoute(!publish),
                    "Completed travel never respawns four paid guards or permits an opposite-choice payout");
                Console.WriteLine("  Homeward starter journey: profession="+profession+", frames="+frames+", health="+g.save.health+", clock="+g.save.totalTime+"; NATIVE_UNVERIFIED");
            }
        }

        public static void HomewardSnapshotsKeepCanonicalSurvivorsAndRejectForeignRecords(Campaign data)
        {
            var g = HomewardGame(data); HomewardStand(g,"reachPost1"); Check(g.StartHomewardStop(0), "The snapshot fixture starts its real guarded wave");
            Enemy hunter = g.enemies.Find(e => e.id == 77000); g.save.x = hunter.x; g.save.y = hunter.y+.8f;
            float health = hunter.hp; g.Attack(hunter.Position-g.Position);
            for (int frame = 0; frame < 80 && hunter.hp == health; frame++) g.Tick(.01f,new Point());
            Check(hunter.hp > 0 && hunter.hp < health, "Snapshot health comes from a real accepted attack instead of test HP replacement");
            g.PrepareSave(); SaveData checkpoint = CloneSave(g.save); var loaded = new GameCore(data,CloneSave(checkpoint)); loaded.Start();
            Check(loaded.enemies.Count == 2 && Math.Abs(loaded.enemies.Find(e => e.id == 77000).hp-hunter.hp) < .001f && !loaded.HomewardStopComplete(0),
                "Same-room saved combat retains canonical survivor HP while requiring a fresh safe attack preparation");
            SaveData malformed = CloneSave(checkpoint); malformed.worldEnemies = new[] {
                new EnemyRecord { id=77000,kind=0,x=37,y=32,hp=19,maxHp=5000 },
                new EnemyRecord { id=77000,kind=0,x=37,y=32,hp=999,maxHp=999 },
                new EnemyRecord { id=77001,kind=2,x=42,y=28,hp=20,maxHp=999 },
                new EnemyRecord { id=77002,kind=0,x=62,y=15,hp=18,maxHp=55 },
                new EnemyRecord { id=333,kind=3,boss=true,x=37,y=32,hp=90,maxHp=90 },
                new EnemyRecord { id=74000,kind=0,x=37,y=32,hp=20,maxHp=40 }
            };
            loaded = new GameCore(data,malformed); loaded.Start();
            Check(loaded.enemies.Count == 2 && loaded.enemies.TrueForAll(e => e.id == 77000 || e.id == 77001) &&
                loaded.enemies.Find(e => e.id == 77000).hp == 19 && loaded.enemies.Find(e => e.id == 77000).maxHp == 55 &&
                loaded.enemies.Find(e => e.id == 77001).kind == 1 && loaded.enemies.Find(e => e.id == 77001).hp == 44,
                "Duplicate, wrong-kind, future-wave and foreign records cannot rewrite canonical encounter identity or maximum health");
            foreach (EnemyRecord invalid in new[] {
                new EnemyRecord { id=77000,kind=0,boss=true,x=37,y=32,hp=19,maxHp=55 },
                new EnemyRecord { id=77000,kind=0,x=float.NaN,y=32,hp=19,maxHp=55 },
                new EnemyRecord { id=77000,kind=0,x=0,y=0,hp=19,maxHp=55 },
                new EnemyRecord { id=77000,kind=0,x=37,y=32,hp=float.PositiveInfinity,maxHp=55 }
            })
            {
                SaveData rejected = CloneSave(checkpoint); rejected.worldEnemies = new[] { invalid };
                loaded = new GameCore(data,rejected); loaded.Start();
                Check(loaded.enemies.Count == 2 && loaded.enemies.Exists(e => e.id == 77000 && !e.boss && e.kind == 0 && e.hp == 55 && loaded.CanStand(e.Position)),
                    "Malformed owned records rebuild the unresolved canonical actor instead of accepting foreign state");
            }
            SaveData inactive = CloneSave(checkpoint); inactive.flags.Remove("homeward_wave_0");
            loaded = new GameCore(data,inactive); loaded.Start(); Check(loaded.enemies.Count == 0, "Unstarted optional waves cannot be restored from forged snapshot IDs");
            SaveData preserveHidden = CloneSave(checkpoint); preserveHidden.flags.Remove("reach_defeated_76006");
            loaded = new GameCore(data,preserveHidden); loaded.Start();
            Check(loaded.enemies.Count == 3 && loaded.enemies.Exists(e => e.id == 76006) && loaded.enemies.Exists(e => e.id == 77000),
                "New wave recovery preserves the unrelated mature hidden-cache guard");
        }

        public static void HomewardTravelAndActualDefeatKeepUnresolvedOwnership(Campaign data)
        {
            var g = HomewardGame(data); HomewardWave(g,0); HomewardStand(g,"reachPost2"); Check(g.StartHomewardStop(1), "The earned second encounter starts before boundary checks");
            int coins = g.save.coins, kills = g.save.kills[11]; g.PrepareSave(); SaveData checkpoint = CloneSave(g.save);
            g.Enter(1); g.Enter(11);
            Check(g.enemies.Count == 2 && g.enemies.TrueForAll(e => e.id >= 77002 && e.id <= 77003 && e.hp == e.maxHp) &&
                g.HomewardStopComplete(0) && !g.HomewardStopComplete(1) && g.save.coins == coins && g.save.kills[11] == kills,
                "Travel rebuilds only the already-started unresolved second wave without repaying the first");
            g = new GameCore(data,checkpoint); g.Start(); g.Close(); g.enemies.Clear(); g.invulnerable = 0;
            g.projectiles.Add(new Projectile { position=g.Position,source="shot",damage=999 }); g.Tick(.02f,new Point()); Advance(g,2);
            Check(g.save.room == 0 && !g.Dying && g.save.deaths == 1 && g.HomewardAccepted && g.HomewardStopComplete(0) && !g.HomewardComplete,
                "A genuine lethal contact recovers in town and retains earned optional story progress");
            int recovered = g.save.coins; g.Enter(11);
            Check(g.enemies.Count == 2 && g.enemies.TrueForAll(e => e.id >= 77002 && e.id <= 77003) && g.save.coins == recovered && g.save.kills[11] == kills,
                "Real defeat cannot resurrect paid first-wave foes, award second-wave loot or skip its manual record");
            g.PrepareSave(); SaveData partial = CloneSave(g.save); partial.flags.Add("homeward_defeated_77002");
            g = new GameCore(data,partial); g.Start();
            Check(g.enemies.Count == 1 && g.enemies[0].id == 77003 && g.enemies[0].kind == 2 && g.save.coins == recovered,
                "A saved owned defeat flag prevents resurrection while leaving exactly its canonical survivor");
        }

        public static void HomewardLegacySavesAndForeignWorldsPreserveMatureState(Campaign data)
        {
            foreach (int version in new[] { 1,2 })
            {
                var old = new SaveData { version=version,room=0,coins=87,shards=9,stage=2,trackedQuest="personal0",presentationVersion=1,
                    flags=new List<string> { "intro","core_1","exp_intro","harbor_report","companion_0","personal_0","reach_complete","legacy_homeward_sentinel" } };
                var loaded = new GameCore(data,old); loaded.Start();
                Check(loaded.HomewardReady && !loaded.HomewardAccepted && !loaded.HomewardComplete && loaded.save.coins == 87 && loaded.save.shards == 9 &&
                    loaded.Has("legacy_homeward_sentinel") && loaded.save.trackedQuest == "personal0" && loaded.save.companion == -1,
                    "Both supported old saves retain their story/economy and offer, rather than auto-start, the new optional route");
            }
            var route = HomewardGame(data); HomewardStand(route,"reachPost1"); Check(route.StartHomewardStop(0), "Foreign-ownership fixture starts its real route wave"); route.PrepareSave();
            SaveData forest = CloneSave(route.save); forest.room=forest.snapshotRoom=1; forest.x=15; forest.y=14;
            forest.worldEnemies = new[] { new EnemyRecord { id=77000,kind=0,x=15,y=14,hp=20,maxHp=55 }, new EnemyRecord { id=17,kind=0,x=15,y=14,hp=20,maxHp=40 } };
            var loadedForest = new GameCore(data,forest); loadedForest.Start();
            Check(loadedForest.enemies.Count == 1 && loadedForest.enemies[0].id == 17, "Homeward reserved actors cannot leak into a mature map snapshot");
            loadedForest.save.flags.Add("scout_saved"); loadedForest.save.x=28; loadedForest.save.y=10; AwaitArenaOpening(loadedForest); Action(loadedForest,"shrine","boss");
            Check(loadedForest.enemies.Exists(e => e.boss && e.id == 18), "Rejected optional IDs cannot inflate the mature guardian allocator");
            var abyss = Unlocked(data); abyss.StartAbyss(0); SaveData expedition = CloneSave(abyss.save); expedition.abyssSnapshotFloor=expedition.abyssFloor;
            expedition.abyssEnemies = new[] { new EnemyRecord { id=77000,kind=0,x=15,y=14,hp=20,maxHp=55 }, new EnemyRecord { id=17,kind=0,x=15,y=14,hp=20,maxHp=40 } };
            var loadedAbyss = new GameCore(data,expedition); loadedAbyss.Start();
            Check(loadedAbyss.InAbyss && loadedAbyss.enemies.Count == 1 && loadedAbyss.enemies[0].id == 17,
                "Reserved new story actors cannot impersonate normal expedition enemies");
        }

        public static void HomewardGuidanceAndFinalChoicesStayLocalPersistentAndOnce(Campaign data)
        {
            string[] oldIds = { "main","shore","ally1","ally2","ally3","personal0","personal1","personal2","bounty","secrets","watch","signal","archive","reach" };
            Check(GameCore.QuestIds.Length >= 15 && GameCore.QuestIds[14] == "homeward", "The new story appends a unique fifteenth quest after every mature entry");
            for (int i=0;i<oldIds.Length;i++) Check(GameCore.QuestIds[i] == oldIds[i], "All mature journal quest identities retain their indices");
            foreach (bool publish in new[] { false,true })
            {
                var g = HomewardGame(data); g.TrackQuest("homeward"); QuestGuide q = g.CurrentGuide;
                Check(q.id == "homeward" && !q.complete && !q.locked && q.room == 11 && (q.target-new Point(39,29)).Length < .01f,
                    "The accepted story guides its first real stop inside the appended map");
                Check(g.NavigationTarget().HasValue && g.NavigationPath(g.NavigationTarget().Value).Count > 0, "The gold route owns a reachable local interaction approach");
                g.Enter(0); Check((g.NavigationTarget().Value-new Point(Object(g,"portal1").x,Object(g,"portal1").y)).Length < .01f,
                    "Town uses its mature forest connection to guide the new riverbank story");
                g.Enter(1); g.enemies.Clear(); Check((g.NavigationTarget().Value-new Point(Object(g,"portal11").x,Object(g,"portal11").y)).Length < .01f,
                    "Forest points the optional new story through the existing riverbank portal");
                g.Enter(11); HomewardCompletedFixture(g); g.save.flags.Remove("history_20"); g.save.flags.Remove("history_21");
                Check(!g.HomewardRecordsFound && g.HomewardRecordCount == 0 && g.CurrentGuide.steps.Count == 4 &&
                    (g.CurrentGuide.target-new Point(Object(g,"history20").x,Object(g,"history20").y)).Length < .01f,
                    "After two earned stops the guide truthfully targets the missing patrol record, not the final camp");
                HomewardStand(g,"campfire"); Check(!g.FinishHomewardRoute(publish), "Two clears without actual old documents cannot fabricate final public or family copies");
                HomewardStand(g,"history20"); g.Talk(Object(g,"history20")); g.Close();
                Check(g.HomewardRecordCount == 1 && !g.HomewardRecordsFound &&
                    (g.CurrentGuide.target-new Point(Object(g,"history21").x,Object(g,"history21").y)).Length < .01f,
                    "Actual first discovery advances the gold guide to the remaining old lamp register");
                g.save.flags.Add("lore_21");
                Check(g.HomewardRecordsFound && g.HomewardRecordCount == 2 &&
                    (g.CurrentGuide.target-new Point(Object(g,"campfire").x,Object(g,"campfire").y)).Length < .01f,
                    "The established codex alias is honored without duplicating or inventing a document");
                HomewardStand(g,"campfire"); g.Interact();
                Check(g.conversation != null && g.conversation.choices.Length >= 7 &&
                    g.conversation.choices[0].action == "fieldrest" && g.conversation.choices[1].action == "tea" && g.conversation.choices[2].action == "drinktea" &&
                    g.conversation.choices[3].action == "companions" && g.conversation.choices[4].action == "close" && g.conversation.choices[5].action == "kitchen",
                    "All six original camp options keep their established positions ahead of the new story handoff");
                g.Close(); int coins=g.save.coins, shards=g.save.shards, points=g.save.skillPoints; g.save.relationships[0]=5;
                g.SelectCompanion(1); Check(!g.FinishHomewardRoute(publish), "The final copy decision still requires the actual selected Mira");
                g.SelectCompanion(0); Point mira=g.companionPosition; g.companionPosition=new Point(61,12);
                Check(!g.FinishHomewardRoute(publish), "A distant selected Mira cannot witness the final handoff"); g.companionPosition=mira;
                g.save.x=39; g.save.y=29; Check(!g.FinishHomewardRoute(publish), "An old captured final decision cannot pay away from its camp");
                HomewardStand(g,"campfire"); g.mode="pause"; Check(!g.FinishHomewardRoute(publish), "Pause keeps the final decision from mutating persistent wealth"); g.Close();
                Check(g.FinishHomewardRoute(publish) && g.HomewardComplete && g.HomewardPublicChart == publish && g.Has(publish?"homeward_public":"homeward_family") &&
                    !g.Has(publish?"homeward_family":"homeward_public") && g.save.coins == coins+30 && g.save.shards == shards+3 && g.save.skillPoints == points && g.save.relationships[0] == 5,
                    "Both deliberate final choices pay the same advertised independent reward, cap bond and save exactly their own outcome");
                g.PrepareSave(); var loaded=new GameCore(data,CloneSave(g.save)); loaded.Start(); HomewardStand(loaded,"campfire");
                Check(loaded.HomewardComplete && loaded.HomewardPublicChart == publish && loaded.GetQuest("homeward").complete &&
                    !loaded.FinishHomewardRoute(!publish) && loaded.save.coins == coins+30 && loaded.save.shards == shards+3 && loaded.save.relationships[0] == 5,
                    "Saved completion keeps the chosen ending and rejects repeat or opposite-outcome payouts");
            }
        }
    }
}
