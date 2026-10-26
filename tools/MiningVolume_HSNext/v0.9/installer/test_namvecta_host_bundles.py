"""Contract tests for five host-selective NAMVECTA Autodesk bundles."""
import hashlib
import json
import shutil
import tempfile
import unittest
import xml.etree.ElementTree as ET
from pathlib import Path

import namvecta_bundle as core
import namvecta_host_bundles as host

SHA = "b" * 40

class HostBundleTests(unittest.TestCase):
    def setUp(self):
        self.work = tempfile.TemporaryDirectory()
        self.addCleanup(self.work.cleanup)
        self.root = Path(self.work.name)
        self.source = self.root / "source"
        self.source.mkdir()
        for year, (_, flavor) in core.VERSIONS.items():
            directory = self.source / flavor
            directory.mkdir()
            required, _ = core.expected_files(year)
            for name in required - {"BUILD_IDENTITY.txt"}:
                (directory / name).write_bytes((flavor + "/" + name).encode())
            hashes = []
            for file in sorted(directory.glob("*.dll")):
                hashes.append(hashlib.sha256(file.read_bytes()).hexdigest() + "  " + file.name)
            (directory / "BUILD_IDENTITY.txt").write_text(
                "SOURCE_SHA=" + SHA + "\n" + "\n".join(hashes) + "\n", encoding="ascii")

    def test_uses_five_separate_single_year_autoload_bundles(self):
        output = self.root / "Bundles"
        host.stage(self.source, output, SHA)
        bundles = sorted(output.glob("*.bundle"))
        self.assertEqual(len(bundles), 5)
        expected = set()
        for year, (series, flavor) in core.VERSIONS.items():
            folder = output / ("NAMVECTAVolume" + str(year) + ".bundle")
            self.assertIn(folder, bundles)
            root = ET.parse(folder / "PackageContents.xml").getroot()
            self.assertEqual(root.attrib["Name"], "NAMVECTA VOLUME")
            self.assertEqual(len(root.findall("Components")), 1)
            component = root.find("Components")
            req = component.find("RuntimeRequirements")
            self.assertEqual(req.attrib["SeriesMin"], series)
            self.assertEqual(req.attrib["SeriesMax"], series)
            entry = component.find("ComponentEntry")
            self.assertIn(core.expected_files(year)[1], entry.attrib["ModuleName"])
            self.assertTrue((folder / entry.attrib["ModuleName"][2:]).exists())
            expected.add(str(year))
        manifest = json.loads((output / "STAGING_QA.json").read_text(encoding="utf-8"))
        self.assertEqual(set(manifest["bundles"]), expected)
        self.assertEqual(manifest["status"], "NOT_RELEASE_HOST_RUNTIME_UNVERIFIED")
        self.assertEqual(set(manifest["host_years"]), set(core.VERSIONS))
        with self.assertRaisesRegex(ValueError, "already exists"):
            host.stage(self.source, output, SHA)

    def test_missing_or_corrupt_binary_fails_before_writing(self):
        folder = self.source / "ACAD2022_NET48"
        (folder / "MiningVolume.Surface.dll").write_bytes(b"corrupted")
        dest = self.root / "broken-output"
        with self.assertRaisesRegex(ValueError, "hash mismatch"):
            host.stage(self.source, dest, SHA)
        self.assertFalse(dest.exists())

    def test_qa_installer_never_auto_deletes_legacy_bundle(self):
        script = (Path(__file__).parent / "NAMVECTA_Volume_V2.iss").read_text(encoding="utf-8")
        self.assertIn("function LegacyBundlePresent: Boolean;", script)
        self.assertIn("MiningVolume2023.bundle", script)
        self.assertIn("else if LegacyBundlePresent then", script)
        self.assertIn("LegacyConflict", script)
        self.assertNotIn("DeleteFolder(ExpandConstant('{commonappdata}", script)

    def test_manifest_and_installer_stage_must_have_only_five_hosts(self):
        self.assertEqual(set(core.VERSIONS.keys()), set(range(2020, 2025)))
        for year in core.VERSIONS:
            manifest = host.package_xml(year, *core.VERSIONS[year])
            root = manifest.getroot()
            self.assertEqual(root.find("Components/RuntimeRequirements").attrib["SeriesMin"],
                             core.VERSIONS[year][0])
        self.assertNotIn(2025, core.VERSIONS)


    def test_accepted_cc0a6cb7_ui_and_inno_setup_are_byte_identical(self):
        """Protect the user's approved UI and installer from accidental branch merges.

        Git's blob hash is SHA1("blob <byte length>\\0" + raw bytes).
        These eight expected blobs were read from commit cc0a6cb7.
        """
        approved = {
            "src/MiningVolume.Plugin2023/UI/MainPaletteControl.cs": "799409885e8749dfa10e688005f2cf5ef1f2cc97",
            "src/MiningVolume.Plugin2023/UI/ModelWorkflowPage.cs": "e0841c0b0b65238561e42607779804dad8295605",
            "src/MiningVolume.Plugin2023/UI/SectionPage.cs": "6a6b6e7d139b2a8ef56325c942fd0250fb549e67",
            "src/MiningVolume.Plugin2023/UI/GridPage.cs": "d38017af3927f5b651a291fb79cc19ad121a0e56",
            "src/MiningVolume.Plugin2023/UI/ExportPage.cs": "2dbdb79a89d50c0b8f16b95926d8520b6b80a5d8",
            "installer/NAMVECTA_Volume_V2.iss": "a21f67ad6ff917c7aeed7a93f153ea0c543c9397",
            "installer/prepare_namvecta_branding.py": "0f4d9ba65670db33053d4944b6f948ef9571e18e",
            "installer/namvecta_host_bundles.py": "e9e56e75cfe91a9cb2c2134a36d4a34da1072a35",
        }
        root = Path(__file__).resolve().parent.parent
        for relative, expected_git_sha in approved.items():
            raw = (root / relative).read_bytes()
            actual = hashlib.sha1(
                b"blob " + str(len(raw)).encode("ascii") + b"\\0" + raw
            ).hexdigest()
            self.assertEqual(actual, expected_git_sha,
                             "Accepted cc0a6cb7 UI/installer drifted: " + relative)


if __name__ == "__main__":
    unittest.main()
