using System;
using System.Collections.Generic;
using System.Globalization;

namespace Starfall
{
    // A read-only, localized view of an authored place. It grants neither the
    // material nor exploration, travel, or authority to perform its exchange.
    public sealed class MaterialSource
    {
        public readonly int item, room;
        public readonly WorldObject target;
        public readonly bool depleted;
        public readonly string method, detail;
        internal readonly SaveData owner;
        internal readonly int generation;

        internal MaterialSource(int item, int room, WorldObject target, bool depleted,
            string method, string detail, SaveData owner, int generation)
        {
            this.item = item; this.room = room; this.target = target; this.depleted = depleted;
            this.method = method; this.detail = detail; this.owner = owner; this.generation = generation;
        }
    }

    public sealed partial class GameCore
    {
        private bool CanReadMaterialSources
        { get { return !Dying && !practicing && !SafeTrainingActive && !InAbyss && save.room != 8; } }

        private static bool MaterialItem(int item)
        { return item == 0 || item == 1 || item == 2 || item == 3 || item == 4 || item == 7; }

        private bool MaterialKind(int item, int room, WorldObject target)
        {
            string kind = target.kind;
            if (item == 0) return kind == "wood" || kind == "cook";
            if (item == 1 || item == 4) return kind == "ore";
            if (item == 2) return kind == "herb" || kind == "garden";
            if (item == 7) return kind == "garden";
            return item == 3 && (kind == "fish" || room == ReachRoom && RiverLessonAccepted &&
                (kind == "riverFishing0" || kind == "riverFishing1"));
        }

        private string MaterialGardenDetails(int item)
        {
            int herbs = GardenHerbYield;
            string result = L("种植耗种子1，60秒后：药草+" + herbs + "、种子+2、硬币+8。",
                "Plant 1 seed; after 60s: herbs +" + herbs + ", seeds +2, coins +8.");
            if (item == 7) result = L("5硬币买种子3。", "Buy 3 seeds for 5 coins. ") + result;
            if (save.plantedAt >= 0)
            {
                float remaining = Math.Max(0, 60 - (save.totalTime - save.plantedAt));
                string seconds = Math.Ceiling(remaining).ToString(CultureInfo.InvariantCulture);
                result += remaining > 0 ? L("还需" + seconds + "秒。", " Ready in " + seconds + "s.") : L("可收获。", " Ready to harvest.");
            }
            return result;
        }

        private MaterialSource DescribeMaterialSource(int item, int room, WorldObject target)
        {
            bool gathering = GatheringLandmark(target), depleted = gathering && Has("node_" + target.id);
            string method, detail;
            if (gathering)
            {
                method = L("采集", "Gather");
                detail = target.kind == "wood" ? L("木材+3。", "Timber +3. ") : target.kind == "herb" ?
                    L("药草+3。", "Herbs +3. ") : L("星晶+3、零件+3。", "Crystals +3 and parts +3. ");
                detail += depleted ? L("已采集，到旅店休息更新资源。", "Gathered; rest at the inn to renew this node.") :
                    L("到达后交互采集；旅店休息更新资源。", "Interact there to gather; inn rest renews nodes.");
            }
            else if (target.kind == "cook")
            {
                method = L("购买", "Buy");
                detail = L(ShopTimberCost + "硬币买木材4，需与店主交谈。", "Buy 4 timber for " + ShopTimberCost + " coins; speak to the cook.");
            }
            else if (target.kind == "garden")
            { method = item == 7 ? L("购买 / 种植", "Buy / grow") : L("种植", "Grow"); detail = MaterialGardenDetails(item); }
            else
            {
                method = L("钓鱼", "Fish");
                detail = L("成功收线3次：银鳍鱼+1、硬币最多+4，不耗鱼饵。", "Three good pulls: Silverfin +1, coins +4 at most; no bait cost.");
            }
            return new MaterialSource(item, room, target, depleted, method, detail, save, WorldGeneration);
        }

        public List<MaterialSource> MaterialSources(int item)
        {
            var result = new List<MaterialSource>();
            if (!CanReadMaterialSources || !MaterialItem(item)) return result;
            // Put the current region first without disturbing authored order
            // within any region. Locked and uncharted places stay undisclosed.
            for (int pass = 0; pass < 2; pass++)
                for (int room = 0; room < data.maps.Length; room++)
                {
                    if ((room == save.room) != (pass == 0) || room == 8 || !CanEnter(room)) continue;
                    foreach (WorldObject target in data.maps[room].objects)
                    {
                        if (!LandmarkEligible(room, target) || !MaterialKind(item, room, target) ||
                            !Explored(room, target.x, target.y)) continue;
                        // IsObjectPresent includes a current-room check for
                        // river lessons; remote lesson sites use saved access.
                        if (room == save.room && !IsObjectPresent(target)) continue;
                        result.Add(DescribeMaterialSource(item, room, target));
                    }
                }
            return result;
        }

        public bool CanPinMaterialSource(MaterialSource source)
        {
            if (mode != "materials" || !CanReadMaterialSources || source == null || source.depleted ||
                source.room != save.room || !ReferenceEquals(source.owner, save) || source.generation != WorldGeneration) return false;
            bool available = false;
            foreach (MaterialSource current in MaterialSources(source.item))
                if (current.room == source.room && ReferenceEquals(current.target, source.target) && !current.depleted)
                { available = true; break; }
            if (!available) return false;
            string flag = LandmarkPinPrefix + save.room.ToString(CultureInfo.InvariantCulture) + ":" + source.target.id;
            return landmarkPinFlag != flag || !HasLandmarkPin;
        }

        public bool PinMaterialSource(MaterialSource source)
        { return CanPinMaterialSource(source) && SetLandmarkPinCore(source.target); }
    }
}
