using System;
using System.Collections;
using System.Reflection;
using MelonLoader;
using UnityEngine;

namespace TheExtensionOfLong
{
    public static class ModLoadConfigWindow
    {
        private enum ActiveBackend
        {
            None,
            Ugui,
            Imgui
        }

        private static MelonPreferences_Category _category;
        private static MelonPreferences_Entry<bool> _openEntry;
        private static MelonPreferences_Entry<ModLoadConfigUiMode> _uiModeEntry;
        private static ModLoadConfigWindowContext _context;
        private static bool _isOpen;
        private static ActiveBackend _activeBackend;
        private static bool _restoreTheBookOfLongEditorOnClose;

        public static bool IsOpen
        {
            get { return _isOpen; }
        }

        public static void InitializePreferences()
        {
            try
            {
                _category = MelonPreferences.CreateCategory("TheBookOfLong_ModManager", "龙之书 Mod 管理");
                _openEntry = _category.CreateEntry(
                    "open_mod_load_manager",
                    false,
                    "打开《龙之书 Mod》管理界面",
                    "设为开启时打开管理界面");

                _uiModeEntry = _category.CreateEntry(
                    "mod_load_manager_ui_mode",
                    ModLoadConfigUiMode.Auto,
                    "管理界面 UI 类型",
                    "Auto 会优先使用 UGUI，失败时回退 MGUI。");
            }
            catch (Exception ex)
            {
                LoggerManager.Warning("ModLoadConfigWindow: 注册 MelonPreferences 设置失败 - " + ex.Message);
            }
        }

        public static void OnUpdate()
        {
            if (_openEntry != null && _openEntry.Value)
            {
                _openEntry.Value = false;
                try
                {
                    MelonPreferences.Save();
                }
                catch
                {
                }

                Open();
            }

            if (_isOpen && (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1)))
            {
                Close();
            }

            if (_isOpen && _activeBackend == ActiveBackend.Ugui)
            {
                ModLoadConfigUguiWindow.OnUpdate();
            }
        }

        public static void OnGUI()
        {
            if (!_isOpen) return;
            if (_activeBackend == ActiveBackend.Imgui)
            {
                ModLoadConfigImguiWindow.OnGUI();
            }
        }

        public static void Open()
        {
            if (TextInputDialog.IsShowing)
            {
                LoggerManager.Warning("ModLoadConfigWindow: TextInputDialog 正在显示，暂不打开 Mod 管理界面");
                return;
            }

            try
            {
                Close(false);
                _restoreTheBookOfLongEditorOnClose = HideTheBookOfLongPreferencesEditor();

                _context = new ModLoadConfigWindowContext();
                _context.Document = ModLoadConfigService.LoadDocument();
                _context.CloseRequested = Close;
                _context.RefreshRequested = RefreshActiveBackend;
                _context.EnsureSelectedEntry();

                if (TryOpenBackend())
                {
                    _isOpen = true;
                }
            }
            catch (Exception ex)
            {
                LoggerManager.Error("ModLoadConfigWindow: 打开失败 - " + ex.Message);
                Close();
            }
        }

        private static bool TryOpenBackend()
        {
            switch (GetUiMode())
            {
                case ModLoadConfigUiMode.UGUI:
                    return TryOpenUgui(false);
                case ModLoadConfigUiMode.MGUI:
                    return TryOpenImgui();
                default:
                    if (TryOpenUgui(true)) return true;
                    return TryOpenImgui();
            }
        }

        private static ModLoadConfigUiMode GetUiMode()
        {
            return _uiModeEntry == null ? ModLoadConfigUiMode.Auto : _uiModeEntry.Value;
        }

        private static bool TryOpenUgui(bool allowFallback)
        {
            try
            {
                if (ModLoadConfigUguiWindow.Open(_context))
                {
                    _activeBackend = ActiveBackend.Ugui;
                    LoggerManager.Debug("ModLoadConfigWindow: 使用 UGUI 管理界面");
                    return true;
                }
            }
            catch (Exception ex)
            {
                LoggerManager.Warning("ModLoadConfigWindow: UGUI 管理界面启动失败 - " + ex.Message);
            }

            if (!allowFallback)
            {
                LoggerManager.Error("ModLoadConfigWindow: UGUI 管理界面不可用，当前源码模式不允许回退 IMGUI");
            }

            return false;
        }

        private static bool TryOpenImgui()
        {
            if (ModLoadConfigImguiWindow.Open(_context))
            {
                _activeBackend = ActiveBackend.Imgui;
                LoggerManager.Debug("ModLoadConfigWindow: 使用 IMGUI 管理界面");
                return true;
            }

            LoggerManager.Error("ModLoadConfigWindow: IMGUI 管理界面启动失败");
            return false;
        }

        public static void RefreshActiveBackend()
        {
            if (!_isOpen) return;

            if (_activeBackend == ActiveBackend.Ugui)
            {
                ModLoadConfigUguiWindow.Refresh();
            }
            else if (_activeBackend == ActiveBackend.Imgui)
            {
                ModLoadConfigImguiWindow.Refresh();
            }
        }

        public static void Close()
        {
            Close(true);
        }

        private static void Close(bool restoreTheBookOfLongEditor)
        {
            if (_activeBackend == ActiveBackend.Ugui)
            {
                ModLoadConfigUguiWindow.Close();
            }
            else if (_activeBackend == ActiveBackend.Imgui)
            {
                ModLoadConfigImguiWindow.Close();
            }

            _activeBackend = ActiveBackend.None;
            _isOpen = false;
            _context = null;

            if (restoreTheBookOfLongEditor && _restoreTheBookOfLongEditorOnClose)
            {
                _restoreTheBookOfLongEditorOnClose = false;
                ShowTheBookOfLongPreferencesEditor();
            }
            else if (!restoreTheBookOfLongEditor)
            {
                _restoreTheBookOfLongEditorOnClose = false;
            }
        }

        private static bool HideTheBookOfLongPreferencesEditor()
        {
            try
            {
                object mainMod = FindTheBookOfLongMainMod();
                if (mainMod == null) return false;

                object editor = GetTheBookOfLongPreferencesEditor(mainMod);
                if (editor == null) return false;

                bool wasVisible = IsTheBookOfLongPreferencesEditorVisible(editor);
                if (!wasVisible) return false;

                MethodInfo setVisibleMethod = editor.GetType().GetMethod(
                    "SetVisible",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                if (setVisibleMethod != null)
                {
                    setVisibleMethod.Invoke(editor, new object[] { false });
                    LoggerManager.Debug("ModLoadConfigWindow: 已关闭龙之书 Mod 配置编辑器");
                    return true;
                }

                FieldInfo visibleField = editor.GetType().GetField(
                    "_isVisible",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                if (visibleField != null)
                {
                    visibleField.SetValue(editor, false);
                    LoggerManager.Debug("ModLoadConfigWindow: 已隐藏龙之书 Mod 配置编辑器");
                    return true;
                }
            }
            catch (Exception ex)
            {
                LoggerManager.Warning("ModLoadConfigWindow: 关闭龙之书 Mod 配置编辑器失败 - " + ex.Message);
            }

            return false;
        }

        private static void ShowTheBookOfLongPreferencesEditor()
        {
            try
            {
                object mainMod = FindTheBookOfLongMainMod();
                if (mainMod == null) return;

                object editor = GetTheBookOfLongPreferencesEditor(mainMod);
                if (editor == null) return;

                MethodInfo openMethod = editor.GetType().GetMethod(
                    "Open",
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (openMethod != null)
                {
                    openMethod.Invoke(editor, null);
                    LoggerManager.Debug("ModLoadConfigWindow: 已重新打开龙之书 Mod 配置编辑器");
                    return;
                }

                MethodInfo setVisibleMethod = editor.GetType().GetMethod(
                    "SetVisible",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                if (setVisibleMethod != null)
                {
                    setVisibleMethod.Invoke(editor, new object[] { true });
                    LoggerManager.Debug("ModLoadConfigWindow: 已恢复龙之书 Mod 配置编辑器");
                    return;
                }

                FieldInfo visibleField = editor.GetType().GetField(
                    "_isVisible",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                if (visibleField != null)
                {
                    visibleField.SetValue(editor, true);
                    LoggerManager.Debug("ModLoadConfigWindow: 已显示龙之书 Mod 配置编辑器");
                }
            }
            catch (Exception ex)
            {
                LoggerManager.Warning("ModLoadConfigWindow: 重新打开龙之书 Mod 配置编辑器失败 - " + ex.Message);
            }
        }

        private static object GetTheBookOfLongPreferencesEditor(object mainMod)
        {
            if (mainMod == null) return null;

            FieldInfo editorField = mainMod.GetType().GetField(
                "_preferencesEditor",
                BindingFlags.Instance | BindingFlags.NonPublic);
            return editorField == null ? null : editorField.GetValue(mainMod);
        }

        private static bool IsTheBookOfLongPreferencesEditorVisible(object editor)
        {
            try
            {
                FieldInfo visibleField = editor.GetType().GetField(
                    "_isVisible",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                if (visibleField != null)
                {
                    object value = visibleField.GetValue(editor);
                    if (value is bool) return (bool)value;
                }
            }
            catch
            {
            }

            return true;
        }

        private static object FindTheBookOfLongMainMod()
        {
            object result = FindTheBookOfLongMainModFromProperty(typeof(MelonHandler), "RegisteredMelons");
            if (result != null) return result;

            result = FindTheBookOfLongMainModFromProperty(typeof(MelonHandler), "Mods");
            if (result != null) return result;

            Type melonBaseType = typeof(MelonMod).BaseType;
            if (melonBaseType != null)
            {
                result = FindTheBookOfLongMainModFromProperty(melonBaseType, "RegisteredMelons");
                if (result != null) return result;
            }

            return null;
        }

        private static object FindTheBookOfLongMainModFromProperty(Type ownerType, string propertyName)
        {
            try
            {
                PropertyInfo property = ownerType.GetProperty(
                    propertyName,
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                if (property == null) return null;

                IEnumerable melons = property.GetValue(null, null) as IEnumerable;
                if (melons == null) return null;

                foreach (object melon in melons)
                {
                    if (melon == null) continue;
                    Type type = melon.GetType();
                    if (type.FullName == "TheBookOfLong.MainMod")
                    {
                        return melon;
                    }
                }
            }
            catch
            {
            }

            return null;
        }
    }
}
