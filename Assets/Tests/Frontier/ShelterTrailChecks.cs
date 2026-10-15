using System;
using System.Collections.Generic;

namespace Starfall.Tests
{
    // Offline shipped Core checks. Positive journeys use real movement and
    // contacts; boundary fixtures explicitly place only the state under test.
    // None of these checks certify native Unity playback or rendering.
    public static partial class CoreChecks
    {
        private static GameCore ShelterGame(Campaign data,int profession=0,bool accepted=true,bool lit=true)
        {
            var g=Fresh(data,profession);g.SkipTutorial();g.save.stage=3;
            foreach(string flag in new[]{"core_1","exp_intro","harbor_report","desert_report","gate_7","bells_lit","boss_7","mountain_report","companion_0","companion_2","personal_2"})g.save.flags.Add(flag);
            g.save.mountainChoice=2;g.save.bells=new[]{2,0,1};g.save.relationships[2]=3;
            g.Enter(7);
            // Mature patrol clearance and the original personal story are earned
            // prerequisites. No new mark, route leg or completion is staged here.
            g.enemies.Clear();g.SelectCompanion(0);
            if(lit)foreach(string flag in new[]{"warm_brazier0","warm_brazier1","warm_brazier2"})g.save.flags.Add(flag);
            ReachStand(g,"keeper");
            if(accepted){Check(g.AcceptShelterTrail(),"The actual keeper accepts the earned road-marker follow-up");g.Close();}
            return g;
        }
        private static void ShelterStand(GameCore g,Point point)
        {
            g.Close();Point[] offsets={new Point(0,.7f),new Point(0,-.7f),new Point(-.7f,0),new Point(.7f,0),new Point()};
            foreach(Point offset in offsets){Point p=point+offset;if(!g.CanStand(p))continue;g.save.x=p.x;g.save.y=p.y;return;}
            throw new Exception("No legal shelter boundary fixture near "+point.x+","+point.y);
        }
        private static int ShelterWalk(GameCore g,Point target)
        {
            g.Close();List<Point> path=g.NavigationPath(target);int frames=0;
            Check(path.Count>0||(g.Position-target).Length<1.6f,"Every trial stop has an actual terrain route");
            while((g.Position-target).Length>=1.5f&&frames++<15000)
            {
                while(path.Count>0&&(path[0]-g.Position).Length<.15f)path.RemoveAt(0);
                Point movement=path.Count>0?(path[0]-g.Position).Normalized:g.DirectionTo(g.Position,target),before=g.Position;
                g.Tick(.02f,movement);
                Check(g.save.room==7&&!g.Dying&&g.CanStand(g.Position),"A starter walks the shelter leg on real terrain without defeat or teleportation");
                Check((g.Position-before).Length<.8f,"Actual shelter walking remains continuous");
            }
            Check(frames<15000&&(g.Position-target).Length<1.6f,"Actual movement reaches its nearby trial stop");return frames;
        }
        private static void ShelterEarnLeg(GameCore g,int leg,bool walking)
        {
            if(walking)ShelterWalk(g,GameCore.ShelterSourcePoints[leg]);else ShelterStand(g,GameCore.ShelterSourcePoints[leg]);
            Check(g.StartShelterLeg(leg)&&g.ShelterLeg==leg,"The current lit brazier deliberately starts the next route leg");
            if(walking)ShelterWalk(g,GameCore.ShelterMarkPoints[leg]);else ShelterStand(g,GameCore.ShelterMarkPoints[leg]);
            Check(g.RecordShelterMark(leg),"The actual current roadside mark records the observation");
            if(walking)ShelterWalk(g,GameCore.ShelterDestinationPoints[leg]);else ShelterStand(g,GameCore.ShelterDestinationPoints[leg]);
            Check(g.ConfirmShelterArrival(leg)&&g.ShelterStep==leg+1&&g.ShelterLeg==-1,"Manual arrival earns exactly one leg and ends its weather attempt");
        }
        private static GameCore ShelterCompletedLegs(Campaign data)
        {
            var g=ShelterGame(data);for(int leg=0;leg<3;leg++)ShelterEarnLeg(g,leg,false);return g;
        }
        private static GameCore ShelterWarning(Campaign data,int cycle=0,Point? exposed=null)
        {
            var g=ShelterGame(data);ShelterStand(g,GameCore.ShelterSourcePoints[0]);Check(g.StartShelterLeg(0),"The contact boundary uses a genuinely started route attempt");
            // Geometry-only fixture: place the body on legal exposed ground before
            // the actual weather clock captures it. This is not a winning run.
            Point start=exposed??new Point(12,13);Check(g.CanStand(start),"Prepared weather fixtures use legal ground");g.save.x=start.x;g.save.y=start.y;g.save.cold=20;g.invulnerable=0;
            for(int n=0;n<cycle;n++)
            {
                for(int frame=0;frame<500&&!g.ShelterWindWarning;frame++)g.Tick(.01f,new Point());
                Check(g.ShelterWindWarning,"The actual weather clock prepares the requested alternating lane");
                Point side=(g.ShelterWindTo-g.ShelterWindFrom).Normalized;side=new Point(-side.y,side.x);
                Point safe=g.ShelterWindCenter+side*1.1f;Check(g.CanStand(safe),"The prior warning has an exposed safe step");g.save.x=safe.x;g.save.y=safe.y;
                for(int frame=0;frame<500&&g.ShelterWindWarning;frame++)g.Tick(.01f,new Point());
                g.save.x=start.x;g.save.y=start.y;
            }
            for(int frame=0;frame<500&&!g.ShelterWindWarning;frame++)g.Tick(.01f,new Point());
            Check(g.ShelterWindWarning&&g.ShelterWindTimer>0&&!g.ShelterColdProtected,"The prepared boundary has a complete warning on unprotected ground");return g;
        }
        private static void ShelterRelease(GameCore g)
        {
            for(int frame=0;frame<500&&g.ShelterWindWarning&&!g.Dying;frame++)g.Tick(.01f,new Point());
            Check(!g.ShelterWindWarning,"The actual warning resolves once rather than lingering as a permanent hazard");
        }

        public static void ShelterAcceptanceRetainsKeeperPersonalStoryAndExplicitCompanion(Campaign data)
        {
            var fresh=Fresh(data);Check(!fresh.ShelterReady&&!fresh.AcceptShelterTrail()&&fresh.GetQuest("shelter").locked,"A fresh hero cannot invent Sen's completed personal promise");
            foreach(string flag in new[]{"mountain_report","companion_2","personal_2"})
            {
                var locked=ShelterGame(data,0,false);locked.save.flags.Remove(flag);int coins=locked.save.coins;
                Check(!locked.ShelterReady&&!locked.AcceptShelterTrail()&&!locked.ShelterAccepted&&locked.save.coins==coins,"An unearned keeper prerequisite rejects acceptance: "+flag);
            }
            var g=ShelterGame(data,0,false);g.CompanionStory(2);
            Check(Array.FindIndex(g.conversation.choices,c=>c.action.StartsWith("shelter_"))<0,"The anywhere companion roster cannot fabricate the local road offer");g.Close();
            g.save.x=6;g.save.y=14;Check(!g.AcceptShelterTrail(),"A stale keeper offer cannot accept from a distant brazier");ReachStand(g,"keeper");
            g.Talk(Object(g,"keeper"));Check(g.conversation.choices[0].action=="gift2"&&g.conversation.choices[1].action=="join2"&&g.conversation.choices[2].action=="close"&&Array.FindIndex(g.conversation.choices,c=>c.action=="shelter_offer")>=3,"The mature keeper gift, invitation and goodbye remain before the local follow-up");g.Close();
            int coins0=g.save.coins,shards=g.save.shards,points=g.save.skillPoints,bond=g.save.relationships[2];
            Check(g.AcceptShelterTrail()&&g.ShelterAccepted&&!g.ShelterReported&&g.ShelterStep==0&&g.ShelterLeg==-1&&g.save.companion==0,"Acceptance starts a manual three-leg road test without silently replacing Mira");
            Check(!g.AcceptShelterTrail()&&g.save.coins==coins0&&g.save.shards==shards&&g.save.skillPoints==points&&g.save.relationships[2]==bond,"Repeat acceptance pays nothing and consumes no resources");
            var personal=ShelterGame(data,0,false);personal.save.flags.Remove("personal_2");personal.save.flags.Add("lore_1");personal.save.inventory[2]=4;
            int beforeCoins=personal.save.coins,beforeShards=personal.save.shards,beforePoints=personal.save.skillPoints,beforeBond=personal.save.relationships[2];Action(personal,"keeper","personal2");
            Check(personal.Has("personal_2")&&personal.save.inventory[2]==0&&personal.save.coins==beforeCoins+35&&personal.save.shards==beforeShards+3&&personal.save.skillPoints==beforePoints+1&&personal.save.relationships[2]==Math.Min(5,beforeBond+2),"The original herb/log story keeps its four-herb cost and original 35/3/point/two-bond reward");
            Check(personal.ShelterReady&&!personal.ShelterAccepted&&personal.save.mountainChoice==2&&personal.save.bells[0]==2&&personal.save.bells[1]==0&&personal.save.bells[2]==1,"Old story completion unlocks only an offer and preserves the mountain choice and bell puzzle");
        }

        public static void ShelterLocalActionsRequireLitBraziersAndOrderedRealMarks(Campaign data)
        {
            var g=ShelterGame(data,0,true,false);ShelterStand(g,GameCore.ShelterSourcePoints[0]);int wood=g.save.inventory[0];
            Check(!g.StartShelterLeg(0)&&!g.StartShelterLeg(-1)&&!g.StartShelterLeg(3),"Dark or invalid sources cannot begin a weather route");
            WorldObject source=Object(g,"brazier");g.Talk(source);int index=Array.FindIndex(g.conversation.choices,c=>c.action=="warm:"+source.id);Check(index>=0,"The original unlit brazier still offers its paid light action");g.Choose(index);
            Check(g.Has("warm_brazier0")&&g.save.inventory[0]==wood-2,"The original brazier lighting still costs exactly two timber");
            Check(!g.StartShelterLeg(0),"The destination must also be lit before the first departure");g.save.flags.Add("warm_brazier1");g.save.flags.Add("warm_brazier2");
            wood=g.save.inventory[0];Check(g.StartShelterLeg(0)&&!g.StartShelterLeg(0)&&!g.StartShelterLeg(1),"The current departure creates one attempt without concurrent or out-of-order legs");
            ShelterStand(g,GameCore.ShelterDestinationPoints[0]);Check(!g.ConfirmShelterArrival(0)&&!g.RecordShelterMark(0),"Arriving without the actual intermediate observation does not complete the leg");
            ShelterStand(g,GameCore.ShelterMarkPoints[0]);Check(!g.RecordShelterMark(1)&&!g.RecordShelterMark(-1)&&!g.RecordShelterMark(3)&&g.RecordShelterMark(0)&&!g.RecordShelterMark(0),"Only the current local mark records once");
            ShelterStand(g,GameCore.ShelterDestinationPoints[0]);Check(g.ConfirmShelterArrival(0)&&!g.ConfirmShelterArrival(0)&&g.ShelterStep==1,"A local recorded arrival advances only its own leg once");
            Check(g.save.inventory[0]==wood&&!g.FinishShelterTrail(true)&&!g.FinishShelterTrail(false),"Route testing has no extra timber charge or early payout");
        }

        public static void ShelterAllStartersWalkThreeRealLegsAndEarnAChosenRoad(Campaign data)
        {
            for(int profession=0;profession<3;profession++)
            {
                var g=ShelterGame(data,profession);int coins=g.save.coins,shards=g.save.shards,points=g.save.skillPoints,xp=g.save.xp,bond=g.save.relationships[2],wood=g.save.inventory[0];int frames=0;
                for(int leg=0;leg<3;leg++)
                {
                    frames+=ShelterWalk(g,GameCore.ShelterSourcePoints[leg]);Check(g.StartShelterLeg(leg),"Actual starter movement reaches its departure");
                    frames+=ShelterWalk(g,GameCore.ShelterMarkPoints[leg]);Check(g.RecordShelterMark(leg),"Actual starter movement records the intermediate mark");
                    frames+=ShelterWalk(g,GameCore.ShelterDestinationPoints[leg]);Check(g.ConfirmShelterArrival(leg),"Actual starter movement reaches the next warm shelter");
                }
                Check(g.ShelterStep==3&&!g.ShelterReported&&g.ShelterLeg==-1&&!g.Dying&&g.save.health>0,"All three earned legs are observed before the manual keeper handoff");
                frames+=ShelterWalk(g,new Point(15,6));bool upper=profession==1;
                Check(g.FinishShelterTrail(upper)&&g.ShelterComplete&&g.ShelterReported&&g.save.coins==coins+35&&g.save.shards==shards+4&&g.save.relationships[2]==Math.Min(5,bond+1),"A genuinely walked starter journey earns the selected maintained road and its advertised single reward");
                Check(g.save.skillPoints==points&&g.save.xp==xp&&g.save.inventory[0]==wood&&g.save.companion==0&&g.Has("personal_2")&&g.Has("boss_7")&&g.save.mountainChoice==2&&g.save.bells[0]==2&&g.save.bells[1]==0&&g.save.bells[2]==1,"The new journey leaves starter equipment, prior story, puzzle, companion and old rewards intact");
                Console.WriteLine("  Shelter starter: class="+profession+", frames="+frames+", hp="+g.save.health+", cold="+g.save.cold+"; NATIVE_UNVERIFIED");
            }
        }

        public static void ShelterWindWarningsCaptureAlternatingClippedFootprintsAndContacts(Campaign data)
        {
            for(int cycle=0;cycle<2;cycle++)foreach(bool inside in new[]{false,true})
            {
                var g=ShelterWarning(data,cycle);Point from=g.ShelterWindFrom,to=g.ShelterWindTo,center=g.ShelterWindCenter;
                Point direction=(to-from).Normalized,side=new Point(-direction.y,direction.x);float length=(to-from).Length;
                Check(length>0&&length<=12.001f&&Math.Abs(cycle==0?direction.y:direction.x)<.001f,"Prepared wind alternates orthogonal terrain-clipped lanes of at most twelve tiles");
                Point p=center+side*(inside?0:GameCore.ShelterWindRadius+.08f);Check(g.CanStand(p),"Wind boundary is actual legal ground");g.save.x=p.x;g.save.y=p.y;g.invulnerable=0;
                float health=g.save.health,cold=g.save.cold,elapsed=g.save.totalTime,timer=g.ShelterWindTimer;
                g.Tick(.01f,new Point(side.x,side.y));
                Check((g.ShelterWindFrom-from).Length<.001f&&(g.ShelterWindTo-to).Length<.001f&&(g.ShelterWindCenter-center).Length<.001f&&g.ShelterWindTimer<timer,"Moving after preparation never retargets or reshapes the current warning");
                g.save.x=p.x;g.save.y=p.y;ShelterRelease(g);
                float passive=(g.save.totalTime-elapsed)*1.1f;
                Check(inside?Math.Abs(health-g.save.health-8)<.001f:g.save.health==health,"Only contact inside the exact displayed wind lane hurts the standard starter");
                Check(Math.Abs(g.save.cold-cold-passive-(inside?7:0))<.06f,"The additional seven cold belongs only to an actual accepted wind hit");
                Check(!g.ShelterReported&&g.ShelterStep==0&&g.enemies.Count==0&&g.hazards.Count==0&&g.projectiles.Count==0,"A weather contact creates no enemy, unrelated persistent hazard, progress or reward");
            }
            var clipped=ShelterWarning(data,0,new Point(16.5f,10.5f));
            Check(clipped.ShelterWindFrom.x>=16&&(clipped.ShelterWindTo-clipped.ShelterWindFrom).Length<12,"The prepared lane genuinely terminates before the mountain wall spanning x14 to x16");
            Point beyond=new Point(12,10.5f);Check(clipped.CanStand(beyond),"The other side of the wall has real legal ground");clipped.save.x=beyond.x;clipped.save.y=beyond.y;float clippedHp=clipped.save.health,clippedCold=clipped.save.cold,clippedTime=clipped.save.totalTime;
            ShelterRelease(clipped);Check(clipped.save.health==clippedHp&&Math.Abs(clipped.save.cold-clippedCold-(clipped.save.totalTime-clippedTime)*1.1f)<.03f,"A displayed wall-clipped wind cannot hurt or add cold through the old terrain");
        }

        public static void ShelterWarningPauseDashAndLitSheltersKeepTheirExistingProtection(Campaign data)
        {
            var pause=ShelterWarning(data);Point center=pause.ShelterWindCenter;float timer=pause.ShelterWindTimer,clock=pause.save.totalTime;
            foreach(string mode in new[]{"pause","dialog","inventory","journal","plans"}){pause.mode=mode;pause.Tick(.05f,new Point(1,0));Check(pause.ShelterWindWarning&&pause.ShelterWindTimer==timer&&pause.save.totalTime==clock&&(pause.ShelterWindCenter-center).Length<.001f,"Paused menus retain the entire captured wind window: "+mode);}pause.Close();
            pause.Tick(0,new Point(1,0));Check(pause.ShelterWindTimer==timer,"A zero-duration tick cannot consume a weather tell");
            var dash=ShelterWarning(data);dash.save.x=dash.ShelterWindCenter.x;dash.save.y=dash.ShelterWindCenter.y;
            while(dash.ShelterWindTimer>.1f)dash.Tick(.01f,new Point());float hp=dash.save.health,cold=dash.save.cold;Point lane=(dash.ShelterWindTo-dash.ShelterWindFrom).Normalized;
            dash.Dash(lane);ShelterRelease(dash);Check(dash.save.health==hp&&dash.save.cold-cold<.25f&&dash.dashCooldown>0,"A real along-lane dodge remains inside the wind shape but its accepted invulnerability prevents both health and extra cold damage");
            var warm=ShelterWarning(data,0,GameCore.ShelterSourcePoints[0]);float warmHp=warm.save.health;warm.save.cold=30;
            Check(GameCore.ShelterSegmentDistance(warm.Position,warm.ShelterWindFrom,warm.ShelterWindTo)<.001f&&warm.ShelterAtLitBrazier,"Lit-brazier immunity is checked inside the actually displayed lane");
            ShelterRelease(warm);Check(warm.save.health==warmHp&&warm.save.cold<30,"The original lit brazier shelter remains immune to lane contact and retains its normal fast warming");
        }

        public static void ShelterReportBothChoicesIsLocalCappedOnceAndCannotReplaceMountainStory(Campaign data)
        {
            foreach(bool upper in new[]{false,true})
            {
                var g=ShelterCompletedLegs(data);g.save.x=6;g.save.y=14;int coins=g.save.coins,shards=g.save.shards,points=g.save.skillPoints;
                Check(!g.FinishShelterTrail(upper),"A completed route still cannot hand in from a distant brazier");ReachStand(g,"keeper");g.save.relationships[2]=5;
                g.mode="pause";Check(!g.FinishShelterTrail(upper),"Paused route callbacks cannot pay the reward");g.Close();g.practicing=true;Check(!g.FinishShelterTrail(upper),"Practice cannot pay a real road reward");g.practicing=false;g.deathTime=.2f;Check(!g.FinishShelterTrail(upper),"Defeat cannot pay a route reward");g.deathTime=0;
                Check(g.FinishShelterTrail(upper)&&g.ShelterReported&&g.ShelterComplete&&g.save.relationships[2]==5&&g.save.coins==coins+35&&g.save.shards==shards+4&&g.save.skillPoints==points,"Both actual keeper decisions grant the same bounded single reward");
                Check(g.Has(upper?"sheltered_upper":"sheltered_lower")&&!g.Has(upper?"sheltered_lower":"sheltered_upper")&&!g.FinishShelterTrail(!upper),"The first road-maintenance decision is exclusive and cannot be reversed for another payout");
                Check(g.save.coins==coins+35&&g.save.shards==shards+4&&g.save.mountainChoice==2&&g.Has("mountain_report")&&g.Has("personal_2")&&g.Has("boss_7"),"Road maintenance preserves mature public names, personal conclusion and Boss identity");
                g.Talk(Object(g,"keeper"));Check(g.conversation.choices[0].action=="gift2"&&g.conversation.choices[1].action=="join2"&&g.conversation.choices[2].action=="close","Keeper rereads preserve old companion choices after the route report");
            }
        }

        public static void ShelterMaintainedRoadPreventsOnlyItsOwnColdGrowthAndRetainsTeaAndBraziers(Campaign data)
        {
            foreach(bool upper in new[]{false,true})
            {
                var g=ShelterCompletedLegs(data);ReachStand(g,"keeper");Check(g.FinishShelterTrail(upper),"Climate fixture earns its chosen maintained route through genuine local route actions");
                Point chosen=upper?new Point(21,8):new Point(12,13),other=upper?new Point(12,13):new Point(21,8);
                g.save.x=chosen.x;g.save.y=chosen.y;g.save.cold=35;g.save.teaUntil=0;float clock=g.save.totalTime;Advance(g,.4f);
                Check(g.ShelterColdProtected&&Math.Abs(g.save.cold-35)<.001f&&g.save.totalTime>clock,"The chosen exposed route stops cold growth without inventing cooling, tea or frozen world time");
                g.save.x=other.x;g.save.y=other.y;g.save.cold=35;Advance(g,.4f);
                Check(!g.ShelterColdProtected&&Math.Abs(g.save.cold-35-.44f)<.01f,"The unchosen exposed route keeps original 1.1-per-second cold accumulation");
                g.save.tea=1;g.DrinkTea();g.save.cold=35;Advance(g,.4f);Check(Math.Abs(g.save.cold-31.8f)<.01f&&g.save.tea==0&&g.save.teaUntil>g.save.totalTime,"The mature paid tea still consumes a cup and warms at the original eight-per-second rate even outside the chosen road");
                g.save.teaUntil=0;ShelterStand(g,GameCore.ShelterSourcePoints[0]);g.save.cold=35;Advance(g,.4f);Check(Math.Abs(g.save.cold-31.8f)<.01f,"The original lit brazier keeps its full warming rate after road maintenance");
                g.Enter(6);g.enemies.Clear();g.save.cold=35;Advance(g,.4f);Check(Math.Abs(g.save.cold-31.8f)<.01f&&!g.ShelterColdProtected,"The added mountain consequence cannot change the mature climate outside its map");
            }
        }

        public static void ShelterCheckpointSavesRetainEarnedMarksWithoutReplayingTransientWeather(Campaign data)
        {
            var g=ShelterGame(data);ShelterEarnLeg(g,0,false);ShelterStand(g,GameCore.ShelterSourcePoints[1]);Check(g.StartShelterLeg(1),"The saved mid-route attempt begins only its next earned leg");ShelterStand(g,GameCore.ShelterMarkPoints[1]);Check(g.RecordShelterMark(1),"The saved intermediate mark is actually earned");
            g.PrepareSave();SaveData checkpoint=CloneSave(g.save);
            foreach(int version in new[]{1,2})
            {
                SaveData copy=CloneSave(checkpoint);copy.version=version;var restored=new GameCore(data,copy);restored.Start();restored.enemies.Clear();
                Check(restored.ShelterAccepted&&restored.ShelterStep==1&&restored.ShelterLeg==-1&&!restored.ShelterWindWarning&&restored.Has("shelter_mark_0")&&restored.Has("shelter_mark_1")&&!restored.Has("shelter_leg_1")&&!restored.ShelterReported,"Save restoration retains exactly earned route observations but never resumes an invisible weather trial");
                ShelterStand(restored,GameCore.ShelterDestinationPoints[1]);Check(!restored.ConfirmShelterArrival(1),"A saved mark alone cannot auto-arrive without a deliberate new attempt");ShelterStand(restored,GameCore.ShelterSourcePoints[1]);Check(restored.StartShelterLeg(1),"The saved next leg is manually retriable from its real warm departure");ShelterStand(restored,GameCore.ShelterDestinationPoints[1]);Check(restored.ConfirmShelterArrival(1)&&restored.ShelterStep==2,"The earned mark survives a manual full-warning retry without duplicate progress");
            }
            foreach(int version in new[]{1,2})
            {
                var old=new SaveData{version=version,room=0,stage=3,x=18,y=15,coins=91,shards=17,mountainChoice=2,flags=new List<string>{"intro","core_1","exp_intro","harbor_report","mountain_report","companion_2","personal_2","boss_7","bells_lit","gate_7"}};
                var restored=new GameCore(data,old);restored.Start();Check(restored.ShelterReady&&!restored.ShelterAccepted&&!restored.ShelterReported&&restored.ShelterLeg==-1&&restored.save.coins==91&&restored.save.shards==17&&restored.save.mountainChoice==2,"Both mature save versions keep the road-marker story but invent no new trial, reward or maintained road");
            }
            var complete=ShelterCompletedLegs(data);ReachStand(complete,"keeper");complete.FinishShelterTrail(true);complete.PrepareSave();var loaded=new GameCore(data,CloneSave(complete.save));loaded.Start();loaded.enemies.Clear();ReachStand(loaded,"keeper");int money=loaded.save.coins;
            Check(loaded.ShelterComplete&&loaded.Has("sheltered_upper")&&!loaded.FinishShelterTrail(false)&&loaded.save.coins==money,"The chosen maintained road and paid-once reward survive a real save round trip");
        }

        public static void ShelterAttemptsCancelOnTravelDefeatPracticeAndExplicitStop(Campaign data)
        {
            foreach(string boundary in new[]{"travel","practice","death","stop"})
            {
                var g=ShelterWarning(data);int coins=g.save.coins,shards=g.save.shards;
                if(boundary=="travel")g.Enter(0);
                else if(boundary=="practice"){g.practicing=true;g.Tick(.01f,new Point());}
                else if(boundary=="death"){g.save.health=1;g.invulnerable=0;g.projectiles.Add(new Projectile{position=g.Position,damage=999,source="shot"});g.Tick(.01f,new Point());Check(g.Dying,"The route cancellation is caused by a real lethal projectile contact");}
                else Check(g.StopShelterLeg(),"The real explicit stop action cancels an active attempt");
                Check(g.ShelterLeg==-1&&!g.ShelterWindWarning&&g.ShelterStep==0&&!g.ShelterReported&&g.save.coins==(boundary=="death"?Math.Max(0,coins-10):coins)&&g.save.shards==shards,"Transient route weather stops at the world boundary without progression or reward: "+boundary);
            }
            var stale=ShelterGame(data);ShelterStand(stale,GameCore.ShelterSourcePoints[0]);
            foreach(string mode in new[]{"pause","inventory","journal","dialog"}){stale.mode=mode;Check(!stale.StartShelterLeg(0),"A paused departure callback revalidates the live mode");}stale.Close();
            stale.enemies.Add(new Enemy(49,0,stale.save.x+1,stale.save.y,40));Check(!stale.StartShelterLeg(0),"A nearby live patrol must be cleared before a quiet route departure");stale.enemies.Clear();Check(stale.StartShelterLeg(0),"Quiet departure begins after the real local prerequisite clears");
            ShelterStand(stale,GameCore.ShelterMarkPoints[0]);stale.enemies.Add(new Enemy(50,0,stale.save.x+1,stale.save.y,40));Check(!stale.RecordShelterMark(0),"Nearby combat prevents an unsafe route observation");stale.enemies.Clear();Check(stale.RecordShelterMark(0),"The current mark remains interactable after local combat clears");
        }

        public static void ShelterJournalAppendsAfterAllMatureIdsAndOwnsReachableProgress(Campaign data)
        {
            string[] old={"main","shore","ally1","ally2","ally3","personal0","personal1","personal2","bounty","secrets","watch","signal","archive","reach","homeward","commission"};
            Check(GameCore.QuestIds.Length==18&&GameCore.QuestIds[16]=="shelter"&&GameCore.QuestIds[17]=="fieldcraft","The shelter follow-up retains its saved seventeenth index while only field notes append afterward");for(int i=0;i<old.Length;i++)Check(GameCore.QuestIds[i]==old[i],"All mature quest identities preserve their exact saved index");
            var g=ShelterGame(data);g.TrackQuest("shelter");HashSet<int> reached=ArenaReachable(g);
            for(int leg=0;leg<3;leg++)
            {
                Check(ReachNearSeen(g,reached,Object(g,"shelterMark"+leg)),"Every new marker has a real full-footprint terrain approach");
                Check((g.CurrentGuide.target-GameCore.ShelterSourcePoints[leg]).Length<.01f&&(g.NavigationPath(g.CurrentGuide.target).Count>0||(g.Position-g.CurrentGuide.target).Length<1.75f),"The gold route points to the next actual departure: leg="+leg+", tracked="+g.save.trackedQuest+", target="+g.CurrentGuide.target.x+","+g.CurrentGuide.target.y+", path="+g.NavigationPath(g.CurrentGuide.target).Count);
                ShelterStand(g,GameCore.ShelterSourcePoints[leg]);Check(g.StartShelterLeg(leg),"The guided departure deliberately starts its owned leg");
                Check((g.CurrentGuide.target-GameCore.ShelterMarkPoints[leg]).Length<.01f,"The gold route changes to the actual current roadside observation");
                ShelterStand(g,GameCore.ShelterMarkPoints[leg]);Check(g.RecordShelterMark(leg),"The guided mark is actually recorded");
                Check((g.CurrentGuide.target-GameCore.ShelterDestinationPoints[leg]).Length<.01f,"The observed leg guides the actual warm arrival rather than an earlier marker");
                ShelterStand(g,GameCore.ShelterDestinationPoints[leg]);Check(g.ConfirmShelterArrival(leg),"The guided arrival earns only its own completed leg");
            }
            Check(!g.CurrentGuide.complete&&(g.CurrentGuide.target-new Point(15,6)).Length<.01f,"All observed legs guide the manual report back to Sen");ReachStand(g,"keeper");Check(g.FinishShelterTrail(false)&&g.CurrentGuide.complete,"Only the actual keeper handoff completes the new journal");
            g.Enter(0);Check(g.ShelterComplete&&!g.ShelterWindWarning,"Travelling away from the complete road cannot recreate an active weather event");
        }
    }
}
