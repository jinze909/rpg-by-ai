using System;
namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        private static bool GuideTargetsObject(GameCore g, QuestGuide q, string kind)
        { foreach (var o in g.data.maps[q.room].objects) if (o.kind == kind && (q.target - new Point(o.x, o.y)).Length < .001f) return true; return false; }
        private static void GatherGuidedResource(GameCore g, string kind)
        {
            QuestGuide q = g.CurrentGuide; Check(GuideTargetsObject(g, q, kind), "A gather guide must select an actual " + kind + " node");
            g.Enter(q.room); g.enemies.Clear(); g.save.x = q.target.x; g.save.y = q.target.y;
            WorldObject node = null; foreach (var o in g.Map.objects) if (o.kind == kind && (q.target - new Point(o.x, o.y)).Length < .001f) node = o;
            Check(node != null && !g.Has("node_" + node.id), "The selected resource must still be available"); g.Talk(node);
            Check(g.Has("node_" + node.id), "The ordinary resource interaction consumes the selected node");
        }
        public static void GatheringRequestsGuideRestAndDelivery(Campaign data)
        {
            foreach (int resident in new[] { 1, 3 })
            {
                var g = Fresh(data); g.SkipTutorial(); int item = resident == 1 ? 2 : 4; string kind = resident == 1 ? "herb" : "ore";
                g.save.inventory[item] = 0; g.TrackQuest("ally" + resident); GatherGuidedResource(g, kind);
                Check(g.save.inventory[item] == 3 && !g.CurrentGuide.complete, "The first node retains its three supplies and cannot finish the request");
                QuestGuide rest = g.CurrentGuide; Check(rest.room == 0 && GuideTargetsObject(g, rest, "rest"), "An exhausted accessible gathering area must point home to a real rest interaction");
                Check(g.NavigationTarget().HasValue && (g.NavigationTarget().Value - new Point(Object(g, "portal0").x, Object(g, "portal0").y)).Length < .001f, "The field navigation leads home when rest is needed");
                Check(rest.next.Contains("休息"), "The Chinese next step explains the required rest instead of repeating collection");
                g.PrepareSave(); var resumed = new GameCore(data, CloneSave(g.save)); resumed.Start();
                Check(resumed.save.trackedQuest == "ally" + resident && resumed.CurrentGuide.room == rest.room && (resumed.CurrentGuide.target - rest.target).Length < .001f, "Consumed-node rest guidance survives a real save resume");
                resumed.save.english = true; Check(resumed.CurrentGuide.next.IndexOf("rest", StringComparison.OrdinalIgnoreCase) >= 0, "Rest guidance is actionable in English too");
                g = resumed; g.Enter(0); float before = g.save.totalTime; Action(g, "rest", "rest");
                Check(g.save.totalTime == before + 45 && g.CurrentGuide.room == 1 && GuideTargetsObject(g, g.CurrentGuide, kind), "The existing forty-five-second rest renews gathering and sends the player back");
                GatherGuidedResource(g, kind); Check(g.save.inventory[item] == 6, "A second ordinary gathering event provides six total supplies");
                QuestGuide delivery = g.CurrentGuide; string recipient = resident == 1 ? "garden" : "forge";
                Check(delivery.room == 0 && GuideTargetsObject(g, delivery, recipient) && delivery.next.IndexOf("deliver", StringComparison.OrdinalIgnoreCase) >= 0, "Enough supplies switch navigation and text to the original recipient");
                g.Enter(0); int coins = g.save.coins, points = g.save.skillPoints, stew = g.save.inventory[6]; Action(g, recipient, "ally" + resident);
                Check(g.Has("ally_" + resident) && g.CurrentGuide.complete && g.NavigationTarget() == null, "The normal delivery completes and stops the tracked request");
                Check(g.save.inventory[item] == (resident == 1 ? 0 : 1) && g.save.coins == coins + 40 && g.save.skillPoints == points + 1 && g.save.inventory[6] == stew + 1, "Delivery preserves the original costs and rewards");
            }
        }
        public static void ResourceGuidesChooseFreshUnlockedNodes(Campaign data)
        {
            var g = Fresh(data); g.SkipTutorial(); g.save.stage = 1; g.Enter(2); g.enemies.Clear(); g.TrackQuest("ally3");
            Check(g.CurrentGuide.room == 2 && GuideTargetsObject(g, g.CurrentGuide, "ore"), "An available current-area node wins over a distant default");
            g.Talk(Object(g, "ore")); g.save.inventory[4] = 0;
            Check(g.CurrentGuide.room == 1 && GuideTargetsObject(g, g.CurrentGuide, "ore"), "A consumed current node switches to a fresh preferred area");
            g.Enter(1); g.enemies.Clear(); g.Talk(Object(g, "ore")); g.save.inventory[4] = 0;
            Check(g.CurrentGuide.room == 0 && GuideTargetsObject(g, g.CurrentGuide, "rest"), "Fresh nodes behind locked routes cannot replace the rest destination");
            g.save.stage = 2; Check(g.CurrentGuide.room == 3 && GuideTargetsObject(g, g.CurrentGuide, "ore"), "Opening a real route immediately makes its fresh resource useful");

            MapData original = data.maps[1]; var objects = new WorldObject[original.objects.Length + 1]; Array.Copy(original.objects, objects, original.objects.Length);
            objects[objects.Length - 1] = new WorldObject { id = "nearGuideHerb", kind = "herb", name = "测试药草", en = "Test herb", x = 7, y = 18 };
            var maps = (MapData[])data.maps.Clone(); maps[1] = new MapData { id = original.id, name = original.name, en = original.en, subtitle = original.subtitle, suben = original.suben, color = original.color, spawnX = original.spawnX, spawnY = original.spawnY, rows = original.rows, objects = objects, decor = original.decor };
            var local = new Campaign { title = data.title, titleEn = data.titleEn, version = data.version, maps = maps, items = data.items, bosses = data.bosses, lore = data.lore };
            g = Fresh(local); g.SkipTutorial(); g.Enter(1); g.enemies.Clear(); g.save.inventory[2] = 0; g.save.x = 6; g.save.y = 18; g.TrackQuest("ally1");
            Check((g.CurrentGuide.target - new Point(7, 18)).Length < .001f, "Multiple current-area nodes select the nearest available interaction");
            g.Talk(objects[objects.Length - 1]); Check((g.CurrentGuide.target - new Point(8, 18)).Length < .001f, "Consuming the nearest node immediately selects the remaining fresh node");
            g.Talk(Object(g, "herb")); g.save.inventory[2] = 0; g.Enter(9);
            Check(g.CurrentGuide.room == 9 && GuideTargetsObject(g, g.CurrentGuide, "rest"), "A player already inside the inn is guided to its real hearth instead of another trip outside");
            float before = g.save.totalTime; Action(g, "rest", "rest");
            Check(g.save.totalTime == before + 45 && g.CurrentGuide.room == 1 && GuideTargetsObject(g, g.CurrentGuide, "herb"), "The interior hearth restores the existing resource cycle and gathering destination");
        }
        public static void OreContractsGuideGatherRestAndClaim(Campaign data)
        {
            var g = Fresh(data); g.SkipTutorial(); g.AcceptBounty(1); g.save.inventory[1] = g.save.inventory[4] = 0;
            for (int eventCount = 1; eventCount <= 3; eventCount++)
            {
                GatherGuidedResource(g, "ore"); Check(g.save.bountyProgress == eventCount && g.save.inventory[1] == eventCount * 3 && g.save.inventory[4] == eventCount * 3, "The ore contract counts gathering events while retaining all crystal and part drops");
                if (eventCount < 3)
                {
                    Check(!g.ClaimBounty() && g.CurrentGuide.room == 0 && GuideTargetsObject(g, g.CurrentGuide, "rest"), "An unfinished ore contract points to rest after the only accessible vein is exhausted");
                    g.Enter(0); float before = g.save.totalTime; Action(g, "rest", "rest"); Check(g.save.totalTime == before + 45 && g.save.bountyProgress == eventCount, "Rest renews nodes without erasing accepted contract progress");
                }
            }
            QuestGuide claim = g.CurrentGuide; Check(claim.room == 0 && GuideTargetsObject(g, claim, "contract") && claim.next.Contains("领取"), "Three gathered veins switch guidance from resources to the contract reward");
            int coins = g.save.coins, shards = g.save.shards, xp = g.save.xp;
            Check(g.ClaimBounty() && g.save.coins == coins + 35 && g.save.shards == shards + 3 && g.save.xp == xp + 30 && g.save.bounty == -1 && g.save.bountyCycle == 1, "The original contract payment and completion transaction remain intact");
            Check(!g.ClaimBounty(), "A completed contract cannot pay twice");
        }
        public static void MinePartsGuidanceReturnsToElevator(Campaign data)
        {
            var g = Fresh(data); g.SkipTutorial(); g.save.stage = 2; g.TrackQuest("main");
            foreach (int room in new[] { 1, 2, 3 }) { g.Enter(room); g.enemies.Clear(); g.Talk(Object(g, "ore")); }
            g.save.inventory[4] = 0; Check(g.CurrentGuide.room == 0 && GuideTargetsObject(g, g.CurrentGuide, "rest"), "An unrepaired elevator with all reachable ore consumed requires an actual rest destination");
            g.Enter(0); Action(g, "rest", "rest");
            Check(g.CurrentGuide.room == 3 && GuideTargetsObject(g, g.CurrentGuide, "ore"), "The main quest prefers the renewed mine vein when outside a gathering region");
            GatherGuidedResource(g, "ore"); Check(g.save.inventory[4] == 3 && g.CurrentGuide.room == 3 && GuideTargetsObject(g, g.CurrentGuide, "engine"), "Exactly three ordinary parts switch the main guide back to the elevator");
            g.PrepareSave(); var resumed = new GameCore(data, CloneSave(g.save)); resumed.Start(); resumed.enemies.Clear();
            Check(resumed.save.stage == 2 && resumed.save.inventory[4] == 3 && GuideTargetsObject(resumed, resumed.CurrentGuide, "engine"), "A saved mine expedition resumes at the original repair objective");
            Action(resumed, "engine", "engine"); Check(resumed.Has("gate_3") && resumed.save.inventory[4] == 0 && resumed.CurrentGuide.room == 3 && (resumed.CurrentGuide.target - new Point(29, 8)).Length < .001f, "Repair spends the original three parts and hands guidance to the guardian altar");
        }
        public static void ScoutPresenceMatchesInteractionAndSavedJourney(Campaign data)
        {
            var g=Fresh(data);WorldObject returned=Object(g,"returnscout");g.save.x=returned.x;g.save.y=returned.y;
            Check(g.Nearest()==null||g.Nearest().kind!="returnscout","The returned scout cannot become an invisible town interaction before the rescue");
            g.Interact();Check(g.mode=="world"&&g.conversation==null&&(g.save.tutorialBits&2)==0,"Pressing E at the absent scout cannot credit the resident tutorial");
            g.Talk(returned);Check(g.mode=="world"&&g.conversation==null&&!g.Has("scout_gift"),"Direct talk cannot open an absent returned scout or award thanks");
            g.PrepareSave();var beforeRescue=new GameCore(data,CloneSave(g.save));beforeRescue.Start();
            Check(beforeRescue.Nearest()==null||beforeRescue.Nearest().kind!="returnscout","Loading an unrescued save preserves the empty town position");
            beforeRescue.Interact();Check((beforeRescue.save.tutorialBits&2)==0&&beforeRescue.mode=="world","Loading cannot turn an absent resident into tutorial progress");
            WorldObject lyra=Object(beforeRescue,"guild");beforeRescue.save.x=lyra.x;beforeRescue.save.y=lyra.y;beforeRescue.Interact();
            Check(beforeRescue.mode=="dialog"&&beforeRescue.conversation.speaker==lyra.name&&(beforeRescue.save.tutorialBits&2)!=0,"Lyra's real conversation still completes the resident interaction tutorial");

            g.Enter(1);g.enemies.Clear();g.save.kills[1]=4;WorldObject scout=Object(g,"scout");g.save.x=scout.x;g.save.y=scout.y;
            Check(g.Nearest()==scout,"The stranded scout remains selectable before the rescue");g.Interact();
            Check(g.mode=="dialog"&&g.conversation.speaker==scout.name&&g.conversation.choices.Length==1&&g.conversation.choices[0].action=="scout","Clearing four beasts retains the original rescue conversation");
            int coins=g.save.coins;g.Choose(0);
            Check(g.Has("scout_saved")&&g.GateOpen&&g.save.coins==coins+20,"Rescue still opens the starcore path and awards the original twenty coins");
            g.save.tutorialBits&=~2;
            Check(g.Nearest()==null||g.Nearest().kind!="scout","The rescued scout cannot remain an invisible forest interaction");
            g.Interact();Check(g.mode=="world"&&g.conversation==null&&(g.save.tutorialBits&2)==0,"The vacated forest position cannot produce a conversation or phantom tutorial credit");
            g.Talk(scout);Check(g.mode=="world"&&g.conversation==null&&g.save.coins==coins+20,"Talking to the retired forest scout cannot repeat the rescue reward");
            g.PrepareSave();var afterRescue=new GameCore(data,CloneSave(g.save));afterRescue.Start();
            Check(afterRescue.Has("scout_saved")&&(afterRescue.Nearest()==null||afterRescue.Nearest().kind!="scout"),"Loading the rescued forest save keeps the scout absent");
            afterRescue.Interact();Check((afterRescue.save.tutorialBits&2)==0&&afterRescue.mode=="world","Resumed forest interaction respects the saved rescue state");
            afterRescue.Enter(0);returned=Object(afterRescue,"returnscout");afterRescue.save.x=returned.x;afterRescue.save.y=returned.y;
            Check(afterRescue.Nearest()==returned,"The rescued scout is selectable at the real town homecoming position");afterRescue.Interact();
            Check(afterRescue.mode=="dialog"&&afterRescue.conversation.speaker==returned.name&&afterRescue.conversation.choices[0].action=="scoutgift","The homecoming retains the original thank-you conversation");
            int stew=afterRescue.save.inventory[6];afterRescue.Choose(0);
            Check(afterRescue.Has("scout_gift")&&afterRescue.save.inventory[6]==stew+1,"The scout's thanks awards one stew");
            Action(afterRescue,"returnscout","scoutgift");Check(afterRescue.save.inventory[6]==stew+1,"Repeated homecoming conversations cannot duplicate the thank-you gift");
            afterRescue.PrepareSave();var home=new GameCore(data,CloneSave(afterRescue.save));home.Start();
            Check(home.Nearest()!=null&&home.Nearest().kind=="returnscout","Loading the homecoming save keeps the returned scout present");
            Action(home,"returnscout","scoutgift");Check(home.save.inventory[6]==stew+1,"The consumed gift remains consumed after loading");
        }
        public static void TutorialAndPresentationMigration(Campaign data)
        {
            var old = new SaveData { presentationVersion = 0, frontierFocus = true, stage = 1 }; old.flags.Add("intro"); old.flags.Add("exp_intro");
            var resumed = new GameCore(data, old); Check(resumed.save.trackedQuest == "shore" && resumed.TutorialStep == 8, "Existing players keep story focus without forced onboarding");
            var g = Fresh(data); Check(g.TutorialStep == 0, "New games start with guided movement");
            g.mode = "pause"; for (int i = 0; i < 30; i++) g.Tick(.05f, new Point(0, -1)); Check(g.TutorialStep == 0 && g.save.tutorialDistance == 0, "Paused movement cannot complete onboarding");
            g.Close(); for (int i = 0; i < 16; i++) g.Tick(.05f, new Point(0, -1)); Check(g.TutorialStep == 1 && g.Explored(0, g.save.x, g.save.y), "Actual walking advances onboarding and exploration");
            g.save.x = 18; g.save.y = 16; g.Interact(); Check((g.save.tutorialBits & 2) != 0, "Actual interaction is recorded"); g.Close();
            g.TrackQuest("main"); PracticeRun(g); g.TutorialAction(6); g.Enter(1); g.Tick(.05f, new Point()); Check(g.TutorialStep == 8, "Accepted actions and first expedition complete onboarding");
            g.save.largeText = true; g.save.effectsQuality = 0; g.Load(g.save); Check(g.save.largeText && g.save.effectsQuality == 0 && g.TutorialStep == 8, "Preferences survive load");
            string narrative = "A long line of travelers waited at the harbor.\n\n"; for (int i = 0; i < 6; i++) narrative += "钟声响起，山路上的每个名字都应该被记住。\n\n";
            var pages = DialogueLayout.Pages(narrative, true); Check(pages.Count > 1 && string.Concat(pages) == narrative, "Paged dialogue preserves all characters");
            g.RestartTutorial(); Check(g.TutorialStep == 0, "Onboarding can be replayed"); g.SkipTutorial(); Check(g.TutorialStep == 8, "Onboarding can be skipped");
        }
        public static void QuestTrackingTransitions(Campaign data)
        {
            var g = Fresh(data); var q = g.CurrentGuide; Check(q.room == 1 && g.NavigationTarget().Value.y == 3, "Main quest points to north exit");
            g.TrackQuest("shore"); Check(g.save.trackedQuest == "main", "Locked stories cannot replace tracking"); g.TrackQuest("ally1"); Check(g.CurrentGuide.room == 1, "Insufficient supplies point to gathering");
            g.save.inventory[2] = 6; q = g.CurrentGuide; Check(q.room == 0 && q.target.y == 7, "Gathered herbs point to the herbalist"); Action(g, "garden", "ally1"); Check(g.CurrentGuide.complete, "Delivery completes tracked request");
            g.TrackQuest("main"); g.save.flags.Add("scout_saved"); Check(g.CurrentGuide.target.x == 29 && g.CurrentGuide.Progress > 0, "Rescue switches guidance to altar");
            g.save.flags.Add("boss_1"); g.save.flags.Add("core_1"); q = g.CurrentGuide; Check(q.room == 0 && q.target.y == 17, "Core points back to Lyra"); Action(g, "guild", "deliver"); Check(g.CurrentGuide.room == 2, "Delivery switches chapter");
            g.TrackQuest("shore"); Check(g.save.frontierFocus && g.CurrentGuide.target.x == 26, "Unlocked shore story points to Lune");
            Action(g, "envoy", "exp_begin"); Check(g.save.trackedQuest == "shore" && g.CurrentGuide.room == 5 && g.CurrentGuide.target.x == 15, "Acceptance automatically tracks Mira");
            g.save.flags.Add("harbor_report"); Check(g.CurrentGuide.room == 6, "Harbor report points to desert"); g.save.flags.Add("desert_report"); Check(g.CurrentGuide.room == 7, "Desert report points to highlands");
            g.save.flags.Add("mountain_report"); Check(g.CurrentGuide.room == 5 && g.CurrentGuide.target.x == 18, "Reports point to council"); g.save.frontierEnding = 1; Check(g.CurrentGuide.complete && g.CurrentGuide.Progress == 1, "Settled covenant is complete");
            g.AcceptBounty(0); Check(g.save.trackedQuest == "bounty", "Accepting a contract automatically tracks it"); g.save.trackedQuest = "invalid"; g.save.explored = null; g.Load(g.save); Check(g.save.trackedQuest == "main" && g.save.explored.Length == data.maps.Length, "Malformed fields repaired");
        }
        public static void NavigationRespectsWorldCollision(Campaign data)
        {
            var g = Fresh(data); g.save.stage = 3; g.save.frontierEnding = 1;
            foreach (string flag in new[] { "scout_saved", "gate_2", "gate_3", "exp_intro", "harbor_report", "desert_report", "gate_5", "gate_6", "gate_7" }) g.save.flags.Add(flag);
            for (int room = 0; room < 8; room++)
            {
                g.save.flags.Add("seal_" + room); g.Enter(room); g.enemies.Clear(); g.save.totalTime = 0;
                foreach (var o in g.Map.objects)
                {
                    if (o.kind == "shade" || o.kind.StartsWith("castaway")) continue;
                    var target = new Point(o.x, o.y); var route = g.NavigationPath(target); Check(route.Count > 0 || (g.Position - target).Length < 1.8f, "A route must approach " + room + "/" + o.kind);
                    foreach (var p in route) Check(!g.Blocked(p.x, p.y) && !g.Blocked(p.x - .22f, p.y - .18f) && !g.Blocked(p.x + .22f, p.y + .18f), "Guide respects hero collision footprint");
                    if (route.Count > 0) Check((route[route.Count - 1] - target).Length <= 1.8f, "Guide ends in interaction range");
                }
            }
            g.Enter(5); g.save.totalTime = 24; Check(g.NavigationPath(new Point(25, 19)).Count == 0, "High-tide shoals cannot be crossed"); g.save.totalTime = 0; Check(g.NavigationPath(new Point(25, 19)).Count > 0, "Relay reachable at low tide");
            g.Enter(1); g.save.flags.Remove("scout_saved"); foreach (var p in g.NavigationPath(new Point(29, 8))) Check(!g.Blocked(p.x, p.y), "No guide crosses blocked gates");
        }
    }
}
