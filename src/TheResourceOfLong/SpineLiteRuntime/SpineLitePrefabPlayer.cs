using System;
using UnityEngine;

namespace TheResourceOfLong
{
#if UNITY_EDITOR
    [ExecuteInEditMode]
#endif
    public sealed class SpineLitePrefabPlayer : MonoBehaviour
    {
        private const int TransformModeNormal = 0;
        private const int TransformModeOnlyTranslation = 7;
        private const int TransformModeNoRotationOrReflection = 1;
        private const int TransformModeNoScale = 2;
        private const int TransformModeNoScaleOrReflection = 6;
        private const int CurveStepped = 1;
        private const int CurveBezier = 2;
        private const int CurveSampleCount = 16;

        public SpineLiteBakedAnimationData AnimationData;
        public MeshFilter[] MeshFilters;
        public MeshRenderer[] MeshRenderers;
        public bool PlayOnEnable = true;
        public bool Loop = true;
        public bool UseUnscaledTime;
        public float UpdateFramesPerSecond = 30f;
        public bool RecalculateBoundsOnAttachmentChange = true;

        private RuntimeBone[] _bones;
        private RuntimeSlot[] _slots;
        private Mesh[] _runtimeMeshes;
        private Vector3[][] _vertices;
        private Color32[][] _colors;
        private int[] _lastAttachmentIndices;
        private float _time;
        private float _lastRealtime;
        private float _updateAccumulator;
        private bool _playing;
        private bool _initialized;

        private void OnEnable()
        {
            _playing = PlayOnEnable;
            _time = 0f;
            _lastRealtime = Time.realtimeSinceStartup;
            _updateAccumulator = 0f;
            Initialize();
            ApplyPose(0f);
        }

        private void OnDisable()
        {
            _playing = false;
        }

        private void OnDestroy()
        {
            ReleaseRuntimeMeshes();
        }

        private void Update()
        {
            Initialize();
            if (!_playing || AnimationData == null) return;

            float duration = AnimationData.Duration;
            if (duration <= 0f)
            {
                ApplyPose(0f);
                return;
            }

            float delta;
            if (Application.isPlaying)
            {
                delta = UseUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            }
            else
            {
                float now = Time.realtimeSinceStartup;
                delta = Mathf.Max(0f, now - _lastRealtime);
                _lastRealtime = now;
            }

            if (UpdateFramesPerSecond > 0f)
            {
                _updateAccumulator += delta;
                float frameDuration = 1f / Mathf.Max(1f, UpdateFramesPerSecond);
                if (_updateAccumulator < frameDuration) return;

                delta = _updateAccumulator;
                _updateAccumulator = 0f;
            }

            _time += delta;
            if (Loop)
            {
                _time = Mathf.Repeat(_time, duration);
            }
            else if (_time > duration)
            {
                _time = duration;
                _playing = false;
            }

            ApplyPose(_time);
        }

        public void Play()
        {
            _playing = true;
            _lastRealtime = Time.realtimeSinceStartup;
        }

        public void Stop()
        {
            _playing = false;
        }

        public void Rewind()
        {
            _time = 0f;
            _lastRealtime = Time.realtimeSinceStartup;
            ApplyPose(0f);
        }

        public void RefreshNow()
        {
            Initialize();
            ApplyPose(_time);
        }

        public void SetTime(float time)
        {
            _time = AnimationData != null && AnimationData.Duration > 0f
                ? Mathf.Repeat(time, AnimationData.Duration)
                : Mathf.Max(0f, time);
            ApplyPose(_time);
        }

        public string GetDebugReport()
        {
            int setupAttachments = 0;
            int validAttachments = 0;
            int runtimeMeshes = 0;
            int enabledRenderers = 0;
            int activeRenderers = 0;
            int materialRenderers = 0;

            SpineLiteSlotData[] slots = AnimationData != null ? AnimationData.Slots : null;
            if (slots != null)
            {
                for (int i = 0; i < slots.Length; i++)
                {
                    SpineLiteSlotData slot = slots[i];
                    if (slot != null && slot.SetupAttachmentIndex >= 0) setupAttachments++;
                }
            }

            if (_slots != null)
            {
                for (int i = 0; i < _slots.Length; i++)
                {
                    RuntimeSlot slot = _slots[i];
                    SpineLiteAttachmentData attachment = slot != null && IsValidAttachment(slot.AttachmentIndex)
                        ? AnimationData.Attachments[slot.AttachmentIndex]
                        : null;
                    if (attachment != null && attachment.Mesh != null && attachment.Material != null) validAttachments++;
                }
            }

            if (_runtimeMeshes != null)
            {
                for (int i = 0; i < _runtimeMeshes.Length; i++)
                    if (_runtimeMeshes[i] != null) runtimeMeshes++;
            }

            if (MeshRenderers != null)
            {
                for (int i = 0; i < MeshRenderers.Length; i++)
                {
                    MeshRenderer renderer = MeshRenderers[i];
                    if (renderer == null) continue;
                    if (renderer.sharedMaterial != null) materialRenderers++;
                    if (renderer.enabled) enabledRenderers++;
                    if (renderer.enabled && renderer.gameObject.activeInHierarchy) activeRenderers++;
                }
            }

            return "playerDebug=initialized:" + _initialized +
                ",time:" + _time.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) +
                ",setupAttachments:" + setupAttachments +
                ",validAttachments:" + validAttachments +
                ",runtimeMeshes:" + runtimeMeshes +
                ",enabledSlotRenderers:" + enabledRenderers +
                ",activeSlotRenderers:" + activeRenderers +
                ",materialSlotRenderers:" + materialRenderers;
        }

        private void Initialize()
        {
            if (_initialized && IsRuntimeShapeValid()) return;
            if (!HasUsableAnimationData())
            {
                _initialized = false;
                return;
            }

            _initialized = true;

            ReleaseRuntimeMeshes();

            int boneCount = SafeLength(AnimationData != null ? AnimationData.Bones : null);
            _bones = new RuntimeBone[boneCount];
            for (int i = 0; i < boneCount; i++)
                _bones[i] = new RuntimeBone();

            int slotCount = SafeLength(AnimationData != null ? AnimationData.Slots : null);
            _slots = new RuntimeSlot[slotCount];
            _runtimeMeshes = new Mesh[slotCount];
            _vertices = new Vector3[slotCount][];
            _colors = new Color32[slotCount][];
            _lastAttachmentIndices = new int[slotCount];

            for (int i = 0; i < slotCount; i++)
            {
                _slots[i] = new RuntimeSlot();
                _lastAttachmentIndices[i] = int.MinValue;
            }
        }

        private bool IsRuntimeShapeValid()
        {
            int slotCount = SafeLength(AnimationData != null ? AnimationData.Slots : null);
            int boneCount = SafeLength(AnimationData != null ? AnimationData.Bones : null);
            return _bones != null && _bones.Length == boneCount
                && _slots != null && _slots.Length == slotCount
                && _runtimeMeshes != null && _runtimeMeshes.Length == slotCount;
        }

        private bool HasUsableAnimationData()
        {
            return AnimationData != null
                && AnimationData.Bones != null
                && AnimationData.Slots != null
                && AnimationData.Attachments != null
                && AnimationData.Bones.Length > 0
                && AnimationData.Slots.Length > 0
                && AnimationData.Attachments.Length > 0
                && HasAnyRenderableAttachment()
                && MeshFilters != null
                && MeshRenderers != null
                && MeshFilters.Length > 0
                && MeshRenderers.Length > 0;
        }

        private bool HasAnyRenderableAttachment()
        {
            if (AnimationData == null || AnimationData.Attachments == null) return false;
            for (int i = 0; i < AnimationData.Attachments.Length; i++)
            {
                SpineLiteAttachmentData attachment = AnimationData.Attachments[i];
                if (attachment != null && attachment.Mesh != null && attachment.Material != null) return true;
            }

            return false;
        }

        private void ReleaseRuntimeMeshes()
        {
            if (_runtimeMeshes == null) return;
            for (int i = 0; i < _runtimeMeshes.Length; i++)
            {
                Mesh mesh = _runtimeMeshes[i];
                if (mesh == null) continue;
                if (Application.isPlaying)
                    Destroy(mesh);
                else
                    DestroyImmediate(mesh);
            }
        }

        private void ApplyPose(float time)
        {
            if (!HasUsableAnimationData()) return;
            Initialize();
            ResetPose();
            ApplyTimelines(time);
            UpdateWorldTransforms();
            ApplySlots();
        }

        private void ResetPose()
        {
            for (int i = 0; i < _bones.Length; i++)
            {
                SpineLiteBoneData data = AnimationData.Bones[i];
                RuntimeBone bone = _bones[i];
                bone.X = data.X;
                bone.Y = data.Y;
                bone.Rotation = data.Rotation;
                bone.ScaleX = data.ScaleX;
                bone.ScaleY = data.ScaleY;
                bone.ShearX = data.ShearX;
                bone.ShearY = data.ShearY;
            }

            for (int i = 0; i < _slots.Length; i++)
            {
                SpineLiteSlotData data = AnimationData.Slots[i];
                RuntimeSlot slot = _slots[i];
                slot.AttachmentIndex = data.SetupAttachmentIndex;
                slot.R = data.R;
                slot.G = data.G;
                slot.B = data.B;
                slot.A = data.A;
                slot.SortingOrder = i;
            }
        }

        private void ApplyTimelines(float time)
        {
            ApplyRotateTimelines(AnimationData.RotateTimelines, time);
            ApplyTranslateTimelines(AnimationData.TranslateTimelines, time, TranslateMode.Translate);
            ApplyTranslateTimelines(AnimationData.ScaleTimelines, time, TranslateMode.Scale);
            ApplyTranslateTimelines(AnimationData.ShearTimelines, time, TranslateMode.Shear);
            ApplyColorTimelines(AnimationData.ColorTimelines, time);
            ApplyAttachmentTimelines(AnimationData.AttachmentTimelines, time);
            ApplyDrawOrderTimelines(AnimationData.DrawOrderTimelines, time);
        }

        private void ApplyRotateTimelines(SpineLiteRotateTimeline[] timelines, float time)
        {
            if (timelines == null) return;
            for (int i = 0; i < timelines.Length; i++)
            {
                SpineLiteRotateTimeline timeline = timelines[i];
                if (timeline == null || !IsValidBone(timeline.BoneIndex)) continue;
                if (!HasTimelineStarted(timeline.Frames, time)) continue;

                float value = EvaluateRotate(timeline.Frames, timeline.CurveTypes, timeline.CurveSamples, time);
                _bones[timeline.BoneIndex].Rotation = AnimationData.Bones[timeline.BoneIndex].Rotation + value;
            }
        }

        private void ApplyTranslateTimelines(SpineLiteTranslateTimeline[] timelines, float time, TranslateMode mode)
        {
            if (timelines == null) return;
            for (int i = 0; i < timelines.Length; i++)
            {
                SpineLiteTranslateTimeline timeline = timelines[i];
                if (timeline == null || !IsValidBone(timeline.BoneIndex)) continue;
                if (!HasTimelineStarted(timeline.Frames, time)) continue;

                float x;
                float y;
                EvaluatePair(timeline.Frames, timeline.CurveTypes, timeline.CurveSamples, time, out x, out y);
                RuntimeBone bone = _bones[timeline.BoneIndex];
                SpineLiteBoneData setup = AnimationData.Bones[timeline.BoneIndex];
                if (mode == TranslateMode.Translate)
                {
                    bone.X = setup.X + x;
                    bone.Y = setup.Y + y;
                }
                else if (mode == TranslateMode.Scale)
                {
                    bone.ScaleX = setup.ScaleX * x;
                    bone.ScaleY = setup.ScaleY * y;
                }
                else
                {
                    bone.ShearX = setup.ShearX + x;
                    bone.ShearY = setup.ShearY + y;
                }
            }
        }

        private void ApplyColorTimelines(SpineLiteColorTimeline[] timelines, float time)
        {
            if (timelines == null) return;
            for (int i = 0; i < timelines.Length; i++)
            {
                SpineLiteColorTimeline timeline = timelines[i];
                if (timeline == null || !IsValidSlot(timeline.SlotIndex)) continue;
                if (!HasTimelineStarted(timeline.Frames, time)) continue;

                RuntimeSlot slot = _slots[timeline.SlotIndex];
                EvaluateColor(timeline.Frames, timeline.CurveTypes, timeline.CurveSamples, time, out slot.R, out slot.G, out slot.B, out slot.A);
            }
        }

        private void ApplyAttachmentTimelines(SpineLiteAttachmentTimeline[] timelines, float time)
        {
            if (timelines == null) return;
            for (int i = 0; i < timelines.Length; i++)
            {
                SpineLiteAttachmentTimeline timeline = timelines[i];
                if (timeline == null || !IsValidSlot(timeline.SlotIndex) || timeline.Frames == null || timeline.Frames.Length == 0) continue;

                if (time < timeline.Frames[0])
                    continue;

                int frame = time >= timeline.Frames[timeline.Frames.Length - 1]
                    ? timeline.Frames.Length - 1
                    : BinarySearch(timeline.Frames, time) - 1;
                _slots[timeline.SlotIndex].AttachmentIndex = GetInt(timeline.AttachmentIndices, frame, -1);
            }
        }

        private void ApplyDrawOrderTimelines(SpineLiteDrawOrderTimeline[] timelines, float time)
        {
            if (timelines == null) return;
            for (int i = 0; i < timelines.Length; i++)
            {
                SpineLiteDrawOrderTimeline timeline = timelines[i];
                if (timeline == null || timeline.Frames == null || timeline.Frames.Length == 0) continue;

                if (time < timeline.Frames[0])
                    continue;

                int frame = time >= timeline.Frames[timeline.Frames.Length - 1]
                    ? timeline.Frames.Length - 1
                    : BinarySearch(timeline.Frames, time) - 1;
                SpineLiteDrawOrderFrame orderFrame = GetObject(timeline.DrawOrders, frame);
                int[] order = orderFrame != null ? orderFrame.SlotIndices : null;

                for (int slotIndex = 0; slotIndex < _slots.Length; slotIndex++)
                    _slots[slotIndex].SortingOrder = slotIndex;

                if (order == null) continue;
                for (int drawIndex = 0; drawIndex < order.Length; drawIndex++)
                {
                    int slotIndex = order[drawIndex];
                    if (IsValidSlot(slotIndex))
                        _slots[slotIndex].SortingOrder = drawIndex;
                }
            }
        }

        private void UpdateWorldTransforms()
        {
            for (int i = 0; i < _bones.Length; i++)
                UpdateWorldTransform(i);
        }

        private void UpdateWorldTransform(int index)
        {
            SpineLiteBoneData data = AnimationData.Bones[index];
            RuntimeBone bone = _bones[index];
            int parentIndex = data.ParentIndex;

            if (parentIndex < 0 || parentIndex >= _bones.Length)
            {
                float rotationY = bone.Rotation + 90f + bone.ShearY;
                bone.A = CosDeg(bone.Rotation + bone.ShearX) * bone.ScaleX;
                bone.B = CosDeg(rotationY) * bone.ScaleY;
                bone.C = SinDeg(bone.Rotation + bone.ShearX) * bone.ScaleX;
                bone.D = SinDeg(rotationY) * bone.ScaleY;
                bone.WorldX = bone.X;
                bone.WorldY = bone.Y;
                return;
            }

            RuntimeBone parent = _bones[parentIndex];
            float pa = parent.A;
            float pb = parent.B;
            float pc = parent.C;
            float pd = parent.D;

            bone.WorldX = pa * bone.X + pb * bone.Y + parent.WorldX;
            bone.WorldY = pc * bone.X + pd * bone.Y + parent.WorldY;

            if (data.TransformMode == TransformModeNormal)
            {
                float rotationY = bone.Rotation + 90f + bone.ShearY;
                float la = CosDeg(bone.Rotation + bone.ShearX) * bone.ScaleX;
                float lb = CosDeg(rotationY) * bone.ScaleY;
                float lc = SinDeg(bone.Rotation + bone.ShearX) * bone.ScaleX;
                float ld = SinDeg(rotationY) * bone.ScaleY;
                bone.A = pa * la + pb * lc;
                bone.B = pa * lb + pb * ld;
                bone.C = pc * la + pd * lc;
                bone.D = pc * lb + pd * ld;
                return;
            }

            if (data.TransformMode == TransformModeOnlyTranslation)
            {
                float rotationY = bone.Rotation + 90f + bone.ShearY;
                bone.A = CosDeg(bone.Rotation + bone.ShearX) * bone.ScaleX;
                bone.B = CosDeg(rotationY) * bone.ScaleY;
                bone.C = SinDeg(bone.Rotation + bone.ShearX) * bone.ScaleX;
                bone.D = SinDeg(rotationY) * bone.ScaleY;
                return;
            }

            if (data.TransformMode == TransformModeNoRotationOrReflection)
            {
                float s = pa * pa + pc * pc;
                float prx;
                if (s > 0.0001f)
                {
                    s = Mathf.Abs(pa * pd - pb * pc) / s;
                    pb = pc * s;
                    pd = pa * s;
                    prx = Mathf.Atan2(pc, pa) * Mathf.Rad2Deg;
                }
                else
                {
                    pa = 0f;
                    pc = 0f;
                    prx = 90f - Mathf.Atan2(pd, pb) * Mathf.Rad2Deg;
                }

                float rx = bone.Rotation + bone.ShearX - prx;
                float ry = bone.Rotation + bone.ShearY - prx + 90f;
                float la = CosDeg(rx) * bone.ScaleX;
                float lb = CosDeg(ry) * bone.ScaleY;
                float lc = SinDeg(rx) * bone.ScaleX;
                float ld = SinDeg(ry) * bone.ScaleY;
                bone.A = pa * la - pb * lc;
                bone.B = pa * lb - pb * ld;
                bone.C = pc * la + pd * lc;
                bone.D = pc * lb + pd * ld;
                return;
            }

            if (data.TransformMode == TransformModeNoScale || data.TransformMode == TransformModeNoScaleOrReflection)
            {
                float cos = CosDeg(bone.Rotation);
                float sin = SinDeg(bone.Rotation);
                float za = pa * cos + pb * sin;
                float zc = pc * cos + pd * sin;
                float s = Mathf.Sqrt(za * za + zc * zc);
                if (s > 0.00001f) s = 1f / s;
                za *= s;
                zc *= s;
                s = Mathf.Sqrt(za * za + zc * zc);
                if (data.TransformMode == TransformModeNoScale && pa * pd - pb * pc < 0f) s = -s;

                float r = Mathf.PI * 0.5f + Mathf.Atan2(zc, za);
                float zb = Mathf.Cos(r) * s;
                float zd = Mathf.Sin(r) * s;
                float la = CosDeg(bone.ShearX) * bone.ScaleX;
                float lb = CosDeg(90f + bone.ShearY) * bone.ScaleY;
                float lc = SinDeg(bone.ShearX) * bone.ScaleX;
                float ld = SinDeg(90f + bone.ShearY) * bone.ScaleY;
                bone.A = za * la + zb * lc;
                bone.B = za * lb + zb * ld;
                bone.C = zc * la + zd * lc;
                bone.D = zc * lb + zd * ld;
            }
        }

        private void ApplySlots()
        {
            int count = Mathf.Min(_slots.Length, Mathf.Min(SafeLength(MeshFilters), SafeLength(MeshRenderers)));
            for (int i = 0; i < count; i++)
            {
                MeshFilter meshFilter = MeshFilters[i];
                MeshRenderer meshRenderer = MeshRenderers[i];
                if (meshFilter == null || meshRenderer == null) continue;

                RuntimeSlot slot = _slots[i];
                SpineLiteAttachmentData attachment = IsValidAttachment(slot.AttachmentIndex)
                    ? AnimationData.Attachments[slot.AttachmentIndex]
                    : null;

                if (attachment == null || attachment.Mesh == null || attachment.Material == null)
                {
                    meshRenderer.enabled = false;
                    continue;
                }

                Mesh runtimeMesh = _runtimeMeshes[i];
                bool attachmentChanged = _lastAttachmentIndices[i] != slot.AttachmentIndex;
                if (attachmentChanged || runtimeMesh == null)
                {
                    runtimeMesh = CloneStaticMeshData(i, meshFilter, attachment.Mesh);
                    if (runtimeMesh == null)
                    {
                        meshRenderer.enabled = false;
                        continue;
                    }

                    _vertices[i] = EnsureVertices(_vertices[i], attachment.VertexCount);
                    _colors[i] = EnsureColors(_colors[i], attachment.VertexCount);
                    _lastAttachmentIndices[i] = slot.AttachmentIndex;
                }

                UpdateAttachmentVertices(i, slot, attachment, runtimeMesh, attachmentChanged && RecalculateBoundsOnAttachmentChange);
                meshRenderer.sharedMaterial = attachment.Material;
                meshRenderer.sortingOrder = slot.SortingOrder;
                meshRenderer.enabled = true;
            }
        }

        private void UpdateAttachmentVertices(int slotIndex, RuntimeSlot slot, SpineLiteAttachmentData attachment, Mesh runtimeMesh, bool recalculateBounds)
        {
            Vector3[] vertices = EnsureVertices(_vertices[slotIndex], attachment.VertexCount);
            Color32[] colors = EnsureColors(_colors[slotIndex], attachment.VertexCount);
            _vertices[slotIndex] = vertices;
            _colors[slotIndex] = colors;

            Color color = new Color(
                slot.R * attachment.R,
                slot.G * attachment.G,
                slot.B * attachment.B,
                slot.A * attachment.A);
            Color32 color32 = color;
            for (int i = 0; i < colors.Length; i++)
                colors[i] = color32;

            if (attachment.Bones == null || attachment.Bones.Length == 0)
            {
                SpineLiteSlotData slotData = AnimationData.Slots[attachment.SlotIndex];
                RuntimeBone bone = IsValidBone(slotData.BoneIndex) ? _bones[slotData.BoneIndex] : null;
                if (bone == null) return;

                for (int i = 0, v = 0; i < attachment.VertexCount; i++, v += 2)
                {
                    float x = GetFloat(attachment.Vertices, v);
                    float y = GetFloat(attachment.Vertices, v + 1);
                    vertices[i] = new Vector3(x * bone.A + y * bone.B + bone.WorldX, x * bone.C + y * bone.D + bone.WorldY, 0f);
                }
            }
            else
            {
                int boneCursor = 0;
                int vertexCursor = 0;
                for (int i = 0; i < attachment.VertexCount; i++)
                {
                    float wx = 0f;
                    float wy = 0f;
                    int influenceCount = attachment.Bones[boneCursor++];
                    for (int n = 0; n < influenceCount; n++)
                    {
                        int boneIndex = attachment.Bones[boneCursor++];
                        RuntimeBone bone = IsValidBone(boneIndex) ? _bones[boneIndex] : null;
                        float vx = GetFloat(attachment.Vertices, vertexCursor++);
                        float vy = GetFloat(attachment.Vertices, vertexCursor++);
                        float weight = GetFloat(attachment.Vertices, vertexCursor++);
                        if (bone == null) continue;

                        wx += (vx * bone.A + vy * bone.B + bone.WorldX) * weight;
                        wy += (vx * bone.C + vy * bone.D + bone.WorldY) * weight;
                    }

                    vertices[i] = new Vector3(wx, wy, 0f);
                }
            }

            runtimeMesh.vertices = vertices;
            runtimeMesh.colors32 = colors;
            if (recalculateBounds) runtimeMesh.RecalculateBounds();
        }

        private Mesh CloneStaticMeshData(int slotIndex, MeshFilter meshFilter, Mesh source)
        {
            if (source == null || meshFilter == null) return null;

            ReleaseRuntimeMesh(slotIndex);

            Mesh clone = UnityEngine.Object.Instantiate(source);
            if (clone == null) return null;

            clone.name = "SpineLiteRuntime_" + slotIndex;
            clone.hideFlags = HideFlags.DontSave;
            _runtimeMeshes[slotIndex] = clone;
            meshFilter.sharedMesh = clone;
            return clone;
        }

        private void ReleaseRuntimeMesh(int slotIndex)
        {
            if (_runtimeMeshes == null || slotIndex < 0 || slotIndex >= _runtimeMeshes.Length) return;

            Mesh mesh = _runtimeMeshes[slotIndex];
            if (mesh == null) return;

            _runtimeMeshes[slotIndex] = null;
            if (Application.isPlaying)
                Destroy(mesh);
            else
                DestroyImmediate(mesh);
        }

        private static float EvaluateRotate(float[] frames, int[] curveTypes, float[] curveSamples, float time)
        {
            if (frames == null || frames.Length < 2) return 0f;
            if (time < frames[0]) return 0f;
            if (time >= frames[frames.Length - 2]) return frames[frames.Length - 1];

            int frame = BinarySearch(frames, time, 2);
            float previous = frames[frame - 1];
            float frameTime = frames[frame];
            float percent = 1f - (time - frameTime) / (frames[frame - 2] - frameTime);
            percent = ApplyCurve(curveTypes, curveSamples, frame / 2 - 1, percent);

            float delta = frames[frame + 1] - previous;
            delta -= (16384 - (int)(16384.499999999996 - delta / 360f)) * 360f;
            return previous + delta * percent;
        }

        private static void EvaluatePair(float[] frames, int[] curveTypes, float[] curveSamples, float time, out float x, out float y)
        {
            x = 0f;
            y = 0f;
            if (frames == null || frames.Length < 3) return;
            if (time < frames[0]) return;

            if (time >= frames[frames.Length - 3])
            {
                x = frames[frames.Length - 2];
                y = frames[frames.Length - 1];
                return;
            }

            int frame = BinarySearch(frames, time, 3);
            x = frames[frame - 2];
            y = frames[frame - 1];
            float frameTime = frames[frame];
            float percent = 1f - (time - frameTime) / (frames[frame - 3] - frameTime);
            percent = ApplyCurve(curveTypes, curveSamples, frame / 3 - 1, percent);
            x += (frames[frame + 1] - x) * percent;
            y += (frames[frame + 2] - y) * percent;
        }

        private static void EvaluateColor(float[] frames, int[] curveTypes, float[] curveSamples, float time, out float r, out float g, out float b, out float a)
        {
            r = 1f;
            g = 1f;
            b = 1f;
            a = 1f;
            if (frames == null || frames.Length < 5) return;
            if (time < frames[0]) return;

            if (time >= frames[frames.Length - 5])
            {
                r = frames[frames.Length - 4];
                g = frames[frames.Length - 3];
                b = frames[frames.Length - 2];
                a = frames[frames.Length - 1];
                return;
            }

            int frame = BinarySearch(frames, time, 5);
            r = frames[frame - 4];
            g = frames[frame - 3];
            b = frames[frame - 2];
            a = frames[frame - 1];
            float frameTime = frames[frame];
            float percent = 1f - (time - frameTime) / (frames[frame - 5] - frameTime);
            percent = ApplyCurve(curveTypes, curveSamples, frame / 5 - 1, percent);
            r += (frames[frame + 1] - r) * percent;
            g += (frames[frame + 2] - g) * percent;
            b += (frames[frame + 3] - b) * percent;
            a += (frames[frame + 4] - a) * percent;
        }

        private static float ApplyCurve(int[] curveTypes, float[] curveSamples, int frameIndex, float percent)
        {
            percent = Mathf.Clamp01(percent);
            int curveType = GetInt(curveTypes, frameIndex, 0);
            if (curveType == CurveStepped) return 0f;
            if (curveType != CurveBezier || curveSamples == null) return percent;

            int offset = frameIndex * CurveSampleCount;
            if (offset < 0 || offset >= curveSamples.Length) return percent;
            float samplePosition = percent * (CurveSampleCount - 1);
            int sampleIndex = Mathf.FloorToInt(samplePosition);
            if (sampleIndex >= CurveSampleCount - 1)
                return curveSamples[Mathf.Min(offset + CurveSampleCount - 1, curveSamples.Length - 1)];

            float a = curveSamples[Mathf.Min(offset + sampleIndex, curveSamples.Length - 1)];
            float b = curveSamples[Mathf.Min(offset + sampleIndex + 1, curveSamples.Length - 1)];
            return Mathf.Lerp(a, b, samplePosition - sampleIndex);
        }

        private static int BinarySearch(float[] values, float target, int step)
        {
            int low = 0;
            int high = values.Length / step - 2;
            if (high == 0) return step;
            int current = (int)((uint)high >> 1);
            while (true)
            {
                if (values[(current + 1) * step] <= target)
                    low = current + 1;
                else
                    high = current;
                if (low == high) return (low + 1) * step;
                current = (int)((uint)(low + high) >> 1);
            }
        }

        private static int BinarySearch(float[] values, float target)
        {
            int low = 0;
            int high = values.Length - 2;
            if (high == 0) return 1;
            int current = (int)((uint)high >> 1);
            while (true)
            {
                if (values[current + 1] <= target)
                    low = current + 1;
                else
                    high = current;
                if (low == high) return low + 1;
                current = (int)((uint)(low + high) >> 1);
            }
        }

        private static bool HasTimelineStarted(float[] frames, float time)
        {
            return frames != null && frames.Length > 0 && time >= frames[0];
        }

        private bool IsValidBone(int index)
        {
            return index >= 0 && _bones != null && index < _bones.Length;
        }

        private bool IsValidSlot(int index)
        {
            return index >= 0 && _slots != null && index < _slots.Length;
        }

        private bool IsValidAttachment(int index)
        {
            return AnimationData != null && AnimationData.Attachments != null && index >= 0 && index < AnimationData.Attachments.Length;
        }

        private static Vector3[] EnsureVertices(Vector3[] values, int length)
        {
            return values != null && values.Length == length ? values : new Vector3[length];
        }

        private static Color32[] EnsureColors(Color32[] values, int length)
        {
            return values != null && values.Length == length ? values : new Color32[length];
        }

        private static int SafeLength(Array items)
        {
            return items == null ? 0 : items.Length;
        }

        private static T GetObject<T>(T[] items, int index) where T : class
        {
            return items != null && index >= 0 && index < items.Length ? items[index] : null;
        }

        private static int GetInt(int[] items, int index, int fallback)
        {
            return items != null && index >= 0 && index < items.Length ? items[index] : fallback;
        }

        private static float GetFloat(float[] items, int index)
        {
            return items != null && index >= 0 && index < items.Length ? items[index] : 0f;
        }

        private static float CosDeg(float degrees)
        {
            return Mathf.Cos(degrees * Mathf.Deg2Rad);
        }

        private static float SinDeg(float degrees)
        {
            return Mathf.Sin(degrees * Mathf.Deg2Rad);
        }

        private enum TranslateMode
        {
            Translate,
            Scale,
            Shear
        }

        private sealed class RuntimeBone
        {
            public float X;
            public float Y;
            public float Rotation;
            public float ScaleX = 1f;
            public float ScaleY = 1f;
            public float ShearX;
            public float ShearY;
            public float A;
            public float B;
            public float C;
            public float D;
            public float WorldX;
            public float WorldY;
        }

        private sealed class RuntimeSlot
        {
            public int AttachmentIndex = -1;
            public float R = 1f;
            public float G = 1f;
            public float B = 1f;
            public float A = 1f;
            public int SortingOrder;
        }
    }
}
