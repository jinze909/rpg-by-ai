using System;

namespace Starfall
{
    public sealed partial class GameCore
    {
        private sealed class RaceCourseSession
        {
            public SaveData owner;
            public int generation, version, checkpoint;
            public WorldObject skiff;
            public Point station;
            public bool running, cancelReview;
            public float time;
        }
        private RaceCourseSession raceCourse;
        private int raceCourseVersion;
        private WorldObject RaceSkiff()
        { return data.maps.Length <= 6 ? null : Array.Find(data.maps[6].objects, o => o.id == "skiff" && o.kind == "skiff"); }
        private bool RaceLiving(string expectedMode)
        { return mode == expectedMode && save.room == 6 && !Dying && !practicing && !InAbyss
                && Finite(save.health) && save.health > 0 && CanStand(Position); }
        private bool RaceAtSkiff(string expectedMode)
        {
            WorldObject skiff = RaceSkiff();
            return RaceLiving(expectedMode) && skiff != null && Array.IndexOf(Map.objects, skiff) >= 0
                && IsObjectPresent(skiff) && ReferenceEquals(Nearest(), skiff)
                && ClearAttackPath(Position, new Point(skiff.x, skiff.y));
        }
        public bool StartSkiffRace()
        {
            if (racing || !RaceAtSkiff("world")) return false;
            racing = true; raceTime = 0; raceCheckpoint = 0;
            Notify("竞速开始：沿金色风旗走！", "Race started: follow the golden flags!"); return true;
        }
        public bool OpenRaceCourse()
        {
            WorldObject skiff = RaceSkiff();
            if (!RaceLiving("world") || skiff == null || (!racing && !RaceAtSkiff("world"))) return false;
            raceCourse = new RaceCourseSession { owner = save, generation = WorldGeneration, version = ++raceCourseVersion,
                skiff = skiff, station = new Point(skiff.x, skiff.y), running = racing, time = raceTime, checkpoint = raceCheckpoint };
            mode = "racecourse"; return true;
        }
        public bool RaceCourseActive
        {
            get
            {
                RaceCourseSession session = raceCourse;
                return session != null && RaceLiving("racecourse") && ReferenceEquals(session.owner, save)
                    && session.generation == WorldGeneration && ReferenceEquals(session.skiff, RaceSkiff())
                    && session.skiff.x == session.station.x && session.skiff.y == session.station.y
                    && session.running == racing && session.time == raceTime && session.checkpoint == raceCheckpoint;
            }
        }
        public int RaceCourseVersion { get { return RaceCourseActive ? raceCourse.version : 0; } }
        public bool RaceCourseCanLaunch { get { return RaceCourseActive && !racing && RaceAtSkiff("racecourse"); } }
        public bool RaceCourseCancellationPending { get { return RaceCourseActive && racing && raceCourse.cancelReview; } }
        public bool LaunchRaceCourse()
        {
            if (!RaceCourseCanLaunch) return false;
            Close(); raceCourse = null; return StartSkiffRace();
        }
        public bool ReviewRaceCancellation()
        { if (!RaceCourseActive || !racing) return false; raceCourse.cancelReview = true; return true; }
        public bool KeepRaceCourse()
        { if (!RaceCourseActive) return false; raceCourse.cancelReview = false; return true; }
        public bool CancelRaceFromCourse()
        {
            if (!RaceCourseCancellationPending) return false;
            racing = false; raceCourse = null; Close();
            Notify("本轮竞速已结束，物资与位置保留。回沙舟可重新出发。", "Attempt ended. Supplies and position are safe. Return to the skiff to launch again."); return true;
        }
        private void TickRaceCourse(float dt)
        {
            if (!racing) return;
            bool invalid = !Finite(raceTime) || raceTime < 0 || raceCheckpoint < 0 || raceCheckpoint >= RaceRoute.Length;
            if (!invalid) raceTime += dt;
            // The deadline owns the final contact too: a late fifth flag may
            // not turn an already expired attempt into a paid finish.
            if (invalid || !Finite(raceTime) || raceTime > 30)
            {
                racing = false;
                Notify("风向变了，本轮结束。没有损失，回到沙舟重新出发。", "The wind shifted. No loss; return to the skiff to retry."); return;
            }
            if ((Position - RaceRoute[raceCheckpoint]).Length < 1.35f)
            {
                raceCheckpoint++; sound = "reward";
                if (raceCheckpoint == RaceRoute.Length) FinishRace();
            }
        }
    }
}
