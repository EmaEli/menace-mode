using System;
using System.Collections.Generic;

namespace GarrisonableBuildings;

internal static class GarrisonAccuracyRules
{
    public const int AccuracyPropertyType = 3;
    public const int AccuracyMultiplierPropertyType = 9;

    public static bool ShouldApply(
        string containerTemplateId,
        ISet<string> enabledBuildingIds,
        bool isAttack,
        int currentStance,
        int deployedStance)
    {
        return isAttack &&
               currentStance >= 0 &&
               currentStance != deployedStance &&
               !string.IsNullOrWhiteSpace(containerTemplateId) &&
               enabledBuildingIds != null &&
               enabledBuildingIds.Contains(containerTemplateId);
    }

    public static float Apply(float currentValue, int propertyType, int amount, float amountMult)
    {
        if (propertyType == AccuracyMultiplierPropertyType)
        {
            if (float.IsNaN(amountMult) || float.IsInfinity(amountMult) || amountMult <= 0f)
                return currentValue;

            return currentValue * amountMult;
        }

        if (propertyType == AccuracyPropertyType)
            return currentValue + amount;

        return currentValue;
    }

    public static object ToPropertyValue(float value, int propertyType)
    {
        if (float.IsNaN(value) || float.IsInfinity(value)) return null;
        if (propertyType == AccuracyPropertyType)
            return value < int.MinValue || value > int.MaxValue ? null : (object)Convert.ToInt32(value);
        if (propertyType == AccuracyMultiplierPropertyType) return value;
        return null;
    }
}