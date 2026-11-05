#nullable enable
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Il2CppMenace.Tactical;
using Il2CppMenace.Tactical.Skills;
using Il2CppMenace.Tactical.Skills.Effects;
using Il2CppMenace.Tools;

namespace GarrisonableBuildings;

public sealed partial class Plugin
{
    private static (EntityPropertyType Type, int Amount, float Mult)[]? _garrisonAccuracyChanges;

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    private static void TryResolveGarrisonAccuracy()
    {
        if (_garrisonAccuracyChanges != null) return;
        try
        {
            var template = DataTemplateLoader.Get<SkillTemplate>("special.stance.deployed", false);
            if (!Valid(template) || template!.EventHandlers == null) return;
            var changes = new List<(EntityPropertyType Type, int Amount, float Mult)>();
            foreach (var handler in template.EventHandlers)
            {
                // IL2CPP collections expose base wrappers: GetType()/reflection
                // cannot see StanceDeployed.Properties without this native cast.
                var deployed = handler?.TryCast<StanceDeployed>();
                if (!Valid(deployed) || deployed!.Properties == null) continue;
                foreach (var change in deployed.Properties)
                {
                    if (change.PropertyType == EntityPropertyType.AccuracyMult)
                    {
                        if (float.IsNaN(change.AmountMult) || float.IsInfinity(change.AmountMult) ||
                            change.AmountMult <= 0f || change.AmountMult > 4f) return;
                    }
                    else if (change.PropertyType == EntityPropertyType.Accuracy)
                    {
                        if (change.Amount < -1000 || change.Amount > 1000) return;
                    }
                    else continue; // Never copy movement/root flags or other deployed effects.
                    changes.Add((change.PropertyType, change.Amount, change.AmountMult));
                }
            }
            if (changes.Count == 0) return;
            _garrisonAccuracyChanges = changes.ToArray();
            DebugMsg($"{ModName}: resolved {changes.Count} native deployed accuracy change(s); applied to current stats without deployment.");
        }
        catch (Exception e) { DebugWarning($"{ModName}: native garrison accuracy lookup failed: {Short(e)}"); }
    }

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    private static void ApplyGarrisonAccuracy(EntityProperties properties)
    {
        foreach (var change in _garrisonAccuracyChanges!)
        {
            if (change.Type == EntityPropertyType.AccuracyMult)
                properties.UpdateMultProperty(change.Type, change.Mult);
            else properties.UpdateProperty(change.Type, change.Amount);
        }
    }
}