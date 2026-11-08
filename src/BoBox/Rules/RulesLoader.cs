using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BoBox.Rules
{
    /// <summary>
    /// Reads the rules file. A missing file gives the defaults; invalid JSON gives a failed result with
    /// the parse error (the caller keeps its last good rules); unknown keys are warnings; out-of-range
    /// numbers are clamped or rejected per field. Never throws: any other exception becomes a failed
    /// result too. Pure apart from file IO.
    /// </summary>
    public static class RulesLoader
    {
        /// <summary>A rules file over this size is rejected unread; the real one is a few kilobytes.</summary>
        public const int MaxFileBytes = 1024 * 1024;

        /// <summary>Warnings kept per load; the rest are counted in one closing line.</summary>
        public const int MaxWarnings = 50;

        private static readonly JsonLoadSettings LoadSettings = new JsonLoadSettings
        {
            CommentHandling = CommentHandling.Ignore,
            DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error,
            LineInfoHandling = LineInfoHandling.Load,
        };

        public static RulesLoadResult Load(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return NotFound(path);
            }

            string json;
            try
            {
                long length = new FileInfo(path).Length;
                if (length > MaxFileBytes)
                {
                    return RulesLoadResult.Failed(
                        path, new[] { $"rules file is {length} bytes, over the {MaxFileBytes} byte limit" });
                }

                json = File.ReadAllText(path);
            }
            catch (Exception ex) when (ex is FileNotFoundException || ex is DirectoryNotFoundException)
            {
                return NotFound(path);
            }
            catch (Exception ex)
            {
                return RulesLoadResult.Failed(path, new[] { $"cannot read rules file: {ex.Message}" });
            }

            return Parse(json, path);
        }

        private static RulesLoadResult NotFound(string path)
        {
            return RulesLoadResult.Defaults(new[] { $"rules file not found: {path}, using defaults" });
        }

        /// <summary>Parses rules JSON already in memory. <paramref name="source"/> names it in the result.</summary>
        public static RulesLoadResult Parse(string json, string source)
        {
            try
            {
                return Build(ParseObject(json), source);
            }
            catch (JsonReaderException ex)
            {
                string where = $"line {ex.LineNumber}, column {ex.LinePosition}";
                return RulesLoadResult.Failed(source, new[] { $"invalid JSON at {where}: {ex.Message}" });
            }
            catch (InvalidDataException ex)
            {
                return RulesLoadResult.Failed(source, new[] { ex.Message });
            }
            catch (Exception ex)
            {
                return RulesLoadResult.Failed(source, new[] { $"invalid rules: {ex.GetType().Name}: {ex.Message}" });
            }
        }

        private static JObject ParseObject(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new InvalidDataException("rules JSON is empty");
            }

            if (!(JToken.Parse(json, LoadSettings) is JObject root))
            {
                throw new InvalidDataException("rules JSON must be an object at the top level");
            }

            // The reader accepts "key": , as an undefined value; that is a typo here, not a rule.
            JValue missing = root.DescendantsAndSelf().OfType<JValue>().FirstOrDefault(v => v.Type == JTokenType.Undefined);
            if (missing != null)
            {
                var position = (IJsonLineInfo)missing;
                throw new InvalidDataException(
                    $"invalid JSON at line {position.LineNumber}, column {position.LinePosition}: value missing at '{missing.Path}'");
            }

            return root;
        }

        private static RulesLoadResult Build(JObject file, string source)
        {
            var mergeWarnings = new List<string>();
            JToken merged = RulesMerge.Overlay(RulesJson.ToJToken(ColonyRules.Default), file, mergeWarnings);
            var sanitizer = new RulesSanitizer();
            JToken clean = sanitizer.Run(merged);

            ColonyRules parsed;
            try
            {
                parsed = clean.ToObject<ColonyRules>(JsonSerializer.Create(RulesJson.Settings()));
            }
            catch (JsonException ex)
            {
                return RulesLoadResult.Failed(source, new[] { $"invalid rules: {ex.Message}" });
            }

            RulesPathwayCheck check = RulesPathwayCheck.Run(parsed, clean);
            if (check.Errors.Count > 0)
            {
                return RulesLoadResult.Failed(source, check.Errors);
            }

            IEnumerable<string> warnings = mergeWarnings
                .Concat(CapWarnings(sanitizer.Warnings))
                .Concat(VersionWarnings(parsed))
                .Concat(ConsistencyWarnings(parsed))
                .Concat(RulesThresholdCheck.ZeroMinimumWarnings(clean))
                .Concat(check.Warnings);
            return new RulesLoadResult(check.Rules, source, null, warnings);
        }

        // A 1 MB file can name tens of thousands of unknown keys; the log and the API response stay short. Only the
        // sanitizer's warnings are capped: the threshold and pathway-check warnings come after them and are never dropped.
        private static IEnumerable<string> CapWarnings(IEnumerable<string> warnings)
        {
            List<string> all = warnings.ToList();
            int total = all.Count;
            if (total <= MaxWarnings)
            {
                return all;
            }

            all.RemoveRange(MaxWarnings, total - MaxWarnings);
            all.Add($"{total - MaxWarnings} more warnings suppressed");
            return all;
        }

        // A rules file kept across mod updates never gains the keys a newer version added, and its
        // arrays replace the shipped ones whole, so they miss any entry added since; a file from a newer
        // mod may carry keys this one does not read.
        private static string[] VersionWarnings(ColonyRules parsed)
        {
            if (parsed.Version > ColonyRules.CurrentVersion)
            {
                return new[] { $"rules file version {parsed.Version} is newer than this mod ({ColonyRules.CurrentVersion}); unknown keys are ignored" };
            }

            return parsed.Version < ColonyRules.CurrentVersion
                ? new[] { $"rules file version {parsed.Version} is older than {ColonyRules.CurrentVersion}; keys added since then use the defaults, but the pathways and map_scale arrays from your file are used as is, so compare them with the shipped rules/bobox-rules.json" }
                : Array.Empty<string>();
        }

        // The free-zone scan covers a square of (2 * radius + 1) zones a side; asking for more free zones
        // than the square holds makes open_frontier unreachable.
        private static string[] ConsistencyWarnings(ColonyRules parsed)
        {
            int radius = parsed.Signals.FreeZoneScanRadius;
            int scanned = (2 * radius + 1) * (2 * radius + 1);
            int wanted = parsed.Scenarios.OpenFrontier.MinFreeZonesInRange;
            return wanted > scanned
                ? new[] { $"scenarios.open_frontier.min_free_zones_in_range = {wanted} is more than the {scanned} zones signals.free_zone_scan_radius = {radius} scans, so open_frontier can never match" }
                : Array.Empty<string>();
        }
    }
}
