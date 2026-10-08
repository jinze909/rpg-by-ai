using System;
namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        public static void StandingRecoveryAndCrossEquipmentDeath(Campaign data)
        {
            Point[] directions={new Point(1,0),new Point(-1,0),new Point(0,-1),new Point(0,1)};
            for(int weapon=0;weapon<3;weapon++)foreach(Point direction in directions)
            {
                var g=Quiet(data,weapon);g.Attack(direction);ActorAction action=g.playerAction;
                while(g.PlayerPhase!=ActionPhase.Recovery)g.Tick(.01f,new Point());
                g.Tick(.01f,new Point(),direction*-1);
                Check(g.playerAction==action&&(g.VisualFacing-direction).Length<.001f,"An idle release pose cannot rotate before recovery ends");
                g.Tick(.01f,direction*-1,direction*-1);
                Check(g.ActuallyMoving&&(g.VisualFacing-direction*-1).Length<.001f&&g.PlayerPose>=2&&g.PlayerPose<=3,"Moving recovery returns body and legs together to locomotion");
                g=Quiet(data,weapon);g.save.weapon=(weapon+1)%3;g.Skill(direction);
                Check(g.AppearanceWeapon==weapon,"Q preparation shows the vocation's real equipment");
                g.invulnerable=0;g.save.health=1;g.projectiles.Add(new Projectile{position=g.Position,damage=999,visual="enemyOrb"});
                g.Tick(.01f,new Point(),direction*-1);
                Check(g.Dying&&g.AppearanceWeapon==weapon&&(g.VisualFacing-direction).Length<.001f,"A lethal hit cannot replace the active outfit or turn the corpse to the live cursor");
                g.Tick(.01f,new Point(1,0),direction*-1);Check((g.VisualFacing-direction).Length<.001f,"A fallen player ignores new movement/aim input");
                Advance(g,1.1f);Check(!g.Dying&&g.save.room==0&&g.AppearanceWeapon==g.save.weapon,"Inn recovery clears transient corpse identity without changing equipment saves");
            }
        }
        public static void StableEnemyFacingAndBackpedaling(Campaign data)
        {
            var enemy=new Enemy(1400,1,13.5f,14,999){facing=new Point(-1,0),timer=999};
            for(int i=0;i<60;i++){enemy.Face(new Point(i%2==0?.001f:-.001f,-1));Check(enemy.VisualFlip,"Near-vertical motion cannot flicker left/right body mirroring");}
            enemy.Face(new Point(1,0));for(int i=0;i<60;i++){enemy.Face(new Point(i%2==0?.001f:-.001f,1));Check(!enemy.VisualFlip,"Changing side deliberately selects a stable right silhouette");}
            var g=Quiet(data);enemy.Face(new Point(-1,0));g.enemies.Add(enemy);float start=enemy.x;
            for(int i=0;i<12;i++)g.Tick(.02f,new Point());
            Check(enemy.x>start&&enemy.backpedaling&&enemy.moving&&enemy.moveTime>0&&enemy.facing.x<-.99f&&enemy.VisualFlip,"A retreating archer moves away while keeping its bow and body toward the player");
            Point before=enemy.facing;enemy.hitTime=.15f;enemy.recoil=new Point(1,0);g.Tick(.02f,new Point());
            Check(!enemy.moving&&!enemy.backpedaling&&(enemy.facing-before).Length<.001f,"Hit recoil cannot masquerade as forward walking or redirect a prepared weapon");
        }
        public static void RecoilRetainsInterruptedBodyDirection(Campaign data)
        {
            foreach(int weapon in new[]{0,1,2})foreach(Point direction in new[]{new Point(-1,0),new Point(0,-1),new Point(0,1)})
            {
                var g=Quiet(data,weapon);g.Attack(direction);while(g.PlayerPhase!=ActionPhase.Recovery)g.Tick(.01f,new Point());
                g.invulnerable=0;g.projectiles.Add(new Projectile{position=g.Position,damage=1,visual="enemyOrb",velocity=new Point(.01f,0)});g.Tick(.01f,new Point(),direction*-1);
                Check(g.playerHitTime>0,"Recoil scenario takes a real hostile projectile hit");
                for(int f=0;f<5;f++){g.Tick(.01f,new Point(1,0),direction*-1);Check((g.VisualFacing-direction).Length<.001f&&g.PlayerPose==10,"Recoil translation cannot rotate the body to a different aim or old locomotion direction");}
            }
        }
        public static void MeleeWarningGeometryMatchesActualContact(Campaign data)
        {
            foreach(float heading in new[]{0f,90f,180f,270f})foreach(float offset in new[]{0f,70f,110f,180f})
            {
                var g=Quiet(data);double a=heading*Math.PI/180,b=(heading+offset)*Math.PI/180;
                var e=new Enemy(1401,0,12,14,999){state=1,timer=.001f,windupDuration=.65f};e.Face(new Point((float)Math.Cos(a),(float)Math.Sin(a)));
                Point target=e.Position+new Point((float)Math.Cos(b),(float)Math.Sin(b))*1.1f;g.save.x=target.x;g.save.y=target.y;g.invulnerable=0;
                bool expected=offset<100;Check(GameCore.InEnemyMeleeArc(e,target)==expected,"Warning sector distinguishes forward/side contact from a rear miss");
                float health=g.save.health;g.enemies.Add(e);g.Tick(.01f,new Point());
                Check((g.save.health<health)==expected&&e.releaseTime>0,"The visible sector and real strike agree for all four headings");
                Check(!GameCore.InEnemyMeleeArc(e,e.Position+e.facing*1.51f),"Warning cannot imply a strike outside the original reach");
            }
        }
        public static void CombatSnapshotsSurviveDeathsAndReinforcements(Campaign data)
        {
            var g=Quiet(data);var boss=new Enemy(1402,0,13.5f,14,1,true){timer=999};g.enemies.Add(boss);
            g.enemies.Add(new Enemy(1403,0,14,14,1){timer=999});g.enemies.Add(new Enemy(1404,0,14.5f,14,1){timer=999});
            g.projectiles.Add(new Projectile{position=boss.Position,friendly=true,explosive=true,damage=999,visual="starfire"});
            g.projectiles.Add(new Projectile{position=new Point(17,14),damage=1,visual="enemyArrow"});
            g.Tick(.01f,new Point());
            Check(g.enemies.Count==0&&g.fallen.Count==3&&g.Has("boss_1")&&g.projectiles.Count==0,"Boss death, hostile retirement and splash removals share mutation-safe snapshots");
            Check(g.effects.Exists(e=>e.kind=="projectileFade")&&g.effects.Exists(e=>e.kind=="magicImpact"),"Retired missiles and splash still finish with their original visual lifecycles");
            g=Quiet(data);boss=new Enemy(1405,0,15,14,999,true){hp=100,state=1,timer=.001f,windupDuration=1,cycle=2,target=g.Position};g.enemies.Add(boss);g.Tick(.01f,new Point());
            Check(boss.cycle==3&&g.enemies.Count==2&&g.enemies[1].state==0&&g.enemies[1].moveTime==0,"A new Boss reinforcement begins next tick rather than mutating active AI iteration");
            for(int i=0;i<120;i++)g.Tick(.02f,new Point());Check(g.enemies.Count>0&&!g.Dying,"Repeated AI, projectile and hazard iterations remain playable after spawning");
        }
    }
}
