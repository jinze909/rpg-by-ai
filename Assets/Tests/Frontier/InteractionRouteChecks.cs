using System;
using System.Collections.Generic;
using System.Reflection;

namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        // Reflection lets this same semantic harness compile against the trusted
        // pre-fix Core for an isolated RED check; no missing API compilation trick.
        private static List<Point> InteractionRoute(GameCore g, WorldObject target)
        {
            MethodInfo method = typeof(GameCore).GetMethod("InteractionPath", new[] { typeof(WorldObject) });
            return method == null ? target == null ? new List<Point>() : g.NavigationPath(new Point(target.x,target.y))
                : (List<Point>)method.Invoke(g,new object[]{target});
        }
        private static List<Point> InteractionGuidance(GameCore g, Point target)
        {
            MethodInfo method = typeof(GameCore).GetMethod("GuidancePath", new[] { typeof(Point) });
            return method == null ? g.NavigationPath(target) : (List<Point>)method.Invoke(g,new object[]{target});
        }
        private static bool InteractionPointsEqual(List<Point> first, List<Point> second)
        {
            if(first.Count!=second.Count)return false;
            for(int i=0;i<first.Count;i++)if((first[i]-second[i]).Length>.0001f)return false;
            return true;
        }
        private static void InteractionRouteOwnsEndpoint(GameCore g, WorldObject target, List<Point> route)
        {
            foreach(Point p in route)Check(g.CanStand(p),"Every drawn interaction route cell fits the actual player footprint: "+target.id);
            Point original=g.Position;
            try
            {
                if(route.Count>0){Point end=route[route.Count-1];g.save.x=end.x;g.save.y=end.y;}
                Check(ReferenceEquals(g.Nearest(),target),"The real E interaction at the route endpoint must own "+target.id+", not "+(g.Nearest()==null?"nothing":g.Nearest().id));
                Check((g.Position-new Point(target.x,target.y)).Length<1.75f,"Endpoint respects the original strict E distance");
            }
            finally{g.save.x=original.x;g.save.y=original.y;}
        }

        public static void InteractionRouteGlassSealEndsAtItsOwnRealEApproach(Campaign data)
        {
            var g=Unlocked(data);g.SkipTutorial();g.Enter(6);g.enemies.Clear();
            WorldObject target=Object(g,"seal1");g.save.flags.Remove("seal_6");g.save.flags.Add("core_1");g.save.x=18;g.save.y=14;
            List<Point> route=InteractionRoute(g,target);
            Check(route.Count>0,"The authored glass-desert seal has a reachable E approach from the arrival road");
            InteractionRouteOwnsEndpoint(g,target,route);
            Point end=route[route.Count-1];g.save.x=end.x;g.save.y=end.y;
            g.Close();g.Interact();
            Check(g.Has("seal_6"),"Manual E at the drawn endpoint uses the actual grove seal rather than its nearby shade or herb");
            WorldObject cache=Object(g,"cache1");g.save.x=5;g.save.y=14;
            List<Point> cacheRoute=InteractionRoute(g,cache);Check(cacheRoute.Count>0,"The opened forgotten cache now has its own E route");InteractionRouteOwnsEndpoint(g,cache,cacheRoute);
            end=cacheRoute[cacheRoute.Count-1];g.save.x=end.x;g.save.y=end.y;g.Close();g.Interact();
            Check(g.Has("cache_6")&&g.mode=="dialog","Manual E at the new cache endpoint opens the cache rather than the earlier equal-distance seal");
        }

        public static void InteractionRouteAuthoredObjectsNeverAdvertiseAnotherObjectsE(Campaign data)
        {
            int reached=0;
            foreach(int room in new[]{0,1,2,3,4,5,6,7,9,10,11})
            {
                var g=Unlocked(data);g.SkipTutorial();g.save.flags.AddRange(new[]{"core_1","core_2","core_3","scout_saved","gate_2","gate_3","seal_0","seal_1","seal_2","seal_3","seal_4","seal_9","seal_10"});
                g.Enter(room);g.enemies.Clear();g.save.totalTime=0;g.save.tideHeldUntil=0;
                foreach(WorldObject target in g.Map.objects)
                {
                    if(!g.IsObjectPresent(target))continue;
                    List<Point> route=InteractionRoute(g,target);
                    if(route.Count==0&&!ReferenceEquals(g.Nearest(),target))continue;
                    InteractionRouteOwnsEndpoint(g,target,route);reached++;
                }
            }
            Check(reached>=120,"The check exercises real interaction endpoints across the mature maps, not one synthetic layout");
        }

        public static void InteractionRouteRejectsStaleReferencesAndIncludesNeededStartCenterReadOnly(Campaign data)
        {
            var g=LandmarkGame(data);WorldObject cook=Object(g,"cook");
            var copied=new WorldObject{id=cook.id,kind=cook.kind,name=cook.name,en=cook.en,x=cook.x,y=cook.y};
            Check(InteractionRoute(g,null).Count==0&&InteractionRoute(g,copied).Count==0&&InteractionRoute(g,data.maps[6].objects[0]).Count==0,
                "Null, copied and foreign objects cannot authorize a current-map interaction destination");
            WorldObject hidden=Object(g,"returnscout");Check(!g.IsObjectPresent(hidden)&&InteractionRoute(g,hidden).Count==0,"A hidden returned scout has no advertised route");
            WorldObject[] original=g.Map.objects;
            try
            {
                var target=new WorldObject{id="route_target",kind="cook",name="Target",x=11.9f,y=14.5f};
                var neighbor=new WorldObject{id="route_neighbor",kind="cook",name="Neighbor",x=9.0f,y=14.05f};
                g.Map.objects=new[]{target,neighbor};g.save.x=10.05f;g.save.y=14.05f;
                Check(g.CanStand(g.Position)&&g.CanStand(new Point(10.5f,14.5f))&&ReferenceEquals(g.Nearest(),neighbor),"Off-center regression begins standable and near the wrong object");
                string saved=LandmarkDescribe(g.save),enemies=LandmarkDescribe(g.enemies),effects=LandmarkDescribe(g.effects);int revision=g.revision,sounds=g.sounds.Count;string mode=g.mode;
                List<Point> route=InteractionRoute(g,target);
                Check(route.Count==1&&(route[0]-new Point(10.5f,14.5f)).Length<.0001f,"A required movement inside the starting cell is retained, not mistaken for arrival");
                InteractionRouteOwnsEndpoint(g,target,route);
                for(int i=0;i<10;i++){InteractionRoute(g,target);InteractionGuidance(g,new Point(target.x,target.y));}
                Check(saved==LandmarkDescribe(g.save)&&enemies==LandmarkDescribe(g.enemies)&&effects==LandmarkDescribe(g.effects)&&g.revision==revision&&g.sounds.Count==sounds&&g.mode==mode,
                    "Routing cannot move, interact, change saves, grant rewards or advance combat");
                g.save.x=10.5f;g.save.y=14.5f;
                Check(ReferenceEquals(g.Nearest(),target)&&InteractionRoute(g,target).Count==0,"An actual E-owned arrival needs no artificial extra path");
                target.x=12.25f;
                Check(!ReferenceEquals(g.Nearest(),target),"A target at exactly 1.75 is outside the original strict E radius");
                List<Point> boundary=InteractionRoute(g,target);Check(boundary.Count>0,"An excluded strict-boundary target routes to an actual closer cell");InteractionRouteOwnsEndpoint(g,target,boundary);
            }
            finally{g.Map.objects=original;}
        }

        public static void InteractionRouteUsesLiveRootsBridgeAndTideWithoutOpeningThem(Campaign data)
        {
            var g=ReachGame(data);g.enemies.Clear();WorldObject cache=Object(g,"reachCache");
            Check(!g.ReachRootsOpen&&InteractionRoute(g,cache).Count==0,"Root-locked cache honestly has no interaction route");
            ReachStand(g,"reachRoot");Check(g.ClearReachRoots(),"The original earned ability remains the only root opener");
            g.save.x=g.Map.spawnX;g.save.y=g.Map.spawnY;List<Point> opened=InteractionRoute(g,cache);
            Check(opened.Count>0,"The same cache becomes reachable after actual roots are cleared");InteractionRouteOwnsEndpoint(g,cache,opened);
            WorldObject island=Object(g,"reachPost1");g.save.x=27.5f;g.save.y=32.5f;List<Point> before=InteractionRoute(g,island);
            ReachStand(g,"reachBridge");Check(g.OpenReachBridge(),"The authored nearby winch still owns bridge repair");
            g.save.x=27.5f;g.save.y=32.5f;List<Point> after=InteractionRoute(g,island);
            Check(before.Count>after.Count&&after.Count>0,"Live repaired bridge shortens the actual interaction route");InteractionRouteOwnsEndpoint(g,island,after);
            var port=Unlocked(data);port.SkipTutorial();port.Enter(5);port.enemies.Clear();port.save.x=5.5f;port.save.y=8.5f;port.save.totalTime=0;port.save.tideHeldUntil=0;
            WorldObject relay=Object(port,"relay2");List<Point> low=InteractionRoute(port,relay);Check(low.Count>0,"Third relay is reachable at authored low tide");InteractionRouteOwnsEndpoint(port,relay,low);
            port.save.totalTime=24;List<Point> high=InteractionRoute(port,relay);
            Check(high.Count==0,"High tide does not retain the former low-tide interaction route");
            Check(port.save.totalTime==24&&port.save.tideHeldUntil==0,"Read-only tide planning cannot hold the tide or advance time");
        }

        public static void InteractionRouteGuidanceKeepsIdentityAndCoordinateNavigationOwnership(Campaign data)
        {
            var g=LandmarkGame(data);WorldObject[] original=g.Map.objects;
            try
            {
                var first=new WorldObject{id="route_first",kind="cook",name="First",x=14,y=14};
                var second=new WorldObject{id="route_second",kind="cook",name="Second",x=14,y=14};
                g.Map.objects=new[]{first,second};g.save.x=10.5f;g.save.y=14.5f;
                Check(g.PinLandmark(second),"An explicit eligible same-coordinate destination keeps its own identity");
                Check(InteractionGuidance(g,new Point(14,14)).Count==0,"A tied but nonselectable explicit pin must not route to its earlier neighbor instead");
                g.ClearLandmarkPin();List<Point> canonical=InteractionGuidance(g,new Point(14,14));Check(canonical.Count>0,"An ordinary shared coordinate resolves to the first actual selectable object");InteractionRouteOwnsEndpoint(g,first,canonical);
                var hidden=new WorldObject{id="route_hidden",kind="returnscout",name="Hidden",x=18,y=14};g.Map.objects=new[]{hidden};
                Check(InteractionGuidance(g,new Point(18,14)).Count==0,"A coordinate occupied only by a hidden story object has no interaction guide");
                Point waypoint=new Point(20,14);Check(InteractionPointsEqual(g.NavigationPath(waypoint),InteractionGuidance(g,waypoint)),"Non-object waypoint coordinates retain generic navigation semantics");
                g.practicing=true;g.enemies.Clear();g.enemies.Add(new Enemy(90001,0,18,14,50));
                Check(InteractionPointsEqual(g.NavigationPath(g.enemies[0].Position),InteractionGuidance(g,g.enemies[0].Position)),"Practice enemies retain coordinate aim even at a hidden story object's coordinate");g.practicing=false;
                g.enemies.Clear();g.enemies.Add(new Enemy(90002,0,18,14,50));
                Check(InteractionPointsEqual(g.NavigationPath(g.enemies[0].Position),InteractionGuidance(g,g.enemies[0].Position)),"A real enemy destination is not reclassified as a hidden interaction object");
            }
            finally{g.Map.objects=original;}
            var abyss=Unlocked(data);abyss.SkipTutorial();abyss.StartAbyss(0);Check(abyss.InAbyss,"The original seeded Abyss starts normally");
            Point destination=abyss.NavigationTarget().Value;
            Check(InteractionPointsEqual(abyss.NavigationPath(destination),InteractionGuidance(abyss,destination)),"Abyss objective coordinates keep the existing generic route and objective owner");
        }
    }
}
