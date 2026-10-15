using Il2CppInterop.Runtime;
using UnityEngine;

namespace MenaceModSuite.Shared;

/// <summary>Resolves vanilla sprites by asset name from the loaded game resources.</summary>
internal static class SpriteLookup
{
    public static Sprite? Find(string spriteName)
    {
        if (string.IsNullOrWhiteSpace(spriteName)) return null;
        try
        {
            foreach (UnityEngine.Object? obj in Resources.FindObjectsOfTypeAll(Il2CppType.Of<Sprite>()))
            {
                Sprite? sprite = obj?.TryCast<Sprite>();
                if (sprite != null && string.Equals(sprite.name, spriteName, StringComparison.OrdinalIgnoreCase))
                    return sprite;
            }
        }
        catch { }
        return null;
    }
}
