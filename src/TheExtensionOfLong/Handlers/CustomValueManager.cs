using Il2Cpp;

namespace TheExtensionOfLong
{
    public static class CustomValueManager
    {
        public static string GetKey(string objectType, string objectID, string propertyName)
        {
            if (string.IsNullOrWhiteSpace(objectType) ||
                string.IsNullOrWhiteSpace(objectID) ||
                string.IsNullOrWhiteSpace(propertyName))
            {
                return null;
            }

            return $"{objectType.Trim()}.{objectID.Trim()}.{propertyName.Trim()}";
        }

        public static bool SetRaw(string objectType, string objectID, string propertyName, string value)
        {
            string key = GetKey(objectType, objectID, propertyName);
            if (string.IsNullOrEmpty(key))
                return false;

            PlotEventLogData logData = CommonHandlers.GetPlotEventLogData();
            if (logData == null)
                return false;

            logData.Set(key, string.IsNullOrEmpty(value) ? null : value);
            return true;
        }

        public static string GetRaw(string objectType, string objectID, string propertyName)
        {
            string key = GetKey(objectType, objectID, propertyName);
            if (string.IsNullOrEmpty(key))
                return "";

            PlotEventLogData logData = CommonHandlers.GetPlotEventLogData();
            if (logData == null || !logData.HaveKey(key))
                return "";

            return logData.Get(key) ?? "";
        }

        public static string GetIntString(string objectType, string objectID, string propertyName)
        {
            string raw = GetRaw(objectType, objectID, propertyName);
            string numberText = (raw ?? "").Trim().Replace("负", "-");
            return int.TryParse(numberText, out int value) ? value.ToString() : "0";
        }

        public static string GetFloatString(string objectType, string objectID, string propertyName)
        {
            string raw = GetRaw(objectType, objectID, propertyName);
            string numberText = (raw ?? "").Trim().Replace("负", "-");
            return float.TryParse(numberText, out float value) ? value.ToString("G") : "0.0";
        }

        public static string GetBoolString(string objectType, string objectID, string propertyName)
        {
            string raw = GetRaw(objectType, objectID, propertyName);
            return CommonHandlers.TryParseStrictBool(raw, out bool value) && value ? "1" : "0";
        }
    }
}
