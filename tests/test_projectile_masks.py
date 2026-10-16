"""Independent alpha geometry and stale/corrupt derivative rejection checks."""
import io
from pathlib import Path
import shutil
import sys
import tempfile
import unittest

from PIL import Image

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "tools"))
import bake_projectile_masks as bake


class ProjectileMaskChecks(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.art = self.root / bake.ART
        self.art.mkdir(parents=True)
        self.source = self.art / "Effects.png"
        shutil.copyfile(bake.ROOT / bake.ART / "Effects.png", self.source)
        bake.write_masks(self.root)
        self.folder = self.art / "ProjectileMasks"

    def test_six_real_masks_match_independent_pixel_geometry(self):
        image = bake.source_image(self.root)
        for name, index in bake.CELLS.items():
            cell = image.crop((index % 4 * 128, index // 4 * 128,
                               (index % 4 + 1) * 128, (index // 4 + 1) * 128))
            core = {(x + 8, y + 8) for y in range(128) for x in range(128)
                    if cell.getpixel((x, y))[3] >= 96}
            rings = {}
            for kind, radius in (("dark", 8), ("edge", 4)):
                expanded = {(x + dx, y + dy) for x, y in core
                            for dy in range(-radius, radius + 1)
                            for dx in range(-radius, radius + 1)
                            if 0 <= x + dx < 144 and 0 <= y + dy < 144}
                expected = expanded - core
                with Image.open(self.folder / (kind + "_" + name + ".png")) as mask:
                    actual = {(x, y) for y in range(144) for x in range(144)
                              if mask.getpixel((x, y))[3] == 255}
                    self.assertEqual(expected, actual, kind + "_" + name)
                    self.assertTrue(all(pixel[:3] == (255, 255, 255) and pixel[3] in (0, 255)
                                        for pixel in mask.getdata()))
                self.assertTrue(core.isdisjoint(actual), "Authored core is never painted by the witness")
                self.assertTrue(all(0 < x < 143 and 0 < y < 143 for x, y in actual), "Accepted source retains clear padded border")
                rings[kind] = actual
            self.assertTrue(rings["edge"] <= rings["dark"])
        self.assertEqual([], bake.validate(self.root))

    def test_cell_isolation_threshold_and_determinism(self):
        image = Image.new("RGBA", (512, 512), (255, 255, 255, 255))
        for index in bake.CELLS.values():
            image.paste((255, 255, 255, 0), (index % 4 * 128, index // 4 * 128,
                                          (index % 4 + 1) * 128, (index // 4 + 1) * 128))
        for mask in bake.mask_images(image).values():
            self.assertIsNone(mask.getchannel("A").getbbox(), "Opaque adjacent cells cannot leak into isolated transparent cells")
        image.putpixel((64, 2 * 128 + 64), (255, 255, 255, 95))
        self.assertIsNone(bake.mask_images(image)["edge_Arrow"].getchannel("A").getbbox())
        image.putpixel((64, 2 * 128 + 64), (255, 255, 255, 96))
        first, second = bake.mask_images(image), bake.mask_images(image)
        self.assertEqual((68, 68, 77, 77), first["edge_Arrow"].getchannel("A").getbbox())
        for name in first:
            self.assertEqual(bake.png_bytes(first[name]), bake.png_bytes(second[name]))
        before = {path.relative_to(self.root): path.read_bytes() for path in self.folder.iterdir()}
        bake.write_masks(self.root)
        after = {path.relative_to(self.root): path.read_bytes() for path in self.folder.iterdir()}
        self.assertEqual(before, after)

    def test_missing_corrupt_wrong_size_mode_and_lfs_masks_rejected(self):
        path = self.folder / "dark_Arrow.png"
        original = path.read_bytes()
        path.unlink()
        self.assertTrue(any("Missing" in error for error in bake.validate(self.root)))
        fixtures = [b"not a PNG", b"version https://git-lfs.github.com/spec/v1\noid sha256:" + b"0" * 64]
        for size, mode in (((143, 144), "RGBA"), ((144, 144), "RGB")):
            stream = io.BytesIO()
            Image.new(mode, size).save(stream, "PNG")
            fixtures.append(stream.getvalue())
        for data in fixtures:
            with self.subTest(header=data[:24]):
                path.write_bytes(data)
                self.assertTrue(bake.validate(self.root))
        path.write_bytes(original)
        self.assertEqual([], bake.validate(self.root))

    def test_valid_but_stale_mask_rejected(self):
        path = self.folder / "edge_Orb.png"
        with Image.open(path) as original:
            mask = original.copy()
        mask.putpixel((0, 0), (255, 255, 255, 255))
        path.write_bytes(bake.png_bytes(mask))
        self.assertTrue(any("Stale derived" in error for error in bake.validate(self.root)))

    def test_equivalent_png_and_native_metadata_reserialization_accepted(self):
        path = self.folder / "edge_Orb.png"
        with Image.open(path) as original:
            image = original.copy()
        alternate = io.BytesIO()
        image.save(alternate, "PNG", compress_level=0)
        self.assertNotEqual(path.read_bytes(), alternate.getvalue())
        path.write_bytes(alternate.getvalue())
        meta = path.with_suffix(".png.meta")
        native = meta.read_text().replace("guid: ", "guid:  ").replace("filterMode: 0", "filterMode:   0 # Point")
        native = native.replace("  externalObjects: {}", "  externalObjects: {}\n  spriteMode: 0\n  generateCubemap: 6")
        native = native.replace("guid:  " + bake.GUIDS["edge_Orb"], "guid:  " + bake.GUIDS["edge_Orb"].upper())
        native = native.replace("\n", "\r\n")
        meta.write_bytes(native.encode())
        folder_meta = self.folder.with_suffix(".meta")
        folder_native = folder_meta.read_bytes() + b"  additionalNativeField: 1\n"
        folder_meta.write_bytes(folder_native)
        self.assertEqual([], bake.validate(self.root))
        bake.write_masks(self.root)
        self.assertEqual(native.encode(), meta.read_bytes(), "Explicit rebake preserves valid native metadata byte-for-byte")
        self.assertEqual(folder_native, folder_meta.read_bytes())
        self.assertEqual([], bake.validate(self.root))

    def test_source_corruption_missing_lfs_and_stale_source_rejected(self):
        original = self.source.read_bytes()
        self.source.unlink()
        self.assertTrue(bake.validate(self.root))
        for data, reason in ((b"broken", "decode"),
                             (b"version https://git-lfs.github.com/spec/v1\n", "LFS pointer")):
            self.source.write_bytes(data)
            self.assertTrue(any(reason in error for error in bake.validate(self.root)))
        with Image.open(io.BytesIO(original)) as image:
            changed = image.copy()
        changed.putpixel((0, 0), (0, 0, 0, 1))
        self.source.write_bytes(bake.png_bytes(changed))
        self.assertTrue(any("source SHA256 changed" in error for error in bake.validate(self.root)))

    def test_metadata_import_settings_and_guid_collisions_rejected(self):
        path = self.folder / "edge_Starfire.png.meta"
        original = path.read_bytes()
        for before, after in (("filterMode: 0", "filterMode: 1"),
                              ("isReadable: 0", "isReadable: 1"),
                              ("enableMipMap: 0", "enableMipMap: 1"),
                              ("wrapU: 1", "wrapU: 0"),
                              ("textureCompression: 0", "textureCompression: 1")):
            with self.subTest(setting=before):
                path.write_text(original.decode().replace(before, after))
                self.assertTrue(any("import metadata" in error for error in bake.validate(self.root)))
                with self.assertRaisesRegex(ValueError, "Refusing to replace"):
                    bake.write_masks(self.root)
        path.write_text(original.decode().replace(bake.GUIDS["edge_Starfire"], "1" * 32))
        self.assertTrue(any("import metadata" in error for error in bake.validate(self.root)))
        with self.assertRaisesRegex(ValueError, "Refusing to replace"):
            bake.write_masks(self.root)
        path.write_bytes(original)
        (self.root / "Assets" / "Other.meta").write_text("guid: " + bake.GUIDS["edge_Starfire"] + "\n")
        self.assertTrue(any("GUID collides" in error for error in bake.validate(self.root)))


if __name__ == "__main__":
    unittest.main()
