"""Test NAMVECTA QA packaging without AutoCAD/Windows or external libraries."""
import hashlib
import json
import tempfile
import unittest
import xml.etree.ElementTree as ET
from pathlib import Path

import namvecta_bundle as pack

SHA = "a" * 40


class BundleTests(unittest.TestCase):
    def setUp(self):
        self.work = tempfile.TemporaryDirectory()
        self.addCleanup(self.work.cleanup)
        self.root = Path(self.work.name)
        self.stage = self.root / "stage"
        self.stage.mkdir()
        for year, (_, flavor) in pack.VERSIONS.items():
            folder = self.stage / flavor
            folder.mkdir()
            required, _ = pack.expected_files(year)
            for name in sorted(required - {"BUILD_IDENTITY.txt"}):
                (folder / name).write_bytes(("AutoCAD %d: " % year + name).encode())
            hashes = [
                hashlib.sha256((folder / name).read_bytes()).hexdigest() + "  " + name
                for name in sorted(required) if name.endswith(".dll")
            ]
            (folder / "BUILD_IDENTITY.txt").write_text(
                "SOURCE_SHA=" + SHA + "\n" + "\n".join(hashes) + "\n",
                encoding="utf-8")

    def test_success_all_five_isolated_components(self):
        output = self.root / "dist" / "NAMVECTAVolume.bundle"
        metadata = pack.build(self.stage, output, SHA)
        self.assertTrue(output.is_dir())
        self.assertEqual(metadata["autocad_years"], [2020, 2021, 2022, 2023, 2024])
        self.assertEqual(metadata["status"], "QA_CANDIDATE_NOT_RELEASE")
        self.assertEqual(metadata["host_runtime"], "NOT_TESTED")
        self.assertEqual(metadata["logo_pa3_production"], "NOT_APPROVED")
        pkg = ET.parse(output / "PackageContents.xml").getroot()
        self.assertEqual(pkg.attrib["Name"], "NAMVECTA VOLUME")
        self.assertEqual(len(pkg.findall("Components")), 5)
        series = []
        paths = []
        for component in pkg.findall("Components"):
            requirements = component.findall("RuntimeRequirements")
            entries = component.findall("ComponentEntry")
            self.assertEqual(len(requirements), 1)
            self.assertEqual(len(entries), 1)
            self.assertEqual(requirements[0].attrib["SeriesMin"],
                             requirements[0].attrib["SeriesMax"])
            series.append(requirements[0].attrib["SeriesMin"])
            paths.append(entries[0].attrib["ModuleName"])
            self.assertTrue((output / entries[0].attrib["ModuleName"].removeprefix("./")).is_file())
        self.assertEqual(series, [x[0] for x in pack.VERSIONS.values()])
        self.assertEqual(len(set(paths)), 5)
        manifest = json.loads((output / "NAMVECTA_BUNDLE_QA_MANIFEST.json").read_text())
        self.assertTrue(all(len(x["sha256"]) == 64 for x in manifest["files"]))
        with self.assertRaisesRegex(ValueError, "Refusing to overwrite"):
            pack.build(self.stage, output, SHA)

    def test_bad_inputs_fail_without_creating_output(self):
        def check_bad(edit):
            with tempfile.TemporaryDirectory() as temp:
                import shutil
                stage = Path(temp) / "stage"
                shutil.copytree(self.stage, stage)
                edit(stage)
                out = Path(temp) / "out.bundle"
                with self.assertRaises(ValueError):
                    pack.build(stage, out, SHA)
                self.assertFalse(out.exists())
        check_bad(lambda p: (p / "ACAD2020_NET47" / "MiningVolume.Core.dll").write_bytes(b"changed"))
        check_bad(lambda p: (p / "ACAD2024_NET48" / "NOTICE.txt").unlink())
        check_bad(lambda p: (p / "ACAD2023_NET48" / "AcMgd.dll").write_bytes(b"foreign"))
        check_bad(lambda p: (p / "ACAD2021_NET48" / "BUILD_IDENTITY.txt").write_text("SOURCE_SHA=wrong"))
        with self.assertRaises(ValueError):
            pack.build(self.stage, self.root / "invalid.bundle", "not-a-commit")

    def test_no_other_years_even_with_matching_dll_files(self):
        self.assertEqual(set(pack.VERSIONS), {2020, 2021, 2022, 2023, 2024})
        self.assertNotIn(2025, pack.VERSIONS)
        self.assertNotIn(2026, pack.VERSIONS)


if __name__ == "__main__":
    unittest.main()
