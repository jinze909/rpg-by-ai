using System;

namespace Starfall
{
    public sealed partial class GameCore
    {
        public const float ComboWindow = 1.1f, SwordFinisherMultiplier = 1.25f;

        // The original counter advances on accepted starts, including misses
        // and other weapons. Reading it must not invent confirmed hit counts.
        public float SwordChainRemaining
        { get { return save.weapon == 0 && combo >= 0 && combo < 3 && Finite(comboTimer) ? Math.Max(0, comboTimer) : 0; } }
        public int SwordChainStep { get { return SwordChainRemaining > 0 ? combo + 1 : 0; } }
        public bool SwordFinisherCommitted
        {
            get
            {
                ActorAction action = playerAction;
                return action != null && action.kind == "attack" && action.weapon == 0 && action.combo == 2
                    && save.weapon == 0 && action.equippedWeapon == save.weapon && action.profession == save.profession
                    && action.style == Style && action.technique == save.technique;
            }
        }
        public bool SwordFinisherReleased { get { return SwordFinisherCommitted && playerAction.released; } }
    }
}
