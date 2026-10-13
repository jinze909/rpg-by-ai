using System;
using UnityEngine;

namespace Starfall
{
    public sealed partial class FrontierRuntime
    {
        private static readonly Point[] archivePorts = { new Point(0,-1), new Point(1,0), new Point(0,1), new Point(-1,0) };
        private static readonly string[] archivePortNames = { "N + E", "E + S", "S + W", "W + N" };
        private static readonly string[] archiveValveNames = { "I", "II", "III" };
        private static readonly Point archivePipeLift = new Point(0,-.6f);

        private void DrawArchivePressureWarning(Hazard hazard)
        {
            Point p = ScreenPoint(hazard.position); float radius = hazard.radius * Tile;
            Color warning = HazardWarningColor(hazard);
            // The hit boundary stays visible on patterned stone even with
            // minimal effects. Both outlines share the actual damage radius.
            for (int i = 0; i < 32; i++)
            {
                float angle = i * (float)Math.PI * 2 / 32;
                float x = p.x + (float)Math.Cos(angle) * radius, y = p.y + (float)Math.Sin(angle) * radius;
                Box(x-4,y-4,8,8,new Color(.025f,.045f,.06f,.95f));
                Box(x-2,y-2,4,4,warning);
            }
            float caption = p.y-radius-28;
            Box(p.x-27,caption,54,24,new Color(.025f,.045f,.06f,.95f));
            Text(p.x-25,caption,50,24,hazard.timer.ToString("0.0"),13,warning,TextAnchor.MiddleCenter);
        }

        private void DrawArchivePipe(Point from, Point to, float width, Color tint)
        {
            Point a = ScreenPoint(from + archivePipeLift), b = ScreenPoint(to + archivePipeLift);
            int steps = Math.Max(1,(int)((b-a).Length / 3));
            for (int i = 0; i <= steps; i++)
            { Point p = a + (b-a)*(i/(float)steps); Box(p.x-width/2,p.y-width/2,width,width,tint); }
        }
        private void ArchivePipeSegment(Point from, Point to, bool flowing)
        {
            DrawArchivePipe(from,to,9,new Color(.055f,.095f,.12f,.9f));
            DrawArchivePipe(from,to,5,flowing ? new Color(.42f,.78f,.81f,.95f) : new Color(.5f,.58f,.59f,.85f));
            // Static center marks show the connected route at minimal effects
            // and reduced motion too; water flow never depends on a particle.
            if (flowing) DrawArchivePipe(from,to,1,new Color(.83f,.94f,.92f,.9f));
        }
        private void DrawArchiveWorld()
        {
            if (Game.save.room != GameCore.ArchiveRoom) return;
            int flow = Game.ArchiveDrained ? 4 : Game.ArchiveFlowSegments;
            Point a = GameCore.ArchiveValvePoints[0], b = GameCore.ArchiveValvePoints[1], c = GameCore.ArchiveValvePoints[2];
            ArchivePipeSegment(new Point(4,6),a,flow > 0);
            ArchivePipeSegment(a,b,flow > 1);
            ArchivePipeSegment(b,c,flow > 2);
            ArchivePipeSegment(c,new Point(10,7),flow > 3);
            Point inlet = ScreenPoint(new Point(4,6) + archivePipeLift), outlet = ScreenPoint(new Point(10,7) + archivePipeLift);
            Text(inlet.x-40,inlet.y-34,80,24,Game.L("进水", "INLET"),12,mint,TextAnchor.MiddleCenter);
            Text(outlet.x-50,outlet.y-35,100,24,Game.ArchiveDrained ? Game.L("已排水", "DRAINED") : Game.L("出水", "OUTLET"),12,Game.ArchiveDrained ? mint : gold,TextAnchor.MiddleCenter);
        }
        private bool DrawArchiveObject(WorldObject obj)
        {
            string kind = obj.kind; Point p = ScreenPoint(new Point(obj.x,obj.y));
            if (kind.StartsWith("archiveValve"))
            {
                int id;
                if (!int.TryParse(kind.Substring(12),out id) || id < 0 || id > 2) return false;
                int rotation = Game.ArchiveValve(id);
                Point center = new Point(obj.x,obj.y) + archivePipeLift;
                Color tint = Game.ArchiveDrained ? mint : gold;
                PropImage("engine",0,p.x-25,p.y-43,50,52,new Color(.72f,.85f,.87f));
                for (int arm = 0; arm < 2; arm++)
                {
                    Point start = new Point(obj.x,obj.y), end = start + archivePorts[(rotation+arm)%4]*.65f;
                    DrawArchivePipe(start,end,7,new Color(.025f,.075f,.1f,.95f));
                    DrawArchivePipe(start,end,3,tint);
                }
                Point screen = ScreenPoint(center); Diamond(screen.x,screen.y,5,tint);
                Text(p.x-65,p.y-82,130,24,archiveValveNames[id]+" · "+archivePortNames[rotation],13,tint,TextAnchor.MiddleCenter);
                return true;
            }
            if (kind == "archivePlan")
            {
                PropImage("post",0,p.x-27,p.y-61,54,64);
                Text(p.x-19,p.y-56,38,26,"?",19,gold,TextAnchor.MiddleCenter);
                return true;
            }
            if (kind.StartsWith("archiveRecord"))
            {
                int id;
                if (!int.TryParse(kind.Substring(13),out id) || id < 0 || id > 2) return false;
                bool found = Game.Has("archive_record_"+id);
                PropImage("cache",0,p.x-29,p.y-42,58,51,found ? muted : Color.white);
                Text(p.x-22,p.y-66,44,24,archiveValveNames[id]+(found ? " ✓" : " ?"),13,found ? mint : gold,TextAnchor.MiddleCenter);
                return true;
            }
            if (kind == "archiveVent" || kind == "archiveKeeper")
            {
                bool done = kind == "archiveVent" ? Game.ArchiveVented : Game.ArchiveComplete;
                PropImage("engine",0,p.x-34,p.y-61,68,72,done ? new Color(.75f,.9f,.87f) : Color.white);
                Ring(new Point(obj.x,obj.y),.6f,done ? mint : gold,24);
                Text(p.x-75,p.y-87,150,25,Game.L(obj.name,obj.en),12,done ? mint : gold,TextAnchor.MiddleCenter);
                return true;
            }
            return false;
        }
    }
}
