using System;

namespace Starfall
{
    public sealed partial class GameCore
    {
        // A single press bridges only the last few frames of the real dodge
        // cooldown. It never reserves energy or grants protection in advance.
        public const float DodgeBufferWindow = .12f;
        private sealed class DodgeIntent
        {
            public float remaining = DodgeBufferWindow;
            public Point direction;
            public SaveData owner;
            public int room, weapon, profession, style, technique, firstRelic, secondRelic, dodgeTalent;
            public bool practicing;
        }
        private DodgeIntent bufferedDodge;
        private bool CanAcceptDodge
        { get { return mode == "world" && !Dying && Finite(save.health) && save.health > 0 && !SparringActionBlocked && !RangeActionBlocked; } }
        private void ClearBufferedDodge() { bufferedDodge = null; }
        public void ClearBufferedCombatInputs() { ClearBufferedSkill(); ClearBufferedDodge(); }

        private bool TryDodgeDirection(Point requested, out Point direction)
        {
            direction = new Point();
            if (!Finite(requested.x) || !Finite(requested.y) || !Finite(requested.Length)) return false;
            Point selected = requested.Length > .1f ? requested : aim;
            if (!Finite(selected.x) || !Finite(selected.y) || !Finite(selected.Length) || selected.Length <= .05f) return false;
            direction = selected.Normalized; return true;
        }
        public void Dash(Point requested)
        {
            Point direction; float cost = Relic(2) ? 25 : 20;
            if (!CanAcceptDodge || !Finite(dashCooldown) || !Finite(save.energy) || !TryDodgeDirection(requested, out direction))
            { ClearBufferedDodge(); return; }
            if (save.energy < cost || dashCooldown > DodgeBufferWindow)
            { ClearBufferedDodge(); ActionUnavailable(dashCooldown, cost); return; }
            if (dashCooldown > 0)
            {
                ClearBufferedSkill();
                bufferedDodge = new DodgeIntent { direction = direction, owner = save, room = save.room,
                    weapon = save.weapon, profession = save.profession, style = Style, technique = save.technique,
                    firstRelic = save.relics[0], secondRelic = save.relics[1], dodgeTalent = save.talents[2], practicing = practicing };
                return;
            }
            StartDodge(direction);
        }
        private void StartDodge(Point direction)
        {
            float cost = Relic(2) ? 25 : 20;
            ClearBufferedDodge();
            if (!CanAcceptDodge || !Finite(save.energy) || save.energy < cost) return;
            CancelAction();
            dashDirection = direction;
            dashTime = .18f; dashCooldown = .75f - save.talents[2] * .06f; invulnerable = .32f; save.energy -= cost;
            perfectDodgeArmed = true;
            if (Relic(0)) { float before = save.health; save.health = Math.Min(save.maxHealth, save.health + 3); RecordHealing(before); }
            effects.Add(new Effect("dash", Position, .3f) { direction = dashDirection }); sound = "dash";
        }
        private void ValidateDodgeProfile()
        {
            DodgeIntent intent = bufferedDodge;
            if (intent != null && (!CanAcceptDodge || !object.ReferenceEquals(save, intent.owner) || save.room != intent.room ||
                save.weapon != intent.weapon || save.profession != intent.profession || Style != intent.style || save.technique != intent.technique ||
                save.relics[0] != intent.firstRelic || save.relics[1] != intent.secondRelic || save.talents[2] != intent.dodgeTalent || practicing != intent.practicing))
                ClearBufferedDodge();
        }
        private void TickBufferedDodge(float dt, float readyDelay)
        {
            DodgeIntent intent = bufferedDodge;
            if (intent == null) return;
            if (!CanAcceptDodge || TrainingClearPending || !Finite(readyDelay) || !Finite(dashCooldown)) { ClearBufferedDodge(); return; }
            float remaining = intent.remaining; intent.remaining -= dt;
            // The ready boundary can fall within this final valid simulation
            // tick. Compare its actual delay with the lifetime before ticking.
            if (dashCooldown <= .000001f && readyDelay <= remaining + .00001f) StartDodge(intent.direction);
            else if (intent.remaining <= .00001f) ClearBufferedDodge();
        }
        private bool TrainingClearPending
        {
            get
            {
                if (RangeActive)
                { foreach (Enemy enemy in enemies) if (IsRangeEnemy(enemy)) return false; return true; }
                return SparringActive && enemies.Count == 0;
            }
        }
    }
}
