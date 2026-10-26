namespace Starfall
{
    public sealed partial class GameCore
    {
        private bool interactionInputRequested;

        // Match the existing Interact/Talk guards. An empty E or an unavailable
        // practice interaction must not swallow a deliberate combat request.
        public bool CanPrioritizeInteraction
        {
            get { return mode == "world" && !Dying && (SparringActionBlocked || !practicing && Nearest() != null); }
        }
        private void ClearInteractionCombatRequests()
        { ClearBufferedCombatInputs(); ClearBufferedCompanionCommand(); }

        // Advance the original world clock and all accepted actions. Only the
        // dispatch of unstarted requests yields to an actual interaction at
        // the moved feet; the runtime still performs Interact after this tick.
        public void TickForInteraction(float dt, Point movement, Point? explicitAim = null)
        {
            bool previous = interactionInputRequested;
            interactionInputRequested = true;
            try
            {
                Tick(dt, movement, explicitAim);
                // Hit stop and zero-delta frames return before dispatch. A
                // valid interaction still abandons their unstarted requests.
                if (CanPrioritizeInteraction) ClearInteractionCombatRequests();
            }
            finally { interactionInputRequested = previous; }
        }
    }
}
