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
            Point travel = delta.Length > .0001f ? delta.Normalized : shot.launchDirection;
            shot.position = shot.launchOrigin; impactRadius = 0;
            // Keep WeaponShot's authored centerline clearance through the
            // attachment. Applying the flying radius at the shooter's feet
            // would kill a valid shot fired away from an adjacent wall.
            if (Blocked(shot.position.x, shot.position.y)) return true;
            if (ProjectileContact(shot, true, travel, out impactRadius)) return true;
            int steps = Math.Max(1, (int)Math.Ceiling(delta.Length / .04f));
            for (int step = 1; step <= steps; step++)
            {
                Point next = shot.launchOrigin + delta * (step / (float)steps);
                if (Blocked(next.x, next.y)) return true;
                shot.position = next;
                if (ProjectileContact(shot, true, travel, out impactRadius)) return true;
            }
            // Preserve the muzzle's original terrain clip even when that wall
            // lies after a valid actor contact in the short launch corridor.
            return shot.blockedAtSpawn || ShotBlocked(shot, shot.position);
        }
        private bool CanProjectileContact(Projectile shot, Enemy enemy, bool launching)
        {
            return !shot.hits.Contains(enemy.id) && (!launching || InLaunchDirection(shot, enemy.Position))
                && (enemy.Position - shot.position).Length < (enemy.boss ? .9f : .5f) + shot.Radius
                && ClearAttackPath(shot.position, enemy.Position);
        }
        private static double ProjectileContactEntry(Projectile shot, Enemy enemy, Point travel)
        {
            Point offset = enemy.Position - shot.position;
            double along = (double)offset.x * travel.x + (double)offset.y * travel.y;
            double perpendicular = (double)offset.x * travel.y - (double)offset.y * travel.x;
            double radius = (enemy.boss ? .9f : .5f) + shot.Radius;
            // Rank only contacts already admitted by the original sampled
            // footprint. Signed entry keeps point-blank overlaps in ray order;
            // a nearer center can still have a later, off-axis circle boundary.
            return along - Math.Sqrt(Math.Max(0, radius * radius - perpendicular * perpendicular));
        }
        private void InsertProjectileContact(Projectile shot, Enemy enemy, Point travel)
        {
            int index = projectileTargets.Count;
            if (travel.Length > .0001f)
            {
                double entry = ProjectileContactEntry(shot, enemy, travel);
                while (index > 0)
                {
                    Enemy previous = projectileTargets[index - 1];
                    double previousEntry = ProjectileContactEntry(shot, previous, travel);
                    if (previousEntry < entry || previousEntry == entry && previous.id <= enemy.id) break;
                    index--;
                }
            }
            projectileTargets.Insert(index, enemy);
        }
        private bool ProjectileContact(Projectile shot, bool launching, Point travel, out float impactRadius)
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
            projectileTargets.Clear();
            foreach (Enemy enemy in enemies)
                if (CanProjectileContact(shot, enemy, launching)) InsertProjectileContact(shot, enemy, travel);
            foreach (Enemy enemy in projectileTargets)
            {
                // A prior contact can retire an encounter or remove actors.
                if (!enemies.Contains(enemy) || !CanProjectileContact(shot, enemy, launching)) continue;
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
