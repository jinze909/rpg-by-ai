using System;

namespace Starfall
{
    // Read-only presentation of the existing twenty-point mastery milestones.
    public sealed class WeaponMasteryProgress
    {
        public readonly int weapon, points, rank, nextThreshold, remaining, baseDamageBonus;
        public readonly bool maximum, stylesUnlocked;
        public readonly float fraction;
        internal WeaponMasteryProgress(int weapon, int points, bool firstCore)
        {
            this.weapon = weapon; this.points = points; rank = points / 20;
            maximum = rank == 5; nextThreshold = maximum ? 100 : (rank + 1) * 20;
            remaining = maximum ? 0 : nextThreshold - points;
            baseDamageBonus = rank * 2; stylesUnlocked = firstCore || rank >= 1;
            fraction = maximum ? 1 : (points - rank * 20) / 20f;
        }
    }

    public sealed partial class GameCore
    {
        public WeaponMasteryProgress GetWeaponMasteryProgress(int weapon)
        {
            if (weapon < 0 || weapon > 2) return null;
            int points = save.mastery != null && weapon < save.mastery.Length ? save.mastery[weapon] : 0;
            return new WeaponMasteryProgress(weapon, Math.Max(0, Math.Min(100, points)), Has("core_1"));
        }
        public string WeaponMasteryProgressText(int weapon)
        {
            WeaponMasteryProgress progress = GetWeaponMasteryProgress(weapon);
            if (progress == null) return "";
            return L("熟练度 ", "Mastery ") + progress.rank + "/5 · " + progress.points + "/100"
                + (progress.maximum ? L(" · 已满级", " · MAX") : L(" · 下一级还需 ", " · Next in ") + progress.remaining + L(" 点", " pts"));
        }
    }
}
