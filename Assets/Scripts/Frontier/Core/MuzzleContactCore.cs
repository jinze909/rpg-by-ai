using System;

namespace Starfall
{
    public sealed partial class GameCore
    {
        private bool InLaunchDirection(Projectile shot, Point target)
        {
            Point offset = target - shot.launchOrigin;
            // Overlapping actors behind the prepared shot are not weapon hits.
            // The later flight retains its original captured muzzle-to-target ray.
            return offset.x * shot.launchDirection.x + offset.y * shot.launchDirection.y > 0;
        }
        private bool SweepProjectileLaunch(Projectile shot, out float impactRadius)
        {
            shot.launchPending = false;
            Point muzzle = shot.position, delta = muzzle - shot.launchOrigin;
            shot.position = shot.launchOrigin; impactRadius = 0;
            // Keep WeaponShot's authored centerline clearance through the
            // attachment. Applying the flying radius at the shooter's feet
            // would kill a valid shot fired away from an adjacent wall.
            if (Blocked(shot.position.x, shot.position.y)) return true;
            if (ProjectileContact(shot, true, out impactRadius)) return true;
            int steps = Math.Max(1, (int)Math.Ceiling(delta.Length / .04f));
            for (int step = 1; step <= steps; step++)
            {
                Point next = shot.launchOrigin + delta * (step / (float)steps);
                if (Blocked(next.x, next.y)) return true;
                shot.position = next;
                if (ProjectileContact(shot, true, out impactRadius)) return true;
            }
            // Preserve the muzzle's original terrain clip even when that wall
            // lies after a valid actor contact in the short launch corridor.
            return shot.blockedAtSpawn || ShotBlocked(shot, shot.position);
        }
        private bool ProjectileContact(Projectile shot, bool launching, out float impactRadius)
        {
            impactRadius = 0;
            Point direction = launching ? shot.launchDirection : shot.velocity.Normalized;
            if (!shot.friendly)
            {
                if ((!launching || InLaunchDirection(shot, Position)) && (Position - shot.position).Length < .38f)
                { Hurt(shot.damage, shot.source ?? "shot", direction); return true; }
                return false;
            }
            bool dead = false;
            projectileTargets.Clear(); projectileTargets.AddRange(enemies);
            foreach (Enemy enemy in projectileTargets)
            {
                if (shot.hits.Contains(enemy.id) || launching && !InLaunchDirection(shot, enemy.Position)
                    || (enemy.Position - shot.position).Length >= (enemy.boss ? .9f : .5f) + shot.Radius
                    || !ClearAttackPath(shot.position, enemy.Position)) continue;
                shot.hits.Add(enemy.id); Hit(enemy, shot.damage, shot.technique, direction, shot.impact);
                // A completed drill can retire every shot during the contact.
                // Never leave damage or an impact in the restored world.
                if (!projectiles.Contains(shot)) { dead = true; break; }
                if (shot.slowing) enemy.stun = Math.Max(enemy.stun, .45f);
                dead = !shot.piercing;
                if (shot.explosive)
                {
                    impactRadius = 1.8f; dead = true;
                    splashTargets.Clear(); splashTargets.AddRange(enemies);
                    foreach (Enemy other in splashTargets)
                        if (other != enemy && (other.Position - shot.position).Length < impactRadius && ClearAttackPath(shot.position, other.Position))
                            Hit(other, shot.damage * .6f, false, (other.Position - shot.position).Normalized, shot.impact);
                    splashTargets.Clear();
                }
                if (!shot.piercing || shot.explosive) break;
            }
            projectileTargets.Clear();
            return dead;
        }
    }
}
