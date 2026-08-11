using System;
using UnityEngine;

namespace TheResourceOfLong
{
    [Serializable]
    public sealed class SpineLiteBakedAnimationData
    {
        public string AnimationName;
        public float Duration;
        public SpineLiteBoneData[] Bones;
        public SpineLiteSlotData[] Slots;
        public SpineLiteAttachmentData[] Attachments;
        public SpineLiteRotateTimeline[] RotateTimelines;
        public SpineLiteTranslateTimeline[] TranslateTimelines;
        public SpineLiteTranslateTimeline[] ScaleTimelines;
        public SpineLiteTranslateTimeline[] ShearTimelines;
        public SpineLiteColorTimeline[] ColorTimelines;
        public SpineLiteAttachmentTimeline[] AttachmentTimelines;
        public SpineLiteDrawOrderTimeline[] DrawOrderTimelines;
    }

    [Serializable]
    public sealed class SpineLiteBoneData
    {
        public string Name;
        public int ParentIndex = -1;
        public int TransformMode;
        public float X;
        public float Y;
        public float Rotation;
        public float ScaleX = 1f;
        public float ScaleY = 1f;
        public float ShearX;
        public float ShearY;
    }

    [Serializable]
    public sealed class SpineLiteSlotData
    {
        public string Name;
        public int BoneIndex;
        public int SetupAttachmentIndex = -1;
        public float R = 1f;
        public float G = 1f;
        public float B = 1f;
        public float A = 1f;
    }

    [Serializable]
    public sealed class SpineLiteAttachmentData
    {
        public string Name;
        public int SlotIndex;
        public Mesh Mesh;
        public Material Material;
        public int VertexCount;
        public int[] Bones;
        public float[] Vertices;
        public float R = 1f;
        public float G = 1f;
        public float B = 1f;
        public float A = 1f;
    }

    [Serializable]
    public sealed class SpineLiteRotateTimeline
    {
        public int BoneIndex;
        public float[] Frames;
        public int[] CurveTypes;
        public float[] CurveSamples;
    }

    [Serializable]
    public sealed class SpineLiteTranslateTimeline
    {
        public int BoneIndex;
        public float[] Frames;
        public int[] CurveTypes;
        public float[] CurveSamples;
    }

    [Serializable]
    public sealed class SpineLiteColorTimeline
    {
        public int SlotIndex;
        public float[] Frames;
        public int[] CurveTypes;
        public float[] CurveSamples;
    }

    [Serializable]
    public sealed class SpineLiteAttachmentTimeline
    {
        public int SlotIndex;
        public float[] Frames;
        public int[] AttachmentIndices;
    }

    [Serializable]
    public sealed class SpineLiteDrawOrderTimeline
    {
        public float[] Frames;
        public SpineLiteDrawOrderFrame[] DrawOrders;
    }

    [Serializable]
    public sealed class SpineLiteDrawOrderFrame
    {
        public int[] SlotIndices;
    }
}
