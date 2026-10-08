using System;
namespace Starfall
{
    public sealed partial class GameCore
    {
        // Searches are synchronous. Share workspace, never the returned route:
        // the HUD can keep a route while several enemies find their next step.
        private int[] pathParents = new int[0], pathQueue = new int[0];
        private static readonly int[,] pursuitSteps = { { 1, 0 }, { -1, 0 }, { 0, 1 }, { 0, -1 } };
        private static readonly int[,] guidanceSteps = { { 1, 0 }, { 0, 1 }, { -1, 0 }, { 0, -1 } };
        private void PreparePathSearch(int cells, int unseen)
        {
            if (pathParents.Length < cells) { pathParents = new int[cells]; pathQueue = new int[cells]; }
            for (int i = 0; i < cells; i++) pathParents[i] = unseen;
        }
    }
}
