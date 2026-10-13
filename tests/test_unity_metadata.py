"""Exercise corrupt metadata fixtures without mutating production assets."""
from pathlib import Path
import sys
import tempfile
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "tools"))
from check_unity_metadata import validate_assets


class UnityMetadataChecks(unittest.TestCase):
    GUID = "7b17694538454915b4f708c18797f6f0"

    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.assets = Path(self.temp.name) / "Assets"
        self.assets.mkdir()

    def asset(self, name, guid=None):
        path = self.assets / name
        path.write_text("fixture")
        path.with_name(path.name + ".meta").write_text(
            "fileFormatVersion: 2\nguid: " + (guid or self.GUID) + "\nMonoImporter:\n  externalObjects: {}\n")
        return path

    def test_valid_files_folders_and_uppercase_guid(self):
        folder = self.assets / "Scripts"
        folder.mkdir()
        folder.with_name("Scripts.meta").write_text("guid: " + "1" * 32 + "\nfolderAsset: yes\n")
        self.asset("Scripts/Hero.cs", self.GUID.upper())
        self.assertEqual([], validate_assets(self.assets))

    def test_rejects_interrupted_33_character_guid(self):
        self.asset("Hero.cs", self.GUID + "1")
        errors = validate_assets(self.assets)
        self.assertEqual(1, len(errors))
        self.assertIn("Invalid GUID", errors[0])
        self.assertIn("Hero.cs.meta", errors[0])

    def test_rejects_short_nonhex_and_zero_guids(self):
        for guid in [self.GUID[:-1], "g" + self.GUID[1:], "0" * 32]:
            with self.subTest(guid=guid):
                self.asset("Hero.cs", guid)
                self.assertIn("Invalid GUID", validate_assets(self.assets)[0])

    def test_rejects_case_insensitive_duplicate_guids(self):
        self.asset("Hero.cs")
        self.asset("Other.cs", self.GUID.upper())
        errors = validate_assets(self.assets)
        self.assertEqual(1, len(errors))
        self.assertIn("Duplicate GUID", errors[0])
        self.assertIn("Hero.cs.meta", errors[0])
        self.assertIn("Other.cs.meta", errors[0])

    def test_rejects_missing_and_multiple_top_level_guid_lines(self):
        path = self.asset("Hero.cs").with_name("Hero.cs.meta")
        for text in ["MonoImporter:\n  guid: " + self.GUID + "\n",
                     "guid: " + self.GUID + "\nguid: " + "2" * 32 + "\n"]:
            with self.subTest(text=text):
                path.write_text(text)
                errors = validate_assets(self.assets)
                self.assertEqual(1, len(errors))
                self.assertIn("Expected one top-level GUID", errors[0])

    def test_rejects_missing_and_orphan_metadata(self):
        self.asset("Hero.cs").with_name("Hero.cs.meta").unlink()
        self.asset("Removed.cs", "2" * 32).unlink()
        errors = validate_assets(self.assets)
        self.assertEqual(2, len(errors))
        self.assertTrue(any("Missing metadata" in error and "Hero.cs" in error for error in errors))
        self.assertTrue(any("Orphan metadata" in error and "Removed.cs.meta" in error for error in errors))

    def test_missing_assets_directory_is_an_error(self):
        self.assertIn("Assets directory missing", validate_assets(self.assets / "Absent")[0])


if __name__ == "__main__":
    unittest.main()
