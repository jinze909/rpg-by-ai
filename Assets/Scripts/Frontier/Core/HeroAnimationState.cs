using System;

namespace Starfall
{
    // One authored whole-body cell. Simulation clocks remain the only authority.
    public struct HeroVisualFrame
    {
        public readonly string atlas;
        public readonly int frame;
        public readonly bool flip;
        public HeroVisualFrame(string atlas, int frame, bool flip)
        { this.atlas = atlas; this.frame = frame; this.flip = flip; }
    }

    public static class HeroAnimationState
    {
        public const int CellSize = 256;
        public const int FootX = 128, FootY = 240;
        public static readonly string[] Atlases = { "PlayerVanguard", "PlayerRanger", "PlayerArcanist",
            "WalkVanguard", "WalkRanger", "WalkArcanist", "PlayerRear", "PlayerSouth", "PlayerReactionsRear", "PlayerReactionsSouth" };

        public static HeroVisualFrame Select(GameCore game)
        {
            if (game == null) throw new ArgumentNullException("game");
            int weapon = game.AppearanceWeapon, pose = game.PlayerPose;
            Point direction = game.VisualFacing;
            int row = GameCore.DirectionRow(direction);
            string atlas = Atlases[weapon]; int frame = pose;
            if (pose == 2 || pose == 3)
            { atlas = Atlases[3 + weapon]; frame = row * 4 + game.WalkFrame; }
            else if (pose <= 1)
            {
                frame = row == 1 ? 1 : 0;
                if (row == 2) { atlas = Atlases[3 + weapon]; frame = 8; }
            }
            else if (pose >= 4 && pose <= 7 && row == 1)
            { atlas = "PlayerRear"; frame = weapon * 4 + (pose == 4 || pose == 6 ? 2 : 3); }
            else if (pose >= 4 && pose <= 7 && row == 2)
            { atlas = "PlayerSouth"; frame = weapon * 4 + pose - 4; }
            else if (pose >= 8 && row != 0)
            { atlas = row == 1 ? "PlayerReactionsRear" : "PlayerReactionsSouth"; frame = weapon * 4 + (pose == 8 ? 0 : pose == 9 ? 1 : pose == 10 ? 2 : 3); }
            return new HeroVisualFrame(atlas, frame, row == 0 && direction.x < -.1f);
        }
    }
}
