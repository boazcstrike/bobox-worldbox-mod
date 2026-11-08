using Newtonsoft.Json.Linq;

namespace BoBox.Bridge
{
    internal sealed class ApiRequest
    {
        public string Method { get; }
        public string Path { get; }
        public string RawBody { get; }

        public ApiRequest(string method, string path, string rawBody)
        {
            Method = method;
            Path = path;
            RawBody = rawBody;
        }

        public JObject ParseJsonBody()
        {
            if (string.IsNullOrWhiteSpace(RawBody))
            {
                throw new BridgeException(400, "JSON body required");
            }

            try
            {
                return JObject.Parse(RawBody);
            }
            catch (Newtonsoft.Json.JsonException ex)
            {
                throw new BridgeException(400, "invalid JSON: " + ex.Message);
            }
        }
    }
}
