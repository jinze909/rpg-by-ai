#if UNITY_EDITOR
using NUnit.Framework;
using UnityEngine;

namespace Starfall.Tests
{
    public sealed class UnityCoreTests
    {
        [Test] public void BossWarningCommitmentAcrossActualHits(){CoreChecks.BossWarningsKeepPreparedPhaseAcrossRealHits(Data());}
        [Test] public void BossReleaseCommitmentAcrossHealthThresholds(){CoreChecks.BossReleaseKeepsPreparedPhaseAcrossThresholds(Data());}
        [Test] public void BossMatureThreePhaseOutputsRemain(){CoreChecks.BossUnchangedPhasesKeepMatureAttackOutputs(Data());}
        [Test] public void BossNewWarningsAfterReleaseAndBreak(){CoreChecks.BossNextAttackAndBreakAdoptCurrentPhase(Data());}
        [Test] public void BossPausedAndRestoredWarnings(){CoreChecks.BossPreparedAttacksPauseAndLoadAsFreshWarnings(Data());}
        [Test] public void ReachEarnedEntryAndMatureMapIdentity(){CoreChecks.ReachAppendedRegionRequiresEarnedPowerAndKeepsMatureRoutes(Data());}
        [Test] public void ReachLargeMapFullFootprintsAndTerrainRoutes(){CoreChecks.ReachLargeMapFootprintsAndPermanentRoutesMatchPresentation(Data());}
        [Test] public void ReachScaledExplorationAndLocalNavigation(){CoreChecks.ReachScaledExplorationKeepsLegacyBitsAndOwnedNavigation(Data());}
        [Test] public void ReachLocalCallbacksAndOnceOnlyTransactions(){CoreChecks.ReachLocalActionsAndOnceOnlyRewardsRejectStaleDecisions(Data());}
        [Test] public void ReachCanonicalSnapshotsAndForeignActorIsolation(){CoreChecks.ReachCanonicalSnapshotsRejectForeignActorsAndPreserveDefeats(Data());}
        [Test] public void ReachLegacyMigrationTravelAndRealDefeatRecovery(){CoreChecks.ReachLegacySavesTravelAndDefeatPreserveEarnedSurvey(Data());}
        [Test] public void ReachThreeStarterProfessionsAndPermanentPatrolDefeats(){CoreChecks.ReachEveryStarterClassClearsActualPatrolsWithoutRewardFarming(Data());}
        [Test] public void GuildChapterBriefingsAndMatureChoices(){CoreChecks.GuildChapterBriefingsFollowEarnedRoutesAndOriginalChoices(Data());}
        [Test] public void GuildDeliveryTransactionsAndCampaignGates(){CoreChecks.GuildCoreDeliveriesKeepTransactionsAndCampaignGates(Data());}
        [Test] public void GuildActualEvidenceAndMissingRecords(){CoreChecks.GuildEvidenceUsesRealDiscoveryAndTruthfulGaps(Data());}
        [Test] public void GuildEvidenceOnceOnlyAndLegacyRecovery(){CoreChecks.GuildEvidenceReviewIsOptionalOnceAndPersistent(Data());}
        [Test] public void GuildStoryLocalAndDefeatBoundaries(){CoreChecks.GuildStoryAccessRevalidatesLocationModesAndDefeat(Data());}
        [Test] public void GuildLetterMemoriesAndMatureEndings(){CoreChecks.GuildLetterReactsToEarnedStoriesAndProtectsEndings(Data());}
        [Test] public void SparringMentorEntryAndMatureChoices(){CoreChecks.SparringEntryRequiresMentorAndPreservesBasicChoices(Data());}
        [Test] public void SparringLiveContactFeedbackAndTactics(){CoreChecks.SparringRealHitsCountersFlanksAndBreaksKeepCombatFeedback(Data());}
        [Test] public void SparringStarterClassesAndOnceOnlyReward(){CoreChecks.SparringEveryStarterClassClearsMixedWavesAndRewardsOnce(Data());}
        [Test] public void SparringSandboxEconomyAndVitals(){CoreChecks.SparringSandboxFreezesWorldEconomyAndRestoresVitals(Data());}
        [Test] public void SparringPauseAndPreparedContactTeardown(){CoreChecks.SparringPauseManualAdvanceAndExitCancelPreparedContacts(Data());}
        [Test] public void SparringSavedReturnBody(){CoreChecks.SparringSaveReturnsSafeBodyWithoutResumingPracticeActors(Data());}
        [Test] public void SparringLegacyAndMaturePractice(){CoreChecks.SparringLegacyProgressAndOriginalDummyPracticeRemainIndependent(Data());}
        [Test] public void SparringMalformedReturnAndOwnedActorRecords(){CoreChecks.SparringMalformedReturnRecordsAndReservedActorsStaySafe(Data());}
        [Test] public void SparringNativeJsonReturnRecordAndLegacyDefaults()
        {
            foreach(int version in new[]{1,2})
            {
                var old = JsonUtility.FromJson<SaveData>("{\"version\":" + version + ",\"coins\":87,\"room\":0}");
                var loaded = new GameCore(Data(),old);
                Assert.IsTrue(loaded.save.sparringReturn==null); Assert.IsFalse(loaded.SparringActive); Assert.AreEqual(87,loaded.save.coins);
            }
            var game = new GameCore(Data()); game.NewGame(2); game.Close(); game.save.practiceBits=7;
            game.save.x=11; game.save.y=15; game.save.health=67; game.save.energy=42;
            Assert.IsTrue(game.StartSparring()); game.PrepareSave();
            var saved = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(game.save));
            Assert.NotNull(saved.sparringReturn);
            var resumed = new GameCore(Data(),saved); resumed.Start();
            Assert.IsFalse(resumed.SparringActive); Assert.IsTrue(resumed.save.sparringReturn==null);
            Assert.AreEqual(11,resumed.save.x); Assert.AreEqual(15,resumed.save.y);
            Assert.AreEqual(67,resumed.save.health); Assert.AreEqual(42,resumed.save.energy);
        }
        [Test] public void ControllerAimRadialDeadzoneAndFiniteValues(){CoreChecks.ControllerAimUsesFiniteRadialDirections(Data());}
        [Test] public void ControllerAimSafeSettingsTransactions(){CoreChecks.ControllerAimSettingsRequireSafeSettingsModes(Data());}
        [Test] public void ControllerAimLegacyAndSavedPreferences(){CoreChecks.ControllerAimLegacyAndSavedPreferencesStayCompatible(Data());}
        [Test] public void ControllerAimJourneyAndRecoveryPreferences(){CoreChecks.ControllerAimPreferencesSurviveJourneyAndRecovery(Data());}
        [Test] public void ControllerAimRetreatFireAndDodge(){CoreChecks.ControllerAimRetreatFireAndDodgeUseSimulationDirections(Data());}
        [Test] public void ControllerAimBufferedSkillIntent(){CoreChecks.ControllerAimBufferedSkillsKeepCapturedStickIntent(Data());}
        [Test] public void ControllerAimNativeJsonDefaultsAndRoundTrip()
        {
            foreach(int version in new[]{1,2})
            {
                var old = JsonUtility.FromJson<SaveData>("{\"version\":" + version + ",\"coins\":87}");
                var loaded = new GameCore(Data(),old);
                Assert.AreEqual(0,loaded.save.controllerAimProfile); Assert.IsFalse(loaded.save.controllerAimInvertY); Assert.AreEqual(87,loaded.save.coins);
            }
            var game = new GameCore(Data()); game.mode="pause"; game.ConfigureControllerAim(1,true); game.PrepareSave();
            var resumed = new GameCore(Data(),JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(game.save)));
            Assert.AreEqual(1,resumed.save.controllerAimProfile); Assert.IsTrue(resumed.save.controllerAimInvertY);
        }
        [Test] public void ArchiveEntryAndOriginalRuins(){CoreChecks.ArchiveEntryRequiresEarnedPowerAndPreservesRuins(Data());}
        [Test] public void ArchiveValveTransactionAndSave(){CoreChecks.ArchiveValvePuzzleIsLocalCostFreeAndPersistent(Data());}
        [Test] public void ArchiveAllChamberRoutes(){CoreChecks.ArchiveAllStagesHaveFootprintSafeRoutesAndAlcoves(Data());}
        [Test] public void ArchiveSealsRejectCombatMobilityBypass(){CoreChecks.ArchiveChamberSealsRejectDodgeAttackAndPowerBypass(Data());}
        [Test] public void ArchiveAllClassesAndStoryChoice(){CoreChecks.ArchiveGuardsAndStoryCompleteWithEveryStarterClass(Data());}
        [Test] public void ArchiveSavedDefenceCannotFarm(){CoreChecks.ArchiveGuardSnapshotsRetainHealthAndCannotFarm(Data());}
        [Test] public void ArchivePressureWarningsAndSafeAlcoves(){CoreChecks.ArchivePressureWarningsMatchDamageAndSafeAlcoves(Data());}
        [Test] public void ArchiveGuidanceTracksRealProgress(){CoreChecks.ArchiveGuidanceFollowsActualPuzzleAndDefenceProgress(Data());}
        [Test] public void ArchiveLocalActionsAndDeathRecovery(){CoreChecks.ArchiveLocalChoicesAndDeathRecoveryPreserveProgress(Data());}
        [Test] public void ArchiveMalformedActorOwnership(){CoreChecks.ArchiveReservedActorSnapshotsRespectOwnership(Data());}
        [Test] public void ArchiveLegacyMapCounterPrefixes(){CoreChecks.ArchiveLegacySavesGrowMapCountersWithoutInventingStory(Data());}
        [Test] public void RoadKitchenIngredientTransactions(){CoreChecks.RoadKitchenAtomicRecipesAndReplacement(Data());}
        [Test] public void RoadKitchenRealAccessAndMatureRecipes(){CoreChecks.RoadKitchenAccessAndMatureCooking(Data());}
        [Test] public void RoadMealWorldClockAndExistingSupplies(){CoreChecks.RoadMealsUseWorldClockAndStackWithExistingSupplies(Data());}
        [Test] public void RoadMealActualCombatAndClimateContacts(){CoreChecks.RoadMealArmorChangesRealCombatAndLeavesEnvironment(Data());}
        [Test] public void RoadMealWalkingDodgeSkillAndRace(){CoreChecks.RoadMealTravelMovementKeepsDashSkillAndRaceRules(Data());}
        [Test] public void RoadMealSavedProgressAndLegacyRecovery(){CoreChecks.RoadMealSaveMigrationSanitizationAndRecovery(Data());}
        [Test] public void SignalWardenStoryReadiness(){CoreChecks.SignalWardenReadinessAndOriginalStory(Data());}
        [Test] public void SignalWardenVictoryTransaction(){CoreChecks.SignalWardenVictoryRewardsAndStoryIsolation(Data());}
        [Test] public void SignalWardenSavedAttempts(){CoreChecks.SignalWardenSnapshotsTravelAndLegacyCompatibility(Data());}
        [Test] public void SignalWardenDeathRecovery(){CoreChecks.SignalWardenDeathAbandonsWithoutReward(Data());}
        [Test] public void SignalWardenLockedPatterns(){CoreChecks.SignalWardenThreePatternsKeepLockedTelegraphs(Data());}
        [Test] public void SignalWardenActualWarningContacts(){CoreChecks.SignalWardenWarningsMatchActualContacts(Data());}
        [Test] public void SignalWardenHitReactionWarningContinuity(){CoreChecks.SignalWardenHitReactionsPreserveAnnouncedRush(Data());}
        [Test] public void SignalWardenOffsetLaunchWalls(){CoreChecks.SignalWardenFanLaunchRespectsInterveningRoots(Data());}
        [Test] public void SignalWardenThreeClassVictory(){CoreChecks.SignalWardenAllClassesCanWinWithPublicActions(Data());}
        [Test] public void LastWatchFullQuest(){CoreChecks.LastWatchFullQuestAndOriginalStory(Data());}
        [Test] public void LastWatchChannelBoundaries(){CoreChecks.LastWatchClockPressureAndStageBoundaries(Data());}
        [Test] public void LastWatchSnapshotRecovery(){CoreChecks.LastWatchSnapshotsTravelAndDefeatRecords(Data());}
        [Test] public void LastWatchLegacySavesAndRoutes(){CoreChecks.LastWatchCompatibilityReadinessAndReachability(Data());}
        [Test] public void LastWatchLethalContactPriority(){CoreChecks.LastWatchDeathCannotCompleteOrPay(Data());}
        [Test] public void GatheringRequestsRestAndDelivery(){CoreChecks.GatheringRequestsGuideRestAndDelivery(Data());}
        [Test] public void FreshUnlockedResourceDestinations(){CoreChecks.ResourceGuidesChooseFreshUnlockedNodes(Data());}
        [Test] public void OreContractsGatherRestAndClaim(){CoreChecks.OreContractsGuideGatherRestAndClaim(Data());}
        [Test] public void MinePartsReturnToElevator(){CoreChecks.MinePartsGuidanceReturnsToElevator(Data());}
        [Test] public void FinalMovingDodgePresentation(){CoreChecks.FinalMovingDodgeOwnsPoseAndFacing(Data());}
        [Test] public void FinalDodgeSimulationBoundaries(){CoreChecks.FinalDodgeFreezesAndClearsAtBoundaries(Data());}
        [Test] public void FinalDodgeActionAndReactionTakeover(){CoreChecks.FinalDodgeYieldsToActionsAndReactions(Data());}
        [Test] public void ReleaseEventGovernsContactPoses(){CoreChecks.ReleasedContactsSelectVisibleActivePoses(Data());}
        [Test] public void RecoveryWalkingRestoresEquipment(){CoreChecks.WalkingSkillRecoveryRestoresEquippedAppearance(Data());}
        [Test] public void RecoveryReactionEquipmentIdentity(){CoreChecks.RecoveryReactionsRetainDisplayedEquipment(Data());}
        [Test] public void ScoutPresenceAndSavedInteractions(){CoreChecks.ScoutPresenceMatchesInteractionAndSavedJourney(Data());}
        [Test] public void HeldAttackPreservesQContact(){CoreChecks.HeldAttacksPreserveSkillContactPoses(Data());}
        [Test] public void BufferedQRetainsInputAndJoinsRecovery(){CoreChecks.BufferedSkillsRetainIntentAndJoinRecovery(Data());}
        [Test] public void BufferedQLifetimeAndResources(){CoreChecks.BufferedSkillsHaveBoundedLifetimeAndResourceChecks(Data());}
        [Test] public void BufferedQVariableFrameDeadlines(){CoreChecks.BufferedSkillsRespectVariableFrameDeadlines(Data());}
        [Test] public void BufferedQWorldAndProfileBoundaries(){CoreChecks.BufferedSkillsClearAtWorldAndProfileBoundaries(Data());}
        [Test] public void PreparedQCombatValues(){CoreChecks.SkillsRetainPreparedCombatValues(Data());}
        [Test] public void PersistentFrostIdentity(){CoreChecks.FrostRetainsCasterProfileAndSource(Data());}
        [Test] public void FrostScheduledContacts(){CoreChecks.FrostPulseClockIsFrameRateIndependent(Data());}
        [Test] public void PlayerLessonsRequirePlayerHits(){CoreChecks.CompanionDamageDoesNotCompletePlayerLessons(Data());}
        [Test] public void DeathCannotStartOrDescendExpedition(){CoreChecks.RecoveryGuardsExpeditionTransitions(Data());}
        [Test] public void ReadSaveIgnoresPreviousOpening(){CoreChecks.ReusedLoadsMatchFreshWorldState(Data());}
        [Test] public void MenuActionProfileCancelsBeforeMovement(){CoreChecks.ChangedActionProfilesCancelBeforeMovement(Data());}
        [Test] public void FlyingWeaponIdentity(){CoreChecks.FlyingShotsRetainReleaseProfile(Data());}
        [Test] public void LastPartialProjectileContact(){CoreChecks.ProjectileLastPartialFrameStillCollides(Data());}
        [Test] public void FrozenProjectileLifetime(){CoreChecks.ZeroDurationTicksPreserveLiveFlights(Data());}
        [Test] public void PracticeCannotInterruptRecovery(){CoreChecks.DeathCannotEnterPractice(Data());}
        [Test] public void MixedWorldStateStress(){CoreChecks.MixedStateTransitionsMaintainWorldInvariants(Data());}
        [Test] public void EncounterSpawnFootClearance(){CoreChecks.EveryEncounterSpawnHasValidFooting(Data());}
        [Test] public void TideGeometryBeforeContact(){CoreChecks.TideTransitionPrecedesAllAttackContacts(Data());}
        [Test] public void RegionalHazardRelease(){CoreChecks.RegionalHazardsKeepActualFootprint(Data());}
        [Test] public void DeathTransactionStopsTheFrame(){CoreChecks.LethalContactStopsRemainingSimulation(Data());}
        [Test] public void PracticePresentationTeardown(){CoreChecks.PracticeExitCancelsAllPendingPresentation(Data());}
        [Test] public void PursuitWorkspaceAndOwnedHudRoutes(){CoreChecks.SharedPathWorkspaceKeepsOwnedRoutes(Data());}
        [Test] public void JournalRefreshTriggers(){CoreChecks.JournalRecordsRefreshOnlyOnStateChanges(Data());}
        [Test] public void RecoveryAndFallenIdentity(){CoreChecks.StandingRecoveryAndCrossEquipmentDeath(Data());}
        [Test] public void EnemyFacingAndRetreat(){CoreChecks.StableEnemyFacingAndBackpedaling(Data());}
        [Test] public void ReactionFacing(){CoreChecks.RecoilRetainsInterruptedBodyDirection(Data());}
        [Test] public void MeleeWarningAndContact(){CoreChecks.MeleeWarningGeometryMatchesActualContact(Data());}
        [Test] public void MutationSafeCombat(){CoreChecks.CombatSnapshotsSurviveDeathsAndReinforcements(Data());}
        [Test] public void CapturedTargetAndExactMuzzle(){CoreChecks.CapturedTargetKeepsBodyAndMuzzleTogether(Data());}
        [Test] public void ThirtySecondDistanceDrivenGaits(){CoreChecks.DistanceDrivenThirtySecondWalking(Data());}
        [Test] public void SharedAttackMobilityAndDirections(){CoreChecks.UnifiedMobilityAndLockedDirections(Data());}
        [Test] public void AllArenaRoutesAndOpeningCollision(){CoreChecks.ArenaBoundariesAndOpeningTransactions(Data());}
        [Test] public void PauseHitStopAndEffectFinishing(){CoreChecks.SharedPauseAndEffectFinishingClock(Data());}
        [Test] public void ActualCompanionAndEnemyTravel(){CoreChecks.AlliesAndEnemiesUseActualTravel(Data());}
        [Test] public void QuietMaterialFootContacts(){CoreChecks.SoftMaterialFootContacts(Data());}
        [Test] public void MuzzleWallsAndCrossEquipmentTechniques(){CoreChecks.PointBlankWallsAndCrossEquipmentActions(Data());}
        [Test] public void SealedCacheRoutesAndCollision(){CoreChecks.ClosedCacheRoutesAndCollision(Data());}
        [Test] public void ActualWeaponTipsAndLaunchHeight(){CoreChecks.AuthoredMuzzlesAndContinuousLaunchHeight(Data());}
        [Test] public void InnDoorFurnitureRecoveryAndSaves() { CoreChecks.InnRoomAndCollisionPersistence(Data()); }
        [Test] public void ActualContactFramesAndCancellation() { CoreChecks.ContactFramesAndCancellation(Data()); }
        [Test] public void FlightAndEnemyStrikeFrames() { CoreChecks.ProjectilesAndEnemyContact(Data()); }
        [Test] public void DeathPosesAndSafeResume() { CoreChecks.DeathPresentationAndResume(Data()); }
        [Test] public void TutorialSaveMigrationAndPagedDialogue() { CoreChecks.TutorialAndPresentationMigration(Data()); }
        [Test] public void LiveQuestProgressAndTracking() { CoreChecks.QuestTrackingTransitions(Data()); }
        [Test] public void QuestRoutesRespectCollisionAndTides() { CoreChecks.NavigationRespectsWorldCollision(Data()); }
        [Test] public void SafePracticeAndReplay() { CoreChecks.InteractivePracticeAndReplay(Data()); }
        [Test] public void CorePowersOpenSecretRoutes() { CoreChecks.ExplorationPowersAndDiscoveries(Data()); }
        [Test] public void WeaponStylesAndEnemyCounters() { CoreChecks.CombatStylesAndEnemyCounters(Data()); }
        [Test] public void EncounterSnapshotsAndWorldResponse() { CoreChecks.WorldSnapshotsAndChoiceResponses(Data()); }
        [Test] public void MenuFocusFollowsGeometry() { CoreChecks.PredictableMenuFocus(Data()); }
        [Test] public void OriginalScoresAndFeedback() { CoreChecks.SoundFeedbackAndRegionalScores(Data()); }
        [Test] public void NativeSaveJsonRoundTrip()
        {
            var game = new GameCore(Data()); game.NewGame(2); game.Close(); game.save.weaponStyles[2] = 1; game.save.practiceBits = 7; game.RecordMoment("回家的路", "The road home"); game.PrepareSave();
            var saved = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(game.save)); var loaded = new GameCore(Data(), saved);
            Assert.AreEqual(2, loaded.save.profession); Assert.AreEqual(1, loaded.Style); Assert.AreEqual(7, loaded.save.practiceBits); Assert.AreEqual("回家的路", loaded.save.moments[0].zh);
        }
        private Campaign Data() { return JsonUtility.FromJson<Campaign>(Resources.Load<TextAsset>("Frontier/Campaign").text); }
        [Test] public void ClassesAndPausedMenus() { CoreChecks.ClassesAndPause(Data()); }
        [Test] public void CraftingAndCommissionTransactions() { CoreChecks.Transactions(Data()); }
        [Test] public void FishingFarmingAndCooking() { CoreChecks.DailyLife(Data()); }
        [Test] public void EveryMapAndQuestIsReachable() { CoreChecks.GatesAndGathering(Data()); }
        [Test] public void InvalidSavesAndBoundaryCollision() { CoreChecks.SaveValidation(Data()); }
        [Test] public void StandardDifficultyCampaignAndAllEndings() { CoreChecks.CompleteCampaign(Data()); }
        [Test] public void OldSaveMigrationAndBuildRules() { CoreChecks.MigrationAndBuilds(Data()); }
        [Test] public void AllNewMapsAndTidesAreReachable() { CoreChecks.DistantReachability(Data()); }
        [Test] public void AlternateSkillsRelicCostsAndDailyGifts() { CoreChecks.TechniquesAndTradeoffs(Data()); }
        [Test] public void ClimateContractsAndRaceRules() { CoreChecks.ClimateAndContracts(Data()); }
        [Test] public void AllShorePrioritiesAndCovenants() { CoreChecks.DistantCampaign(Data()); }
        [Test] public void SixFloorExpeditionAndBanking() { CoreChecks.AbyssExpeditions(Data()); }
    }
}
#endif
