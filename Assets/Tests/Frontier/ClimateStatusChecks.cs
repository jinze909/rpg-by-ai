using System;
using System.Reflection;

namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        // Resolve the new read through reflection so an archived production
        // baseline compiles before failing the missing feedback assertion.
        private static object ClimateRead(GameCore game)
        {
            MethodInfo method = typeof(GameCore).GetMethod("GetClimateReading", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
            Check(method != null, "Existing climate simulation exposes its read-only current reading");
            return method.Invoke(game, null);
        }
        private static T ClimateValue<T>(object reading, string name)
        {
            Check(reading != null, "A climate reading exists before reading " + name);
            FieldInfo field = reading.GetType().GetField(name, BindingFlags.Public | BindingFlags.Instance);
            Check(field != null && field.IsInitOnly && field.FieldType == typeof(T), "Climate " + name + " is a public immutable " + typeof(T).Name);
            return (T)field.GetValue(reading);
        }
        private static void ClimateExpected(GameCore game, bool cold, float amount, float rate, string protection, bool slowed, bool harmful)
        {
            object reading = ClimateRead(game);
            Check(ClimateValue<bool>(reading,"cold") == cold && ClimateValue<string>(reading,"protection") == protection
                && ClimateValue<bool>(reading,"slowed") == slowed && ClimateValue<bool>(reading,"harmful") == harmful,
                "Current climate, source and authored threshold flags agree: room=" + game.save.room + ", amount=" + amount + ", source=" + protection);
            PlanStatsNear(ClimateValue<float>(reading,"amount"), amount, "Climate amount retains the actual fractional value");
            PlanStatsNear(ClimateValue<float>(reading,"rate"), rate, "Climate trend retains the original accumulation or recovery rate");
        }
        private static GameCore ClimateGame(Campaign data, int room)
        {
            GameCore game = Unlocked(data); game.Enter(room); game.Close(); game.SkipTutorial();
            game.enemies.Clear(); game.projectiles.Clear(); game.hazards.Clear(); game.effects.Clear();
            game.save.companion = -1; game.save.totalTime = 100; game.save.teaUntil = 0;
            game.save.heat = game.save.cold = 0; game.save.reducedMotion = true; game.invulnerable = 1000;
            game.save.flags.RemoveAll(flag => flag.StartsWith("warm_", StringComparison.Ordinal));
            // Use actual open ground away from every climate source. This also
            // gives the real movement check an unobstructed eastward step.
            bool found = false;
            for (int y = 3; y < game.Map.rows.Length-3 && !found; y++)
                for (int x = 3; x < game.Map.rows[y].Length-3 && !found; x++)
                {
                    Point point = new Point(x+.5f,y+.5f); bool near = false;
                    foreach (WorldObject obj in game.Map.objects)
                        if ((obj.kind == "shade" || obj.kind == "brazier") && (new Point(obj.x,obj.y)-point).Length < 4) near = true;
                    if (near || !game.CanStand(point) || !game.CanStand(point+new Point(1,0))) continue;
                    game.save.x = point.x; game.save.y = point.y; found = true;
                }
            Check(found, "The production region contains an open unprotected movement fixture");
            return game;
        }
        private static void ClimatePlace(GameCore game, WorldObject obj, float distance)
        { game.save.x = obj.x+distance; game.save.y = obj.y; }
        private static void ClimatePulse(GameCore game, float timer)
        {
            FieldInfo field = typeof(GameCore).GetField("climateTimer", BindingFlags.NonPublic | BindingFlags.Instance);
            Check(field != null, "The existing climate pulse clock remains the owner of harm"); field.SetValue(game,timer);
        }

        public static void ClimateStatusReportsExactThresholdsAndRegionalOwnership(Campaign data)
        {
            foreach (int room in new[] {6,7})
            {
                GameCore game = ClimateGame(data,room); bool cold = room == 7;
                foreach (float amount in new[] {0f,69.99f,70f,70.001f,84.999f,85f,89.999f,90f,99.75f,100f})
                {
                    game.save.heat = cold ? 0 : amount; game.save.cold = cold ? amount : 0;
                    ClimateExpected(game,cold,amount,cold ? 1.1f : 1.3f,"none",cold && amount > 70,amount >= (cold ? 90 : 85));
                }
                // Lingering opposite-region values retain their real effects,
                // but do not impersonate the region named by the card.
                game.save.heat = cold ? 100 : 20; game.save.cold = cold ? 20 : 100;
                ClimateExpected(game,cold,20,cold ? 1.1f : 1.3f,"none",false,false);
                foreach (int other in new[] {0,1,2,3,4,5,8,9,GameCore.ArchiveRoom,GameCore.ReachRoom})
                { game.save.room = other; Check(ClimateRead(game) == null, "No regional climate card is invented outside the desert and mountain: " + other); }
            }
        }

        public static void ClimateStatusProtectionSourcesMatchStrictAuthoredRanges(Campaign data)
        {
            GameCore heat = ClimateGame(data,6); WorldObject shade = Object(heat,"shade"); heat.save.heat = 53.125f;
            ClimatePlace(heat,shade,2.999f); ClimateExpected(heat,false,53.125f,-10,"shade",false,false);
            ClimatePlace(heat,shade,3); ClimateExpected(heat,false,53.125f,1.3f,"none",false,false);
            heat.racing = true; ClimateExpected(heat,false,53.125f,-10,"race",false,false);
            heat.save.teaUntil = heat.save.totalTime+.001f; ClimateExpected(heat,false,53.125f,-10,"tea",false,false);
            heat.save.teaUntil = heat.save.totalTime; ClimateExpected(heat,false,53.125f,-10,"race",false,false);
            heat.racing = false; ClimateExpected(heat,false,53.125f,1.3f,"none",false,false);

            GameCore cold = ClimateGame(data,7); WorldObject fire = Object(cold,"brazier"); cold.save.cold = 78.25f;
            ClimatePlace(cold,fire,0); ClimateExpected(cold,true,78.25f,1.1f,"none",true,false);
            cold.save.flags.Add("warm_"+fire.id); ClimatePlace(cold,fire,3.499f); ClimateExpected(cold,true,78.25f,-8,"brazier",true,false);
            ClimatePlace(cold,fire,3.5f); ClimateExpected(cold,true,78.25f,1.1f,"none",true,false);
            cold.save.teaUntil = cold.save.totalTime+.001f; ClimateExpected(cold,true,78.25f,-8,"tea",true,false);
            cold.save.teaUntil = cold.save.totalTime; ClimateExpected(cold,true,78.25f,1.1f,"none",true,false);
            foreach (string flag in new[] {"personal_2","shelter_accepted","shelter_leg_0","shelter_leg_1","shelter_leg_2","sheltered_reported"})
                if (!cold.Has(flag)) cold.save.flags.Add(flag);
            cold.save.x = 12; cold.save.y = 13;
            Check(cold.ShelterColdProtected && !cold.ShelterAtLitBrazier, "The completed lower route contains a real sheltered point away from lit fire");
            ClimateExpected(cold,true,78.25f,0,"shelter",true,false);
            cold.save.teaUntil = cold.save.totalTime+1; ClimateExpected(cold,true,78.25f,-8,"tea",true,false);
        }

        public static void ClimateStatusMatchesActualRatesDamageAndColdPenalties(Campaign data)
        {
            foreach (int room in new[] {6,7}) foreach (bool protectedByTea in new[] {false,true})
            {
                GameCore game = ClimateGame(data,room); bool cold = room == 7;
                game.save.heat = cold ? 0 : 40; game.save.cold = cold ? 40 : 0;
                if (protectedByTea) game.save.teaUntil = game.save.totalTime+2;
                float rate = ClimateValue<float>(ClimateRead(game),"rate"), energy = game.save.energy;
                game.Tick(.05f,new Point());
                PlanStatsNear(cold ? game.save.cold : game.save.heat,40+rate*.05f,"An actual world Tick applies precisely the reported mature climate rate");
                ClimateExpected(game,cold,40+rate*.05f,rate,protectedByTea ? "tea" : "none",false,false);
                Check(game.save.energy >= energy, "Reading climate does not spend attack or item energy");
            }
            foreach (string source in new[] {"shade","race","brazier","shelter","expired-tea"})
            {
                bool cold = source == "brazier" || source == "shelter"; GameCore game = ClimateGame(data,cold ? 7 : 6);
                game.save.heat = cold ? 0 : 40; game.save.cold = cold ? 40 : 0;
                float expectedRate; string protection = source;
                if (source == "shade") { ClimatePlace(game,Object(game,"shade"),0); expectedRate = -10; }
                else if (source == "race") { game.racing = true; expectedRate = -10; }
                else if (source == "brazier") { WorldObject fire = Object(game,"brazier"); game.save.flags.Add("warm_"+fire.id); ClimatePlace(game,fire,0); expectedRate = -8; }
                else if (source == "shelter")
                {
                    foreach (string flag in new[] {"personal_2","shelter_accepted","shelter_leg_0","shelter_leg_1","shelter_leg_2","sheltered_reported"})
                        if (!game.Has(flag)) game.save.flags.Add(flag);
                    game.save.x = 12; game.save.y = 13; expectedRate = 0;
                }
                else
                {
                    game.save.teaUntil = game.save.totalTime+.025f;
                    ClimateExpected(game,false,40,-10,"tea",false,false);
                    protection = "none"; expectedRate = 1.3f;
                }
                game.Tick(.05f,new Point());
                PlanStatsNear(cold ? game.save.cold : game.save.heat,40+expectedRate*.05f,
                    "Actual " + source + " uses its original rate, including tea expiry before the simulation step");
                ClimateExpected(game,cold,40+expectedRate*.05f,expectedRate,protection,false,false);
            }
            foreach (int room in new[] {6,7})
            {
                GameCore game = ClimateGame(data,room); game.save.difficulty = 1; game.save.talents[1] = 0; game.save.relics[0] = game.save.relics[1] = -1;
                game.save.mealKind = 0; game.save.mealUntil = 0; game.invulnerable = 0;
                game.save.heat = room == 6 ? 95 : 0; game.save.cold = room == 7 ? 95 : 0;
                float health = game.save.health; ClimatePulse(game,.01f); game.Tick(.05f,new Point());
                PlanStatsNear(health-game.save.health,4,"The original harmful climate pulse still loses four unmitigated health");
                float after = game.save.health; Advance(game,.9f); PlanStatsNear(game.save.health,after,"No extra damage is manufactured before the authored next pulse");
                Advance(game,.15f); PlanStatsNear(after-game.save.health,4,"The original one-second climate pulse continues once invulnerability expires");
                game.save.tea = 1; game.DrinkTea();
                Check(game.save.heat == 0 && game.save.cold == 0 && game.save.tea == 0,"Actual tea still clears both climates using exactly one item");
                ClimateExpected(game,room == 7,0,room == 7 ? -8 : -10,"tea",false,false);
            }
            GameCore clear = ClimateGame(data,7), chilled = ClimateGame(data,7); clear.save.cold = 70; chilled.save.cold = 70.01f;
            clear.save.energy = chilled.save.energy = 20; float speed = clear.Speed; Point p = clear.Position;
            PlanStatsNear(speed-chilled.Speed,.55f,"Cold above seventy retains the original .55 movement penalty");
            clear.Tick(.05f,new Point(1,0)); chilled.Tick(.05f,new Point(1,0));
            PlanStatsNear(clear.save.energy-chilled.save.energy,.2f,"The real cold penalty still removes four energy recovery per second");
            PlanStatsNear((clear.Position-p).Length-(chilled.Position-p).Length,.55f*.05f,"Actual unobstructed walking uses the unchanged cold speed penalty");
        }

        public static void ClimateStatusReadsPreserveActionsPauseAndLegacySaves(Campaign data)
        {
            GameCore game = ClimateGame(data,6); game.save.heat = 86.125f; game.save.energy = 100;
            game.Attack(new Point(1,0)); ActorAction action = game.playerAction; Check(action != null && !action.released,"A paid action exists before climate reads");
            object old = ClimateRead(game); SaveData owner = game.save; int[] inventory = game.save.inventory;
            foreach (string mode in new[] {"world","inventory","journal","dialog","pause","camp"})
            {
                game.mode = mode; string before = PlanStatsState(game);
                for (int repeat = 0; repeat < 8; repeat++) { ClimateRead(game); game.Conditions(); }
                Check(PlanStatsState(game) == before && ReferenceEquals(game.save,owner) && ReferenceEquals(game.save.inventory,inventory)
                    && ReferenceEquals(game.playerAction,action),"Climate reads preserve save, simulation, paid action and original owners in "+mode);
            }
            game.mode = "inventory"; float time = game.save.totalTime, elapsed = action.elapsed; game.Tick(.05f,new Point(1,0));
            Check(game.save.totalTime == time && game.save.heat == 86.125f && action.elapsed == elapsed,"Visible paused climate does not advance weather or a prepared action");
            game.Close(); game.hitStop = .1f; game.Tick(.05f,new Point()); Check(game.save.totalTime == time && game.save.heat == 86.125f,"Existing hit-stop still freezes climate");
            game.save.heat = 23; game.save.teaUntil = time+12;
            ClimateExpected(game,false,23,-10,"tea",false,false);
            Check(ClimateValue<float>(old,"amount") == 86.125f && ClimateValue<string>(old,"protection") == "none","An old reading stays an immutable snapshot after the saved climate changes");
            foreach (string kind in new[] {"attack","skill","dodge","companion"})
            {
                GameCore queued = ClimateGame(data,7); string field;
                if (kind == "attack") { queued.attackCooldown = .08f; queued.PressAttack(new Point(0,1)); field = "bufferedAttack"; }
                else if (kind == "skill") { queued.skillCooldown = .08f; queued.Skill(new Point(0,1)); field = "bufferedSkill"; }
                else if (kind == "dodge") { queued.dashCooldown = .08f; queued.Dash(new Point(0,1)); field = "bufferedDodge"; }
                else { queued.SelectCompanion(1); queued.companionCooldown = .08f; queued.CommandCompanion(); field = "bufferedCompanionCommand"; }
                object intent = PlanStatsIntent(queued,field); Check(intent != null,"The real queued input owns "+kind);
                string before = PlanStatsState(queued); ClimateRead(queued); queued.Conditions();
                Check(PlanStatsState(queued) == before && ReferenceEquals(PlanStatsIntent(queued,field),intent),"Climate feedback preserves queued "+kind+" ownership and lifetime");
            }
            foreach (int version in new[] {1,2}) foreach (int room in new[] {6,7})
            {
                GameCore source = ClimateGame(data,room); source.save.version = version; source.save.heat = 41.5f; source.save.cold = 74.25f;
                source.save.teaUntil = 150; source.save.coins = 91; source.save.flags.Add("legacy_climate_read_keep");
                GameCore loaded = new GameCore(data,CloneSave(source.save)); string before = PlanStatsState(loaded); ClimateRead(loaded); loaded.Conditions();
                Check(PlanStatsState(loaded) == before && loaded.save.heat == 41.5f && loaded.save.cold == 74.25f && loaded.save.teaUntil == 150
                    && loaded.save.coins == 91 && loaded.Has("legacy_climate_read_keep"),"Legacy v"+version+" keeps weather, tea deadline and mature progress without a new persisted status");
                loaded.PrepareSave(); GameCore resumed = new GameCore(data,CloneSave(loaded.save));
                ClimateExpected(resumed,room == 7,room == 7 ? 74.25f : 41.5f,room == 7 ? -8 : -10,"tea",room == 7,false);
                Check(resumed.save.coins == 91 && resumed.Has("legacy_climate_read_keep"),"Prepare/save/reload retains the old progression owner");
            }
        }
    }
}
