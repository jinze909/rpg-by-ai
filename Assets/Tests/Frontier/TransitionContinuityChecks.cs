using System;
namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        public static void FinalMovingDodgeOwnsPoseAndFacing(Campaign data)
        {
            float[] steps={.01f,.02f,.033f,.05f};int[] expectedFrames={18,10,6,4};
            for(int rate=0;rate<steps.Length;rate++)foreach(bool heldMovement in new[]{false,true})
            {
                var g=Quiet(data);g.Tick(.01f,new Point(-1,0));
                Point start=g.Position;float gait=g.walkDistance;int contacts=g.footContacts;
                g.save.energy=100;g.Dash(new Point(1,0));float step=steps[rate];int frames=0;
                Check(g.dashTime==.18f&&g.dashCooldown==.75f&&g.invulnerable==.32f&&g.save.energy==80,"Dodge starts with its established timers and energy cost");
                while(g.dashTime>0&&frames<30)
                {
                    float timer=g.dashTime;Point before=g.Position;
                    g.Tick(step,heldMovement?new Point(1,0):new Point(),new Point(-1,0));frames++;
                    Check(g.dashTime==Math.Max(0,timer-step)&&Math.Abs((g.Position-before).x-step*15)<.0001f,"Each existing dodge step retains its full displacement and timer decrement");
                    Check(g.PlayerPose==8&&(g.VisualFacing-new Point(1,0)).Length<.001f,"Every moving dodge step keeps the dodge pose and direction, including timer expiry at "+step);
                }
                float elapsed=frames*step;
                Check(frames==expectedFrames[rate]&&g.dashTime==0&&Math.Abs((g.Position-start).x-elapsed*15)<.0002f,"Presentation cannot add or shorten a dodge step, including the existing 20 ms float residue");
                Check(Math.Abs(g.dashCooldown-(.75f-elapsed))<.00001f&&Math.Abs(g.invulnerable-(.32f-elapsed))<.00001f&&Math.Abs(g.save.energy-(80+14*elapsed))<.0001f,"Final-step presentation cannot extend cooldown, protection or change energy regeneration");
                Check(g.walkDistance==gait&&g.footContacts==contacts&&g.locomotionFacing.x<-.99f,"Dodge displacement creates neither a gait step nor a replacement locomotion direction");
                Check(!g.ActionTransitionBlocked&&g.playerAction==null,"An expired dodge remains immediately available for the established combat transition");
                g.Tick(.01f,heldMovement?new Point(1,0):new Point(),new Point(-1,0));
                Check(heldMovement?g.PlayerPose>=2&&g.PlayerPose<=3&&g.VisualFacing.x>.99f:g.PlayerPose==0&&!g.ActuallyMoving&&g.VisualFacing.x<-.99f,"The next real simulation step releases dodge presentation to actual walking or idle aim");
            }
        }
        private static GameCore MovingDodgeExpiry(Campaign data)
        {
            var g=Quiet(data);g.Tick(.01f,new Point(-1,0));g.Dash(new Point(1,0));
            for(int frame=0;frame<4;frame++)g.Tick(.05f,new Point(),new Point(-1,0));
            Check(g.dashTime==0&&g.ActuallyMoving,"Boundary fixture reaches an actual moving final dodge step");return g;
        }
        public static void FinalDodgeFreezesAndClearsAtBoundaries(Campaign data)
        {
            foreach(string mode in new[]{"inventory","dialog","paused"})
            {
                var g=MovingDodgeExpiry(data);Point position=g.Position,movement=g.movementDirection;
                g.mode=mode;g.Tick(.05f,new Point(0,1),new Point(0,-1));
                g.mode="world";g.hitStop=.03f;g.Tick(.05f,new Point(0,1),new Point(0,-1));
                g.Tick(0,new Point(0,1),new Point(0,-1));
                Check((g.Position-position).Length<.001f&&(g.movementDirection-movement).Length<.001f&&g.PlayerPose==8&&g.VisualFacing.x>.99f,"Menus, hit stop and zero-duration ticks freeze the last sampled dodge body together");
                g.Tick(.01f,new Point(),new Point(-1,0));
                Check(g.PlayerPose==0&&!g.ActuallyMoving&&g.VisualFacing.x<-.99f,"The first resumed movement sample clears final-dodge presentation");
            }
            var blocked=Quiet(data);Point wall=WallStand(blocked);blocked.save.x=wall.x;blocked.save.y=wall.y;blocked.Dash(new Point(-1,0));
            for(int frame=0;frame<4;frame++)blocked.Tick(.05f,new Point(),new Point(1,0));
            Check(blocked.dashTime==0&&!blocked.ActuallyMoving&&blocked.PlayerPose==0&&blocked.VisualFacing.x>.99f,"A fully blocked final dodge step stays idle rather than inventing motion ownership");
            var travelled=MovingDodgeExpiry(data);travelled.Enter(0);
            Check(!travelled.ActuallyMoving&&travelled.PlayerPose!=8,"Travel clears the previous map's sampled dodge");
            var loaded=MovingDodgeExpiry(data);loaded.PrepareSave();loaded.Load(CloneSave(loaded.save));
            Check(!loaded.ActuallyMoving&&loaded.PlayerPose!=8,"Loading clears transient dodge presentation without adding save state");
            var practice=Quiet(data);practice.Enter(0);practice.StartPractice();practice.enemies.Clear();practice.Dash(new Point(1,0));
            for(int frame=0;frame<4;frame++)practice.Tick(.05f,new Point(),new Point(-1,0));
            Check(practice.practicing&&practice.dashTime==0&&practice.ActuallyMoving&&practice.PlayerPose==8,"Practice reaches the same moving final-dodge presentation");practice.EndPractice();
            Check(!practice.ActuallyMoving&&practice.PlayerPose!=8,"Leaving practice clears sampled dodge presentation immediately");
        }
        public static void FinalDodgeYieldsToActionsAndReactions(Campaign data)
        {
            var attack=MovingDodgeExpiry(data);attack.Attack(new Point(0,-1));
            Check(attack.playerAction!=null&&attack.PlayerPose==4&&attack.VisualFacing.y<-.99f,"A fresh basic attack replaces expired dodge presentation in the same frame");
            var skill=MovingDodgeExpiry(data);skill.Skill(new Point(0,-1));
            Check(skill.playerAction!=null&&skill.PlayerPose==6&&skill.VisualFacing.y<-.99f,"A fresh Q replaces expired dodge presentation in the same frame");
            var buffered=Quiet(data);buffered.Tick(.01f,new Point(-1,0));buffered.Dash(new Point(1,0));buffered.Skill(new Point(0,-1));
            for(int frame=0;frame<4;frame++)buffered.Tick(.05f,new Point(),new Point(-1,0));
            Check(buffered.dashTime==0&&buffered.playerAction!=null&&buffered.PlayerPose==6&&buffered.VisualFacing.y<-.99f,"Buffered Q takes ownership on the existing final-dodge boundary");
            var nextDash=MovingDodgeExpiry(data);nextDash.dashCooldown=0;nextDash.save.energy=100;nextDash.Dash(new Point(0,-1));
            Check(nextDash.dashTime==.18f&&nextDash.PlayerPose==8&&nextDash.VisualFacing.y<-.99f,"An accepted new dodge replaces the previous sampled direction");
            foreach(bool lethal in new[]{false,true})
            {
                var g=Quiet(data);g.Tick(.01f,new Point(-1,0));g.Dash(new Point(1,0));
                for(int frame=0;frame<3;frame++)g.Tick(.05f,new Point(),new Point(-1,0));
                // Normal dash protection still exists here. Explicitly remove it
                // only to exercise reaction/death presentation on this boundary.
                g.invulnerable=0;if(lethal)g.save.health=1;
                g.projectiles.Add(new Projectile{position=g.Position+new Point(.75f,0),damage=lethal?999:1,source="shot"});
                g.Tick(.05f,new Point(),new Point(-1,0));
                Check(g.dashTime==0&&g.PlayerPose==(lethal?11:10)&&g.VisualFacing.x>.99f,"An actual final-step hit owns recoil/death while capturing the displayed dodge direction");
            }
        }
        public static void ReleasedContactsSelectVisibleActivePoses(Campaign data)
        {
            foreach(float step in new[]{.02f,.04f}) for(int profession=0;profession<3;profession++)
            {
                var g=Quiet(data,profession);g.Skill(new Point(1,0));ActorAction skill=g.playerAction;
                int frames=step==.02f?14:7;
                for(int frame=0;frame<frames-1;frame++)g.Tick(step,new Point());
                Check(!skill.released&&g.PlayerPhase==ActionPhase.Windup&&g.PlayerPose==6,"Q retains its authored preparation before contact");
                g.Tick(step,new Point());
                Check(skill.released&&g.PlayerPhase==ActionPhase.Active&&g.PlayerPose==7,"Q's actual release selects Active and the contact pose despite float residue, profession "+profession+" step "+step);
                Check(profession==1?g.projectiles.Count==5:g.effects.Exists(e=>e.kind=="burst"),"The visible Q contact corresponds to its actual volley or burst event");
            }
            for(int weapon=0;weapon<3;weapon++) for(int style=0;style<3;style++) foreach(int slices in new[]{11,17})
            {
                var g=Quiet(data,weapon);g.save.weaponStyles[weapon]=style;g.Attack(new Point(1,0));ActorAction attack=g.playerAction;
                float step=attack.contact/slices;
                for(int frame=0;frame<slices-1;frame++)g.Tick(step,new Point());
                Check(!attack.released&&g.PlayerPhase==ActionPhase.Windup,"Variable weapon/style contact stays pending before its release boundary");
                g.Tick(step,new Point());
                int pose=weapon==0&&style==1?7:5;
                Check(attack.released&&g.PlayerPhase==ActionPhase.Active&&g.PlayerPose==pose,"Actual contact governs the light/heavy release pose across variable weapon/style timestep, weapon "+weapon+" style "+style+" slices "+slices);
            }
        }
        public static void WalkingSkillRecoveryRestoresEquippedAppearance(Campaign data)
        {
            foreach(int equipped in new[]{1,2}) foreach(Point direction in new[]{new Point(1,0),new Point(0,-1),new Point(0,1)})
            {
                var g=Quiet(data);g.save.flags.Add("weapon_"+equipped);g.Equip(equipped);g.Skill(direction);
                ActorAction skill=g.playerAction;ImpactProfile impact=skill.impact;float damage=skill.damage;
                Check(g.AppearanceWeapon==0&&g.PlayerPose==6,"Cross-equipped Vanguard Q prepares with its actual class outfit");
                while(g.PlayerPhase!=ActionPhase.Recovery)g.Tick(.01f,new Point());
                Check(g.AppearanceWeapon==0&&!g.ActuallyMoving&&g.PlayerPose==7,"Standing Q recovery retains its class release outfit");
                Point motion=new Point(-direction.y,direction.x),before=g.Position;
                g.Tick(.01f,motion,direction*-1);
                Check((g.Position-before).Length>.001f&&g.ActuallyMoving&&g.PlayerPose>=2&&g.PlayerPose<=3,"Vanguard recovery restores collision-safe walking and its locomotion pose together");
                Check(g.AppearanceWeapon==equipped&&(g.VisualFacing-motion).Length<.001f,"Walking recovery returns body equipment and facing to the actual equipped weapon");
                Check(g.playerAction==skill&&skill.damage==damage&&(skill.direction-direction).Length<.001f&&object.ReferenceEquals(skill.impact,impact)&&skill.weapon==0&&skill.equippedWeapon==equipped,"Appearance handoff cannot mutate the prepared Q's damage, direction, impact or equipment identity");
            }
            foreach(int profession in new[]{1,2})
            {
                var g=Quiet(data,profession);g.Equip(0);g.Skill(new Point(0,-1));ActorAction skill=g.playerAction;
                while(g.PlayerPhase!=ActionPhase.Recovery)g.Tick(.01f,new Point());
                Point before=g.Position;g.Tick(.01f,new Point(1,0));
                Check((g.Position-before).Length<.001f&&!g.ActuallyMoving&&g.PlayerPose==7&&g.AppearanceWeapon==profession,"Ranged Q keeps its established stationary recovery and class equipment");
                while(g.playerAction==skill)g.Tick(.01f,new Point(1,0));
                g.Tick(.01f,new Point(1,0));Check(g.ActuallyMoving&&g.AppearanceWeapon==0,"Completed ranged Q returns to walking in the saved equipment");
            }
        }
        public static void RecoveryReactionsRetainDisplayedEquipment(Campaign data)
        {
            foreach(int equipped in new[]{1,2}) for(int state=0;state<3;state++)
            {
                var g=Quiet(data);g.save.flags.Add("weapon_"+equipped);g.Equip(equipped);g.Skill(new Point(1,0));
                ActorAction skill=g.playerAction;ImpactProfile impact=skill.impact;float damage=skill.damage;
                if(state>0)while(g.PlayerPhase!=ActionPhase.Recovery)g.Tick(.01f,new Point());
                Point motion=state==2?new Point(0,-1):new Point(),facing=state==2?new Point(0,-1):new Point(1,0);
                if(state==2)g.Tick(.01f,motion);int displayed=state==2?equipped:0;
                g.invulnerable=0;g.projectiles.Add(new Projectile{position=g.Position,velocity=new Point(.01f,0),damage=1,source="shot"});g.Tick(.01f,motion,new Point(-1,0));
                Check(g.playerHitTime>0&&g.PlayerPose==10&&g.AppearanceWeapon==displayed&&(g.VisualFacing-facing).Length<.001f,"Actual recoil keeps the outfit and direction visible at impact, equipment "+equipped+" action state "+state);
                for(int frame=0;frame<3;frame++)
                {
                    g.Tick(.01f,new Point(0,1),new Point(-1,0));
                    Check(g.AppearanceWeapon==displayed&&(g.VisualFacing-facing).Length<.001f,"Recoil movement and live aim cannot restore an interrupted skill outfit or turn the body");
                }
                Check(g.playerAction==skill&&skill.damage==damage&&object.ReferenceEquals(skill.impact,impact)&&skill.direction.x==1,"Reaction presentation cannot rewrite the retained action's combat values");
                g.PrepareSave();SaveData saved=CloneSave(g.save);var loaded=new GameCore(data,CloneSave(saved));loaded.Start();
                Check(loaded.playerHitTime==0&&loaded.playerAction==null&&loaded.AppearanceWeapon==equipped,"Loading clears the transient reaction outfit and restores saved equipment");
                var travelled=new GameCore(data,CloneSave(saved));travelled.Start();travelled.Skill(new Point(1,0));travelled.invulnerable=0;
                travelled.projectiles.Add(new Projectile{position=travelled.Position,damage=1,source="shot"});travelled.Tick(.01f,new Point());travelled.Enter(0);
                Check(travelled.playerHitTime==0&&travelled.playerAction==null&&travelled.AppearanceWeapon==equipped,"Travel clears a new class-outfit recoil without changing saved equipment");
                g.save.health=1;g.invulnerable=0;g.projectiles.Add(new Projectile{position=g.Position,damage=999,source="shot"});g.Tick(.01f,new Point());
                Check(g.Dying&&g.PlayerPose==11&&g.AppearanceWeapon==displayed&&(g.VisualFacing-facing).Length<.001f,"An actual lethal followup captures the displayed recoil outfit and facing");
            }
        }
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
