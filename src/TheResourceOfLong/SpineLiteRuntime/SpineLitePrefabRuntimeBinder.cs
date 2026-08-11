using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace TheResourceOfLong
{
    public static class SpineLitePrefabRuntimeBinder
    {
        public const string SlotsRootName = "Slots";
        public const string AttachmentBankRootName = "__SpineLiteAttachmentBank";
        private static readonly Dictionary<int, SpineLiteBakedAnimationData> ParsedDataCache = new Dictionary<int, SpineLiteBakedAnimationData>();

        public static bool TryBind(GameObject instance, string jsonText, out SpineLitePrefabPlayer player, out string report)
        {
            player = null;
            report = string.Empty;
            if (instance == null)
            {
                report = "instance=null";
                return false;
            }

            if (string.IsNullOrWhiteSpace(jsonText))
            {
                report = "json=empty";
                return false;
            }

            SpineLiteBakedAnimationData animationData;
            try
            {
                animationData = GetOrParseAnimationData(jsonText);
            }
            catch (Exception ex)
            {
                report = "json=parseError:" + ex.GetType().Name + ":" + ex.Message;
                return false;
            }

            if (animationData == null)
            {
                report = "json=null";
                return false;
            }

            if (!HasRequiredData(animationData))
            {
                report = "data=incomplete," + DescribeData(animationData);
                return false;
            }

            int boundAttachments = BindAttachmentAssets(instance, animationData);
            MeshFilter[] meshFilters;
            MeshRenderer[] meshRenderers;
            int boundSlots = BindSlotRenderers(instance, animationData, out meshFilters, out meshRenderers);

            if (boundAttachments <= 0 || boundSlots <= 0)
            {
                report = "attachments:" + boundAttachments + ",slots:" + boundSlots + "," + DescribeData(animationData);
                return false;
            }

            player = instance.GetComponent<SpineLitePrefabPlayer>();
            if (player == null) player = instance.AddComponent<SpineLitePrefabPlayer>();
            player.AnimationData = animationData;
            player.MeshFilters = meshFilters;
            player.MeshRenderers = meshRenderers;
            player.PlayOnEnable = true;
            player.Loop = true;
            player.UseUnscaledTime = false;
            player.UpdateFramesPerSecond = 30f;
            player.enabled = true;
            player.RefreshNow();

            report = "attachments:" + boundAttachments + ",slots:" + boundSlots + ",animation:" + Safe(animationData.AnimationName);
            return true;
        }

        public static bool HasUsablePlayer(GameObject instance)
        {
            if (instance == null) return false;
            SpineLitePrefabPlayer[] players = instance.GetComponentsInChildren<SpineLitePrefabPlayer>(true);
            if (players == null) return false;

            for (int i = 0; i < players.Length; i++)
            {
                SpineLitePrefabPlayer player = players[i];
                SpineLiteBakedAnimationData data = player == null ? null : player.AnimationData;
                if (HasRequiredData(data) && player.MeshFilters != null && player.MeshRenderers != null)
                    return true;
            }

            return false;
        }

        private static bool HasRequiredData(SpineLiteBakedAnimationData data)
        {
            return data != null
                && data.Bones != null
                && data.Bones.Length > 0
                && data.Slots != null
                && data.Slots.Length > 0
                && data.Attachments != null
                && data.Attachments.Length > 0;
        }

        private static string DescribeData(SpineLiteBakedAnimationData data)
        {
            if (data == null) return "data=null";
            return "bones:" + SafeLength(data.Bones) +
                ",slotsData:" + SafeLength(data.Slots) +
                ",attachmentsData:" + SafeLength(data.Attachments) +
                ",rotates:" + SafeLength(data.RotateTimelines) +
                ",translates:" + SafeLength(data.TranslateTimelines) +
                ",attachmentsTimelines:" + SafeLength(data.AttachmentTimelines);
        }

        private static SpineLiteBakedAnimationData ParseAnimationData(string jsonText)
        {
            Dictionary<string, object> root = SimpleJson.ParseObject(jsonText);
            SpineLiteBakedAnimationData data = new SpineLiteBakedAnimationData();
            data.AnimationName = GetString(root, "AnimationName", string.Empty);
            data.Duration = GetFloat(root, "Duration", 0f);
            data.Bones = ParseBones(GetArray(root, "Bones"));
            data.Slots = ParseSlots(GetArray(root, "Slots"));
            data.Attachments = ParseAttachments(GetArray(root, "Attachments"));
            data.RotateTimelines = ParseRotateTimelines(GetArray(root, "RotateTimelines"));
            data.TranslateTimelines = ParseTranslateTimelines(GetArray(root, "TranslateTimelines"));
            data.ScaleTimelines = ParseTranslateTimelines(GetArray(root, "ScaleTimelines"));
            data.ShearTimelines = ParseTranslateTimelines(GetArray(root, "ShearTimelines"));
            data.ColorTimelines = ParseColorTimelines(GetArray(root, "ColorTimelines"));
            data.AttachmentTimelines = ParseAttachmentTimelines(GetArray(root, "AttachmentTimelines"));
            data.DrawOrderTimelines = ParseDrawOrderTimelines(GetArray(root, "DrawOrderTimelines"));
            return data;
        }

        private static SpineLiteBakedAnimationData GetOrParseAnimationData(string jsonText)
        {
            int key = jsonText.GetHashCode();
            SpineLiteBakedAnimationData cached;
            if (ParsedDataCache.TryGetValue(key, out cached) && cached != null)
                return cached;

            SpineLiteBakedAnimationData data = ParseAnimationData(jsonText);
            ParsedDataCache[key] = data;
            return data;
        }

        private static SpineLiteBoneData[] ParseBones(object[] items)
        {
            if (items == null) return null;
            SpineLiteBoneData[] result = new SpineLiteBoneData[items.Length];
            for (int i = 0; i < items.Length; i++)
            {
                Dictionary<string, object> item = AsObject(items[i]);
                if (item == null) continue;

                SpineLiteBoneData data = new SpineLiteBoneData();
                data.Name = GetString(item, "Name", string.Empty);
                data.ParentIndex = GetInt(item, "ParentIndex", -1);
                data.TransformMode = GetInt(item, "TransformMode", 0);
                data.X = GetFloat(item, "X", 0f);
                data.Y = GetFloat(item, "Y", 0f);
                data.Rotation = GetFloat(item, "Rotation", 0f);
                data.ScaleX = GetFloat(item, "ScaleX", 1f);
                data.ScaleY = GetFloat(item, "ScaleY", 1f);
                data.ShearX = GetFloat(item, "ShearX", 0f);
                data.ShearY = GetFloat(item, "ShearY", 0f);
                result[i] = data;
            }

            return result;
        }

        private static SpineLiteSlotData[] ParseSlots(object[] items)
        {
            if (items == null) return null;
            SpineLiteSlotData[] result = new SpineLiteSlotData[items.Length];
            for (int i = 0; i < items.Length; i++)
            {
                Dictionary<string, object> item = AsObject(items[i]);
                if (item == null) continue;

                SpineLiteSlotData data = new SpineLiteSlotData();
                data.Name = GetString(item, "Name", string.Empty);
                data.BoneIndex = GetInt(item, "BoneIndex", 0);
                data.SetupAttachmentIndex = GetInt(item, "SetupAttachmentIndex", -1);
                data.R = GetFloat(item, "R", 1f);
                data.G = GetFloat(item, "G", 1f);
                data.B = GetFloat(item, "B", 1f);
                data.A = GetFloat(item, "A", 1f);
                result[i] = data;
            }

            return result;
        }

        private static SpineLiteAttachmentData[] ParseAttachments(object[] items)
        {
            if (items == null) return null;
            SpineLiteAttachmentData[] result = new SpineLiteAttachmentData[items.Length];
            for (int i = 0; i < items.Length; i++)
            {
                Dictionary<string, object> item = AsObject(items[i]);
                if (item == null) continue;

                SpineLiteAttachmentData data = new SpineLiteAttachmentData();
                data.Name = GetString(item, "Name", string.Empty);
                data.SlotIndex = GetInt(item, "SlotIndex", 0);
                data.VertexCount = GetInt(item, "VertexCount", 0);
                data.Bones = ToIntArray(GetValue(item, "Bones"));
                data.Vertices = ToFloatArray(GetValue(item, "Vertices"));
                data.R = GetFloat(item, "R", 1f);
                data.G = GetFloat(item, "G", 1f);
                data.B = GetFloat(item, "B", 1f);
                data.A = GetFloat(item, "A", 1f);
                result[i] = data;
            }

            return result;
        }

        private static SpineLiteRotateTimeline[] ParseRotateTimelines(object[] items)
        {
            if (items == null) return null;
            SpineLiteRotateTimeline[] result = new SpineLiteRotateTimeline[items.Length];
            for (int i = 0; i < items.Length; i++)
            {
                Dictionary<string, object> item = AsObject(items[i]);
                if (item == null) continue;

                SpineLiteRotateTimeline data = new SpineLiteRotateTimeline();
                data.BoneIndex = GetInt(item, "BoneIndex", 0);
                data.Frames = ToFloatArray(GetValue(item, "Frames"));
                data.CurveTypes = ToIntArray(GetValue(item, "CurveTypes"));
                data.CurveSamples = ToFloatArray(GetValue(item, "CurveSamples"));
                result[i] = data;
            }

            return result;
        }

        private static SpineLiteTranslateTimeline[] ParseTranslateTimelines(object[] items)
        {
            if (items == null) return null;
            SpineLiteTranslateTimeline[] result = new SpineLiteTranslateTimeline[items.Length];
            for (int i = 0; i < items.Length; i++)
            {
                Dictionary<string, object> item = AsObject(items[i]);
                if (item == null) continue;

                SpineLiteTranslateTimeline data = new SpineLiteTranslateTimeline();
                data.BoneIndex = GetInt(item, "BoneIndex", 0);
                data.Frames = ToFloatArray(GetValue(item, "Frames"));
                data.CurveTypes = ToIntArray(GetValue(item, "CurveTypes"));
                data.CurveSamples = ToFloatArray(GetValue(item, "CurveSamples"));
                result[i] = data;
            }

            return result;
        }

        private static SpineLiteColorTimeline[] ParseColorTimelines(object[] items)
        {
            if (items == null) return null;
            SpineLiteColorTimeline[] result = new SpineLiteColorTimeline[items.Length];
            for (int i = 0; i < items.Length; i++)
            {
                Dictionary<string, object> item = AsObject(items[i]);
                if (item == null) continue;

                SpineLiteColorTimeline data = new SpineLiteColorTimeline();
                data.SlotIndex = GetInt(item, "SlotIndex", 0);
                data.Frames = ToFloatArray(GetValue(item, "Frames"));
                data.CurveTypes = ToIntArray(GetValue(item, "CurveTypes"));
                data.CurveSamples = ToFloatArray(GetValue(item, "CurveSamples"));
                result[i] = data;
            }

            return result;
        }

        private static SpineLiteAttachmentTimeline[] ParseAttachmentTimelines(object[] items)
        {
            if (items == null) return null;
            SpineLiteAttachmentTimeline[] result = new SpineLiteAttachmentTimeline[items.Length];
            for (int i = 0; i < items.Length; i++)
            {
                Dictionary<string, object> item = AsObject(items[i]);
                if (item == null) continue;

                SpineLiteAttachmentTimeline data = new SpineLiteAttachmentTimeline();
                data.SlotIndex = GetInt(item, "SlotIndex", 0);
                data.Frames = ToFloatArray(GetValue(item, "Frames"));
                data.AttachmentIndices = ToIntArray(GetValue(item, "AttachmentIndices"));
                result[i] = data;
            }

            return result;
        }

        private static SpineLiteDrawOrderTimeline[] ParseDrawOrderTimelines(object[] items)
        {
            if (items == null) return null;
            SpineLiteDrawOrderTimeline[] result = new SpineLiteDrawOrderTimeline[items.Length];
            for (int i = 0; i < items.Length; i++)
            {
                Dictionary<string, object> item = AsObject(items[i]);
                if (item == null) continue;

                SpineLiteDrawOrderTimeline data = new SpineLiteDrawOrderTimeline();
                data.Frames = ToFloatArray(GetValue(item, "Frames"));
                data.DrawOrders = ParseDrawOrderFrames(GetArray(item, "DrawOrders"));
                result[i] = data;
            }

            return result;
        }

        private static SpineLiteDrawOrderFrame[] ParseDrawOrderFrames(object[] items)
        {
            if (items == null) return null;
            SpineLiteDrawOrderFrame[] result = new SpineLiteDrawOrderFrame[items.Length];
            for (int i = 0; i < items.Length; i++)
            {
                Dictionary<string, object> item = AsObject(items[i]);
                if (item == null) continue;

                SpineLiteDrawOrderFrame data = new SpineLiteDrawOrderFrame();
                data.SlotIndices = ToIntArray(GetValue(item, "SlotIndices"));
                result[i] = data;
            }

            return result;
        }

        private static object GetValue(Dictionary<string, object> values, string key)
        {
            object value;
            return SimpleJson.TryGetValueIgnoreCase(values, key, out value) ? value : null;
        }

        private static object[] GetArray(Dictionary<string, object> values, string key)
        {
            return GetValue(values, key) as object[];
        }

        private static Dictionary<string, object> AsObject(object value)
        {
            return value as Dictionary<string, object>;
        }

        private static string GetString(Dictionary<string, object> values, string key, string fallback)
        {
            object value = GetValue(values, key);
            return value == null ? fallback : Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        private static int GetInt(Dictionary<string, object> values, string key, int fallback)
        {
            object value = GetValue(values, key);
            return value == null ? fallback : Convert.ToInt32(value, CultureInfo.InvariantCulture);
        }

        private static float GetFloat(Dictionary<string, object> values, string key, float fallback)
        {
            object value = GetValue(values, key);
            return value == null ? fallback : Convert.ToSingle(value, CultureInfo.InvariantCulture);
        }

        private static int[] ToIntArray(object value)
        {
            object[] items = value as object[];
            if (items == null) return null;

            int[] result = new int[items.Length];
            for (int i = 0; i < items.Length; i++)
                result[i] = Convert.ToInt32(items[i], CultureInfo.InvariantCulture);
            return result;
        }

        private static float[] ToFloatArray(object value)
        {
            object[] items = value as object[];
            if (items == null) return null;

            float[] result = new float[items.Length];
            for (int i = 0; i < items.Length; i++)
                result[i] = Convert.ToSingle(items[i], CultureInfo.InvariantCulture);
            return result;
        }

        private static int SafeLength(Array items)
        {
            return items == null ? 0 : items.Length;
        }

        private static int BindAttachmentAssets(GameObject instance, SpineLiteBakedAnimationData animationData)
        {
            Transform bankRoot = FindChild(instance.transform, AttachmentBankRootName);
            if (bankRoot == null) return 0;

            int count = Mathf.Min(animationData.Attachments.Length, bankRoot.childCount);
            int bound = 0;
            for (int i = 0; i < count; i++)
            {
                SpineLiteAttachmentData attachment = animationData.Attachments[i];
                Transform child = bankRoot.GetChild(i);
                if (attachment == null || child == null) continue;

                MeshFilter meshFilter = child.GetComponent<MeshFilter>();
                MeshRenderer meshRenderer = child.GetComponent<MeshRenderer>();
                attachment.Mesh = meshFilter == null ? null : meshFilter.sharedMesh;
                attachment.Material = meshRenderer == null ? null : meshRenderer.sharedMaterial;
                if (attachment.Mesh != null && attachment.Material != null) bound++;
            }

            return bound;
        }

        private static int BindSlotRenderers(GameObject instance, SpineLiteBakedAnimationData animationData, out MeshFilter[] meshFilters, out MeshRenderer[] meshRenderers)
        {
            int slotCount = animationData.Slots.Length;
            meshFilters = new MeshFilter[slotCount];
            meshRenderers = new MeshRenderer[slotCount];

            Transform slotsRoot = FindChild(instance.transform, SlotsRootName);
            if (slotsRoot == null) return 0;

            int count = Mathf.Min(slotCount, slotsRoot.childCount);
            int bound = 0;
            for (int i = 0; i < count; i++)
            {
                Transform child = slotsRoot.GetChild(i);
                if (child == null) continue;

                MeshFilter meshFilter = child.GetComponent<MeshFilter>();
                MeshRenderer meshRenderer = child.GetComponent<MeshRenderer>();
                meshFilters[i] = meshFilter;
                meshRenderers[i] = meshRenderer;
                if (meshFilter != null && meshRenderer != null) bound++;
            }

            return bound;
        }

        private static Transform FindChild(Transform root, string childName)
        {
            if (root == null || string.IsNullOrEmpty(childName)) return null;
            Transform direct = root.Find(childName);
            if (direct != null) return direct;

            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                Transform found = FindChild(child, childName);
                if (found != null) return found;
            }

            return null;
        }

        private static string Safe(string value)
        {
            return value ?? string.Empty;
        }
    }
}
