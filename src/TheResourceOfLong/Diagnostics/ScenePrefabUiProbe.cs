using System;
using System.Collections.Generic;
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
    internal static class ScenePrefabUiProbe
    {
        private const string LogFileName = "scene-prefab-ui-probe-log.csv";
        private const string UiPrefabLogFileName = "prefab-ui-probe-log.csv";
        private const string AreaHeroListPathMarker = "Canvas/AreaUIPanel/AreaUIBelow/AreaHeroScrollView/Viewport/Content/";
        private const string HeroSearchListPathMarker = "Canvas/HeroSearchPanel/HeroSearchRoot/HeroList/Viewport/Content/";
        private const int MaxFrames = 5;
        private static readonly object SyncRoot = new object();
        private static readonly List<ProbeSession> Sessions = new List<ProbeSession>();
        private static string _logPath;
        private static string _uiPrefabLogPath;
        private static bool _initialized;
        private static bool _enabled;
        private static bool _headerWritten;
        private static bool _uiPrefabHeaderWritten;

        public static void Initialize(string gameRoot)
        {
            if (_initialized) return;
            _initialized = true;

            ResourceProbeConfig config = UserConfigManager.LoadOrCreate(gameRoot);
            _enabled = config != null && config.EnableScenePrefabUiProbe;
            if (!_enabled) return;

            string directory = UserConfigManager.GetConfigDirectoryPath(gameRoot);
            Directory.CreateDirectory(directory);
            _logPath = Path.Combine(directory, LogFileName);
            _uiPrefabLogPath = Path.Combine(directory, UiPrefabLogFileName);
            _headerWritten = File.Exists(_logPath) && new FileInfo(_logPath).Length > 0;
            _uiPrefabHeaderWritten = File.Exists(_uiPrefabLogPath) && new FileInfo(_uiPrefabLogPath).Length > 0;
            LoggerManager.Info("ScenePrefabUiProbe enabled. Log file: " + _logPath);
        }

        public static void TrackAreaHeroList(HeroData heroData, Transform targetSkeletonParent, Transform root, Transform content, RawImage rawImage, RenderTexture renderTexture, string note)
        {
            if (!_enabled || targetSkeletonParent == null || root == null || content == null || rawImage == null) return;

            string targetPath = GetTransformPath(targetSkeletonParent);
            if (!IsListTargetPath(targetPath)) return;

            Sessions.Add(new ProbeSession
            {
                HeroId = heroData == null ? -1 : heroData.heroID,
                HeroName = heroData == null ? string.Empty : Safe(heroData.heroName),
                TargetPath = targetPath,
                RootPath = GetTransformPath(root),
                ContentPath = GetTransformPath(content),
                Note = note,
                Target = targetSkeletonParent,
                Root = root,
                Content = content,
                RawImage = rawImage,
                RenderTexture = renderTexture,
                StartFrame = Time.frameCount
            });
        }

        public static void LogUiPrefab(HeroData heroData, Transform targetSkeletonParent, Transform root, Transform content, RectTransform referenceRect, string note)
        {
            if (!_enabled || targetSkeletonParent == null || root == null || content == null) return;

            string targetPath = GetTransformPath(targetSkeletonParent);
            if (!IsListTargetPath(targetPath)) return;
            if (string.IsNullOrEmpty(_uiPrefabLogPath)) return;

            try
            {
                RectTransform targetRect = targetSkeletonParent.GetComponent<RectTransform>();
                RectTransform rootRect = root.GetComponent<RectTransform>();
                RectTransform contentRect = content.GetComponent<RectTransform>();
                GraphicProbe graphicProbe = GraphicProbe.From(content);
                RectProbe rootProbe = RectProbe.From(targetRect, rootRect);
                RectProbe contentProbe = RectProbe.From(targetRect, contentRect);

                StringBuilder builder = new StringBuilder();
                lock (SyncRoot)
                {
                    if (!_uiPrefabHeaderWritten)
                    {
                        builder.AppendLine("Timestamp,Scene,Frame,HeroID,HeroName,TargetPath,RootPath,ContentPath,Note,TargetActiveInHierarchy,RootActiveInHierarchy,ContentActiveInHierarchy,ReferencePath,TargetW,TargetH,ReferenceW,ReferenceH,RootW,RootH,RootAnchoredX,RootAnchoredY,RootScaleX,RootScaleY,RootMinXInTarget,RootMinYInTarget,RootMaxXInTarget,RootMaxYInTarget,RootIntersectsTarget,ContentW,ContentH,ContentAnchoredX,ContentAnchoredY,ContentScaleX,ContentScaleY,ContentMinXInTarget,ContentMinYInTarget,ContentMaxXInTarget,ContentMaxYInTarget,ContentIntersectsTarget,GraphicCount,EnabledGraphicCount,ActiveGraphicCount,CulledGraphicCount,FirstGraphicType,FirstGraphicPath,FirstGraphicEnabled,FirstGraphicActive,FirstGraphicCull,FirstGraphicAlpha,FirstGraphicColorA,FirstGraphicRaycastTarget,SkeletonGraphicCount,FirstSkeletonDataAsset,FirstSkeletonStartingAnimation");
                        _uiPrefabHeaderWritten = true;
                    }

                    builder.AppendLine(string.Join(",",
                        Csv(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture)),
                        Csv(GetSceneName()),
                        Csv(Time.frameCount.ToString(CultureInfo.InvariantCulture)),
                        Csv(heroData == null ? string.Empty : heroData.heroID.ToString(CultureInfo.InvariantCulture)),
                        Csv(heroData == null ? string.Empty : Safe(heroData.heroName)),
                        Csv(targetPath),
                        Csv(GetTransformPath(root)),
                        Csv(GetTransformPath(content)),
                        Csv(Safe(note)),
                        Csv(Bool(targetSkeletonParent.gameObject.activeInHierarchy)),
                        Csv(Bool(root.gameObject.activeInHierarchy)),
                        Csv(Bool(content.gameObject.activeInHierarchy)),
                        Csv(referenceRect == null ? string.Empty : GetTransformPath(referenceRect.transform)),
                        Csv(Width(targetRect)),
                        Csv(Height(targetRect)),
                        Csv(Width(referenceRect)),
                        Csv(Height(referenceRect)),
                        Csv(Width(rootRect)),
                        Csv(Height(rootRect)),
                        Csv(AnchoredX(rootRect)),
                        Csv(AnchoredY(rootRect)),
                        Csv(Format(root.localScale.x)),
                        Csv(Format(root.localScale.y)),
                        Csv(Format(rootProbe.MinX)),
                        Csv(Format(rootProbe.MinY)),
                        Csv(Format(rootProbe.MaxX)),
                        Csv(Format(rootProbe.MaxY)),
                        Csv(Bool(rootProbe.IntersectsTarget)),
                        Csv(Width(contentRect)),
                        Csv(Height(contentRect)),
                        Csv(AnchoredX(contentRect)),
                        Csv(AnchoredY(contentRect)),
                        Csv(Format(content.localScale.x)),
                        Csv(Format(content.localScale.y)),
                        Csv(Format(contentProbe.MinX)),
                        Csv(Format(contentProbe.MinY)),
                        Csv(Format(contentProbe.MaxX)),
                        Csv(Format(contentProbe.MaxY)),
                        Csv(Bool(contentProbe.IntersectsTarget)),
                        Csv(graphicProbe.GraphicCount.ToString(CultureInfo.InvariantCulture)),
                        Csv(graphicProbe.EnabledGraphicCount.ToString(CultureInfo.InvariantCulture)),
                        Csv(graphicProbe.ActiveGraphicCount.ToString(CultureInfo.InvariantCulture)),
                        Csv(graphicProbe.CulledGraphicCount.ToString(CultureInfo.InvariantCulture)),
                        Csv(graphicProbe.FirstGraphicType),
                        Csv(graphicProbe.FirstGraphicPath),
                        Csv(Bool(graphicProbe.FirstGraphicEnabled)),
                        Csv(Bool(graphicProbe.FirstGraphicActive)),
                        Csv(Bool(graphicProbe.FirstGraphicCull)),
                        Csv(Format(graphicProbe.FirstGraphicAlpha)),
                        Csv(Format(graphicProbe.FirstGraphicColorA)),
                        Csv(Bool(graphicProbe.FirstGraphicRaycastTarget)),
                        Csv(graphicProbe.SkeletonGraphicCount.ToString(CultureInfo.InvariantCulture)),
                        Csv(graphicProbe.FirstSkeletonDataAsset),
                        Csv(graphicProbe.FirstSkeletonStartingAnimation)));

                    File.AppendAllText(_uiPrefabLogPath, builder.ToString(), new UTF8Encoding(true));
                }
            }
            catch (Exception ex)
            {
                LoggerManager.Warning("PrefabUiProbe failed: " + ex.Message);
            }
        }

        public static void Update()
        {
            if (!_enabled || Sessions.Count <= 0) return;

            for (int i = Sessions.Count - 1; i >= 0; i--)
            {
                ProbeSession session = Sessions[i];
                try
                {
                    WriteFrame(session);
                }
                catch (Exception ex)
                {
                    LoggerManager.Warning("ScenePrefabUiProbe failed: " + ex.Message);
                    Sessions.RemoveAt(i);
                    continue;
                }

                session.FramesLogged++;
                if (session.FramesLogged >= MaxFrames)
                {
                    Sessions.RemoveAt(i);
                }
            }
        }

        private static void WriteFrame(ProbeSession session)
        {
            if (string.IsNullOrEmpty(_logPath)) return;

            Transform target = session.Target;
            Transform root = session.Root;
            Transform content = session.Content;
            RawImage rawImage = session.RawImage;
            RenderTexture renderTexture = session.RenderTexture;
            CanvasRenderer canvasRenderer = rawImage == null ? null : rawImage.canvasRenderer;
            RectTransform rootRect = root == null ? null : root.GetComponent<RectTransform>();
            RectTransform contentRect = content == null ? null : content.GetComponent<RectTransform>();

            StringBuilder builder = new StringBuilder();
            lock (SyncRoot)
            {
                if (!_headerWritten)
                {
                    builder.AppendLine("Timestamp,Scene,Frame,FrameOffset,HeroID,HeroName,TargetPath,RootPath,ContentPath,Note,TargetExists,TargetActiveSelf,TargetActiveInHierarchy,RootExists,RootActiveSelf,RootActiveInHierarchy,RootSiblingIndex,ContentExists,ContentActiveSelf,ContentActiveInHierarchy,RawImageExists,RawImageEnabled,RawImageActiveAndEnabled,RawImageTextureExists,RawImageColorA,CanvasRendererExists,CanvasRendererCull,CanvasRendererAlpha,RenderTextureExists,RenderTextureCreated,RenderTextureW,RenderTextureH,RootW,RootH,RootAnchoredX,RootAnchoredY,RootScaleX,RootScaleY,ContentW,ContentH,ContentAnchoredX,ContentAnchoredY,ContentScaleX,ContentScaleY,CurrentRootPath,CurrentContentPath");
                    _headerWritten = true;
                }

                builder.AppendLine(string.Join(",",
                    Csv(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture)),
                    Csv(GetSceneName()),
                    Csv(Time.frameCount.ToString(CultureInfo.InvariantCulture)),
                    Csv((Time.frameCount - session.StartFrame).ToString(CultureInfo.InvariantCulture)),
                    Csv(session.HeroId.ToString(CultureInfo.InvariantCulture)),
                    Csv(session.HeroName),
                    Csv(session.TargetPath),
                    Csv(session.RootPath),
                    Csv(session.ContentPath),
                    Csv(session.Note),
                    Csv(Bool(target != null)),
                    Csv(Bool(target != null && target.gameObject.activeSelf)),
                    Csv(Bool(target != null && target.gameObject.activeInHierarchy)),
                    Csv(Bool(root != null)),
                    Csv(Bool(root != null && root.gameObject.activeSelf)),
                    Csv(Bool(root != null && root.gameObject.activeInHierarchy)),
                    Csv(root == null ? string.Empty : root.GetSiblingIndex().ToString(CultureInfo.InvariantCulture)),
                    Csv(Bool(content != null)),
                    Csv(Bool(content != null && content.gameObject.activeSelf)),
                    Csv(Bool(content != null && content.gameObject.activeInHierarchy)),
                    Csv(Bool(rawImage != null)),
                    Csv(Bool(rawImage != null && rawImage.enabled)),
                    Csv(Bool(rawImage != null && rawImage.isActiveAndEnabled)),
                    Csv(Bool(rawImage != null && rawImage.texture != null)),
                    Csv(rawImage == null ? string.Empty : Format(rawImage.color.a)),
                    Csv(Bool(canvasRenderer != null)),
                    Csv(Bool(canvasRenderer != null && canvasRenderer.cull)),
                    Csv(canvasRenderer == null ? string.Empty : Format(canvasRenderer.GetAlpha())),
                    Csv(Bool(renderTexture != null)),
                    Csv(Bool(renderTexture != null && renderTexture.IsCreated())),
                    Csv(renderTexture == null ? string.Empty : renderTexture.width.ToString(CultureInfo.InvariantCulture)),
                    Csv(renderTexture == null ? string.Empty : renderTexture.height.ToString(CultureInfo.InvariantCulture)),
                    Csv(Width(rootRect)),
                    Csv(Height(rootRect)),
                    Csv(AnchoredX(rootRect)),
                    Csv(AnchoredY(rootRect)),
                    Csv(root == null ? string.Empty : Format(root.localScale.x)),
                    Csv(root == null ? string.Empty : Format(root.localScale.y)),
                    Csv(Width(contentRect)),
                    Csv(Height(contentRect)),
                    Csv(AnchoredX(contentRect)),
                    Csv(AnchoredY(contentRect)),
                    Csv(content == null ? string.Empty : Format(content.localScale.x)),
                    Csv(content == null ? string.Empty : Format(content.localScale.y)),
                    Csv(GetTransformPath(root)),
                    Csv(GetTransformPath(content))));

                File.AppendAllText(_logPath, builder.ToString(), new UTF8Encoding(true));
            }
        }

        private static string Width(RectTransform rect)
        {
            if (rect == null) return string.Empty;
            float width = Mathf.Abs(rect.rect.width);
            if (width <= 0f) width = Mathf.Abs(rect.sizeDelta.x);
            return Format(width);
        }

        private static string Height(RectTransform rect)
        {
            if (rect == null) return string.Empty;
            float height = Mathf.Abs(rect.rect.height);
            if (height <= 0f) height = Mathf.Abs(rect.sizeDelta.y);
            return Format(height);
        }

        private static string AnchoredX(RectTransform rect)
        {
            return rect == null ? string.Empty : Format(rect.anchoredPosition.x);
        }

        private static string AnchoredY(RectTransform rect)
        {
            return rect == null ? string.Empty : Format(rect.anchoredPosition.y);
        }

        private static string GetSceneName()
        {
            try
            {
                return SceneManager.GetActiveScene().name;
            }
            catch
            {
                return string.Empty;
            }
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

        private static string Bool(bool value)
        {
            return value ? "true" : "false";
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

        private static bool IsListTargetPath(string targetPath)
        {
            if (string.IsNullOrEmpty(targetPath)) return false;
            return targetPath.IndexOf(AreaHeroListPathMarker, StringComparison.Ordinal) >= 0 ||
                   targetPath.IndexOf(HeroSearchListPathMarker, StringComparison.Ordinal) >= 0;
        }

        private struct RectProbe
        {
            public float MinX;
            public float MinY;
            public float MaxX;
            public float MaxY;
            public bool IntersectsTarget;

            public static RectProbe From(RectTransform targetRect, RectTransform nodeRect)
            {
                RectProbe result = new RectProbe();
                if (targetRect == null || nodeRect == null) return result;

                Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(targetRect, nodeRect);
                result.MinX = bounds.min.x;
                result.MinY = bounds.min.y;
                result.MaxX = bounds.max.x;
                result.MaxY = bounds.max.y;

                Rect target = targetRect.rect;
                float targetMinX = target.xMin;
                float targetMaxX = target.xMax;
                float targetMinY = target.yMin;
                float targetMaxY = target.yMax;
                result.IntersectsTarget = result.MaxX >= targetMinX && result.MinX <= targetMaxX &&
                                          result.MaxY >= targetMinY && result.MinY <= targetMaxY;
                return result;
            }
        }

        private struct GraphicProbe
        {
            public int GraphicCount;
            public int EnabledGraphicCount;
            public int ActiveGraphicCount;
            public int CulledGraphicCount;
            public string FirstGraphicType;
            public string FirstGraphicPath;
            public bool FirstGraphicEnabled;
            public bool FirstGraphicActive;
            public bool FirstGraphicCull;
            public float FirstGraphicAlpha;
            public float FirstGraphicColorA;
            public bool FirstGraphicRaycastTarget;
            public int SkeletonGraphicCount;
            public string FirstSkeletonDataAsset;
            public string FirstSkeletonStartingAnimation;

            public static GraphicProbe From(Transform content)
            {
                GraphicProbe result = new GraphicProbe();
                if (content == null) return result;

                Graphic[] graphics = content.GetComponentsInChildren<Graphic>(true);
                result.GraphicCount = graphics == null ? 0 : graphics.Length;
                for (int i = 0; i < result.GraphicCount; i++)
                {
                    Graphic graphic = graphics[i];
                    if (graphic == null) continue;

                    if (graphic.enabled) result.EnabledGraphicCount++;
                    if (graphic.gameObject.activeInHierarchy) result.ActiveGraphicCount++;
                    CanvasRenderer renderer = graphic.canvasRenderer;
                    bool cull = renderer != null && renderer.cull;
                    if (cull) result.CulledGraphicCount++;

                    if (!string.IsNullOrEmpty(result.FirstGraphicType)) continue;

                    result.FirstGraphicType = graphic.GetType().FullName;
                    result.FirstGraphicPath = GetTransformPathStatic(graphic.transform);
                    result.FirstGraphicEnabled = graphic.enabled;
                    result.FirstGraphicActive = graphic.gameObject.activeInHierarchy;
                    result.FirstGraphicCull = cull;
                    result.FirstGraphicAlpha = renderer == null ? 0f : renderer.GetAlpha();
                    result.FirstGraphicColorA = graphic.color.a;
                    result.FirstGraphicRaycastTarget = graphic.raycastTarget;
                }

                SkeletonGraphic[] skeletonGraphics = content.GetComponentsInChildren<SkeletonGraphic>(true);
                result.SkeletonGraphicCount = skeletonGraphics == null ? 0 : skeletonGraphics.Length;
                if (result.SkeletonGraphicCount > 0 && skeletonGraphics[0] != null)
                {
                    SkeletonGraphic skeletonGraphic = skeletonGraphics[0];
                    result.FirstSkeletonDataAsset = skeletonGraphic.skeletonDataAsset == null ? string.Empty : skeletonGraphic.skeletonDataAsset.name;
                    result.FirstSkeletonStartingAnimation = skeletonGraphic.startingAnimation ?? string.Empty;
                }

                return result;
            }
        }

        private static string GetTransformPathStatic(Transform transform)
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

        private sealed class ProbeSession
        {
            public int HeroId;
            public string HeroName;
            public string TargetPath;
            public string RootPath;
            public string ContentPath;
            public string Note;
            public Transform Target;
            public Transform Root;
            public Transform Content;
            public RawImage RawImage;
            public RenderTexture RenderTexture;
            public int StartFrame;
            public int FramesLogged;
        }
    }
}
