using System;

namespace Starfall
{
    // Preparation meals connect the existing garden, fishing and supply loop.
    // One timed choice supplements, rather than replaces, stew and tea.
    public sealed partial class GameCore
    {
        public const float RoadMealSeconds = 180;
        public int ActiveRoadMeal
        { get { return save.mealKind >= 1 && save.mealKind <= 3 && Finite(save.mealUntil) && save.mealUntil > save.totalTime ? save.mealKind : 0; } }
        public bool RoadMealActive { get { return ActiveRoadMeal > 0; } }
        public float RoadMealRemaining { get { return RoadMealActive ? Math.Max(0, save.mealUntil - save.totalTime) : 0; } }
        public float RoadMealEnergyBonus { get { return ActiveRoadMeal == 1 ? 3 : 0; } }
        public float RoadMealWalkBonus { get { return ActiveRoadMeal == 3 && !racing ? .3f : 0; } }
        public string RoadMealName(int kind)
        {
            return L(RoadMealNameZh(kind), RoadMealNameEn(kind));
        }
        private static string RoadMealNameZh(int kind)
        { return kind == 1 ? "药草鱼餐" : kind == 2 ? "炉火鱼汤" : kind == 3 ? "星路清餐" : "未备远行餐"; }
        private static string RoadMealNameEn(int kind)
        { return kind == 1 ? "Moonherb fish" : kind == 2 ? "Hearth broth" : kind == 3 ? "Starpath greens" : "No road meal"; }
        public string RoadMealStatus
        { get { return RoadMealActive ? RoadMealName(ActiveRoadMeal) + " · " + (int)Math.Ceiling(RoadMealRemaining) + "s" : RoadMealName(0); } }
        public float RoadMealDamageMultiplier(string source)
        { return ActiveRoadMeal == 2 && (source == "shot" || source == "slam" || source == "lunge" || source == "area") ? .9f : 1; }
        private void SanitizeRoadKitchen()
        {
            if (save.mealKind < 1 || save.mealKind > 3 || !Finite(save.mealUntil) || save.mealUntil <= save.totalTime)
            { save.mealKind = 0; save.mealUntil = 0; return; }
            save.mealUntil = Math.Min(save.mealUntil, save.totalTime + RoadMealSeconds);
        }
        private bool NearRoadKitchen()
        {
            foreach (WorldObject o in Map.objects)
            {
                if (!IsObjectPresent(o) || (Position - new Point(o.x, o.y)).Length >= 1.75f) continue;
                if (o.kind == "cook") return true;
                if (o.kind == "campfire" && save.projects[1] > 0 && SafeTravel) return true;
            }
            return false;
        }
        public void ShowRoadKitchen()
        {
            if (mode != "world" || Dying || practicing || !NearRoadKitchen())
            { Notify("在旅店，或有驿站补给的安全营地备餐。", "Prepare meals at the inn, or a safe camp supplied by a waystation."); return; }
            string zh = "阿茴：带上药田与湖边的力量，汤茶照常用。\n药草鱼餐：草2鱼1，耐力回复+3/秒。\n炉火鱼汤：木1鱼1，战斗伤害-10%，暑寒不变。\n星路清餐：草2晶1，步行+0.3，闪避与竞速不变。\n180秒一餐；再做付完整材料，更新或替换；不治疗或补耐力。\n当前：" + RoadMealStatus + "\n材料：木材" + save.inventory[0] + "、星晶" + save.inventory[1] + "、药草" + save.inventory[2] + "、鱼" + save.inventory[3];
            string en = "Ari: garden and lake feed the road. Stew and tea still work.\nMoonherb fish: herbs2 fish1; energy recovery +3/s.\nHearth broth: timber1 fish1; combat damage -10%, climate unchanged.\nStarpath greens: herbs2 crystal1; walk +0.3, dash/race unchanged.\n180s; one meal. Re-cook: full cost, refresh/replace; no heal/refill.\nCurrent: " + RoadMealStatus + "\nStock: timber " + save.inventory[0] + ", crystal " + save.inventory[1] + ", herbs " + save.inventory[2] + ", fish " + save.inventory[3];
            Dialog("阿茴的远行厨房", "Ari's Road Kitchen", zh, en,
                C("备药草鱼餐 · 草2 鱼1", "Moonherb fish · herbs2 fish1", "kitchen_cook_1"),
                C("备炉火鱼汤 · 木1 鱼1", "Hearth broth · timber1 fish1", "kitchen_cook_2"),
                C("备星路清餐 · 草2 晶1", "Starpath greens · herbs2 crystal1", "kitchen_cook_3"),
                C("稍后再做", "Prepare later", "close"));
        }
        public bool CookRoadMeal(int kind)
        {
            if (kind < 1 || kind > 3 || mode != "world" || Dying || practicing || !NearRoadKitchen()) return false;
            bool paid = kind == 1 ? Spend(0, 0, 2, 1, 0) : kind == 2 ? Spend(1, 0, 0, 1, 0) : Spend(0, 1, 2, 0, 0);
            if (!paid) return false;
            save.mealKind = kind; save.mealUntil = save.totalTime + RoadMealSeconds;
            string record = "kitchen_recipe_" + kind;
            if (!Has(record))
            {
                Flag(record);
                RecordMoment("学会阿茴的远行餐：" + RoadMealNameZh(kind) + "。药田与湖边的收成，成了路上的力量。", "Learned Ari's road recipe: " + RoadMealNameEn(kind) + ". The garden and lake now help carry the journey.");
            }
            revision++; sound = "reward";
            Notify("远行餐已备好：" + RoadMealStatus + "。", "Road meal prepared: " + RoadMealStatus + ".");
            return true;
        }
        private bool ChooseKitchen(string action)
        {
            if (action == "kitchen") { ShowRoadKitchen(); return true; }
            if (action == null || !action.StartsWith("kitchen_cook_")) return false;
            int kind;
            if (int.TryParse(action.Substring(13), out kind)) CookRoadMeal(kind);
            return true;
        }
    }
}
