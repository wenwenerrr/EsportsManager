using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;

namespace EsportsManager.Players
{
    /// <summary>
    /// Strict structural check of JSON text against a wire-format DTO type, run BEFORE
    /// <c>JsonUtility.FromJson</c>. JsonUtility silently coerces wrong value types (measured on Unity
    /// 6000.6: "3" into a float gives 3, "abc"/true/[1]/{} into a float give 0, 2.7 into an int gives 2,
    /// a number into a string gives its text, a scalar into an array gives an empty array, an explicit
    /// null on a number gives 0, Infinity is accepted, duplicate keys keep the first) and ignores
    /// unknown keys. This checker rejects all of those, so the DTO only ever sees well-typed data.
    /// </summary>
    /// <remarks>
    /// Mapping rules: <c>float</c> needs a finite JSON number inside float range; <c>int</c> needs a
    /// JSON number with an integral value inside int range; <c>string</c> needs a JSON string; arrays
    /// need a JSON array whose elements match the element type; any other class needs a JSON object
    /// whose keys are exactly the type's public instance fields (missing or unknown key = error).
    /// Thread-safe (no shared state). Allocates; intended for load time only.
    /// </remarks>
    /// <example><code>JsonShapeChecker.Check(json, typeof(MyDto), "PlayerConfig"); // throws InvalidOperationException</code></example>
    internal static class JsonShapeChecker
    {
        private sealed class JsonNumber { public string Text; }
        private sealed class JsonNull { public static readonly JsonNull Instance = new JsonNull(); }

        /// <summary>Parses <paramref name="json"/> strictly and checks it against <paramref name="dtoType"/>.</summary>
        /// <exception cref="InvalidOperationException">Malformed JSON, wrong type, missing/unknown/duplicate key, non-finite number.</exception>
        public static void Check(string json, Type dtoType, string owner)
        {
            var parser = new Parser(json, owner);
            object root = parser.ParseDocument();
            CheckValue(root, dtoType, "", owner);
        }

        private static string Join(string path, string key) => path.Length == 0 ? key : path + "." + key;

        private static InvalidOperationException Fail(string owner, string message) =>
            new InvalidOperationException(owner + ": " + message);

        private static void CheckValue(object node, Type type, string path, string owner)
        {
            string where = path.Length == 0 ? "the root" : "'" + path + "'";
            if (node is JsonNull) throw Fail(owner, "JSON null is not allowed at " + where + "; write a value.");

            if (type == typeof(float) || type == typeof(int))
            {
                if (!(node is JsonNumber num))
                    throw Fail(owner, "key " + where + " must be a number, got " + Describe(node) + ".");
                if (!double.TryParse(num.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double v) || double.IsInfinity(v) || double.IsNaN(v))
                    throw Fail(owner, "key " + where + " is not a finite number: " + num.Text + ".");
                if (type == typeof(float) && Math.Abs(v) > float.MaxValue)
                    throw Fail(owner, "key " + where + " is not a finite float: " + num.Text + ".");
                if (type == typeof(int) && (v != Math.Floor(v) || v < int.MinValue || v > int.MaxValue))
                    throw Fail(owner, "key " + where + " must be a whole number inside int range, got " + num.Text + ".");
                return;
            }
            if (type == typeof(string))
            {
                if (!(node is string)) throw Fail(owner, "key " + where + " must be a string, got " + Describe(node) + ".");
                return;
            }
            if (type.IsArray)
            {
                if (!(node is List<object> list)) throw Fail(owner, "key " + where + " must be an array, got " + Describe(node) + ".");
                var element = type.GetElementType();
                for (int i = 0; i < list.Count; i++) CheckValue(list[i], element, path + "[" + i + "]", owner);
                return;
            }
            if (type.IsClass)
            {
                if (!(node is Dictionary<string, object> obj)) throw Fail(owner, "key " + where + " must be an object, got " + Describe(node) + ".");
                var fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);
                foreach (var f in fields)
                    if (!obj.ContainsKey(f.Name)) throw Fail(owner, "missing or misspelled key '" + Join(path, f.Name) + "'.");
                foreach (var key in obj.Keys)
                    if (Array.Find(fields, f => f.Name == key) == null)
                        throw Fail(owner, "unknown key '" + Join(path, key) + "' (misspelled?).");
                foreach (var f in fields) CheckValue(obj[f.Name], f.FieldType, Join(path, f.Name), owner);
                return;
            }
            throw Fail(owner, "unsupported DTO field type " + type.Name + " at " + where + ".");
        }

        private static string Describe(object node)
        {
            switch (node)
            {
                case string s: return "string \"" + s + "\"";
                case JsonNumber n: return "number " + n.Text;
                case bool b: return b ? "true" : "false";
                case List<object> _: return "an array";
                case Dictionary<string, object> _: return "an object";
                default: return "null";
            }
        }

        /// <summary>Minimal strict RFC 8259 parser producing Dictionary / List / string / JsonNumber / bool / JsonNull.</summary>
        private sealed class Parser
        {
            private readonly string _s;
            private readonly string _owner;
            private int _i;

            public Parser(string s, string owner) { _s = s; _owner = owner; }

            public object ParseDocument()
            {
                object v = ParseValue();
                SkipWs();
                if (_i != _s.Length) throw Error("unexpected text after the JSON value");
                return v;
            }

            private InvalidOperationException Error(string what) =>
                new InvalidOperationException(_owner + ": JSON could not be parsed: " + what + " at character " + _i + ".");

            private void SkipWs()
            {
                while (_i < _s.Length && (_s[_i] == ' ' || _s[_i] == '\t' || _s[_i] == '\n' || _s[_i] == '\r')) _i++;
            }

            private object ParseValue()
            {
                SkipWs();
                if (_i >= _s.Length) throw Error("unexpected end");
                char c = _s[_i];
                if (c == '{') return ParseObject();
                if (c == '[') return ParseArray();
                if (c == '"') return ParseString();
                if (c == '-' || (c >= '0' && c <= '9')) return ParseNumber();
                if (Literal("true")) return true;
                if (Literal("false")) return false;
                if (Literal("null")) return JsonNull.Instance;
                throw Error("unexpected character '" + c + "'");
            }

            private bool Literal(string word)
            {
                if (string.CompareOrdinal(_s, _i, word, 0, word.Length) != 0) return false;
                _i += word.Length;
                return true;
            }

            private Dictionary<string, object> ParseObject()
            {
                var obj = new Dictionary<string, object>();
                _i++; // {
                SkipWs();
                if (_i < _s.Length && _s[_i] == '}') { _i++; return obj; }
                while (true)
                {
                    SkipWs();
                    if (_i >= _s.Length || _s[_i] != '"') throw Error("expected a key");
                    string key = ParseString();
                    SkipWs();
                    if (_i >= _s.Length || _s[_i] != ':') throw Error("expected ':'");
                    _i++;
                    object value = ParseValue();
                    if (obj.ContainsKey(key)) throw new InvalidOperationException(_owner + ": duplicate key '" + key + "' at character " + _i + ".");
                    obj[key] = value;
                    SkipWs();
                    if (_i >= _s.Length) throw Error("unexpected end in object");
                    if (_s[_i] == ',') { _i++; continue; }
                    if (_s[_i] == '}') { _i++; return obj; }
                    throw Error("expected ',' or '}'");
                }
            }

            private List<object> ParseArray()
            {
                var list = new List<object>();
                _i++; // [
                SkipWs();
                if (_i < _s.Length && _s[_i] == ']') { _i++; return list; }
                while (true)
                {
                    list.Add(ParseValue());
                    SkipWs();
                    if (_i >= _s.Length) throw Error("unexpected end in array");
                    if (_s[_i] == ',') { _i++; continue; }
                    if (_s[_i] == ']') { _i++; return list; }
                    throw Error("expected ',' or ']'");
                }
            }

            private string ParseString()
            {
                var sb = new StringBuilder();
                _i++; // opening quote
                while (true)
                {
                    if (_i >= _s.Length) throw Error("unterminated string");
                    char c = _s[_i++];
                    if (c == '"') return sb.ToString();
                    if (c < 0x20) throw Error("control character in string");
                    if (c != '\\') { sb.Append(c); continue; }
                    if (_i >= _s.Length) throw Error("unterminated escape");
                    char e = _s[_i++];
                    switch (e)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            if (_i + 4 > _s.Length || !int.TryParse(_s.Substring(_i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int code))
                                throw Error("bad \\u escape");
                            sb.Append((char)code);
                            _i += 4;
                            break;
                        default: throw Error("bad escape '\\" + e + "'");
                    }
                }
            }

            private static bool IsD(char c) => c >= '0' && c <= '9';

            private JsonNumber ParseNumber()
            {
                int start = _i;
                if (_s[_i] == '-') _i++;
                if (_i >= _s.Length) throw Error("bad number");
                if (_s[_i] == '0') _i++;
                else if (_s[_i] >= '1' && _s[_i] <= '9') while (_i < _s.Length && IsD(_s[_i])) _i++;
                else throw Error("bad number");
                if (_i < _s.Length && _s[_i] == '.')
                {
                    _i++;
                    if (_i >= _s.Length || !IsD(_s[_i])) throw Error("bad number");
                    while (_i < _s.Length && IsD(_s[_i])) _i++;
                }
                if (_i < _s.Length && (_s[_i] == 'e' || _s[_i] == 'E'))
                {
                    _i++;
                    if (_i < _s.Length && (_s[_i] == '+' || _s[_i] == '-')) _i++;
                    if (_i >= _s.Length || !IsD(_s[_i])) throw Error("bad number");
                    while (_i < _s.Length && IsD(_s[_i])) _i++;
                }
                return new JsonNumber { Text = _s.Substring(start, _i - start) };
            }
        }
    }
}
