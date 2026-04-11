#!/usr/bin/env python3
"""Bake per-gas canister sprites from the SS13 greyscale canister DMI.

Reads the russtation greyscale canister layers from a DMI file, composites
each gas using its SS13 greyscale template + color pair, and writes the
result as flat PNGs into an SS14 RSI directory.

Usage:
  python3 Tools/canister_sprite_baker.py                 # bake every gas in the config
  python3 Tools/canister_sprite_baker.py --gas plasma    # bake a single gas
  python3 Tools/canister_sprite_baker.py --list          # show the gas table

Data files live next to this script in Tools/canister_sprite_baker/:
  - canisters.dmi   source greyscale layers from russtation/tgstation
  - gases.json      per-gas template and color pair table (machine-readable)
  - gases.md        human-scannable version of the same table with entity names
  - out/            default bake output (PNGs land here for review)

By default the baker writes to Tools/canister_sprite_baker/out/ so you can eyeball
the results before copying them into the live RSI at
Resources/Textures/@RussStation/Structures/Storage/canister.rsi/. Pass --out to
override the destination directly.

Adding a new gas:
  1. Edit Tools/canister_sprite_baker/gases.json to add an entry.
  2. Run this script with --write-meta to regenerate the staging meta.json.
  3. Copy the new PNGs + meta.json into the live RSI.

The four greyscale templates mirror SS13 (`canister_default`, `canister_stripe`,
`canister_double_stripe`, `canister_hazard`). Each template composites the
greyscale layers (`base`, `add_shader`, `multi_shader`, `outline`, `lights`,
optional `stripe`/`double_stripe`/`hazard_stripes`) with BYOND-style blend
modes. The broken (`-1`) variant overlays the `broken` layer on the live
canister and rotates 90 degrees clockwise to match SS14's tipped-over look.
"""

from __future__ import annotations

import argparse
import json
import re
import sys
from pathlib import Path

try:
    from PIL import Image
except ImportError:
    sys.exit("Pillow is required: pip install Pillow numpy")

try:
    import numpy as np
except ImportError:
    sys.exit("numpy is required: pip install Pillow numpy")


TILE = 32

REPO_ROOT = Path(__file__).resolve().parent.parent
DATA_DIR = Path(__file__).resolve().parent / "canister_sprite_baker"
DEFAULT_CONFIG = DATA_DIR / "gases.json"
DEFAULT_DMI = DATA_DIR / "canisters.dmi"
DEFAULT_OUT = DATA_DIR / "out"


# ---- DMI parsing -----------------------------------------------------------


def parse_dmi(path: Path):
    """Return a callable that yields the 32x32 RGBA tile for a named state."""
    img = Image.open(path).convert("RGBA")
    desc = img.info.get("Description")
    if desc is None:
        raise RuntimeError(f"{path} is not a valid DMI (no Description zTXt chunk)")

    cols = img.size[0] // TILE
    header = re.compile(r'state = "([^"]*)"')
    frames_re = re.compile(r"frames = (\d+)")

    frame_map: dict[str, int] = {}
    cursor = 0
    for block in re.split(r'(?=state = ")', desc):
        m = header.search(block)
        if not m:
            continue
        name = m.group(1)
        fm = frames_re.search(block)
        frame_count = int(fm.group(1)) if fm else 1
        frame_map[name] = cursor
        cursor += frame_count

    def extract(state_name: str) -> Image.Image:
        if state_name not in frame_map:
            raise KeyError(f"DMI state '{state_name}' not found in {path}")
        idx = frame_map[state_name]
        col = idx % cols
        row = idx // cols
        return img.crop((col * TILE, row * TILE, (col + 1) * TILE, (row + 1) * TILE))

    return extract


# ---- Numpy RGBA blend ops (BYOND ICON_* equivalents) -----------------------


def _to_arr(img: Image.Image) -> np.ndarray:
    # int32 avoids overflow in 255*255 products.
    return np.asarray(img, dtype=np.int32).copy()


def _to_img(arr: np.ndarray) -> Image.Image:
    return Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8), "RGBA")


def _hex_to_rgb(h: str) -> tuple[int, int, int]:
    h = h.lstrip("#")
    return int(h[0:2], 16), int(h[2:4], 16), int(h[4:6], 16)


def tint(src: Image.Image, color: tuple[int, int, int]) -> Image.Image:
    """ICON_MULTIPLY with a solid color: per-channel (src * color / 255)."""
    a = _to_arr(src)
    cr, cg, cb = color
    a[..., 0] = a[..., 0] * cr // 255
    a[..., 1] = a[..., 1] * cg // 255
    a[..., 2] = a[..., 2] * cb // 255
    return _to_img(a)


def blend_overlay(dst: Image.Image | None, src: Image.Image) -> Image.Image:
    """ICON_OVERLAY: Porter-Duff src-over-dst."""
    if dst is None:
        return src.copy()
    out = dst.copy()
    out.alpha_composite(src)
    return out


def blend_add(dst: Image.Image, src: Image.Image) -> Image.Image:
    """ICON_ADD: dst.rgb += src.rgb (scaled by src alpha), alpha from dst."""
    d = _to_arr(dst)
    s = _to_arr(src)
    sa = s[..., 3:4]
    d[..., :3] = d[..., :3] + (s[..., :3] * sa // 255)
    return _to_img(d)


def blend_subtract(dst: Image.Image, src: Image.Image) -> Image.Image:
    """ICON_SUBTRACT: dst.rgb -= src.rgb (scaled by src alpha, clamped to 0)."""
    d = _to_arr(dst)
    s = _to_arr(src)
    sa = s[..., 3:4]
    d[..., :3] = d[..., :3] - (s[..., :3] * sa // 255)
    return _to_img(d)


def blend_multiply(dst: Image.Image, src: Image.Image) -> Image.Image:
    """ICON_MULTIPLY: dst.rgb *= src.rgb / 255 where src has alpha, else keep dst."""
    d = _to_arr(dst)
    s = _to_arr(src)
    sa = s[..., 3:4]
    mask = sa > 0
    product = d[..., :3] * s[..., :3] // 255
    d[..., :3] = np.where(mask, product, d[..., :3])
    return _to_img(d)


BLEND_OPS = {
    "overlay": blend_overlay,
    "add": blend_add,
    "subtract": blend_subtract,
    "multiply": blend_multiply,
}


def apply(dst: Image.Image | None, src: Image.Image, mode: str) -> Image.Image:
    if dst is None:
        return src.copy()
    return BLEND_OPS[mode](dst, src)


# ---- Greyscale templates (hand-translated from SS13 json configs) ----------


def _make_base(extract, c1):
    out = apply(None, tint(extract("base"), c1), "overlay")
    out = apply(out, extract("add_shader"), "add")
    out = apply(out, extract("multi_shader"), "multiply")
    return out


def _make_post(extract, c1):
    out = apply(None, tint(extract("outline"), c1), "overlay")
    out = apply(out, extract("lights"), "overlay")
    return out


def canister_default(extract, colors):
    c1 = colors[0]
    out = _make_base(extract, c1)
    return apply(out, _make_post(extract, c1), "overlay")


def canister_stripe(extract, colors):
    c1, c2 = colors[0], colors[1]
    out = _make_base(extract, c1)
    out = apply(out, tint(extract("stripe"), c2), "overlay")
    return apply(out, _make_post(extract, c1), "overlay")


def canister_double_stripe(extract, colors):
    c1, c2 = colors[0], colors[1]
    out = _make_base(extract, c1)
    inner = apply(None, tint(extract("double_stripe"), c2), "overlay")
    inner = apply(inner, extract("double_stripe_shader"), "subtract")
    out = apply(out, inner, "overlay")
    return apply(out, _make_post(extract, c1), "overlay")


def canister_hazard(extract, colors):
    c1, c2 = colors[0], colors[1]
    out = _make_base(extract, c1)
    out = apply(out, tint(extract("hazard_stripes"), c2), "overlay")
    return apply(out, _make_post(extract, c1), "overlay")


TEMPLATES = {
    "default": canister_default,
    "stripe": canister_stripe,
    "double_stripe": canister_double_stripe,
    "hazard": canister_hazard,
}


def make_broken(live: Image.Image, extract) -> Image.Image:
    """Overlay the broken shading on the live canister, then tip it on its side."""
    out = apply(live.copy(), extract("broken"), "overlay")
    return out.rotate(-90, expand=False)


# ---- Config + CLI ----------------------------------------------------------


def load_config(path: Path) -> dict:
    with path.open() as f:
        data = json.load(f)
    gases = data.get("gases")
    if not isinstance(gases, dict):
        raise ValueError(f"{path}: expected top-level 'gases' object")
    for name, entry in gases.items():
        if entry.get("template") not in TEMPLATES:
            raise ValueError(
                f"{path}: gas '{name}' uses unknown template '{entry.get('template')}'"
            )
        colors = entry.get("colors")
        if not isinstance(colors, list) or not (1 <= len(colors) <= 2):
            raise ValueError(f"{path}: gas '{name}' must have 1-2 hex colors")
    return data


def write_meta_json(rsi_dir: Path, gases: dict) -> None:
    names = sorted(gases.keys())
    states = []
    for name in names:
        states.append({"name": name})
        states.append({"name": f"{name}-1"})
    meta = {
        "version": 1,
        "license": "CC-BY-SA-3.0",
        "copyright": (
            "Baked from tgstation (AGPL-3.0 code, CC-BY-SA-3.0 sprites) greyscale "
            "canister layers at icons/obj/pipes_n_cables/canisters.dmi."
        ),
        "size": {"x": TILE, "y": TILE},
        "states": states,
    }
    (rsi_dir / "meta.json").write_text(json.dumps(meta, indent=2) + "\n")


def bake_one(name: str, entry: dict, extract, out_dir: Path) -> None:
    template_fn = TEMPLATES[entry["template"]]
    colors = [_hex_to_rgb(c) for c in entry["colors"]]
    live = template_fn(extract, colors)
    live.save(out_dir / f"{name}.png")
    make_broken(live, extract).save(out_dir / f"{name}-1.png")


def main(argv=None) -> int:
    parser = argparse.ArgumentParser(
        description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter
    )
    parser.add_argument(
        "--dmi",
        type=Path,
        default=DEFAULT_DMI,
        help=f"source DMI path (default: {DEFAULT_DMI})",
    )
    parser.add_argument(
        "--out",
        type=Path,
        default=DEFAULT_OUT,
        help=f"output directory for baked PNGs (default: {DEFAULT_OUT.relative_to(REPO_ROOT)})",
    )
    parser.add_argument(
        "--config", type=Path, default=DEFAULT_CONFIG, help="gas table JSON path"
    )
    parser.add_argument(
        "--gas",
        action="append",
        help="bake only this gas (repeatable). Defaults to every gas in the config.",
    )
    parser.add_argument(
        "--list", action="store_true", help="list configured gases and exit"
    )
    parser.add_argument(
        "--write-meta",
        action="store_true",
        help="regenerate meta.json with every configured gas as a state",
    )
    args = parser.parse_args(argv)

    config = load_config(args.config)
    gases = config["gases"]

    if args.list:
        for name in sorted(gases):
            entry = gases[name]
            print(f"  {name:<16} {entry['template']:<14} {entry['colors']}")
        return 0

    if not args.dmi.exists():
        print(f"error: DMI source not found: {args.dmi}", file=sys.stderr)
        print(
            "hint: set --dmi to the path of a russtation canisters.dmi", file=sys.stderr
        )
        return 2

    args.out.mkdir(parents=True, exist_ok=True)
    extract = parse_dmi(args.dmi)

    targets = args.gas if args.gas else sorted(gases.keys())
    for name in targets:
        if name not in gases:
            print(
                f"error: unknown gas '{name}' (not in {args.config})", file=sys.stderr
            )
            return 2
        bake_one(name, gases[name], extract, args.out)
        print(f"  baked {name}")

    if args.write_meta:
        write_meta_json(args.out, gases)
        print(f"  wrote {args.out / 'meta.json'}")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
