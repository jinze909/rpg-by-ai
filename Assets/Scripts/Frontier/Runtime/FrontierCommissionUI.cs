using System;
using UnityEngine;

namespace Starfall
{
    public sealed partial class FrontierRuntime
    {
        private Rect CommissionWarningBounds(Enemy enemy)
        {
            Rect contact = GameCore.CommissionPattern(enemy) == 0
                ? SignalLaneBounds(enemy.attackOrigin, GameCore.CommissionBeamEnd(enemy), GameCore.CommissionBeamRadius)
                : SignalLaneBounds(enemy.target, enemy.target, GameCore.CommissionRadius(enemy));
            if(GameCore.CommissionPattern(enemy) == 2 && enemy.timer > .35f)
            {
                Point station = ScreenPoint(GameCore.CommissionReceiverPoint);
                contact = UnionSignalBounds(contact, new Rect(station.x-128,station.y-119,256,80));
            }
            return contact;
        }
        private void DrawCommissionWarning(Enemy enemy, bool boundaryOnly = false)
        {
            if(!GameCore.IsCommissionGuardian(enemy) || enemy.state != 1) return;
            float progress = Mathf.Clamp01(1-enemy.timer/Math.Max(.01f,enemy.windupDuration));
            Color warning = PixelArt.C("#f0b676"); warning.a = boundaryOnly ? .95f : .8f + progress*.15f;
            if(GameCore.CommissionPattern(enemy) == 0)
            {
                DrawSignalLane(enemy.attackOrigin, GameCore.CommissionBeamEnd(enemy), GameCore.CommissionBeamRadius, warning);
                if(!boundaryOnly) DrawRay(enemy.attackOrigin, GameCore.CommissionBeamEnd(enemy), new Color(warning.r,warning.g,warning.b,.3f));
            }
            else
            {
                float radius = GameCore.CommissionRadius(enemy);
                Ring(enemy.target,radius,warning,48);
                if(!boundaryOnly)
                {
                    Ring(enemy.target,radius,new Color(.02f,.035f,.07f,.95f),64);
                    Ring(enemy.target,radius,warning,48);
                    Ring(enemy.target,radius*(.2f+progress*.8f),new Color(warning.r,warning.g,warning.b,.38f),32);
                }
            }
            if(GameCore.CommissionPattern(enemy) == 2 && enemy.timer > .35f)
            {
                Point station = ScreenPoint(GameCore.CommissionReceiverPoint);
                Ring(GameCore.CommissionReceiverPoint,.65f,mint,32);
                Box(station.x-128,station.y-119,256,63,new Color(.035f,.07f,.10f,.92f));
                Frame(station.x-128,station.y-119,256,63,mint);
                Text(station.x-118,station.y-112,236,49,Game.L("到蓄光池按E减压\n时机 ","At the reservoir: E to vent\nWindow ") + Math.Max(0,enemy.timer-.35f).ToString("0.0") + "s",13,mint,TextAnchor.MiddleCenter);
            }
        }
        private void DrawCommissionWorld()
        {
            if(Game.save.room != 6 || !Game.CommissionAccepted) return;
            Enemy guardian = Game.enemies.Find(GameCore.IsCommissionGuardian);
            if(guardian != null)
            {
                string hint = GameCore.CommissionHint(guardian,Game.save.english);
                Box(383,113,514,65,new Color(.035f,.065f,.10f,.92f));
                Text(397,121,486,49,hint,14,gold,TextAnchor.MiddleCenter);
                return;
            }
            Point point = Game.CommissionReported ? Game.CommissionWaterPoint : Game.CommissionTested ? new Point(22,18) : GameCore.CommissionReceiverPoint;
            Point screen = ScreenPoint(point);
            string label = Game.CommissionReported
                ? Game.Has("commission_water_day_"+Game.Day) ? Game.L("今日补给已领取","TODAY'S SUPPLY TAKEN") : Game.L("供水补给 · 凉茶1","WATER SUPPLY · 1 TEA")
                : Game.CommissionTested ? Game.L("向伊沃回报试机","REPORT THE TEST TO IVO") : Game.L("明天 · 自愿试机","TOMORROW · OPTIONAL TEST");
            Ring(point,.55f,Game.CommissionReported?mint:gold,28);
            Text(screen.x-120,screen.y-98,240,47,label,12,Game.CommissionReported?mint:gold,TextAnchor.MiddleCenter);
        }
        private string CommissionMoveHint(Enemy enemy)
        { return GameCore.CommissionHint(enemy,Game.save.english); }
    }
}
