using System;
using System.Globalization;

namespace Starfall
{
    public sealed partial class GameCore
    {
        // Read the same authored object and saved flags used by E. This query
        // never selects another target, advances a clock or performs an action.
        public string InteractionHint(WorldObject target, bool english)
        {
            if (target == null || target.kind == null || practicing || Dying
                || Array.IndexOf(Map.objects, target) < 0 || !IsObjectPresent(target)) return "";
            string kind = target.kind;
            if (kind == "waterDraw") return english ? "Fill the garden bucket" : "为药田装一桶清水";
            if (kind == "waterBed") return english ? "Water the trial bed" : "给苗床试灌";
            if (kind == "herb" || kind == "wood" || kind == "ore")
                return Has("node_" + target.id)
                    ? (english ? "Gathered · rest in town to renew" : "已采集 · 回镇休息后更新")
                    : (english ? "Gather supplies" : "可采集");
            if (kind == "garden")
            {
                // E talks to An; planting and harvesting still require their
                // original dialogue choice. Paused world time means no growth.
                if (save.plantedAt < 0) return english ? "Talk · plot empty" : "交谈 · 田地空置";
                float remaining = 60 - (save.totalTime - save.plantedAt);
                if (remaining <= 0) return english ? "Talk · crops ready" : "交谈 · 作物成熟";
                string seconds = Math.Ceiling(remaining).ToString("0", CultureInfo.InvariantCulture);
                return english ? "Talk · crops ready in " + seconds + "s" : "交谈 · 作物还需 " + seconds + "秒";
            }
            int power;
            if (kind.StartsWith("seal", StringComparison.Ordinal)
                && int.TryParse(kind.Substring(4), out power) && power >= 1 && power <= 3)
            {
                if (Has("seal_" + save.room)) return english ? "Route open" : "旧路已开启";
                if (!Ability(power)) return english ? "Requires exploration power from core " + power
                    : "需要第 " + power + " 枚星核能力";
                return english ? "Release the seal" : "可解除封印";
            }
            if (kind.StartsWith("cache", StringComparison.Ordinal))
            {
                if (!Has("seal_" + save.room)) return english ? "Release the entrance seal first" : "先解除入口封印";
                return Has("cache_" + save.room)
                    ? (english ? "Supplies claimed · reread record" : "补给已取 · 可重读记录")
                    : (english ? "Open the supply cache" : "可取补给");
            }
            return "";
        }
    }
}
