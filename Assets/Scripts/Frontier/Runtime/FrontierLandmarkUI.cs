using System;
using UnityEngine;

namespace Starfall
{
    public sealed partial class FrontierRuntime
    {
        private int selectedLandmark, landmarkPage;
        private bool CanOpenLandmarks { get { return selectedRegion == Game.save.room && !Game.practicing && !Game.InAbyss && !Game.Dying; } }

        private void OpenLandmarks()
        {
            if (Game.mode != "map" || !CanOpenLandmarks) return;
            var places = Game.DiscoveredLandmarks();
            selectedLandmark = Math.Max(0, places.IndexOf(Game.PinnedLandmark));
            landmarkPage = selectedLandmark / 6;
            OpenMenu("landmarks");
        }

        private string LandmarkDistance(WorldObject place)
        {
            int distance = (int)Math.Ceiling((new Point(place.x, place.y) - Game.Position).Length);
            return Game.L("直线距离 ", "Direct distance: ") + distance + Game.L(" 格", " tiles");
        }

        private string LandmarkArrivalHint(WorldObject place, bool reachable)
        {
            if ((new Point(place.x, place.y) - Game.Position).Length < 1.75f)
                return Game.L("已在附近。返回地区，按 ", "In reach. Return and press ") + ControlKey("E", "B") + Game.L(" 交互。", " to interact.");
            return reachable
                ? Game.L("路线通往附近。到达后按 ", "Follow the route, then press ") + ControlKey("E", "B") + Game.L(" 交互。", " to interact.")
                : Game.L("当前无步行路线。检查潮水、门或断桥。", "No walking route now. Check tides, gates or bridges.");
        }

        private string LandmarkHudHint(WorldObject place)
        {
            Point destination = new Point(place.x, place.y);
            if ((destination - Game.Position).Length < 1.75f) return Game.L("已到达附近 · ", "In reach · ") + ControlKey("E", "B") + Game.L(" 交互", " to interact");
            if (routeGoal.HasValue && SameGoal(routeGoal, destination) && route.Count == 0)
                return Game.L("当前无步行路线。检查潮水、门或断桥。", "No walking route. Check tides, gates or bridges.");
            return LandmarkDistance(place) + Game.L("\n任务仍在手帐中追踪。", "\nYour quest stays tracked.");
        }

        private void DrawLandmarks()
        {
            if (Game.practicing || Game.InAbyss || Game.Dying) { OpenMenu("map"); return; }
            var places = Game.DiscoveredLandmarks();
            int pages = Math.Max(1, (places.Count + 5) / 6);
            landmarkPage = Math.Max(0, Math.Min(pages - 1, landmarkPage));
            selectedLandmark = Math.Max(0, Math.Min(Math.Max(0, places.Count - 1), selectedLandmark));
            WorldObject selected = places.Count == 0 ? null : places[selectedLandmark];
            WorldObject pin = Game.PinnedLandmark;
            Window(Game.L("本地路标", "Local landmarks"), Game.L("只显示已探索地点 · 自选路线不改变任务追踪", "Explored places only · a waypoint keeps your quest tracked"));
            Card(279, 178, 535, 438);
            Text(299, 190, 495, 26, Game.L(Game.Map.name, Game.Map.en) + "  ·  " + places.Count, 17, paper);
            if (places.Count == 0)
                Text(305, 261, 477, 135, Game.L("这一带还没有已探索的地点。回到地区，沿路调查后再查看地图。", "No landmarks charted here yet. Explore the region, then return to the map."), 19, muted);
            for (int row = 0; row < 6 && landmarkPage * 6 + row < places.Count; row++)
            {
                int index = landmarkPage * 6 + row;
                WorldObject place = places[index];
                string title = (ReferenceEquals(pin, place) ? "◆ " : "") + Game.L(place.name, place.en);
                if (Button(279, 225 + row * 64, 535, 54, title, selectedLandmark == index)) selectedLandmark = index;
            }
            if (Button(279, 625, 160, 32, Game.L("上一页", "Previous page"), false, landmarkPage > 0)) { landmarkPage--; selectedLandmark = landmarkPage * 6; }
            Text(453, 628, 188, 26, (landmarkPage + 1) + " / " + pages, 15, muted, TextAnchor.MiddleCenter);
            if (Button(654, 625, 160, 32, Game.L("下一页", "Next page"), false, landmarkPage + 1 < pages)) { landmarkPage++; selectedLandmark = landmarkPage * 6; }
            Text(291, 670, 511, 28, Game.L("采集点不列入，地点随剧情更新。", "Gathering nodes omitted. Places follow the story."), 14, muted);

            Card(838, 178, 379, 534);
            Text(860, 193, 334, 62, selected == null ? Game.L("选择一个地点", "Choose a place") : Game.L(selected.name, selected.en), 22, paper);
            MiniTerrain(888, 274, 8.4f);
            if (selected != null)
            {
                float cell = MapCellSize(Game.Map, 8.4f), x = 888 + selected.x * cell, y = 274 + selected.y * cell;
                Box(x - 5, y - 5, 11, 2, mint); Box(x - 5, y + 4, 11, 2, mint);
                Box(x - 5, y - 5, 2, 11, mint); Box(x + 4, y - 5, 2, 11, mint);
                bool reachable = (new Point(selected.x, selected.y) - Game.Position).Length < 1.75f || Game.NavigationPath(new Point(selected.x, selected.y)).Count > 0;
                Text(860, 487, 334, 27, LandmarkDistance(selected), 15, mint);
                Text(860, 520, 334, 43, LandmarkArrivalHint(selected, reachable), 13, muted);
            }
            if (Button(866, 572, 351, 42, ReferenceEquals(pin, selected) && selected != null ? Game.L("当前路标", "Current waypoint") : Game.L("设为路标", "Set waypoint"), selected != null && ReferenceEquals(pin, selected), selected != null && !ReferenceEquals(pin, selected)))
                Game.PinLandmark(selected);
            if (Button(866, 622, 351, 36, Game.L("清除路标，恢复任务路线", "Clear pin; restore quest route"), false, Game.HasLandmarkPin)) Game.ClearLandmarkPin();
            if (Button(866, 670, 351, 28, Game.L("返回地图", "Back to map"))) OpenMenu("map");
        }
    }
}
