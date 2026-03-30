# Fast Song Search

A MelonLoader mod for Synth Riders that eliminates stutter during Twitch song requests.

### 1. Stutter Fix
When viewers use the `!srr` command to request songs, the game's built-in search runs synchronously on the main thread, causing noticeable stutters - especially with large song libraries. This mod replaces the slow search with a pre-built word index that executes instantly.

### 2. Queue Fix
The game has a bug where the last song in the queue never gets removed after playing. This mod properly removes played songs from the queue by working around the buggy `QueueRemove` function.

## The Solution

This mod replaces the slow search with a pre-built cached index that executes instantly, eliminating the stutter completely.

## Installation

1. Install [MelonLoader](https://melonwiki.xyz/) for Synth Riders
2. Download `FastSongSearch.dll` from [Releases](https://github.com/0mniDreamer/FastSongSearch/releases)
3. Place `FastSongSearch.dll` in your `SynthRiders/Mods/` folder
4. Launch the game

## How It Works

**Fast Search:**
- On first song request, the mod builds a word-based index of all songs so it will stutter on first search.(Important note: This is a one-time cost that happens only on the first search after launching the game) 
- Subsequent searches use the cached index for instant lookups
- The cache automatically rebuilds when changing scenes
- Falls back to the original search if any errors occur (Failsafe)

**Queue Fix:**
- Tracks the queue state when entering gameplay
- When returning to song selection after playing, removes the played song
- Uses `QueueClear()` for the last song to work around a game bug where `QueueRemove()` fails

## Compatibility

- Synth Riders 3.6.x (Steam/PC VR)
- MelonLoader 0.7.x
- .NET 6.0 / IL2CPP
## License

MIT License - see [LICENSE](LICENSE)
