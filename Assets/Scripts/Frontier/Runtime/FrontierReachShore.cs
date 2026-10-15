using UnityEngine;

namespace Starfall
{
    public sealed partial class FrontierRuntime
    {
        private int ReachShoreTopology(int x, int y)
        {
            // Read live presentation cells: a repaired bridge changes its bank
            // immediately, while the closed root road remains solid terrain.
            int mask = 0;
            if (!WaterSurface(Game.PresentationTile(x, y - 1))) mask |= 1;
            if (!WaterSurface(Game.PresentationTile(x + 1, y))) mask |= 2;
            if (!WaterSurface(Game.PresentationTile(x, y + 1))) mask |= 4;
            if (!WaterSurface(Game.PresentationTile(x - 1, y))) mask |= 8;
            if ((mask & 3) == 0 && !WaterSurface(Game.PresentationTile(x + 1, y - 1))) mask |= 16;
            if ((mask & 6) == 0 && !WaterSurface(Game.PresentationTile(x + 1, y + 1))) mask |= 32;
            if ((mask & 12) == 0 && !WaterSurface(Game.PresentationTile(x - 1, y + 1))) mask |= 64;
            if ((mask & 9) == 0 && !WaterSurface(Game.PresentationTile(x - 1, y - 1))) mask |= 128;
            return mask;
        }

        private void DrawReachShore(int x, int y, Point p)
        {
            int mask = ReachShoreTopology(x, y);
            Texture2D shore = art.ReachShore(mask, TileSeed(x, y, GameCore.ReachRoom) & 1);
            if (shore != null) Image(shore, p.x, p.y, Tile, Tile);
        }
    }
}
