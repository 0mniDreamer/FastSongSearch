using HarmonyLib;
using MelonLoader;
using System;
using System.Collections.Generic;
using System.Linq;
using Il2CppSynth.Twitch;
using Il2CppMiKu.NET.Charting;

[assembly: MelonInfo(typeof(FastSongSearch.FastSongSearchMod), "Fast Song Search", "1.0.0", "OmniDreamer", "https://github.com/0mniDreamer/FastSongSearch")]
[assembly: MelonGame("Kluge Interactive", "SynthRiders")]

namespace FastSongSearch
{
    /// <summary>
    /// Fast Song Search - Eliminates stutter during Twitch song requests
    /// by replacing the slow synchronous search with a pre-built cached index.
    /// </summary>
    public class FastSongSearchMod : MelonMod
    {
        public static FastSongSearchMod Instance { get; private set; }

        public override void OnInitializeMelon()
        {
            Instance = this;
            LoggerInstance.Msg("Fast Song Search loaded!");
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            SongSearchCache.Invalidate();
        }
    }

    /// <summary>
    /// Cached song search index for fast lookups.
    /// </summary>
    public static class SongSearchCache
    {
        private static Dictionary<string, List<Chart>> _wordIndex = new();
        private static List<Chart> _allSongs = new();
        private static bool _isBuilt = false;
        private static readonly object _lock = new();

        /// <summary>
        /// Invalidate the cache when song list changes.
        /// </summary>
        public static void Invalidate()
        {
            lock (_lock)
            {
                _wordIndex.Clear();
                _allSongs.Clear();
                _isBuilt = false;
            }
        }

        /// <summary>
        /// Build the search index from the song list.
        /// </summary>
        private static void Build(Il2CppSystem.Collections.Generic.List<Chart> songs)
        {
            if (_isBuilt) return;

            lock (_lock)
            {
                if (_isBuilt) return;

                _wordIndex.Clear();
                _allSongs.Clear();

                for (int i = 0; i < songs.Count; i++)
                {
                    var chart = songs[i];
                    if (chart == null) continue;

                    _allSongs.Add(chart);
                    IndexChart(chart);
                }

                _isBuilt = true;
                MelonLogger.Msg($"[FastSongSearch] Indexed {_allSongs.Count} songs");
            }
        }

        /// <summary>
        /// Index a single chart by its searchable fields.
        /// </summary>
        private static void IndexChart(Chart chart)
        {
            var fields = new[] 
            { 
                chart.Name, 
                chart.Author, 
                chart.Beatmapper 
            };

            foreach (var field in fields)
            {
                if (string.IsNullOrEmpty(field)) continue;

                var words = field.ToLowerInvariant()
                    .Split(new[] { ' ', '-', '_', '(', ')', '[', ']', '.' }, StringSplitOptions.RemoveEmptyEntries);

                foreach (var word in words)
                {
                    if (word.Length < 2) continue;

                    if (!_wordIndex.TryGetValue(word, out var list))
                    {
                        list = new List<Chart>();
                        _wordIndex[word] = list;
                    }

                    if (!list.Contains(chart))
                        list.Add(chart);
                }
            }
        }

        /// <summary>
        /// Search for a song by query string.
        /// </summary>
        public static Chart Search(string query, Il2CppSystem.Collections.Generic.List<Chart> songs)
        {
            if (string.IsNullOrWhiteSpace(query))
                return null;

            if (!_isBuilt)
                Build(songs);

            query = query.ToLowerInvariant().Trim();
            var queryWords = query.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            if (queryWords.Length == 0)
                return null;

            var scores = new Dictionary<Chart, int>();

            // Score by indexed word matches
            foreach (var word in queryWords)
            {
                // Exact word match
                if (_wordIndex.TryGetValue(word, out var exactMatches))
                {
                    foreach (var chart in exactMatches)
                        AddScore(scores, chart, 10);
                }

                // Partial word matches
                foreach (var kvp in _wordIndex)
                {
                    if (kvp.Key.Contains(word) || word.Contains(kvp.Key))
                    {
                        foreach (var chart in kvp.Value)
                            AddScore(scores, chart, 3);
                    }
                }
            }

            // Fallback: scan all songs if no indexed matches
            if (scores.Count == 0)
            {
                foreach (var chart in _allSongs)
                {
                    var name = chart.Name?.ToLowerInvariant() ?? "";
                    if (name.Contains(query))
                        AddScore(scores, chart, 15);
                }
            }

            if (scores.Count == 0)
                return null;

            // Bonus for exact or prefix matches
            foreach (var chart in scores.Keys.ToList())
            {
                var name = chart.Name?.ToLowerInvariant() ?? "";
                
                if (name == query)
                    return chart; // Perfect match - return immediately
                
                if (name.StartsWith(query))
                    scores[chart] += 20;
            }

            return scores.OrderByDescending(x => x.Value).First().Key;
        }

        private static void AddScore(Dictionary<Chart, int> scores, Chart chart, int points)
        {
            if (!scores.ContainsKey(chart))
                scores[chart] = 0;
            scores[chart] += points;
        }
    }

    /// <summary>
    /// Harmony patches to replace the slow search with the fast version.
    /// </summary>
    [HarmonyPatch]
    public static class SearchPatch
    {
        [HarmonyPatch(typeof(TwitchBot), nameof(TwitchBot.SearchSongsByName))]
        [HarmonyPrefix]
        public static bool FastSearchPrefix(
            string query,
            Il2CppSystem.Collections.Generic.List<Chart> availableSongs,
            ref Chart __result)
        {
            try
            {
                __result = SongSearchCache.Search(query, availableSongs);
                return false;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[FastSongSearch] Error: {ex.Message}");
                return true; // Fall back to original on error
            }
        }
    }
}
