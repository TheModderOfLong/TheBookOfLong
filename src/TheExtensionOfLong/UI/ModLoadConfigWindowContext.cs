using System;

namespace TheExtensionOfLong
{
    internal sealed class ModLoadConfigWindowContext
    {
        public ModLoadConfigDocument Document { get; set; }
        public Action CloseRequested { get; set; }
        public Action RefreshRequested { get; set; }
        public string SelectedFolderName { get; private set; }

        public void Close()
        {
            if (CloseRequested != null) CloseRequested();
        }

        public void MarkDirty(string message)
        {
            if (Document == null) return;
            Document.IsDirty = true;
            Document.LastMessage = message;
            Refresh();
        }

        public void MoveEntry(int fromIndex, int toIndex)
        {
            if (Document == null) return;
            if (fromIndex < 0 || fromIndex >= Document.Entries.Count) return;
            if (toIndex < 0) toIndex = 0;
            if (toIndex >= Document.Entries.Count) toIndex = Document.Entries.Count - 1;
            if (fromIndex == toIndex) return;

            ModLoadConfigEntry entry = Document.Entries[fromIndex];
            Document.Entries.RemoveAt(fromIndex);
            Document.Entries.Insert(toIndex, entry);
            SelectedFolderName = entry.FolderName;
            MarkDirty("已调整加载顺序。");
        }

        public void SetAllEnabled(bool enabled)
        {
            if (Document == null) return;

            bool changed = false;
            for (int i = 0; i < Document.Entries.Count; i++)
            {
                if (Document.Entries[i].Enabled != enabled)
                {
                    Document.Entries[i].Enabled = enabled;
                    changed = true;
                }
            }

            if (changed)
            {
                MarkDirty(enabled ? "已全部启用。" : "已全部禁用。");
            }
        }

        public void SaveDocument()
        {
            if (Document == null) return;

            try
            {
                ModLoadConfigService.SaveDocument(Document);
                Refresh();
            }
            catch (Exception ex)
            {
                Document.LastMessage = "保存失败: " + ex.Message;
                LoggerManager.Error("ModLoadConfigWindow: 保存失败 - " + ex.Message);
                Refresh();
            }
        }

        public void ReloadDocument()
        {
            try
            {
                Document = ModLoadConfigService.LoadDocument();
                EnsureSelectedEntry();
                Document.LastMessage = "已重新扫描目录并合并现有配置。";
                Refresh();
            }
            catch (Exception ex)
            {
                if (Document != null) Document.LastMessage = "重新扫描失败: " + ex.Message;
                LoggerManager.Error("ModLoadConfigWindow: 重新扫描失败 - " + ex.Message);
                Refresh();
            }
        }

        public void Refresh()
        {
            EnsureSelectedEntry();
            if (RefreshRequested != null) RefreshRequested();
        }

        public void SelectEntry(ModLoadConfigEntry entry)
        {
            if (entry == null) return;
            SelectedFolderName = entry.FolderName;
            Refresh();
        }

        public ModLoadConfigEntry GetSelectedEntry()
        {
            EnsureSelectedEntry();
            if (Document == null || Document.Entries == null) return null;

            for (int i = 0; i < Document.Entries.Count; i++)
            {
                ModLoadConfigEntry entry = Document.Entries[i];
                if (entry != null && string.Equals(entry.FolderName, SelectedFolderName, StringComparison.OrdinalIgnoreCase))
                {
                    return entry;
                }
            }

            return null;
        }

        public bool IsSelected(ModLoadConfigEntry entry)
        {
            return entry != null && string.Equals(entry.FolderName, SelectedFolderName, StringComparison.OrdinalIgnoreCase);
        }

        public void EnsureSelectedEntry()
        {
            if (Document == null || Document.Entries == null || Document.Entries.Count == 0)
            {
                SelectedFolderName = string.Empty;
                return;
            }

            for (int i = 0; i < Document.Entries.Count; i++)
            {
                ModLoadConfigEntry entry = Document.Entries[i];
                if (entry != null && string.Equals(entry.FolderName, SelectedFolderName, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            SelectedFolderName = Document.Entries[0].FolderName;
        }
    }
}
