using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace LookImportant.EditorTools
{
    /// <summary>Just enough JSON for materials.json (objects, arrays, strings, numbers, bools).</summary>
    static class MiniJson
    {
        public static object Parse(string s)
        {
            int i = 0;
            return Value(s, ref i);
        }

        static void Skip(string s, ref int i)
        {
            while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
        }

        static object Value(string s, ref int i)
        {
            Skip(s, ref i);
            char c = s[i];
            if (c == '{')
            {
                i++;
                var d = new Dictionary<string, object>();
                Skip(s, ref i);
                if (s[i] == '}') { i++; return d; }
                while (true)
                {
                    var key = (string)Value(s, ref i);
                    Skip(s, ref i);
                    i++; // ':'
                    d[key] = Value(s, ref i);
                    Skip(s, ref i);
                    if (s[i++] == '}') return d;
                }
            }
            if (c == '[')
            {
                i++;
                var list = new List<object>();
                Skip(s, ref i);
                if (s[i] == ']') { i++; return list; }
                while (true)
                {
                    list.Add(Value(s, ref i));
                    Skip(s, ref i);
                    if (s[i++] == ']') return list;
                }
            }
            if (c == '"')
            {
                i++;
                var sb = new StringBuilder();
                while (s[i] != '"')
                {
                    if (s[i] == '\\') i++;
                    sb.Append(s[i++]);
                }
                i++;
                return sb.ToString();
            }
            if (string.CompareOrdinal(s, i, "true", 0, 4) == 0) { i += 4; return true; }
            if (string.CompareOrdinal(s, i, "false", 0, 5) == 0) { i += 5; return false; }
            if (string.CompareOrdinal(s, i, "null", 0, 4) == 0) { i += 4; return null; }
            int start = i;
            while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
            return double.Parse(s.Substring(start, i - start), CultureInfo.InvariantCulture);
        }
    }
}
