using System;
using System.Collections.Generic;

namespace Starfall.Tests
{
    // Offline simulation evidence. Earned first-core/crafting materials and live
    // enemy formations are explicit fixtures; all new notes use real actions and
    // HP contacts, without invented profiles, invulnerability or completion flags.
    public static partial class CoreChecks
    {
        private static GameCore FieldGame(Campaign data,int profession=0,bool accepted=true)
        {
            var g=Fresh(data,profession);g.SkipTutorial();g.save.flags.Add("core_1");ReachStand(g,"mentor");
            if(accepted)Check(g.AcceptFieldcraft(),"Actual Ada accepts the earned field assignment");
            return g;
        }
        private static void FieldCraftWeapons(GameCore g)
        {
            g.Enter(0);g.save.inventory[0]+=4;g.save.inventory[1]+=4;g.save.inventory[4]+=5;
            Action(g,"forge","bow");g.Close();Action(g,"forge","staff");g.Close();
            Check(g.Has("weapon_1")&&g.Has("weapon_2"),"Existing forge transactions craft both owned weapons from an earned-material fixture");
        }
        private static void FieldArena(GameCore g,int weapon,int style=0)
        {
            g.Enter(1);g.enemies.Clear();g.save.x=12;g.save.y=14;g.save.reducedMotion=true;g.Equip(weapon);g.SetStyle(style);
            Check(g.CanStand(g.Position)&&g.save.weapon==weapon&&g.Style==style,"The live formation uses legal mature terrain and actual equipment controls");
        }
        private static Enemy FieldFoe(GameCore g,int id,float x,float y,float health=70)
        {var e=new Enemy(id,0,x,y,health);g.enemies.Add(e);Check(g.CanStand(e.Position),"The explicit live-foe formation stands on legal ground");return e;}
        private static ActorAction FieldAttack(GameCore g,Point direction)
        {
            for(int f=0;f<200&&(g.attackCooldown>0||g.playerAction!=null||g.dashTime>0);f++)g.Tick(.01f,new Point());
            g.Attack(direction);Check(g.playerAction!=null&&g.playerAction.kind=="attack"&&!g.playerAction.released,"A real basic attack is accepted before contact");return g.playerAction;
        }
        private static void FieldFlight(GameCore g,float seconds=.9f)
        {for(int f=0;f<(int)(seconds*100);f++){g.Tick(.01f,new Point());Check(!g.Dying,"The ordinary living hero survives the formation without a test immunity grant");}}
        private static void FieldSword(GameCore g)
        {
            FieldArena(g,0);Enemy e=FieldFoe(g,81,13.1f,14,90);e.timer=.05f;
            for(int f=0;f<500&&(e.state!=1||e.timer>.025f);f++)g.Tick(.01f,new Point());
            Check(e.state==1&&e.timer<=.025f&&g.riposteTime<=0,"A real hunter AI reaches the final wind-up before Space");
            float health=g.save.health;g.Dash(new Point(-1,0));
            for(int f=0;f<30&&g.dashTime>0;f++)g.Tick(.01f,new Point());
            Check(g.riposteTime>0&&g.save.health==health,"Space blocks the real hunter release and earns the existing perfect-dodge counter window");
            for(int f=0;f<100&&(g.Position-e.Position).Length>1.35f;f++)g.Tick(.01f,g.DirectionTo(g.Position,e.Position));
            Check(g.riposteTime>0,"Normal walking returns to sword reach while the real counter window remains open");
            ActorAction action=FieldAttack(g,e.Position-g.Position);Check(action.impact.counter,"The actual accepted sword action captures the earned counter");
            FieldFlight(g,.5f);Check(e.hp<e.maxHp&&g.FieldcraftRecorded(0),"The actual perfect-dodge sword contact records the first demonstration");
        }
        private static void FieldBow(GameCore g,bool gearSwitch=false)
        {
            FieldArena(g,1,1);Enemy a=FieldFoe(g,82,17,14),b=FieldFoe(g,83,19,14);
            ActorAction action=FieldAttack(g,new Point(1,0));
            for(int f=0;f<100&&!action.released;f++)g.Tick(.01f,new Point());
            Check(action.released&&g.projectiles.Exists(p=>p.impact==action.impact&&p.piercing),"One accepted focused basic arrow is launched with its immutable action identity");
            if(gearSwitch){g.Equip(0);g.SetStyle(2);}
            FieldFlight(g,1.3f);Check(a.hp<a.maxHp&&b.hp<b.maxHp&&g.FieldcraftRecorded(1),"The same live arrow reaches two different foes"+(gearSwitch?" after a real equipment change":""));
        }
        private static void FieldStaff(GameCore g)
        {
            FieldArena(g,2);Enemy a=FieldFoe(g,84,15,14),b=FieldFoe(g,85,15.7f,14.55f),c=FieldFoe(g,86,15.7f,13.45f);
            FieldAttack(g,new Point(1,0));FieldFlight(g,.9f);
            Check(a.hp<a.maxHp&&b.hp<b.maxHp&&c.hp<c.maxHp&&g.FieldcraftRecorded(2),"One genuine starfire impact damages its direct foe and both live splash neighbors");
        }
        private static GameCore FieldEarnAll(Campaign data,int profession=0)
        {
            var g=FieldGame(data,profession);FieldCraftWeapons(g);FieldSword(g);FieldBow(g);FieldStaff(g);
            Check(g.FieldcraftProgress==3&&g.FieldcraftDemonstrationsComplete&&!g.FieldcraftComplete,"Three real demonstrations unlock only a manual hand-in");return g;
        }

        public static void FieldcraftAcceptanceKeepsAdaPracticeAndRequiresEarnedLocalWorld(Campaign data)
        {
            var fresh=Fresh(data);fresh.SkipTutorial();ReachStand(fresh,"mentor");Check(!fresh.FieldcraftReady&&!fresh.AcceptFieldcraft()&&fresh.GetQuest("fieldcraft").locked,"The first core gates field notes without replacing old practice");
            var g=FieldGame(data,0,false);g.Talk(Object(g,"mentor"));
            Check(g.conversation.choices.Length>=4&&g.conversation.choices[0].action=="practice"&&g.conversation.choices[1].action=="close"&&g.conversation.choices[2].action=="sparring_menu"&&g.conversation.choices[3].action=="fieldcraft_offer","All three mature Ada choices retain their exact prefix");g.Close();
            g.save.x=20;g.save.y=14;Check(!g.AcceptFieldcraft(),"A distant stale mentor callback cannot accept");ReachStand(g,"mentor");
            foreach(string mode in new[]{"pause","inventory","journal","dialog","plans"}){g.mode=mode;Check(!g.AcceptFieldcraft(),"Acceptance revalidates live world mode: "+mode);}g.Close();
            g.practicing=true;Check(!g.AcceptFieldcraft(),"Practice cannot accept world demonstrations");g.practicing=false;g.deathTime=.2f;Check(!g.AcceptFieldcraft(),"A defeated hero cannot accept");g.deathTime=0;
            int coins=g.save.coins,shards=g.save.shards,points=g.save.skillPoints,weapon=g.save.weapon,profession=g.save.profession;float health=g.save.health,energy=g.save.energy;
            Check(g.AcceptFieldcraft()&&!g.AcceptFieldcraft()&&g.FieldcraftAccepted&&g.FieldcraftProgress==0&&!g.FieldcraftComplete,"Explicit acceptance creates no automatic notes or duplicate reward");
            Check(g.save.coins==coins&&g.save.shards==shards&&g.save.skillPoints==points&&g.save.weapon==weapon&&g.save.profession==profession&&g.save.health==health&&g.save.energy==energy,"Acceptance preserves paid growth, resources, vitals, class and equipment");
        }

        public static void FieldcraftAllThreeProfessionsEarnActualSwordBowStaffAndReport(Campaign data)
        {
            for(int profession=0;profession<3;profession++)
            {
                var g=FieldEarnAll(data,profession);int coins=g.save.coins,shards=g.save.shards,points=g.save.skillPoints;
                Check(!g.FinishFieldcraft(),"Earned field notes cannot report from the combat map");g.Enter(0);ReachStand(g,"mentor");
                Check(g.FinishFieldcraft()&&g.FieldcraftComplete&&g.save.coins==coins+40&&g.save.shards==shards+5&&g.save.skillPoints==points+1&&g.save.profession==profession,"Each original class earns the complete field journey and exact one-time Ada reward");
                Check(!g.FinishFieldcraft()&&g.save.coins==coins+40&&g.save.shards==shards+5&&g.save.skillPoints==points+1,"Repeated hand-in cannot pay twice");
                Console.WriteLine("  Fieldcraft actual journey: class="+profession+", hp="+g.save.health+", notes="+g.FieldcraftProgress+"; NATIVE_UNVERIFIED");
            }
        }

        public static void FieldcraftBowRequiresOneFocusedArrowAndCapturedGear(Campaign data)
        {
            var g=FieldGame(data,1);FieldArena(g,1,0);Enemy plainA=FieldFoe(g,87,15,14),plainB=FieldFoe(g,88,17,14);
            FieldAttack(g,new Point(1,0));FieldFlight(g,.9f);Check(plainA.hp<plainA.maxHp&&plainB.hp==plainB.maxHp&&!g.FieldcraftRecorded(1)&&g.FieldcraftContactCount(1)==0,"An ordinary basic arrow cannot invent the focused piercing demonstration");
            FieldArena(g,1,1);Enemy first=FieldFoe(g,89,15,14);FieldAttack(g,new Point(1,0));FieldFlight(g,.9f);Check(first.hp<first.maxHp&&g.FieldcraftContactCount(1)==1&&!g.FieldcraftRecorded(1),"A single damaged foe is only one contact");
            g.enemies.Clear();Enemy second=FieldFoe(g,90,15,14);FieldAttack(g,new Point(1,0));FieldFlight(g,.9f);Check(second.hp<second.maxHp&&!g.FieldcraftRecorded(1),"Two arrows contacting one different foe each cannot accumulate a fake same-arrow note");
            FieldBow(g,true);Check(g.save.weapon==0&&g.Style==2,"A launched bow note keeps its real captured source instead of borrowing current sword gear");
        }

        public static void FieldcraftStaffCountsDistinctLiveBlastContactsIncludingLethal(Campaign data)
        {
            var g=FieldGame(data,2);FieldArena(g,2);Enemy a=FieldFoe(g,91,15,14),b=FieldFoe(g,92,15.7f,14.5f);
            FieldAttack(g,new Point(1,0));FieldFlight(g,.9f);Check(a.hp<a.maxHp&&b.hp<b.maxHp&&g.FieldcraftContactCount(2)==2&&!g.FieldcraftRecorded(2),"One blast damaging only two foes cannot finish a three-foe note");
            g.enemies.Clear();Enemy c=FieldFoe(g,93,15,14);FieldAttack(g,new Point(1,0));FieldFlight(g,.9f);Check(c.hp<c.maxHp&&!g.FieldcraftRecorded(2),"A second separate blast cannot borrow contacts from the first");
            FieldArena(g,2);Enemy lethal=FieldFoe(g,94,15,14,1),left=FieldFoe(g,95,15.7f,14.5f,1),right=FieldFoe(g,96,15.7f,13.5f,1);
            FieldAttack(g,new Point(1,0));FieldFlight(g,.9f);Check(!g.enemies.Contains(lethal)&&!g.enemies.Contains(left)&&!g.enemies.Contains(right)&&g.FieldcraftRecorded(2),"Real lethal HP contacts count before ordinary defeat removes the three actors");
        }

        public static void FieldcraftOnlyRegisteredPostAcceptanceBasicsCount(Campaign data)
        {
            var g=FieldGame(data,1,false);FieldArena(g,1,1);Enemy first=FieldFoe(g,97,15,14),second=FieldFoe(g,98,17,14);
            // Explicit acceptance boundary: an old launched arrow cannot acquire
            // witness ownership even if acceptance happens before its contact.
            FieldAttack(g,new Point(1,0));g.save.flags.Add("fieldcraft_accepted");FieldFlight(g,1.1f);
            Check(first.hp<first.maxHp&&second.hp<second.maxHp&&!g.FieldcraftRecorded(1),"An attack launched before acceptance has no registered ownership");
            var skill=FieldGame(data,1);FieldArena(skill,1,1);FieldFoe(skill,99,15,14);FieldFoe(skill,100,17,14);skill.save.technique=1;skill.Skill(new Point(1,0));FieldFlight(skill,1.2f);
            Check(skill.enemies.Exists(e=>e.hp<e.maxHp)&&!skill.FieldcraftRecorded(1)&&skill.FieldcraftContactCount(1)==0,"A real piercing Ranger Q cannot impersonate one focused basic arrow");
            var staff=FieldGame(data,2);FieldArena(staff,2);FieldFoe(staff,101,14.3f,14);FieldFoe(staff,102,14.3f,14.5f);FieldFoe(staff,103,14.3f,13.5f);staff.Skill(new Point(1,0));FieldFlight(staff,.9f);
            Check(staff.enemies.Exists(e=>e.hp<e.maxHp)&&!staff.FieldcraftRecorded(2),"The real Arcanist Q area cannot impersonate a basic starfire blast");
            var practice=FieldGame(data,1);practice.StartPractice();practice.SetStyle(1);practice.Attack(new Point(1,0));FieldFlight(practice,.8f);Check(practice.practicing&&!practice.FieldcraftRecorded(1)&&practice.FieldcraftProgress==0,"Mature safe practice still works but records no field achievement");
            var plain=FieldGame(data);FieldArena(plain,0);Enemy ordinary=FieldFoe(plain,110,13.2f,14);FieldAttack(plain,new Point(1,0));FieldFlight(plain,.5f);
            Check(ordinary.hp<ordinary.maxHp&&!plain.FieldcraftRecorded(0),"An ordinary sword hit without a genuinely earned dodge-counter cannot count");
            var ally=FieldGame(data);ally.save.flags.Add("companion_2");ally.SelectCompanion(2);FieldArena(ally,0);FieldFoe(ally,111,14.5f,14);FieldFoe(ally,112,14.5f,14.5f);FieldFoe(ally,113,14.5f,13.5f);ally.CommandCompanion();FieldFlight(ally,.9f);
            Check(ally.enemies.Exists(e=>e.hp<e.maxHp)&&ally.FieldcraftProgress==0,"A real companion-command frost contact cannot impersonate the hero's staff demonstration");
            var sparring=FieldGame(data);Check(sparring.StartSparring(),"Ada's original tactical sparring still starts after accepting field notes");Enemy drill=sparring.enemies[0];
            for(int frame=0;frame<400&&drill.hp==drill.maxHp;frame++){Point direction=sparring.DirectionTo(sparring.Position,drill.Position);sparring.Tick(.01f,(drill.Position-sparring.Position).Length>1.3f?direction:new Point());sparring.Attack(direction);}
            Check(drill.hp<drill.maxHp&&sparring.SparringActive&&sparring.FieldcraftProgress==0,"Actual tactical-drill HP contacts still count for sparring but never field notes");
        }

        public static void FieldcraftConcurrentActionsKeepIndependentOwnershipAndSafeRoomsStayExcluded(Campaign data)
        {
            var g=FieldGame(data,1);FieldArena(g,1,1);Enemy a=FieldFoe(g,114,20,14),b=FieldFoe(g,115,22,14);
            ActorAction first=FieldAttack(g,new Point(1,0));for(int frame=0;frame<100&&!first.released;frame++)g.Tick(.01f,new Point());
            ActorAction second=FieldAttack(g,new Point(-1,0));Check(first.impact!=second.impact&&a.hp==a.maxHp&&b.hp==b.maxHp,"A second real basic action aimed away from the foes starts while the first distant arrow is still live and uncontacted");
            FieldFlight(g,1.3f);Check(a.hp<a.maxHp&&b.hp<b.maxHp&&g.FieldcraftRecorded(1),"Registering a concurrent action cannot overwrite the first arrow's independent two-foe witness");
            foreach(int room in new[]{0,9})
            {
                var safe=FieldGame(data,2);safe.Enter(room);safe.enemies.Clear();safe.save.x=12;safe.save.y=14;safe.save.reducedMotion=true;
                Enemy one=FieldFoe(safe,116,15,14),two=FieldFoe(safe,117,15.7f,14.5f),three=FieldFoe(safe,118,15.7f,13.5f);
                FieldAttack(safe,new Point(1,0));FieldFlight(safe,.9f);Check(one.hp<one.maxHp&&two.hp<two.maxHp&&three.hp<three.maxHp&&safe.FieldcraftProgress==0,"Explicit malformed live foes in safe room "+room+" cannot turn town/gallery contacts into expedition notes");
            }
        }

        public static void FieldcraftWhiffsCanceledWindupsPauseAndExpiredContactsStayUnpaid(Campaign data)
        {
            var g=FieldGame(data,1);FieldArena(g,1,1);FieldAttack(g,new Point(-1,0));FieldFlight(g,.7f);Check(g.FieldcraftProgress==0,"An accepted whiff never records a hit");
            Enemy a=FieldFoe(g,104,15,14),b=FieldFoe(g,105,17,14);FieldAttack(g,new Point(1,0));g.Equip(0);FieldFlight(g,.8f);Check(a.hp==a.maxHp&&b.hp==b.maxHp&&g.FieldcraftProgress==0,"A gear-canceled wind-up cannot emit or later record an old shot");
            g.Equip(1);g.SetStyle(1);ActorAction prepared=FieldAttack(g,new Point(1,0));float clock=g.save.totalTime;g.mode="pause";Advance(g,2);
            Check(g.save.totalTime==clock&&!prepared.released&&g.FieldcraftProgress==0,"Paused simulation freezes both attack contact and witness lifetime");g.Close();FieldFlight(g,1.1f);Check(g.FieldcraftRecorded(1),"Resuming preserves the same legitimate accepted arrow without a second input");
            var expired=FieldGame(data,1);FieldArena(expired,1,1);Enemy x=FieldFoe(expired,106,15,14),y=FieldFoe(expired,107,17,14);ActorAction action=FieldAttack(expired,new Point(1,0));
            for(int frame=0;frame<100&&!action.released;frame++)expired.Tick(.01f,new Point());
            // Deliberately malformed late-flight snapshot boundary; the action
            // itself is real, but its time cannot authorize contacts forever.
            expired.save.totalTime+=6;FieldFlight(expired,1.1f);Check(x.hp<x.maxHp&&y.hp<y.maxHp&&!expired.FieldcraftRecorded(1),"An expired action registry rejects late reused contacts without suppressing actual damage");
        }

        public static void FieldcraftPartialContactsResetAcrossWorldBoundariesButNotesPersist(Campaign data)
        {
            foreach(string boundary in new[]{"load","travel","practice","death"})
            {
                var g=FieldGame(data,1);FieldArena(g,1,1);FieldFoe(g,108,15,14);FieldAttack(g,new Point(1,0));FieldFlight(g,.8f);
                Check(g.FieldcraftContactCount(1)==1&&!g.FieldcraftRecorded(1),"A real partial live-arrow witness exists before "+boundary);
                if(boundary=="load"){g.PrepareSave();g.Load(CloneSave(g.save));g.Start();}
                else if(boundary=="travel")g.Enter(0);
                else if(boundary=="practice"){g.Enter(0);g.StartPractice();}
                else {for(int frame=0;frame<100&&!g.Dying;frame++){if(frame%5==0)g.projectiles.Add(new Projectile{position=g.Position,damage=999,source="shot"});g.Tick(.01f,new Point());}Check(g.Dying,"A genuine lethal hostile projectile causes the reset boundary after any existing hurt immunity expires naturally");}
                Check(g.FieldcraftContactCount(1)==0&&!g.FieldcraftRecorded(1)&&g.FieldcraftAccepted,"Transient one-arrow contacts cannot survive "+boundary+" or mint a completed note");
            }
            var earned=FieldGame(data,1);FieldBow(earned);earned.PrepareSave();var restored=new GameCore(data,CloneSave(earned.save));restored.Start();
            Check(restored.FieldcraftRecorded(1)&&restored.FieldcraftProgress==1&&!restored.FieldcraftComplete,"Only a genuinely completed note survives the ordinary save path");
        }

        public static void FieldcraftReportingRechecksLocalSafetyModesAndPaysExactlyOnce(Campaign data)
        {
            // This transaction boundary fixture represents already-earned notes;
            // the complete real journey above earns those same flags via combat.
            var g=FieldGame(data);foreach(string suffix in new[]{"sword","bow","staff"})g.save.flags.Add("fieldcraft_"+suffix);
            int coins=g.save.coins,shards=g.save.shards,points=g.save.skillPoints;g.save.x=25;g.save.y=14;Check(!g.FinishFieldcraft(),"A distant report cannot pay");ReachStand(g,"mentor");
            foreach(string mode in new[]{"pause","inventory","journal","dialog","plans"}){g.mode=mode;Check(!g.FinishFieldcraft(),"Report revalidates mode "+mode);}g.Close();
            g.practicing=true;Check(!g.FinishFieldcraft(),"Training cannot pay a world hand-in");g.practicing=false;g.deathTime=.2f;Check(!g.FinishFieldcraft(),"Defeat cannot pay a hand-in");g.deathTime=0;
            Enemy nearby=FieldFoe(g,109,g.save.x+.7f,g.save.y);Check(!g.FinishFieldcraft(),"A nearby live foe blocks an unsafe hand-in");g.enemies.Remove(nearby);
            Check(g.FinishFieldcraft()&&!g.FinishFieldcraft()&&g.save.coins==coins+40&&g.save.shards==shards+5&&g.save.skillPoints==points+1,"The single actual mentor transaction grants exactly its advertised reward");
            g.PrepareSave();var loaded=new GameCore(data,CloneSave(g.save));loaded.Start();ReachStand(loaded,"mentor");Check(loaded.FieldcraftComplete&&!loaded.FinishFieldcraft()&&loaded.save.coins==coins+40,"Paid completion remains paid after load");
        }

        public static void FieldcraftJournalLegacyAndQuestPrefixRemainCompatible(Campaign data)
        {
            string[] prefix={"main","shore","ally1","ally2","ally3","personal0","personal1","personal2","bounty","secrets","watch","signal","archive","reach","homeward","commission","shelter"};
            Check(GameCore.QuestIds.Length>=19&&GameCore.QuestIds[17]=="fieldcraft"&&GameCore.QuestIds[18]=="supper","Field notes retain eighteenth place when later stories append after supper");for(int i=0;i<prefix.Length;i++)Check(GameCore.QuestIds[i]==prefix[i],"Every mature quest keeps its saved index "+i);
            foreach(int version in new[]{1,2})
            {
                var legacy=new SaveData{version=version,presentationVersion=1,coins=91,shards=17,health=62,energy=44,trackedQuest="shelter"};legacy.flags.Add("core_1");legacy.flags.Add("shelter_accepted");
                var g=new GameCore(data,legacy);g.Start();Check(g.FieldcraftReady&&!g.FieldcraftAccepted&&g.FieldcraftProgress==0&&!g.FieldcraftComplete&&g.save.coins==91&&g.save.shards==17&&g.save.health==62&&g.save.energy==44&&g.save.trackedQuest=="shelter","Legacy version "+version+" gains no fabricated notes, rewards, healing or changed tracking");
            }
            var guide=FieldGame(data);guide.TrackQuest("fieldcraft");Check(!guide.CurrentGuide.complete&&guide.CurrentGuide.id=="fieldcraft"&&guide.CurrentGuide.steps.Count==5,"The accepted journal exposes all three explicit demonstrations and manual hand-in");
            foreach(int invalid in new[]{-1,3,int.MinValue,int.MaxValue})Check(!guide.FieldcraftRecorded(invalid)&&guide.FieldcraftContactCount(invalid)==0,"Malformed public weapon indices cannot invent a demonstration");
        }
    }
}
