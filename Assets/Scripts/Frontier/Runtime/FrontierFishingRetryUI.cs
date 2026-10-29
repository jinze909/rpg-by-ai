using System;
using UnityEngine;

namespace Starfall
{
    public sealed partial class FrontierRuntime
    {
        private FishingRetryOffer drawnFishingRetry, armedFishingRetry;
        private bool fishingRetryPointerFrame;
        private bool fishingRetryMousePress;
        private bool FishingRetryVisible
        {
            get { return Game != null && Game.CanRetryFishing && !applicationPaused
                && !VisualReviewPaused && !TrainingReview; }
        }
        private Rect FishingRetryBounds() { return new Rect(382, 604, 516, 72); }
        private bool FishingRetryPointerProtected
        { get { return Game != null && Game.mode == "world" && !applicationPaused && !VisualReviewPaused
                && (FishingRetryVisible || fishingRetryPointerFrame); } }
        // Hiding resets activation, while a mouse press stays owned until release.
        private void ClearFishingRetryInput() { drawnFishingRetry = armedFishingRetry = null; fishingRetryPointerFrame = false; }
        private void UpdateFishingRetryInput()
        {
            // Keep the last visible card's mouse region for this input frame
            // even if Tick expires or invalidates its offer before the click.
            fishingRetryPointerFrame = drawnFishingRetry != null && Game.mode == "world";
            if (!Input.GetMouseButton(0) && !Input.GetMouseButtonDown(0)) fishingRetryMousePress = false;
            if (Input.GetMouseButtonDown(0)) fishingRetryMousePress = FishingRetryPointerProtected
                && Contains(FishingRetryBounds(), MouseVirtual());
            FishingRetryOffer offer = FishingRetryVisible ? Game.RecentFishingRetry : null;
            // The first draw is disabled. Only a later valid Update can arm
            // that exact offer, never the input which just ended the cast.
            armedFishingRetry = offer != null && ReferenceEquals(offer, drawnFishingRetry) ? offer : null;
            if (offer == null) drawnFishingRetry = null;
        }
        private void DrawFishingRetryHud()
        {
            if (!FishingRetryVisible) { ClearFishingRetryInput(); return; }
            FishingRetryOffer offer = Game.RecentFishingRetry;
            Rect rect = FishingRetryBounds();
            Panel(rect.x, rect.y, rect.width, rect.height, new Color(.045f, .08f, .13f, .96f), mint);
            Icon("fish", 394, 610, 20);
            Text(422, 610, 464, 22, Game.L("本次垂钓已结束", "This cast is finished"), 15, paper);
            bool ready = ReferenceEquals(offer, armedFishingRetry);
            if (Button(394, 638, 492, 30, offer.success ? Game.L("再钓一条", "Fish again")
                : Game.L("再试一次", "Try again"), true, ready) && ReferenceEquals(offer, armedFishingRetry)
                && Game.TryRetryFishing())
            {
                // A new cast cannot also accept a replayed reel callback.
                fishingInputConsumed = true;
                drawnFishingRetry = armedFishingRetry = null; confirmControl = null;
                return;
            }
            drawnFishingRetry = offer;
        }
    }
}
