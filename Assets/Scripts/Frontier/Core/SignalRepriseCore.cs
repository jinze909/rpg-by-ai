using System;

namespace Starfall
{
    public sealed class SignalRepriseRecord
    {
        public readonly bool won;
        public readonly float seconds, damage;
        public readonly int contacts, breaks, hitsTaken;
        public SignalRepriseRecord(bool won, float seconds, float damage, int contacts, int breaks, int hitsTaken)
        { this.won = won; this.seconds = seconds; this.damage = damage; this.contacts = contacts; this.breaks = breaks; this.hitsTaken = hitsTaken; }
    }

    public sealed partial class GameCore
    {
        private Enemy signalRepriseEnemy, signalRepriseFallenEnemy;
        private SaveData signalRepriseSave;
        private int signalRepriseGeneration, signalRepriseContacts, signalRepriseBreaks, signalRepriseHits;
        private float signalRepriseClock, signalRepriseDamage;
        public SignalRepriseRecord LastSignalReprise { get; private set; }
        public SignalRepriseRecord PreviousSignalReprise { get; private set; }
        private bool OwnsSignalReprise(Enemy enemy)
        {
            return enemy != null && ReferenceEquals(enemy, signalRepriseEnemy) && ReferenceEquals(save, signalRepriseSave) &&
                WorldGeneration == signalRepriseGeneration && save.room == 1 && SignalReady && SignalComplete && !practicing && IsSignalWarden(enemy);
        }
        public bool SignalRepriseActive
        { get { return OwnsSignalReprise(signalRepriseEnemy) && enemies.Contains(signalRepriseEnemy) && signalRepriseEnemy.hp > 0; } }
        private float SignalRepriseElapsed { get { return OwnsSignalReprise(signalRepriseEnemy) ? signalRepriseClock : 0; } }
        public float SignalRepriseTime { get { return SignalRepriseActive ? SignalRepriseElapsed : LastSignalReprise == null ? 0 : LastSignalReprise.seconds; } }
        public float SignalRepriseDamage { get { return signalRepriseDamage; } }
        public int SignalRepriseContacts { get { return signalRepriseContacts; } }
        public int SignalRepriseBreaks { get { return signalRepriseBreaks; } }
        public int SignalRepriseHitsTaken { get { return signalRepriseHits; } }
        public bool IsSignalRepriseActor(Enemy enemy) { return OwnsSignalReprise(enemy); }
        public bool IsFallenSignalRepriseActor(Enemy enemy)
        { return enemy != null && ReferenceEquals(enemy, signalRepriseFallenEnemy) && ReferenceEquals(save, signalRepriseSave) &&
                WorldGeneration == signalRepriseGeneration && save.room == 1; }
        private bool AtSignalRepriseLamp()
        {
            if (save.room != 1 || !SignalReady || !SignalComplete || !WatchLampLit(2) || practicing || Dying || save.health <= 0 || !Finite(save.health)) return false;
            foreach (WorldObject obj in Map.objects)
                if (obj.kind == "watchSignal2" && IsObjectPresent(obj) && (new Point(obj.x, obj.y) - Position).Length < 1.75f &&
                    ClearAttackPath(Position, new Point(obj.x, obj.y))) return true;
            return false;
        }
        private void RetireSignalRepriseActions()
        {
            CancelAction(); ClearBufferedCompanionCommand(); ClearCombatFocus(); ClearFrost(); companionAction = null;
            foreach (Projectile shot in projectiles.ToArray())
                if (shot.friendly || shot.ownerId == SignalWardenId)
                {
                    projectiles.Remove(shot);
                    effects.Add(new Effect("projectileFade", shot.position, .25f)
                        { direction = shot.velocity.Normalized, variant = ProjectileFadeFrame(shot), height = shot.height });
                }
        }
        public bool StartSignalReprise()
        {
            if (mode != "world" || !AtSignalRepriseLamp() || playerAction != null || dashTime > 0 || playerHitTime > 0 ||
                enemies.Exists(e => e.boss || e.id == SignalWardenId)) return false;
            RetireSignalRepriseActions();
            signalRepriseEnemy = CreatePlacedEnemy(SignalWardenId, 3, SignalPoint + new Point(-3, 0), SignalWardenHealth, true);
            signalRepriseEnemy.timer = 1.2f; signalRepriseEnemy.Face(Position - signalRepriseEnemy.Position);
            signalRepriseSave = save; signalRepriseGeneration = WorldGeneration; signalRepriseClock = 0;
            signalRepriseDamage = 0; signalRepriseContacts = signalRepriseBreaks = signalRepriseHits = 0;
            enemies.Add(signalRepriseEnemy); revision++;
            Notify("旧路复试开始。正常承伤与消耗，不重复发奖。", "Warden reprise begins. Normal damage and supplies; no repeat reward.");
            sound = "windup"; return true;
        }
        private void OnSignalRepriseContact(Enemy enemy, ImpactProfile impact, float healthBefore, bool broke)
        {
            if (!OwnsSignalReprise(enemy) || impact == null || !impact.playerOwned || !Finite(healthBefore) || !Finite(enemy.hp)) return;
            float lost = Math.Min(Math.Max(0, healthBefore), Math.Max(0, healthBefore - enemy.hp));
            if (lost <= 0) return;
            signalRepriseDamage += lost; signalRepriseContacts++; if (broke) signalRepriseBreaks++;
        }
        private void TickSignalRepriseClock(float dt)
        { if (SignalRepriseActive) signalRepriseClock += dt; }
        private void OnSignalRepriseHurt(float healthBefore)
        { if (SignalRepriseActive && save.health < healthBefore) signalRepriseHits++; }
        private void FinishSignalReprise(bool won)
        {
            if (won) signalRepriseFallenEnemy = signalRepriseEnemy;
            PreviousSignalReprise = LastSignalReprise;
            LastSignalReprise = new SignalRepriseRecord(won, SignalRepriseElapsed, signalRepriseDamage,
                signalRepriseContacts, signalRepriseBreaks, signalRepriseHits);
            RetireSignalRepriseActions();
            // Keep the reference for the existing fallen-body message only;
            // live membership, not an ID or completion flag, owns the attempt.
            revision++;
        }
        private bool TryFinishSignalReprise(Enemy enemy)
        {
            if (!OwnsSignalReprise(enemy)) return false;
            FinishSignalReprise(true);
            Notify("旧路复试完成。记录已保留，本次没有报酬。", "Warden reprise complete. Result recorded; no reward for this repeat."); sound = "ui";
            return true;
        }
        public bool StopSignalReprise()
        {
            if (mode != "world" || !AtSignalRepriseLamp() || !SignalRepriseActive) return false;
            Enemy enemy = signalRepriseEnemy; FinishSignalReprise(false); enemies.Remove(enemy);
            Notify("已结束复试。生命、物资与世界时间照常保留。", "Reprise ended. Health, supplies and world time keep their actual values."); return true;
        }
        private void ResetSignalReprise()
        {
            signalRepriseEnemy = signalRepriseFallenEnemy = null; signalRepriseSave = null;
            signalRepriseDamage = signalRepriseClock = 0;
            signalRepriseContacts = signalRepriseBreaks = signalRepriseHits = 0;
            LastSignalReprise = PreviousSignalReprise = null;
        }
        public void ShowSignalReprise()
        {
            if (Dying || practicing || !SignalReady || !SignalComplete || !NearSignalContact()) return;
            string zh = "旧灯继续照路。你也可以在第三处灯自愿再试一次守望者的三招，比较新的装备和打法。\n\n正常承伤、消耗物资，世界时间照常推进；开始和结束不会回复生命或耐力；复试守望者不提供熟练度、悬赏进度、经验、硬币、碎片或技能点。森林的其他敌人与报酬、原完成记录照常保留。\n\n记录本次用时、自身对守望者造成的实际伤害、命中、破势和受击；不排名。菜单暂停计时。回灯旁可以结束，也可在获胜后再试。旅行、倒下或读档会结束复试，结果只保留在本次地区会话。";
            string en = "The old light still guides travelers. At the third signal, freely face the warden's three moves again and compare new gear and approaches.\n\nNormal damage, supplies and world time apply. Starting or stopping grants no healing or energy refill. The reprise warden awards no mastery, bounty progress, XP, coins, shards or talent points. Other forest foes and their rewards, and your original completion, stay.\n\nRecord this attempt's time, your actual damage to the warden, contacts, breaks and hits taken; no ranking. Menus pause the clock. Return to the lamp to stop, or retry after victory. Travel, defeat or loading ends it; results last only in this region session.";
            if (SignalRepriseActive)
                Dialog("旧路复试", "Warden reprise", zh, en, C("继续交手", "Continue the fight", "close"), C("结束本次复试", "End this attempt", "signal_reprise_stop"));
            else if (save.room == 1)
                Dialog("旧路复试", "Warden reprise", zh, en, C("开始复试 · 无报酬", "Begin reprise · no reward", "signal_reprise_start"), C("先做准备", "Prepare first", "close"));
            else Dialog("旧路复试", "Warden reprise", zh, en, C("去第三处旧灯再试", "Visit the third forest signal", "close"));
        }
        private bool ChooseSignalReprise(string action)
        {
            if (action == "signal_reprise_menu") { ShowSignalReprise(); return true; }
            if (action == "signal_reprise_start") { if (!StartSignalReprise()) Notify("请在第三处旧灯旁，等动作结束后再试。", "At the third signal, finish your current action before retrying."); return true; }
            if (action == "signal_reprise_stop") { StopSignalReprise(); return true; }
            return false;
        }
    }
}
