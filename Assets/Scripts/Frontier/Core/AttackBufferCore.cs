using System;

namespace Starfall
{
    public sealed partial class GameCore
    {
        // Only a fresh press can create this intent. Held attacks keep their
        // original repeat cadence and cannot refresh its lifetime or aim.
        public const float AttackBufferWindow = .12f;
        private sealed class AttackIntent
        {
            public float remaining = AttackBufferWindow;
            public Point direction;
            public Point? target;
            public SaveData owner;
            public int room, weapon, profession, style, technique;
            public bool practicing;
        }
        private AttackIntent bufferedAttack;
        private bool CanAcceptAttack
        { get { return mode == "world" && !Dying && Finite(save.health) && save.health > 0 && !SparringActionBlocked && !RangeActionBlocked; } }
        public void ClearBufferedAttack() { bufferedAttack = null; }

        public void PressAttack(Point requested)
        {
            // A basic press never displaces a queued deliberate Q or dodge.
            if (bufferedSkill != null || bufferedDodge != null) return;
            float delay = ActionReadyDelay(attackCooldown);
            if (!CanAcceptAttack || TrainingClearPending || !Finite(delay) || !Finite(attackCooldown)
                || !Finite(save.energy) || !Finite(requested.x) || !Finite(requested.y) || !Finite(requested.Length))
            { ClearBufferedAttack(); return; }
            WeaponProfile profile = Profile(save.weapon, Style);
            // Keep the cooldown cutoff strict; subtraction at an authored
            // contact/recovery boundary can leave a few float ULPs of residue.
            if (attackCooldown > AttackBufferWindow || delay > AttackBufferWindow + .00001f)
            { ClearBufferedAttack(); return; }
            if (save.energy < profile.energy)
            { ClearBufferedAttack(); ActionUnavailable(0, profile.energy); return; }
            Point facing = requested.Length > .05f ? requested.Normalized : aim;
            facing = ResolveCombatFocusDirection(facing);
            if (!Finite(facing.x) || !Finite(facing.y) || !Finite(facing.Length) || facing.Length <= .05f)
            { ClearBufferedAttack(); return; }
            if (attackCooldown <= 0 && !ActionTransitionBlocked)
            { ClearBufferedAttack(); StartAttack(facing, aimTarget); return; }
            bufferedAttack = new AttackIntent { direction = facing, target = aimTarget, owner = save,
                room = save.room, weapon = save.weapon, profession = save.profession,
                style = Style, technique = save.technique, practicing = practicing };
        }
        private void ValidateAttackProfile()
        {
            AttackIntent intent = bufferedAttack;
            if (intent != null && (!CanAcceptAttack || !ReferenceEquals(save, intent.owner) || save.room != intent.room
                || save.weapon != intent.weapon || save.profession != intent.profession || Style != intent.style
                || save.technique != intent.technique || practicing != intent.practicing)) ClearBufferedAttack();
        }
        private void TickBufferedAttack(float dt, float readyDelay)
        {
            AttackIntent intent = bufferedAttack;
            if (intent == null) return;
            if (!CanAcceptAttack || TrainingClearPending || bufferedSkill != null || bufferedDodge != null
                || !Finite(attackCooldown) || !Finite(save.energy) || !Finite(readyDelay))
            { ClearBufferedAttack(); return; }
            float remaining = intent.remaining; intent.remaining -= dt;
            // Use the boundary before this tick; a long frame cannot extend
            // the window. Contact hit stop freezes all clocks on the next tick.
            bool ready = attackCooldown <= .000001f && readyDelay <= remaining + .00001f;
            if (ready)
            {
                // Repeated subtraction may leave a sub-microsecond tail on
                // dodge or recoil. Do not lose the final valid press to it.
                if (dashTime > 0 && dashTime <= .000001f) dashTime = 0;
                if (playerHitTime > 0 && playerHitTime <= .000001f) playerHitTime = 0;
            }
            if (ready && !ActionTransitionBlocked)
            {
                ClearBufferedAttack();
                if (attackCooldown > 0) attackCooldown = 0;
                StartAttack(intent.direction, intent.target);
            }
            else if (intent.remaining <= .00001f) ClearBufferedAttack();
        }
    }
}
