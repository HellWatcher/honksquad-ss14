#!/usr/bin/env python3
"""Flatten the AirTanks layered-artwork pose-folder layout into honksquad
layered tank RSIs (issue #692 / PR #761).

The artist delivers each tank shape as a folder of per-pose subfolders, where
every subfolder holds the separated layer components (silhouette/multi/add/
details). honksquad RSIs are flat: one PNG per `{layer}{pose-suffix}` state.
This script does the rename+flatten and regenerates meta.json so the import is
reproducible from the original delivery.

Usage:
    python3 Tools/tank_sprite_flatten.py <extracted_zip_root> <textures_root>

<textures_root> is the Tanks dir, e.g.
Resources/Textures/@RussStation/Objects/Tanks
"""
import json
import shutil
import sys
from pathlib import Path

SHAPE_RSI = {"Tank": "standard.rsi", "Emergency": "emergency.rsi", "Double": "double.rsi"}

# Upstream tank RSIs ship no separate human/default equipped-SUITSTORAGE: it is
# byte-identical to the shape's carried pose (BACKPACK for generic/oxygen-shaped
# tanks, BELT for the emergency/double). Mirror that — alias the carried-pose
# layer set as the generic equipped-SUITSTORAGE so unlisted species (human, ...)
# still render. Verified identical via md5 against generic/oxygen/emergency/
# emergency_double .rsi.
GENERIC_FALLBACK_SRC = {
    "Tank": "-equipped-BACKPACK",
    "Emergency": "-equipped-BELT",
    "Double": "-equipped-BELT",
}

LAYER_RENAME = {
    "silhouette-base": "base",
    "silhouette-band": "band",
    "details": "detail",
    "multi-base": "multi-base",
    "multi-band": "multi-band",
    "add-base": "add-base",
    "add-band": "add-band",
}


def pose_suffix(pose: str) -> str:
    p = pose.lower()
    if p == "icon":
        return ""
    if p == "storage":
        return "-storage"
    if p in ("inhand-left", "inhand-right"):
        return f"-{p}"
    if pose == "equipped-SUITSTORAGE_BELT":
        return "-equipped-BELT"
    if pose == "equipped-SUITSTORAGE_BACKPACK":
        return "-equipped-BACKPACK"
    if pose.startswith("equipped-SUITSTORAGE-"):
        return f"-{pose}"
    raise SystemExit(f"unmapped pose folder: {pose}")


def is_four_dir(state_name: str) -> bool:
    return (
        state_name.endswith("-inhand-left")
        or state_name.endswith("-inhand-right")
        or "-equipped-" in state_name
    )


def write_meta(rsi_dir: Path) -> None:
    states = []
    for png in sorted(rsi_dir.glob("*.png")):
        s = {"name": png.stem}
        if is_four_dir(png.stem):
            s["directions"] = 4
        states.append(s)
    meta = {
        "version": 1,
        "license": "CC-BY-SA-3.0",
        "copyright": "Drawn by Katyes for honksquad-ss14.",
        "size": {"x": 32, "y": 32},
        "states": states,
    }
    (rsi_dir / "meta.json").write_text(json.dumps(meta, indent=2) + "\n")


def main() -> None:
    if len(sys.argv) != 3:
        raise SystemExit(__doc__)
    src_root = Path(sys.argv[1])
    tex_root = Path(sys.argv[2])

    for shape, rsi in SHAPE_RSI.items():
        shape_dir = src_root / shape
        if not shape_dir.is_dir():
            raise SystemExit(f"missing shape folder: {shape_dir}")
        out = tex_root / rsi
        out.mkdir(parents=True, exist_ok=True)
        for pose_dir in sorted(shape_dir.iterdir()):
            if not pose_dir.is_dir():
                continue
            suffix = pose_suffix(pose_dir.name)
            for png in sorted(pose_dir.glob("*.png")):
                layer = LAYER_RENAME.get(png.stem)
                if layer is None:
                    raise SystemExit(f"unmapped layer file: {png}")
                shutil.copy2(png, out / f"{layer}{suffix}.png")

        src_suffix = GENERIC_FALLBACK_SRC[shape]
        carried = sorted(out.glob(f"*{src_suffix}.png"))
        if not carried:
            raise SystemExit(f"{rsi}: no {src_suffix} pose to alias as generic")
        for png in carried:
            dst = png.name.replace(src_suffix, "-equipped-SUITSTORAGE")
            shutil.copy2(png, out / dst)

        write_meta(out)
        print(f"{shape} -> {rsi}: {len(list(out.glob('*.png')))} png")

    print("flatten complete")


if __name__ == "__main__":
    main()
