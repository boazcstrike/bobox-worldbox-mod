using Newtonsoft.Json.Linq;

namespace BoBox.Bridge
{
    internal static class RequestFields
    {
        public static string RequireString(JObject body, string name)
        {
            JToken token = body[name];
            if (token == null || token.Type != JTokenType.String || string.IsNullOrEmpty((string)token))
            {
                throw new BridgeException(400, "\"" + name + "\" must be a non-empty string");
            }

            return (string)token;
        }

        public static bool RequireBool(JObject body, string name)
        {
            JToken token = body[name];
            if (token == null || token.Type != JTokenType.Boolean)
            {
                throw new BridgeException(400, "\"" + name + "\" must be true or false");
            }

            return (bool)token;
        }

        public static int RequireInt(JObject body, string name)
        {
            JToken token = body[name];
            if (token == null)
            {
                throw new BridgeException(400, "\"" + name + "\" is required");
            }

            return ToInt(token, name);
        }

        public static int OptionalInt(JObject body, string name, int fallback)
        {
            JToken token = body[name];
            return token == null ? fallback : ToInt(token, name);
        }

        private static int ToInt(JToken token, string name)
        {
            if (token.Type != JTokenType.Integer)
            {
                throw new BridgeException(400, "\"" + name + "\" must be an integer");
            }

            try
            {
                return (int)token;
            }
            catch (System.OverflowException)
            {
                throw new BridgeException(400, "\"" + name + "\" is out of range");
            }
        }
    }
}
