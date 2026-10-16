using System;
using UnityEngine;
namespace Starfall
{
    public sealed partial class FrontierRuntime
    {
        private Texture2D[] playerSheets;
        private Texture2D[] companionSheets;
        private Texture2D[] walkSheets, companionWalkSheets, bossWalkSheets;
        private Texture2D monsterWalkSheet;
        // The wider canvas leaves space for weapons while keeping the same
        // logical body scale and 120/128 foot anchor in every pose.
        private static float BossCanvasSize(int room) { return room == 5 ? 264 : 240; }
        private Texture2D playerPortraits, playerRear, playerSouth, playerReactionsRear, playerReactionsSouth, enemyArcherVertical, npcSheet, monsterSheet, bossSheet, fieldProps, terrainSheet, terrainVariants, detailSheet, effectSheet, iconSheet;
        private void LoadVisuals()
        {
            string root = "Frontier/Art/";
            playerSheets = new[] { Resources.Load<Texture2D>(root + "PlayerVanguard"), Resources.Load<Texture2D>(root + "PlayerRanger"), Resources.Load<Texture2D>(root + "PlayerArcanist") };
            companionSheets = new[] { Resources.Load<Texture2D>(root + "CompanionMira"), Resources.Load<Texture2D>(root + "CompanionIvo"), Resources.Load<Texture2D>(root + "CompanionSen") };
            walkSheets = new[] { Resources.Load<Texture2D>(root + "WalkVanguard"), Resources.Load<Texture2D>(root + "WalkRanger"), Resources.Load<Texture2D>(root + "WalkArcanist") };
            companionWalkSheets = new[] { Resources.Load<Texture2D>(root + "WalkMira"), Resources.Load<Texture2D>(root + "WalkIvo"), Resources.Load<Texture2D>(root + "WalkSen") };
            bossWalkSheets = new[] { Resources.Load<Texture2D>(root + "WalkBossA"), Resources.Load<Texture2D>(root + "WalkBossB") };
            monsterWalkSheet = Resources.Load<Texture2D>(root + "WalkMonsters");
            playerPortraits = Resources.Load<Texture2D>(root + "PlayerPortraits"); npcSheet = Resources.Load<Texture2D>(root + "Npcs");
            playerRear = Resources.Load<Texture2D>(root + "PlayerRear");
            playerSouth = Resources.Load<Texture2D>(root + "PlayerSouth");
            playerReactionsRear=Resources.Load<Texture2D>(root+"PlayerReactionsRear");
            playerReactionsSouth=Resources.Load<Texture2D>(root+"PlayerReactionsSouth");
            enemyArcherVertical = Resources.Load<Texture2D>(root + "EnemyArcherVertical");
            monsterSheet = Resources.Load<Texture2D>(root + "Monsters"); bossSheet = Resources.Load<Texture2D>(root + "Bosses");
            // Load immutable GPU masks before play, rather than on a first hit.
            foreach(Texture2D sheet in playerSheets) if(sheet!=null) { art.FlashMask(sheet); art.OutlineMask(sheet); }
            foreach(Texture2D sheet in companionSheets) if(sheet!=null) { art.FlashMask(sheet); art.OutlineMask(sheet); }
            foreach(Texture2D sheet in walkSheets) if(sheet!=null) { art.FlashMask(sheet); art.OutlineMask(sheet); }
            foreach(Texture2D sheet in companionWalkSheets) if(sheet!=null) { art.FlashMask(sheet); art.OutlineMask(sheet); }
            foreach(Texture2D sheet in bossWalkSheets) if(sheet!=null) { art.FlashMask(sheet); art.OutlineMask(sheet); }
            foreach(Texture2D sheet in new[]{playerRear,playerSouth,playerReactionsRear,playerReactionsSouth,enemyArcherVertical,monsterSheet,bossSheet,monsterWalkSheet})
                if(sheet!=null) { art.FlashMask(sheet); art.OutlineMask(sheet); }
            fieldProps = Resources.Load<Texture2D>(root + "FieldProps"); terrainSheet = Resources.Load<Texture2D>(root + "Terrain");
            terrainVariants = Resources.Load<Texture2D>(root + "TerrainVariants");
            detailSheet = Resources.Load<Texture2D>(root + "Details"); effectSheet = Resources.Load<Texture2D>(root + "Effects"); iconSheet = Resources.Load<Texture2D>(root + "Icons");
            LoadProjectileWitnessMasks();
        }
        private void Atlas(Texture2D sheet, int columns, int rows, int index, Rect rect, Color color, bool flip = false, float angle = 0)
        {
            if (sheet == null) return;
            index = Math.Max(0, Math.Min(columns * rows - 1, index));
            var matrix = GUI.matrix; Color previousColor = GUI.color;
            try
            {
                if (angle != 0) GUI.matrix = GuiGeometry.RotateInCanvas(matrix, rect, angle);
                GUI.color = color;
                GUI.DrawTextureWithTexCoords(rect, sheet, new Rect((index % columns + (flip ? 1 : 0)) / (float)columns, 1 - (index / columns + 1) / (float)rows, (flip ? -1 : 1) / (float)columns, 1f / rows));
            }
            finally { GUI.color = previousColor; GUI.matrix = matrix; }
        }
        private void Actor(Texture2D sheet, int columns, int rows, int frame, Point foot, float size, Color color, bool flip = false, float angle = 0)
        {
            if (drawingOutline && sheet != null) { sheet = art.OutlineMask(sheet); color = new Color(1,.56f,.35f,.9f); }
            Atlas(sheet, columns, rows, frame, new Rect(foot.x - size / 2, foot.y - size * 120 / 128, size, size), color, flip, angle);
        }
        private void DrawPlayerPreview(int equipment, float x, float y, float size)
        { Texture2D sheet = playerSheets[(int)GameCore.Clamp(equipment,0,2)]; Actor(sheet,4,3,0,new Point(x,y),ActorCanvas(sheet,4,size/192),Color.white); }
        private void DrawPlayerActor()
        {
            Point p = ScreenPoint(Game.Position);
            int look = Game.AppearanceWeapon;
            if (playerSheets == null || playerSheets[look] == null) { DrawLegacyHero(); return; }
            DrawContactShadow(p, 34, 7, -3, new Color(.025f, .04f, .07f, .38f));
            if (Game.racing && !drawingOutline) PropImage("skiff", 0, p.x - 51, p.y - 68, 102, 118);
            Point direction = Game.VisualFacing;
            Color tint = Game.playerFlash > 0 ? new Color(1.5f, 1.5f, 1.5f) : Color.white;
            if (Game.invulnerable > 0 && !Game.Dying && Game.playerFlash <= 0) tint.a = .65f + .35f * (float)Math.Abs(Math.Sin(clock * 16));
            HeroVisualFrame selected = HeroAnimationState.Select(Game);
            DrawWholeHero(selected, p, tint);
            if (!drawingOutline && Game.save.upgrades > 0 && !Game.Dying)
            { Point rune=p+new Point(direction.x<0?-24:24,-26);Atlas(effectSheet,4,4,13,new Rect(rune.x-7,rune.y-7,14,14),new Color(1,.83f,.54f,Math.Min(.8f,.3f+Game.save.upgrades*.09f))); }
            if (!drawingOutline && Game.guardTime > 0 && !Game.Dying) Ring(Game.Position, .44f, new Color(.76f, .87f, .73f, .65f), 20);
        }
        private Texture2D HeroAtlas(string name)
        {
            for (int i = 0; i < 3; i++)
            {
                if (name == HeroAnimationState.Atlases[i]) return playerSheets[i];
                if (name == HeroAnimationState.Atlases[i + 3]) return walkSheets[i];
            }
            if (name == "PlayerRear") return playerRear;
            if (name == "PlayerSouth") return playerSouth;
            if (name == "PlayerReactionsRear") return playerReactionsRear;
            if (name == "PlayerReactionsSouth") return playerReactionsSouth;
            throw new InvalidOperationException("Unknown whole-body hero atlas: " + name);
        }
        private void DrawWholeHero(HeroVisualFrame selected, Point foot, Color tint)
        {
            try
            {
#if UNITY_5_3_OR_NEWER
            // Native SpriteRenderer/Animator output occupies the original sorted
            // actor slot. A screen-space renderer would be hidden by IMGUI terrain.
            RenderTexture target = nativeHero.Render(selected, drawingOutline ? new Color(1, .56f, .35f, .9f) : tint, drawingOutline,
                !drawingOutline && Game.playerFlash > 0 ? Game.playerFlash / .09f * .86f : 0);
            Color previous = GUI.color;
            try { GUI.color = Color.white; GUI.DrawTexture(new Rect(foot.x - 96, foot.y - 180, 192, 192), target); }
            finally { GUI.color = previous; }
#else
            // Offline draw-call evidence uses the identical whole cell and foot.
            // This branch cannot certify native Animator or GPU behavior.
            Texture2D sheet = HeroAtlas(selected.atlas);
            Actor(sheet, 4, 3, selected.frame, foot, 192, tint, selected.flip);
            if (!drawingOutline && Game.playerFlash > 0)
                Actor(art.FlashMask(sheet), 4, 3, selected.frame, foot, 192,
                    new Color(1, 1, 1, Game.playerFlash / .09f * .86f), selected.flip);
#endif
            }
            catch (Exception ex) { throw new HeroPresentationException(ex); }
        }
        private int EnemyPose(Enemy e)
        {
            if (e.hitTime > 0) return 4;
            if (e.state == 1) return e.boss && Game.save.room == 1 && e.timer < .2f && (!GameCore.IsSignalWarden(e) || GameCore.SignalPattern(e) == 0) ? 3 : 2;
            if (e.releaseTime > 0) return 3;
            return e.moving ? 1 : 0;
        }
        private float BossCoverOpacity(Enemy boss)
        {
            float cover = BossCoverWeight(boss, Game.Position);
            if (Game.save.companion >= 0)
                cover = Math.Max(cover, BossCoverWeight(boss, Game.companionPosition));
            return 1 - .45f * cover;
        }
        private static float BossCoverWeight(Enemy boss, Point actor)
        {
            // The front edge fades INSIDE the old -.01 boundary: actors drawn
            // in front (including equal-depth ties) must never reveal the Boss.
            float front = GameCore.Clamp((boss.y - .01f - actor.y) / .2f, 0, 1);
            if (front <= 0) return 0;
            float dx = Math.Max(0, Math.Abs(actor.x - boss.x) - 1.4f);
            float dy = Math.Max(0, boss.y - 2.7f - actor.y);
            float edge = GameCore.Clamp(1 - (float)Math.Sqrt(dx * dx + dy * dy) / .8f, 0, 1);
            // Preserve the original .55 floor without a new timer, frame
            // history or changes to the authored body, flash or warning phase.
            return edge * edge * (3 - 2 * edge) * front * front * (3 - 2 * front);
        }
        private void DrawEnemyActor(Enemy e)
        {
            int bossRoom = BossArtRoom(e,Game.save.room);
            Point p = ScreenPoint(e.Position); float size = e.boss ? BossCanvasSize(bossRoom) : e.kind == 2 ? 106 : 96;
            DrawContactShadow(p, size * .44f, 7, -3, new Color(.025f, .04f, .07f, .35f));
            if (Game.practicing && !Game.SparringActive)
            {
                // The safe training rig still demonstrates a bodily wind-up and
                // release. A static sign plus a red circle cannot teach timing.
                Actor(monsterSheet, 6, 3, 12 + EnemyPose(e), p, 106, Color.white, e.VisualFlip);
                if(!drawingOutline)Box(p.x - 16, p.y + 2, 32, 3, mint); return;
            }
            int pose = EnemyPose(e);
            float charge = e.state == 1 ? 1 - e.timer / Math.Max(.01f, e.windupDuration > 0 ? e.windupDuration : e.boss ? 1.1f : .65f) : 0;
            Point offset = new Point();
            // Recoil is swept in Core rather than sliding the drawing through walls.
            Color tint = e.flash > 0 ? new Color(1.55f, 1.55f, 1.55f) : Color.white;
            if (GameCore.IsSignalWarden(e) && e.flash <= 0) tint = new Color(.8f,1,.88f);
            if (e.boss) tint.a = BossCoverOpacity(e);
            if (e.boss)
            {
                int row = Math.Max(0, bossRoom - 1), frame = pose == 2 ? 1 : pose == 3 ? 2 : 0;
                if (pose == 1 && bossWalkSheets[row / 4] != null)
                    Actor(bossWalkSheets[row / 4], 4, 4, row % 4 * 4 + GameCore.GaitFrame(e.moveTime, 2.8f), p, size * 384 / 320f, tint, e.VisualFlip);
                else Actor(bossSheet, 4, 8, row * 4 + frame, p + offset, size, tint, e.VisualFlip);
                if (!drawingOutline && e.flash > 0 && bossSheet != null) Actor(art.FlashMask(bossSheet), 4, 8, row * 4 + frame, p + offset, size, new Color(1, 1, 1, e.flash / .09f * .86f * tint.a), e.VisualFlip);
                if (!drawingOutline && e.state == 1) Atlas(effectSheet,4,4,13,new Rect(p.x-19,p.y-GameCore.BossEmissionHeight(bossRoom)-19,38,38),new Color(1,.79f,.49f,charge*.6f));
            }
            else
            {
                Texture2D sheet = monsterSheet; int columns = 6, rows = 3, frame = Math.Min(2, e.kind) * 6 + pose;
                if (pose == 1 && monsterWalkSheet != null) { sheet = monsterWalkSheet; columns = 4; rows = 3; int gait=GameCore.GaitFrame(e.moveTime,e.kind==0?1.35f:1.8f);frame=Math.Min(2,e.kind)*4+(e.backpedaling?(4-gait)%4:gait); }
                bool vertical = e.kind == 1 && enemyArcherVertical != null && (pose == 2 || pose == 3) && Math.Abs(e.facing.y) > Math.Abs(e.facing.x);
                if (vertical) { sheet = enemyArcherVertical; columns = 3; rows = 2; frame = (e.facing.y < 0 ? 0 : 2) + pose - 2; }
                float canvas=ActorCanvas(sheet,columns,size/128);
                Actor(sheet, columns, rows, frame, p + offset, canvas, tint, !vertical && e.VisualFlip);
                if (!drawingOutline && e.flash > 0 && sheet != null) Actor(art.FlashMask(sheet), columns, rows, frame, p + offset, canvas, new Color(1, 1, 1, e.flash / .09f * .86f), !vertical && e.VisualFlip);
            }
        }
        private void DrawFallen(FallenActor body)
        {
            Point p = ScreenPoint(body.actor.Position); float progress = 1 - body.life / body.duration;
            Color tint = new Color(1, 1, 1, Math.Min(1, body.life / (body.duration * .45f)));
            int bossRoom = BossArtRoom(body.actor,body.room);
            if (GameCore.IsSignalWarden(body.actor)) { tint.r = .8f; tint.b = .88f; }
            if (body.actor.boss) Actor(bossSheet, 4, 8, Math.Max(0, bossRoom - 1) * 4 + 3, p, BossCanvasSize(bossRoom), tint,body.actor.VisualFlip);
            else Actor(monsterSheet, 6, 3, Math.Min(2, body.actor.kind) * 6 + 5, p, body.actor.kind == 2 ? 106 : 96, tint,body.actor.VisualFlip);
            if (body.actor.boss && progress < .6f) Atlas(effectSheet, 4, 4, 15, new Rect(p.x - 52, p.y - 80 - progress * 40, 104, 104), new Color(.8f, .92f, .84f, .7f * (1 - progress)));
        }
        private int NpcId(string kind)
        {
            if (kind == "guild") return 0; if (kind == "garden") return 1; if (kind == "forge") return 2;
            if (kind == "cook" || kind == "rest") return 3; if (kind == "fish") return 4;
            if (kind == "scout" || kind == "returnscout") return 5; if (kind == "mentor") return 6;
            if (kind == "envoy") return 7; if (kind == "captain") return 8; if (kind == "tinker") return 9;
            if (kind == "keeper") return 10; if (kind == "council") return 11;
            if (kind.StartsWith("castaway")) return 12 + (kind.EndsWith("1") ? 1 : 0);
            return kind == "innGuest" ? 15 : 14;
        }
        private void DrawNpc(string kind, Point position, int id = -1)
        {
            Point p = ScreenPoint(position);
            DrawContactShadow(p, 26, 5, -2, new Color(.025f, .04f, .07f, .32f));
            bool occludes = Math.Abs(position.x - Game.save.x) < .85f && Game.save.y < position.y && Game.save.y > position.y - 1.8f;
            Actor(npcSheet, 4, 4, id < 0 ? NpcId(kind) : id, p, 96, occludes ? new Color(1, 1, 1, .45f) : Color.white);
        }
    }
}
