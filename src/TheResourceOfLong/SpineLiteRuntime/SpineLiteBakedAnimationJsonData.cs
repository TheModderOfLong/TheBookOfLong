using System;
using UnityEngine;

namespace TheResourceOfLong
{
    [Serializable]
    public sealed class SpineLiteBakedAnimationJsonData
    {
        public string AnimationName;
        public float Duration;
        public SpineLiteBoneData[] Bones;
        public SpineLiteSlotData[] Slots;
        public SpineLiteAttachmentJsonData[] Attachments;
        public SpineLiteRotateTimeline[] RotateTimelines;
        public SpineLiteTranslateTimeline[] TranslateTimelines;
        public SpineLiteTranslateTimeline[] ScaleTimelines;
        public SpineLiteTranslateTimeline[] ShearTimelines;
        public SpineLiteColorTimeline[] ColorTimelines;
        public SpineLiteAttachmentTimeline[] AttachmentTimelines;
        public SpineLiteDrawOrderTimeline[] DrawOrderTimelines;

        public static SpineLiteBakedAnimationJsonData FromRuntimeData(SpineLiteBakedAnimationData data)
        {
            if (data == null) return null;

            SpineLiteBakedAnimationJsonData result = new SpineLiteBakedAnimationJsonData();
            result.AnimationName = data.AnimationName;
            result.Duration = data.Duration;
            result.Bones = data.Bones;
            result.Slots = data.Slots;
            result.Attachments = FromRuntimeAttachments(data.Attachments);
            result.RotateTimelines = data.RotateTimelines;
            result.TranslateTimelines = data.TranslateTimelines;
            result.ScaleTimelines = data.ScaleTimelines;
            result.ShearTimelines = data.ShearTimelines;
            result.ColorTimelines = data.ColorTimelines;
            result.AttachmentTimelines = data.AttachmentTimelines;
            result.DrawOrderTimelines = data.DrawOrderTimelines;
            return result;
        }

        public SpineLiteBakedAnimationData ToRuntimeData()
        {
            SpineLiteBakedAnimationData result = new SpineLiteBakedAnimationData();
            result.AnimationName = AnimationName;
            result.Duration = Duration;
            result.Bones = Bones;
            result.Slots = Slots;
            result.Attachments = ToRuntimeAttachments(Attachments);
            result.RotateTimelines = RotateTimelines;
            result.TranslateTimelines = TranslateTimelines;
            result.ScaleTimelines = ScaleTimelines;
            result.ShearTimelines = ShearTimelines;
            result.ColorTimelines = ColorTimelines;
            result.AttachmentTimelines = AttachmentTimelines;
            result.DrawOrderTimelines = DrawOrderTimelines;
            return result;
        }

        private static SpineLiteAttachmentJsonData[] FromRuntimeAttachments(SpineLiteAttachmentData[] attachments)
        {
            if (attachments == null) return null;

            SpineLiteAttachmentJsonData[] result = new SpineLiteAttachmentJsonData[attachments.Length];
            for (int i = 0; i < attachments.Length; i++)
            {
                SpineLiteAttachmentData attachment = attachments[i];
                if (attachment == null) continue;

                result[i] = new SpineLiteAttachmentJsonData
                {
                    Name = attachment.Name,
                    SlotIndex = attachment.SlotIndex,
                    VertexCount = attachment.VertexCount,
                    Bones = attachment.Bones,
                    Vertices = attachment.Vertices,
                    R = attachment.R,
                    G = attachment.G,
                    B = attachment.B,
                    A = attachment.A
                };
            }

            return result;
        }

        private static SpineLiteAttachmentData[] ToRuntimeAttachments(SpineLiteAttachmentJsonData[] attachments)
        {
            if (attachments == null) return null;

            SpineLiteAttachmentData[] result = new SpineLiteAttachmentData[attachments.Length];
            for (int i = 0; i < attachments.Length; i++)
            {
                SpineLiteAttachmentJsonData attachment = attachments[i];
                if (attachment == null) continue;

                result[i] = new SpineLiteAttachmentData
                {
                    Name = attachment.Name,
                    SlotIndex = attachment.SlotIndex,
                    VertexCount = attachment.VertexCount,
                    Bones = attachment.Bones,
                    Vertices = attachment.Vertices,
                    R = attachment.R,
                    G = attachment.G,
                    B = attachment.B,
                    A = attachment.A
                };
            }

            return result;
        }
    }

    [Serializable]
    public sealed class SpineLiteAttachmentJsonData
    {
        public string Name;
        public int SlotIndex;
        public int VertexCount;
        public int[] Bones;
        public float[] Vertices;
        public float R = 1f;
        public float G = 1f;
        public float B = 1f;
        public float A = 1f;
    }
}
