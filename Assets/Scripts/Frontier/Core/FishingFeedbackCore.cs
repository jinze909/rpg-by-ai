using System;

namespace Starfall
{
    public enum FishingPullOutcome { Good, Left, Right, Timeout }

    public sealed class FishingPullFeedback
    {
        public readonly FishingPullOutcome outcome;
        public readonly float cursor, lower, upper;
        public readonly int goodPulls, misses;
        public readonly bool river;

        internal FishingPullFeedback(FishingPullOutcome outcome, float cursor, float lower, float upper,
            int goodPulls, int misses, bool river)
        {
            this.outcome = outcome; this.cursor = cursor; this.lower = lower; this.upper = upper;
            this.goodPulls = goodPulls; this.misses = misses; this.river = river;
        }
    }

    public sealed partial class GameCore
    {
        public const float FishingPullFeedbackDuration = 1.2f;
        private FishingPullFeedback fishingPullFeedback;
        private SaveData fishingPullFeedbackOwner;
        private int fishingPullFeedbackGeneration, fishingPullFeedbackRoom;
        private float fishingPullFeedbackRemaining;

        private bool FishingPullFeedbackActive
        {
            get
            {
                return fishingPullFeedback != null && fishingPullFeedbackRemaining > 0 && mode == "fish"
                    && ReferenceEquals(save, fishingPullFeedbackOwner) && WorldGeneration == fishingPullFeedbackGeneration
                    && save.room == fishingPullFeedbackRoom && (!fishingPullFeedback.river || RiverFishingActive);
            }
        }
        public FishingPullFeedback RecentFishingPull
        { get { return FishingPullFeedbackActive ? fishingPullFeedback : null; } }
        public float FishingPullFeedbackRemaining
        { get { return FishingPullFeedbackActive ? fishingPullFeedbackRemaining : 0; } }

        private void ResetFishingPullFeedback()
        { fishingPullFeedback = null; fishingPullFeedbackOwner = null; fishingPullFeedbackRemaining = 0; }
        private void RecordFishingPull(FishingPullOutcome outcome, float lower, float upper, bool river)
        {
            fishingPullFeedback = new FishingPullFeedback(outcome, fishCursor, lower, upper, fishHits, fishMisses, river);
            fishingPullFeedbackOwner = save; fishingPullFeedbackGeneration = WorldGeneration; fishingPullFeedbackRoom = save.room;
            fishingPullFeedbackRemaining = FishingPullFeedbackDuration;
        }
        private void TickFishingPullFeedback(float dt)
        {
            if (!FishingPullFeedbackActive) { ResetFishingPullFeedback(); return; }
            fishingPullFeedbackRemaining = Math.Max(0, fishingPullFeedbackRemaining - dt);
            if (fishingPullFeedbackRemaining <= 0) ResetFishingPullFeedback();
        }
    }
}
