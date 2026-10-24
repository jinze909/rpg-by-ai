using System;

namespace Starfall
{
    public sealed partial class GameCore
    {
        public const float CompanionCommandBufferWindow = .12f;
        private sealed class CompanionCommandIntent
        {
            public float remaining = CompanionCommandBufferWindow;
            public Point direction;
            public Point? target;
            public SaveData owner;
            public int generation, room, companion, weapon, profession, style, technique;
        }
        private CompanionCommandIntent bufferedCompanionCommand;
        private bool CanAcceptCompanionCommand
        {
            get { return mode == "world" && !Dying && !practicing && Finite(save.health) && save.health > 0
                && save.companion >= 0 && save.companion <= 2 && Finite(companionCooldown); }
        }
        private bool CompanionCommandIntentValid(CompanionCommandIntent intent)
        {
            return intent != null && CanAcceptCompanionCommand && ReferenceEquals(save, intent.owner)
                && WorldGeneration == intent.generation && save.room == intent.room && save.companion == intent.companion
                && save.weapon == intent.weapon && save.profession == intent.profession && Style == intent.style && save.technique == intent.technique;
        }
        public bool CompanionCommandQueued { get { return CompanionCommandIntentValid(bufferedCompanionCommand); } }
        public void ClearBufferedCompanionCommand() { bufferedCompanionCommand = null; }

        public void CommandCompanion()
        {
            if (!CanAcceptCompanionCommand || companionCooldown > CompanionCommandBufferWindow)
            { ClearBufferedCompanionCommand(); return; }
            Point direction = aimTarget.HasValue ? (aimTarget.Value - companionPosition).Normalized : aim;
            if (!Finite(direction.x) || !Finite(direction.y) || !Finite(direction.Length)
                || aimTarget.HasValue && (!Finite(aimTarget.Value.x) || !Finite(aimTarget.Value.y)))
            { ClearBufferedCompanionCommand(); return; }
            if (companionCooldown <= 0) { StartCompanionCommand(direction, aimTarget); return; }
            // A fresh press remembers only intent. Existing automatic support,
            // cooldown, receipts and effects continue until a real command starts.
            bufferedCompanionCommand = new CompanionCommandIntent { direction = direction, target = aimTarget, owner = save,
                generation = WorldGeneration, room = save.room, companion = save.companion, weapon = save.weapon,
                profession = save.profession, style = Style, technique = save.technique };
        }
        private void TickBufferedCompanionCommand(float dt, float readyDelay)
        {
            CompanionCommandIntent intent = bufferedCompanionCommand;
            if (intent == null) return;
            if (!CompanionCommandIntentValid(intent) || !Finite(readyDelay))
            { ClearBufferedCompanionCommand(); return; }
            float remaining = intent.remaining; intent.remaining -= dt;
            // Check the boundary before this tick, not just the new zero. A long
            // frame cannot extend the window or advance the new preparation.
            if (companionCooldown <= .000001f && readyDelay <= remaining + .00001f)
            {
                if (companionCooldown > 0) companionCooldown = 0;
                StartCompanionCommand(intent.direction, intent.target);
            }
            else if (intent.remaining <= .00001f) ClearBufferedCompanionCommand();
        }
    }
}
