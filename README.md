# Fast Song Search

A MelonLoader mod for Synth Riders that eliminates stutter during Twitch song requests.

## The Problem

When viewers use the `!sr` command to request songs, the game's built-in search function runs synchronously on the main thread, causing noticeable frame drops and stuttering during gameplay.

## The Solution

This mod replaces the slow search with a pre-built cached index that executes instantly, eliminating the stutter completely.

## Installation

1. Install [MelonLoader](https://melonwiki.xyz/) for Synth Riders
2. Download `FastSongSearch.dll` from [Releases](https://github.com/0mniDreamer/FastSongSearch/releases)
3. Place `FastSongSearch.dll` in your `SynthRiders/Mods/` folder
4. Launch the game

## How It Works

- On first song request, the mod builds a word-based index of all songs so it will stutter on first search.(Important note: This is a one-time cost that happens only on the first search after launching the game) 
- Subsequent searches use the cached index for instant lookups
- The cache automatically rebuilds when changing scenes
- Falls back to the original search if any errors occur (Failsafe)

## Compatibility

- Synth Riders (Steam/PC VR)
- MelonLoader 0.7.2
- .NET 6.0

## License

MIT License - see [LICENSE](LICENSE)
