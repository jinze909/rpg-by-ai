using System;

namespace Starfall
{
    // Read-only descriptions and contact receipts are session state. SaveData
    // and the mature command/automatic-support simulation remain unchanged.
    public sealed class CompanionTacticInfo
    {
        public readonly int id, bond, arrows, frostPulseCount;
        public readonly float cooldown, contact, duration, heal, energy, guardSeconds, incomingMultiplier;
        public readonly float arrowBaseDamage, frostRadius, frostDuration, frostPulseDamage, controlRadius, controlSeconds;
        internal CompanionTacticInfo(int id, int bond, float cooldown, float damage)
        {
            this.id = id; this.bond = bond; this.cooldown = cooldown;
            contact = GameCore.CompanionCommandContact; duration = GameCore.CompanionCommandDuration;
            heal = id == 0 ? GameCore.CompanionCommandHealing(bond) : 0;
            energy = id == 0 ? GameCore.CompanionCommandEnergy : 0;
            guardSeconds = id == 0 ? GameCore.CompanionCommandGuardSeconds : 0;
            incomingMultiplier = id == 0 ? GameCore.GuardIncomingMultiplier : 1;
            arrows = id == 1 ? GameCore.CompanionCommandArrows : 0;
            arrowBaseDamage = id == 1 ? GameCore.CompanionCommandArrowDamage(damage, bond) : 0;
            frostRadius = id == 2 ? FrostField.Radius : 0;
            frostDuration = id == 2 ? FrostField.Duration : 0;
            frostPulseCount = id == 2 ? FrostField.PulseCount : 0;
            frostPulseDamage = id == 2 ? damage * GameCore.FrostDamageMultiplier : 0;
            controlRadius = id == 2 ? GameCore.CompanionControlRadius : 0;
            controlSeconds = id == 2 ? GameCore.CompanionControlSeconds : 0;
        }
    }

    public sealed class CompanionCommandReceipt
    {
        public readonly int id, bond, arrowCount, controlCount;
        public readonly float actualHeal, actualEnergy, arrowBaseDamage, frostPulseDamage;
        public readonly Point center;
        internal CompanionCommandReceipt(int id, int bond, float actualHeal, float actualEnergy,
            int arrowCount, float arrowBaseDamage, int controlCount, Point center, float frostPulseDamage)
        {
            this.id = id; this.bond = bond; this.actualHeal = actualHeal; this.actualEnergy = actualEnergy;
            this.arrowCount = arrowCount; this.arrowBaseDamage = arrowBaseDamage;
            this.controlCount = controlCount; this.center = center; this.frostPulseDamage = frostPulseDamage;
        }
    }

    public sealed partial class GameCore
    {
        public const float CompanionCommandCooldown = 18, CompanionCommandPactCooldown = 12;
        public const float CompanionCommandContact = .2f, CompanionCommandDuration = .52f, CompanionReceiptDuration = 2.2f;
        public const float CompanionCommandEnergy = 15, CompanionCommandGuardSeconds = 3, GuardIncomingMultiplier = .45f;
        public const int CompanionCommandArrows = 3, CompanionHealingBase = 18, CompanionHealingPerBond = 3;
        public const float CompanionControlRadius = 5, CompanionControlSeconds = 1.4f;
        public const float FrostDamageMultiplier = .4f;
        public float CompanionCommandCooldownTotal { get { return save.frontierEnding == 3 ? CompanionCommandPactCooldown : CompanionCommandCooldown; } }
        public CompanionCommandReceipt LastCompanionReceipt { get; private set; }
        public float CompanionFeedbackTime { get; private set; }
        public static float CompanionCommandHealing(int bond) { return CompanionHealingBase + bond * CompanionHealingPerBond; }
        public static float CompanionCommandArrowDamage(float damage, int bond) { return damage * (.8f + bond * .05f); }
        public CompanionTacticInfo CompanionTactic(int id)
        { return id < 0 || id > 2 ? null : new CompanionTacticInfo(id, save.relationships[id], CompanionCommandCooldownTotal, Damage); }
        private void ResetCompanionTactics()
        { ClearBufferedCompanionCommand(); LastCompanionReceipt = null; CompanionFeedbackTime = 0; }
        private void TickCompanionReceipt(float dt)
        {
            if (practicing || Dying || save.companion < 0) { ResetCompanionTactics(); return; }
            CompanionFeedbackTime = Math.Max(0, CompanionFeedbackTime - dt);
        }
        private void RecordCompanionReceipt(CompanionCommandReceipt receipt)
        { LastCompanionReceipt = receipt; CompanionFeedbackTime = CompanionReceiptDuration; }
    }
}
