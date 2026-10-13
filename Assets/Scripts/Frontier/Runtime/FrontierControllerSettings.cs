using System;
using UnityEngine;

namespace Starfall
{
    public sealed partial class FrontierRuntime
    {
        private void OpenControllerSettings() { OpenMenu("controls"); }

        private void SetControllerAimPreferences(int profile, bool invertY)
        { if (Game.ConfigureControllerAim(profile, invertY)) { Save(); Game.sound = "ui"; } }

        private static float CalibrationAxis(float value)
        { return float.IsNaN(value) || float.IsInfinity(value) ? 0 : Math.Max(-1, Math.Min(1, value)); }

        private void DrawControllerSettings()
        {
            Window(Game.L("摇杆瞄准", "Controller aiming"), Game.L("边走边瞄准，在此试好方向再出发。", "Aim while moving. Check your stick directions here before returning to the road."));
            string[] zh = { "标准摇杆", "另一组摇杆", "关闭摇杆瞄准" };
            string[] en = { "Standard stick", "Alternate stick", "Movement aiming" };
            for (int i = 0; i < 3; i++)
                if (Button(279 + i * 318, 180, 301, 44, Game.L(zh[i], en[i]), Game.save.controllerAimProfile == i))
                    SetControllerAimPreferences(i, Game.save.controllerAimInvertY);
            if (Button(279, 248, 460, 44, Game.L("反转上下 ", "Invert vertical ") + (Game.save.controllerAimInvertY ? "ON" : "OFF"), Game.save.controllerAimInvertY, Game.save.controllerAimProfile != 2))
                SetControllerAimPreferences(Game.save.controllerAimProfile, !Game.save.controllerAimInvertY);
            Text(758, 249, 458, 46, Game.L("向右推时，标记应向右；向上推时，标记应向上。", "Push right: the marker moves right. Push up: the marker moves up."), 15, muted);

            Card(279, 322, 460, 314); Card(756, 322, 461, 314);
            Text(301, 341, 416, 28, Game.L("方向试验 · 世界暂停", "Direction check · world paused"), 18, gold);
            Point raw = ReadControllerAimStick();
            Point sample = new Point(CalibrationAxis(raw.x), CalibrationAxis(raw.y));
            Point direction = GameCore.ControllerStickDirection(raw);
            Point center = new Point(509, 465); const float radius = 82;
            Box(center.x - radius, center.y - 1, radius * 2, 2, muted);
            Box(center.x - 1, center.y - radius, 2, radius * 2, muted);
            for (int i = 0; i < 48; i++)
            {
                float a = i * (float)Math.PI * 2 / 48;
                Point edge = center + new Point((float)Math.Cos(a), (float)Math.Sin(a)) * radius;
                Point quiet = center + new Point((float)Math.Cos(a), (float)Math.Sin(a)) * (radius * GameCore.ControllerAimDeadZone);
                Box(edge.x - 1, edge.y - 1, 2, 2, muted); Box(quiet.x - 1, quiet.y - 1, 2, 2, muted);
            }
            Text(473, 371, 72, 22, Game.L("上", "UP"), 12, muted, TextAnchor.MiddleCenter);
            Text(473, 552, 72, 22, Game.L("下", "DOWN"), 12, muted, TextAnchor.MiddleCenter);
            Text(385, 454, 40, 22, Game.L("左", "LEFT"), 11, muted, TextAnchor.MiddleCenter);
            Text(594, 454, 44, 22, Game.L("右", "RIGHT"), 11, muted, TextAnchor.MiddleCenter);
            if (direction.Length > .1f)
                for (int i = 1; i <= 20; i++)
                { Point p = center + direction * (radius * i / 20); Box(p.x - 1.5f, p.y - 1.5f, 3, 3, gold); }
            Point marker = center + sample * radius;
            Box(marker.x - 6, marker.y - 6, 12, 12, ink); Box(marker.x - 4, marker.y - 4, 8, 8, paper);
            string state = Game.save.controllerAimProfile == 2 ? Game.L("已关闭：沿移动方向瞄准", "Off: aim follows movement") : direction.Length > .1f ? Game.L("已读到方向", "Direction detected") : Game.L("中心区域：等待推动摇杆", "Center zone: push the stick to aim");
            Text(300, 594, 418, 29, state, 14, direction.Length > .1f ? gold : muted, TextAnchor.MiddleCenter);

            Text(778, 341, 417, 29, Game.L("走位与瞄准", "Move and aim"), 18, gold);
            Text(778, 386, 417, 55, Game.L("左摇杆移动，右摇杆定攻击方向。松开右摇杆会保留方向。", "Left stick moves; right stick aims. Release the right stick to keep that direction."), 15, paper);
            Text(778, 455, 417, 52, Game.L("A 闪避沿移动方向；站定时沿瞄准方向。X 攻击，Y 战技。", "A dodges along movement, or aim when still. X attacks; Y uses your technique."), 15, paper);
            Text(778, 522, 417, 54, Game.L("标记方向不对时，试另一组摇杆或反转上下。", "If directions are wrong, try Alternate stick or Invert vertical."), 15, muted);
            Text(778, 587, 417, 32, Game.L("键盘或鼠标可随时接回操作。", "Keyboard or mouse can take over anytime."), 14, muted);
            if (Button(279, 661, 938, 45, Game.L("返回设置", "Back to settings"), true)) Back();
        }
    }
}
