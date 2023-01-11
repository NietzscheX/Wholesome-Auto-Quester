﻿namespace Wholesome_Auto_Quester.Database.Models
{
    public class ModelSpell
    {
        /*public int Id { get; }
        public int category { get; }
        public int dispelType { get; }
        public int mechanic { get; }
        public int attributes { get; }
        public int attributesEx { get; }
        public int attributesExB { get; }
        public int attributesExC { get; }
        public int attributesExD { get; }
        public int attributesExE { get; }
        public int attributesExF { get; }
        public int attributesExG { get; }
        public int shapeshiftMask { get; }
        public int shapeshiftExclude { get; }
        public int targets { get; }
        public int targetCreatureType { get; }
        public int requiresSpellFocus { get; }
        public int facingCasterFlags { get; }
        public int casterAuraState { get; }
        public int targetAuraState { get; }
        public int excludeCasterAuraState { get; }
        public int excludeTargetAuraState { get; }
        public int casterAuraSpell { get; }
        public int targetAuraSpell { get; }
        public int excludeCasterAuraSpell { get; }
        public int excludeTargetAuraSpell { get; }
        public int castingTimeIndex { get; }
        public int recoveryTime { get; }
        public int categoryRecoveryTime { get; }
        public int interruptFlags { get; }
        public int auraInterruptFlags { get; }
        public int channelInterruptFlags { get; }
        public int procTypeMask { get; }
        public int procChance { get; }
        public int procCharges { get; }
        public int maxLevel { get; }
        public int baseLevel { get; }
        public int spellLevel { get; }
        public int durationIndex { get; }
        public int powerType { get; }
        public int manaCost { get; }
        public int manaCostPerLevel { get; }
        public int manaPerSecond { get; }
        public int manaPerSecondPerLevel { get; }
        public int rangeIndex { get; }
        public int speed { get; }
        public int modalNextSpell { get; }
        public int cumulativeAura { get; }
        public int tote1 { get; }
        public int tote2 { get; }
        public int reagent_1 { get; }
        public int reagent_2 { get; }
        public int reagent_3 { get; }
        public int reagent_4 { get; }
        public int reagent_5 { get; }
        public int reagent_6 { get; }
        public int reagent_7 { get; }
        public int reagent_8 { get; }
        public int reagentCount_1 { get; }
        public int reagentCount_2 { get; }
        public int reagentCount_3 { get; }
        public int reagentCount_4 { get; }
        public int reagentCount_5 { get; }
        public int reagentCount_6 { get; }
        public int reagentCount_7 { get; }
        public int reagentCount_8 { get; }
        public int equippedItemClass { get; }
        public int equippedItemSubclass { get; }
        public int equippedItemInvTypes { get; }
        public int effect_1 { get; }
        public int effect_2 { get; }
        public int effect_3 { get; }
        public int effectDieSides_1 { get; }
        public int effectDieSides_2 { get; }
        public int effectDieSides_3 { get; }
        public int effectRealPointsPerLevel_1 { get; }
        public int effectRealPointsPerLevel_2 { get; }
        public int effectRealPointsPerLevel_3 { get; }
        public int effectBasePoints_1 { get; }
        public int effectBasePoints_2 { get; }
        public int effectBasePoints_3 { get; }
        public int effectMechanic_1 { get; }
        public int effectMechanic_2 { get; }
        public int effectMechanic_3 { get; }
        public int implicitTargetA_1 { get; }
        public int implicitTargetA_2 { get; }
        public int implicitTargetA_3 { get; }
        public int implicitTargetB_1 { get; }
        public int implicitTargetB_2 { get; }
        public int implicitTargetB_3 { get; }
        public int effectRadiusIndex_1 { get; }
        public int effectRadiusIndex_2 { get; }
        public int effectRadiusIndex_3 { get; }
        public int effectAura_1 { get; }
        public int effectAura_2 { get; }
        public int effectAura_3 { get; }
        public int effectAuraPeriod_1 { get; }
        public int effectAuraPeriod_2 { get; }
        public int effectAuraPeriod_3 { get; }
        public int effectAmplitude_1 { get; }
        public int effectAmplitude_2 { get; }
        public int effectAmplitude_3 { get; }
        public int effectChainTargets_1 { get; }
        public int effectChainTargets_2 { get; }
        public int effectChainTargets_3 { get; }
        public int effectItemType_1 { get; }
        public int effectItemType_2 { get; }
        public int effectItemType_3 { get; }
        public int effectMiscValue_1 { get; }
        public int effectMiscValue_2 { get; }
        public int effectMiscValue_3 { get; }
        public int effectMiscValueB_1 { get; }
        public int effectMiscValueB_2 { get; }
        public int effectMiscValueB_3 { get; }
        public int effectTriggerSpell_1 { get; }
        public int effectTriggerSpell_2 { get; }
        public int effectTriggerSpell_3 { get; }
        public int effectPointsPerCombo_1 { get; }
        public int effectPointsPerCombo_2 { get; }
        public int effectPointsPerCombo_3 { get; }
        public int effectSpellClassMaskA_1 { get; }
        public int effectSpellClassMaskA_2 { get; }
        public int effectSpellClassMaskA_3 { get; }
        public int effectSpellClassMaskB_1 { get; }
        public int effectSpellClassMaskB_2 { get; }
        public int effectSpellClassMaskB_3 { get; }
        public int effectSpellClassMaskC_1 { get; }
        public int effectSpellClassMaskC_2 { get; }
        public int effectSpellClassMaskC_3 { get; }
        public int spellVisualID_1 { get; }
        public int spellVisualID_2 { get; }
        public int spellIconID { get; }
        public int activeIconID { get; }
        public int spellPriority { get; }
        public int name_flag { get; }
        public string name_lang_1 { get; }
        public int nameSubtext_flag { get; }
        public string nameSubtext_lang_1 { get; }
        public int description_flag { get; }
        public string description_lang_1 { get; }
        public int auraDescription_flag { get; }
        public string auraDescription_lang_1 { get; }
        public int manaCostPct { get; }
        public int startRecoveryCategory { get; }
        public int startRecoveryTime { get; }
        public int maxTargetLevel { get; }
        public int spellClassSet { get; }
        public int spellClassMask_1 { get; }
        public int spellClassMask_2 { get; }
        public int spellClassMask_3 { get; }
        public int maxTargets { get; }
        public int defenseType { get; }
        public int preventionType { get; }
        public int stanceBarOrder { get; }
        public int effectChainAmplitude_1 { get; }
        public int effectChainAmplitude_2 { get; }
        public int effectChainAmplitude_3 { get; }
        public int minFactionID { get; }
        public int minReputation { get; }
        public int requiredAuraVision { get; }
        public int requiredTotemCategoryID_1 { get; }
        public int requiredTotemCategoryID_2 { get; }
        public int requiredAreasID { get; }
        public int schoolMask { get; }
        public int runeCostID { get; }
        public int spellMissileID { get; }
        public int powerDisplayID { get; }
        public int unk1_1 { get; }
        public int unk1_2 { get; }
        public int unk1_3 { get; }
        public int spellDescriptionVariableID { get; }
        public int spellDifficultyID { get; }*/
    }
}
