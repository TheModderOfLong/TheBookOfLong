using System;
using System.IO;
using UnityEngine;

namespace TheResourceOfLong
{
    public static class RawResourceLoader
    {
        public static UnityEngine.Object Load(ModResourceEntry entry, Type requestedType)
        {
            if (entry == null || string.IsNullOrEmpty(entry.FullSourcePath) || !File.Exists(entry.FullSourcePath))
            {
                return null;
            }

            Type effectiveType = requestedType == null || requestedType == typeof(UnityEngine.Object)
                ? ResourceTypeResolver.Resolve(entry.ResourceTypeName)
                : requestedType;

            string extension = Path.GetExtension(entry.FullSourcePath).ToLowerInvariant();

            if (effectiveType == typeof(TextAsset) || IsTextExtension(extension))
            {
                LoggerManager.Warning("Raw TextAsset cannot be created directly in this IL2CPP build: " + entry.FullSourcePath + ". Existing game TextAsset loads are patched through text/bytes getters; use AssetBundle for brand-new TextAsset paths.");
                return null;
            }

            if (effectiveType == typeof(Sprite))
            {
                Texture2D texture = LoadTexture(entry.FullSourcePath);
                if (texture == null) return null;

                Rect rect = new Rect(0f, 0f, texture.width, texture.height);
                Vector2 pivot = new Vector2(entry.PivotX, entry.PivotY);
                return Sprite.Create(texture, rect, pivot, entry.PixelsPerUnit <= 0f ? 100f : entry.PixelsPerUnit);
            }

            if (effectiveType == typeof(Texture2D) || effectiveType == typeof(Texture))
            {
                return LoadTexture(entry.FullSourcePath);
            }

            if (effectiveType == typeof(AudioClip))
            {
                if (extension == ".wav")
                {
                    return WavAudioLoader.TryLoad(entry.FullSourcePath);
                }

                LoggerManager.Warning("Raw audio format is not synchronously supported yet: " + entry.FullSourcePath + ". Use AssetBundle for this audio, or provide WAV PCM.");
                return null;
            }

            if (IsImageExtension(extension))
            {
                return LoadTexture(entry.FullSourcePath);
            }

            LoggerManager.Warning("Unsupported raw resource type '" + effectiveType.FullName + "' for " + entry.FullSourcePath);
            return null;
        }

        private static Texture2D LoadTexture(string path)
        {
            byte[] bytes = File.ReadAllBytes(path);
            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            bool loaded = ImageConversion.LoadImage(texture, bytes);
            if (!loaded)
            {
                UnityEngine.Object.Destroy(texture);
                LoggerManager.Warning("Failed to decode image: " + path);
                return null;
            }

            texture.name = Path.GetFileNameWithoutExtension(path);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            return texture;
        }

        private static bool IsTextExtension(string extension)
        {
            return extension == ".csv" || extension == ".json" || extension == ".txt" || extension == ".bytes";
        }

        private static bool IsImageExtension(string extension)
        {
            return extension == ".png" || extension == ".jpg" || extension == ".jpeg";
        }
    }
}
