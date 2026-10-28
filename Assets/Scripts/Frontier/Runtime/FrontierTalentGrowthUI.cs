using UnityEngine;

namespace Starfall
{
    public sealed partial class FrontierRuntime
    {
        private string TalentGrowthLabel(TalentGrowthStat stat)
        {
            switch (stat)
            {
                case TalentGrowthStat.ContactDamage: return Game.save.weapon == 1 ? Game.L("普攻每箭", "Damage / arrow") : Game.L("普攻首击", "Base contact");
                case TalentGrowthStat.CriticalChance: return Game.L("暴击率", "Critical chance");
                case TalentGrowthStat.MaxHealth: return Game.L("最大生命", "Max health");
                case TalentGrowthStat.IncomingTalentFactor: return Game.L("天赋承伤", "Talent damage taken");
                case TalentGrowthStat.WalkSpeed: return Game.L("移动速度", "Walk speed");
                default: return Game.L("闪避冷却", "Dodge cooldown");
            }
        }
        private static string TalentGrowthAmount(TalentGrowthValue value, bool next)
        {
            float number = next ? value.after : value.before;
            bool percentage = value.stat == TalentGrowthStat.CriticalChance || value.stat == TalentGrowthStat.IncomingTalentFactor;
            return TalentHealth(percentage ? number * 100 : number) + (percentage ? "%" : value.stat == TalentGrowthStat.DodgeCooldown ? "s" : "");
        }
        private void DrawTalentGrowthValue(float x, float y, float valueY, TalentGrowthValue value)
        {
            Text(x, y, 256, 20, TalentGrowthLabel(value.stat), 13, muted, TextAnchor.MiddleCenter);
            Text(x, valueY, 256, 27, TalentGrowthAmount(value, false) + " → " + TalentGrowthAmount(value, true),
                18, value.before == value.after ? paper : mint, TextAnchor.MiddleCenter);
        }
        private void DrawTalentGrowthCard(int talent, float x, string name, string icon)
        {
            TalentGrowthPreview preview = Game.GetTalentGrowthPreview(talent);
            Card(x, 178, 302, 338); Icon(icon, x + 111, 194, 80);
            Text(x + 23, 288, 256, 40, name + " " + (preview == null ? "-" : preview.rank.ToString()) + "/5", 27, gold, TextAnchor.MiddleCenter);
            if (preview == null) Text(x + 23, 350, 256, 76, Game.L("成长状态暂不可用", "Growth values unavailable"), 17, muted, TextAnchor.MiddleCenter);
            else { DrawTalentGrowthValue(x + 23, 333, 354, preview.first); DrawTalentGrowthValue(x + 23, 386, 408, preview.second); }
            for (int n = 0; n < 5; n++) Box(x + 62 + n * 37, 442, 26, 5, preview != null && n < preview.rank ? gold : PixelArt.C("#43566b"));
            string spend = preview != null && preview.rank == 5 ? Game.L("已满级", "Max rank") : Game.L("投入1技能点", "Spend 1 point");
            if (Button(x + 18, 475, 266, 29, spend, false, preview != null && preview.canLearn)) Game.Learn(talent);
        }
    }
}
