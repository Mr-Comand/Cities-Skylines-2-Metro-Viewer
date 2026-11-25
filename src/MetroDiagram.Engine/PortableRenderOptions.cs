using System;
using System.Collections.Generic;
using System.Linq;

namespace MetroDiagram.Engine
{
    public enum PortableLayoutMode
    {
        Geographic,
        SchematicAnneal
    }

    /// <summary>
    /// Stable transport-mode tokens used by the exporter (line.Mode), the preview
    /// options (VisibleModes) and the in-game UI. Keep these lowercase and
    /// in sync with CS2 Metro.mjs and the state JSON keys.
    /// </summary>
    public static class PortableTransportModes
    {
        public const string Metro = "metro";
        public const string Train = "train";
        public const string Bus = "bus";
        public const string Tram = "tram";

        public static readonly string[] All = { Metro, Train, Bus, Tram };

        public static bool IsValid(string? token)
        {
            foreach (string mode in All)
            {
                if (string.Equals(mode, token, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Returns the canonical lowercase token, or null when unknown.</summary>
        public static string? Normalize(string? token)
        {
            foreach (string mode in All)
            {
                if (string.Equals(mode, token, StringComparison.OrdinalIgnoreCase))
                {
                    return mode;
                }
            }

            return null;
        }

        /// <summary>
        /// Parses a comma-separated token list into a canonical, deduplicated set.
        /// Unknown tokens are dropped; an empty result falls back to metro so a
        /// bad settings string can never blank out the whole map.
        /// </summary>
        public static HashSet<string> Parse(string? tokens)
        {
            HashSet<string> modes = new HashSet<string>(StringComparer.Ordinal);
            if (!string.IsNullOrWhiteSpace(tokens))
            {
                foreach (string raw in tokens.Split(','))
                {
                    string? mode = Normalize(raw);
                    if (mode != null)
                    {
                        modes.Add(mode);
                    }
                }
            }

            return modes.Count == 0
                ? new HashSet<string>(StringComparer.Ordinal) { Metro }
                : modes;
        }

        /// <summary>Serializes a mode set into a canonical, ordered token list.</summary>
        public static string ToTokenList(IEnumerable<string> modes)
        {
            HashSet<string> set = new HashSet<string>(modes ?? Array.Empty<string>(), StringComparer.Ordinal);
            List<string> ordered = All.Where(set.Contains).ToList();
            return string.Join(",", ordered);
        }
    }

    public sealed class PortableRenderOptions
    {
        public PortableLayoutMode LayoutMode { get; set; } = PortableLayoutMode.SchematicAnneal;

        public int Width { get; set; } = 1800;

        public int Height { get; set; } = 1100;

        public int Padding { get; set; } = 64;

        public int LegendWidth { get; set; } = 250;

        public double RouteWidth { get; set; } = 9;

        public double StationRadius { get; set; } = 5;

        public double LabelFontSize { get; set; } = 12;

        public double GridSize { get; set; } = 30;

        public bool ShowGenericStationNames { get; set; }

        public bool HideCrowdedLabels { get; set; } = true;

        public bool MergeServiceFamilies { get; set; } = true;

        /// <summary>
        /// Development-only geographic passenger-rail overlay. It stays false in
        /// every in-game profile until the owner validates it against CS2 data.
        /// </summary>
        public bool ShowIntercityRail { get; set; }

        /// <summary>
        /// Transport modes that should be rendered (subset of
        /// <see cref="PortableTransportModes.All"/>). Null or empty means
        /// "metro only", which keeps every existing render byte-identical.
        /// The in-game preview UI populates this from the mode toggles.
        /// </summary>
        public IReadOnlyCollection<string> VisibleModes { get; set; }

        /// <summary>
        /// True when a line's mode is visible under the current options. Metro
        /// is always treated as visible so legacy callers (no VisibleModes set)
        /// keep rendering metro-only maps.
        /// </summary>
        public bool IsModeVisible(string mode)
        {
            if (string.Equals(mode, PortableTransportModes.Metro, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (VisibleModes == null || VisibleModes.Count == 0)
            {
                return false;
            }

            foreach (string visible in VisibleModes)
            {
                if (string.Equals(visible, mode, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        public int AnnealAttemptLimit { get; set; } = 24000;

        public bool AdaptCanvasHeightToNetwork { get; set; } = true;

        /// <summary>
        /// Draws route corners as geometric curves instead of relying on
        /// <c>stroke-linejoin: round</c>, whose effective radius is only half the
        /// stroke width. Matches the desktop renderer's default.
        /// </summary>
        public bool RoundRouteCorners { get; set; } = true;

        /// <summary>
        /// Places every run of the annealed layout exactly on one of the eight
        /// compass headings. Moves station positions, so it stays off until the
        /// in-game result has been looked at.
        /// </summary>
        public bool SnapToOctilinearGrid { get; set; }

        /// <summary>
        /// Appended to the city name to form the sheet title (defaults keep the
        /// historical "{city} Metro Diagram"). Null or empty falls back to the
        /// default so existing outputs stay byte-identical.
        /// </summary>
        public string? TitleSuffix { get; set; }

        /// <summary>
        /// Legend header text; null or empty falls back to "Lines".
        /// </summary>
        public string? LegendHeader { get; set; }
    }

    public sealed class PortableRenderResult
    {
        internal PortableRenderResult(string svg, string snapshotRevision, int stationCount, int lineCount, long elapsedMilliseconds)
        {
            Svg = svg;
            SnapshotRevision = snapshotRevision;
            StationCount = stationCount;
            LineCount = lineCount;
            ElapsedMilliseconds = elapsedMilliseconds;
        }

        public string Svg { get; }

        public string SnapshotRevision { get; }

        public int StationCount { get; }

        public int LineCount { get; }

        public long ElapsedMilliseconds { get; }
    }

    public static class PortableRenderProfiles
    {
        public static PortableRenderOptions CreateInGameSchematic(bool showGenericStationNames, bool hideCrowdedLabels)
        {
            return new PortableRenderOptions
            {
                LayoutMode = PortableLayoutMode.SchematicAnneal,
                Width = 1800,
                Height = 1100,
                Padding = 64,
                LegendWidth = 250,
                RouteWidth = 9,
                StationRadius = 5,
                // 11 instead of 12: labels are proportionally ~2.5x larger on the
                // in-game panel than on the desktop poster, and since beta.6 the
                // panel zoom is vector-crisp, so a smaller base size trades cheap
                // readability for a lot of breathing room in dense centers.
                LabelFontSize = 11,
                GridSize = 30,
                ShowGenericStationNames = showGenericStationNames,
                HideCrowdedLabels = hideCrowdedLabels,
                MergeServiceFamilies = true,
                AnnealAttemptLimit = 24000,
                AdaptCanvasHeightToNetwork = false,
                SnapToOctilinearGrid = true
            };
        }

        public static PortableRenderOptions CreateInGameGeographic(bool showGenericStationNames, bool hideCrowdedLabels)
        {
            PortableRenderOptions options = CreateInGameSchematic(showGenericStationNames, hideCrowdedLabels);
            options.LayoutMode = PortableLayoutMode.Geographic;
            options.SnapToOctilinearGrid = false;
            return options;
        }
    }
}
