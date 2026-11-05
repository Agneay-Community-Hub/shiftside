# SHIFTSIDE

> *That broken bridge might exist in the other world. Unfortunately, so might the monster standing on it.*

A puzzle-platformer about two overlapping realities, made for **[GitHub Game Off 2026](https://itch.io/jam/game-off-2026)**. The theme is **SHIFT**.

You can shift at any moment between **SOL**, a warm daytime world, and **UMBRA**, a cool night-time one. Both worlds share the same space, but each has its own ground, walls, spikes and residents. You always see the other world as faint ghosts, so you can plan ahead. Things that exist only there can't touch you until you join them.

## How to play

| Action | Keys |
|---|---|
| Move | `A` / `D` or arrow keys |
| Jump | `Space`, `W`, `Up` or `Z` (hold for a higher jump) |
| **Shift worlds** | **`Shift`** or `X` (gamepad B/X) |
| Restart level | `R` |
| Menu | `Esc` |
| Toggle music | `M` |

### Rules of the two worlds
- **Solid in both worlds:** pale stone. **Sol only:** amber. **Umbra only:** cyan. The world you're not in shows as faded ghosts.
- You **can't shift into something solid.** If a wall stands where you are in the other world, the shift is refused.
- You **can** shift into a monster. You'll regret it.
- Ghost platforms are pass-through. Jump up through one, then shift in mid-air to land on it.
- Monsters keep moving in their own world while you're away, so watch their ghosts.

## Levels
1. The Broken Bridge
2. Ghost Steps
3. Walls Between
4. Residents
5. Freefall
6. Night Shift
7. Crossfire
8. Unfortunately

Each level has optional **shards** to collect. Progress and shards are saved.

## Tech
- **Unity 6** (`6000.4.3f1`), Built-in Render Pipeline, legacy Input Manager.
- **No imported assets.** All sprites are generated pixel by pixel at startup (`Art.cs`). All sound effects and both music layers are synthesised in code (`Sfx.cs`). The two music loops share tempo and length, so shifting worlds crossfades the soundtrack in sync.
- Levels are ASCII maps in [`Assets/Scripts/Levels.cs`](Assets/Scripts/Levels.cs). The legend is at the top of that file.
- A custom tile-collision controller (`Player.cs`) gives exact per-world collision, coyote time and jump buffering.

### Project layout
```
Assets/
  Scripts/
    GameManager.cs   game flow, shifting, hazards, camera, FX, UI
    LevelRuntime.cs  parses a level, both worlds' grids, monsters, visuals
    Player.cs        platformer physics
    Levels.cs        level data
    Art.cs           procedural sprites
    Sfx.cs           procedural audio and music
    Palette.cs       colours of the two worlds
  Editor/
    BuildScript.cs   CLI build entry points
```

## Building from the command line
```bash
# WebGL (for itch.io)
"C:\Program Files\Unity\Hub\Editor\6000.4.3f1\Editor\Unity.exe" -batchmode -nographics -quit -projectPath . -buildTarget WebGL -executeMethod BuildScript.BuildWebGL -logFile Logs/build-webgl.log

# Windows
"C:\Program Files\Unity\Hub\Editor\6000.4.3f1\Editor\Unity.exe" -batchmode -nographics -quit -projectPath . -buildTarget StandaloneWindows64 -executeMethod BuildScript.BuildWindows -logFile Logs/build-win.log
```
WebGL output goes to `Builds/WebGL`. Zip the **contents** of that folder (`index.html` must be at the root of the zip) and upload it to itch.io as an HTML game, with "This file will be played in the browser" checked. The build uses Gzip with decompression fallback, which itch.io supports.

## Jam submission checklist
- [ ] Push this project to a **public GitHub repository** before **December 1, 2026, 13:37 PST**
- [ ] Upload the WebGL zip to itch.io and submit it to the Game Off 2026 jam page
- [ ] Link the repo from the itch.io page

## License
MIT. See [LICENSE](LICENSE).
