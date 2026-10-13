using System;

namespace Starfall
{
    public sealed partial class GameCore
    {
        public const float ControllerAimDeadZone = .25f;

        // Aim speed does not depend on stick travel. A deliberate direction
        // leaves the radial dead zone and shares the combat snapshot rules.
        public static Point ControllerStickDirection(Point sample)
        {
            if (!Finite(sample.x) || !Finite(sample.y)) return new Point();
            double length = Math.Sqrt((double)sample.x * sample.x + (double)sample.y * sample.y);
            if (length <= ControllerAimDeadZone) return new Point();
            return new Point((float)(sample.x / length), (float)(sample.y / length));
        }

        private void SanitizeControllerAim()
        { if (save.controllerAimProfile < 0 || save.controllerAimProfile > 2) save.controllerAimProfile = 0; }

        public bool ConfigureControllerAim(int profile, bool invertY)
        {
            if (Dying || mode != "pause" && mode != "controls" || profile < 0 || profile > 2) return false;
            if (save.controllerAimProfile != profile || save.controllerAimInvertY != invertY)
            { save.controllerAimProfile = profile; save.controllerAimInvertY = invertY; revision++; }
            return true;
        }
    }
}
