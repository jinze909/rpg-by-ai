using System;
using System.Collections.Generic;

namespace Starfall
{
    // One simulation clock drives poses and damage. Rendering never dispatches damage.
    public sealed class ActorAction
    {
        public string kind;
        public float elapsed, duration, contact, damage;
        public int weapon, equippedWeapon, profession, style, combo, technique;
        public Point direction;
        public Point? target;
        public bool released;
        public ImpactProfile impact;
        public float Progress { get { return GameCore.Clamp(elapsed / duration, 0, 1); } }
    }
    public sealed class FallenActor
    {
        public Enemy actor;
        public int room;
        public float life, duration;
    }
    public sealed partial class GameCore
    {
        public ActorAction playerAction;
        public ActorAction companionAction;
        public Point companionAim;
        public bool companionMoving;
        public float companionMoveTime;
        public readonly List<FallenActor> fallen = new List<FallenActor>();
        public float playerHitTime, playerFlash, deathTime, hitStop, impactShake;
        public Point playerRecoil, movementDirection;
        private float finaleDelay;
        private int fallenPlayerWeapon=-1;
        private Point fallenPlayerFacing;
        private Point playerHitFacing;
        public bool Dying { get { return deathTime > 0; } }
        public int AppearanceWeapon { get { return Dying&&fallenPlayerWeapon>=0?fallenPlayerWeapon:playerAction != null ? playerAction.weapon : save.weapon; } }

        private void ResetCombatPresentation()
        {
            playerAction = null; fallen.Clear(); playerHitTime = playerFlash = deathTime = hitStop = impactShake = finaleDelay = 0;
            playerRecoil = movementDirection = new Point();
            fallenPlayerWeapon=-1;fallenPlayerFacing=new Point();
            playerHitFacing=new Point();
        }
        private void CancelAction() { playerAction = null; }
        private void ValidateActionProfile()
        {
            ActorAction a = playerAction;
            if (a != null && (a.equippedWeapon != save.weapon || a.profession != save.profession || a.style != Style || a.technique != save.technique)) CancelAction();
        }
        private void BeginAction(string kind, float damage, float duration, float contact)
        {
            playerAction = new ActorAction { kind = kind, damage = damage, duration = duration, contact = contact,
                direction = aim, target = aimTarget, weapon = kind == "skill" ? save.profession : save.weapon, equippedWeapon = save.weapon, profession = save.profession, style = Style, combo = combo, technique = save.technique,
                impact = CaptureImpact(kind == "skill" ? save.profession : save.weapon,kind == "attack" ? combo : 0) };
        }
        private void TickAction(float dt)
        {
            ValidateActionProfile();
            ActorAction a = playerAction;
            if (a == null) return;
            // A recoil pose cannot hide a still-pending contact frame. Resume the
            // preparation after recoil, then release damage on the visible pose.
            if (!a.released && playerHitTime > 0) return;
            a.elapsed += dt;
            if (!a.released && a.elapsed + .00001f >= a.contact)
            {
                a.released = true;
                if (a.kind == "attack") ReleaseAttack(a); else ReleaseSkill(a);
            }
            if (playerAction == a && a.elapsed >= a.duration) playerAction = null;
        }
        private void ReleaseAttack(ActorAction a)
        {
            sound = a.weapon == 0 ? "attack" : a.weapon == 1 ? "arrow" : "spell";
            if (a.weapon > 0)
            {
                int spread = a.weapon == 1 && a.style == 2 ? 1 : 0;
                for (int i = -spread; i <= spread; i++)
                {
                    Projectile shot = FriendlyShot(Position, a.weapon, a.direction, a.weapon == 1 ? 12 : 8, a.damage * (spread > 0 ? .65f : 1), a.weapon == 2, a.target, a.impact);
                    double angle = Math.Atan2(shot.velocity.y, shot.velocity.x) + i * .18;
                    Point direction = new Point((float)Math.Cos(angle), (float)Math.Sin(angle));
                    shot.velocity = direction * (a.weapon == 1 ? 12 : 8); shot.piercing = a.weapon == 1 && a.style == 1; shot.slowing = a.weapon == 2 && a.style == 2;
                    projectiles.Add(shot);
                }
                effects.Add(new Effect("release", WeaponOrigin(Position, a.weapon, a.direction), .18f) { direction = a.direction, variant = a.weapon, height = WeaponHeight(a.weapon, a.direction) });
                return;
            }
            effects.Add(new Effect("slash", Position + a.direction * .7f, .22f) { direction = a.direction, variant = a.combo });
            foreach (Enemy e in enemies.ToArray())
            {
                Point d = e.Position - Position;
                if (d.Length < (a.combo == 2 ? 2.2f : 1.85f) && d.Normalized.x * a.direction.x + d.Normalized.y * a.direction.y > -.15f && ClearAttackPath(Position, e.Position)) Hit(e, a.damage,false,null,a.impact);
            }
        }
        private void ReleaseSkill(ActorAction a)
        {
            Point previous = aim; aim = a.direction;
            try { ResolveSkill(a); } finally { aim = previous; }
        }
        private void TickVisuals(float dt)
        {
            playerHitTime = Math.Max(0, playerHitTime - dt); playerFlash = Math.Max(0, playerFlash - dt);
            impactShake = Math.Max(0, impactShake - dt);
            for(int i=fallen.Count-1;i>=0;i--){FallenActor body=fallen[i];body.life-=dt;if(body.life<=0)fallen.RemoveAt(i);}
            if (finaleDelay > 0 && mode == "world")
            { finaleDelay -= dt; if (finaleDelay <= 0 && save.room == 4 && Has("boss_4")) FinalConversation(); }
        }
        private void BeginDeath()
        {
            fallenPlayerWeapon=AppearanceWeapon;fallenPlayerFacing=VisualFacing;
            CancelAction(); companionAction = null; dashTime = 0; deathTime = .9f; hitStop = 0;
            Flag("recovery_pending"); // A quit during the death pose resumes safely at the inn.
        }
        private void CompleteDeath()
        {
            bool abyss = InAbyss;
            if (abyss) EndAbyss(false);
            save.flags.Remove("recovery_pending"); save.health = save.maxHealth; save.energy = 100; Enter(0);
            Notify("镇民把你带回了旅店。失去 10 硬币，物品和任务进度保留。", "The town brought you home. Lost 10 coins; items and quests are safe.");
            if (abyss) Notify("远征中止：本层未结算收益丢失，先前结算与永久进度保留。", "Expedition ended: this floor's unbanked loot is lost; banked rewards and permanent progress remain.");
        }
        // Exposes the exact contact boundary to native capture/tests without duplicating rules.
        public int PlayerPose
        {
            get
            {
                if (Dying) return 11;
                if (dashTime > 0) return 8;
                if (playerHitTime > 0) return 10;
                if (playerAction != null)
                {
                    if (PlayerPhase == ActionPhase.Recovery && ActuallyMoving) return 2 + WalkFrame % 2;
                    bool ready = playerAction.elapsed < playerAction.contact;
                    bool heavy = playerAction.weapon == 0 && (playerAction.combo == 2 || playerAction.style == 1);
                    return playerAction.kind == "skill" || heavy ? (ready ? 6 : 7) : (ready ? 4 : 5);
                }
                if (guardTime > 0 && !ActuallyMoving) return 9;
                if (ActuallyMoving) return 2 + WalkFrame % 2;
                return aim.y < -.65f ? 1 : 0;
            }
        }
    }
}
