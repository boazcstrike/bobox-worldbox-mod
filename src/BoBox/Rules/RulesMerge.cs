using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace BoBox.Rules
{
    /// <summary>
    /// Lays a rules file over the default rules as JSON, so a key missing from the file keeps its
    /// default. Objects merge key by key; arrays (the pathway list, the map scale) and values from the
    /// file replace the default whole; a null in the file keeps the default. The one exception is an
    /// entry of the capacity.jobs or capacity.buildings map: a null there deletes the default entry
    /// and adds a warning, because the map is keyed by id and has no other way to drop one.
    /// </summary>
    internal static class RulesMerge
    {
        private const string CapacityJobsPath = "capacity.jobs";
        private const string CapacityBuildingsPath = "capacity.buildings";

        public static JToken Overlay(JToken baseline, JToken overlay, ICollection<string> warnings)
        {
            return Merge(baseline, overlay, string.Empty, warnings);
        }

        private static JToken Merge(JToken baseline, JToken overlay, string path, ICollection<string> warnings)
        {
            if (overlay == null || overlay.Type == JTokenType.Null)
            {
                return baseline.DeepClone();
            }

            if (!(baseline is JObject baseObject) || !(overlay is JObject overlayObject))
            {
                return overlay.DeepClone();
            }

            bool isCapacityMap = path == CapacityJobsPath || path == CapacityBuildingsPath;
            var result = new JObject();
            foreach (JProperty property in baseObject.Properties())
            {
                JToken fromFile = overlayObject[property.Name];
                if (isCapacityMap && fromFile != null && fromFile.Type == JTokenType.Null)
                {
                    warnings.Add($"removed default capacity '{property.Name}' from '{path}'");
                    continue;
                }

                result.Add(property.Name, Merge(property.Value, fromFile, Join(path, property.Name), warnings));
            }

            foreach (JProperty property in overlayObject.Properties())
            {
                bool isNullEntry = isCapacityMap && property.Value.Type == JTokenType.Null;
                if (baseObject[property.Name] == null && !isNullEntry)
                {
                    result.Add(property.Name, property.Value.DeepClone());
                }
            }

            return result;
        }

        private static string Join(string path, string name)
        {
            return path.Length == 0 ? name : path + "." + name;
        }
    }
}
