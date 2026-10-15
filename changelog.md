# Changelog

Each commit adds an entry and bumps the version by 0.0.0001. Major and minor bumps are set by Matt.

## 0.0.0002 - 2026-10-15
- Toolchain: Blender 5.2.2 LTS (user-local in `~/.local/opt`), Unity 6000.3.26f1 LTS with WebGL and Windows Mono build support (`~/Unity/Hub/Editor`).
- `blender/build_scene.py`: procedural models and layout for the workbench, pegboard (real through-holes), room, and first props: toggle bank with indicator lamps, big red button, selector dial, knife switch, gauge, desk lamp, lock box with padlock, hanging tools, mug. Exports `game/Assets/Art/Models/Workbench.fbx` and saves `blender/workbench.blend`.
- `blender/render_preview.py`: Cycles preview from the player camera.
- `game/Assets/Art/materials.json`: shared material palette for Blender previews and Unity URP materials.
- `tools/fetch_textures.py`: CC0 Poly Haven textures (bench wood, wall plaster, floor concrete), smoothness packed into albedo alpha.
- `tools/make_dial_textures.py`: gauge and selector dial faces (numbers and ticks only).

## 0.0.0001 - 2026-10-15
- Added `intent.md`: draft intent for the workbench puzzle-box game.
- Engine decided: Unity Personal. Engine install size is not a constraint; performance must be optimised for the engine in use.
- Added `changelog.md`.
