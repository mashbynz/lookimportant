# /// script
# requires-python = ">=3.11"
# dependencies = ["pillow"]
# ///
"""Download the CC0 Poly Haven textures used by the game and pack them for URP.

Each texture set becomes two files under game/Assets/Art/Textures/<name>/:
  <name>_albedo.png  RGB = diffuse, A = smoothness (1 - roughness)
  <name>_normal.png  OpenGL-convention normal map (what Unity expects)

Packing smoothness into the albedo alpha lets URP Lit read both from one
texture (Smoothness Source = Albedo Alpha), saving a sample per pixel.

Run: uv run tools/fetch_textures.py
"""
import io
import json
import pathlib
import urllib.request

from PIL import Image

ROOT = pathlib.Path(__file__).resolve().parent.parent
OUT = ROOT / "game" / "Assets" / "Art" / "Textures"
RES = "1k"
UA = {"User-Agent": "lookimportant-build"}

# name -> what it is used for in the scene
TEXTURES = {
    "kitchen_wood": "workbench butcher-block top",
    "grey_plaster": "room walls (tinted in the material)",
    "concrete_floor_worn_001": "floor",
}


def get(url: str) -> bytes:
    with urllib.request.urlopen(urllib.request.Request(url, headers=UA)) as r:
        return r.read()


def main() -> None:
    credits = []
    for name, use in TEXTURES.items():
        files = json.loads(get(f"https://api.polyhaven.com/files/{name}"))
        info = json.loads(get(f"https://api.polyhaven.com/info/{name}"))
        diff = Image.open(io.BytesIO(get(files["Diffuse"][RES]["jpg"]["url"]))).convert("RGB")
        rough = Image.open(io.BytesIO(get(files["Rough"][RES]["jpg"]["url"]))).convert("L")
        normal = Image.open(io.BytesIO(get(files["nor_gl"][RES]["jpg"]["url"]))).convert("RGB")

        smooth = rough.point(lambda v: 255 - v)
        albedo = diff.copy()
        albedo.putalpha(smooth)

        d = OUT / name
        d.mkdir(parents=True, exist_ok=True)
        albedo.save(d / f"{name}_albedo.png", optimize=True)
        normal.save(d / f"{name}_normal.png", optimize=True)
        size_m = info["dimensions"][0] / 1000.0
        credits.append({
            "name": name,
            "use": use,
            "real_world_size_m": round(size_m, 3),
            "authors": info.get("authors", {}),
            "source": f"https://polyhaven.com/a/{name}",
            "license": "CC0",
        })
        print(f"{name}: {albedo.size[0]}px, {size_m:.2f} m tile")

    (OUT / "textures.json").write_text(json.dumps(credits, indent=2) + "\n")


if __name__ == "__main__":
    main()
