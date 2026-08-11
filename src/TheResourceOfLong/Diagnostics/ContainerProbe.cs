using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using Il2Cpp;
using Il2CppSpine.Unity;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TheResourceOfLong
{
    internal static class ContainerProbe
    {
        private const string LogFileName = "container-probe-log.csv";
        private const string SpeSkeletonLayoutLogFileName = "spe-skeleton-layout-log.csv";
        private const string VisibleOverlayName = "TheResourceOfLongContainerProbeVisibleOverlay";
        private const string ReferenceOverlayName = "TheResourceOfLongContainerProbeReferenceOverlay";
        private static readonly object SyncRoot = new object();
        private static ResourceProbeConfig _config = ResourceProbeConfig.CreateDefault();
        private static string _logPath;
        private static string _speSkeletonLayoutLogPath;
        private static bool _initialized;
        private static bool _headerWritten;
        private static bool _speSkeletonLayoutHeaderWritten;
        private static bool _overlayLogged;

        public static void Initialize(string gameRoot)
        {
            if (_initialized) return;
            _initialized = true;

            _config = UserConfigManager.LoadOrCreate(gameRoot);
            if (_config == null || !_config.EnableContainerProbe) return;

            string directory = UserConfigManager.GetConfigDirectoryPath(gameRoot);
            Directory.CreateDirectory(directory);
            _logPath = Path.Combine(directory, LogFileName);
            _speSkeletonLayoutLogPath = Path.Combine(directory, SpeSkeletonLayoutLogFileName);
            RotateLegacyLogIfNeeded(_logPath);
            _headerWritten = File.Exists(_logPath) && new FileInfo(_logPath).Length > 0;
            _speSkeletonLayoutHeaderWritten = File.Exists(_speSkeletonLayoutLogPath) && new FileInfo(_speSkeletonLayoutLogPath).Length > 0;

            LoggerManager.Info("ContainerProbe enabled. Log file: " + _logPath);
        }

        public static void Log(HeroData heroData, Transform targetSkeletonParent, RectTransform referenceRect, string phase)
        {
            if (!_initialized || _config == null || !_config.EnableContainerProbe) return;
            if (heroData == null || targetSkeletonParent == null) return;

            try
            {
                RectTransform parentRect = targetSkeletonParent.GetComponent<RectTransform>();
                RectTransform effectiveRect = referenceRect != null ? referenceRect : parentRect;
                if (effectiveRect == null) return;

                ProbeRect rect = ProbeRect.From(effectiveRect);
                ProbeRect parent = parentRect == null ? ProbeRect.Empty : ProbeRect.From(parentRect);
                ProbeVisual visual = DrawOverlay(parentRect, effectiveRect);
                string scene = GetSceneName();
                string caller = _config.LogStackTrace ? GetCaller() : string.Empty;
                string targetPath = GetTransformPath(targetSkeletonParent);
                string referencePath = referenceRect == null ? string.Empty : GetTransformPath(referenceRect.transform);

                string message = "ContainerProbe phase=" + phase +
                                 ", heroID=" + heroData.heroID +
                                 ", heroName=" + Safe(heroData.heroName) +
                                 ", containerW=" + Format(rect.Width) +
                                 ", containerH=" + Format(rect.Height) +
                                 ", parentW=" + Format(parent.Width) +
                                 ", parentH=" + Format(parent.Height) +
                                 ", scene=" + scene +
                                 (string.IsNullOrEmpty(caller) ? string.Empty : ", caller=" + caller);

                LoggerManager.Debug(message);
                WriteCsv(heroData, phase, scene, caller, targetPath, referencePath, rect, parent, visual);
            }
            catch (Exception ex)
            {
                LoggerManager.Warning("ContainerProbe failed: " + ex.Message);
            }
        }

        public static void LogSpeSkeletonLayout(HeroData heroData, Transform targetSkeletonParent, Transform node, string phase, string note)
        {
            if (!_initialized || _config == null || !_config.EnableContainerProbe) return;
            if (targetSkeletonParent == null || node == null) return;

            try
            {
                RectTransform targetRect = targetSkeletonParent.GetComponent<RectTransform>();
                RectTransform nodeRect = node.GetComponent<RectTransform>();
                if (nodeRect == null) return;

                ProbeRect target = targetRect == null ? ProbeRect.Empty : ProbeRect.From(targetRect);
                ProbeRect rect = ProbeRect.From(nodeRect);
                LayoutWorldRect worldRect = LayoutWorldRect.From(targetRect, nodeRect);
                SkeletonGraphic skeletonGraphic = node.GetComponent<SkeletonGraphic>();
                string skeletonDataName = string.Empty;
                if (skeletonGraphic != null && skeletonGraphic.skeletonDataAsset != null)
                {
                    skeletonDataName = skeletonGraphic.skeletonDataAsset.name;
                }

                string message = "SpeSkeletonLayout phase=" + phase +
                                 ", note=" + Safe(note) +
                                 ", heroID=" + (heroData == null ? string.Empty : heroData.heroID.ToString(CultureInfo.InvariantCulture)) +
                                 ", heroName=" + (heroData == null ? string.Empty : Safe(heroData.heroName)) +
                                 ", node=" + GetTransformPath(node) +
                                 ", target=" + GetTransformPath(targetSkeletonParent) +
                                 ", nodeW=" + Format(rect.Width) +
                                 ", nodeH=" + Format(rect.Height) +
                                 ", localScale=" + Format(node.localScale.x) + "/" + Format(node.localScale.y) +
                                 ", localPos=" + Format(node.localPosition.x) + "/" + Format(node.localPosition.y);

                LoggerManager.Debug(message);
                WriteSpeSkeletonLayoutCsv(heroData, phase, note, targetSkeletonParent, node, target, rect, worldRect, skeletonDataName);
            }
            catch (Exception ex)
            {
                LoggerManager.Warning("SpeSkeletonLayout probe failed: " + ex.Message);
            }
        }

        private static void WriteSpeSkeletonLayoutCsv(
            HeroData heroData,
            string phase,
            string note,
            Transform targetSkeletonParent,
            Transform node,
            ProbeRect target,
            ProbeRect rect,
            LayoutWorldRect worldRect,
            string skeletonDataName)
        {
            if (string.IsNullOrEmpty(_speSkeletonLayoutLogPath)) return;

            StringBuilder builder = new StringBuilder();
            lock (SyncRoot)
            {
                if (!_speSkeletonLayoutHeaderWritten)
                {
                    builder.AppendLine("Timestamp,Scene,Phase,Note,HeroID,HeroName,TargetPath,NodePath,SkeletonDataAsset,TargetW,TargetH,NodeW,NodeH,NodeAnchoredX,NodeAnchoredY,NodeSizeDeltaX,NodeSizeDeltaY,NodeAnchorMinX,NodeAnchorMinY,NodeAnchorMaxX,NodeAnchorMaxY,NodePivotX,NodePivotY,NodeLocalX,NodeLocalY,NodeLocalZ,NodeScaleX,NodeScaleY,NodeScaleZ,NodeMinXInTarget,NodeMinYInTarget,NodeMaxXInTarget,NodeMaxYInTarget,NodeCenterXInTarget,NodeCenterYInTarget");
                    _speSkeletonLayoutHeaderWritten = true;
                }

                builder.AppendLine(string.Join(",",
                    Csv(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture)),
                    Csv(GetSceneName()),
                    Csv(phase),
                    Csv(Safe(note)),
                    Csv(heroData == null ? string.Empty : heroData.heroID.ToString(CultureInfo.InvariantCulture)),
                    Csv(heroData == null ? string.Empty : Safe(heroData.heroName)),
                    Csv(GetTransformPath(targetSkeletonParent)),
                    Csv(GetTransformPath(node)),
                    Csv(Safe(skeletonDataName)),
                    Csv(Format(target.Width)),
                    Csv(Format(target.Height)),
                    Csv(Format(rect.Width)),
                    Csv(Format(rect.Height)),
                    Csv(Format(rect.AnchoredX)),
                    Csv(Format(rect.AnchoredY)),
                    Csv(Format(rect.SizeDeltaX)),
                    Csv(Format(rect.SizeDeltaY)),
                    Csv(Format(rect.AnchorMinX)),
                    Csv(Format(rect.AnchorMinY)),
                    Csv(Format(rect.AnchorMaxX)),
                    Csv(Format(rect.AnchorMaxY)),
                    Csv(Format(rect.PivotX)),
                    Csv(Format(rect.PivotY)),
                    Csv(Format(node.localPosition.x)),
                    Csv(Format(node.localPosition.y)),
                    Csv(Format(node.localPosition.z)),
                    Csv(Format(node.localScale.x)),
                    Csv(Format(node.localScale.y)),
                    Csv(Format(node.localScale.z)),
                    Csv(Format(worldRect.MinX)),
                    Csv(Format(worldRect.MinY)),
                    Csv(Format(worldRect.MaxX)),
                    Csv(Format(worldRect.MaxY)),
                    Csv(Format(worldRect.CenterX)),
                    Csv(Format(worldRect.CenterY))));

                File.AppendAllText(_speSkeletonLayoutLogPath, builder.ToString(), new UTF8Encoding(true));
            }
        }

        private static void WriteCsv(HeroData heroData, string phase, string scene, string caller, string targetPath, string referencePath, ProbeRect rect, ProbeRect parent, ProbeVisual visual)
        {
            if (string.IsNullOrEmpty(_logPath)) return;

            StringBuilder builder = new StringBuilder();
            lock (SyncRoot)
            {
                if (!_headerWritten)
                {
                    builder.AppendLine("Timestamp,Scene,Phase,HeroID,HeroName,TargetPath,ReferencePath,ContainerW,ContainerH,ParentW,ParentH,AnchoredX,AnchoredY,SizeDeltaX,SizeDeltaY,AnchorMinX,AnchorMinY,AnchorMaxX,AnchorMaxY,PivotX,PivotY,VisibleW,VisibleH,ReferenceVisualW,ReferenceVisualH,ReferenceCenterInVisibleX,ReferenceCenterInVisibleY,Caller");
                    _headerWritten = true;
                }

                builder.AppendLine(string.Join(",",
                    Csv(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture)),
                    Csv(scene),
                    Csv(phase),
                    Csv(heroData.heroID.ToString(CultureInfo.InvariantCulture)),
                    Csv(Safe(heroData.heroName)),
                    Csv(targetPath),
                    Csv(referencePath),
                    Csv(Format(rect.Width)),
                    Csv(Format(rect.Height)),
                    Csv(Format(parent.Width)),
                    Csv(Format(parent.Height)),
                    Csv(Format(rect.AnchoredX)),
                    Csv(Format(rect.AnchoredY)),
                    Csv(Format(rect.SizeDeltaX)),
                    Csv(Format(rect.SizeDeltaY)),
                    Csv(Format(rect.AnchorMinX)),
                    Csv(Format(rect.AnchorMinY)),
                    Csv(Format(rect.AnchorMaxX)),
                    Csv(Format(rect.AnchorMaxY)),
                    Csv(Format(rect.PivotX)),
                    Csv(Format(rect.PivotY)),
                    Csv(Format(visual.VisibleW)),
                    Csv(Format(visual.VisibleH)),
                    Csv(Format(visual.ReferenceW)),
                    Csv(Format(visual.ReferenceH)),
                    Csv(Format(visual.ReferenceCenterX)),
                    Csv(Format(visual.ReferenceCenterY)),
                    Csv(caller)));

                File.AppendAllText(_logPath, builder.ToString(), new UTF8Encoding(true));
            }
        }

        private static ProbeVisual DrawOverlay(RectTransform parentRect, RectTransform referenceRect)
        {
            if (_config == null || !_config.EnableContainerProbeOverlay) return ProbeVisual.Empty;
            if (referenceRect == null) return ProbeVisual.Empty;
            if (parentRect == null) parentRect = referenceRect;

            RectTransform visibleRect = UsesRectClipping(parentRect) ? parentRect : referenceRect;
            RectTransform visibleOverlay = GetOrCreateOverlay(parentRect, visibleRect, VisibleOverlayName, visibleRect == parentRect);
            RectTransform referenceOverlay = GetOrCreateOverlay(parentRect, referenceRect, ReferenceOverlayName, false);
            if (visibleOverlay == null || referenceOverlay == null) return ProbeVisual.Empty;

            visibleOverlay.SetAsLastSibling();
            referenceOverlay.SetAsLastSibling();
            if (!_overlayLogged)
            {
                _overlayLogged = true;
                LoggerManager.Debug("ContainerProbe overlay enabled.");
            }

            DrawOverlayLines(visibleOverlay, new Color(0.1f, 1f, 0.25f, 0.95f), new Color(1f, 0.85f, 0.1f, 0.8f), 3f);
            DrawOverlayLines(referenceOverlay, new Color(1f, 0.1f, 0.1f, 0.95f), new Color(0.2f, 0.8f, 1f, 0.75f), 2f);
            return ProbeVisual.FromOverlay(visibleOverlay, referenceOverlay);
        }

        private static bool UsesRectClipping(RectTransform rect)
        {
            if (rect == null) return false;

            try
            {
                RectMask2D rectMask = rect.GetComponent<RectMask2D>();
                if (rectMask != null && rectMask.enabled) return true;

                Mask mask = rect.GetComponent<Mask>();
                if (mask != null && mask.enabled) return true;
            }
            catch
            {
                return false;
            }

            return false;
        }

        private static void RotateLegacyLogIfNeeded(string logPath)
        {
            try
            {
                if (!File.Exists(logPath)) return;
                FileInfo info = new FileInfo(logPath);
                if (info.Length <= 0) return;

                string firstLine;
                using (StreamReader reader = new StreamReader(logPath, Encoding.UTF8, true))
                {
                    firstLine = reader.ReadLine();
                }

                if (!string.IsNullOrEmpty(firstLine) && firstLine.IndexOf("ReferenceCenterInVisibleX", StringComparison.Ordinal) >= 0) return;

                string backupPath = Path.Combine(
                    Path.GetDirectoryName(logPath) ?? string.Empty,
                    Path.GetFileNameWithoutExtension(logPath) + "." + DateTime.Now.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture) + ".bak.csv");
                File.Move(logPath, backupPath);
                LoggerManager.Info("ContainerProbe legacy log backed up: " + backupPath);
            }
            catch (Exception ex)
            {
                LoggerManager.Warning("ContainerProbe legacy log rotation failed: " + ex.Message);
            }
        }

        private static void DrawOverlayLines(RectTransform overlay, Color borderColor, Color centerColor, float borderThickness)
        {
            SetLine(overlay, "Top", new Vector2(0.5f, 1f), new Vector2(1f, 0f), new Vector2(0f, -1f), borderColor, borderThickness);
            SetLine(overlay, "Bottom", new Vector2(0.5f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), borderColor, borderThickness);
            SetLine(overlay, "Left", new Vector2(0f, 0.5f), new Vector2(0f, 1f), new Vector2(1f, 0f), borderColor, borderThickness);
            SetLine(overlay, "Right", new Vector2(1f, 0.5f), new Vector2(0f, 1f), new Vector2(-1f, 0f), borderColor, borderThickness);
            SetLine(overlay, "CenterV", new Vector2(0.5f, 0.5f), new Vector2(0f, 1f), Vector2.zero, centerColor, 1f);
            SetLine(overlay, "CenterH", new Vector2(0.5f, 0.5f), new Vector2(1f, 0f), Vector2.zero, centerColor, 1f);
        }

        private static RectTransform GetOrCreateOverlay(RectTransform parentRect, RectTransform referenceRect, string overlayName, bool stretchToParent)
        {
            Transform existing = parentRect.Find(overlayName);
            RectTransform overlay = existing == null ? null : existing.GetComponent<RectTransform>();
            if (overlay != null)
            {
                CopyOverlayRect(parentRect, referenceRect, overlay, stretchToParent);
                return overlay;
            }

            GameObject overlayObject = new GameObject(overlayName);
            overlayObject.layer = parentRect.gameObject.layer;
            overlayObject.transform.SetParent(parentRect, false);

            CanvasGroup canvasGroup = overlayObject.AddComponent<CanvasGroup>();
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;

            overlay = overlayObject.AddComponent<RectTransform>();
            CopyOverlayRect(parentRect, referenceRect, overlay, stretchToParent);
            return overlay;
        }

        private static void CopyOverlayRect(RectTransform parentRect, RectTransform referenceRect, RectTransform overlay, bool stretchToParent)
        {
            if (overlay == null || referenceRect == null) return;

            if (stretchToParent || referenceRect == parentRect)
            {
                overlay.anchorMin = Vector2.zero;
                overlay.anchorMax = Vector2.one;
                overlay.pivot = new Vector2(0.5f, 0.5f);
                overlay.anchoredPosition = Vector2.zero;
                overlay.sizeDelta = Vector2.zero;
            }
            else
            {
                overlay.anchorMin = referenceRect.anchorMin;
                overlay.anchorMax = referenceRect.anchorMax;
                overlay.pivot = referenceRect.pivot;
                overlay.anchoredPosition = referenceRect.anchoredPosition;
                overlay.sizeDelta = referenceRect.sizeDelta;
            }

            overlay.localRotation = Quaternion.identity;
            overlay.localScale = Vector3.one;
        }

        private static void SetLine(RectTransform parent, string name, Vector2 anchor, Vector2 stretch, Vector2 offset, Color color, float thickness)
        {
            RectTransform line = GetOrCreateLine(parent, name, color);
            line.anchorMin = anchor;
            line.anchorMax = anchor;
            line.pivot = new Vector2(0.5f, 0.5f);
            line.anchoredPosition = offset;

            Vector2 parentSize = parent.rect.size;
            float width = stretch.x > 0f ? Mathf.Abs(parentSize.x) : thickness;
            float height = stretch.y > 0f ? Mathf.Abs(parentSize.y) : thickness;
            line.sizeDelta = new Vector2(width, height);
            line.localScale = Vector3.one;
        }

        private static RectTransform GetOrCreateLine(RectTransform parent, string name, Color color)
        {
            Transform existing = parent.Find(name);
            RectTransform rect = existing == null ? null : existing.GetComponent<RectTransform>();
            if (rect != null)
            {
                Image existingImage = rect.GetComponent<Image>();
                if (existingImage != null) existingImage.color = color;
                return rect;
            }

            GameObject lineObject = new GameObject(name);
            lineObject.layer = parent.gameObject.layer;
            lineObject.transform.SetParent(parent, false);

            rect = lineObject.AddComponent<RectTransform>();
            Image image = lineObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return rect;
        }

        private static string GetSceneName()
        {
            try
            {
                return SceneManager.GetActiveScene().name ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string GetCaller()
        {
            try
            {
                StackTrace trace = new StackTrace();
                for (int i = 0; i < trace.FrameCount; i++)
                {
                    StackFrame frame = trace.GetFrame(i);
                    if (frame == null) continue;

                    var method = frame.GetMethod();
                    if (method == null || method.DeclaringType == null) continue;

                    string typeName = method.DeclaringType.FullName ?? method.DeclaringType.Name;
                    if (typeName.StartsWith("TheResourceOfLong.", StringComparison.Ordinal)) continue;
                    if (typeName.StartsWith("HarmonyLib.", StringComparison.Ordinal)) continue;

                    return typeName + "." + method.Name;
                }
            }
            catch
            {
            }

            return string.Empty;
        }

        private static string GetTransformPath(Transform transform)
        {
            if (transform == null) return string.Empty;

            StringBuilder builder = new StringBuilder(transform.name);
            Transform current = transform.parent;
            while (current != null)
            {
                builder.Insert(0, current.name + "/");
                current = current.parent;
            }

            return builder.ToString();
        }

        private static string Safe(string value)
        {
            return value ?? string.Empty;
        }

        private static string Format(float value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static string Csv(string value)
        {
            if (value == null) value = string.Empty;
            bool mustQuote = value.IndexOf(',') >= 0 || value.IndexOf('"') >= 0 || value.IndexOf('\r') >= 0 || value.IndexOf('\n') >= 0;
            value = value.Replace("\"", "\"\"");
            return mustQuote ? "\"" + value + "\"" : value;
        }

        private struct ProbeRect
        {
            public static readonly ProbeRect Empty = new ProbeRect();

            public float Width;
            public float Height;
            public float AnchoredX;
            public float AnchoredY;
            public float SizeDeltaX;
            public float SizeDeltaY;
            public float AnchorMinX;
            public float AnchorMinY;
            public float AnchorMaxX;
            public float AnchorMaxY;
            public float PivotX;
            public float PivotY;

            public static ProbeRect From(RectTransform rectTransform)
            {
                Rect rect = rectTransform.rect;
                ProbeRect result = new ProbeRect();
                result.Width = Mathf.Abs(rect.width);
                result.Height = Mathf.Abs(rect.height);
                if (result.Width <= 0f) result.Width = Mathf.Abs(rectTransform.sizeDelta.x);
                if (result.Height <= 0f) result.Height = Mathf.Abs(rectTransform.sizeDelta.y);
                result.AnchoredX = rectTransform.anchoredPosition.x;
                result.AnchoredY = rectTransform.anchoredPosition.y;
                result.SizeDeltaX = rectTransform.sizeDelta.x;
                result.SizeDeltaY = rectTransform.sizeDelta.y;
                result.AnchorMinX = rectTransform.anchorMin.x;
                result.AnchorMinY = rectTransform.anchorMin.y;
                result.AnchorMaxX = rectTransform.anchorMax.x;
                result.AnchorMaxY = rectTransform.anchorMax.y;
                result.PivotX = rectTransform.pivot.x;
                result.PivotY = rectTransform.pivot.y;
                return result;
            }
        }

        private struct ProbeVisual
        {
            public static readonly ProbeVisual Empty = new ProbeVisual();

            public float VisibleW;
            public float VisibleH;
            public float ReferenceW;
            public float ReferenceH;
            public float ReferenceCenterX;
            public float ReferenceCenterY;

            public static ProbeVisual FromOverlay(RectTransform visibleOverlay, RectTransform referenceOverlay)
            {
                ProbeVisual result = new ProbeVisual();
                if (visibleOverlay == null || referenceOverlay == null) return result;

                visibleOverlay.ForceUpdateRectTransforms();
                referenceOverlay.ForceUpdateRectTransforms();

                result.VisibleW = Mathf.Abs(visibleOverlay.rect.width);
                result.VisibleH = Mathf.Abs(visibleOverlay.rect.height);
                if (result.VisibleW <= 0f) result.VisibleW = Mathf.Abs(visibleOverlay.sizeDelta.x);
                if (result.VisibleH <= 0f) result.VisibleH = Mathf.Abs(visibleOverlay.sizeDelta.y);

                try
                {
                    Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(visibleOverlay, referenceOverlay);
                    result.ReferenceW = Mathf.Abs(bounds.size.x);
                    result.ReferenceH = Mathf.Abs(bounds.size.y);
                    Vector2 visibleCenter = visibleOverlay.rect.center;
                    result.ReferenceCenterX = bounds.center.x - visibleCenter.x;
                    result.ReferenceCenterY = bounds.center.y - visibleCenter.y;
                }
                catch
                {
                    result.ReferenceW = 0f;
                    result.ReferenceH = 0f;
                }

                if (result.ReferenceW > 0f && result.ReferenceH > 0f) return result;

                Vector2 visibleSize = GetRectSize(visibleOverlay);
                Vector2 referenceSize = GetRectSize(referenceOverlay);
                Vector2 visibleCenterInParent = GetRectCenterInParent(visibleOverlay, visibleSize);
                Vector2 referenceCenterInParent = GetRectCenterInParent(referenceOverlay, referenceSize);
                result.ReferenceW = referenceSize.x;
                result.ReferenceH = referenceSize.y;
                result.ReferenceCenterX = referenceCenterInParent.x - visibleCenterInParent.x;
                result.ReferenceCenterY = referenceCenterInParent.y - visibleCenterInParent.y;
                return result;
            }

            private static Vector2 GetRectSize(RectTransform rect)
            {
                if (rect == null) return Vector2.zero;
                Rect localRect = rect.rect;
                float width = Mathf.Abs(localRect.width);
                float height = Mathf.Abs(localRect.height);
                if (width <= 0f) width = Mathf.Abs(rect.sizeDelta.x);
                if (height <= 0f) height = Mathf.Abs(rect.sizeDelta.y);
                return new Vector2(width, height);
            }

            private static Vector2 GetRectCenterInParent(RectTransform rect, Vector2 size)
            {
                if (rect == null) return Vector2.zero;

                RectTransform parent = rect.parent == null ? null : rect.parent.GetComponent<RectTransform>();
                Vector2 parentSize = parent == null ? Vector2.zero : GetRectSize(parent);
                Vector2 anchorCenter = (rect.anchorMin + rect.anchorMax) * 0.5f;
                float x = rect.anchoredPosition.x + (anchorCenter.x - 0.5f) * parentSize.x + (0.5f - rect.pivot.x) * size.x;
                float y = rect.anchoredPosition.y + (anchorCenter.y - 0.5f) * parentSize.y + (0.5f - rect.pivot.y) * size.y;
                return new Vector2(x, y);
            }
        }

        private struct LayoutWorldRect
        {
            public float MinX;
            public float MinY;
            public float MaxX;
            public float MaxY;
            public float CenterX;
            public float CenterY;

            public static LayoutWorldRect From(RectTransform targetRect, RectTransform nodeRect)
            {
                LayoutWorldRect result = new LayoutWorldRect();
                if (nodeRect == null) return result;

                Vector3[] corners = new Vector3[4];
                nodeRect.GetWorldCorners(corners);

                for (int i = 0; i < corners.Length; i++)
                {
                    if (targetRect != null)
                    {
                        corners[i] = targetRect.InverseTransformPoint(corners[i]);
                    }
                }

                result.MinX = corners[0].x;
                result.MaxX = corners[0].x;
                result.MinY = corners[0].y;
                result.MaxY = corners[0].y;

                for (int i = 1; i < corners.Length; i++)
                {
                    result.MinX = Mathf.Min(result.MinX, corners[i].x);
                    result.MaxX = Mathf.Max(result.MaxX, corners[i].x);
                    result.MinY = Mathf.Min(result.MinY, corners[i].y);
                    result.MaxY = Mathf.Max(result.MaxY, corners[i].y);
                }

                result.CenterX = (result.MinX + result.MaxX) * 0.5f;
                result.CenterY = (result.MinY + result.MaxY) * 0.5f;
                return result;
            }
        }
    }
}
