# Changelog

Each commit adds an entry and bumps the version by 0.0.0001. Major and minor bumps are set by Matt.

## 0.0.0004 - 2026-10-16
- First alpha build: Linux, Windows and WebGL players build from `tools/build.sh` (output in `Builds/`, not committed).
- Unity Personal activated on the build machine with `tools/activate-unity.sh`. A copied `.ulf` is rejected for machine bindings.
- Fixed hinge axes after checking how Unity imports Blender pivots (local Z is flipped): gauge needle, toggles, knife switch, selector, button and swinging tools now move as designed. Verified with headless captures of the rest, live, hover and lamp-off states.
- Performance pass on the target-class GPU (Intel HD 6000, 1440x900): 34.4 ms to 13.8 ms per frame. SSAO off, FXAA instead of MSAA x4, hard shadows on the desk lamp only (ceiling light is an unshadowed fill), quarter-res bloom, Forward renderer. `AdaptiveResolution` lowers render scale (down to 0.7) only when frames keep missing the refresh rate, since there is no settings menu.
- Pilot lamps: dark glass when off, emissive and bloom when lit (point lights removed). Softer hover glow.
- `tools/perf.sh`: GPU benchmark through headless Weston, with `-perf-*` switches for A/B testing render features. `tools/capture.sh` focuses the window itself under Xvfb.
- Tooling notes: `ffmpeg` is needed for WebGL audio import, and `xdotool`, `weston` and `xwayland` are used by the test scripts (all in `tools/install-tools.sh`).

## 0.0.0003 - 2026-10-15
- Unity project in `game/` (URP from the Universal 3D template, Input System), tuned for a small room: 4 m shadow distance, one cascade, shadowed spot lights, MSAA, half-res SSAO on PC, vsync.
- Runtime scripts: point-and-click interaction with hover glow, seated head-look camera, spring-driven toggles, push button, detented selector, knife switch, gauge, pilot lamps, desk lamp, and swinging/rattling fidget props. `WorkbenchCircuit` is placeholder wiring between them.
- Editor tooling: URP materials generated from `materials.json`, import rules, `SceneBuilder` that turns the Blender export into `Assets/Scenes/Workbench.unity`, `BuildScript` for Linux/Windows/WebGL.
- `tools/make_sounds.py`: synthesised sound effects and loops (no recorded or purchased audio).
- `tools/build.sh`, `tools/capture.sh`, `tools/serve_webgl.py`, `tools/install-tools.sh`.

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
