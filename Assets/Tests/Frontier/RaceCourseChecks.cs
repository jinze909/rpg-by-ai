using System;
using System.Reflection;
using System.Text;

namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        // Reflection keeps the archived shipped baseline compilable: its actual
        // "Study the route" choice must fail behaviorally, not on a missing API.
        private static bool CourseCall(GameCore game, string name)
        {
            MethodInfo method = typeof(GameCore).GetMethod(name, BindingFlags.Public | BindingFlags.Instance);
            Check(method != null, "The production course exposes its real action " + name);
            object result = method.Invoke(game, null); return method.ReturnType == typeof(void) || (bool)result;
        }
        private static bool CourseRead(GameCore game, string name)
        {
            PropertyInfo property = typeof(GameCore).GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            Check(property != null, "The production course exposes its read-only status " + name);
            return (bool)property.GetValue(game, null);
        }
        private static int CourseVersion(GameCore game)
        {
            PropertyInfo property = typeof(GameCore).GetProperty("RaceCourseVersion", BindingFlags.Public | BindingFlags.Instance);
            Check(property != null, "Course control ownership exposes a session version"); return Convert.ToInt32(property.GetValue(game, null));
        }
        private static GameCore CourseGame(Campaign data, int profession = 0)
        {
            var game = Unlocked(data); game.SkipTutorial(); game.save.profession = profession; game.save.weapon = profession;
            if (profession > 0) game.save.flags.Add("weapon_" + profession);
            game.Enter(6); Check(game.save.room == 6, "The existing harbor report unlocks the actual desert course");
            game.enemies.Clear(); game.projectiles.Clear(); game.hazards.Clear(); game.effects.Clear(); game.save.companion = -1;
            game.save.x = 14; game.save.y = 18; game.save.health = 37; game.save.energy = 27; game.invulnerable = 10000;
            Check(game.CanStand(game.Position) && ReferenceEquals(game.Nearest(), Object(game, "skiff")),
                "The original authored skiff has legal complete-foot local interaction"); return game;
        }
        private static string CourseResources(GameCore game) { return ResetSaveState(game.save); }
        private static string CourseLiveState(GameCore game)
        {
            var text = new StringBuilder(CourseResources(game));
            text.Append('/').Append(game.mode).Append('/').Append(game.racing).Append('/').Append(game.raceTime).Append('/').Append(game.raceCheckpoint)
                .Append('/').Append(game.revision).Append('/').Append(game.WorldGeneration).Append('/').Append(game.sound).Append('/').Append(game.toast)
                .Append('/').Append(game.attackCooldown).Append('/').Append(game.skillCooldown).Append('/').Append(game.dashCooldown)
                .Append('/').Append(game.effects.Count).Append('/').Append(game.projectiles.Count).Append('/').Append(game.hazards.Count).Append('/').Append(game.sounds.Count);
            foreach (Enemy enemy in game.enemies) text.Append('/').Append(enemy.id).Append(':').Append(enemy.x).Append(':').Append(enemy.y)
                .Append(':').Append(enemy.hp).Append(':').Append(enemy.timer).Append(':').Append(enemy.state);
            return text.ToString();
        }
        private static void CourseChoose(GameCore game, string action)
        {
            Check(game.mode == "dialog" && game.conversation != null, "Course choices originate in the actual skiff conversation");
            int index = Array.FindIndex(game.conversation.choices, choice => choice.action == action);
            Check(index >= 0, "The real skiff offers " + action); game.Choose(index);
        }
        private static void CourseReject(GameCore game, string action, string reason)
        {
            string resources = CourseResources(game); bool racing = game.racing; float time = game.raceTime; int checkpoint = game.raceCheckpoint;
            Check(!CourseCall(game, action), "The real course rejects " + reason);
            Check(CourseResources(game) == resources && game.racing == racing && game.raceTime.Equals(time) && game.raceCheckpoint == checkpoint,
                "Rejected course actions cannot spend, heal, teleport, reset the attempt or pay rewards: " + reason);
        }

        public static void RaceCoursePhysicalPreviewAndDeliberateLaunchProtectResources(Campaign data)
        {
            for (int profession = 0; profession < 3; profession++)
            {
                var game = CourseGame(data, profession); game.attackCooldown = .9f; game.skillCooldown = 3; game.dashCooldown = .5f;
                string resources = CourseResources(game); float speed = game.Speed;
                game.Interact(); Check(game.mode == "dialog" && game.conversation.choices.Length >= 2 && game.conversation.choices[0].action == "race",
                    "The original immediate launch remains the first local skiff choice");
                game.Choose(1);
                Check(game.mode == "racecourse" && !game.racing,
                    "Choosing the actual Study the route option opens a real non-running course preview rather than merely closing dialogue");
                Check(CourseRead(game, "RaceCourseActive") && CourseRead(game, "RaceCourseCanLaunch") && !CourseRead(game, "RaceCourseCancellationPending"),
                    "A nearby inactive course review owns a deliberate launch without arming cancellation");
                Check(CourseResources(game) == resources && game.attackCooldown == .9f && game.skillCooldown == 3 && game.dashCooldown == .5f && game.Speed == speed,
                    "Reading the course does not consume supplies, heal, apply racing speed or reset a prepared combat clock");
                Check(CourseCall(game, "LaunchRaceCourse") && game.mode == "world" && game.racing && game.raceTime == 0 && game.raceCheckpoint == 0,
                    "Only deliberate confirmation starts the original first flag at time zero for class " + profession);
                Check(CourseResources(game) == resources && Math.Abs(game.Speed - speed - .65f) < .0001f,
                    "Launching costs no hidden payment and keeps the original class-specific race-speed increment");
                game.raceTime = 2; game.raceCheckpoint = 1; CourseReject(game, "StartSkiffRace", "an already running attempt instead of silently restarting it");
                game.Interact(); CourseChoose(game, "race");
                Check(game.racing && game.raceTime == 2 && game.raceCheckpoint == 1, "The mature dialogue launch also cannot silently restart an active attempt");
            }
            foreach (string boundary in new[] { "remote", "inventory", "practice", "dead", "health", "nan", "wall", "otherroom", "canonical", "blockedpath" })
            {
                var game = CourseGame(data); WorldObject skiff = Object(game, "skiff"); WorldObject[] authored = game.Map.objects; string[] rows = (string[])game.Map.rows.Clone();
                if (boundary == "remote") { game.save.x = 30; game.save.y = 18; }
                else if (boundary == "inventory") game.mode = "inventory";
                else if (boundary == "practice") game.practicing = true;
                else if (boundary == "dead") game.deathTime = 1;
                else if (boundary == "health") game.save.health = 0;
                else if (boundary == "nan") game.save.x = float.NaN;
                else if (boundary == "wall") { game.save.x = 0; game.save.y = 0; }
                else if (boundary == "otherroom") game.Enter(0);
                try
                {
                    if (boundary == "canonical") game.Map.objects = Array.ConvertAll(authored, item => ReferenceEquals(item, skiff)
                        ? new WorldObject { id = "foreign-skiff", kind = "skiff", x = skiff.x, y = skiff.y } : item);
                    if (boundary == "blockedpath")
                    {
                        game.save.y = 19.25f; Check(game.CanStand(game.Position), "Path rejection keeps the hero on legal authored ground");
                        char[] row = game.Map.rows[(int)skiff.y].ToCharArray(); row[(int)skiff.x] = '#'; game.Map.rows[(int)skiff.y] = new string(row);
                    }
                    CourseReject(game, "StartSkiffRace", boundary + " start"); CourseReject(game, "OpenRaceCourse", boundary + " inactive preview");
                }
                finally { data.maps[6].objects = authored; data.maps[6].rows = rows; }
            }
            var threatened = CourseGame(data); threatened.enemies.Add(new Enemy(95011, 0, 14.5f, 18, 20));
            Check(!threatened.SafeTravel && CourseCall(threatened, "StartSkiffRace"),
                "The original local race is still available near enemies; route study does not introduce a new safe-travel prerequisite");
        }

        public static void RaceCoursePausedReviewCancellationAndOwnerRevalidation(Campaign data)
        {
            var game = CourseGame(data); Check(CourseCall(game, "StartSkiffRace"), "Begin an actual live race before reviewing it");
            game.save.x = 23; game.save.y = 5; game.Tick(.05f, new Point());
            Check(game.racing && game.raceCheckpoint == 0 && game.raceTime > 0, "A distant later flag cannot bypass the first ordered checkpoint");
            Check(CourseCall(game, "OpenRaceCourse") && game.mode == "racecourse" && !CourseRead(game, "RaceCourseCanLaunch"),
                "A running attempt can inspect the course away from the launch station without offering a reset");
            string before = CourseLiveState(game); int version = CourseVersion(game);
            for (int frame = 0; frame < 100; frame++)
            {
                Check(CourseRead(game, "RaceCourseActive") && !CourseRead(game, "RaceCourseCanLaunch") && !CourseRead(game, "RaceCourseCancellationPending") && CourseVersion(game) == version,
                    "Repeated status reads retain the same owned course without advancing its control version");
                game.Tick(.05f, new Point(1, 0));
            }
            Check(CourseLiveState(game) == before, "Course study pauses the real clock, movement, climate, combat, RNG, sound and economy");
            CourseReject(game, "CancelRaceFromCourse", "cancellation without explicit review");
            Check(CourseCall(game, "ReviewRaceCancellation") && CourseRead(game, "RaceCourseCancellationPending"), "Stopping an attempt first presents a separate deliberate review");
            string resources = CourseResources(game); float time = game.raceTime; int checkpoint = game.raceCheckpoint; Point foot = game.Position;
            CourseCall(game, "KeepRaceCourse");
            Check(CourseRead(game, "RaceCourseActive") && !CourseRead(game, "RaceCourseCancellationPending") && game.racing,
                "Declining cancellation remains in the same non-running course review with the attempt intact");
            game.Close(); Check(!CourseRead(game, "RaceCourseActive") && game.mode == "world" && game.racing && game.raceTime == time && game.raceCheckpoint == checkpoint,
                "Closing course study resumes the same running attempt rather than cancelling or restarting it");
            game.Tick(.05f, new Point()); Check(game.raceTime > time, "The resumed attempt advances again through the real world simulation");
            Check(CourseCall(game, "OpenRaceCourse") && CourseVersion(game) != version, "Reopening owns a fresh UI control session");
            CourseCall(game, "ReviewRaceCancellation"); time = game.raceTime; checkpoint = game.raceCheckpoint; resources = CourseResources(game);
            Check(CourseCall(game, "CancelRaceFromCourse") && game.mode == "world" && !game.racing && !CourseRead(game, "RaceCourseActive"), "Only the reviewed stop ends the current attempt");
            Check(game.raceTime == time && game.raceCheckpoint == checkpoint && (game.Position - foot).Length == 0 && CourseResources(game) == resources && !game.Has("race_finished"),
                "Stopping records no reward or fee, spends no tea, never teleports and retains the stopped timing for honest feedback");
            CourseReject(game, "CancelRaceFromCourse", "the already consumed stop"); CourseReject(game, "StartSkiffRace", "a remote restart after stopping");

            foreach (string boundary in new[] { "save", "load", "travel", "newgame", "post", "postmove", "timer", "checkpoint", "practice", "death", "mode" })
            {
                var stale = CourseGame(data); CourseCall(stale, "StartSkiffRace"); stale.raceTime = 4; stale.raceCheckpoint = 1;
                CourseCall(stale, "OpenRaceCourse"); CourseCall(stale, "ReviewRaceCancellation");
                WorldObject post = Object(stale, "skiff"); WorldObject[] authored = stale.Map.objects; float postX = post.x;
                try
                {
                    if (boundary == "save") stale.save = CloneSave(stale.save);
                    else if (boundary == "load") stale.Load(CloneSave(stale.save));
                    else if (boundary == "travel") stale.Enter(0);
                    else if (boundary == "newgame") stale.NewGame(1);
                    else if (boundary == "post") stale.Map.objects = Array.ConvertAll(authored, item => ReferenceEquals(item, post)
                        ? new WorldObject { id = post.id, kind = post.kind, x = post.x, y = post.y } : item);
                    else if (boundary == "postmove") post.x += 1;
                    else if (boundary == "timer") stale.raceTime += .1f;
                    else if (boundary == "checkpoint") stale.raceCheckpoint++;
                    else if (boundary == "practice") stale.practicing = true;
                    else if (boundary == "death") stale.deathTime = 1;
                    else if (boundary == "mode") stale.mode = "inventory";
                    Check(!CourseRead(stale, "RaceCourseActive"), "The saved preview no longer owns a live course after " + boundary);
                    CourseReject(stale, "LaunchRaceCourse", "stale " + boundary + " launch"); CourseReject(stale, "CancelRaceFromCourse", "stale " + boundary + " stop");
                }
                finally { data.maps[6].objects = authored; post.x = postX; }
            }
        }

        private static void CourseBoundaryFinish(GameCore game, float before)
        {
            game.Close(); game.save.x = 14; game.save.y = 18;
            game.Interact(); CourseChoose(game, "race");
            Check(game.mode == "world" && game.racing && game.raceTime == 0 && game.raceCheckpoint == 0,
                "A deadline fixture begins through the original physical skiff interaction and launch choice");
            game.raceCheckpoint = 4; game.raceTime = before; game.Tick(.05f, new Point());
        }
        public static void RaceCourseDeadlineBadgesAndPhysicalReplays(Campaign data)
        {
            Point[] authored = { new Point(8, 18), new Point(8, 5), new Point(23, 5), new Point(30, 18), new Point(14, 18) };
            Check(GameCore.RaceRoute.Length == authored.Length, "The complete mature route still has exactly five ordered flags");
            for (int i = 0; i < authored.Length; i++) Check((GameCore.RaceRoute[i] - authored[i]).Length == 0, "Course study preserves authored flag geometry " + (i + 1));
            var exact = CourseGame(data); int coins = exact.save.coins, shards = exact.save.shards;
            CourseBoundaryFinish(exact, 29.95f);
            Check(!exact.racing && exact.Has("race_finished") && !exact.Has("race_badge") && exact.raceCheckpoint == 5 && exact.save.bestRace == 300
                && exact.save.coins == coins + 25 && exact.save.shards == shards + 3, "Exactly thirty seconds remains a valid original first finish with no fast badge");
            var late = CourseGame(data); CourseBoundaryFinish(late, 29.96f);
            Check(!late.racing && !late.Has("race_finished") && late.raceCheckpoint == 4 && late.save.bestRace == 0 && late.save.coins == coins && late.save.shards == shards,
                "The real deadline expires before a final checkpoint at 30.01 seconds and cannot mint a late finish");
            var badge = CourseGame(data); coins = badge.save.coins; shards = badge.save.shards; int points = badge.save.skillPoints;
            CourseBoundaryFinish(badge, 15.95f);
            Check(!badge.racing && badge.Has("race_finished") && badge.Has("race_badge") && badge.save.bestRace == 160 && badge.save.coins == coins + 25
                && badge.save.shards == shards + 8 && badge.save.skillPoints == points + 1, "Exactly sixteen seconds retains the original inclusive badge and both once-only awards");
            coins = badge.save.coins; shards = badge.save.shards; points = badge.save.skillPoints; CourseBoundaryFinish(badge, 14.95f);
            Check(badge.save.bestRace == 150 && badge.save.coins == coins && badge.save.shards == shards && badge.save.skillPoints == points,
                "A faster replay improves the record while both mature finish rewards remain consumed");
            foreach (int invalid in new[] { -1, 5, int.MaxValue })
            {
                var broken = CourseGame(data); CourseCall(broken, "StartSkiffRace"); broken.raceCheckpoint = invalid; coins = broken.save.coins;
                broken.Tick(.05f, new Point()); Check(!broken.racing && !broken.Has("race_finished") && broken.save.bestRace == 0 && broken.save.coins == coins,
                    "Malformed transient checkpoint " + invalid + " stops gracefully without indexing beyond the course or creating a result");
            }
            foreach (float invalid in new[] { -1f, float.NaN, float.PositiveInfinity })
            {
                var broken = CourseGame(data); CourseCall(broken, "StartSkiffRace"); broken.raceTime = invalid; coins = broken.save.coins;
                broken.Tick(.05f, new Point()); Check(!broken.racing && !broken.Has("race_finished") && broken.save.bestRace == 0 && broken.save.coins == coins,
                    "Malformed transient race time stops without granting a reward");
            }
            for (int profession = 0; profession < 3; profession++)
            {
                var runner = CourseGame(data, profession); runner.save.energy = 100;
                RunRace(runner); Check(runner.Has("race_finished") && runner.save.bestRace > 0 && runner.raceCheckpoint == 5 && runner.raceTime <= 30,
                    "Every class actually walks and dodges through the entire authored route within its unchanged deadline");
                coins = runner.save.coins; shards = runner.save.shards; points = runner.save.skillPoints;
                RunRace(runner); Check(runner.save.coins == coins && runner.save.shards == shards && runner.save.skillPoints == points,
                    "Real movement replay for class " + profession + " cannot farm any consumed finish or badge reward");
            }
        }

        public static void RaceCourseOldSavesKeepRecordsWhileTransientSessionsClear(Campaign data)
        {
            foreach (int version in new[] { 1, 2 }) foreach (string stage in new[] { "preview", "running", "review" })
            {
                var original = CourseGame(data); original.save.bestRace = 143; original.save.coins = 91; original.save.shards = 23; original.save.skillPoints = 7;
                foreach (string flag in new[] { "race_finished", "race_badge", "weapon_1", "weapon_2", "relic_0", "watergarden_complete", "returnletter_complete", "personal_1" }) original.save.flags.Add(flag);
                original.save.weapon = 1; original.save.weaponStyles[1] = 2; original.save.mastery[1] = 47; original.save.relics[0] = 0;
                if (stage != "preview") { CourseCall(original, "StartSkiffRace"); original.raceTime = 4.7f; original.raceCheckpoint = 2; }
                if (stage != "running") CourseCall(original, "OpenRaceCourse");
                if (stage == "review") CourseCall(original, "ReviewRaceCancellation");
                original.PrepareSave(); SaveData saved = CloneSave(original.save); saved.version = version;
                var expected = new GameCore(data, CloneSave(saved)); expected.Start(); expected.Close();
                original.Load(CloneSave(saved)); original.Start(); original.Close();
                Check(CourseResources(original) == CourseResources(expected) && original.save.version == 2 && original.save.bestRace == 143 && original.save.coins == 91
                    && original.save.shards == 23 && original.save.skillPoints == 7 && original.save.weapon == 1 && original.save.weaponStyles[1] == 2 && original.save.mastery[1] == 47,
                    "Version " + version + " from " + stage + " preserves the complete existing migration, record, supplies, story and equipment instead of inventing a save format");
                Check(!original.racing && original.raceTime == 0 && original.raceCheckpoint == 0 && !CourseRead(original, "RaceCourseActive") && !CourseRead(original, "RaceCourseCancellationPending"),
                    "Reloading clears live checkpoint, clock and confirmation ownership; unfinished attempts are not persisted");
                Check(original.Has("watergarden_complete") && original.Has("returnletter_complete") && original.Has("personal_1") && original.Has("race_finished") && original.Has("race_badge"),
                    "Old and new saved story flags and consumed race rewards survive review and reconstruction");
                int coins = original.save.coins, shards = original.save.shards, points = original.save.skillPoints;
                CourseBoundaryFinish(original, 12.95f);
                Check(original.save.bestRace == 130 && original.save.coins == coins && original.save.shards == shards && original.save.skillPoints == points,
                    "A resumed old save can improve its record without repaying either original reward");
            }
            foreach (FieldInfo field in typeof(SaveData).GetFields(BindingFlags.Public | BindingFlags.Instance))
                Check(field.Name.IndexOf("raceCourse", StringComparison.OrdinalIgnoreCase) < 0 && field.Name != "raceTime" && field.Name != "raceCheckpoint" && field.Name != "racing",
                    "Preview ownership, race timing and stop confirmation remain transient rather than changing the mature save schema");
        }
    }
}
