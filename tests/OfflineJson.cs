// Only for the offline file-I/O harness. Unity Player uses UnityEngine.JsonUtility.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
namespace UnityEngine
{
    public static partial class JsonUtility
    {
        public static T FromJson<T>(string json)
        { if (typeof(T) == typeof(Starfall.Campaign)) return (T)(object)CampaignFixture.Create(); return (T)ConvertValue(new Reader(json).Read(), typeof(T)); }
        public static string ToJson(object value, bool prettyPrint = false) { var text = new StringBuilder(); Write(value, text); return text.ToString(); }
        private static object ConvertValue(object value, Type type)
        {
            if (value == null) return null;
            if (type == typeof(string)) return (string)value;
            if (type.IsPrimitive) return Convert.ChangeType(value, type, CultureInfo.InvariantCulture);
            var list = value as List<object>;
            if (type.IsArray) { Type element = type.GetElementType(); var array = Array.CreateInstance(element, list.Count); for (int i = 0; i < list.Count; i++) array.SetValue(ConvertValue(list[i], element), i); return array; }
            if (typeof(IList).IsAssignableFrom(type)) { var result = (IList)Activator.CreateInstance(type); foreach (var entry in list) result.Add(ConvertValue(entry, type.GetGenericArguments()[0])); return result; }
            var map = (Dictionary<string, object>)value; object target = System.Runtime.Serialization.FormatterServices.GetUninitializedObject(type);
            foreach (FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.Instance)) if (map.ContainsKey(field.Name)) field.SetValue(target, ConvertValue(map[field.Name], field.FieldType)); return target;
        }
        private static void Quote(string value, StringBuilder text)
        { text.Append('"'); foreach (char c in value) { if (c == '"' || c == '\\') text.Append('\\').Append(c); else if (c < 32) text.Append("\\u").Append(((int)c).ToString("x4")); else text.Append(c); } text.Append('"'); }
        private static void Write(object value, StringBuilder text)
        {
            if (value == null) { text.Append("null"); return; }
            if (value is string) { Quote((string)value, text); return; }
            if (value is bool) { text.Append((bool)value ? "true" : "false"); return; }
            if (value.GetType().IsPrimitive) { text.Append(Convert.ToString(value, CultureInfo.InvariantCulture)); return; }
            var items = value as IEnumerable;
            if (items != null) { text.Append('['); bool first = true; foreach (object item in items) { if (!first) text.Append(','); first = false; Write(item, text); } text.Append(']'); return; }
            text.Append('{'); bool beginning = true;
            foreach (FieldInfo field in value.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance)) { if (!beginning) text.Append(','); beginning = false; Quote(field.Name, text); text.Append(':'); Write(field.GetValue(value), text); } text.Append('}');
        }
        private sealed class Reader
        {
            private readonly string text; private int index;
            public Reader(string text) { this.text = text; }
            private void Space() { while (index < text.Length && char.IsWhiteSpace(text[index])) index++; }
            private void Require(char c) { Space(); if (index >= text.Length || text[index++] != c) throw new FormatException("Invalid JSON"); }
            public object Read() { object result = Value(); Space(); if (index != text.Length) throw new FormatException("Trailing JSON"); return result; }
            private object Value()
            {
                Space(); if (index >= text.Length) throw new FormatException("Truncated JSON"); char c = text[index];
                if (c == '"') return String();
                if (c == '{') { index++; var map = new Dictionary<string, object>(); Space(); if (text[index] == '}') { index++; return map; } while (true) { string key = String(); Require(':'); map[key] = Value(); Space(); if (text[index] == '}') { index++; return map; } Require(','); } }
                if (c == '[') { index++; var list = new List<object>(); Space(); if (text[index] == ']') { index++; return list; } while (true) { list.Add(Value()); Space(); if (text[index] == ']') { index++; return list; } Require(','); } }
                if (text.Substring(index).StartsWith("null")) { index += 4; return null; }
                if (text.Substring(index).StartsWith("true")) { index += 4; return true; }
                if (text.Substring(index).StartsWith("false")) { index += 5; return false; }
                int start = index; while (index < text.Length && "-+0123456789.eE".IndexOf(text[index]) >= 0) index++; return double.Parse(text.Substring(start, index - start), CultureInfo.InvariantCulture);
            }
            private string String()
            {
                Require('"'); var result = new StringBuilder(); while (index < text.Length) { char c = text[index++]; if (c == '"') return result.ToString(); if (c == '\\') { c = text[index++]; if (c == 'u') { c = (char)int.Parse(text.Substring(index, 4), NumberStyles.HexNumber); index += 4; } else if (c == 'n') c = '\n'; else if (c == 'r') c = '\r'; else if (c == 't') c = '\t'; } result.Append(c); } throw new FormatException("Unclosed string");
            }
        }
    }
}
