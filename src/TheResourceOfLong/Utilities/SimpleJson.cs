using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace TheResourceOfLong
{
    public static class SimpleJson
    {
        public static Dictionary<string, object> ParseObject(string json)
        {
            object value = new Parser(json).ParseValue();
            Dictionary<string, object> result = value as Dictionary<string, object>;
            if (result == null) throw new FormatException("JSON root is not an object.");
            return result;
        }

        public static string Serialize(ResourceProbeConfig config)
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("{");
            builder.AppendLine("  \"EnableResourceProbe\": " + Bool(config.EnableResourceProbe) + ",");
            builder.AppendLine("  \"LogMisses\": " + Bool(config.LogMisses) + ",");
            builder.AppendLine("  \"LogStackTrace\": " + Bool(config.LogStackTrace) + ",");
            builder.AppendLine("  \"LogLoadAll\": " + Bool(config.LogLoadAll) + ",");
            builder.AppendLine("  \"EnableContainerProbe\": " + Bool(config.EnableContainerProbe) + ",");
            builder.AppendLine("  \"EnableContainerProbeOverlay\": " + Bool(config.EnableContainerProbeOverlay) + ",");
            builder.AppendLine("  \"EnableScenePrefabUiProbe\": " + Bool(config.EnableScenePrefabUiProbe) + ",");
            builder.AppendLine("  \"EnableResourceManifestGenerator\": " + Bool(config.EnableResourceManifestGenerator) + ",");
            builder.AppendLine("  \"EnableMappingRulesGenerator\": " + Bool(config.EnableMappingRulesGenerator) + ",");
            builder.AppendLine("  \"MaxStackTraceLength\": " + config.MaxStackTraceLength.ToString(CultureInfo.InvariantCulture));
            builder.Append("}");
            return builder.ToString();
        }

        public static bool TryGetValueIgnoreCase(Dictionary<string, object> values, string key, out object value)
        {
            value = null;
            if (values == null) return false;

            foreach (KeyValuePair<string, object> pair in values)
            {
                if (string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    value = pair.Value;
                    return true;
                }
            }

            return false;
        }

        public static string GetString(Dictionary<string, object> values, string key)
        {
            object value;
            if (!TryGetValueIgnoreCase(values, key, out value) || value == null) return null;
            return Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        public static bool GetBool(Dictionary<string, object> values, string key, bool defaultValue)
        {
            object value;
            if (!TryGetValueIgnoreCase(values, key, out value) || value == null) return defaultValue;
            if (value is bool) return (bool)value;

            bool parsed;
            return bool.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), out parsed) ? parsed : defaultValue;
        }

        public static int GetInt(Dictionary<string, object> values, string key, int defaultValue)
        {
            object value;
            if (!TryGetValueIgnoreCase(values, key, out value) || value == null) return defaultValue;

            int parsed;
            return int.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed)
                ? parsed
                : defaultValue;
        }

        public static float? GetNullableFloat(Dictionary<string, object> values, string key)
        {
            object value;
            if (!TryGetValueIgnoreCase(values, key, out value) || value == null) return null;

            float parsed;
            return float.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture, out parsed)
                ? (float?)parsed
                : null;
        }

        private static string Bool(bool value)
        {
            return value ? "true" : "false";
        }

        private sealed class Parser
        {
            private readonly string _json;
            private int _index;

            public Parser(string json)
            {
                _json = json ?? string.Empty;
            }

            public object ParseValue()
            {
                SkipWhiteSpace();
                if (_index >= _json.Length) throw new FormatException("Unexpected end of JSON.");

                char c = _json[_index];
                if (c == '{') return ParseObjectInternal();
                if (c == '[') return ParseArray();
                if (c == '"') return ParseString();
                if (c == 't') return ParseLiteral("true", true);
                if (c == 'f') return ParseLiteral("false", false);
                if (c == 'n') return ParseLiteral("null", null);
                if (c == '-' || char.IsDigit(c)) return ParseNumber();

                throw new FormatException("Unexpected JSON token '" + c + "' at position " + _index + ".");
            }

            private Dictionary<string, object> ParseObjectInternal()
            {
                Dictionary<string, object> result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                Expect('{');
                SkipWhiteSpace();

                if (TryConsume('}')) return result;

                while (true)
                {
                    SkipWhiteSpace();
                    string key = ParseString();
                    SkipWhiteSpace();
                    Expect(':');
                    object value = ParseValue();
                    result[key] = value;
                    SkipWhiteSpace();

                    if (TryConsume('}')) return result;
                    Expect(',');
                }
            }

            private object[] ParseArray()
            {
                List<object> result = new List<object>();
                Expect('[');
                SkipWhiteSpace();

                if (TryConsume(']')) return result.ToArray();

                while (true)
                {
                    result.Add(ParseValue());
                    SkipWhiteSpace();

                    if (TryConsume(']')) return result.ToArray();
                    Expect(',');
                }
            }

            private string ParseString()
            {
                Expect('"');
                StringBuilder builder = new StringBuilder();

                while (_index < _json.Length)
                {
                    char c = _json[_index++];
                    if (c == '"') return builder.ToString();

                    if (c != '\\')
                    {
                        builder.Append(c);
                        continue;
                    }

                    if (_index >= _json.Length) throw new FormatException("Unexpected end of JSON string escape.");
                    char escaped = _json[_index++];
                    switch (escaped)
                    {
                        case '"':
                        case '\\':
                        case '/':
                            builder.Append(escaped);
                            break;
                        case 'b':
                            builder.Append('\b');
                            break;
                        case 'f':
                            builder.Append('\f');
                            break;
                        case 'n':
                            builder.Append('\n');
                            break;
                        case 'r':
                            builder.Append('\r');
                            break;
                        case 't':
                            builder.Append('\t');
                            break;
                        case 'u':
                            builder.Append(ParseUnicodeEscape());
                            break;
                        default:
                            throw new FormatException("Unsupported JSON string escape '\\" + escaped + "'.");
                    }
                }

                throw new FormatException("Unterminated JSON string.");
            }

            private char ParseUnicodeEscape()
            {
                if (_index + 4 > _json.Length) throw new FormatException("Invalid JSON unicode escape.");

                string hex = _json.Substring(_index, 4);
                _index += 4;
                return (char)int.Parse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            }

            private object ParseNumber()
            {
                int start = _index;
                if (_json[_index] == '-') _index++;
                while (_index < _json.Length && char.IsDigit(_json[_index])) _index++;

                bool isFloat = false;
                if (_index < _json.Length && _json[_index] == '.')
                {
                    isFloat = true;
                    _index++;
                    while (_index < _json.Length && char.IsDigit(_json[_index])) _index++;
                }

                if (_index < _json.Length && (_json[_index] == 'e' || _json[_index] == 'E'))
                {
                    isFloat = true;
                    _index++;
                    if (_index < _json.Length && (_json[_index] == '+' || _json[_index] == '-')) _index++;
                    while (_index < _json.Length && char.IsDigit(_json[_index])) _index++;
                }

                string text = _json.Substring(start, _index - start);
                if (isFloat)
                {
                    double parsedDouble;
                    if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out parsedDouble)) return parsedDouble;
                }
                else
                {
                    int parsedInt;
                    if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsedInt)) return parsedInt;

                    long parsedLong;
                    if (long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsedLong)) return parsedLong;
                }

                throw new FormatException("Invalid JSON number '" + text + "'.");
            }

            private object ParseLiteral(string literal, object value)
            {
                if (_index + literal.Length > _json.Length ||
                    !string.Equals(_json.Substring(_index, literal.Length), literal, StringComparison.Ordinal))
                {
                    throw new FormatException("Invalid JSON literal at position " + _index + ".");
                }

                _index += literal.Length;
                return value;
            }

            private void SkipWhiteSpace()
            {
                while (_index < _json.Length && char.IsWhiteSpace(_json[_index])) _index++;
            }

            private bool TryConsume(char expected)
            {
                if (_index < _json.Length && _json[_index] == expected)
                {
                    _index++;
                    return true;
                }

                return false;
            }

            private void Expect(char expected)
            {
                SkipWhiteSpace();
                if (_index >= _json.Length || _json[_index] != expected)
                {
                    throw new FormatException("Expected '" + expected + "' at position " + _index + ".");
                }

                _index++;
            }
        }
    }
}
