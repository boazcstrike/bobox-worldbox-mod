using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace BoBox.Rules
{
    /// <summary>
    /// Walks a rules JSON tree along the <see cref="ColonyRules"/> contract. Drops keys the contract
    /// does not know and clamps numbers to <see cref="RulesLimits"/>, noting each change in
    /// <see cref="Warnings"/> (a NaN becomes the field's default), and warns about capacity ids the
    /// planner does not know. A numeric string where a limited number belongs ("1e9", "NaN") is read
    /// as that number and clamped the same way. Returns a new tree and leaves its input alone. A value
    /// of the wrong type is left for the deserializer to reject.
    /// </summary>
    internal sealed class RulesSanitizer
    {
        private const double MaxExactLong = 9e15;

        private static readonly JToken Defaults = RulesJson.ToJToken(ColonyRules.Default);

        private readonly List<string> _warnings = new List<string>();

        public IReadOnlyList<string> Warnings => _warnings;

        public JToken Run(JToken rules)
        {
            return Walk(rules, typeof(ColonyRules), string.Empty, string.Empty, Defaults);
        }

        // fallback is the shipped default at this position, or null where the defaults have none.
        private JToken Walk(JToken token, Type type, string path, string pattern, JToken fallback)
        {
            JsonContract contract = RulesJson.Resolver.ResolveContract(type);
            if (token is JObject obj && contract is JsonObjectContract objectContract)
            {
                return WalkObject(obj, objectContract, path, pattern, fallback);
            }

            if (token is JObject map && contract is JsonDictionaryContract dictionaryContract)
            {
                return WalkMap(map, dictionaryContract, path, pattern, fallback);
            }

            if (token is JArray array && contract is JsonArrayContract arrayContract)
            {
                return WalkArray(array, arrayContract, path, pattern);
            }

            return token is JValue value ? Clamp(value, path, pattern, fallback) : token;
        }

        private JToken WalkObject(JObject obj, JsonObjectContract contract, string path, string pattern, JToken fallback)
        {
            var result = new JObject();
            foreach (JProperty property in obj.Properties())
            {
                JsonProperty known = contract.Properties.GetProperty(property.Name, StringComparison.Ordinal);
                string childPath = Join(path, property.Name);
                if (known == null || known.Ignored)
                {
                    _warnings.Add($"unknown key '{childPath}' ignored");
                    continue;
                }

                JToken childFallback = (fallback as JObject)?[property.Name];
                result.Add(property.Name, Walk(property.Value, known.PropertyType, childPath, Join(pattern, property.Name), childFallback));
            }

            return WithConstructorDefaults(result, contract);
        }

        // Newtonsoft passes default(T) for a constructor argument missing from the JSON; fill in the
        // declared default instead (see RulesJson), so a new entry without max_ceiling has no ceiling.
        private static JObject WithConstructorDefaults(JObject obj, JsonObjectContract contract)
        {
            var result = new JObject(obj);
            foreach (JsonProperty parameter in contract.CreatorParameters)
            {
                if (obj[parameter.PropertyName] == null && parameter.DefaultValue is IConvertible)
                {
                    result.Add(parameter.PropertyName, new JValue(parameter.DefaultValue));
                }
            }

            return result;
        }

        private JToken WalkMap(JObject map, JsonDictionaryContract contract, string path, string pattern, JToken fallback)
        {
            var result = new JObject();
            foreach (JProperty property in map.Properties())
            {
                if (RulesKnownIds.IsUnknown(pattern, property.Name, out string kind))
                {
                    _warnings.Add($"unknown {kind} id '{property.Name}' in '{path}', kept");
                }

                result.Add(property.Name, Walk(
                    property.Value, contract.DictionaryValueType, Join(path, property.Name), Join(pattern, "*"), (fallback as JObject)?[property.Name]));
            }

            return result;
        }

        private JToken WalkArray(JArray array, JsonArrayContract contract, string path, string pattern)
        {
            var result = new JArray();
            for (int i = 0; i < array.Count; i++)
            {
                result.Add(Walk(array[i], contract.CollectionItemType, $"{path}[{i}]", pattern + "[]", null));
            }

            return result;
        }

        private JToken Clamp(JValue value, string path, string pattern, JToken fallback)
        {
            if (!RulesLimits.TryGet(pattern, out RulesLimits.Range range) || !TryReadNumber(value, out double number))
            {
                return value;
            }

            bool isWhole = value.Type == JTokenType.Integer || (value.Type == JTokenType.String && !IsFractional(number));
            double clamped = double.IsNaN(number) ? DefaultFor(fallback, range) : Math.Min(range.Max, Math.Max(range.Min, number));
            if (clamped.Equals(number) && value.Type != JTokenType.String)
            {
                return value;
            }

            if (!clamped.Equals(number))
            {
                _warnings.Add(FormattableString.Invariant(
                    $"{path} = {number} is outside {range.Min}..{range.Max}, clamped to {clamped}"));
            }

            return isWhole && !IsFractional(clamped) && Math.Abs(clamped) < MaxExactLong ? new JValue((long)clamped) : new JValue(clamped);
        }

        // A JSON number, or a string Newtonsoft would convert to one ("1e9", "NaN", "Infinity", "5000").
        private static bool TryReadNumber(JValue value, out double number)
        {
            number = 0;
            switch (value.Type)
            {
                case JTokenType.Integer:
                case JTokenType.Float:
                    number = (double)value;
                    return true;
                case JTokenType.String:
                    return double.TryParse((string)value, NumberStyles.Float, CultureInfo.InvariantCulture, out number);
                default:
                    return false;
            }
        }

        private static bool IsFractional(double number)
        {
            return !number.Equals(Math.Floor(number));
        }

        // A NaN has no side of the range to fall to, so it takes the field's shipped default. Array
        // entries and ids the defaults lack have no counterpart, so they take the range minimum.
        private static double DefaultFor(JToken fallback, RulesLimits.Range range)
        {
            bool isNumber = fallback is JValue number && (number.Type == JTokenType.Integer || number.Type == JTokenType.Float);
            return isNumber ? Math.Min(range.Max, Math.Max(range.Min, (double)fallback)) : range.Min;
        }

        private static string Join(string path, string name)
        {
            return path.Length == 0 ? name : path + "." + name;
        }
    }
}
