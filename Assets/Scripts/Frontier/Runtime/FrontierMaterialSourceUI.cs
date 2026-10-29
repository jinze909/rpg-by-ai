using System;
using UnityEngine;

namespace Starfall
{
    public sealed partial class FrontierRuntime
    {
        private static readonly int[] MaterialItems = { 0, 1, 2, 3, 4, 7 };
        private int materialItem, materialPage, selectedMaterialSource, materialGeneration = -1;
        private readonly RectOffset materialSourcePadding = new RectOffset(8, 8, 2, 2);
        private bool CanOpenMaterialSources
        { get { return !Game.Dying && !Game.practicing && !Game.SafeTrainingActive && !Game.InAbyss; } }

        private void OpenMaterialSources(int item)
        {
            if (Game.mode != "inventory" || !CanOpenMaterialSources) return;
            materialItem = Array.IndexOf(MaterialItems, item) >= 0 ? item : 0;
            materialPage = selectedMaterialSource = 0; materialGeneration = Game.WorldGeneration;
            OpenMenu("materials");
        }

        private bool MaterialSourceRow(float y, string label, bool selected)
        {
            RectOffset normal = buttonStyle.padding, disabled = disabledButtonStyle.padding;
            try
            {
                buttonStyle.padding = disabledButtonStyle.padding = materialSourcePadding;
                return Button(279, y, 535, 62, label, selected);
            }
            finally { buttonStyle.padding = normal; disabledButtonStyle.padding = disabled; }
        }

        private void DrawMaterialSources()
        {
            if (!CanOpenMaterialSources) { OpenMenu("inventory"); return; }
            if (materialGeneration != Game.WorldGeneration)
            { materialPage = selectedMaterialSource = 0; materialGeneration = Game.WorldGeneration; }
            if (Array.IndexOf(MaterialItems, materialItem) < 0) materialItem = 0;
            var sources = Game.MaterialSources(materialItem);
            int pages = Math.Max(1, (sources.Count + 4) / 5);
            materialPage = Math.Max(0, Math.Min(pages - 1, materialPage));
            selectedMaterialSource = Math.Max(0, Math.Min(Math.Max(0, sources.Count - 1), selectedMaterialSource));
            Window(Game.L("材料来源", "Material sources"), Game.L(Game.data.items[materialItem].name, Game.data.items[materialItem].en)
                + " × " + Game.save.inventory[materialItem] + Game.L(" · 已探索来源；查看不会采集或交易。", " · charted sources; viewing never gathers or trades."));
            if (Game.mode != "materials") return;
            for (int i = 0; i < MaterialItems.Length; i++)
            {
                int item = MaterialItems[i];
                if (Button(279 + i * 158, 177, 148, 42, Game.L(Game.data.items[item].name, Game.data.items[item].en), materialItem == item) && materialItem != item)
                { materialItem = item; materialPage = selectedMaterialSource = 0; return; }
            }
            Card(279, 235, 535, 381);
            if (sources.Count == 0)
                Text(303, 269, 487, 167, Game.L("还没有已探索的来源。沿开放地区的道路探索，或与镇民交谈；未探索地点不会列出。", "No sources charted yet. Explore open regions or meet townsfolk. Unexplored locations stay hidden."), 18, muted);
            for (int row = 0; row < 5 && materialPage * 5 + row < sources.Count; row++)
            {
                int index = materialPage * 5 + row; MaterialSource source = sources[index];
                MapData map = Game.data.maps[source.room];
                string label = Game.L(map.name, map.en) + " · " + Game.L(source.target.name, source.target.en) + "\n" + source.method
                    + (source.depleted ? Game.L(" · 已采完", " · Gathered") : "");
                if (MaterialSourceRow(241 + row * 72, label, selectedMaterialSource == index)) selectedMaterialSource = index;
            }
            if (Button(279, 625, 160, 32, Game.L("上一页", "Previous page"), false, materialPage > 0))
            { materialPage--; selectedMaterialSource = materialPage * 5; return; }
            Text(453, 628, 188, 26, (materialPage + 1) + " / " + pages, 15, muted, TextAnchor.MiddleCenter);
            if (Button(654, 625, 160, 32, Game.L("下一页", "Next page"), false, materialPage + 1 < pages))
            { materialPage++; selectedMaterialSource = materialPage * 5; return; }
            Text(291, 674, 511, 28, Game.L("沿路探索新来源；旅店休息可更新采集点。", "Explore to chart sources; inn rest renews gathered nodes."), 13, muted);

            MaterialSource selected = sources.Count == 0 ? null : sources[selectedMaterialSource];
            Card(838, 235, 379, 477);
            if (selected != null)
            {
                MapData map = Game.data.maps[selected.room];
                Text(860, 247, 334, 30, Game.L(map.name, map.en), 19, gold);
                Text(860, 283, 334, 56, Game.L(selected.target.name, selected.target.en), 20, paper);
                Text(860, 346, 334, 24, selected.method, 15, mint);
                Text(860, 377, 334, 100, selected.detail, 14, paper);
                string hint = selected.room != Game.save.room
                    ? Game.L("在地区地图查看来源，也可确认前往并设路标；任务追踪保留。", "Preview this source on its region map, or confirm travel and a waypoint. Your quest stays tracked.")
                    : selected.depleted ? Game.L("这一处已采完。到旅店休息后，再回来查看。", "This node is gathered. Rest at the inn, then check again.")
                    : LandmarkArrivalHint(selected.target, ReferenceEquals(Game.Nearest(), selected.target) || Game.InteractionPath(selected.target).Count > 0);
                Text(860, 487, 334, 72, hint, 13, muted);
            }
            else Text(860, 283, 334, 80, Game.L("选择一个已知来源，查看实际所得与取得方式。", "Select a charted source to review its yield and how to use it."), 18, paper);
            bool pinned = selected != null && selected.room == Game.save.room && ReferenceEquals(Game.PinnedLandmark, selected.target);
            if (Button(866, 572, 351, 38, pinned ? Game.L("当前路标", "Current waypoint") : Game.L("设为路标", "Set waypoint"), pinned, selected != null && Game.CanPinMaterialSource(selected)))
                Game.PinMaterialSource(selected);
            if (Button(866, 622, 351, 36, Game.L("查看地区地图", "Show region map"), false, selected != null))
            { OpenMaterialDestination(selected); }
            if (Button(866, 670, 351, 28, CanReturnToSupplyDialogue ? Game.L("返回对话", "Back to dialogue") : Game.L("返回背包", "Back to inventory"))) ReturnFromMaterialSources();
        }
    }
}
