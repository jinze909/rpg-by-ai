using System;
using UnityEngine;

namespace Starfall
{
    public sealed partial class FrontierRuntime
    {
        private bool DrawReachObject(WorldObject obj)
        {
            if(!obj.kind.StartsWith("reach",StringComparison.Ordinal)) return false;
            Point p=ScreenPoint(new Point(obj.x,obj.y));
            bool post=obj.kind.StartsWith("reachPost",StringComparison.Ordinal),opened=false;
            if(post){int index=int.Parse(obj.kind.Substring(9));opened=Game.ReachPostRecorded(index);}
            else opened=obj.kind=="reachBoard"?Game.ReachComplete:obj.kind=="reachBridge"?Game.ReachBridgeOpen:obj.kind=="reachRoot"?Game.ReachRootsOpen:obj.kind=="reachCache"&&Game.ReachCacheRecovered;
            string prop=obj.kind=="reachExit"?"boat":obj.kind=="reachRoot"?"root":obj.kind=="reachCache"?"cache":"post";
            Color tint=(obj.kind=="reachRoot"||obj.kind=="reachCache")&&opened?new Color(1,1,1,.45f):Color.white;
            PropImage(prop,0,p.x-32,p.y-64,64,74,tint);
            Color color=opened?mint:gold;
            Ring(new Point(obj.x,obj.y),.38f,color,16);
            string mark=post?(int.Parse(obj.kind.Substring(9))+1).ToString():obj.kind=="reachBoard"?"!":obj.kind=="reachBridge"?"+":obj.kind=="reachRoot"?"R":obj.kind=="reachExit"?"<":"?";
            Text(p.x-18,p.y-59,36,22,opened?"+":mark,15,color,TextAnchor.MiddleCenter);
            return true;
        }
        private void DrawReachWorld()
        {
            if(Game.save.room!=GameCore.ReachRoom)return;
            if(Game.ReachBridgeOpen)
                for(int y=28;y<=30;y++)for(int x=30;x<=33;x++)
                {Point p=ScreenPoint(new Point(x,y));Atlas(terrainSheet,8,2,11,new Rect(p.x,p.y,Tile,Tile),Color.white);}
            if(!Game.ReachRootsOpen)
                for(int y=11;y<=13;y++)for(int x=45;x<=47;x++)
                {
                    Point p=ScreenPoint(new Point(x,y));
                    Box(p.x,p.y,Tile,Tile,new Color(.14f,.27f,.19f,.94f));
                    Atlas(fieldProps,4,4,11,new Rect(p.x-2,p.y-9,Tile+4,Tile+12),Color.white);
                }
        }
    }
}
