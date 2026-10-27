using System;

namespace Starfall
{
    public sealed partial class GameCore
    {
        // A map preview retains the catalogue's original save/generation and
        // canonical destination. Reading it cannot disclose a new source.
        public MaterialSource GetMaterialRouteSource(MaterialSource source)
        {
            if ((mode != "materials" && mode != "map") || source == null || save == null ||
                Dying || practicing || SafeTrainingActive || InAbyss ||
                save.room < 0 || save.room >= data.maps.Length ||
                !Finite(save.health) || save.health <= 0 || !CanStand(Position) ||
                !ReferenceEquals(source.owner, save) || source.generation != WorldGeneration) return null;
            foreach (MaterialSource current in MaterialSources(source.item))
                if (current.room == source.room && ReferenceEquals(current.target, source.target)) return current;
            return null;
        }

        public bool CanRouteMaterialSource(MaterialSource source)
        {
            if (mode != "map") return false;
            MaterialSource current = GetMaterialRouteSource(source);
            return current != null && !current.depleted && (current.room == save.room || SafeTravel);
        }

        // Only the explicit map confirmation travels. Enter owns every mature
        // travel reset; the arriving source is then resolved in its new world.
        public bool RouteToMaterialSource(MaterialSource source)
        {
            if (!CanRouteMaterialSource(source)) return false;
            MaterialSource current = GetMaterialRouteSource(source);
            if (current.room != save.room)
            {
                int room = current.room, item = current.item;
                WorldObject target = current.target;
                Enter(room);
                if (save.room != room || mode != "world" || Dying || practicing || InAbyss) return false;
                current = null;
                foreach (MaterialSource arrived in MaterialSources(item))
                    if (arrived.room == room && ReferenceEquals(arrived.target, target) && !arrived.depleted)
                    { current = arrived; break; }
                if (current == null) return false;
            }
            else Close();
            // Reconfirming an existing waypoint succeeds without another flag,
            // revision, sound or payment. Quest tracking remains independent.
            return ReferenceEquals(PinnedLandmark, current.target) || SetLandmarkPinCore(current.target);
        }
    }
}
