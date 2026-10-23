using System;
using System.Collections.Generic;

namespace Starfall
{
    public sealed partial class GameCore
    {
        // Only ordinary, authored regional placements opt in. Task encounters,
        // reinforcements and practice actors retain their existing AI.
        private void EnableEnemyReturn(Enemy enemy)
        {
            if (enemy == null || enemy.boss || !CanStand(enemy.Position)) return;
            enemy.returnEnabled = true; enemy.returnPost = enemy.Position;
        }
        private void RestoreEnemyReturn(Enemy enemy, List<Enemy> placements)
        {
            Enemy placement = placements.Find(e => e.returnEnabled && e.id == enemy.id && e.kind == enemy.kind && !e.boss);
            if (placement == null || enemy.boss || !CanStand(placement.returnPost)) return;
            enemy.returnEnabled = true; enemy.returnPost = placement.returnPost;
            // Keep the saved feet/health and the established fresh awareness.
            // The next live pursuit can return to the same authored post.
        }
        public bool EnemyReturningToPost(Enemy enemy)
        {
            return enemy != null && mode == "world" && !Dying && !practicing && !InAbyss
                && enemies.Contains(enemy) && !enemy.boss && enemy.hp > 0
                && enemy.returnEnabled && enemy.returningToPost;
        }
        private void TickEnemyReturn(Enemy enemy, float dt)
        {
            if (!enemy.returnEnabled || !enemy.returningToPost) return;
            Point before = enemy.Position; float remaining = (enemy.returnPost - before).Length;
            if (!Finite(remaining) || remaining <= .08f || !CanStand(enemy.returnPost) || !CanStand(before))
            { enemy.returningToPost = false; enemy.returnStalledTime = 0; return; }
            float speed = enemy.kind == 0 ? 2.1f : 1.5f;
            Point step = DirectionTo(before, enemy.returnPost) * Math.Min(remaining, speed * dt);
            Point next = SweepActor(before, step); enemy.x = next.x; enemy.y = next.y;
            Point travelled = next - before; enemy.moving = travelled.Length > .001f;
            if (enemy.moving)
            {
                enemy.moveTime += travelled.Length; enemy.Face(travelled); enemy.returnStalledTime = 0;
                if ((enemy.returnPost - next).Length <= .08f) enemy.returningToPost = false;
            }
            else
            {
                // A changed/closed route must not warp or endlessly grind the
                // actor into a wall. A fresh acquisition may try again later.
                enemy.returnStalledTime += dt;
                if (enemy.returnStalledTime >= 1) { enemy.returningToPost = false; enemy.returnStalledTime = 0; }
            }
        }
    }
}
