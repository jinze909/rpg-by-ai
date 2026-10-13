using System;
using UnityEngine;

namespace Starfall
{
    public sealed partial class FrontierRuntime
    {
        private bool controllerAimAcquired, controllerStickBlocked;
        private Point controllerAimDirection;
        private SaveData controllerAimSave;
        private int controllerAimRoom = -1, controllerAimProfile = -1, controllerAimGeneration = -1;
        private bool controllerAimInvert;
        private string controllerAimMode;

        public bool ControllerAimActive
        {
            get { return controllerAimAcquired && controllerUsed && Game != null && Game.mode == "world" && !Game.Dying &&
                ReferenceEquals(controllerAimSave, Game.save) && controllerAimRoom == Game.save.room &&
                controllerAimGeneration == Game.WorldGeneration &&
                controllerAimProfile == Game.save.controllerAimProfile && controllerAimInvert == Game.save.controllerAimInvertY; }
        }
        // Calibration reads this sample without acquiring a world aim. The
        // existing vertical-axis convention is up-positive, world y is down.
        private Point ReadControllerAimStick()
        {
            if (Game == null || Game.save.controllerAimProfile == 2) return new Point();
            bool alternate = Game.save.controllerAimProfile == 1;
            float x = Input.GetAxisRaw(alternate ? "Alt Aim Horizontal" : "Aim Horizontal");
            float y = -Input.GetAxisRaw(alternate ? "Alt Aim Vertical" : "Aim Vertical");
            if (Game.save.controllerAimInvertY) y = -y;
            return new Point(x, y);
        }
        private void ResetControllerAim(bool blockHeldStick = true)
        {
            controllerAimAcquired = false; controllerAimDirection = new Point();
            controllerStickBlocked = blockHeldStick && GameCore.ControllerStickDirection(ReadControllerAimStick()).Length > 0;
            if (Game == null) return;
            controllerAimSave = Game.save; controllerAimRoom = Game.save.room;
            controllerAimGeneration = Game.WorldGeneration;
            controllerAimProfile = Game.save.controllerAimProfile; controllerAimInvert = Game.save.controllerAimInvertY;
            controllerAimMode = Game.mode;
        }
        private void UpdateControllerAim(bool desktopInput)
        {
            Point direction = GameCore.ControllerStickDirection(ReadControllerAimStick());
            bool deflected = direction.Length > 0;
            bool changed = controllerAimGeneration != Game.WorldGeneration || !ReferenceEquals(controllerAimSave, Game.save) || controllerAimRoom != Game.save.room ||
                controllerAimProfile != Game.save.controllerAimProfile || controllerAimInvert != Game.save.controllerAimInvertY ||
                controllerAimMode != Game.mode;
            if (changed) ResetControllerAim();
            if (Game.mode != "world" || Game.Dying)
            { ResetControllerAim(); return; }
            if (desktopInput)
            {
                controllerUsed = false; controllerAimAcquired = false;
                controllerStickBlocked = deflected;
                return;
            }
            if (!deflected) { controllerStickBlocked = false; return; }
            if (controllerStickBlocked || Game.save.controllerAimProfile == 2) return;
            controllerUsed = true; controllerAimAcquired = true; controllerAimDirection = direction;
            pointerAimActive = false;
            // Only live pointer ownership changes. Prepared attacks and queued
            // Q retain their own immutable direction/target snapshots.
            Game.SetAimTarget(null);
        }
        private void ValidateControllerAimBoundary()
        {
            if (controllerAimGeneration != Game.WorldGeneration || !ReferenceEquals(controllerAimSave, Game.save) || controllerAimRoom != Game.save.room ||
                controllerAimProfile != Game.save.controllerAimProfile || controllerAimInvert != Game.save.controllerAimInvertY ||
                controllerAimMode != Game.mode || Game.Dying) ResetControllerAim();
        }
        private void DrawControllerAimReticle()
        {
            if (!ControllerAimActive) return;
            Point p = ScreenPoint(Game.Position + Game.aim * 1.8f);
            // A stationary outlined direction marker. It identifies the live
            // aim without implying a lock, projectile origin or damage radius.
            Box(p.x-6,p.y-2,12,4,new Color(.025f,.045f,.06f,.95f));
            Box(p.x-2,p.y-6,4,12,new Color(.025f,.045f,.06f,.95f));
            Box(p.x-4,p.y-1,8,2,paper); Box(p.x-1,p.y-4,2,8,paper);
        }
    }
}
