#if UNITY_EDITOR
using NUnit.Framework;
using UnityEngine;

namespace Starfall.Tests
{
    public sealed class UnityCoreTests
    {
        [Test] public void VolleyPlansMatchActualEightRegionThreePhaseReleases(){CoreChecks.VolleyPlansMatchActualEightRegionThreePhaseReleases(Data());}
        [Test] public void VolleyPreparedThresholdsCyclesCancellationAndSpecialActorsStayHonest(){CoreChecks.VolleyPreparedThresholdsCyclesCancellationAndSpecialActorsStayHonest(Data());}
        [Test] public void VolleyTerrainClipsToActualOrbClearanceWithoutMutatingSimulation(){CoreChecks.VolleyTerrainClipsToActualOrbClearanceWithoutMutatingSimulation(Data());}
        [Test] public void VolleyQueriesKeepLegacySavesExpansionHazardsAndCapturedPlayerActions(){CoreChecks.VolleyQueriesKeepLegacySavesExpansionHazardsAndCapturedPlayerActions(Data());}
        [Test] public void SmithQuotesExposeRecipesBenefitsAndReadOnlySnapshots(){CoreChecks.SmithQuotesExposeRecipesBenefitsAndReadOnlySnapshots(Data());}
        [Test] public void SmithTransactionsKeepFiveUpgradeTiersAndFreeOwnedEquipment(){CoreChecks.SmithTransactionsKeepFiveUpgradeTiersAndFreeOwnedEquipment(Data());}
        [Test] public void SmithChoicesRecheckLiveMaterialsAndKeepRejectedDialogue(){CoreChecks.SmithChoicesRecheckLiveMaterialsAndKeepRejectedDialogue(Data());}
        [Test] public void SmithQuotesPreserveLegacyProgressAndCapturedCombat(){CoreChecks.SmithQuotesPreserveLegacyProgressAndCapturedCombat(Data());}
        [Test] public void TalentResetQuotesExposeExactThreeClassCostsRefundsAndHealthWithoutMutation(){CoreChecks.TalentResetQuotesExposeExactThreeClassCostsRefundsAndHealthWithoutMutation(Data());}
        [Test] public void TalentResetTransactionsPayOnceKeepOriginalWrapperAndTrainingAccess(){CoreChecks.TalentResetTransactionsPayOnceKeepOriginalWrapperAndTrainingAccess(Data());}
        [Test] public void TalentResetRejectsStaleOwnersDataWorldsAndInvalidRefundsAtomically(){CoreChecks.TalentResetRejectsStaleOwnersDataWorldsAndInvalidRefundsAtomically(Data());}
        [Test] public void TalentResetRetainsLegacyProgressAndCapturedCombatActions()
        {
            var data = Data(); CoreChecks.TalentResetRetainsLegacyProgressAndCapturedCombatActions(data);
            foreach (int version in new[] { 1, 2 })
            {
                var old = JsonUtility.FromJson<SaveData>("{\"version\":" + version + ",\"profession\":0,\"upgrades\":2,\"coins\":73,\"skillPoints\":8,\"health\":42,\"talents\":[2,3,1],\"flags\":[\"intro\",\"core_1\",\"technique_0\",\"relic_1\",\"relic_4\"],\"technique\":1,\"weaponStyles\":[2,1,2],\"mastery\":[23,64,87],\"relics\":[1,4],\"relicRank\":2}");
                var game = new GameCore(data, old); game.Start(); Assert.IsTrue(game.TryResetTalents(game.GetTalentResetQuote()));
                game.PrepareSave(); string json = JsonUtility.ToJson(game.save);
                Assert.IsFalse(json.Contains("TalentResetQuote")); Assert.IsFalse(json.Contains("pointsBefore")); Assert.IsFalse(json.Contains("saveOwner"));
                var resumed = new GameCore(data, JsonUtility.FromJson<SaveData>(json)); resumed.Start();
                Assert.AreEqual(53, resumed.save.coins); Assert.AreEqual(14, resumed.save.skillPoints);
                Assert.AreEqual(160, resumed.save.maxHealth); Assert.AreEqual(42, resumed.save.health);
                Assert.AreEqual(new[] { 0, 0, 0 }, resumed.save.talents); Assert.AreEqual(1, resumed.save.technique);
                Assert.AreEqual(new[] { 2, 1, 2 }, resumed.save.weaponStyles); Assert.AreEqual(new[] { 23, 64, 87 }, resumed.save.mastery);
                // Preserve the existing v1 migration's empty equipment slots;
                // relic ownership remains independently earned in both versions.
                Assert.AreEqual(version == 1 ? new[] { -1, -1 } : new[] { 1, 4 }, resumed.save.relics);
                Assert.IsTrue(resumed.Has("relic_1")); Assert.IsTrue(resumed.Has("relic_4")); Assert.AreEqual(2, resumed.save.relicRank);
            }
        }
        [Test] public void CounterHitEarnedSwordAppearsOnlyAtRealContactAndExpires(){CoreChecks.CounterHitEarnedSwordAppearsOnlyAtRealContactAndExpires(Data());}
        [Test] public void CounterHitPreparedBowAndStaffRetainCapturedCounterAcrossFlightAndGear(){CoreChecks.CounterHitPreparedBowAndStaffRetainCapturedCounterAcrossFlightAndGear(Data());}
        [Test] public void CounterHitMissesSkillsCompanionsDummyAndZeroLossStayUnmarked(){CoreChecks.CounterHitMissesSkillsCompanionsDummyAndZeroLossStayUnmarked(Data());}
        [Test] public void CounterHitPreservesArmorCriticalLethalRewardsAndLegacySaveState(){CoreChecks.CounterHitPreservesArmorCriticalLethalRewardsAndLegacySaveState(Data());}
        [Test] public void EnemyReturnWalksAuthoredWaterDetoursAtTwentyAndSixtyFrames(){CoreChecks.EnemyReturnWalksAuthoredWaterDetoursAtTwentyAndSixtyFrames(Data());}
        [Test] public void EnemyReturnRetainsAcquisitionCommittedAttacksAndReactionPriority(){CoreChecks.EnemyReturnRetainsAcquisitionCommittedAttacksAndReactionPriority(Data());}
        [Test] public void EnemyReturnKeepsOldSnapshotsDamageAndInstanceOwnership(){CoreChecks.EnemyReturnKeepsOldSnapshotsDamageAndInstanceOwnership(Data());}
        [Test] public void EnemyReturnPreservesSpecialEncountersTrainingAndBlockedHomes(){CoreChecks.EnemyReturnPreservesSpecialEncountersTrainingAndBlockedHomes(Data());}
        [Test] public void EnemyPursuitCrossesShippedWaterDetourAtTwentyAndSixtyFrames(){CoreChecks.EnemyPursuitCrossesShippedWaterDetourAtTwentyAndSixtyFrames(Data());}
        [Test] public void EnemyPursuitUsesExactAcquisitionRetentionAndOriginalTrainingGates(){CoreChecks.EnemyPursuitUsesExactAcquisitionRetentionAndOriginalTrainingGates(Data());}
        [Test] public void EnemyPursuitPreservesCommittedWarningsReleasesStunAndRecoil(){CoreChecks.EnemyPursuitPreservesCommittedWarningsReleasesStunAndRecoil(Data());}
        [Test] public void EnemyPursuitKeepsAlertsTransientAcrossSavesActorsTravelAndDiagnostics()
        {
            var data = Data(); CoreChecks.EnemyPursuitKeepsAlertsTransientAcrossSavesActorsTravelAndDiagnostics(data);
            foreach (int version in new[] { 1, 2 })
            {
                var old = JsonUtility.FromJson<SaveData>("{\"version\":" + version + ",\"room\":1,\"stage\":1,\"coins\":91,\"x\":21,\"y\":14,\"snapshotRoom\":1,\"flags\":[\"intro\"],\"worldEnemies\":[{\"id\":98701,\"kind\":0,\"x\":12,\"y\":14,\"hp\":31,\"maxHp\":43}]}");
                var game = new GameCore(data, old); game.Start(); var enemy = game.enemies.Find(e => e.id == 98701);
                Assert.NotNull(enemy); game.Tick(.02f, new Point()); Assert.AreEqual(12, enemy.x); Assert.AreEqual(31, enemy.hp);
                game.save.x = 20; game.Tick(.02f, new Point()); Assert.Greater(enemy.x, 12);
                game.save.x = enemy.x + 9; float acquiredX = enemy.x; game.Tick(.02f, new Point()); Assert.Greater(enemy.x, acquiredX);
                game.PrepareSave(); string json = JsonUtility.ToJson(game.save);
                Assert.IsFalse(json.Contains("pursuit")); Assert.IsFalse(json.Contains("alert"));
                var resumed = new GameCore(data, JsonUtility.FromJson<SaveData>(json)); resumed.Start();
                var restored = resumed.enemies.Find(e => e.id == 98701); Assert.NotNull(restored);
                Assert.AreEqual(enemy.x, restored.x); Assert.AreEqual(enemy.y, restored.y); Assert.AreEqual(31, restored.hp); Assert.AreEqual(91, resumed.save.coins);
                float restoredTimer = restored.timer; resumed.Tick(.02f, new Point());
                Assert.AreEqual(enemy.x, restored.x); Assert.AreEqual(enemy.y, restored.y); Assert.AreEqual(restoredTimer, restored.timer);
            }
        }
        [Test] public void ArcherEvasionEscapesMirroredWallsAndRejectsClosingCornerSlides(){CoreChecks.ArcherEvasionEscapesMirroredWallsAndRejectsClosingCornerSlides(Data());}
        [Test] public void ArcherEvasionPreservesStraightRetreatAndExistingPerception(){CoreChecks.ArcherEvasionPreservesStraightRetreatAndExistingPerception(Data());}
        [Test] public void ArcherEvasionKeepsCommittedShotsAndTrainingExclusions(){CoreChecks.ArcherEvasionKeepsCommittedShotsAndTrainingExclusions(Data());}
        [Test] public void ArcherEvasionPersistsOnlyExistingCoordinatesAndLoadsBothSaveVersions()
        {
            var data = Data(); CoreChecks.ArcherEvasionPersistsOnlyExistingCoordinatesAndLoadsBothSaveVersions(data);
            foreach(int version in new[]{1,2})
            {
                var old = JsonUtility.FromJson<SaveData>("{\"version\":" + version + ",\"room\":1,\"stage\":1,\"coins\":91,\"x\":13.225,\"y\":9.5,\"snapshotRoom\":1,\"flags\":[\"intro\"],\"worldEnemies\":[{\"id\":98601,\"kind\":1,\"x\":11.225,\"y\":9.5,\"hp\":31,\"maxHp\":43}]}");
                var loaded = new GameCore(data, old); loaded.Start(); var archer = loaded.enemies.Find(e => e.id == 98601);
                Assert.NotNull(archer); Assert.AreEqual(11.225f, archer.x); Assert.AreEqual(9.5f, archer.y); Assert.AreEqual(31, archer.hp);
                Assert.AreEqual(91, loaded.save.coins); Assert.IsFalse(archer.backpedaling); Assert.AreEqual(0, archer.state);
                loaded.PrepareSave(); var resumed = new GameCore(data, JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(loaded.save)));
                archer = resumed.enemies.Find(e => e.id == 98601); Assert.NotNull(archer); Assert.AreEqual(11.225f, archer.x); Assert.AreEqual(9.5f, archer.y);
            }
        }
        [Test] public void SupplyPreparationQuotesAreImmutablePureAndExposeExactSharedCosts(){CoreChecks.SupplyPreparationQuotesAreImmutablePureAndExposeExactSharedCosts(Data());}
        [Test] public void SupplyPreparationTransactionsPayOncePreserveBuffsAndRespectOutputCaps(){CoreChecks.SupplyPreparationTransactionsPayOncePreserveBuffsAndRespectOutputCaps(Data());}
        [Test] public void SupplyPreparationRejectsStaleInvalidAndUnauthorizedContextsAtomically(){CoreChecks.SupplyPreparationRejectsStaleInvalidAndUnauthorizedContextsAtomically(Data());}
        [Test] public void SupplyPreparationRealInteractionKeepsOriginalChoicesAndRechecksDisabledSelections(){CoreChecks.SupplyPreparationRealInteractionKeepsOriginalChoicesAndRechecksDisabledSelections(Data());}
        [Test] public void SupplyPreparationPersistenceAndLegacyRestShopSingleRecipesRemainIntact(){CoreChecks.SupplyPreparationPersistenceAndLegacyRestShopSingleRecipesRemainIntact(Data());}

        [Test] public void InteractionRouteGlassSealEndsAtItsOwnRealEApproach(){CoreChecks.InteractionRouteGlassSealEndsAtItsOwnRealEApproach(Data());}
        [Test] public void InteractionRouteAuthoredObjectsNeverAdvertiseAnotherObjectsE(){CoreChecks.InteractionRouteAuthoredObjectsNeverAdvertiseAnotherObjectsE(Data());}
        [Test] public void InteractionRouteRejectsStaleReferencesAndIncludesNeededStartCenterReadOnly(){CoreChecks.InteractionRouteRejectsStaleReferencesAndIncludesNeededStartCenterReadOnly(Data());}
        [Test] public void InteractionRouteUsesLiveRootsBridgeAndTideWithoutOpeningThem(){CoreChecks.InteractionRouteUsesLiveRootsBridgeAndTideWithoutOpeningThem(Data());}
        [Test] public void InteractionRouteGuidanceKeepsIdentityAndCoordinateNavigationOwnership(){CoreChecks.InteractionRouteGuidanceKeepsIdentityAndCoordinateNavigationOwnership(Data());}

        [Test] public void CompanionSupplyIvoTracksEitherShortfallAndClampsBothCounters(){CoreChecks.CompanionSupplyIvoTracksEitherShortfallAndClampsBothCounters(Data());}
        [Test] public void CompanionSupplySenFindsTheForestRecordBeforeGatheringHerbs(){CoreChecks.CompanionSupplySenFindsTheForestRecordBeforeGatheringHerbs(Data());}
        [Test] public void CompanionSupplyActualGatherRecordAndOriginalPaymentsRemainConnected(){CoreChecks.CompanionSupplyActualGatherRecordAndOriginalPaymentsRemainConnected(Data());}
        [Test] public void CompanionSupplyDepletedSourcesLeadToExistingRestAndRenewOnlyOnRest(){CoreChecks.CompanionSupplyDepletedSourcesLeadToExistingRestAndRenewOnlyOnRest(Data());}
        [Test] public void CompanionSupplyPreservesLockedCompletedMiraPinsAndLegacyState(){CoreChecks.CompanionSupplyPreservesLockedCompletedMiraPinsAndLegacyState(Data());}

        [Test] public void HealingFoodReportsActualRecoveryAndKeepsExistingBuffTransactions(){CoreChecks.HealingFoodReportsActualRecoveryAndKeepsExistingBuffTransactions(Data());}
        [Test] public void HealingSkillRelicWaitsForThePaidCastAndNeverTheQueuedPress(){CoreChecks.HealingSkillRelicWaitsForThePaidCastAndNeverTheQueuedPress(Data());}
        [Test] public void HealingDodgeRelicLabelsOnlyOneAcceptedImmediateOrBufferedStart(){CoreChecks.HealingDodgeRelicLabelsOnlyOneAcceptedImmediateOrBufferedStart(Data());}
        [Test] public void HealingMiraCommandKeepsTheOriginalRingAndLabelsItsRealContactDelta(){CoreChecks.HealingMiraCommandKeepsTheOriginalRingAndLabelsItsRealContactDelta(Data());}
        [Test] public void HealingMiraAutomaticRecoveryUsesActualContactAndExpiresWithoutPhantomLabels(){CoreChecks.HealingMiraAutomaticRecoveryUsesActualContactAndExpiresWithoutPhantomLabels(Data());}

        [Test] public void ForgeQuotesExposeExactCostsAndImmutableSnapshots(){CoreChecks.ForgeQuotesExposeExactCostsAndImmutableSnapshots(Data());}
        [Test] public void ForgeShortfallsReportEveryMissingResourceWithoutMutation(){CoreChecks.ForgeShortfallsReportEveryMissingResourceWithoutMutation(Data());}
        [Test] public void ForgeTransactionsRecheckStaleQuotesBeforeAnySpend(){CoreChecks.ForgeTransactionsRecheckStaleQuotesBeforeAnySpend(Data());}
        [Test] public void ForgeSuccessfulUpgradesKeepExactTransactionsAndExistingEntryModes(){CoreChecks.ForgeSuccessfulUpgradesKeepExactTransactionsAndExistingEntryModes(Data());}
        [Test] public void ForgeLegacySavesPreserveRanksAndDamageRequiresAnEquippedRelic(){CoreChecks.ForgeLegacySavesPreserveRanksAndDamageRequiresAnEquippedRelic(Data());}

        [Test] public void DodgeLatePressCapturesDirectionAndExecutesOnceAtRealCooldown(){CoreChecks.DodgeLatePressCapturesDirectionAndExecutesOnceAtRealCooldown(Data());}
        [Test] public void DodgeRejectedRequestsNeverWaitForEnergyOrAnUnrelatedCooldown(){CoreChecks.DodgeRejectedRequestsNeverWaitForEnergyOrAnUnrelatedCooldown(Data());}
        [Test] public void DodgeAndSkillHonorTheLastValidDiscreteIntent(){CoreChecks.DodgeAndSkillHonorTheLastValidDiscreteIntent(Data());}
        [Test] public void DodgeIntentCannotSurviveMenusTravelDeathOrTrainingBoundaries(){CoreChecks.DodgeIntentCannotSurviveMenusTravelDeathOrTrainingBoundaries(Data());}
        [Test] public void DodgeIntentRevalidatesGearProfilesAndCurrentExecutionCost(){CoreChecks.DodgeIntentRevalidatesGearProfilesAndCurrentExecutionCost(Data());}
        [Test] public void DodgeBufferPreservesImmediateCancellationHitSafetyAndWalls(){CoreChecks.DodgeBufferPreservesImmediateCancellationHitSafetyAndWalls(Data());}

        [Test] public void RangeMenusPreserveMatureAdaChoicesAndRequireEarnedLocalControl(){CoreChecks.RangeMenusPreserveMatureAdaChoicesAndRequireEarnedLocalControl(Data());}
        [Test] public void RangeThreeClassesAndLayoutsUseRealContactsAndClearWithoutRewards(){CoreChecks.RangeThreeClassesAndLayoutsUseRealContactsAndClearWithoutRewards(Data());}
        [Test] public void RangeArmorFlanksBreaksAndStunKeepActualSimulation(){CoreChecks.RangeArmorFlanksBreaksAndStunKeepActualSimulation(Data());}
        [Test] public void RangeDamageCountsOnlyAcceptedPlayerOwnedPositiveHealthLoss(){CoreChecks.RangeDamageCountsOnlyAcceptedPlayerOwnedPositiveHealthLoss(Data());}
        [Test] public void RangeAcceptedActionTimingMenusAndSafeVitalsStayIsolated(){CoreChecks.RangeAcceptedActionTimingMenusAndSafeVitalsStayIsolated(Data());}
        [Test] public void RangeManualResetsSeverPreparedMissilesAndPersistentFrost(){CoreChecks.RangeManualResetsSeverPreparedMissilesAndPersistentFrost(Data());}
        [Test] public void RangeSaveRecoveryAndReservedActorsKeepCampaignAndAbyssBoundaries(){CoreChecks.RangeSaveRecoveryAndReservedActorsKeepCampaignAndAbyssBoundaries(Data());}

        [Test] public void LandmarksExploredPresentOrderedObjects(){CoreChecks.LandmarksProjectOnlyExploredPresentNamedNonresourceObjectsInAuthoredOrder(Data());}
        [Test] public void LandmarksCanonicalCallbacksAndModes(){CoreChecks.LandmarksRejectCopiedForeignMalformedAndStalePinCallbacks(Data());}
        [Test] public void LandmarksUnambiguousMetadata(){CoreChecks.LandmarksValidateCanonicalMetadataAndUnambiguousSavedIds(Data());}
        [Test] public void LandmarksExistingNavigationOwnership(){CoreChecks.LandmarksKeepQuestTutorialPracticeAndAbyssNavigationOwnership(Data());}
        [Test] public void LandmarksTravelDeathLegacyAndNewJourneys(){CoreChecks.LandmarksPersistThroughTravelDefeatLegacyLoadsAndResetForNewJourneys(Data());}
        [Test] public void LandmarksNamespaceAndStoryPresence(){CoreChecks.LandmarksNormalizeOnlyTheirNamespaceAndSuppressHiddenStoryObjectsReadOnly(Data());}
        [Test] public void LandmarksLiveBridgeRootsAndTide(){CoreChecks.LandmarksUseLiveBridgeRootAndTidePathsWithoutOpeningGates(Data());}
        [Test] public void LandmarksReadOnlySimulationIsolation(){CoreChecks.LandmarksReadQueriesAndRoutesNeverAwardInteractOrAdvanceWorldState(Data());}
        [Test] public void HeavyArmorEightHeadingsMatchExactContactSector(){CoreChecks.HeavyArmorEightHeadingsMatchExactContactSector(Data());}
        [Test] public void HeavyArmorRolesStatesAndTechniquesKeepMatureDamage(){CoreChecks.HeavyArmorRolesStatesAndTechniquesKeepMatureDamage(Data());}
        [Test] public void HeavyArmorRealWindupReleaseAndBreakRestoreEligibility(){CoreChecks.HeavyArmorRealWindupReleaseAndBreakRestoreEligibility(Data());}
        [Test] public void HeavyArmorSafeDummySparringAndDefeatStayDistinct(){CoreChecks.HeavyArmorSafeDummySparringAndDefeatStayDistinct(Data());}
        [Test] public void HeroFullBodyContactsFollowRealAttackAndTechnique(){CoreChecks.HeroFullBodyContactsFollowRealAttackAndTechnique(Data());}
        [Test] public void HeroFullBodyRecoveryAndReactionsKeepActualEquipment(){CoreChecks.HeroFullBodyRecoveryAndReactionsKeepActualEquipment(Data());}
        [Test] public void HeroFullBodyLastDodgeAndDeathKeepCapturedFacing(){CoreChecks.HeroFullBodyLastDodgeAndDeathKeepCapturedFacing(Data());}
        [Test] public void HeroFullBodySelectionPreservesPauseHitStopAndSaveState(){CoreChecks.HeroFullBodySelectionPreservesPauseHitStopAndSaveState(Data());}
        [Test] public void FieldcraftAcceptanceKeepsAdaPracticeAndRequiresEarnedLocalWorld(){CoreChecks.FieldcraftAcceptanceKeepsAdaPracticeAndRequiresEarnedLocalWorld(Data());}
        [Test] public void FieldcraftAllThreeProfessionsEarnActualSwordBowStaffAndReport(){CoreChecks.FieldcraftAllThreeProfessionsEarnActualSwordBowStaffAndReport(Data());}
        [Test] public void FieldcraftBowRequiresOneFocusedArrowAndCapturedGear(){CoreChecks.FieldcraftBowRequiresOneFocusedArrowAndCapturedGear(Data());}
        [Test] public void FieldcraftStaffCountsDistinctLiveBlastContactsIncludingLethal(){CoreChecks.FieldcraftStaffCountsDistinctLiveBlastContactsIncludingLethal(Data());}
        [Test] public void FieldcraftOnlyRegisteredPostAcceptanceBasicsCount(){CoreChecks.FieldcraftOnlyRegisteredPostAcceptanceBasicsCount(Data());}
        [Test] public void FieldcraftConcurrentActionsKeepIndependentOwnershipAndSafeRoomsStayExcluded(){CoreChecks.FieldcraftConcurrentActionsKeepIndependentOwnershipAndSafeRoomsStayExcluded(Data());}
        [Test] public void FieldcraftWhiffsCanceledWindupsPauseAndExpiredContactsStayUnpaid(){CoreChecks.FieldcraftWhiffsCanceledWindupsPauseAndExpiredContactsStayUnpaid(Data());}
        [Test] public void FieldcraftPartialContactsResetAcrossWorldBoundariesButNotesPersist(){CoreChecks.FieldcraftPartialContactsResetAcrossWorldBoundariesButNotesPersist(Data());}
        [Test] public void FieldcraftReportingRechecksLocalSafetyModesAndPaysExactlyOnce(){CoreChecks.FieldcraftReportingRechecksLocalSafetyModesAndPaysExactlyOnce(Data());}
        [Test] public void FieldcraftJournalLegacyAndQuestPrefixRemainCompatible(){CoreChecks.FieldcraftJournalLegacyAndQuestPrefixRemainCompatible(Data());}
        [Test] public void ShelterAcceptanceRetainsKeeperPersonalStoryAndExplicitCompanion(){CoreChecks.ShelterAcceptanceRetainsKeeperPersonalStoryAndExplicitCompanion(Data());}
        [Test] public void ShelterLocalActionsRequireLitBraziersAndOrderedRealMarks(){CoreChecks.ShelterLocalActionsRequireLitBraziersAndOrderedRealMarks(Data());}
        [Test] public void ShelterAllStartersWalkThreeRealLegsAndEarnAChosenRoad(){CoreChecks.ShelterAllStartersWalkThreeRealLegsAndEarnAChosenRoad(Data());}
        [Test] public void ShelterWindWarningsCaptureAlternatingClippedFootprintsAndContacts(){CoreChecks.ShelterWindWarningsCaptureAlternatingClippedFootprintsAndContacts(Data());}
        [Test] public void ShelterWarningPauseDashAndLitSheltersKeepTheirExistingProtection(){CoreChecks.ShelterWarningPauseDashAndLitSheltersKeepTheirExistingProtection(Data());}
        [Test] public void ShelterReportBothChoicesIsLocalCappedOnceAndCannotReplaceMountainStory(){CoreChecks.ShelterReportBothChoicesIsLocalCappedOnceAndCannotReplaceMountainStory(Data());}
        [Test] public void ShelterMaintainedRoadPreventsOnlyItsOwnColdGrowthAndRetainsTeaAndBraziers(){CoreChecks.ShelterMaintainedRoadPreventsOnlyItsOwnColdGrowthAndRetainsTeaAndBraziers(Data());}
        [Test] public void ShelterCheckpointSavesRetainEarnedMarksWithoutReplayingTransientWeather(){CoreChecks.ShelterCheckpointSavesRetainEarnedMarksWithoutReplayingTransientWeather(Data());}
        [Test] public void ShelterAttemptsCancelOnTravelDefeatPracticeAndExplicitStop(){CoreChecks.ShelterAttemptsCancelOnTravelDefeatPracticeAndExplicitStop(Data());}
        [Test] public void ShelterJournalAppendsAfterAllMatureIdsAndOwnsReachableProgress(){CoreChecks.ShelterJournalAppendsAfterAllMatureIdsAndOwnsReachableProgress(Data());}
        [Test] public void CommissionAcceptanceRequiresEarnedActualEngineerAndPreservesPersonalRepair(){CoreChecks.CommissionAcceptanceRequiresEarnedActualEngineerAndPreservesPersonalRepair(Data());}
        [Test] public void CommissionStartsAndVentRevalidateLocalWindows(){CoreChecks.CommissionStartsAndVentRevalidateLocalWindows(Data());}
        [Test] public void CommissionPreparedFootprintsKeepTimingAcrossRealThresholdHits(){CoreChecks.CommissionPreparedFootprintsKeepTimingAcrossRealThresholdHits(Data());}
        [Test] public void CommissionWarningsMatchBeamCircleAndUnventedChargeContacts(){CoreChecks.CommissionWarningsMatchBeamCircleAndUnventedChargeContacts(Data());}
        [Test] public void CommissionEveryStarterWinsActualTrialAndEarnsFirstWater(){CoreChecks.CommissionEveryStarterWinsActualTrialAndEarnsFirstWater(Data());}
        [Test] public void CommissionReportIsLocalOnceAndKeepsMatureDesertChoices(){CoreChecks.CommissionReportIsLocalOnceAndKeepsMatureDesertChoices(Data());}
        [Test] public void CommissionRealHitSnapshotsKeepCanonicalHealthAndFreshWarnings(){CoreChecks.CommissionRealHitSnapshotsKeepCanonicalHealthAndFreshWarnings(Data());}
        [Test] public void CommissionTravelAndActualDefeatRequireDeliberateUnpaidRetries(){CoreChecks.CommissionTravelAndActualDefeatRequireDeliberateUnpaidRetries(Data());}
        [Test] public void CommissionDailyWaterStaysChosenLocalBoundedAndIndependentOfRest(){CoreChecks.CommissionDailyWaterStaysChosenLocalBoundedAndIndependentOfRest(Data());}
        [Test] public void CommissionJournalPrefixLegacyAndForeignSnapshotBoundaries(){CoreChecks.CommissionJournalPrefixLegacyAndForeignSnapshotBoundaries(Data());}
        [Test] public void BattlePlansOwnedThreeSlotCopies(){CoreChecks.BattlePlansCaptureThreeOwnedBuildsAndReturnCopies(Data());}
        [Test] public void BattlePlansAtomicOwnedEquipment(){CoreChecks.BattlePlansApplyOnlySelectedWeaponStanceAndOwnedChoices(Data());}
        [Test] public void BattlePlansOwnershipRevalidation(){CoreChecks.BattlePlansRecheckAllUnlocksBeforeAtomicApply(Data());}
        [Test] public void BattlePlansModesDefeatAndSlotBoundaries(){CoreChecks.BattlePlanTransactionsRequireLivePreparationAndValidSlots(Data());}
        [Test] public void BattlePlansPreservePaidProgressAndVitals(){CoreChecks.BattlePlansPreservePaidGrowthStoryEconomyAndTransientVitals(Data());}
        [Test] public void BattlePlansLegacyAndMalformedRecords(){CoreChecks.BattlePlansNormalizeLegacyRecordsWithoutInventingUnlocks(Data());}
        [Test] public void BattlePlansPreparedContactCancellation(){CoreChecks.BattlePlansChangedApplyCancelsPreparationWithoutRefundingContacts(Data());}
        [Test] public void BattlePlansLaunchedArrowAndFrostContacts(){CoreChecks.BattlePlansRetainLaunchedMissilesAndPersistentFrostProfiles(Data());}
        [Test] public void BattlePlansNativeJsonDefaultsAndRoundTrip()
        {
            foreach(int version in new[]{1,2})
            {
                var old = JsonUtility.FromJson<SaveData>("{\"version\":" + version + ",\"coins\":87}");
                var loaded = new GameCore(Data(),old);
                Assert.AreEqual(3,loaded.save.battlePlans.Length);
                for(int slot=0;slot<3;slot++) Assert.IsTrue(loaded.BattlePlanAt(slot)==null);
                Assert.AreEqual(87,loaded.save.coins);
            }
            var ghost = JsonUtility.FromJson<BattlePlan>("{}");
            Assert.IsFalse(ghost.recorded);
            var ghostLoaded = new GameCore(Data(),new SaveData { battlePlans=new[]{ghost} });
            Assert.IsTrue(ghostLoaded.BattlePlanAt(0)==null);
            Assert.IsFalse(ghostLoaded.BattlePlanAvailable(0));
            var game = new GameCore(Data()); game.NewGame(2); game.Close(); game.mode="plans";
            Assert.IsTrue(game.SaveBattlePlan(2)); game.PrepareSave();
            var resumed = new GameCore(Data(),JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(game.save)));
            Assert.IsTrue(resumed.BattlePlanAt(0)==null); Assert.IsTrue(resumed.BattlePlanAt(1)==null);
            Assert.NotNull(resumed.BattlePlanAt(2)); Assert.IsTrue(resumed.BattlePlanIsCurrent(2));
            Assert.IsTrue(resumed.BattlePlanAt(2).recorded);
            Assert.AreEqual(2,resumed.BattlePlanAt(2).weapon);
            Assert.AreEqual(-1,resumed.BattlePlanAt(2).relicFirst); Assert.AreEqual(-1,resumed.BattlePlanAt(2).relicSecond);
        }
        [Test] public void HomewardEarnedOfferAndCompanionChoice(){CoreChecks.HomewardEarnedAcceptanceKeepsMaturePersonalStoryAndSelection(Data());}
        [Test] public void HomewardLocalOrderedMiraWitnesses(){CoreChecks.HomewardLocalStartsRequireOrderedStopsAndActualMira(Data());}
        [Test] public void HomewardActualDefeatsAndManualRecords(){CoreChecks.HomewardObservationsRequireRealDefeatsAndWitnessedHandoff(Data());}
        [Test] public void HomewardThreeStarterJourneys(){CoreChecks.HomewardEveryStarterWalksWithMiraAndWinsOrdinaryEncounters(Data());}
        [Test] public void HomewardCanonicalCombatSnapshotOwnership(){CoreChecks.HomewardSnapshotsKeepCanonicalSurvivorsAndRejectForeignRecords(Data());}
        [Test] public void HomewardTravelAndRealDeathRecovery(){CoreChecks.HomewardTravelAndActualDefeatKeepUnresolvedOwnership(Data());}
        [Test] public void HomewardOldSaveAndForeignWorldIsolation(){CoreChecks.HomewardLegacySavesAndForeignWorldsPreserveMatureState(Data());}
        [Test] public void HomewardJournalCampAndSavedFinalChoices(){CoreChecks.HomewardGuidanceAndFinalChoicesStayLocalPersistentAndOnce(Data());}
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
        [Test] public void RootEchoPreservesSixSeededFloorsAndCanonicalEncounters() { CoreChecks.RootEchoPreservesSixSeededFloorsAndCanonicalEncounters(Data()); }
        [Test] public void RootEchoRequiresEarnedClearanceAndRechecksPhysicalChoices() { CoreChecks.RootEchoRequiresEarnedClearanceAndRechecksPhysicalChoices(Data()); }
        [Test] public void RootEchoThreeWarningsKeepTheirCapturedReadableGeometry() { CoreChecks.RootEchoThreeWarningsKeepTheirCapturedReadableGeometry(Data()); }
        [Test] public void RootEchoThreeClassesAndRisksEarnUnbankedRewardsThroughRealMovement() { CoreChecks.RootEchoThreeClassesAndRisksEarnUnbankedRewardsThroughRealMovement(Data()); }
        [Test] public void RootEchoRestReloadAndFreshRunNeverDuplicateResolution() { CoreChecks.RootEchoRestReloadAndFreshRunNeverDuplicateResolution(Data()); }
        [Test] public void RootEchoCancellationPauseAndBankGuardsRemainTransactional() { CoreChecks.RootEchoCancellationPauseAndBankGuardsRemainTransactional(Data()); }
        [Test] public void RootEchoActualWarningContactAndDodgeKeepCombatRules() { CoreChecks.RootEchoActualWarningContactAndDodgeKeepCombatRules(Data()); }
        [Test] public void SupperEarnedOfferPreservesOriginalCommissionsAndMenus() { CoreChecks.SupperEarnedOfferPreservesOriginalCommissionsAndMenus(Data()); }
        [Test] public void SupperAcceptanceAndPreparationRecheckPhysicalCallbacks() { CoreChecks.SupperAcceptanceAndPreparationRecheckPhysicalCallbacks(Data()); }
        [Test] public void SupperCookingServingAndFinishRequireActualOwnedStations() { CoreChecks.SupperCookingServingAndFinishRequireActualOwnedStations(Data()); }
        [Test] public void SupperAllProfessionsAndPreparationServiceOrdersFinishOnce() { CoreChecks.SupperAllProfessionsAndPreparationServiceOrdersFinishOnce(Data()); }
        [Test] public void SupperEveryCheckpointAndLegacyVersionReloadKeepsEarnedProgress() { CoreChecks.SupperEveryCheckpointAndLegacyVersionReloadKeepsEarnedProgress(Data()); }
        [Test] public void SupperGuidanceAppendsReadOnlyAndTargetsPhysicalMissingSteps() { CoreChecks.SupperGuidanceAppendsReadOnlyAndTargetsPhysicalMissingSteps(Data()); }
        [Test] public void SupperLeavesMatureKitchenRestAndTownLifeAvailable() { CoreChecks.SupperLeavesMatureKitchenRestAndTownLifeAvailable(Data()); }
        [Test] public void CompanionTacticNotesAreReadOnlyAndShareActualFormulas() { CoreChecks.CompanionTacticNotesAreReadOnlyAndShareActualFormulas(Data()); }
        [Test] public void CompanionReceiptsRequireActualContactAndKeepMatureCommands() { CoreChecks.CompanionReceiptsRequireActualContactAndKeepMatureCommands(Data()); }
        [Test] public void CompanionMiraReceiptReportsClampedRecoveryAndActualGuard() { CoreChecks.CompanionMiraReceiptReportsClampedRecoveryAndActualGuard(Data()); }
        [Test] public void CompanionIvoAndSenReceiptsKeepCapturedDamageAndRealGeometry() { CoreChecks.CompanionIvoAndSenReceiptsKeepCapturedDamageAndRealGeometry(Data()); }
        [Test] public void CompanionReceiptClockPauseRetentionAndNewCommandsAreHonest() { CoreChecks.CompanionReceiptClockPauseRetentionAndNewCommandsAreHonest(Data()); }
        [Test] public void CompanionReceiptsCancelAtLifecycleBoundariesWithoutSaveFields() { CoreChecks.CompanionReceiptsCancelAtLifecycleBoundariesWithoutSaveFields(Data()); }
        [Test] public void CombatFocusSelectionWeightsAimDistanceAndStableActorIdentity() { CoreChecks.CombatFocusSelectionWeightsAimDistanceAndStableActorIdentity(Data()); }
        [Test] public void CombatFocusRangeSightAndHealthLossRequireDeliberateReacquisition() { CoreChecks.CombatFocusRangeSightAndHealthLossRequireDeliberateReacquisition(Data()); }
        [Test] public void CombatFocusBackpedalingAndDodgeKeepRealMovementIndependent() { CoreChecks.CombatFocusBackpedalingAndDodgeKeepRealMovementIndependent(Data()); }
        [Test] public void CombatFocusActionsCaptureLiveTargetsWithoutChangingCostsOrContacts() { CoreChecks.CombatFocusActionsCaptureLiveTargetsWithoutChangingCostsOrContacts(Data()); }
        [Test] public void CombatFocusQueuedSkillsAndFlyingMissilesRetainOriginalAim() { CoreChecks.CombatFocusQueuedSkillsAndFlyingMissilesRetainOriginalAim(Data()); }
        [Test] public void CombatFocusMenusWorldDeathAndTrainingClearTransientOwnership() { CoreChecks.CombatFocusMenusWorldDeathAndTrainingClearTransientOwnership(Data()); }
        [Test] public void CombatFocusQueriesLeaveEntireSimulationAndSaveReadOnly() { CoreChecks.CombatFocusQueriesLeaveEntireSimulationAndSaveReadOnly(Data()); }
        [Test] public void SignalRepriseRequiresCompletedStoryAndPhysicalThirdLamp() { CoreChecks.SignalRepriseRequiresCompletedStoryAndPhysicalThirdLamp(Data()); }
        [Test] public void SignalRepriseThreeStartersWinThroughNormalPublicActions() { CoreChecks.SignalRepriseThreeStartersWinThroughNormalPublicActions(Data()); }
        [Test] public void SignalRepriseClockCountsOnlyActiveSimulation() { CoreChecks.SignalRepriseClockCountsOnlyActiveSimulation(Data()); }
        [Test] public void SignalRepriseRecordsReadOnlyCappedPlayerDamageAndHealthLoss() { CoreChecks.SignalRepriseRecordsReadOnlyCappedPlayerDamageAndHealthLoss(Data()); }
        [Test] public void SignalRepriseStopRetryRetiresOnlyAttemptActions() { CoreChecks.SignalRepriseStopRetryRetiresOnlyAttemptActions(Data()); }
        [Test] public void SignalRepriseClonesAndLegacySavesNeverResumeOrRepay() { CoreChecks.SignalRepriseClonesAndLegacySavesNeverResumeOrRepay(Data()); }
        [Test] public void SignalRepriseDeathTravelNewAndTrainingClearSession() { CoreChecks.SignalRepriseDeathTravelNewAndTrainingClearSession(Data()); }
        [Test] public void SignalRepriseKeepsOriginalPatternsCompletedStoryAndFirstReward() { CoreChecks.SignalRepriseKeepsOriginalPatternsCompletedStoryAndFirstReward(Data()); }
        [Test] public void AbyssPlanningFourEarnedStacksRejectFifthBeforeBankHealOrDescent() { CoreChecks.AbyssPlanningFourEarnedStacksRejectFifthBeforeBankHealOrDescent(Data()); }
        [Test] public void AbyssPlanningAllStartersRisksAndBuildPathsPreserveSixFloors() { CoreChecks.AbyssPlanningAllStartersRisksAndBuildPathsPreserveSixFloors(Data()); }
        [Test] public void AbyssPlanningBoonSummariesMatchLiveDamageAndEnergyWithoutMutation() { CoreChecks.AbyssPlanningBoonSummariesMatchLiveDamageAndEnergyWithoutMutation(Data()); }
        [Test] public void AbyssPlanningBankPreviewMatchesCovenantAndOnceOnlyMilestones() { CoreChecks.AbyssPlanningBankPreviewMatchesCovenantAndOnceOnlyMilestones(Data()); }
        [Test] public void AbyssPlanningForecastDescribesActualNextFloorAndNeverRequiresEcho() { CoreChecks.AbyssPlanningForecastDescribesActualNextFloorAndNeverRequiresEcho(Data()); }
        [Test] public void AbyssPlanningStaleChoicesRejectEnemyClearanceDeathAndActiveEcho() { CoreChecks.AbyssPlanningStaleChoicesRejectEnemyClearanceDeathAndActiveEcho(Data()); }
        [Test] public void AbyssPlanningLegacyAllCappedRetainsBuildWithoutBlockingExtraction() { CoreChecks.AbyssPlanningLegacyAllCappedRetainsBuildWithoutBlockingExtraction(Data()); }
        [Test] public void AbyssPlanningCandidateProfilesMatchActuallyEquippedMasteryAndEdge() { CoreChecks.AbyssPlanningCandidateProfilesMatchActuallyEquippedMasteryAndEdge(Data()); }

        [Test] public void ReturnLetterEarnedBranchesPreserveOriginalConversations() { CoreChecks.ReturnLetterEarnedBranchesPreserveOriginalConversations(Data()); }
        [Test] public void ReturnLetterTwoRoutesCompleteWithLocalWitnessesAndSingleReward() { CoreChecks.ReturnLetterTwoRoutesCompleteWithLocalWitnessesAndSingleReward(Data()); }
        [Test] public void ReturnLetterCallbacksRecheckLocalWorldAndOriginalRecords() { CoreChecks.ReturnLetterCallbacksRecheckLocalWorldAndOriginalRecords(Data()); }
        [Test] public void ReturnLetterHandoffRequiresCanonicalSafeUnobstructedRelay() { CoreChecks.ReturnLetterHandoffRequiresCanonicalSafeUnobstructedRelay(Data()); }
        [Test] public void ReturnLetterSignalEarlyLateRetryAndDistanceNeverSpendSupplies() { CoreChecks.ReturnLetterSignalEarlyLateRetryAndDistanceNeverSpendSupplies(Data()); }
        [Test] public void ReturnLetterClockPausesWithActualSimulation() { CoreChecks.ReturnLetterClockPausesWithActualSimulation(Data()); }
        [Test] public void ReturnLetterTemporarySignalClearsAcrossWorldBoundaries() { CoreChecks.ReturnLetterTemporarySignalClearsAcrossWorldBoundaries(Data()); }
        [Test] public void ReturnLetterLegacySavesRetainCheckpointsAndNeverRepay() { CoreChecks.ReturnLetterLegacySavesRetainCheckpointsAndNeverRepay(Data()); }
        [Test] public void CombatFocusCycleOrdersWrapsAndSkipsInvalidActors() { CoreChecks.CombatFocusCycleOrdersWrapsAndSkipsInvalidActors(Data()); }
        [Test] public void CombatFocusCycleNeedsAnExistingLiveSession() { CoreChecks.CombatFocusCycleNeedsAnExistingLiveSession(Data()); }
        [Test] public void CombatFocusCyclePreservesAcceptedQueuedAndFlyingAim() { CoreChecks.CombatFocusCyclePreservesAcceptedQueuedAndFlyingAim(Data()); }
        [Test] public void CombatFocusCycleQueriesAndSelectionKeepProgressReadOnly() { CoreChecks.CombatFocusCycleQueriesAndSelectionKeepProgressReadOnly(Data()); }
        [Test] public void WeaponMasteryThresholdsAndLocalizedReadsArePure() { CoreChecks.WeaponMasteryThresholdsAndLocalizedReadsArePure(Data()); }
        [Test] public void WeaponMasteryInvalidClampedReadsAndSnapshotsStayIndependent() { CoreChecks.WeaponMasteryInvalidClampedReadsAndSnapshotsStayIndependent(Data()); }
        [Test] public void WeaponMasteryBaseBonusMatchesExistingDamageAndEveryProfileMultiplier() { CoreChecks.WeaponMasteryBaseBonusMatchesExistingDamageAndEveryProfileMultiplier(Data()); }
        [Test] public void WeaponMasteryRealRewardsRefreshMilestonesRetainReleasedCreditAndExcludeTraining() { CoreChecks.WeaponMasteryRealRewardsRefreshMilestonesRetainReleasedCreditAndExcludeTraining(Data()); }
        [Test] public void WeaponMasteryV1V2ResumptionPreservesSavedProgressAndOtherFields() { CoreChecks.WeaponMasteryV1V2ResumptionPreservesSavedProgressAndOtherFields(Data()); }
        [Test] public void SkillCooldownThresholdsAndOverlappingReadiness() { CoreChecks.SkillCooldownThresholdsAndOverlappingReadiness(Data()); }
        [Test] public void SkillCooldownRetainsReleasedVolleyAndExecutionValues() { CoreChecks.SkillCooldownRetainsReleasedVolleyAndExecutionValues(Data()); }
        [Test] public void SkillCooldownRejectsInvalidRequestsAndRechecksCosts() { CoreChecks.SkillCooldownRejectsInvalidRequestsAndRechecksCosts(Data()); }
        [Test] public void SkillCooldownHonorsDiscretePriorityAndHitStop() { CoreChecks.SkillCooldownHonorsDiscretePriorityAndHitStop(Data()); }
        [Test] public void SkillCooldownClearsAtLifecycleProfilesAndTrainingReview() { CoreChecks.SkillCooldownClearsAtLifecycleProfilesAndTrainingReview(Data()); }
        [Test] public void AttackBufferThresholdCadencesOverlapAndSingleDispatch() { CoreChecks.AttackBufferThresholdCadencesOverlapAndSingleDispatch(Data()); }
        [Test] public void AttackBufferCapturesAimAndUsesExecutionValuesOnce() { CoreChecks.AttackBufferCapturesAimAndUsesExecutionValuesOnce(Data()); }
        [Test] public void AttackBufferHonorsDiscretePriorityLatestPressAndHitStop() { CoreChecks.AttackBufferHonorsDiscretePriorityLatestPressAndHitStop(Data()); }
        [Test] public void AttackBufferRejectsInvalidRequestsAndRechecksResources() { CoreChecks.AttackBufferRejectsInvalidRequestsAndRechecksResources(Data()); }
        [Test] public void AttackBufferClearsLifecycleProfilesAndTrainingReview() { CoreChecks.AttackBufferClearsLifecycleProfilesAndTrainingReview(Data()); }
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
        [Test] public void CombatLootActualAwardsMatchKindsBossAndLevelRollover() { CoreChecks.CombatLootActualAwardsMatchKindsBossAndLevelRollover(Data()); }
        [Test] public void CombatLootSharedDefeatsIncludeSkillsProjectilesCompanionsAndFrost() { CoreChecks.CombatLootSharedDefeatsIncludeSkillsProjectilesCompanionsAndFrost(Data()); }
        [Test] public void CombatLootAggregationSnapshotsAndSimulationClockStayReadOnly() { CoreChecks.CombatLootAggregationSnapshotsAndSimulationClockStayReadOnly(Data()); }
        [Test] public void CombatLootAbyssMatchesUnbankedPayoutBankingLossAndEcho() { CoreChecks.CombatLootAbyssMatchesUnbankedPayoutBankingLossAndEcho(Data()); }
        [Test] public void CombatLootSpecialActorsLifecycleAndLegacySavesKeepOwnership() { CoreChecks.CombatLootSpecialActorsLifecycleAndLegacySavesKeepOwnership(Data()); }
        [Test] public void InteractionHintResourcesFollowRealGatherAndInnRest() { CoreChecks.InteractionHintResourcesFollowRealGatherAndInnRest(Data()); }
        [Test] public void InteractionHintGardenKeepsDialogueGrowthHarvestAndRecipes() { CoreChecks.InteractionHintGardenKeepsDialogueGrowthHarvestAndRecipes(Data()); }
        [Test] public void InteractionHintSealsAndCachesMatchActualPowersAndOnceRewards() { CoreChecks.InteractionHintSealsAndCachesMatchActualPowersAndOnceRewards(Data()); }
        [Test] public void InteractionHintPreservesNearestAndRejectsForeignOrUnsupportedTargets() { CoreChecks.InteractionHintPreservesNearestAndRejectsForeignOrUnsupportedTargets(Data()); }
        [Test] public void InteractionHintQueriesAndLegacyReloadsKeepAllStateReadOnly() { CoreChecks.InteractionHintQueriesAndLegacyReloadsKeepAllStateReadOnly(Data()); }

        [Test] public void RiverLessonOriginalCommissionAndLakeChoicesRemain() { CoreChecks.RiverLessonOriginalCommissionAndLakeChoicesRemain(Data()); }
        [Test] public void RiverLessonLockedForeignStaleAndUnsafeActionsAreRejected() { CoreChecks.RiverLessonLockedForeignStaleAndUnsafeActionsAreRejected(Data()); }
        [Test] public void RiverLessonBothBanksUseActualPhasesTimeoutsAndSinglePulls() { CoreChecks.RiverLessonBothBanksUseActualPhasesTimeoutsAndSinglePulls(Data()); }
        [Test] public void RiverLessonReportIsLocalAtomicCappedAndPaidOnlyOnce() { CoreChecks.RiverLessonReportIsLocalAtomicCappedAndPaidOnlyOnce(Data()); }
        [Test] public void RiverLessonAppendsQuietGuidanceAndKeepsReachTopology() { CoreChecks.RiverLessonAppendsQuietGuidanceAndKeepsReachTopology(Data()); }
        [Test] public void RiverLessonLegacyCheckpointsAndLiveOwnershipNeverMintCatch() { CoreChecks.RiverLessonLegacyCheckpointsAndLiveOwnershipNeverMintCatch(Data()); }

        [Test] public void WaterGardenOrderedPhysicalHandoffs() { CoreChecks.WaterGardenOrderedPhysicalHandoffs(Data()); }
        [Test] public void WaterGardenRejectsInvalidAndStaleActions() { CoreChecks.WaterGardenRejectsInvalidAndStaleActions(Data()); }
        [Test] public void WaterGardenRewardsAndOldSaves() { CoreChecks.WaterGardenRewardsAndOldSaves(Data()); }
        [Test] public void WaterGardenPreservesTownAndNavigation() { CoreChecks.WaterGardenPreservesTownAndNavigation(Data()); }

        [Test] public void RaceCoursePhysicalPreviewAndDeliberateLaunchProtectResources() { CoreChecks.RaceCoursePhysicalPreviewAndDeliberateLaunchProtectResources(Data()); }
        [Test] public void RaceCoursePausedReviewCancellationAndOwnerRevalidation() { CoreChecks.RaceCoursePausedReviewCancellationAndOwnerRevalidation(Data()); }
        [Test] public void RaceCourseDeadlineBadgesAndPhysicalReplays() { CoreChecks.RaceCourseDeadlineBadgesAndPhysicalReplays(Data()); }
        [Test] public void RaceCourseOldSavesKeepRecordsWhileTransientSessionsClear() { CoreChecks.RaceCourseOldSavesKeepRecordsWhileTransientSessionsClear(Data()); }

        [Test] public void TownProjectQuotesExposeExactCostsBenefitsAndReadOnlySnapshots() { CoreChecks.TownProjectQuotesExposeExactCostsBenefitsAndReadOnlySnapshots(Data()); }
        [Test] public void TownProjectTransactionsKeepSixTiersAndActualFacilityBenefits() { CoreChecks.TownProjectTransactionsKeepSixTiersAndActualFacilityBenefits(Data()); }
        [Test] public void TownProjectStaleInvalidAndForeignReviewsRejectWithoutPayment() { CoreChecks.TownProjectStaleInvalidAndForeignReviewsRejectWithoutPayment(Data()); }
        [Test] public void TownProjectLegacyProgressAndCapturedActionsRemainCompatible() { CoreChecks.TownProjectLegacyProgressAndCapturedActionsRemainCompatible(Data()); }

        [Test] public void ContractHuntLiveTargetsFollowNearestReachableCountingActors() { CoreChecks.ContractHuntLiveTargetsFollowNearestReachableCountingActors(Data()); }
        [Test] public void ContractHuntRealKillsRewardsAndNonCombatContractsStayOriginal() { CoreChecks.ContractHuntRealKillsRewardsAndNonCombatContractsStayOriginal(Data()); }
        [Test] public void ContractHuntFallbacksGuardianPrerequisitesAndNavigationOwnership() { CoreChecks.ContractHuntFallbacksGuardianPrerequisitesAndNavigationOwnership(Data()); }
        [Test] public void ContractHuntQueriesAndLegacySnapshotsRemainReadOnly() { CoreChecks.ContractHuntQueriesAndLegacySnapshotsRemainReadOnly(Data()); }

        [Test] public void SixFloorExpeditionAndBanking() { CoreChecks.AbyssExpeditions(Data()); }
    }
}
#endif
