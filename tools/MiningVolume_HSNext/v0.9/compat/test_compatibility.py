"""Never confuse an AutoCAD version declaration with a working binary."""
import unittest
from pathlib import Path
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
PROPS = ROOT / "compat" / "IMSAT.CadCompatibility.props"


class CompatibilityBuildContracts(unittest.TestCase):
    def test_five_supported_compile_flavors_are_unique(self):
        root = ET.parse(PROPS).getroot()
        props = [
            (n.attrib.get("Condition", ""), n.text.strip() if n.text else "")
            for n in root.iter() if n.tag == "IMSATCadPackageVersion"
        ]
        flavors = ("ACAD2021_NET48", "ACAD2022_NET48",
                   "ACAD2023_NET48", "ACAD2024_NET48",
                   "ACAD2025_NET8", "ACAD2025_NET10",
                   "ACAD2026_NET8", "ACAD2026_NET10")
        versions = ("24.0.0", "24.1.51000", "24.2.0", "24.3.0",
                    "25.0.1", "25.0.2", "25.1.0", "25.1.1")
        self.assertEqual(len(props), 8)
        for flavor, version in zip(flavors, versions):
            self.assertEqual(sum(flavor in cond for cond, _ in props), 1)
            self.assertTrue(any(flavor in cond and value == version
                                for cond, value in props))

    def test_all_four_existing_projects_use_shared_platform_manifest(self):
        project_files = [
            "MiningVolume.Core/MiningVolume.Core.csproj",
            "MiningVolume.Surface/MiningVolume.Surface.csproj",
            "MiningVolume.Cad2023/MiningVolume.Cad2023.csproj",
            "MiningVolume.Plugin2023/MiningVolume.Plugin2023.csproj",
        ]
        for path in project_files:
            with self.subTest(path=path):
                file = ROOT / "src" / path
                tree = ET.parse(file).getroot()
                self.assertTrue(any(
                    x.tag == "Import" and
                    "IMSAT.CadCompatibility.props" in x.attrib.get("Project", "")
                    for x in tree))
                self.assertTrue(any(
                    x.tag == "TargetFramework" and x.text == "$(IMSATCadFramework)"
                    for x in tree.iter()))

    def test_2023_package_stays_restricted_to_2023(self):
        package = ROOT / "bundle" / "MiningVolume2023.bundle" / "PackageContents.xml"
        data = package.read_text(encoding="utf-8")
        self.assertIn('SeriesMin="R24.2"', data)
        self.assertIn('SeriesMax="R24.2"', data)
        self.assertNotIn('SeriesMax="R25.1"', data)

    def test_candidate_artifacts_cannot_impersonate_approved_release(self):
        script = (ROOT / "compat" / "build_compat.ps1").read_text("utf-8")
        self.assertIn("NOT FOR INSTALLATION", script)
        self.assertIn("HostRuntimeAcceptance=NOT_TESTED", script)
        self.assertIn("InstallerApproval=BLOCKED", script)
        self.assertIn("UseAutoCADNuGet=true", script)

    def test_unknown_legacy_flavor_does_not_fall_back_to_net48(self):
        data = PROPS.read_text(encoding="utf-8")
        self.assertIn("unsupported CAD flavor", data)
        self.assertNotIn("ACAD2012_NET48", data)


if __name__ == "__main__":
    unittest.main()
