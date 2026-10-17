using System;
namespace Starfall
{
    // The Editor review lab and PlayMode checks share the real runtime renderer.
    // Keep the original core object intact: no review progress is written to disk.
    public sealed partial class FrontierRuntime
    {
        private GameCore reviewOriginal;
        private Point reviewCamera;
        private float reviewSaveTimer;
        public bool VisualReviewActive { get { return reviewOriginal != null; } }
        public bool VisualReviewPaused { get; set; }
        public void BeginVisualReview(int profession, int room, bool encounter = false)
        {
            if (Game == null) return;
            if (!VisualReviewActive) { reviewOriginal = Game; reviewCamera = camera; reviewSaveTimer = saveTimer; }
            Game = new GameCore(reviewOriginal.data);
            Game.NewGame((int)GameCore.Clamp(profession, 0, 2)); Game.Close(); Game.SkipTutorial();
            Game.save.stage = 3; Game.save.frontierEnding = 1;
            foreach (string flag in new[] { "exp_intro", "harbor_report", "desert_report", "gate_1", "gate_2", "gate_3", "gate_5", "gate_6", "gate_7", "weapon_1", "weapon_2", "core_1", "core_2", "core_3", "companion_0" })
                if (!Game.Has(flag)) Game.save.flags.Add(flag);
            Game.save.companion = 0;
            if (room == 8)
            {
                Game.StartAbyss(0);
                // Review the real final-floor spawn, using the normal exit action.
                for (int floor = 1; floor < 6; floor++)
                {
                    Game.enemies.Clear();
                    Game.Talk(Array.Find(Game.Map.objects, o => o.kind == "abyssGate")); Game.Choose(0);
                }
            }
            else Game.Enter((int)GameCore.Clamp(room, 0, Game.data.maps.Length - 1));
            Game.Close();
            if (encounter && room > 0 && room < 8)
            {
                WorldObject altar = Array.Find(Game.Map.objects, o => o.kind == "shrine" || o.kind == "expAltar");
                if (altar != null)
                {
                    Game.Talk(altar);
                    if (Game.conversation != null)
                    {
                        int choice = Array.FindIndex(Game.conversation.choices, c => c.action == "boss" || c.action == "expboss");
                        if (choice >= 0) Game.Choose(choice);
                    }
                    Game.Close();
                }
            }
            if (encounter && room > 0 && room < 9)
            {
                Enemy boss = Game.enemies.Find(e => e.boss);
                if (boss != null)
                {
                    SetVisualReviewPosition(new Point(boss.x - 3.5f, boss.y + 1));
                }
            }
            Game.invulnerable = 0; Game.toastTime = 0; camera = CameraTarget(); clock = 0;
            previousRoom = Game.save.room; previousFloor = Game.save.abyssFloor; previousTutorial = Game.TutorialStep;
            arrivalTime = shakeTime = shakeX = shakeY = 0;
            route.Clear(); routeGoal = null; routeTimer = 0; resumeTime = combatHudTime = 0;
            saveConfirmation = null; replacePrompt = false; dialoguePage = 0; lastConversation = null;
            settingsParent = "world"; dialogParent = null;
            controllerUsed = pointerAimActive = false;
            VisualReviewPaused = true;
        }
        // Staged captures must use the same footprint as actual walking; never
        // place a hero in flood water just to stand beside a boss.
        public bool SetVisualReviewPosition(Point preferred)
        {
            if (!VisualReviewActive) return false;
            Point position = preferred; bool found = Game.CanStand(position);
            for (float radius = .25f; !found && radius <= 3; radius += .25f)
                for (int direction = 0; direction < 8 && !found; direction++)
                {
                    float angle = direction * (float)Math.PI / 4;
                    Point candidate = preferred + new Point((float)Math.Cos(angle), (float)Math.Sin(angle)) * radius;
                    if (Game.CanStand(candidate)) { position = candidate; found = true; }
                }
            if (!found) return false;
            Game.save.x = position.x; Game.save.y = position.y;
            // A frozen review never ticks normal following. Place the ally on
            // real ground here instead of leaving it inside a wall or water.
            Point[] offsets = { new Point(-1.65f, .55f), new Point(1.65f, .55f), new Point(-1.65f, -.55f), new Point(1.65f, -.55f), new Point(0, 1.9f), new Point(0, -1.9f) };
            Game.companionPosition = position;
            foreach (Point offset in offsets)
                if (Game.CanStand(position + offset)) { Game.companionPosition = position + offset; break; }
            return true;
        }
        public void AdvanceVisualReview(float dt, Point movement)
        {
            if (!VisualReviewActive || Game == null) return;
            Game.Tick(dt, movement); clock += dt; moving = movement.Length > .1f;
            UpdatePresentation(dt); FollowCamera(dt);
        }
        public void EndVisualReview()
        {
            if (!VisualReviewActive) return;
            ResetCombatTextLayout();
            Game = reviewOriginal; reviewOriginal = null; VisualReviewPaused = false;
            camera = reviewCamera; saveTimer = reviewSaveTimer; moving = false;
            combatHudTime = 0; previousRoom = -1; route.Clear(); routeGoal = null; arrivalTime = shakeTime = 0;
            saveConfirmation = null; replacePrompt = false; dialoguePage = 0; lastConversation = null;
        }
    }
}
