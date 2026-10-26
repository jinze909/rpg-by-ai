using System;

namespace Starfall
{
    public sealed partial class GameCore
    {
        // Shared with presentation; these are the original supply rules.
        public const float StewAttackBonus = 7, StewDuration = 90;
        public const float TeaRecoveryBonus = 6, TeaDuration = 180;

        public float StewBoostRemaining { get { return ProvisionRemaining(save.foodUntil); } }
        public float TeaBoostRemaining { get { return ProvisionRemaining(save.teaUntil); } }

        private float ProvisionRemaining(float deadline)
        {
            if (float.IsNaN(deadline) || float.IsInfinity(deadline)
                || float.IsNaN(save.totalTime) || float.IsInfinity(save.totalTime)) return 0;
            float remaining = deadline - save.totalTime;
            // Read the saved deadline, including valid older food durations.
            // Drawing a status must neither renew nor cap the actual effect.
            return remaining > 0 && !float.IsInfinity(remaining) ? remaining : 0;
        }
    }
}
