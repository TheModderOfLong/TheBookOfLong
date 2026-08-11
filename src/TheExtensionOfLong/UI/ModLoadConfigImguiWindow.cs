using UnityEngine;

namespace TheExtensionOfLong
{
    internal static class ModLoadConfigImguiWindow
    {
        private const float UiScale = 1.25f;
        private static readonly int WindowId = "TheExtensionOfLong.ModLoadConfigImguiWindow".GetHashCode();
        private static ModLoadConfigWindowContext _context;
        private static Rect _windowRect = new Rect(80f, 80f, 920f, 640f);
        private static Vector2 _scroll;
        private static Vector2 _detailScroll;
        private static bool _isOpen;
        private static GUIStyle _detailLabelStyle;
        private static GUIStyle _orangeLabelStyle;

        public static bool Open(ModLoadConfigWindowContext context)
        {
            _context = context;
            _isOpen = true;
            ClampWindowToScreen();
            return true;
        }

        public static void Close()
        {
            _isOpen = false;
            _context = null;
        }

        public static void Refresh()
        {
        }

        public static void OnGUI()
        {
            if (!_isOpen) return;

            int oldDepth = GUI.depth;
            Matrix4x4 oldMatrix = GUI.matrix;
            GUI.depth = -2000;
            GUIUtility.ScaleAroundPivot(new Vector2(UiScale, UiScale), Vector2.zero);
            _windowRect = GUI.ModalWindow(WindowId, _windowRect, (GUI.WindowFunction)DrawWindow, "《龙之书 Mod》管理界面");
            GUI.matrix = oldMatrix;
            GUI.depth = oldDepth;
        }

        private static void DrawWindow(int id)
        {
            ModLoadConfigDocument document = _context == null ? null : _context.Document;
            if (document == null)
            {
                GUI.Label(new Rect(20f, 36f, _windowRect.width - 40f, 24f), "配置尚未加载。");
                if (GUI.Button(new Rect(20f, 72f, 120f, 28f), "关闭"))
                {
                    if (_context != null) _context.Close();
                    return;
                }
                GUI.DragWindow();
                return;
            }

            float y = 34f;
            float contentWidth = _windowRect.width - 32f;
            EnsureStyles();

            GUI.Label(new Rect(16f, y, contentWidth, 22f), "配置文件: " + document.ConfigPath);
            y += 24f;
            GUI.Label(new Rect(16f, y, contentWidth, 22f), "ModsOfLong: " + document.ModsOfLongRoot);
            y += 32f;

            if (GUI.Button(new Rect(16f, y, 96f, 28f), "重新扫描")) _context.ReloadDocument();
            if (GUI.Button(new Rect(120f, y, 96f, 28f), "全部启用")) _context.SetAllEnabled(true);
            if (GUI.Button(new Rect(224f, y, 96f, 28f), "全部禁用")) _context.SetAllEnabled(false);

            GUI.enabled = document.IsDirty;
            if (GUI.Button(new Rect(_windowRect.width - 224f, y, 96f, 28f), "保存")) _context.SaveDocument();
            GUI.enabled = true;
            if (GUI.Button(new Rect(_windowRect.width - 120f, y, 96f, 28f), "关闭"))
            {
                _context.Close();
                return;
            }
            y += 34f;

            GUI.Label(new Rect(16f, y, contentWidth, 22f), document.IsDirty ? "状态: 有未保存修改" : "状态: 无未保存修改");
            y += 22f;
            if (!string.IsNullOrWhiteSpace(document.LastMessage))
            {
                GUI.Label(new Rect(16f, y, contentWidth, 22f), document.LastMessage);
                y += 22f;
            }

            y += 8f;
            float detailWidth = 280f;
            float gap = 12f;
            float listWidth = contentWidth - detailWidth - gap;

            DrawHeader(y);
            y += 24f;

            float footerHeight = 32f;
            Rect viewRect = new Rect(16f, y, listWidth, Mathf.Max(120f, _windowRect.height - y - footerHeight - 12f));
            Rect detailRect = new Rect(16f + listWidth + gap, y - 24f, detailWidth, viewRect.height + 24f);
            float rowHeight = 28f;
            Rect contentRect = new Rect(0f, 0f, listWidth - 20f, Mathf.Max(viewRect.height, document.Entries.Count * rowHeight + 4f));
            _scroll = GUI.BeginScrollView(viewRect, _scroll, contentRect);

            int actionFrom = -1;
            int actionTo = -1;
            for (int i = 0; i < document.Entries.Count; i++)
            {
                DrawEntryRow(document, i, i * rowHeight, ref actionFrom, ref actionTo);
            }

            GUI.EndScrollView();
            if (actionFrom >= 0) _context.MoveEntry(actionFrom, actionTo);

            DrawDetailPanel(detailRect);

            GUI.Label(new Rect(16f, _windowRect.height - 30f, contentWidth, 22f), "说明: 列表顺序就是龙之书 Mod 加载顺序，越靠后覆盖能力越强；保存后需要完全重启游戏。", _orangeLabelStyle);
            GUI.DragWindow();
        }

        private static void DrawHeader(float y)
        {
            GUI.Label(new Rect(16f, y, 52f, 22f), "启用");
            GUI.Label(new Rect(70f, y, 240f, 22f), "名称");
            GUI.Label(new Rect(314f, y, 92f, 22f), "版本");
            GUI.Label(new Rect(410f, y, 160f, 22f), "排序");
        }

        private static void DrawEntryRow(ModLoadConfigDocument document, int index, float y, ref int actionFrom, ref int actionTo)
        {
            if (_context == null || document == null || document.Entries == null) return;
            ModLoadConfigEntry entry = document.Entries[index];
            bool selected = _context.IsSelected(entry);
            Rect rowRect = new Rect(0f, y, 570f, 26f);

            Color oldColor = GUI.color;
            GUI.color = selected ? new Color(0.24f, 0.38f, 0.58f, 0.9f) :
                (index % 2 == 0 ? new Color(0.16f, 0.16f, 0.17f, 0.65f) : new Color(0.11f, 0.11f, 0.12f, 0.65f));
            GUI.Box(rowRect, string.Empty);
            GUI.color = oldColor;

            bool enabled = GUI.Toggle(new Rect(0f, y + 4f, 52f, 22f), entry.Enabled, string.Empty);
            if (enabled != entry.Enabled)
            {
                entry.Enabled = enabled;
                _context.MarkDirty("已修改启用状态。");
            }

            if (GUI.Button(new Rect(54f, y + 2f, 244f, 24f), string.Empty, GUIStyle.none))
            {
                _context.SelectEntry(entry);
            }
            GUI.Label(new Rect(58f, y + 4f, 236f, 22f), entry.DisplayName);
            GUI.Label(new Rect(304f, y + 4f, 90f, 22f), entry.Version);

            if (GUI.Button(new Rect(404f, y + 1f, 36f, 24f), "↑"))
            {
                actionFrom = index;
                actionTo = index - 1;
                return;
            }
            if (GUI.Button(new Rect(444f, y + 1f, 36f, 24f), "↓"))
            {
                actionFrom = index;
                actionTo = index + 1;
                return;
            }
            if (GUI.Button(new Rect(486f, y + 1f, 48f, 24f), "置顶"))
            {
                actionFrom = index;
                actionTo = 0;
                return;
            }
            if (GUI.Button(new Rect(538f, y + 1f, 48f, 24f), "置底"))
            {
                actionFrom = index;
                actionTo = document.Entries.Count - 1;
            }
        }

        private static void DrawDetailPanel(Rect rect)
        {
            ModLoadConfigEntry entry = _context.GetSelectedEntry();
            GUI.Box(rect, ResolveDetailTitle(entry));
            if (entry == null)
            {
                return;
            }

            Rect viewRect = new Rect(rect.x + 10f, rect.y + 30f, rect.width - 20f, rect.height - 40f);
            Rect contentRect = new Rect(0f, 0f, viewRect.width - 18f, 520f);
            _detailScroll = GUI.BeginScrollView(viewRect, _detailScroll, contentRect);

            float y = 0f;
            DrawDetailLine("目录", entry.FolderName, ref y);
            DrawDetailLine("版本", UnspecifiedFallback(entry.Version), ref y);
            DrawDetailLine("作者", UnspecifiedFallback(entry.Author), ref y);
            y += 6f;
            GUI.Label(new Rect(0f, y, contentRect.width, 22f), "描述");
            y += 24f;
            string desc = entry.Desc ?? string.Empty;
            GUI.Label(new Rect(0f, y, contentRect.width, 260f), desc, _detailLabelStyle);

            GUI.EndScrollView();
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

        private static void DrawDetailLine(string label, string value, ref float y)
        {
            GUI.Label(new Rect(0f, y, 70f, 22f), label);
            GUI.Label(new Rect(76f, y, 180f, 22f), value ?? string.Empty, _detailLabelStyle);
            y += 28f;
        }

        private static void EnsureStyles()
        {
            if (_detailLabelStyle == null)
            {
                _detailLabelStyle = new GUIStyle(GUI.skin.label);
                _detailLabelStyle.wordWrap = true;
                _detailLabelStyle.richText = true;
            }

            if (_orangeLabelStyle == null)
            {
                _orangeLabelStyle = new GUIStyle(GUI.skin.label);
                _orangeLabelStyle.normal.textColor = new Color(1f, 0.62f, 0.18f, 1f);
            }
        }

        private static void ClampWindowToScreen()
        {
            float scaledScreenWidth = Screen.width / UiScale;
            float scaledScreenHeight = Screen.height / UiScale;
            _windowRect.width = Mathf.Min(_windowRect.width, Mathf.Max(520f, scaledScreenWidth - 40f));
            _windowRect.height = Mathf.Min(_windowRect.height, Mathf.Max(360f, scaledScreenHeight - 40f));
            _windowRect.x = Mathf.Clamp(_windowRect.x, 0f, Mathf.Max(0f, scaledScreenWidth - _windowRect.width));
            _windowRect.y = Mathf.Clamp(_windowRect.y, 0f, Mathf.Max(0f, scaledScreenHeight - _windowRect.height));
        }
    }
}
