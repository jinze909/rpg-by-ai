using System;

namespace Starfall
{
    public sealed partial class FrontierRuntime
    {
        private MaterialSource materialMapSource;
        private GameCore materialMapOwner;
        private SaveData materialMapSave;
        private int materialMapGeneration, materialMapSerial;
        private bool materialRoutePending;

        private MaterialSource MapPreviewMaterialSource
        {
            get
            {
                if (Game == null || Game.mode != "map" || materialMapSource == null
                    || !ReferenceEquals(Game, materialMapOwner) || !ReferenceEquals(Game.save, materialMapSave)
                    || Game.WorldGeneration != materialMapGeneration) return null;
                return Game.GetMaterialRouteSource(materialMapSource);
            }
        }
        private void ClearMaterialMapPreview()
        {
            if (materialMapSource != null) confirmControl = null;
            materialMapSource = null; materialMapOwner = null; materialMapSave = null;
            materialRoutePending = false; materialMapSerial++;
        }
        private void CancelMaterialRouteConfirmation()
        {
            if (!materialRoutePending) return;
            materialRoutePending = false; confirmControl = null;
        }
        private void ValidateMaterialMapPreview()
        {
            if (materialMapSource == null) return;
            MaterialSource source = MapPreviewMaterialSource;
            if (source == null) { ClearMaterialMapPreview(); return; }
            if (selectedRegion != source.room || !Game.CanRouteMaterialSource(source)) CancelMaterialRouteConfirmation();
        }
        private string MaterialMapInputScope
        {
            get { return materialMapSource == null ? "" : "/material-map/" + materialMapSerial + "/" + materialMapGeneration
                + "/" + selectedRegion + "/" + materialRoutePending; }
        }
        private void OpenMaterialDestination(MaterialSource source)
        {
            if (Game.mode != "materials") return;
            MaterialSource current = Game.GetMaterialRouteSource(source);
            if (current == null) return;
            OpenMenu("map"); selectedRegion = current.room;
            materialMapSource = current; materialMapOwner = Game; materialMapSave = Game.save;
            materialMapGeneration = Game.WorldGeneration;
        }
        private bool MaterialMapTargetVisible(MaterialSource source, int room)
        {
            if (source == null || source.room != room || room < 0 || room >= Game.data.maps.Length) return false;
            MapData map = Game.data.maps[room];
            return source.target.x >= 0 && source.target.y >= 0 && source.target.y < map.rows.Length
                && source.target.x < map.rows[(int)source.target.y].Length;
        }
        private void DrawMaterialMapMarker(MaterialSource source, int room, float x, float y, float cell)
        {
            if (!MaterialMapTargetVisible(source, room)) return;
            float px = x + source.target.x * cell, py = y + source.target.y * cell;
            Box(px - 3, py - 3, 7, 7, mint); Box(px - 1, py - 1, 3, 3, ink);
        }
        private void ReturnToMaterialSource(MaterialSource source)
        {
            // The list is current-region-first; restore by authored identity,
            // never by an old row index or a stale pre-travel quote.
            int item = source.item, room = source.room; WorldObject target = source.target;
            OpenMenu("materials"); materialItem = item; materialGeneration = Game.WorldGeneration;
            var sources = Game.MaterialSources(item);
            selectedMaterialSource = Math.Max(0, sources.FindIndex(s => s.room == room && ReferenceEquals(s.target, target)));
            materialPage = selectedMaterialSource / 5;
        }
        private void DrawMaterialMapActions(MaterialSource source)
        {
            if (Button(279, 690, 266, 30, Game.L("返回材料来源", "Back to sources")))
            { ReturnToMaterialSource(source); return; }
            bool remote = source.room != Game.save.room;
            string label = !remote ? Game.L("设为路标", "Set waypoint")
                : materialRoutePending ? Game.L("确认前往并设路标", "Confirm travel + pin") : Game.L("前往并设路标", "Go + waypoint");
            bool enabled = selectedRegion == source.room && Game.CanRouteMaterialSource(source);
            if (!Button(559, 690, 275, 30, label, materialRoutePending, enabled)) return;
            if (remote && !materialRoutePending)
            {
                materialRoutePending = true; confirmControl = null;
                // This is the same enabled destination, now asking for a fresh
                // confirmation. Keep Enter/A on it without replaying the input
                // accepted by the previous scope or jumping to the sidebar.
                drawnScope = InputScope(); navigation.Scope(drawnScope);
                navigation.SetControls(drawnControls); navigation.Select(ControlId(559, 690, 275, 30));
                return;
            }
            if (Game.RouteToMaterialSource(source))
            { ClearMaterialMapPreview(); camera = CameraTarget(); }
            else CancelMaterialRouteConfirmation();
        }
    }
}
