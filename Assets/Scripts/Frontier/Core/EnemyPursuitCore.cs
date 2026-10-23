namespace Starfall
{
    public sealed partial class GameCore
    {
        private bool EnemyPursuitDormant(Enemy enemy, float distance)
        {
            // Keep chapter guardians and every existing training drill's exact
            // activation rule. Their committed attacks are handled before here.
            if (enemy.boss || practicing)
            { enemy.pursuitAlerted = enemy.returningToPost = false; return distance > (enemy.boss ? 18 : 8); }
            if (!Finite(distance)) { enemy.pursuitAlerted = enemy.returningToPost = false; return true; }
            // A legal route around water or a wall may briefly lead away from
            // the player. Remember acquisition, with a bounded escape distance;
            // this does not alter movement, line of sight or attack reach.
            if (distance <= 8)
            { enemy.pursuitAlerted = true; enemy.returningToPost = false; enemy.returnStalledTime = 0; }
            else if (distance > 10)
            {
                if (enemy.pursuitAlerted && enemy.returnEnabled)
                { enemy.returningToPost = true; enemy.returnStalledTime = 0; }
                enemy.pursuitAlerted = false;
            }
            return !enemy.pursuitAlerted;
        }
    }
}
