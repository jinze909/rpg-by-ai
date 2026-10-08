using System.Collections.Generic;
namespace Starfall
{
    public sealed partial class GameCore
    {
        // Separate reusable snapshots retain mutation-safe iteration order.
        // A Boss death can retire missiles during a projectile contact; splash
        // damage must not overwrite its parent target iteration.
        private readonly List<Enemy> tickingEnemies=new List<Enemy>(16);
        private readonly List<Projectile> tickingProjectiles=new List<Projectile>(128);
        private readonly List<Enemy> projectileTargets=new List<Enemy>(16);
        private readonly List<Enemy> splashTargets=new List<Enemy>(16);
        private readonly List<Hazard> tickingHazards=new List<Hazard>(32);
        private readonly List<LightRay> lightRayBuffer=new List<LightRay>(5);
        private readonly HashSet<int> visitedMirrors=new HashSet<int>();
        private static readonly Point[] companionOffsets = { new Point(-1.65f, .55f), new Point(1.65f, .55f), new Point(-1.65f, -.55f), new Point(1.65f, -.55f), new Point(0, 1.9f), new Point(0, -1.9f) };
    }
}
