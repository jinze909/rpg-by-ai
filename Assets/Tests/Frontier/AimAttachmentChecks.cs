using System;
using System.Reflection;
namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        public static void CapturedTargetKeepsBodyAndMuzzleTogether(Campaign data)
        {
            var shotMethod=typeof(GameCore).GetMethod("WeaponShot",BindingFlags.Instance|BindingFlags.NonPublic);
            var g=Fresh(data);g.enemies.Clear();Point feet=g.Position;
            for(int weapon=1;weapon<=2;weapon++)for(int degrees=0;degrees<360;degrees++)foreach(float distance in new[]{2f,6f,12f})
            {
                double angle=degrees*Math.PI/180;Point body=new Point((float)Math.Cos(angle),(float)Math.Sin(angle)),target=feet+body*distance;
                var shot=(Projectile)shotMethod.Invoke(g,new object[]{feet,weapon,body,9f,10f,(Point?)target});
                Point ray=target-shot.position;
                Check((ray.Normalized-shot.velocity.Normalized).Length<.0001f,"Flight direction must reach the target from the exact created muzzle at every row boundary");
                Check(Math.Abs(shot.launchHeight-GameCore.WeaponHeight(weapon,body))<.001f,"Continuous flight correction must not select a second weapon/body height");
                if(!shot.blockedAtSpawn)Check((shot.position-GameCore.WeaponOrigin(feet,weapon,body)).Length<.001f,"A continuous aim vector cannot relocate a four-direction weapon");
                Check(Math.Abs(shot.velocity.Length-9)<.001f,"Target correction must retain projectile speed");
            }
            g.SetAimTarget(feet+new Point(5,-5));g.Attack(new Point(1,-1));var action=g.playerAction;Point stored=action.target.Value;
            g.SetAimTarget(feet+new Point(-5,5));g.SetAim(new Point(-1,1));
            Check((action.target.Value-stored).Length==0&&action.direction.x>0&&action.direction.y<0,"Mouse changes during preparation cannot swap the action's muzzle or target");
            g.Enter(0);Check(!g.aimTarget.HasValue,"Travel cannot carry a previous room's target into a new action");
            g.SetAimTarget(new Point(float.NaN,0));Check(!g.aimTarget.HasValue,"Invalid target coordinates cannot reach projectile geometry");
        }
    }
}
