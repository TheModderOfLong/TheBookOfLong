using System;
using System.Collections.Generic;
using Il2Cpp;
using Il2CppInterop.Runtime;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TheExtensionOfLong
{
    internal static class ModLoadConfigUguiWindow
    {
        private const float UiScale = 1.25f;
        private const float PanelWidth = 960f;
        private const float PanelHeight = 640f;
        private const float RowHeight = 34f;
        private const float ListLeft = -456f;
        private const float ListRight = 162f;
        private const float DetailLeft = 196f;
        private const float DetailRight = 456f;

        private static ModLoadConfigWindowContext _context;
        private static GameObject _root;
        private static GameObject _panel;
        private static GameObject _contentRoot;
        private static RectTransform _contentRect;
        private static TMP_FontAsset _tmpFont;
        private static bool _useTmp;
        private static LabelHandle _statusLabel;
        private static LabelHandle _messageLabel;
        private static LabelHandle _configPathLabel;
        private static LabelHandle _modsPathLabel;
        private static LabelHandle _detailTitleLabel;
        private static LabelHandle _detailVersionLabel;
        private static LabelHandle _detailAuthorLabel;
        private static LabelHandle _detailFolderLabel;
        private static LabelHandle _detailDescLabel;
        private static RectTransform _detailContentRect;
        private static Canvas _rootCanvas;
        private static GraphicRaycaster _rootRaycaster;
        private static readonly List<RaycasterState> DisabledRaycasters = new List<RaycasterState>();

        public static bool Open(ModLoadConfigWindowContext context)
        {
            _context = context;
            return TryCreateWindow();
        }

        public static void Close()
        {
            RestoreOtherRaycasters();

            if (_root != null)
            {
                Object.Destroy(_root);
                _root = null;
            }

            _panel = null;
            _contentRoot = null;
            _contentRect = null;
            _context = null;
            _statusLabel = null;
            _messageLabel = null;
            _configPathLabel = null;
            _modsPathLabel = null;
            _detailTitleLabel = null;
            _detailVersionLabel = null;
            _detailAuthorLabel = null;
            _detailFolderLabel = null;
            _detailDescLabel = null;
            _detailContentRect = null;
            _rootCanvas = null;
            _rootRaycaster = null;
        }

        public static void Refresh()
        {
            if (_root == null || _context == null) return;
            UpdateStaticTexts();
            RebuildRows();
            UpdateDetailTexts();
        }

        public static void OnUpdate()
        {
            EnsureTopMost();
            DisableOtherRaycasters();
        }

        private static bool TryCreateWindow()
        {
            try
            {
                EventSystem eventSystem = EventSystem.current;
                if (eventSystem == null)
                {
                    eventSystem = FindComponentInScene<EventSystem>();
                }
                if (eventSystem == null)
                {
                    LoggerManager.Warning("ModLoadConfigUguiWindow: 场景中未找到 EventSystem");
                    return false;
                }

                _tmpFont = FindTmpFont();
                _useTmp = _tmpFont != null;

                _root = new GameObject("ModLoadConfigUguiWindow");

                _rootCanvas = SafeAddComponent<Canvas>(_root);
                _rootCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                _rootCanvas.overrideSorting = true;
                _rootCanvas.sortingOrder = short.MaxValue;
                _rootRaycaster = SafeAddComponent<GraphicRaycaster>(_root);

                CanvasScaler canvasScaler = SafeAddComponent<CanvasScaler>(_root);
                canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
                canvasScaler.scaleFactor = UiScale;

                CanvasGroup canvasGroup = SafeAddComponent<CanvasGroup>(_root);
                canvasGroup.blocksRaycasts = true;
                canvasGroup.interactable = true;

                RectTransform rootRect = GetRectTransform(_root);
                rootRect.anchorMin = Vector2.zero;
                rootRect.anchorMax = Vector2.one;
                rootRect.offsetMin = Vector2.zero;
                rootRect.offsetMax = Vector2.zero;

                GameObject overlay = CreateImage(_root, "Overlay", new Color(0f, 0f, 0f, 0.48f),
                    Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                Image overlayImage = overlay.GetComponent(Il2CppType.Of<Image>()).TryCast<Image>();
                overlayImage.raycastTarget = true;

                _panel = CreateImage(_root, "Panel", new Color(0.12f, 0.13f, 0.15f, 0.97f),
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(-PanelWidth / 2f, -PanelHeight / 2f),
                    new Vector2(PanelWidth / 2f, PanelHeight / 2f));
                AddBorder(_panel, new Color(0.36f, 0.38f, 0.42f, 0.95f));

                BuildChrome();
                BuildScrollArea();
                BuildDetailArea();
                UpdateStaticTexts();
                RebuildRows();
                UpdateDetailTexts();
                DisableOtherRaycasters();
                EnsureTopMost();
                return true;
            }
            catch (Exception ex)
            {
                LoggerManager.Error("ModLoadConfigUguiWindow: 创建失败 - " + ex.Message + "\n" + ex.StackTrace);
                Close();
                return false;
            }
        }

        private static void BuildChrome()
        {
            CreateLabel(_panel, "Title", "《龙之书 Mod》管理界面", 22, Color.white, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(-PanelWidth / 2f + 140f, -46f),
                new Vector2(PanelWidth / 2f - 140f, -12f));

            Button closeButton = CreateButton(_panel, "CloseButton", "关闭",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(PanelWidth / 2f - 116f, -44f),
                new Vector2(PanelWidth / 2f - 24f, -14f),
                new Color(0.34f, 0.36f, 0.4f, 1f));
            closeButton.onClick.AddListener((Action)(() => _context.Close()));

            _configPathLabel = CreateLabel(_panel, "ConfigPath", string.Empty, 13, new Color(0.82f, 0.84f, 0.88f), TextAnchor.MiddleLeft,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(-PanelWidth / 2f + 24f, -76f),
                new Vector2(PanelWidth / 2f - 24f, -52f));

            _modsPathLabel = CreateLabel(_panel, "ModsPath", string.Empty, 13, new Color(0.82f, 0.84f, 0.88f), TextAnchor.MiddleLeft,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(-PanelWidth / 2f + 24f, -102f),
                new Vector2(PanelWidth / 2f - 24f, -78f));

            Button reloadButton = CreateButton(_panel, "ReloadButton", "重新扫描",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(-PanelWidth / 2f + 24f, -142f),
                new Vector2(-PanelWidth / 2f + 124f, -112f),
                new Color(0.25f, 0.46f, 0.72f, 1f));
            reloadButton.onClick.AddListener((Action)(() => _context.ReloadDocument()));

            Button enableAllButton = CreateButton(_panel, "EnableAllButton", "全部启用",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(-PanelWidth / 2f + 136f, -142f),
                new Vector2(-PanelWidth / 2f + 236f, -112f),
                new Color(0.25f, 0.56f, 0.42f, 1f));
            enableAllButton.onClick.AddListener((Action)(() => _context.SetAllEnabled(true)));

            Button disableAllButton = CreateButton(_panel, "DisableAllButton", "全部禁用",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(-PanelWidth / 2f + 248f, -142f),
                new Vector2(-PanelWidth / 2f + 348f, -112f),
                new Color(0.54f, 0.34f, 0.32f, 1f));
            disableAllButton.onClick.AddListener((Action)(() => _context.SetAllEnabled(false)));

            Button saveButton = CreateButton(_panel, "SaveButton", "保存",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(PanelWidth / 2f - 116f, -142f),
                new Vector2(PanelWidth / 2f - 24f, -112f),
                new Color(0.26f, 0.59f, 0.98f, 1f));
            saveButton.onClick.AddListener((Action)(() => _context.SaveDocument()));

            _statusLabel = CreateLabel(_panel, "Status", string.Empty, 14, new Color(0.92f, 0.93f, 0.95f), TextAnchor.MiddleLeft,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(-PanelWidth / 2f + 24f, -174f),
                new Vector2(-PanelWidth / 2f + 260f, -148f));

            _messageLabel = CreateLabel(_panel, "Message", string.Empty, 14, new Color(0.96f, 0.78f, 0.42f), TextAnchor.MiddleLeft,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(-PanelWidth / 2f + 280f, -174f),
                new Vector2(PanelWidth / 2f - 24f, -148f));

            CreateHeaderLabel("启用", ListLeft + 14f, -206f, 54f);
            CreateHeaderLabel("名称", ListLeft + 62f, -206f, 260f);
            CreateHeaderLabel("版本", ListLeft + 326f, -206f, 92f);
            CreateHeaderLabel("排序", ListLeft + 428f, -206f, 170f);

            CreateLabel(_panel, "Footer", "说明: 列表顺序就是龙之书 Mod 加载顺序，越靠后覆盖能力越强；保存后需要完全重启游戏。", 13,
                new Color(1f, 0.62f, 0.18f), TextAnchor.MiddleLeft,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(-PanelWidth / 2f + 24f, 14f),
                new Vector2(PanelWidth / 2f - 24f, 40f));
        }

        private static void BuildScrollArea()
        {
            GameObject viewport = CreateImage(_panel, "Viewport", new Color(0.08f, 0.09f, 0.1f, 0.92f),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(ListLeft, -584f),
                new Vector2(ListRight, -218f));
            SafeAddComponent<RectMask2D>(viewport);
            RectTransform viewportRect = GetRectTransform(viewport);

            _contentRoot = new GameObject("Content");
            _contentRoot.transform.SetParent(viewport.transform, false);
            _contentRect = GetRectTransform(_contentRoot);
            _contentRect.anchorMin = new Vector2(0f, 1f);
            _contentRect.anchorMax = new Vector2(1f, 1f);
            _contentRect.pivot = new Vector2(0.5f, 1f);
            _contentRect.offsetMin = new Vector2(0f, 0f);
            _contentRect.offsetMax = new Vector2(0f, 0f);

            ScrollRect scrollRect = SafeAddComponent<ScrollRect>(viewport);
            scrollRect.viewport = viewportRect;
            scrollRect.content = _contentRect;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 24f;

            GameObject scrollbarObj = CreateImage(_panel, "Scrollbar", new Color(0.14f, 0.15f, 0.17f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(ListRight + 6f, -584f),
                new Vector2(ListRight + 22f, -218f));
            GameObject handleObj = CreateImage(scrollbarObj, "Handle", new Color(0.45f, 0.48f, 0.54f, 1f),
                Vector2.zero, Vector2.one, new Vector2(2f, 2f), new Vector2(-2f, -2f));

            Scrollbar scrollbar = SafeAddComponent<Scrollbar>(scrollbarObj);
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scrollbar.targetGraphic = handleObj.GetComponent(Il2CppType.Of<Image>()).TryCast<Image>();
            scrollbar.handleRect = GetRectTransform(handleObj);
            scrollRect.verticalScrollbar = scrollbar;
        }

        private static void BuildDetailArea()
        {
            GameObject detailPanel = CreateImage(_panel, "DetailPanel", new Color(0.095f, 0.105f, 0.12f, 0.96f),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(DetailLeft, -584f),
                new Vector2(DetailRight, -218f));
            AddBorder(detailPanel, new Color(0.32f, 0.35f, 0.4f, 0.92f));

            _detailTitleLabel = CreateLabel(detailPanel, "DetailTitle", "Mod 详细信息", 15, Color.white, TextAnchor.MiddleCenter,
                new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(12f, -38f), new Vector2(-12f, -8f), true, true);

            CreateLabel(detailPanel, "DetailFolderTitle", "目录", 13, new Color(0.68f, 0.72f, 0.78f), TextAnchor.MiddleLeft,
                new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(14f, -70f), new Vector2(76f, -46f));
            _detailFolderLabel = CreateLabel(detailPanel, "DetailFolderValue", string.Empty, 13, Color.white, TextAnchor.MiddleLeft,
                new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(80f, -70f), new Vector2(-14f, -46f), true, true);

            CreateLabel(detailPanel, "DetailVersionTitle", "版本", 13, new Color(0.68f, 0.72f, 0.78f), TextAnchor.MiddleLeft,
                new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(14f, -98f), new Vector2(76f, -74f));
            _detailVersionLabel = CreateLabel(detailPanel, "DetailVersionValue", string.Empty, 13, Color.white, TextAnchor.MiddleLeft,
                new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(80f, -98f), new Vector2(-14f, -74f), true, true);

            CreateLabel(detailPanel, "DetailAuthorTitle", "作者", 13, new Color(0.68f, 0.72f, 0.78f), TextAnchor.MiddleLeft,
                new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(14f, -126f), new Vector2(76f, -102f));
            _detailAuthorLabel = CreateLabel(detailPanel, "DetailAuthorValue", string.Empty, 13, Color.white, TextAnchor.MiddleLeft,
                new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(80f, -126f), new Vector2(-14f, -102f), true, true);

            CreateLabel(detailPanel, "DetailDescTitle", "描述", 13, new Color(0.68f, 0.72f, 0.78f), TextAnchor.MiddleLeft,
                new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(14f, -156f), new Vector2(-14f, -132f));

            GameObject descViewport = CreateImage(detailPanel, "DetailDescViewport", new Color(0.06f, 0.065f, 0.075f, 0.9f),
                Vector2.zero, Vector2.one, new Vector2(14f, 14f), new Vector2(-14f, -180f));
            SafeAddComponent<RectMask2D>(descViewport);
            RectTransform viewportRect = GetRectTransform(descViewport);

            GameObject detailContentRoot = new GameObject("DetailDescContent");
            detailContentRoot.transform.SetParent(descViewport.transform, false);
            _detailContentRect = GetRectTransform(detailContentRoot);
            _detailContentRect.anchorMin = new Vector2(0f, 1f);
            _detailContentRect.anchorMax = new Vector2(1f, 1f);
            _detailContentRect.pivot = new Vector2(0.5f, 1f);
            _detailContentRect.offsetMin = Vector2.zero;
            _detailContentRect.offsetMax = Vector2.zero;

            _detailDescLabel = CreateLabel(detailContentRoot, "DetailDescValue", string.Empty, 13, new Color(0.9f, 0.92f, 0.95f), TextAnchor.UpperLeft,
                new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0f, -160f), new Vector2(0f, 0f), true, true);

            ScrollRect scrollRect = SafeAddComponent<ScrollRect>(descViewport);
            scrollRect.viewport = viewportRect;
            scrollRect.content = _detailContentRect;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 18f;
        }

        private static void EnsureTopMost()
        {
            try
            {
                if (_root == null) return;
                _root.transform.SetAsLastSibling();
                if (_rootCanvas != null)
                {
                    _rootCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                    _rootCanvas.overrideSorting = true;
                    _rootCanvas.sortingOrder = short.MaxValue;
                }
                if (_rootRaycaster != null && !_rootRaycaster.enabled)
                {
                    _rootRaycaster.enabled = true;
                }
            }
            catch (Exception ex)
            {
                LoggerManager.Warning("ModLoadConfigUguiWindow: 保持顶层显示失败 - " + ex.Message);
            }
        }

        private static void DisableOtherRaycasters()
        {
            try
            {
                List<GraphicRaycaster> raycasters = FindComponentsInScene<GraphicRaycaster>();
                for (int i = 0; i < raycasters.Count; i++)
                {
                    GraphicRaycaster raycaster = raycasters[i];
                    if (raycaster == null || raycaster == _rootRaycaster) continue;
                    if (IsChildOfRoot(raycaster.gameObject)) continue;
                    if (!raycaster.enabled) continue;

                    RememberRaycasterState(raycaster, true);
                    raycaster.enabled = false;
                }
            }
            catch (Exception ex)
            {
                LoggerManager.Warning("ModLoadConfigUguiWindow: 禁用后方 GraphicRaycaster 失败 - " + ex.Message);
            }
        }

        private static void RestoreOtherRaycasters()
        {
            try
            {
                for (int i = 0; i < DisabledRaycasters.Count; i++)
                {
                    RaycasterState state = DisabledRaycasters[i];
                    if (state.Raycaster != null)
                    {
                        state.Raycaster.enabled = state.WasEnabled;
                    }
                }
            }
            catch (Exception ex)
            {
                LoggerManager.Warning("ModLoadConfigUguiWindow: 恢复后方 GraphicRaycaster 失败 - " + ex.Message);
            }
            finally
            {
                DisabledRaycasters.Clear();
            }
        }

        private static void RememberRaycasterState(GraphicRaycaster raycaster, bool wasEnabled)
        {
            for (int i = 0; i < DisabledRaycasters.Count; i++)
            {
                if (DisabledRaycasters[i].Raycaster == raycaster) return;
            }

            DisabledRaycasters.Add(new RaycasterState(raycaster, wasEnabled));
        }

        private static bool IsChildOfRoot(GameObject obj)
        {
            if (_root == null || obj == null) return false;

            Transform current = obj.transform;
            while (current != null)
            {
                if (current.gameObject == _root) return true;
                current = current.parent;
            }

            return false;
        }

        private static void RebuildRows()
        {
            if (_contentRoot == null || _context == null || _context.Document == null) return;

            List<GameObject> children = new List<GameObject>();
            for (int i = 0; i < _contentRoot.transform.childCount; i++)
            {
                children.Add(_contentRoot.transform.GetChild(i).gameObject);
            }
            for (int i = 0; i < children.Count; i++)
            {
                Object.Destroy(children[i]);
            }

            int count = _context.Document.Entries.Count;
            float contentHeight = Mathf.Max(370f, count * RowHeight + 6f);
            _contentRect.sizeDelta = new Vector2(0f, contentHeight);

            for (int i = 0; i < count; i++)
            {
                CreateRow(i, _context.Document.Entries[i]);
            }
        }

        private static void CreateRow(int index, ModLoadConfigEntry entry)
        {
            float top = -index * RowHeight;
            bool selected = _context.IsSelected(entry);
            GameObject row = CreateImage(_contentRoot, "Row_" + entry.FolderName,
                selected ? new Color(0.22f, 0.34f, 0.52f, 0.98f) :
                (index % 2 == 0 ? new Color(0.12f, 0.13f, 0.145f, 0.96f) : new Color(0.10f, 0.11f, 0.125f, 0.96f)),
                new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0f, top - RowHeight), new Vector2(0f, top));
            Image rowImage = row.GetComponent(Il2CppType.Of<Image>()).TryCast<Image>();
            Button rowButton = SafeAddComponent<Button>(row);
            rowButton.targetGraphic = rowImage;
            rowButton.transition = Selectable.Transition.None;
            rowButton.onClick.AddListener((Action)(() => _context.SelectEntry(entry)));

            Toggle toggle = CreateToggle(row, "EnabledToggle", entry.Enabled,
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(12f, -10f), new Vector2(32f, 10f));
            toggle.onValueChanged.AddListener((Action<bool>)(value =>
            {
                if (entry.Enabled == value) return;
                entry.Enabled = value;
                _context.MarkDirty("已修改启用状态。");
            }));

            CreateLabel(row, "DisplayName", entry.DisplayName, 14, Color.white, TextAnchor.MiddleLeft,
                new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(54f, 0f), new Vector2(318f, 0f), true, false);
            CreateLabel(row, "Version", entry.Version, 13, new Color(0.72f, 0.76f, 0.82f), TextAnchor.MiddleLeft,
                new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(322f, 0f), new Vector2(414f, 0f), true, false);

            Button up = CreateButton(row, "UpButton", "↑", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(424f, -12f), new Vector2(456f, 12f), new Color(0.25f, 0.31f, 0.39f, 1f));
            up.onClick.AddListener((Action)(() => _context.MoveEntry(index, index - 1)));

            Button down = CreateButton(row, "DownButton", "↓", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(460f, -12f), new Vector2(492f, 12f), new Color(0.25f, 0.31f, 0.39f, 1f));
            down.onClick.AddListener((Action)(() => _context.MoveEntry(index, index + 1)));

            Button topButton = CreateButton(row, "TopButton", "置顶", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(498f, -12f), new Vector2(548f, 12f), new Color(0.25f, 0.31f, 0.39f, 1f));
            topButton.onClick.AddListener((Action)(() => _context.MoveEntry(index, 0)));

            Button bottomButton = CreateButton(row, "BottomButton", "置底", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(552f, -12f), new Vector2(602f, 12f), new Color(0.25f, 0.31f, 0.39f, 1f));
            bottomButton.onClick.AddListener((Action)(() => _context.MoveEntry(index, _context.Document.Entries.Count - 1)));
        }

        private static void UpdateStaticTexts()
        {
            if (_context == null || _context.Document == null) return;

            _configPathLabel.SetText("配置文件: " + _context.Document.ConfigPath);
            _modsPathLabel.SetText("ModsOfLong: " + _context.Document.ModsOfLongRoot);
            _statusLabel.SetText(_context.Document.IsDirty ? "状态: 有未保存修改" : "状态: 无未保存修改");
            _messageLabel.SetText(_context.Document.LastMessage ?? string.Empty);
        }

        private static void UpdateDetailTexts()
        {
            if (_context == null) return;
            if (_detailTitleLabel == null || _detailVersionLabel == null ||
                _detailAuthorLabel == null || _detailFolderLabel == null || _detailDescLabel == null)
            {
                return;
            }

            ModLoadConfigEntry entry = _context.GetSelectedEntry();
            if (entry == null)
            {
                _detailTitleLabel.SetText("Mod 详细信息");
                _detailVersionLabel.SetText(string.Empty);
                _detailAuthorLabel.SetText(string.Empty);
                _detailFolderLabel.SetText(string.Empty);
                _detailDescLabel.SetText(string.Empty);
                ResizeDetailDescription(160f);
                return;
            }

            _detailTitleLabel.SetText(ResolveDetailTitle(entry));
            _detailVersionLabel.SetText(UnspecifiedFallback(entry.Version));
            _detailAuthorLabel.SetText(UnspecifiedFallback(entry.Author));
            _detailFolderLabel.SetText(entry.FolderName ?? string.Empty);

            string desc = entry.Desc ?? string.Empty;
            _detailDescLabel.SetText(desc);
            ResizeDetailDescription(EstimateDetailDescriptionHeight(desc, DetailRight - DetailLeft - 28f, 13));
        }

        private static string ResolveDetailTitle(ModLoadConfigEntry entry)
        {
            if (entry == null) return "Mod 详细信息";
            if (!string.IsNullOrWhiteSpace(entry.DisplayName)) return entry.DisplayName;
            if (!string.IsNullOrWhiteSpace(entry.FolderName)) return entry.FolderName;
            return "Mod 详细信息";
        }

        private static string UnspecifiedFallback(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "unspecified" : value;
        }

        private static void ResizeDetailDescription(float height)
        {
            height = Mathf.Max(160f, height);
            if (_detailContentRect != null)
            {
                _detailContentRect.sizeDelta = new Vector2(0f, height);
            }

            if (_detailDescLabel != null && _detailDescLabel.Rect != null)
            {
                _detailDescLabel.Rect.offsetMin = new Vector2(0f, -height);
                _detailDescLabel.Rect.offsetMax = Vector2.zero;
            }
        }

        private static float EstimateDetailDescriptionHeight(string text, float width, int fontSize)
        {
            if (string.IsNullOrEmpty(text)) return 160f;

            string normalized = text.Replace("\r\n", "\n").Replace("\r", "\n");
            string[] lines = normalized.Split('\n');
            float charsPerLine = Mathf.Max(8f, width / Mathf.Max(1f, fontSize * 0.55f));
            int visualLines = 0;
            for (int i = 0; i < lines.Length; i++)
            {
                int length = StripSimpleRichTextTags(lines[i]).Length;
                visualLines += Mathf.Max(1, Mathf.CeilToInt(length / charsPerLine));
            }

            return Mathf.Min(900f, visualLines * (fontSize + 6f) + 18f);
        }

        private static string StripSimpleRichTextTags(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;

            char[] buffer = new char[value.Length];
            int count = 0;
            bool inTag = false;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (c == '<')
                {
                    inTag = true;
                    continue;
                }

                if (c == '>' && inTag)
                {
                    inTag = false;
                    continue;
                }

                if (!inTag)
                {
                    buffer[count++] = c;
                }
            }

            return new string(buffer, 0, count);
        }

        private static void CreateHeaderLabel(string text, float x, float centerY, float width)
        {
            float height = 30f;
            float halfHeight = height * 0.5f;
            CreateLabel(_panel, "Header_" + text, text, 14, new Color(0.68f, 0.72f, 0.78f), TextAnchor.MiddleLeft,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(x, centerY - halfHeight), new Vector2(x + width, centerY + halfHeight));
        }

        private static Toggle CreateToggle(GameObject parent, string name, bool value,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            GameObject obj = CreateImage(parent, name, new Color(0.18f, 0.19f, 0.21f, 1f), anchorMin, anchorMax, offsetMin, offsetMax);
            Image bg = obj.GetComponent(Il2CppType.Of<Image>()).TryCast<Image>();
            AddBorder(obj, new Color(0.55f, 0.58f, 0.64f, 1f));

            GameObject check = CreateImage(obj, "Checkmark", new Color(0.28f, 0.72f, 0.44f, 1f),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-6f, -6f), new Vector2(6f, 6f));
            Image checkImage = check.GetComponent(Il2CppType.Of<Image>()).TryCast<Image>();

            Toggle toggle = SafeAddComponent<Toggle>(obj);
            toggle.targetGraphic = bg;
            toggle.graphic = checkImage;
            toggle.isOn = value;
            return toggle;
        }

        private static Button CreateButton(GameObject parent, string name, string label,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, Color color)
        {
            GameObject obj = CreateImage(parent, name, color, anchorMin, anchorMax, offsetMin, offsetMax);
            Image img = obj.GetComponent(Il2CppType.Of<Image>()).TryCast<Image>();
            Button button = SafeAddComponent<Button>(obj);
            button.targetGraphic = img;

            CreateLabel(obj, "Label", label, label.Length <= 2 ? 15 : 13, Color.white, TextAnchor.MiddleCenter,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            return button;
        }

        private static LabelHandle CreateLabel(GameObject parent, string name, string text, int fontSize, Color color, TextAnchor alignment,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, bool richText = false, bool wordWrap = false)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent.transform, false);
            RectTransform rect = GetRectTransform(obj);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;

            LabelHandle handle = new LabelHandle();
            if (_useTmp && _tmpFont != null)
            {
                TextMeshProUGUI tmp = SafeAddComponent<TextMeshProUGUI>(obj);
                tmp.font = _tmpFont;
                tmp.text = text;
                tmp.fontSize = fontSize;
                tmp.color = color;
                tmp.alignment = ConvertTmpAlignment(alignment);
                tmp.richText = richText;
                tmp.enableWordWrapping = wordWrap;
                tmp.overflowMode = wordWrap ? TextOverflowModes.Overflow : TextOverflowModes.Ellipsis;
                tmp.raycastTarget = false;
                handle.Tmp = tmp;
            }
            else
            {
                Text uiText = SafeAddComponent<Text>(obj);
                uiText.text = text;
                uiText.fontSize = fontSize;
                uiText.color = color;
                uiText.alignment = alignment;
                uiText.font = GetBuiltinFont();
                uiText.supportRichText = richText;
                uiText.horizontalOverflow = wordWrap ? HorizontalWrapMode.Wrap : HorizontalWrapMode.Overflow;
                uiText.verticalOverflow = VerticalWrapMode.Overflow;
                uiText.raycastTarget = false;
                handle.Ui = uiText;
            }

            handle.Rect = rect;
            return handle;
        }

        private static TextAlignmentOptions ConvertTmpAlignment(TextAnchor alignment)
        {
            switch (alignment)
            {
                case TextAnchor.MiddleLeft:
                    return TextAlignmentOptions.MidlineLeft;
                case TextAnchor.MiddleCenter:
                    return TextAlignmentOptions.Center;
                case TextAnchor.UpperLeft:
                    return TextAlignmentOptions.TopLeft;
                case TextAnchor.UpperCenter:
                    return TextAlignmentOptions.Top;
                default:
                    return TextAlignmentOptions.Center;
            }
        }

        private static GameObject CreateImage(GameObject parent, string name, Color color,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent.transform, false);
            Image img = SafeAddComponent<Image>(obj);
            img.color = color;
            RectTransform rect = GetRectTransform(obj);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            return obj;
        }

        private static void AddBorder(GameObject target, Color borderColor)
        {
            Outline outline = SafeAddComponent<Outline>(target);
            outline.effectColor = borderColor;
            outline.effectDistance = new Vector2(1f, -1f);
        }

        private static Canvas FindComponentInSceneCanvas()
        {
            return FindComponentInScene<Canvas>();
        }

        private static T FindComponentInScene<T>() where T : Component
        {
            try
            {
                var il2cppType = Il2CppType.Of<T>();
                for (int s = 0; s < SceneManager.sceneCount; s++)
                {
                    var scene = SceneManager.GetSceneAt(s);
                    if (!scene.isLoaded) continue;
                    foreach (var rootObj in scene.GetRootGameObjects())
                    {
                        if (rootObj == null) continue;
                        var comp = rootObj.GetComponentInChildren(il2cppType)?.TryCast<T>();
                        if (comp != null) return comp;
                    }
                }
            }
            catch (Exception ex)
            {
                LoggerManager.Warning("ModLoadConfigUguiWindow: FindComponentInScene<" + typeof(T).Name + "> 失败 - " + ex.Message);
            }

            return null;
        }

        private static List<T> FindComponentsInScene<T>() where T : Component
        {
            List<T> results = new List<T>();
            try
            {
                var il2cppType = Il2CppType.Of<T>();
                for (int s = 0; s < SceneManager.sceneCount; s++)
                {
                    var scene = SceneManager.GetSceneAt(s);
                    if (!scene.isLoaded) continue;
                    foreach (var rootObj in scene.GetRootGameObjects())
                    {
                        if (rootObj == null) continue;
                        var comps = rootObj.GetComponentsInChildren(il2cppType);
                        if (comps == null) continue;
                        foreach (var c in comps)
                        {
                            var casted = c?.TryCast<T>();
                            if (casted != null) results.Add(casted);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LoggerManager.Warning("ModLoadConfigUguiWindow: FindComponentsInScene<" + typeof(T).Name + "> 失败 - " + ex.Message);
            }

            return results;
        }

        private static TMP_FontAsset FindTmpFont()
        {
            try
            {
                List<TMP_Text> tmpTexts = FindComponentsInScene<TMP_Text>();
                for (int i = 0; i < tmpTexts.Count; i++)
                {
                    if (tmpTexts[i] != null && tmpTexts[i].font != null)
                    {
                        return tmpTexts[i].font;
                    }
                }

                ChatController chatCtrl = FindComponentInScene<ChatController>();
                if (chatCtrl != null && chatCtrl.ChatInputField != null &&
                    chatCtrl.ChatInputField.textComponent != null &&
                    chatCtrl.ChatInputField.textComponent.font != null)
                {
                    return chatCtrl.ChatInputField.textComponent.font;
                }
            }
            catch (Exception ex)
            {
                LoggerManager.Warning("ModLoadConfigUguiWindow: 查找 TMP 字体失败 - " + ex.Message);
            }

            return null;
        }

        private static T SafeAddComponent<T>(GameObject obj) where T : Component
        {
            return obj.AddComponent(Il2CppType.Of<T>()).TryCast<T>();
        }

        private static RectTransform GetRectTransform(GameObject obj)
        {
            try
            {
                RectTransform rect = obj.transform.TryCast<RectTransform>();
                if (rect != null) return rect;
            }
            catch
            {
            }

            try
            {
                RectTransform rect = obj.GetComponent(Il2CppType.Of<RectTransform>())?.TryCast<RectTransform>();
                if (rect != null) return rect;
            }
            catch
            {
            }

            return SafeAddComponent<RectTransform>(obj);
        }

        private static Font _cachedBuiltinFont;

        private static Font GetBuiltinFont()
        {
            if (_cachedBuiltinFont != null) return _cachedBuiltinFont;
            try
            {
                _cachedBuiltinFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }
            catch (Exception ex)
            {
                LoggerManager.Warning("ModLoadConfigUguiWindow: GetBuiltinResource<Font> 失败 - " + ex.Message);
            }

            return _cachedBuiltinFont;
        }

        private sealed class LabelHandle
        {
            public TMP_Text Tmp;
            public Text Ui;
            public RectTransform Rect;

            public void SetText(string text)
            {
                if (Tmp != null) Tmp.text = text;
                if (Ui != null) Ui.text = text;
            }
        }

        private sealed class RaycasterState
        {
            public readonly GraphicRaycaster Raycaster;
            public readonly bool WasEnabled;

            public RaycasterState(GraphicRaycaster raycaster, bool wasEnabled)
            {
                Raycaster = raycaster;
                WasEnabled = wasEnabled;
            }
        }
    }
}
