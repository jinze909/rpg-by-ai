using System;

namespace Starfall
{
    // This session-only offer observes an attempt that already settled. It
    // never owns rewards, lesson progress or a persistent save field.
    public sealed class FishingRetryOffer
    {
        public readonly bool river, success;
        public readonly int site;
        internal readonly SaveData owner;
        internal readonly int generation, room;
        internal readonly WorldObject host;
        internal readonly Point hostPosition, position;

        internal FishingRetryOffer(bool river, bool success, int site, SaveData owner, int generation,
            int room, WorldObject host, Point position)
        {
            this.river = river; this.success = success; this.site = site;
            this.owner = owner; this.generation = generation; this.room = room;
            this.host = host; hostPosition = new Point(host.x, host.y); this.position = position;
        }
    }

    public sealed partial class GameCore
    {
        public const float FishingRetryDuration = 8f;
        private FishingRetryOffer fishingRetry;
        private float fishingRetryRemaining;

        private WorldObject LakeFishingHost
        { get { return Array.Find(data.maps[0].objects, o => o.id == "fisher" && o.kind == "fish"); } }

        private bool FishingRetryValid(FishingRetryOffer offer)
        {
            if (offer == null || fishingRetryRemaining <= 0 || mode != "world" || Dying || practicing || InAbyss
                || !ReferenceEquals(save, offer.owner) || WorldGeneration != offer.generation || save.room != offer.room
                || !Finite(save.health) || save.health <= 0 || !Finite(save.x) || !Finite(save.y) || !Fits(Position)
                || !Finite(offer.position.x) || !Finite(offer.position.y) || (Position - offer.position).Length > .2f
                || playerAction != null || !Finite(dashTime) || !Finite(hitStop) || !Finite(playerHitTime)
                || dashTime > 0 || hitStop > 0 || playerHitTime > 0
                || bufferedAttack != null || bufferedSkill != null || bufferedDodge != null || bufferedCompanionCommand != null
                || !SafeTravel || hazards.Count != 0 || projectiles.Exists(p => !p.friendly && p.life > 0)) return false;
            WorldObject host = offer.host;
            if (host == null || !Finite(host.x) || !Finite(host.y) || host.x != offer.hostPosition.x || host.y != offer.hostPosition.y
                || Array.IndexOf(Map.objects, host) < 0 || !IsObjectPresent(host) || !ReferenceEquals(Nearest(), host)
                || !ClearAttackPath(Position, new Point(host.x, host.y))) return false;
            return offer.river
                ? save.room == ReachRoom && RiverLessonAccepted && ReferenceEquals(RiverSpot(offer.site), host)
                : save.room == 0 && offer.site == -1 && ReferenceEquals(LakeFishingHost, host);
        }

        public FishingRetryOffer RecentFishingRetry
        { get { return FishingRetryValid(fishingRetry) ? fishingRetry : null; } }
        public bool CanRetryFishing
        { get { return RecentFishingRetry != null && save.inventory[3] >= 0 && save.inventory[3] < SupplyInventoryLimit; } }

        private void ClearFishingRetry()
        { fishingRetry = null; fishingRetryRemaining = 0; }

        private void RecordFishingRetry(bool river, bool success, int site, WorldObject host)
        {
            ClearFishingRetry();
            if (host == null) return;
            fishingRetry = new FishingRetryOffer(river, success, site, save, WorldGeneration, save.room, host, Position);
            fishingRetryRemaining = FishingRetryDuration;
            if (!FishingRetryValid(fishingRetry)) ClearFishingRetry();
        }

        private void TickFishingRetry(float dt)
        {
            // Retire invalid ownership before movement or queued combat can
            // advance. HUD reads only hide invalid offers and never mutate.
            if (!FishingRetryValid(fishingRetry)) { ClearFishingRetry(); return; }
            fishingRetryRemaining = Math.Max(0, fishingRetryRemaining - dt);
            if (fishingRetryRemaining <= .00001f) ClearFishingRetry();
        }

        private void BeginLakeFishing()
        {
            ResetFishingPullFeedback(); fishTime = fishCursor = 0; fishHits = fishMisses = 0; mode = "fish";
        }

        public bool TryRetryFishing()
        {
            FishingRetryOffer offer = RecentFishingRetry;
            if (offer == null || !CanRetryFishing) return false;
            // Consume before the new cast changes mode. Repeated GUI or API
            // activation cannot reuse the settled attempt as another restart.
            ClearFishingRetry();
            if (offer.river) return StartRiverFishing(offer.site);
            BeginLakeFishing(); return true;
        }
    }
}
