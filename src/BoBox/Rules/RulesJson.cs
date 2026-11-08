using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace BoBox.Rules
{
    /// <summary>
    /// The one JSON shape for <see cref="ColonyRules"/>: snake_case property names, enum values as
    /// snake_case strings, dictionary keys (job and building type ids) kept as they are. Used by the
    /// loader, by rules/bobox-rules.json, and by the GET /rules endpoint. Integer enum values are
    /// refused, so a tag written as 99 or "1" fails the load instead of becoming an undefined or
    /// wrong <see cref="BoBox.Planner.Scenario.ScenarioTag"/>.
    /// </summary>
    public static class RulesJson
    {
        private static readonly SnakeCaseNamingStrategy SnakeCase = new SnakeCaseNamingStrategy();

        internal static readonly IContractResolver Resolver = new RulesContractResolver();

        public static JsonSerializerSettings Settings()
        {
            return new JsonSerializerSettings
            {
                ContractResolver = Resolver,
                Converters = { new StringEnumConverter(SnakeCase, false) },
                Formatting = Formatting.Indented,
            };
        }

        public static string Serialize(ColonyRules rules)
        {
            return JsonConvert.SerializeObject(rules, Settings());
        }

        public static JToken ToJToken(ColonyRules rules)
        {
            return JToken.FromObject(rules, JsonSerializer.Create(Settings()));
        }

        /// <summary>
        /// Newtonsoft passes default(T) for a constructor argument missing from the JSON, ignoring the
        /// argument's own default. This resolver records the declared default on the creator parameter
        /// so <see cref="RulesSanitizer"/> can fill a missing key with it (for example a capacity entry
        /// without max_ceiling).
        /// </summary>
        private sealed class RulesContractResolver : DefaultContractResolver
        {
            public RulesContractResolver()
            {
                NamingStrategy = SnakeCase;
            }

            protected override IList<JsonProperty> CreateConstructorParameters(
                ConstructorInfo constructor, JsonPropertyCollection memberProperties)
            {
                IList<JsonProperty> properties = base.CreateConstructorParameters(constructor, memberProperties);
                ParameterInfo[] parameters = constructor.GetParameters();
                foreach (JsonProperty property in properties)
                {
                    foreach (ParameterInfo parameter in parameters)
                    {
                        if (parameter.Name == property.UnderlyingName && parameter.HasDefaultValue)
                        {
                            property.DefaultValue = parameter.DefaultValue;
                        }
                    }
                }

                return properties;
            }
        }
    }
}
