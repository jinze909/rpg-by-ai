using System;
using UnityEngine;

namespace Starfall
{
    public sealed partial class FrontierRuntime
    {
        // Presentation only: never add these open facilities to Map objects or
        // decoration, whose authored contents also govern interaction and ground.
        private struct ProjectScenery
        {
            public int project, tier;
            public Point position;
            public bool camp;
        }

        private void AddProjectSceneryEntries()
        {
            if (Game.save.projects == null || Game.save.projects.Length != 3) return;
            if (Game.save.room == 0)
            {
                if (Game.save.projects[0] > 0)
                    foreach (Decoration garden in Game.Map.decor)
                        if (garden.kind == "garden")
                        { AddProjectScenery(0, new Point(garden.x - 1.75f, garden.y + .45f), false); break; }
                foreach (WorldObject host in Game.Map.objects)
                {
                    if (!Game.IsObjectPresent(host)) continue;
                    if (host.kind == "cook") AddProjectScenery(1, new Point(host.x - 3.6f, host.y + .45f), false);
                    else if (host.kind == "forge") AddProjectScenery(2, new Point(host.x + 1.6f, host.y + .65f), false);
                }
            }
            else if (Game.save.room == 5 || Game.save.room == 6 || Game.save.room == 7 || Game.save.room == GameCore.ReachRoom)
            {
                if (Game.save.projects[1] <= 0) return;
                foreach (WorldObject host in Game.Map.objects)
                    if (host.kind == "campfire" && Game.IsObjectPresent(host))
                        AddProjectScenery(1, new Point(host.x + 1.35f, host.y + .5f), true);
            }
        }

        private void AddProjectScenery(int project, Point position, bool camp)
        {
            int tier = Game.save.projects[project];
            if (tier <= 0 || tier > 2) return;
            ProjectScenery prop = new ProjectScenery { project = project, tier = tier, position = position, camp = camp };
            Rect bounds = ProjectSceneryBounds(prop); float apron = Tile * 2;
            if (bounds.x + bounds.width < -apron || bounds.x > Width + apron || bounds.y + bounds.height < -apron || bounds.y > Height + apron) return;
            AddWorldEntry(new WorldDrawEntry { depth = position.y, kind = WorldDrawKind.Project, project = prop });
        }

        private Rect ProjectSceneryBounds(ProjectScenery prop)
        {
            Point p = ScreenPoint(prop.position);
            if (prop.camp) return new Rect(p.x - 34, p.y - 52, 68, 60);
            if (prop.project == 0) return new Rect(p.x - 58, p.y - 25, 116, 32);
            return prop.project == 1 ? new Rect(p.x - 58, p.y - 66, 116, 74) : new Rect(p.x - 46, p.y - 66, 92, 74);
        }

        private static float ProjectCoverWeight(Point actor, ProjectScenery prop)
        {
            float half = prop.camp ? .65f : prop.project == 0 ? 1.4f : 1.05f;
            float height = prop.camp ? 1 : prop.project == 0 ? .35f : 1.35f;
            float dx = Math.Max(0, Math.Abs(actor.x - prop.position.x) - half);
            float dy = Math.Max(0, Math.Max(actor.y - prop.position.y, prop.position.y - height - actor.y));
            float t = GameCore.Clamp(1 - (float)Math.Sqrt(dx * dx + dy * dy) / .7f, 0, 1);
            return t * t * (3 - 2 * t);
        }

        private float ProjectSceneryOpacity(ProjectScenery prop)
        {
            float cover = ProjectCoverWeight(Game.Position, prop);
            foreach (Enemy enemy in Game.enemies) cover = Math.Max(cover, ProjectCoverWeight(enemy.Position, prop));
            if (Game.save.companion >= 0) cover = Math.Max(cover, ProjectCoverWeight(Game.companionPosition, prop));
            return 1 - .6f * cover;
        }

        private void ProjectBox(Point p, float x, float y, float width, float height, Color color, float opacity)
        { color.a *= opacity; Box(p.x + x, p.y + y, width, height, color); }

        private void DrawProjectCrate(Point p, float size, float opacity)
        {
            if (fieldProps != null && detailSheet != null)
            { PropImage("crate", 0, p.x - size / 2, p.y - size + 8, size, size, new Color(1, 1, 1, opacity)); return; }
            // The legacy generated atlas has no crate cell. A bounded wooden
            // fallback must never silently turn supplies into another NPC.
            Color dark = PixelArt.C("#514738"), wood = PixelArt.C("#a48b61"), light = PixelArt.C("#c6ad7c");
            ProjectBox(p, -size * .4f, -size * .56f, size * .8f, size * .6f, dark, opacity);
            ProjectBox(p, -size * .35f, -size * .51f, size * .7f, size * .5f, wood, opacity);
            ProjectBox(p, -size * .35f, -size * .51f, size * .7f, 3, light, opacity);
            ProjectBox(p, -size * .3f, -size * .5f, 3, size * .5f, light, opacity);
            ProjectBox(p, size * .22f, -size * .5f, 3, size * .5f, light, opacity);
            ProjectBox(p, -size * .1f, -size * .51f, 3, size * .5f, dark, opacity);
        }

        private void DrawProjectTable(Point p, float opacity)
        {
            if (fieldProps != null && detailSheet != null)
            { PropImage("table", 0, p.x - 32, p.y - 40, 64, 48, new Color(1, 1, 1, opacity)); return; }
            Color wood = PixelArt.C("#a48b61"), dark = PixelArt.C("#514738"), light = PixelArt.C("#c6ad7c");
            ProjectBox(p, -28, -22, 5, 25, dark, opacity); ProjectBox(p, 23, -22, 5, 25, dark, opacity);
            ProjectBox(p, -34, -27, 68, 8, wood, opacity); ProjectBox(p, -34, -27, 68, 3, light, opacity);
            ProjectBox(p, -31, -19, 62, 3, dark, opacity);
        }

        private void DrawProjectTea(Point p, float opacity)
        {
            Color ceramic = PixelArt.C("#9ab8ac"), light = PixelArt.C("#d3dbbd"), dark = PixelArt.C("#405953");
            ProjectBox(p, -8, -12, 16, 10, dark, opacity); ProjectBox(p, -6, -14, 12, 11, ceramic, opacity);
            ProjectBox(p, -5, -16, 10, 3, light, opacity); ProjectBox(p, -1, -19, 3, 3, dark, opacity);
            ProjectBox(p, 6, -12, 7, 3, ceramic, opacity); ProjectBox(p, 10, -15, 3, 4, ceramic, opacity);
            ProjectBox(p, -12, -13, 4, 3, light, opacity); ProjectBox(p, -13, -12, 3, 7, light, opacity);
            ProjectBox(p, -12, -6, 4, 3, light, opacity);
        }

        private void DrawProjectScenery(ProjectScenery prop)
        {
            if (drawingOutline || prop.project < 0 || prop.project > 2 || prop.tier <= 0 || prop.tier > 2) return;
            Point p = ScreenPoint(prop.position); float opacity = ProjectSceneryOpacity(prop);
            Color stone = PixelArt.C("#677b79"), edge = PixelArt.C("#aab39c"), dark = PixelArt.C("#354c50"), water = PixelArt.C("#73a5ae");
            if (prop.project == 0)
            {
                ProjectBox(p, -50, -10, 100, 16, dark, opacity);
                ProjectBox(p, -46, -6, 92, 8, water, opacity);
                ProjectBox(p, -50, -11, 100, 4, stone, opacity); ProjectBox(p, -50, 2, 100, 4, stone, opacity);
                ProjectBox(p, -50, -11, 5, 17, edge, opacity); ProjectBox(p, 45, -11, 5, 17, edge, opacity);
                for (int i = -30; i <= 30; i += 20)
                { ProjectBox(p, i, -10, 2, 3, edge, opacity); ProjectBox(p, i + 5, 3, 2, 2, dark, opacity); }
                ProjectBox(p, -38, -4, 34, 1, new Color(.7f, .84f, .8f), opacity);
                if (prop.tier == 2)
                {
                    ProjectBox(p, -55, -22, 10, 18, stone, opacity); ProjectBox(p, -52, -20, 4, 17, water, opacity);
                    ProjectBox(p, 45, -22, 10, 18, stone, opacity); ProjectBox(p, 48, -20, 4, 17, water, opacity);
                    ProjectBox(p, -56, -24, 12, 3, edge, opacity); ProjectBox(p, 44, -24, 12, 3, edge, opacity);
                }
                return;
            }
            if (prop.camp)
            {
                DrawProjectCrate(p, 42, opacity);
                ProjectBox(p, -13, -23, 26, 5, PixelArt.C("#84998a"), opacity);
                if (prop.tier == 2) DrawProjectTea(p + new Point(0, -26), opacity);
                return;
            }
            DrawProjectTable(p, opacity);
            if (prop.project == 1)
            {
                DrawProjectCrate(p + new Point(-32, 2), 42, opacity);
                ProjectBox(p, -14, -29, 28, 8, PixelArt.C("#84998a"), opacity);
                ProjectBox(p, -10, -29, 3, 8, PixelArt.C("#d3c39b"), opacity);
                if (prop.tier == 2)
                {
                    DrawProjectTea(p + new Point(14, -25), opacity);
                    ProjectBox(p, -43, -39, 22, 3, edge, opacity);
                }
            }
            else if (prop.project == 2)
            {
                ProjectBox(p, 7, -34, 18, 5, stone, opacity); ProjectBox(p, 13, -30, 4, 13, PixelArt.C("#9f8059"), opacity);
                ProjectBox(p, -21, -34, 6, 12, water, opacity); ProjectBox(p, -24, -30, 12, 5, water, opacity);
                if (prop.tier == 2)
                {
                    ProjectBox(p, -29, -48, 4, 28, PixelArt.C("#9f8059"), opacity);
                    ProjectBox(p, -29, -49, 54, 5, PixelArt.C("#b5a17a"), opacity);
                    ProjectBox(p, -13, -45, 3, 10, stone, opacity); ProjectBox(p, 9, -45, 3, 10, stone, opacity);
                    ProjectBox(p, -8, -34, 5, 12, PixelArt.C("#b6c5d3"), opacity);
                }
            }
        }
    }
}
