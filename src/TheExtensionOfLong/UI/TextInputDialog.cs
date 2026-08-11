using System;
using System.Collections.Generic;
using Il2Cpp;
using Il2CppInterop.Runtime;
using Il2CppTMPro;
using MelonLoader;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TheExtensionOfLong
{
    /// <summary>
    /// 文本输入弹窗工具类
    /// 优先使用 UGUI TMP_InputField（需要游戏内存在 TMP 字体资源），
    /// 否则回退到 UnityEngine.UI.InputField，
    /// 若两者均不可用则使用 IMGUI 方式。
    /// 
    /// 用法：
    ///   TextInputDialog.Show("请输入角色名", (text) => { /* 处理输入 */ });
    ///   TextInputDialog.Show("请输入角色名", "默认文本", (text) => { /* 处理输入 */ });
    /// </summary>
    public static class TextInputDialog
    {
        // ==================== 状态 ====================
        private static bool _isShowing;
        private static string _title = "";
        private static string _inputText = "";
        private static string _placeholderText = "请输入文本...";
        private static Action<string> _onComplete;
        private static Action _onCancel;

        // UGUI 实例
        private static GameObject _dialogRoot;
        private static bool _useIMGUI;

        // EventSystem 控制（IMGUI 模式下禁用，防止鼠标穿透到后方 UGUI）
        private static EventSystem _cachedEventSystem;
        private static bool _eventSystemWasEnabled;

        // IMGUI 窗口参数
        private static Rect _windowRect;
        private static GUISkin _cachedSkin;
        private static bool _imeEnabled;
        private static int _windowId = "TextInputDialog".GetHashCode();

        /// <summary>当前是否正在显示弹窗</summary>
        public static bool IsShowing => _isShowing;

        /// <summary>
        /// 在 MelonMod.OnUpdate() 中调用此方法
        /// </summary>
        public static void OnUpdate()
        {
            if (!_isShowing) return;

            if (_useIMGUI)
            {
                // 每帧重新禁用 EventSystem（游戏代码可能会每帧重新启用它）
                DisableEventSystem();

                // ESC 关闭弹窗 - 多种检测方式
                if (CheckEscapeKey())
                {
                    LoggerManager.Debug("TextInputDialog: OnUpdate中检测到ESC");
                    OnCancel();
                    return;
                }
            }
            else
            {
                // UGUI 模式：检测回车确认和 ESC 取消
                if (CheckReturnKey())
                {
                    LoggerManager.Debug("TextInputDialog: OnUpdate中检测到回车");
                    OnConfirm();
                    return;
                }
                if (CheckEscapeKey())
                {
                    LoggerManager.Debug("TextInputDialog: OnUpdate中检测到ESC");
                    OnCancel();
                    return;
                }
            }
        }

        /// <summary>检测 ESC 按键，兼容 Il2Cpp strip</summary>
        private static bool CheckEscapeKey()
        {
            // 方式1：Input.GetKeyDown（泛型，可能被 strip）
            try
            {
                if (Input.GetKeyDown(KeyCode.Escape))
                    return true;
            }
            catch (Exception ex)
            {
                LoggerManager.Debug("TextInputDialog: Input.GetKeyDown被strip - " + ex.Message);
            }

            // 方式2：Input.GetKey + 帧去重（泛型，可能被 strip，但行为不同于GetKeyDown）
            try
            {
                bool isHeld = Input.GetKey(KeyCode.Escape);
                bool wasHeld = _escWasHeld;
                _escWasHeld = isHeld;
                if (isHeld && !wasHeld)
                    return true;
            }
            catch (Exception ex)
            {
                LoggerManager.Debug("TextInputDialog: Input.GetKey被strip - " + ex.Message);
            }

            return false;
        }

        // ESC 按键帧去重
        private static bool _escWasHeld;

        // 回车按键帧去重
        private static bool _returnWasHeld;

        /// <summary>检测回车键（确认），兼容 Il2Cpp strip</summary>
        private static bool CheckReturnKey()
        {
            try
            {
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                    return true;
            }
            catch (Exception ex)
            {
                LoggerManager.Debug("TextInputDialog: Input.GetKeyDown(Return)被strip - " + ex.Message);
            }

            try
            {
                bool isHeld = Input.GetKey(KeyCode.Return) || Input.GetKey(KeyCode.KeypadEnter);
                bool wasHeld = _returnWasHeld;
                _returnWasHeld = isHeld;
                if (isHeld && !wasHeld)
                    return true;
            }
            catch (Exception ex)
            {
                LoggerManager.Debug("TextInputDialog: Input.GetKey(Return)被strip - " + ex.Message);
            }

            return false;
        }

        /// <summary>禁用 EventSystem，防止 IMGUI 弹窗期间鼠标穿透到后方 UGUI</summary>
        /// <remarks>需要在 OnUpdate 中每帧调用，因为游戏代码可能每帧重新启用 EventSystem</remarks>
        private static void DisableEventSystem()
        {
            try
            {
                // 首次查找：EventSystem.current
                if (_cachedEventSystem == null)
                {
                    _cachedEventSystem = EventSystem.current;
                }
                // 备用查找：场景遍历
                if (_cachedEventSystem == null)
                {
                    _cachedEventSystem = FindComponentInScene<EventSystem>();
                }
                if (_cachedEventSystem != null && _cachedEventSystem.enabled)
                {
                    _eventSystemWasEnabled = true; // 只记录原始状态
                    _cachedEventSystem.enabled = false;
                }
            }
            catch (Exception ex)
            {
                LoggerManager.Warning("TextInputDialog: 禁用EventSystem失败 - " + ex.Message);
            }
        }

        /// <summary>恢复 EventSystem 状态</summary>
        private static void RestoreEventSystem()
        {
            try
            {
                if (_cachedEventSystem != null)
                {
                    _cachedEventSystem.enabled = _eventSystemWasEnabled;
                    _cachedEventSystem = null;
                }
            }
            catch (Exception ex)
            {
                LoggerManager.Warning("TextInputDialog: 恢复EventSystem失败 - " + ex.Message);
            }
        }

        /// <summary>
        /// 显示文本输入弹窗
        /// </summary>
        /// <param name="title">弹窗标题</param>
        /// <param name="onComplete">确认回调，参数为输入文本</param>
        /// <param name="onCancel">取消回调（可选）</param>
        public static void Show(string title, Action<string> onComplete, Action onCancel = null)
        {
            Show(title, "", "请输入文本...", onComplete, onCancel);
        }

        /// <summary>
        /// 显示文本输入弹窗（带默认文本）
        /// </summary>
        /// <param name="title">弹窗标题</param>
        /// <param name="defaultText">默认输入文本</param>
        /// <param name="onComplete">确认回调</param>
        /// <param name="onCancel">取消回调（可选）</param>
        public static void Show(string title, string defaultText, Action<string> onComplete, Action onCancel = null)
        {
            Show(title, defaultText, "请输入文本...", onComplete, onCancel);
        }

        /// <summary>
        /// 显示文本输入弹窗（完整参数）
        /// </summary>
        public static void Show(string title, string defaultText, string placeholder, Action<string> onComplete, Action onCancel = null)
        {
            if (_isShowing)
            {
                LoggerManager.Warning("TextInputDialog: 已有弹窗在显示中，忽略新请求");
                return;
            }

            _title = title ?? "";
            _inputText = defaultText ?? "";
            _placeholderText = placeholder ?? "请输入文本...";
            _onComplete = onComplete;
            _onCancel = onCancel;
            _isShowing = true;

            // 尝试创建 UGUI 弹窗
            if (!TryCreateUGUIDialog())
            {
                // 回退到 IMGUI
                _useIMGUI = true;
                _imeEnabled = false;
                float w = 400f, h = 140f;
                _windowRect = new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f, w, h);
                DisableEventSystem();
//                 LoggerManager.Debug("TextInputDialog: UGUI不可用，使用IMGUI模式");
            }

            // 暂停游戏（可选）
            // Time.timeScale = 0f;
        }

        /// <summary>
        /// 关闭弹窗
        /// </summary>
        public static void Close()
        {
            if (!_isShowing) return;

            if (_dialogRoot != null)
            {
                UnityEngine.Object.Destroy(_dialogRoot);
                _dialogRoot = null;
            }

            _useIMGUI = false;
            RestoreEventSystem();
            _isShowing = false;
            _onComplete = null;
            _onCancel = null;
            _inputText = "";

            // Time.timeScale = 1f;
        }

        // ==================== UGUI 实现 ====================

        /// <summary>通过场景根对象遍历查找指定类型的组件（绕过 Il2Cpp strip 限制）</summary>
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
                LoggerManager.Warning($"TextInputDialog: FindComponentInScene<{typeof(T).Name}> 失败 - " + ex.Message);
            }
            return null;
        }

        /// <summary>通过场景根对象遍历查找所有指定类型的组件</summary>
        private static List<T> FindComponentsInScene<T>() where T : Component
        {
            var results = new List<T>();
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
                LoggerManager.Warning($"TextInputDialog: FindComponentsInScene<{typeof(T).Name}> 失败 - " + ex.Message);
            }
            return results;
        }

        /// <summary>Il2Cpp 安全的 AddComponent 封装</summary>
        private static T SafeAddComponent<T>(GameObject obj) where T : Component
        {
            return obj.AddComponent(Il2CppType.Of<T>()).TryCast<T>();
        }

        /// <summary>Il2Cpp 安全的 RectTransform 获取</summary>
        /// <remarks>
        /// 在 Il2Cpp 中，SetParent 不会自动将 Transform 升级为 RectTransform，
        /// GetComponent&lt;RectTransform&gt;() 泛型方法可能被 strip，
        /// 因此需要多种回退策略：
        /// 1. transform.TryCast（正常 Unity 下有效）
        /// 2. 非泛型 GetComponent（绕过 strip）
        /// 3. 显式 AddComponent（Il2Cpp 中 Transform 未升级时需要手动添加）
        /// </remarks>
        private static RectTransform GetRectTransform(GameObject obj)
        {
            // 方法1：TryCast transform（正常 Unity 中 Canvas 子对象的 transform 就是 RectTransform）
            try
            {
                var rect = obj.transform.TryCast<RectTransform>();
                if (rect != null) return rect;
            }
            catch { }

            // 方法2：非泛型 GetComponent（绕过泛型方法 strip）
            try
            {
                var rect = obj.GetComponent(Il2CppType.Of<RectTransform>())?.TryCast<RectTransform>();
                if (rect != null) return rect;
            }
            catch { }

            // 方法3：显式 AddComponent（Il2Cpp 中 SetParent 不会自动升级 Transform→RectTransform）
            try
            {
                var rect = SafeAddComponent<RectTransform>(obj);
                if (rect != null)
                {
//                     LoggerManager.Debug("TextInputDialog: 通过AddComponent获取到RectTransform");
                    return rect;
                }
            }
            catch { }

            LoggerManager.Error("TextInputDialog: GetRectTransform 所有方法均失败");
            return null;
        }

        private static bool TryCreateUGUIDialog()
        {
            try
            {
                Canvas canvas = FindComponentInScene<Canvas>();
                if (canvas == null)
                {
                    LoggerManager.Debug("TextInputDialog: 场景中未找到Canvas");
                    return false;
                }
//                 LoggerManager.Debug("TextInputDialog: 找到Canvas - " + canvas.name);

                // 尝试获取 TMP 字体资源
                TMP_FontAsset tmpFont = FindTmpFont();
                bool useTmp = tmpFont != null;

//                 LoggerManager.Debug($"TextInputDialog: 使用 {(useTmp ? "TMP_InputField" : "UnityEngine.UI.InputField")} 模式");

                // 创建弹窗根节点
                _dialogRoot = new GameObject("TextInputDialog");
                _dialogRoot.transform.SetParent(canvas.transform, false);

                // 添加 CanvasGroup 用于模态遮罩
                var canvasGroup = SafeAddComponent<CanvasGroup>(_dialogRoot);
                if (canvasGroup == null) { LoggerManager.Error("TextInputDialog: CanvasGroup为null"); throw new Exception("CanvasGroup AddComponent failed"); }
                canvasGroup.blocksRaycasts = true;
                canvasGroup.interactable = true;

                // RectTransform 并铺满父级（Canvas 子对象的 transform 就是 RectTransform）
                var rootRect = GetRectTransform(_dialogRoot);
                if (rootRect == null) { LoggerManager.Error("TextInputDialog: rootRect为null"); throw new Exception("RectTransform missing"); }
                rootRect.anchorMin = Vector2.zero;
                rootRect.anchorMax = Vector2.one;
                rootRect.offsetMin = Vector2.zero;
                rootRect.offsetMax = Vector2.zero;

                // 半透明遮罩背景
                CreateImage(_dialogRoot, "Overlay", new Color(0, 0, 0, 0.5f),
                    Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

                // 面板容器
                float panelW = 420f, panelH = 180f;
                var panelObj = CreateImage(_dialogRoot, "Panel", new Color(0.15f, 0.15f, 0.17f, 0.95f),
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(-panelW / 2, -panelH / 2), new Vector2(panelW / 2, panelH / 2));
                if (panelObj == null) { LoggerManager.Error("TextInputDialog: Panel为null"); throw new Exception("Panel creation failed"); }
                AddBorder(panelObj, new Color(0.35f, 0.35f, 0.38f));

                // 标题（父对象为 panelObj，锚点相对于面板）
                if (useTmp)
                {
                    CreateTmpLabel(panelObj, "Title", _title, tmpFont, 20, Color.white,
                        new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                        new Vector2(-panelW / 2, -40f), new Vector2(panelW / 2, -10f));
                }
                else
                {
                    CreateUiLabel(panelObj, "Title", _title, 20, Color.white,
                        new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                        new Vector2(-panelW / 2, -40f), new Vector2(panelW / 2, -10f));
                }

                // 输入框
                float inputY = -70f, inputH = 36f;
                var inputRect = new Vector2(-panelW / 2 + 20f, inputY - inputH / 2);
                var inputRectMax = new Vector2(panelW / 2 - 20f, inputY + inputH / 2);

                if (useTmp)
                {
                    CreateTmpInputField(panelObj, "InputField", tmpFont, _inputText, _placeholderText,
                        inputRect, inputRectMax);
                }
                else
                {
                    CreateUiInputField(panelObj, "InputField", _inputText, _placeholderText,
                        inputRect, inputRectMax);
                }

                // 确认按钮
                float btnW = 80f, btnH = 32f, btnY = -130f;
                var confirmBtn = CreateButton(panelObj, "ConfirmBtn", "确认",
                    new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(-btnW - 5f, btnY - btnH / 2), new Vector2(-5f, btnY + btnH / 2),
                    useTmp, tmpFont);
                if (confirmBtn == null) { LoggerManager.Error("TextInputDialog: ConfirmBtn为null"); throw new Exception("ConfirmBtn creation failed"); }
                confirmBtn.onClick.AddListener((Action)OnConfirm);

                // 取消按钮
                var cancelBtn = CreateButton(panelObj, "CancelBtn", "取消",
                    new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(5f, btnY - btnH / 2), new Vector2(btnW + 5f, btnY + btnH / 2),
                    useTmp, tmpFont);
                if (cancelBtn == null) { LoggerManager.Error("TextInputDialog: CancelBtn为null"); throw new Exception("CancelBtn creation failed"); }
                cancelBtn.onClick.AddListener((Action)OnCancel);

                _useIMGUI = false;
                return true;
            }
            catch (Exception ex)
            {
                LoggerManager.Error("TextInputDialog: UGUI创建失败 - " + ex.Message + "\n" + ex.StackTrace);
                if (_dialogRoot != null)
                {
                    UnityEngine.Object.Destroy(_dialogRoot);
                    _dialogRoot = null;
                }
                return false;
            }
        }

        /// <summary>从场景中查找可用的 TMP 字体资源</summary>
        private static TMP_FontAsset FindTmpFont()
        {
            try
            {
                // 方法1：从场景中已有的 TMP 文本组件获取
                var tmpTexts = FindComponentsInScene<TMP_Text>();
                if (tmpTexts != null)
                {
                    foreach (var t in tmpTexts)
                    {
                        if (t != null && t.font != null)
                        {
                            LoggerManager.Debug("TextInputDialog: 从场景TMP_Text获取字体: " + t.font.name);
                            return t.font;
                        }
                    }
                }

                // 方法2：从 ChatController 获取
                var chatCtrl = FindComponentInScene<ChatController>();
                if (chatCtrl != null && chatCtrl.ChatInputField != null
                    && chatCtrl.ChatInputField.textComponent != null
                    && chatCtrl.ChatInputField.textComponent.font != null)
                {
                    LoggerManager.Debug("TextInputDialog: 从ChatController获取字体: " + chatCtrl.ChatInputField.textComponent.font.name);
                    return chatCtrl.ChatInputField.textComponent.font;
                }

                // 方法3：从资源中加载（尝试常见路径）
                // TMP 默认字体通常在 "Assets/TextMesh Pro/Resources/Fonts & Materials/" 下
                // 但在 Il2Cpp 中 Resources.Load 可能无法直接访问

//                 LoggerManager.Debug("TextInputDialog: 未找到TMP字体资源，将使用UnityEngine.UI.InputField");
                return null;
            }
            catch (Exception ex)
            {
                LoggerManager.Warning("TextInputDialog: 查找TMP字体失败 - " + ex.Message);
                return null;
            }
        }

        // ==================== UGUI 辅助方法 ====================

        private static GameObject CreateImage(GameObject parent, string name, Color color,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent.transform, false);
            var img = SafeAddComponent<Image>(obj);
            img.color = color;
            var rect = GetRectTransform(obj);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            return obj;
        }

        private static void AddBorder(GameObject target, Color borderColor)
        {
            var outline = SafeAddComponent<Outline>(target);
            outline.effectColor = borderColor;
            outline.effectDistance = new Vector2(1, -1);
        }

        private static GameObject CreateTmpLabel(GameObject parent, string name, string text,
            TMP_FontAsset font, int fontSize, Color color,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent.transform, false);
            var tmp = SafeAddComponent<TextMeshProUGUI>(obj);
            tmp.font = font;
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Center;
            var rect = GetRectTransform(obj);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            return obj;
        }

        private static Font _cachedBuiltinFont;

        /// <summary>获取内置 Arial 字体（带缓存，避免重复调用 strip 方法）</summary>
        private static Font GetBuiltinFont()
        {
            if (_cachedBuiltinFont != null) return _cachedBuiltinFont;
            try
            {
                _cachedBuiltinFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }
            catch (Exception ex)
            {
                LoggerManager.Warning("TextInputDialog: GetBuiltinResource<Font> 失败 - " + ex.Message);
            }
            return _cachedBuiltinFont;
        }

        private static GameObject CreateUiLabel(GameObject parent, string name, string text,
            int fontSize, Color color,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent.transform, false);
            var uiText = SafeAddComponent<Text>(obj);
            uiText.text = text;
            uiText.fontSize = fontSize;
            uiText.color = color;
            uiText.alignment = TextAnchor.MiddleCenter;
            uiText.font = GetBuiltinFont();
            var rect = GetRectTransform(obj);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            return obj;
        }

        private static void CreateTmpInputField(GameObject parent, string name,
            TMP_FontAsset font, string text, string placeholder,
            Vector2 offsetMin, Vector2 offsetMax)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent.transform, false);
            var rect = GetRectTransform(obj);
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;

            // 背景
            var bg = SafeAddComponent<Image>(obj);
            bg.color = new Color(0.95f, 0.95f, 0.95f);

            // Text 子对象（实际输入文本）
            var textObj = new GameObject("Text");
            textObj.transform.SetParent(obj.transform, false);
            var tmpText = SafeAddComponent<TextMeshProUGUI>(textObj);
            tmpText.font = font;
            tmpText.text = text;
            tmpText.fontSize = 18;
            tmpText.color = Color.black;
            tmpText.alignment = TextAlignmentOptions.MidlineLeft;
            var textRect = GetRectTransform(textObj);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(8, 0);
            textRect.offsetMax = new Vector2(-8, 0);

            // Placeholder 子对象
            var phObj = new GameObject("Placeholder");
            phObj.transform.SetParent(obj.transform, false);
            var phTmp = SafeAddComponent<TextMeshProUGUI>(phObj);
            phTmp.font = font;
            phTmp.text = placeholder;
            phTmp.fontSize = 18;
            phTmp.color = new Color(0.5f, 0.5f, 0.5f);
            phTmp.alignment = TextAlignmentOptions.MidlineLeft;
            var phRect = GetRectTransform(phObj);
            phRect.anchorMin = Vector2.zero;
            phRect.anchorMax = Vector2.one;
            phRect.offsetMin = new Vector2(8, 0);
            phRect.offsetMax = new Vector2(-8, 0);

            // TMP_InputField 组件
            var inputField = SafeAddComponent<TMP_InputField>(obj);
            inputField.textComponent = tmpText;
            inputField.placeholder = phTmp;
            inputField.text = text;

            // 注册回调：onEndEdit 在失焦和回车时都会触发，仅同步文本，不直接确认
            // 确认逻辑由"确认"按钮和 OnUpdate 中的回车检测触发
            inputField.onEndEdit.AddListener((Action<string>)(result =>
            {
                _inputText = result;
            }));
        }

        private static void CreateUiInputField(GameObject parent, string name,
            string text, string placeholder,
            Vector2 offsetMin, Vector2 offsetMax)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent.transform, false);
            var rect = GetRectTransform(obj);
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;

            // 背景
            var bg = SafeAddComponent<Image>(obj);
            bg.color = new Color(0.95f, 0.95f, 0.95f);

            var builtinFont = GetBuiltinFont();

            // Text 子对象
            var textObj = new GameObject("Text");
            textObj.transform.SetParent(obj.transform, false);
            var uiText = SafeAddComponent<Text>(textObj);
            uiText.text = text;
            uiText.fontSize = 16;
            uiText.color = Color.black;
            uiText.alignment = TextAnchor.MiddleLeft;
            uiText.font = builtinFont;
            var textRect = GetRectTransform(textObj);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(8, 0);
            textRect.offsetMax = new Vector2(-8, 0);

            // Placeholder 子对象
            var phObj = new GameObject("Placeholder");
            phObj.transform.SetParent(obj.transform, false);
            var phText = SafeAddComponent<Text>(phObj);
            phText.text = placeholder;
            phText.fontSize = 16;
            phText.color = new Color(0.5f, 0.5f, 0.5f);
            phText.alignment = TextAnchor.MiddleLeft;
            phText.font = builtinFont;
            var phRect = GetRectTransform(phObj);
            phRect.anchorMin = Vector2.zero;
            phRect.anchorMax = Vector2.one;
            phRect.offsetMin = new Vector2(8, 0);
            phRect.offsetMax = new Vector2(-8, 0);

            // InputField 组件
            var inputField = SafeAddComponent<InputField>(obj);
            inputField.textComponent = uiText;
            inputField.placeholder = phText;
            inputField.text = text;
            inputField.targetGraphic = bg;

            // 注册回调：onEndEdit 在失焦和回车时都会触发，仅同步文本，不直接确认
            // 确认逻辑由"确认"按钮和 OnUpdate 中的回车检测触发
            inputField.onEndEdit.AddListener((Action<string>)(result =>
            {
                _inputText = result;
            }));
        }

        private static Button CreateButton(GameObject parent, string name, string label,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax,
            bool useTmp, TMP_FontAsset tmpFont)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent.transform, false);
            var img = SafeAddComponent<Image>(obj);
            img.color = new Color(0.26f, 0.59f, 0.98f);
            var rect = GetRectTransform(obj);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;

            var btn = SafeAddComponent<Button>(obj);
            btn.targetGraphic = img;

            // 按钮文字
            if (useTmp && tmpFont != null)
            {
                var labelObj = new GameObject("Label");
                labelObj.transform.SetParent(obj.transform, false);
                var tmp = SafeAddComponent<TextMeshProUGUI>(labelObj);
                tmp.font = tmpFont;
                tmp.text = label;
                tmp.fontSize = 16;
                tmp.color = Color.white;
                tmp.alignment = TextAlignmentOptions.Center;
                var labelRect = GetRectTransform(labelObj);
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                labelRect.offsetMin = Vector2.zero;
                labelRect.offsetMax = Vector2.zero;
            }
            else
            {
                var labelObj = new GameObject("Label");
                labelObj.transform.SetParent(obj.transform, false);
                var uiText = SafeAddComponent<Text>(labelObj);
                uiText.text = label;
                uiText.fontSize = 14;
                uiText.color = Color.white;
                uiText.alignment = TextAnchor.MiddleCenter;
                uiText.font = GetBuiltinFont();
                var labelRect = GetRectTransform(labelObj);
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                labelRect.offsetMin = Vector2.zero;
                labelRect.offsetMax = Vector2.zero;
            }

            return btn;
        }

        // ==================== 回调 ====================

        private static void OnConfirm()
        {
            var callback = _onComplete;
            var text = _inputText;
            Close();
            callback?.Invoke(text);
        }

        private static void OnCancel()
        {
            var callback = _onCancel;
            Close();
            callback?.Invoke();
        }

        // ==================== IMGUI 回退实现 ====================

        /// <summary>
        /// 在 XGMod.OnGUI() 中调用此方法以支持 IMGUI 模式
        /// </summary>
        public static void OnGUI()
        {
            if (!_isShowing || !_useIMGUI) return;

            var evt = Event.current;

            // ===== 第1步：在全层级拦截所有鼠标和键盘事件，防止穿透到 UGUI =====
            if (evt != null)
            {
                // ESC 关闭弹窗（最高优先级）
                if (evt.type == EventType.KeyDown && evt.keyCode == KeyCode.Escape)
                {
                    evt.Use();
                    OnCancel();
                    return;
                }

                // 拦截所有鼠标点击事件（防止穿透到后方 UGUI）
                if (evt.type == EventType.MouseDown || evt.type == EventType.MouseUp ||
                    evt.type == EventType.MouseDrag)
                {
                    // 点击窗口外 → 关闭弹窗
                    if (!_windowRect.Contains(evt.mousePosition))
                    {
                        evt.Use();
                        OnCancel();
                        return;
                    }
                    // 点击窗口内 → 消费事件（防止穿透）
                    if (evt.type != EventType.MouseDrag)
                    {
                        evt.Use();
                    }
                }
            }

            // ===== 第2步：IME 和样式设置 =====
            if (!_imeEnabled)
            {
                _imeEnabled = true;
                try { Input.imeCompositionMode = IMECompositionMode.On; } catch { }
            }

            GUI.skin.textField.fontSize = 18;
            GUI.skin.label.fontSize = 18;
            GUI.skin.button.fontSize = 16;
            GUI.skin.window.fontSize = 18;

            // ===== 第3步：绘制半透明遮罩背景 =====
            var overlayColor = GUI.color;
            GUI.color = new Color(0, 0, 0, 0.5f);
            GUI.Box(new Rect(0, 0, Screen.width, Screen.height), "");
            GUI.color = overlayColor;

            // ===== 第4步：绘制模态窗口 =====
            _windowRect = GUI.ModalWindow(_windowId, _windowRect, (GUI.WindowFunction)DrawIMGUIWindow, _title);
        }

        private static void DrawIMGUIWindow(int windowId)
        {
            GUILayout.Space(10);
            GUILayout.Label(_title, GUILayout.ExpandWidth(true));
            GUILayout.Space(8);

            // 输入框
            GUI.SetNextControlName("TextInputField");
            _inputText = GUILayout.TextField(_inputText, GUILayout.Height(30));

            GUILayout.Space(12);

            // 按钮行（居中：左侧留白 + 两个按钮 + 间隔）
            GUILayout.BeginHorizontal();
            GUILayout.Space(110);

            if (GUILayout.Button("确认", GUILayout.Width(80), GUILayout.Height(28)))
            {
                OnConfirm();
                return;
            }

            GUILayout.Space(10);

            if (GUILayout.Button("取消", GUILayout.Width(80), GUILayout.Height(28)))
            {
                OnCancel();
                return;
            }

            GUILayout.EndHorizontal();

            // 自动聚焦输入框
            GUI.FocusControl("TextInputField");

            // 允许拖动窗口
            GUI.DragWindow();
        }
    }
}
