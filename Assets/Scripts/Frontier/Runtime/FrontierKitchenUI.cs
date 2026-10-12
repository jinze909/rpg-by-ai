using UnityEngine;

namespace Starfall
{
    public sealed partial class FrontierRuntime
    {
        // Keep preparation status below the enemy tell and away from climate,
        // toasts and action controls in both world HUD arrangements.
        private Rect RoadMealHudBounds(bool compact)
        { return new Rect(compact ? 20 : 24, 324, compact ? 260 : 298, 54); }

        private string RoadMealBenefit()
        {
            return Game.ActiveRoadMeal == 1 ? Game.L("耐力回复 +3/秒", "Energy recovery +3/s")
                : Game.ActiveRoadMeal == 2 ? Game.L("战斗伤害 -10%", "Combat damage -10%")
                : Game.RoadMealWalkBonus > 0 ? Game.L("行走速度 +0.3", "Walking speed +0.3") : Game.L("竞速时速度不变", "Race speed unchanged");
        }

        private void DrawRoadMealHud(bool compact)
        {
            if (!Game.RoadMealActive || Game.mode != "world") return;
            Rect r = RoadMealHudBounds(compact);
            Panel(r.x,r.y,r.width,r.height,new Color(.045f,.08f,.13f,compact ? .6f : .85f),new Color(mint.r,mint.g,mint.b,.35f));
            Icon("stew",r.x+9,r.y+9,25);
            Text(r.x+41,r.y+7,r.width-51,23,Game.RoadMealStatus,12,gold);
            Text(r.x+10,r.y+32,r.width-20,19,RoadMealBenefit(),11,mint);
        }

        private void DrawRoadMealInventory()
        {
            // Use the gap above weapon comparison, where reward toasts cannot
            // cover the meal immediately after it has been prepared.
            Card(279,420,619,24);
            Icon("stew",285,422,20);
            if (Game.RoadMealActive)
            {
                Text(313,422,285,21,Game.RoadMealStatus,13,gold);
                Text(607,422,281,21,RoadMealBenefit(),13,mint);
            }
            else Text(313,422,574,21,Game.L("远行餐：找阿茴，或到有补给的安全营地。", "Road meal: visit Ari or a safe camp supplied by a waystation."),13,muted);
        }
    }
}
