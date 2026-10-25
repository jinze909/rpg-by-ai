using System;
using System.Collections.Generic;
using System.Globalization;

namespace Starfall
{
    // A personal route is a reference to an already discovered map object, not
    // a new quest or a coordinate which can teleport or reveal hidden places.
    public sealed partial class GameCore
    {
        private const string LandmarkPinPrefix = "landmark_pin:";
        private int landmarkPinRoom = -1;
        private string landmarkPinId;
        private string landmarkPinFlag;

        private bool CanManageLandmarkPin
        { get { return mode == "landmarks" && !Dying && !practicing && !InAbyss && save.room != 8; } }

        private static bool GatheringLandmark(WorldObject obj)
        { return obj != null && (obj.kind == "wood" || obj.kind == "herb" || obj.kind == "ore"); }

        private bool LandmarkEligible(int room, WorldObject obj)
        {
            if (room < 0 || room >= data.maps.Length || room == 8 || obj == null ||
                string.IsNullOrWhiteSpace(obj.id) || obj.id.IndexOf(':') >= 0 ||
                string.IsNullOrWhiteSpace(obj.kind) ||
                string.IsNullOrWhiteSpace(obj.name) && string.IsNullOrWhiteSpace(obj.en) ||
                !Finite(obj.x) || !Finite(obj.y)) return false;
            MapData map = data.maps[room];
            if (obj.x < 0 || obj.y < 0 || obj.y >= map.rows.Length || obj.x >= map.rows[(int)obj.y].Length) return false;
            int matching = 0;
            foreach (WorldObject candidate in map.objects)
                if (candidate != null && candidate.id == obj.id) matching++;
            return matching == 1;
        }

        private WorldObject StoredLandmark(int room, string id)
        {
            if (room < 0 || room >= data.maps.Length || room == 8 || !CanEnter(room) || string.IsNullOrEmpty(id)) return null;
            foreach (WorldObject obj in data.maps[room].objects)
                if (obj != null && obj.id == id && LandmarkEligible(room, obj) && Explored(room, obj.x, obj.y) &&
                    (!GatheringLandmark(obj) || !Has("node_" + obj.id) && IsObjectPresent(obj))) return obj;
            return null;
        }

        public List<WorldObject> DiscoveredLandmarks()
        {
            var result = new List<WorldObject>();
            if (Dying || practicing || InAbyss || save.room == 8) return result;
            foreach (WorldObject obj in Map.objects)
                if (!GatheringLandmark(obj) && LandmarkEligible(save.room, obj) && IsObjectPresent(obj) && Explored(save.room, obj.x, obj.y)) result.Add(obj);
            return result;
        }

        // Keep the places list intact. Gathering is an explicit second view of
        // already explored, still available nodes, never an exploration reveal.
        public List<WorldObject> DiscoveredGatheringNodes()
        {
            var result = new List<WorldObject>();
            if (Dying || practicing || SafeTrainingActive || InAbyss || save.room == 8 || !CanEnter(save.room)) return result;
            foreach (WorldObject obj in Map.objects)
                if (GatheringLandmark(obj) && LandmarkEligible(save.room, obj) && IsObjectPresent(obj) &&
                    Explored(save.room, obj.x, obj.y) && !Has("node_" + obj.id)) result.Add(obj);
            return result;
        }

        public bool HasLandmarkPin
        {
            get { return landmarkPinFlag != null && save.flags.Contains(landmarkPinFlag) && StoredLandmark(landmarkPinRoom, landmarkPinId) != null; }
        }

        public WorldObject PinnedLandmark
        {
            get
            {
                if (Dying || practicing || InAbyss || save.room != landmarkPinRoom || !HasLandmarkPin) return null;
                WorldObject obj = StoredLandmark(landmarkPinRoom, landmarkPinId);
                return IsObjectPresent(obj) ? obj : null;
            }
        }

        public bool PinLandmark(WorldObject obj)
        {
            if (!CanManageLandmarkPin || !LandmarkEligible(save.room, obj) || !IsObjectPresent(obj) || !Explored(save.room, obj.x, obj.y)) return false;
            if (GatheringLandmark(obj) && (!CanEnter(save.room) || Has("node_" + obj.id))) return false;
            bool canonical = false;
            foreach (WorldObject candidate in Map.objects) if (ReferenceEquals(candidate, obj)) { canonical = true; break; }
            if (!canonical) return false;
            string flag = LandmarkPinPrefix + save.room.ToString(CultureInfo.InvariantCulture) + ":" + obj.id;
            if (landmarkPinFlag == flag && HasLandmarkPin) return false;
            save.flags.RemoveAll(value => value != null && value.StartsWith(LandmarkPinPrefix, StringComparison.Ordinal));
            save.flags.Add(flag); landmarkPinRoom = save.room; landmarkPinId = obj.id; landmarkPinFlag = flag;
            revision++; sound = "quest"; return true;
        }

        private void CompleteGatheringLandmarkPin(WorldObject obj)
        {
            // Called only after the original yield and depletion flag. Retire
            // this destination without disturbing a different personal route.
            if (!GatheringLandmark(obj) || landmarkPinRoom != save.room || landmarkPinId != obj.id ||
                landmarkPinFlag == null || !save.flags.Contains(landmarkPinFlag) || !Has("node_" + obj.id) ||
                !LandmarkEligible(save.room, obj)) return;
            bool canonical = false;
            foreach (WorldObject candidate in Map.objects) if (ReferenceEquals(candidate, obj)) { canonical = true; break; }
            if (!canonical) return;
            string completed = landmarkPinFlag;
            save.flags.RemoveAll(value => value == completed);
            landmarkPinRoom = -1; landmarkPinId = landmarkPinFlag = null;
        }

        public bool ClearLandmarkPin()
        {
            if (!CanManageLandmarkPin) return false;
            int removed = save.flags.RemoveAll(value => value != null && value.StartsWith(LandmarkPinPrefix, StringComparison.Ordinal));
            landmarkPinRoom = -1; landmarkPinId = landmarkPinFlag = null;
            if (removed == 0) return false;
            revision++; sound = "ui"; return true;
        }

        private void SanitizeLandmarkPin()
        {
            landmarkPinRoom = -1; landmarkPinId = landmarkPinFlag = null;
            foreach (string flag in save.flags)
            {
                if (flag == null || !flag.StartsWith(LandmarkPinPrefix, StringComparison.Ordinal)) continue;
                string tail = flag.Substring(LandmarkPinPrefix.Length); int separator = tail.IndexOf(':'); int room;
                if (separator <= 0 || !int.TryParse(tail.Substring(0, separator), NumberStyles.None, CultureInfo.InvariantCulture, out room)) continue;
                string id = tail.Substring(separator + 1);
                if (StoredLandmark(room, id) == null) continue;
                landmarkPinRoom = room; landmarkPinId = id;
                landmarkPinFlag = LandmarkPinPrefix + room.ToString(CultureInfo.InvariantCulture) + ":" + id;
                break;
            }
            // Only this feature's namespace is normalized. Mature and unknown
            // flags keep their order, identity and values, including duplicates.
            save.flags.RemoveAll(value => value != null && value.StartsWith(LandmarkPinPrefix, StringComparison.Ordinal));
            if (landmarkPinFlag != null) save.flags.Add(landmarkPinFlag);
        }
    }
}
