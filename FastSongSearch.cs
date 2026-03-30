using HarmonyLib;
using MelonLoader;
using System;
using System.Collections.Generic;
using System.Linq;
using Il2CppSynth.Twitch;
using Il2CppMiKu.NET.Charting;

[assembly: MelonInfo(typeof(FastSongSearch.FastSongSearchMod), "Fast Song Search", "1.0.1", "OmniDreamer")]
[assembly: MelonGame("Kluge Interactive", "SynthRiders")]

namespace FastSongSearch
{
    /// <summary>
    /// MelonLoader mod that fixes Twitch song request issues in Synth Riders:
    /// 1. Eliminates stutter when viewers use !srr (cached search index)
    /// 2. Fixes songs not being removed from queue after playing
    /// </summary>
    public class FastSongSearchMod : MelonMod
    {
        private static string _lastScene = "";
        private static int _queueCountBeforeGame = 0;
        private static string _firstQueuedSongName = "";

        public override void OnInitializeMelon()
        {
            LoggerInstance.Msg("Fast Song Search loaded - Twitch requests optimized!");
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            // Snapshot queue when entering gameplay
            if (IsGameScene(sceneName) && !IsGameScene(_lastScene))
            {
                try
                {
                    var queue = TwitchBot.GetSongsInQueue();
                    _queueCountBeforeGame = queue?.Count ?? 0;
                    _firstQueuedSongName = (_queueCountBeforeGame > 0) ? queue[0]?.Name ?? "" : "";
                }
                catch { }
            }

            // Remove played song when returning to menu
            if (sceneName == "SongSelection" && WasInGame(_lastScene))
            {
                if (_queueCountBeforeGame > 0 && !string.IsNullOrEmpty(_firstQueuedSongName))
                {
                    QueueManager.RemoveFirstSongFromQueue(_queueCountBeforeGame);
                }
                _queueCountBeforeGame = 0;
                _firstQueuedSongName = "";
            }

            _lastScene = sceneName;
            SongSearchCache.Invalidate();
        }

        private static bool IsGameScene(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName)) return false;
            var lower = sceneName.ToLowerInvariant();
            return lower.Contains("stage") && !lower.Contains("gameend") && !lower.Contains("gamestart");
        }

        private static bool WasInGame(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName)) return false;
            return sceneName.ToLowerInvariant().Contains("gameend");
        }
    }

    /// <summary>
    /// Pre-indexed song search for instant lookups.
    /// </summary>
    internal static class SongSearchCache
    {
        private static Dictionary<string, List<Chart>> _wordIndex = new();
        private static List<Chart> _allSongs = new();
        private static bool _isBuilt = false;
        private static readonly object _lock = new();

        public static void Invalidate()
        {
            lock (_lock)
            {
                _wordIndex.Clear();
                _allSongs.Clear();
                _isBuilt = false;
            }
        }

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
            }
        }

        private static void IndexChart(Chart chart)
        {
            var fields = new[] { chart.Name, chart.Author, chart.Beatmapper };

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

            // Score by word matches
            foreach (var word in queryWords)
            {
                if (_wordIndex.TryGetValue(word, out var exactMatches))
                {
                    foreach (var chart in exactMatches)
                        AddScore(scores, chart, 10);
                }

                foreach (var kvp in _wordIndex)
                {
                    if (kvp.Key.Contains(word) || word.Contains(kvp.Key))
                    {
                        foreach (var chart in kvp.Value)
                            AddScore(scores, chart, 3);
                    }
                }
            }

            // Fallback to substring search
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

            // Boost exact and prefix matches
            foreach (var chart in scores.Keys.ToList())
            {
                var name = chart.Name?.ToLowerInvariant() ?? "";

                if (name == query)
                    return chart;

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
    /// Handles removing songs from the Twitch request queue.
    /// Works around a game bug where the last song doesn't get removed.
    /// </summary>
    internal static class QueueManager
    {
        public static void RemoveFirstSongFromQueue(int queueCountBeforeGame)
        {
            try
            {
                var queue = TwitchBot.GetSongsInQueue();
                if (queue == null || queue.Count == 0) return;

                // Use the count from BEFORE we started playing
                // If there was only 1 song, use QueueClear (workaround for game bug)
                // If there were multiple songs, use QueueRemove
                if (queueCountBeforeGame == 1)
                {
                    TwitchBot.QueueClear();
                }
                else
                {
                    var songToRemove = queue[0];
                    if (songToRemove != null)
                    {
                        TwitchBot.QueueRemove(songToRemove);
                    }
                }
            }
            catch { }
        }
    }

    [HarmonyPatch]
    internal static class Patches
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
            catch
            {
                return true; // Fall back to original on error
            }
        }
    }
}