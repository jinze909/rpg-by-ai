using UnityEngine;

namespace Starfall
{
    public sealed partial class FrontierRuntime
    {
        private bool BossPhaseCueVisible
        {
            get
            {
                if (Game.mode != "world" || Game.Dying || applicationPaused || VisualReviewPaused || TrainingReview) return false;
                BossPhaseCue cue = Game.GetBossPhaseCue(Game.enemies.Find(enemy => enemy.boss));
                return cue.phase > 0 && (cue.Changed || cue.Pending);
            }
        }
        private Rect BossPhaseCueBounds()
        { return CombatHud ? new Rect(380, 169, 520, 34) : new Rect(383, 573, 514, 34); }
        private bool SameBossPhaseCueRegion(Rect rect)
        {
            Rect cue = BossPhaseCueBounds();
            return rect.x == cue.x && rect.y == cue.y && rect.width == cue.width && rect.height == cue.height;
        }
        private void DrawBossPhaseThreshold(Enemy boss, float x, float y, float width, float height, float threshold)
        {
            float at = x + width * threshold;
            Box(at - 2, y - 2, 4, height + 4, new Color(.025f, .035f, .05f, .95f));
            Box(at - .5f, y - 1, 1, height + 2, gold);
        }
        private void DrawBossPhaseThresholds(Enemy boss, float x, float y, float width, float height)
        {
            if (Game.GetBossPhaseCue(boss).phase == 0) return;
            DrawBossPhaseThreshold(boss, x, y, width, height, GameCore.BossPhaseTwoThreshold);
            DrawBossPhaseThreshold(boss, x, y, width, height, GameCore.BossPhaseThreeThreshold);
        }
        private void DrawBossPhaseCue()
        {
            if (!BossPhaseCueVisible) return;
            BossPhaseCue cue = Game.GetBossPhaseCue(Game.enemies.Find(enemy => enemy.boss));
            Rect rect = BossPhaseCueBounds();
            float alpha = cue.Pending ? 1 : GameCore.Clamp(cue.remaining / .35f, 0, 1);
            Color fill = new Color(.035f, .065f, .095f, .92f * alpha), line = gold;
            if (ThreatOver(rect)) fill.a = .12f * alpha;
            line.a *= alpha;
            Box(rect.x, rect.y, rect.width, rect.height, fill); Frame(rect.x, rect.y, rect.width, rect.height, line);
            string text = cue.Pending
                ? Game.L("阶段 ", "PHASE ") + cue.phase + Game.L(" · 当前预告仍为阶段 ", " · current warning stays PHASE ") + cue.preparedPhase
                : Game.L("阶段 ", "PHASE ") + cue.fromPhase + " → " + cue.phase + Game.L(" · 注意后续攻击", " · watch the next attacks");
            Text(rect.x + 10, rect.y + 3, rect.width - 20, rect.height - 6, text, 14, line, TextAnchor.MiddleCenter);
        }
    }
}
