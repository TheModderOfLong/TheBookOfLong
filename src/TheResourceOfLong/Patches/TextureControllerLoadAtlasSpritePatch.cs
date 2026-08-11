using HarmonyLib;
using Il2Cpp;
using UnityEngine;

namespace TheResourceOfLong
{
    [HarmonyPatch(typeof(TextureController), "LoadAtlasSprite")]
    internal static class TextureControllerLoadAtlasSpritePatch
    {
        [HarmonyPrefix]
        public static bool Prefix(string atlasPath, string spriteName, ref Sprite __result)
        {
            if (string.IsNullOrWhiteSpace(atlasPath) || string.IsNullOrWhiteSpace(spriteName)) return true;

            Sprite sprite;
            if (!IconSpriteOverrideRegistry.TryLoad(atlasPath, spriteName, out sprite) || sprite == null) return true;

            __result = sprite;
            return false;
        }
    }
}
