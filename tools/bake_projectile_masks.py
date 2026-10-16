#!/usr/bin/env python3
"""Check or explicitly bake immutable projectile alpha-outline render data.

These masks derive only from accepted Effects.png alpha. They add no artwork,
collision geometry or runtime pixel reads. Default operation never writes.
"""
import argparse
import hashlib
import io
from pathlib import Path
import re
import sys

from PIL import Image, ImageChops, ImageFilter

ROOT = Path(__file__).resolve().parents[1]
ART = Path("Assets/Resources/Frontier/Art")
SOURCE_SHA256 = "6b049b573548fef51fcc80d845f3704a8a0eeba2ecd5a14eb739fe0867e1a8c7"
CELL = 128
PADDING = 8
SIZE = CELL + PADDING * 2
THRESHOLD = 96
CELLS = {"Arrow": 8, "Starfire": 9, "Orb": 10}
GUIDS = {
    "ProjectileMasks": "e18bad822430406592780b9267fc8fef",
    "dark_Arrow": "31988b2dd0964985bbf782981638fdac",
    "edge_Arrow": "39764414ba184825b6ab9a76f553a5af",
    "dark_Starfire": "d68d1c82519f43a49f5e439a0912c320",
    "edge_Starfire": "998e668c176741f3bcb736ae07284ab1",
    "dark_Orb": "41d9e07a7a574e559955df5cf492d30e",
    "edge_Orb": "fa38305b044b40358082096c18f7c4ff",
}


def metadata(name):
    start = "fileFormatVersion: 2\nguid: " + GUIDS[name] + "\n"
    if name == "ProjectileMasks":
        return start + "folderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n"
    return start + """TextureImporter:
  externalObjects: {}
  serializedVersion: 12
  mipmaps:
    enableMipMap: 0
  isReadable: 0
  maxTextureSize: 4096
  textureSettings:
    serializedVersion: 2
    filterMode: 0
    wrapU: 1
    wrapV: 1
    wrapW: 1
  nPOTScale: 0
  alphaIsTransparency: 1
  textureType: 0
  textureCompression: 0
  userData:
  assetBundleName:
  assetBundleVariant:
"""


def source_image(root):
    path = Path(root) / ART / "Effects.png"
    data = path.read_bytes()
    if data.startswith(b"version https://git-lfs.github.com/spec/v1"):
        raise ValueError("Effects.png is an unexpanded LFS pointer")
    try:
        with Image.open(io.BytesIO(data)) as source:
            source.load()
            if source.format != "PNG" or source.mode != "RGBA" or source.size != (512, 512):
                raise ValueError("Effects.png must decode as 512x512 RGBA PNG")
            image = source.copy()
    except (OSError, SyntaxError) as exc:
        raise ValueError("Effects.png cannot decode as PNG: " + str(exc)) from exc
    digest = hashlib.sha256(data).hexdigest()
    if digest != SOURCE_SHA256:
        raise ValueError("Effects.png source SHA256 changed: " + digest)
    return image


def mask_images(image):
    """Isolate each cell before dilation, so adjacent effect cells cannot leak."""
    results = {}
    for name, index in CELLS.items():
        left, top = index % 4 * CELL, index // 4 * CELL
        alpha = image.crop((left, top, left + CELL, top + CELL)).getchannel("A")
        binary = alpha.point(lambda value: 255 if value >= THRESHOLD else 0)
        padded = Image.new("L", (SIZE, SIZE), 0)
        padded.paste(binary, (PADDING, PADDING))
        for kind, kernel in (("dark", 17), ("edge", 9)):
            ring = ImageChops.subtract(padded.filter(ImageFilter.MaxFilter(kernel)), padded)
            mask = Image.new("RGBA", (SIZE, SIZE), (255, 255, 255, 0))
            mask.putalpha(ring)
            results[kind + "_" + name] = mask
    return results


def png_bytes(image):
    stream = io.BytesIO()
    image.save(stream, format="PNG", optimize=False, compress_level=9)
    return stream.getvalue()


def metadata_errors(data, name):
    """Read Unity's simple mapping fields without prescribing its formatting.

    Native imports may add fields or reserialize YAML. Only the identity and
    render-critical importer choices belong to this derivative contract.
    """
    try:
        text = data.decode("utf-8-sig")
    except UnicodeError:
        return ["metadata is not UTF-8"]
    fields = {}
    stack = []
    for line in text.splitlines():
        clean = line.split("#", 1)[0].rstrip()
        match = re.match(r"^( *)([A-Za-z_][A-Za-z_0-9]*):\s*(.*?)\s*$", clean)
        if not match:
            continue
        indent, key, value = len(match.group(1)), match.group(2), match.group(3)
        while stack and stack[-1][0] >= indent:
            stack.pop()
        path = "/".join([entry[1] for entry in stack] + [key])
        fields.setdefault(path, []).append(value.strip("\"'"))
        if not value:
            stack.append((indent, key))
    required = {"guid": GUIDS[name]}
    if name == "ProjectileMasks":
        required["folderAsset"] = "yes"
        section = "DefaultImporter"
    else:
        section = "TextureImporter"
        required.update({
            "TextureImporter/mipmaps/enableMipMap": "0",
            "TextureImporter/isReadable": "0",
            "TextureImporter/maxTextureSize": "4096",
            "TextureImporter/textureSettings/filterMode": "0",
            "TextureImporter/textureSettings/wrapU": "1",
            "TextureImporter/textureSettings/wrapV": "1",
            "TextureImporter/textureSettings/wrapW": "1",
            "TextureImporter/nPOTScale": "0",
            "TextureImporter/alphaIsTransparency": "1",
            "TextureImporter/textureType": "0",
            "TextureImporter/textureCompression": "0",
        })
    errors = []
    if section not in fields:
        errors.append("missing " + section + " section")
    for path, expected in required.items():
        actual = fields.get(path, [])
        if path == "guid":
            actual = [value.lower() for value in actual]
        if actual != [expected]:
            errors.append("expected one " + path + ": " + expected)
    return errors


def expected_files(root):
    folder = Path(root) / ART / "ProjectileMasks"
    expected = {folder.with_suffix(".meta"): metadata("ProjectileMasks").encode()}
    for name, image in mask_images(source_image(root)).items():
        expected[folder / (name + ".png")] = png_bytes(image)
        expected[folder / (name + ".png.meta")] = metadata(name).encode()
    return expected


def validate(root=ROOT):
    try:
        expected = expected_files(root)
    except (OSError, ValueError) as exc:
        return [str(exc)]
    errors = []
    for path, data in expected.items():
        if not path.is_file():
            errors.append("Missing derived projectile file: " + str(path))
            continue
        actual = path.read_bytes()
        if path.suffix == ".png":
            try:
                with Image.open(io.BytesIO(actual)) as image:
                    image.load()
                    if image.format != "PNG" or image.mode != "RGBA" or image.size != (SIZE, SIZE):
                        errors.append("Projectile mask must decode as 144x144 RGBA PNG: " + str(path))
                        continue
                    with Image.open(io.BytesIO(data)) as expected_image:
                        if image.tobytes() != expected_image.tobytes():
                            errors.append("Stale derived projectile mask: " + str(path))
            except (OSError, SyntaxError, ValueError) as exc:
                errors.append("Projectile mask cannot decode: " + str(path) + ": " + str(exc))
                continue
        else:
            name = "ProjectileMasks" if path.name == "ProjectileMasks.meta" else path.name.removesuffix(".png.meta")
            details = metadata_errors(actual, name)
            if details:
                errors.append("Invalid projectile import metadata: " + str(path) + ": " + "; ".join(details))
    # Check the fixed new GUIDs against the actual project, without modifying it.
    owned_metadata = {path for path in expected if path.suffix == ".meta"}
    guids = set(GUIDS.values())
    for path in (Path(root) / "Assets").rglob("*.meta"):
        if path in owned_metadata:
            continue
        found = re.search(r"^guid:\s*([0-9a-fA-F]{32})\s*$", path.read_text(), re.MULTILINE)
        if found and found.group(1).lower() in guids:
            errors.append("Projectile GUID collides with existing asset: " + str(path))
    return errors


def write_masks(root=ROOT):
    expected = expected_files(root)
    # Keep interrupted reruns safe: never silently replace a different GUID or
    # importer setup, and never touch the accepted source or other art assets.
    for path, data in expected.items():
        if path.suffix == ".meta" and path.exists():
            name = "ProjectileMasks" if path.name == "ProjectileMasks.meta" else path.name.removesuffix(".png.meta")
            if metadata_errors(path.read_bytes(), name):
                raise ValueError("Refusing to replace existing metadata: " + str(path))
    for path, data in expected.items():
        if path.suffix == ".meta" and path.exists():
            continue
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(data)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--write", action="store_true", help="Explicitly bake six alpha masks and their new Unity metadata")
    args = parser.parse_args()
    try:
        if args.write:
            write_masks()
        errors = validate()
    except (OSError, ValueError) as exc:
        errors = [str(exc)]
    for error in errors:
        print("FAIL " + error, file=sys.stderr)
    if errors:
        return 1
    print("PASS six 144x144 projectile alpha-outline masks and Point/clamp/no-mip/non-readable/uncompressed metadata; Effects source SHA256 " + SOURCE_SHA256)
    return 0


if __name__ == "__main__":
    sys.exit(main())
