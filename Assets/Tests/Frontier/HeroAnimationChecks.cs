using System;
namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        private static readonly Point[] HeroHeadings = { new Point(1,0), new Point(-1,0), new Point(0,-1), new Point(0,1) };
        private static readonly string[] HeroLooks = { "Vanguard", "Ranger", "Arcanist" };
        private static GameCore HeroGame(Campaign data,int profession)
        {
            var g=new GameCore(data);g.NewGame(profession);g.Close();g.Enter(1);g.Close();
            g.enemies.Clear();g.save.companion=-1;g.save.x=12;g.save.y=14;g.save.energy=100;
            g.save.reducedMotion=true;
            foreach(string flag in new[]{"weapon_1","weapon_2"})if(!g.Has(flag))g.save.flags.Add(flag);
            Check(g.CanStand(g.Position),"Hero animation fixture uses actual walkable forest ground");return g;
        }
        private static void HeroFrame(GameCore g,string atlas,int frame,bool flip)
        {
            HeroVisualFrame actual=HeroAnimationState.Select(g);
            Check(actual.atlas==atlas&&actual.frame==frame&&actual.flip==flip,
                "Full-body selection expected "+atlas+":"+frame+":"+flip+", actual "+actual.atlas+":"+actual.frame+":"+actual.flip);
        }
        public static void HeroFullBodyContactsFollowRealAttackAndTechnique(Campaign data)
        {
            for(int profession=0;profession<3;profession++)foreach(Point direction in HeroHeadings)foreach(bool skill in new[]{false,true})
            {
                var g=HeroGame(data,profession);g.SetAim(direction);
                if(skill)g.Skill(direction);else g.Attack(direction);
                ActorAction action=g.playerAction;Check(action!=null&&!action.released,"Real action starts in its existing windup");
                int row=GameCore.DirectionRow(direction),prep=skill?6:4,release=skill?7:5;
                string sheet=row==0?"Player"+HeroLooks[profession]:row==1?"PlayerRear":"PlayerSouth";
                int prepFrame=row==0?prep:profession*4+(row==1?2:skill?2:0),releaseFrame=row==0?release:profession*4+(row==1?3:skill?3:1);
                bool flip=row==0&&direction.x<0;HeroFrame(g,sheet,prepFrame,flip);
                float elapsed=action.elapsed;int projectiles=g.projectiles.Count;
                for(int draw=0;draw<200;draw++)HeroFrame(g,sheet,prepFrame,flip);
                Check(action.elapsed==elapsed&&g.projectiles.Count==projectiles&&!action.released,"Selecting a frame cannot advance clock or release damage");
                for(int step=0;step<100&&!action.released;step++)
                {
                    g.Tick(.01f,new Point());
                    if(!action.released)HeroFrame(g,sheet,prepFrame,flip);
                }
                Check(action.released&&g.PlayerPhase==ActionPhase.Active,"Existing simulation reaches its real contact boundary");
                HeroFrame(g,sheet,releaseFrame,flip);
                Check(skill?profession!=1||g.projectiles.Count>0:profession==0||g.projectiles.Count>0,"Existing ranged actions create real projectiles at the displayed release");
            }
        }
        public static void HeroFullBodyRecoveryAndReactionsKeepActualEquipment(Campaign data)
        {
            foreach(int equipped in new[]{1,2})foreach(Point direction in HeroHeadings)
            {
                var g=HeroGame(data,0);g.Equip(equipped);g.Skill(direction);
                for(int frame=0;frame<20;frame++)g.Tick(.02f,new Point());
                Check(g.playerAction!=null&&g.PlayerPhase==ActionPhase.Recovery,"Cross-equipped Q retains actual standing recovery");
                int row=GameCore.DirectionRow(direction);
                HeroFrame(g,row==0?"PlayerVanguard":row==1?"PlayerRear":"PlayerSouth",row==0?7:3,row==0&&direction.x<0);
                g.Tick(.02f,direction);Check(g.RecoveryWalking&&g.AppearanceWeapon==equipped,"Actual recovery movement restores equipped weapon");
                HeroFrame(g,"Walk"+HeroLooks[equipped],row*4+g.WalkFrame,row==0&&direction.x<0);
                g.invulnerable=0;g.projectiles.Add(new Projectile{position=g.Position,damage=1,velocity=new Point(.01f,0),visual="enemyOrb",source="shot"});g.Tick(.01f,direction);
                Check(g.playerHitTime>0&&!g.Dying,"Actual hostile projectile enters nonlethal reaction");
                string sheet=row==0?"Player"+HeroLooks[equipped]:row==1?"PlayerReactionsRear":"PlayerReactionsSouth";
                HeroFrame(g,sheet,row==0?10:equipped*4+2,row==0&&direction.x<0);
                g.SetAim(direction*-1);g.Tick(.01f,new Point());
                HeroFrame(g,sheet,row==0?10:equipped*4+2,row==0&&direction.x<0);
            }
        }
        public static void HeroFullBodyLastDodgeAndDeathKeepCapturedFacing(Campaign data)
        {
            for(int profession=0;profession<3;profession++)foreach(Point direction in HeroHeadings)
            {
                var g=HeroGame(data,profession);g.Tick(.02f,direction*-1);g.SetAim(direction);g.Dash(new Point());
                Check(g.dashTime>0,"Real cursor-directed dodge is accepted");
                for(int frame=0;frame<100&&g.dashTime>0;frame++)g.Tick(.02f,new Point());
                Check(g.dashTime<=0&&g.ActuallyMoving&&g.PlayerPose==8,"Final dodge displacement remains a dodge after timer zero");
                int row=GameCore.DirectionRow(direction);string sheet=row==0?"Player"+HeroLooks[profession]:row==1?"PlayerReactionsRear":"PlayerReactionsSouth";
                HeroFrame(g,sheet,row==0?8:profession*4,row==0&&direction.x<0);
                g.Tick(.02f,new Point());Check(g.PlayerPose!=8,"Following ordinary tick releases final dodge presentation");
                g.SetAim(direction);g.invulnerable=0;g.save.health=1;g.projectiles.Add(new Projectile{position=g.Position,damage=999,velocity=new Point(.01f,0),visual="enemyOrb",source="shot"});g.Tick(.01f,new Point());
                Check(g.Dying,"Real lethal contact enters death");HeroFrame(g,sheet,row==0?11:profession*4+3,row==0&&direction.x<0);
                g.SetAim(direction*-1);g.Equip((profession+1)%3);g.Tick(.01f,new Point());
                HeroFrame(g,sheet,row==0?11:profession*4+3,row==0&&direction.x<0);
            }
        }
        public static void HeroFullBodySelectionPreservesPauseHitStopAndSaveState(Campaign data)
        {
            Check(HeroAnimationState.Atlases.Length==10,"All ten existing full-body atlas sources are explicit");
            foreach(string atlas in HeroAnimationState.Atlases)Check(!string.IsNullOrEmpty(atlas),"A full-body atlas cannot be missing");
            for(int profession=0;profession<3;profession++)
            {
                var g=HeroGame(data,profession);g.Attack(new Point(1,0));var action=g.playerAction;
                g.Tick(.02f,new Point());HeroVisualFrame expected=HeroAnimationState.Select(g);
                float elapsed=action.elapsed,energy=g.save.energy,time=g.save.totalTime,x=g.save.x,y=g.save.y;
                g.mode="pause";for(int frame=0;frame<5;frame++){g.Tick(.02f,new Point(1,0));HeroFrame(g,expected.atlas,expected.frame,expected.flip);}
                Check(action.elapsed==elapsed&&g.save.energy==energy&&g.save.x==x&&g.save.y==y,"Pause freezes actual action, resources and feet");
                g.Close();g.hitStop=.08f;for(int frame=0;frame<3;frame++){g.Tick(.02f,new Point(1,0));HeroFrame(g,expected.atlas,expected.frame,expected.flip);}
                Check(action.elapsed==elapsed,"Existing hitstop freezes action while selector remains read-only");
                int flags=g.save.flags.Count,shots=g.projectiles.Count;float cooldown=g.attackCooldown;
                for(int sample=0;sample<1000;sample++)HeroFrame(g,expected.atlas,expected.frame,expected.flip);
                Check(g.save.flags.Count==flags&&g.projectiles.Count==shots&&g.attackCooldown==cooldown,"Repeated render selection cannot write saves, dispatch contacts or change cooldowns");
                Check(g.save.totalTime>=time,"The selector does not reverse the existing simulation clock");
            }
        }
    }
}
