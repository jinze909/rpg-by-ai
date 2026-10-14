using System;
using System.Collections.Generic;

namespace Starfall.Tests
{
    // Core simulation checks, including actual starter combat. Snapshot and
    // contact boundary fixtures deliberately stage only the boundary under test.
    // These checks do not certify native Unity import, rendering or game feel.
    public static partial class CoreChecks
    {
        private static GameCore CommissionGame(Campaign data, int profession = 0, bool accepted = true)
        {
            var g = Fresh(data, profession); g.SkipTutorial(); g.save.stage = 3;
            foreach (string flag in new[] { "core_1", "exp_intro", "harbor_report", "gate_6", "mirrors_lit", "boss_6", "desert_report", "companion_0", "companion_1", "personal_1" })
                g.save.flags.Add(flag);
            g.save.desertChoice = 2; g.save.mirrors = new[] { 1, 2, 1 }; g.save.relationships[1] = 3;
            g.Enter(6); g.enemies.Clear(); g.SelectCompanion(0); ReachStand(g,"tinker");
            if (accepted) { Check(g.AcceptCommission(), "The actual engineer accepts the earned shared-machine trial"); g.Close(); }
            return g;
        }
        private static void CommissionStand(GameCore g, Point point)
        {
            g.Close(); Point[] offsets = { new Point(0,.7f),new Point(0,-.7f),new Point(-.7f,0),new Point(.7f,0) };
            foreach (Point offset in offsets)
            {
                Point p = point + offset;
                if (!g.CanStand(p)) continue;
                g.save.x = p.x; g.save.y = p.y;
                return;
            }
            throw new Exception("No legal Commission boundary fixture near " + point.x + "," + point.y);
        }
        private static Enemy CommissionStart(GameCore g)
        {
            CommissionStand(g,GameCore.CommissionReceiverPoint);
            Check(g.StartCommissionEncounter(),"The nearby receiver deliberately begins the earned commissioning attempt");
            Enemy e = g.enemies.Find(GameCore.IsCommissionGuardian);
            Check(e != null && e.hp == GameCore.CommissionGuardianHealth && g.CommissionActive && e.Position.x == GameCore.CommissionGuardianPoint.x && e.Position.y == GameCore.CommissionGuardianPoint.y,
                "An attempt owns exactly the canonical fixed full-health test guardian");
            Check(g.CurrentBoss.en=="Commissioning guardian"&&g.CurrentBoss.hp==260&&g.EnemyHint(e)==GameCore.CommissionHint(e,g.save.english),"The mature Boss HUD and hints identify the active trial rather than the already defeated Glass Custodian");
            return e;
        }
        private static Enemy CommissionWarning(GameCore g, int pattern)
        {
            Enemy e = g.enemies.Find(GameCore.IsCommissionGuardian);
            Check(e != null,"A real started test guardian exists before warning boundary setup");
            // This is an explicit pattern boundary fixture, not a winning run.
            e.state=0; e.timer=.01f; e.cycle=pattern; e.stun=e.hitTime=e.releaseTime=0;
            g.save.x=e.x; g.save.y=e.y+2.5f; g.invulnerable=0;
            for(int frame=0;frame<100&&e.state!=1;frame++)g.Tick(.01f,new Point());
            Check(e.state==1&&GameCore.CommissionPattern(e)==pattern,"The actual AI prepares the chosen test pattern");
            Check(g.EnemyHint(e)==GameCore.CommissionHint(e,g.save.english),"The world hint uses the same current beam/mark/valve guidance as the displayed test warning");
            return e;
        }
        private static void CommissionRelease(GameCore g, Enemy e)
        {
            int cycle=e.cycle;
            for(int frame=0;frame<400&&e.cycle==cycle;frame++)g.Tick(.01f,new Point());
            Check(!g.Dying&&e.cycle==cycle+1&&e.state==0&&e.releaseTime>0,"The announced pattern releases once through normal simulation");
        }
        private static void CommissionTestedFixture(GameCore g)
        {
            // Transaction/snapshot controls intentionally start at an earned
            // success checkpoint. Real defeat is checked by all three starters.
            foreach(string flag in new[]{"commission_started","commission_tested"}) if(!g.Has(flag))g.save.flags.Add(flag);
            g.enemies.RemoveAll(GameCore.IsCommissionGuardian);
        }
        private static int CommissionFight(GameCore g)
        {
            int frames=0,attacks=0,skills=0,dodges=0,vents=0; Point fixedFoot=GameCore.CommissionGuardianPoint;
            while(g.CommissionActive&&frames++<18000)
            {
                Check(g.save.room==6&&!g.Dying,"A standard starter survives the actual trial without a test invulnerability grant");
                Enemy e=g.enemies.Find(GameCore.IsCommissionGuardian);
                Check(e!=null&&(e.Position-fixedFoot).Length<.0001f,"The machine's contact and hit reactions never move its captured station");
                float distance=(e.Position-g.Position).Length; Point toward=g.DirectionTo(g.Position,e.Position);
                Point movement=distance>1.3f?toward:new Point();
                if(e.state==1)
                {
                    int pattern=GameCore.CommissionPattern(e);
                    if(pattern==2)
                    {
                        Point receiver=GameCore.CommissionReceiverPoint;
                        movement=g.DirectionTo(g.Position,receiver);
                        if((g.Position-receiver).Length<1.7f&&g.TryCommissionVent()){vents++;movement=toward;}
                    }
                    else if(pattern==0)
                    {
                        Point side=new Point(-e.facing.y,e.facing.x);
                        float lateral=(g.Position.x-e.attackOrigin.x)*side.x+(g.Position.y-e.attackOrigin.y)*side.y;
                        if(Math.Abs(lateral)<GameCore.CommissionBeamRadius+.5f)
                        {movement=side*(lateral<0?-1:1);if(e.timer<.45f){float clock=g.dashCooldown;g.Dash(movement);if(g.dashCooldown>clock)dodges++;}}
                    }
                    else if((g.Position-e.target).Length<GameCore.CommissionRadius(e)+.4f)
                    {
                        movement=(g.Position-e.target).Length>.05f?(g.Position-e.target).Normalized:new Point(1,0);
                        if(e.timer<.45f){float clock=g.dashCooldown;g.Dash(movement);if(g.dashCooldown>clock)dodges++;}
                    }
                }
                g.Tick(.02f,movement);
                if(!g.CommissionActive)break;
                if(g.save.health<g.save.maxHealth*.6f&&g.save.inventory[6]>0)g.Eat();
                if(g.save.heat>55&&g.save.tea>0)g.DrinkTea();
                if(distance<(g.save.profession==0?3:6)){float clock=g.skillCooldown;g.Skill(e.Position-g.Position);if(g.skillCooldown>clock)skills++;}
                float old=g.attackCooldown;g.Attack(e.Position-g.Position);if(g.attackCooldown>old)attacks++;
                if(g.save.companion>=0)g.CommandCompanion();
            }
            Check(frames<18000&&!g.Dying&&g.CommissionTested&&!g.CommissionActive,
                "The real starter reaches commissioning victory: profession="+g.save.profession+", hp="+g.save.health+", frames="+frames);
            Check(attacks>0&&skills>0,"Starter victory includes accepted Attack and Q contacts rather than forced enemy HP or deletion");
            Console.WriteLine("  Commission starter: class="+g.save.profession+", frames="+frames+", hp="+g.save.health+", attacks="+attacks+", Q="+skills+", dodges="+dodges+", vents="+vents+"; NATIVE_UNVERIFIED");
            return frames;
        }

        public static void CommissionAcceptanceRequiresEarnedActualEngineerAndPreservesPersonalRepair(Campaign data)
        {
            var fresh=Fresh(data);Check(!fresh.CommissionReady&&!fresh.CommissionAccepted&&!fresh.AcceptCommission(),"A fresh hero has no fabricated shared-machine promise");
            foreach(string missing in new[]{"personal_1","companion_1","desert_report"})
            {
                var locked=CommissionGame(data,0,false);locked.save.flags.Remove(missing);int coins=locked.save.coins;
                Check(!locked.CommissionReady&&!locked.AcceptCommission()&&!locked.CommissionAccepted&&locked.save.coins==coins,"An unearned original prerequisite cannot be bypassed: "+missing);
            }
            var g=CommissionGame(data,0,false);int money=g.save.coins,shards=g.save.shards,points=g.save.skillPoints;
            g.CompanionStory(1);Check(Array.FindIndex(g.conversation.choices,c=>c.action.StartsWith("commission_"))<0,"The anywhere roster story cannot invent a local engineer offer");g.Close();
            g.save.x=5;g.save.y=14;Check(!g.AcceptCommission(),"A stale offer cannot be accepted from the desert entry");ReachStand(g,"tinker");
            foreach(string mode in new[]{"pause","inventory","dialog","journal","plans"}){g.mode=mode;Check(!g.AcceptCommission(),"Acceptance revalidates live world mode: "+mode);}g.Close();
            g.practicing=true;Check(!g.AcceptCommission(),"Practice cannot begin a real machine event");g.practicing=false;
            g.deathTime=.2f;Check(!g.AcceptCommission(),"A fallen hero cannot accept a trial");g.deathTime=0;
            g.Talk(Object(g,"tinker"));Check(g.conversation.choices[0].action=="gift1"&&g.conversation.choices[1].action=="join1"&&g.conversation.choices[2].action=="close"&&Array.FindIndex(g.conversation.choices,c=>c.action=="commission_offer")>=3,
                "The completed old Tomorrow story retains gift, explicit travel and goodbye before the new local trial");g.Close();
            Check(g.AcceptCommission()&&g.CommissionAccepted&&g.save.companion==0&&!g.CommissionActive,"Acceptance starts only the new story and never silently swaps Mira for Ivo");
            Check(!g.AcceptCommission()&&g.save.coins==money&&g.save.shards==shards&&g.save.skillPoints==points&&g.Has("personal_1")&&g.save.desertChoice==2,"Repeat acceptance neither repays nor replaces the original repaired-machine reward and desert decision");
            var repair=CommissionGame(data,0,false);repair.save.flags.Remove("personal_1");repair.save.inventory[4]=4;repair.save.inventory[1]=4;
            int oldCoins=repair.save.coins,oldShards=repair.save.shards,oldPoints=repair.save.skillPoints;Action(repair,"tinker","personal1");
            Check(repair.Has("personal_1")&&repair.save.inventory[4]==0&&repair.save.inventory[1]==0&&repair.save.coins==oldCoins+35&&repair.save.shards==oldShards+3&&repair.save.skillPoints==oldPoints+1,
                "The mature four-part/four-crystal personal repair still pays its original 35/3/point independently");
            Check(repair.CommissionReady&&!repair.CommissionAccepted,"Completing the old repair unlocks an offer rather than automatically accepting or spawning the new event");
        }

        public static void CommissionStartsAndVentRevalidateLocalWindows(Campaign data)
        {
            var g=CommissionGame(data);int coins=g.save.coins,shards=g.save.shards;
            Check(!g.StartCommissionEncounter(),"The engineer conversation is not the remote receiver start");CommissionStand(g,GameCore.CommissionReceiverPoint);
            g.enemies.Add(new Enemy(19,0,g.save.x+1,g.save.y,40));Check(!g.StartCommissionEncounter(),"The test cannot stack onto nearby ordinary combat");g.enemies.Clear();
            foreach(string mode in new[]{"pause","dialog","inventory"}){g.mode=mode;Check(!g.StartCommissionEncounter(),"Starting rechecks the paused callback mode");}g.Close();
            g.practicing=true;Check(!g.StartCommissionEncounter(),"Practice cannot spawn a world test actor");g.practicing=false;
            g.deathTime=.2f;Check(!g.StartCommissionEncounter(),"Defeat cannot begin the event");g.deathTime=0;
            Enemy e=CommissionStart(g);Check(!g.StartCommissionEncounter()&&g.enemies.FindAll(GameCore.IsCommissionGuardian).Count==1,"Repeat interaction never duplicates the canonical test guardian");
            for(int pattern=0;pattern<2;pattern++){CommissionWarning(g,pattern);CommissionStand(g,GameCore.CommissionReceiverPoint);Check(!g.TryCommissionVent(),"The receiver cannot interrupt the beam or paired release outside its advertised charge window");}
            CommissionWarning(g,2);CommissionStand(g,GameCore.CommissionReceiverPoint);e.timer=.35f;
            Check(!g.TryCommissionVent(),"The final .35 seconds do not accept a late unadvertised vent");e.timer=.351f;
            g.mode="pause";Check(!g.TryCommissionVent(),"A paused receiver callback cannot vent");g.Close();g.practicing=true;Check(!g.TryCommissionVent(),"Practice cannot vent the world guardian");g.practicing=false;
            g.deathTime=.2f;Check(!g.TryCommissionVent(),"A fallen hero cannot vent");g.deathTime=0;
            g.save.x=22;g.save.y=18;Check(!g.TryCommissionVent(),"Leaving the receiver invalidates its captured operation callback");CommissionStand(g,GameCore.CommissionReceiverPoint);
            int cycle=e.cycle;Check(g.TryCommissionVent()&&e.state==0&&e.stun>0&&e.timer>=2&&e.cycle==cycle+1,"A timely nearby receiver action genuinely cancels the charge into a recovery window");
            Check(!g.TryCommissionVent()&&g.CommissionVents==1&&g.save.coins==coins&&g.save.shards==shards&&!g.CommissionTested,"A vent has feedback but cannot fabricate a victory, repeat or grant rewards");
        }

        public static void CommissionPreparedFootprintsKeepTimingAcrossRealThresholdHits(Campaign data)
        {
            for(int pattern=0;pattern<3;pattern++)foreach(int phase in new[]{1,2})
            {
                var g=CommissionGame(data);Enemy e=CommissionStart(g);e.hp=e.maxHp*(phase==1?.67f:.34f);CommissionWarning(g,pattern);
                Point origin=e.attackOrigin,destination=e.attackDestination,target=e.target,facing=e.facing;float radius=GameCore.CommissionRadius(e),duration=e.windupDuration;
                Check(Math.Abs(duration-(pattern==0?1.15f:pattern==1?1.3f:1.8f))<.001f,"The displayed test pattern has its complete authored preparation");
                Check(pattern!=0||Math.Abs((destination-origin).Length-8)<.001f,"The fixed beam warning shows its exact eight-tile reach");
                g.save.x=e.x;g.save.y=e.y+.8f;float hp=e.hp;g.Attack(e.Position-g.Position);
                for(int frame=0;frame<80&&e.hp==hp;frame++)g.Tick(.01f,new Point());
                Check(e.hp<hp&&e.hp>0&&e.BossPhase==phase+1&&e.state==1,"An actual ordinary hit crosses the live HP threshold during this prepared warning");
                Check(GameCore.CommissionPattern(e)==pattern&&GameCore.CommissionRadius(e)==radius&&e.windupDuration==duration&&(e.attackOrigin-origin).Length<.001f&&(e.attackDestination-destination).Length<.001f&&(e.target-target).Length<.001f&&(e.facing-facing).Length<.001f,
                    "Health thresholds and real reactions cannot reshape, retarget or shorten the already prepared test move");
                Check((e.Position-GameCore.CommissionGuardianPoint).Length<.001f,"Actual hit recoil cannot move the stationary warning origin");
                float timer=e.timer;g.mode="pause";g.Tick(.05f,new Point(1,0));
                Check(e.timer==timer&&e.state==1&&e.windupDuration==duration,"Paused UI freezes the same prepared pattern without consuming its operation window");g.Close();
            }
        }

        public static void CommissionWarningsMatchBeamCircleAndUnventedChargeContacts(Campaign data)
        {
            for(int pattern=0;pattern<3;pattern++)foreach(bool inside in new[]{false,true})
            {
                var g=CommissionGame(data);Enemy e=CommissionStart(g);CommissionWarning(g,pattern);
                Point p;if(pattern==0){Point side=new Point(-e.facing.y,e.facing.x);p=e.attackOrigin+e.facing*2+side*(inside?.2f:GameCore.CommissionBeamRadius+.08f);}
                else p=e.target+new Point(inside?0:GameCore.CommissionRadius(e)+.08f,0);
                Check(g.CanStand(p),"Contact boundary stands on the actual desert footprint");g.save.x=p.x;g.save.y=p.y;g.invulnerable=0;float health=g.save.health;
                CommissionRelease(g,e);float damage=pattern==0?20:pattern==1?22:24;
                Check(inside?Math.Abs(health-g.save.health-damage)<.001f:g.save.health==health,"Actual damage matches only the advertised fixed footprint for pattern "+pattern+" inside="+inside);
                Check(g.projectiles.Count==0&&g.hazards.Count==0&&g.enemies.Count==1,"Test releases cannot inherit desert chapter pillars, generic radial shots or reinforcements");
                Check((e.Position-GameCore.CommissionGuardianPoint).Length<.001f,"The beam and fallback charge remain anchored after release");
            }
        }

        public static void CommissionEveryStarterWinsActualTrialAndEarnsFirstWater(Campaign data)
        {
            for(int profession=0;profession<3;profession++)
            {
                var g=CommissionGame(data,profession);g.SelectCompanion(1);
                int coins=g.save.coins,xp=g.save.xp,shards=g.save.shards,points=g.save.skillPoints,kills=g.save.kills[6],bond=g.save.relationships[1],mastery=g.save.mastery[g.save.weapon],tea=g.save.tea;
                int[] inventory=(int[])g.save.inventory.Clone();int[] mirrors=(int[])g.save.mirrors.Clone();g.AcceptBounty(3);
                Enemy e=CommissionStart(g);Check(e.hp==260&&g.save.difficulty==1&&g.save.upgrades==0&&g.save.talents[0]==0&&g.save.talents[1]==0&&g.save.talents[2]==0,"Every starter gets the same fixed 260 HP trial on standard difficulty with starter gear");
                CommissionFight(g);Advance(g,.6f);ReachStand(g,"tinker");bool oasis=profession!=1;
                Check(g.CommissionTested&&!g.CommissionReported&&g.save.coins==coins&&g.save.xp==xp&&g.save.shards==shards&&g.save.kills[6]==kills&&g.save.skillPoints==points&&g.save.bountyProgress==0&&g.save.mastery[g.save.weapon]==mastery,
                    "Actual commissioning defeat records success without farming normal Boss money, XP, shards, skill points, kills, contracts or mastery");
                for(int i=0;i<6;i++)Check(g.save.inventory[i]==inventory[i],"Defeating the test machine cannot award mature material loot");
                Check(g.FinishCommission(oasis)&&g.CommissionReported&&g.save.coins==coins+35&&g.save.shards==shards+4&&g.save.relationships[1]==Math.Min(5,bond+1)&&g.save.skillPoints==points,
                    "Ivo's actual manual commissioning handoff grants the independent advertised 35 coins/four shards/bond once");
                CommissionStand(g,g.CommissionWaterPoint);Check(g.CollectCommissionWater()&&g.CommissionComplete&&g.save.tea==tea+1,"The actual selected delivery point supplies the first playable cup and completes the chain");
                Check(g.Has("personal_1")&&g.Has("desert_report")&&g.Has("boss_6")&&g.save.desertChoice==2&&g.save.mirrors[0]==mirrors[0]&&g.save.mirrors[1]==mirrors[1]&&g.save.mirrors[2]==mirrors[2],"Trial, handoff and water leave the original personal story, desert branch and solved light puzzle intact");
                Check(!g.FinishCommission(!oasis)&&!g.CollectCommissionWater()&&!g.StartCommissionEncounter(),"The completed same-day chain cannot repeat reward, reverse delivery or respawn a farming target");
                g.Enter(0);g.Enter(6);Check(!g.CommissionActive&&g.CommissionComplete&&g.save.coins==coins+35&&g.save.shards==shards+4,"Travel after genuine completion never creates a second paid guardian or handoff");
            }
        }

        public static void CommissionReportIsLocalOnceAndKeepsMatureDesertChoices(Campaign data)
        {
            foreach(bool oasis in new[]{false,true})
            {
                var g=CommissionGame(data);Check(!g.FinishCommission(oasis),"Accepting without a actual success checkpoint cannot report victory");CommissionTestedFixture(g);
                g.save.x=5;g.save.y=14;Check(!g.FinishCommission(oasis),"A successful test still requires a local engineer handoff");ReachStand(g,"tinker");
                g.mode="dialog";Check(!g.FinishCommission(oasis),"Paused handoffs cannot execute world transactions");g.Close();g.practicing=true;Check(!g.FinishCommission(oasis),"Practice handoffs cannot grant real rewards");g.practicing=false;
                g.deathTime=.2f;Check(!g.FinishCommission(oasis),"A defeated hero cannot claim the commissioning payment");g.deathTime=0;g.save.relationships[1]=5;
                int coins=g.save.coins,shards=g.save.shards,points=g.save.skillPoints;Check(g.FinishCommission(oasis)&&g.save.relationships[1]==5&&g.save.coins==coins+35&&g.save.shards==shards+4&&g.save.skillPoints==points,"Both local delivery choices use the same capped-bond reward without duplicating the old personal talent point");
                Check(!g.FinishCommission(!oasis)&&g.save.coins==coins+35&&g.save.shards==shards+4,"The opposite choice cannot replay a successful transaction");
                Point expected=oasis?new Point(8,17):new Point(6,12);Check((g.CommissionWaterPoint-expected).Length<.001f,"The delivery decision selects the actual existing oasis or caravan camp");
                g.Talk(Object(g,"tinker"));Check(g.conversation.choices[0].action=="gift1"&&g.conversation.choices[1].action=="join1"&&g.conversation.choices[2].action=="close"&&g.save.desertChoice==2&&g.Has("personal_1"),"Commissioned machine rereads retain the mature companion choices and desert allocation");
            }
        }

        public static void CommissionRealHitSnapshotsKeepCanonicalHealthAndFreshWarnings(Campaign data)
        {
            var g=CommissionGame(data);Enemy e=CommissionStart(g);g.save.x=e.x;g.save.y=e.y+.8f;float health=e.hp;g.Attack(e.Position-g.Position);
            for(int frame=0;frame<80&&e.hp==health;frame++)g.Tick(.01f,new Point());Check(e.hp>0&&e.hp<health,"Snapshot survivor health comes from a real accepted contact");
            for(int frame=0;frame<300&&e.state!=1;frame++)g.Tick(.01f,new Point());
            Check(e.state==1&&e.windupDuration>0,"The actual hit survivor later prepares a real fresh warning");
            for(int frame=0;frame<120&&e.timer>.6f;frame++)g.Tick(.01f,new Point());
            Check(e.state==1&&e.timer<e.windupDuration,"The saved actual warning is partially elapsed rather than a staged idle enemy");
            g.PrepareSave();SaveData checkpoint=CloneSave(g.save);
            foreach(int version in new[]{1,2})
            {
                SaveData copy=CloneSave(checkpoint);copy.version=version;var restored=new GameCore(data,copy);restored.Start();Enemy kept=restored.enemies.Find(GameCore.IsCommissionGuardian);
                Check(kept!=null&&kept.hp==e.hp&&kept.maxHp==260&&(kept.Position-GameCore.CommissionGuardianPoint).Length<.001f&&kept.state==0&&kept.windupDuration==0&&kept.releaseTime==0&&kept.timer>=1,
                    "Version "+version+" preserves actual survivor HP on the canonical machine with a fresh full warning rather than partial hidden attack");
                for(int frame=0;frame<300&&kept.state!=1;frame++)restored.Tick(.01f,new Point());
                Check(kept.state==1&&Math.Abs(kept.windupDuration-1.15f)<.001f&&Math.Abs(kept.timer-kept.windupDuration)<.001f,"The restored AI announces the complete first beam before any contact resumes");
            }
            foreach(EnemyRecord invalid in new[]{new EnemyRecord{id=78000,kind=0,boss=true,x=29,y=13,hp=99,maxHp=260},new EnemyRecord{id=78000,kind=3,boss=false,x=29,y=13,hp=99,maxHp=260},new EnemyRecord{id=78000,kind=3,boss=true,x=1,y=1,hp=99,maxHp=260},new EnemyRecord{id=78000,kind=3,boss=true,x=float.NaN,y=13,hp=99,maxHp=260},new EnemyRecord{id=78000,kind=3,boss=true,x=29,y=13,hp=float.NaN,maxHp=260},new EnemyRecord{id=78000,kind=3,boss=true,x=29,y=13,hp=99,maxHp=float.NaN}})
            {
                SaveData copy=CloneSave(checkpoint);copy.worldEnemies=new[]{invalid};var loaded=new GameCore(data,copy);loaded.Start();
                Check(!loaded.CommissionActive&&!loaded.CommissionTested&&loaded.CommissionAccepted,"An invalid optional Boss snapshot cannot fabricate an active or defeated machine");
            }
            SaveData oversized=CloneSave(checkpoint);oversized.worldEnemies=new[]{new EnemyRecord{id=78000,kind=3,boss=true,x=29,y=13,hp=999,maxHp=5000}};
            var bounded=new GameCore(data,oversized);bounded.Start();Enemy canonical=bounded.enemies.Find(GameCore.IsCommissionGuardian);
            Check(canonical!=null&&canonical.maxHp==260&&canonical.hp<=260,"Finite corrupted maxima cannot replace the authored machine health");
            SaveData forest=CloneSave(checkpoint);forest.room=forest.snapshotRoom=1;forest.x=5;forest.y=14;forest.flags.Add("scout_saved");
            forest.worldEnemies=new[]{new EnemyRecord{id=78000,kind=3,boss=true,x=29,y=13,hp=99,maxHp=260},new EnemyRecord{id=17,kind=0,x=13,y=14,hp=30,maxHp=50}};
            var allocator=new GameCore(data,forest);allocator.Start();Check(allocator.enemies.Count==1&&allocator.enemies[0].id==17,"Rejecting the reserved machine preserves an unrelated original forest actor");
            allocator.save.x=28;allocator.save.y=10;AwaitArenaOpening(allocator);Action(allocator,"shrine","boss");
            Enemy oldBoss=allocator.enemies.Find(x=>x.boss);Check(oldBoss!=null&&oldBoss.id==18&&!GameCore.IsCommissionGuardian(oldBoss),"A reserved foreign snapshot cannot inflate the mature chapter guardian allocator");
            var abyss=Unlocked(data);abyss.StartAbyss(0);SaveData expedition=CloneSave(abyss.save);expedition.abyssSnapshotFloor=expedition.abyssFloor;
            expedition.abyssEnemies=new[]{new EnemyRecord{id=78000,kind=3,boss=true,x=29,y=13,hp=99,maxHp=260},new EnemyRecord{id=17,kind=0,x=15,y=14,hp=20,maxHp=40}};
            var abyssLoaded=new GameCore(data,expedition);abyssLoaded.Start();Check(abyssLoaded.InAbyss&&abyssLoaded.enemies.Count==1&&abyssLoaded.enemies[0].id==17&&!abyssLoaded.CommissionActive,"Reserved test-machine snapshots cannot become expedition foes or replace the original abyss actor");
            foreach(int room in new[]{0,1,5,GameCore.ArchiveRoom,GameCore.ReachRoom})
            {
                SaveData foreign=CloneSave(checkpoint);foreign.room=foreign.snapshotRoom=room;foreign.x=data.maps[room].spawnX;foreign.y=data.maps[room].spawnY;
                var loaded=new GameCore(data,foreign);loaded.Start();Check(!loaded.CommissionActive&&!loaded.enemies.Exists(x=>x.id==78000),"The reserved test-machine identity cannot restore in a foreign mature map "+room);
            }
        }

        public static void CommissionTravelAndActualDefeatRequireDeliberateUnpaidRetries(Campaign data)
        {
            var g=CommissionGame(data);CommissionStart(g);int coins=g.save.coins,shards=g.save.shards,tea=g.save.tea;
            g.Enter(0);g.Enter(6);Check(g.CommissionAccepted&&!g.CommissionActive&&!g.CommissionTested&&!g.CommissionReported,"Leaving an unresolved trial preserves its accepted promise but never respawns an ambush or grants victory");
            // This is an unpaid travel/death control fixture. Isolate only the
            // seven mature respawned patrols; never remove the new test actor.
            g.enemies.RemoveAll(x=>!GameCore.IsCommissionGuardian(x));
            Enemy retry=CommissionStart(g);Check(retry.hp==260,"A deliberate travel retry starts a fresh canonical machine");
            g.save.health=1;g.invulnerable=0;g.projectiles.Add(new Projectile{position=g.Position,damage=999,source="shot"});g.Tick(.01f,new Point());
            Check(g.Dying&&!g.CommissionTested&&!g.StartCommissionEncounter()&&!g.TryCommissionVent(),"Actual lethal contact cannot turn failed attendance into test success or operation");
            g.PrepareSave();var savedDeath=new GameCore(data,CloneSave(g.save));savedDeath.Start();
            Check(savedDeath.save.room==0&&savedDeath.CommissionAccepted&&!savedDeath.CommissionActive&&!savedDeath.CommissionTested&&savedDeath.save.shards==shards&&savedDeath.save.tea==tea,"Saving the fallen pose recovers once in town without test payment or a cup");
            Advance(g,1);Check(g.save.room==0&&!g.CommissionActive&&!g.CommissionTested&&g.save.shards==shards&&g.save.tea==tea,"Normal death recovery safely abandons the attempt");
            g.Enter(6);Check(!g.CommissionActive,"Returning after death does not silently restart a warned encounter");g.enemies.RemoveAll(x=>!GameCore.IsCommissionGuardian(x));CommissionStart(g);
            Check(g.save.coins<=coins&&g.save.shards==shards&&g.save.tea==tea,"Retries and real defeat have no commissioning reward payout");
        }

        public static void CommissionDailyWaterStaysChosenLocalBoundedAndIndependentOfRest(Campaign data)
        {
            foreach(bool oasis in new[]{false,true})
            {
                var g=CommissionGame(data);CommissionTestedFixture(g);ReachStand(g,"tinker");Check(g.FinishCommission(oasis),"Daily-water transaction fixture reports at the actual engineer");
                Point actual=g.CommissionWaterPoint,other=oasis?new Point(6,12):new Point(8,17);
                CommissionStand(g,other);Check(!g.CollectCommissionWater(),"A delivery choice does not enable collection at the other existing site");CommissionStand(g,actual);
                foreach(string mode in new[]{"pause","inventory","dialog"}){g.mode=mode;Check(!g.CollectCommissionWater(),"Daily collection rechecks its live world mode");}g.Close();
                g.practicing=true;Check(!g.CollectCommissionWater(),"Practice cannot stock real shared water");g.practicing=false;g.deathTime=.2f;Check(!g.CollectCommissionWater(),"A defeated hero cannot collect");g.deathTime=0;
                g.enemies.Add(new Enemy(19,0,g.save.x+1,g.save.y,40));Check(!g.CollectCommissionWater(),"The supply transaction requires a quiet chosen local point");g.enemies.Clear();
                int tea=g.save.tea,coins=g.save.coins,shards=g.save.shards;Check(g.CollectCommissionWater()&&g.save.tea==tea+1&&g.CommissionComplete,"First local cup completes the already reported story");
                Check(!g.CollectCommissionWater()&&g.save.tea==tea+1,"The same-day machine cup cannot repeat");int day=g.Day;g.save.totalTime=day*240+.01f;
                Check(g.CollectCommissionWater()&&g.save.tea==tea+2&&g.save.coins==coins&&g.save.shards==shards,"The next in-game day provides one playable tea without replaying story payment");
                g.save.totalTime=g.Day*240+.01f;g.save.tea=999;Check(!g.CollectCommissionWater()&&g.save.tea==999,"The tea-stock ceiling rejects rather than consuming the day's collection");
                g.save.tea=998;Check(g.CollectCommissionWater()&&g.save.tea==999&&!g.CollectCommissionWater(),"An actual available stock slot still permits that unspent day once");
                g.PrepareSave();var resumed=new GameCore(data,CloneSave(g.save));resumed.Start();CommissionStand(resumed,actual);
                Check(!resumed.CollectCommissionWater()&&resumed.save.tea==999&&resumed.CommissionComplete,"Saved daily collection and chosen first cup do not duplicate on resume");
                resumed.Enter(0);float clock=resumed.save.totalTime;Action(resumed,"rest","rest");Check(Math.Abs(resumed.save.totalTime-clock-45)<.01f&&resumed.save.tea==999,"The old town rest still advances exactly 45 seconds and grants no extra machine cup");
                resumed.Enter(6);ReachStand(resumed,"campfire");resumed.save.projects[1]=0;float campClock=resumed.save.totalTime;Action(resumed,"campfire","fieldrest");
                Check(resumed.save.totalTime==campClock&&resumed.save.projects[1]==0,"Shared water cannot bypass the mature waystation requirement for field rest");
            }
        }

        public static void CommissionJournalPrefixLegacyAndForeignSnapshotBoundaries(Campaign data)
        {
            string[] old={"main","shore","ally1","ally2","ally3","personal0","personal1","personal2","bounty","secrets","watch","signal","archive","reach","homeward"};
            Check(GameCore.QuestIds.Length==16&&GameCore.QuestIds[15]=="commission","The machine trial appends one journal entry after all fifteen mature quests");
            for(int i=0;i<old.Length;i++)Check(GameCore.QuestIds[i]==old[i],"The existing quest prefix retains exact index and identity: "+old[i]);
            var g=CommissionGame(data,0,false);Check(!g.GetQuest("commission").locked&&!g.GetQuest("commission").complete&&g.GetQuest("commission").room==6,"The earned unaccepted guide points to the original desert engineer");
            Check(g.AcceptCommission()&&g.save.trackedQuest=="commission","Real acceptance tracks the appended story");Check((g.GetQuest("commission").target-GameCore.CommissionReceiverPoint).Length<.01f,"The accepted unstarted guide targets the actual reservoir");
            CommissionTestedFixture(g);Check((g.GetQuest("commission").target-new Point(22,18)).Length<.01f,"A genuine success checkpoint returns the handoff guide to Ivo");ReachStand(g,"tinker");Check(g.FinishCommission(true),"The guide fixture reports the earned trial");
            Check((g.GetQuest("commission").target-g.CommissionWaterPoint).Length<.01f&&!g.GetQuest("commission").complete,"Handoff tracks the actual chosen first water point rather than hiding the remaining playable step");CommissionStand(g,g.CommissionWaterPoint);g.CollectCommissionWater();Check(g.GetQuest("commission").complete,"Only the earned complete chain marks its journal completion");
            foreach(int version in new[]{1,2})
            {
                var legacy=new SaveData{version=version,room=0,stage=3,x=18,y=15,coins=91,shards=17,desertChoice=2,flags=new List<string>{"intro","core_1","exp_intro","harbor_report","desert_report","companion_1","personal_1","boss_6","mirrors_lit","gate_6"}};
                var loaded=new GameCore(data,legacy);loaded.Start();Check(loaded.save.coins==91&&loaded.save.shards==17&&loaded.save.desertChoice==2&&loaded.Has("personal_1")&&loaded.CommissionReady&&!loaded.CommissionAccepted&&!loaded.CommissionActive&&!loaded.CommissionComplete,"Old saves retain the mature machine story but invent no attendance, completion, cup or reward");
            }
            SaveData completed=CloneSave(g.save);completed.snapshotRoom=6;completed.worldEnemies=new[]{new EnemyRecord{id=78000,kind=3,boss=true,x=29,y=13,hp=260,maxHp=260}};
            var paid=new GameCore(data,completed);paid.Start();Check(paid.CommissionComplete&&!paid.CommissionActive,"A stale full-health machine snapshot cannot replay a completed commissioning chain");
            var unearned=Fresh(data);foreach(string flag in new[]{"commission_accepted","commission_started","commission_tested","commission_reported","commission_water_first"})unearned.save.flags.Add(flag);
            unearned.Load(CloneSave(unearned.save));unearned.Start();Check(!unearned.CommissionAccepted&&!unearned.CommissionActive&&!unearned.CommissionComplete&&!unearned.CollectCommissionWater(),"Missing mature prerequisites cannot turn stray new flags into a usable paid machine or daily supply");
            foreach(Enemy wrong in new[]{new Enemy(78000,0,29,13,1),new Enemy(78000,0,29,13,1,true),new Enemy(78000,3,29,13,1),new Enemy(78000,3,29,13,1,true)})
            {
                var guarded=CommissionGame(data);guarded.enemies.Add(wrong);guarded.save.x=29;guarded.save.y=13.8f;
                int money=guarded.save.coins,xp=guarded.save.xp,kills=guarded.save.kills[6],mastery=guarded.save.mastery[guarded.save.weapon];guarded.Attack(wrong.Position-guarded.Position);
                for(int frame=0;frame<100&&guarded.enemies.Contains(wrong);frame++)guarded.Tick(.01f,new Point());
                Check(!guarded.enemies.Contains(wrong)&&!guarded.CommissionTested&&guarded.save.coins==money&&guarded.save.xp==xp&&guarded.save.kills[6]==kills&&guarded.save.mastery[guarded.save.weapon]==mastery,"Actual defeat of a malformed or unstarted reserved actor cannot invent trial success or ordinary chapter loot");
            }
            Check(!GameCore.IsCommissionGuardian(new Enemy(78000,0,29,13,260,true))&&!GameCore.IsCommissionGuardian(new Enemy(78000,3,29,13,260))&&!GameCore.IsCommissionGuardian(new Enemy(77999,3,29,13,260,true)),"The optional machine identity requires reserved ID, canonical kind and Boss role together");
        }
    }
}
