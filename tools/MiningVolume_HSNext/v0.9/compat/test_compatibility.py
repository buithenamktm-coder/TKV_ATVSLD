"""Never confuse an AutoCAD version declaration with a working binary."""
import unittest
from pathlib import Path
import xml.etree.ElementTree as ET
import importlib.util
import hashlib
import json
import shutil
import subprocess
import tempfile

ROOT = Path(__file__).resolve().parents[1]
PROPS = ROOT / "compat" / "IMSAT.CadCompatibility.props"


class CompatibilityBuildContracts(unittest.TestCase):
    @unittest.skipUnless(shutil.which('pwsh'), 'PowerShell required for executable packaging test')
    def test_package_accepts_dll_names_and_rolls_back_unsafe_names(self):
        # Exercise the actual PowerShell validator, hashes and XML writer.
        # Synthetic payloads test packaging only, never AutoCAD compatibility.
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            compiled = root / 'compiled'
            compiled.mkdir()
            names = ['MiningVolume2023.dll', 'MiningVolume.Core.dll',
                     'MiningVolume.Surface.dll', 'MiningVolume.Cdt.Native.dll',
                     'MiningVolume.Cad2023.dll']
            files = []
            for name in names:
                data = ('packaging fixture ' + name).encode()
                (compiled / name).write_bytes(data)
                files.append({'name': name, 'sha256': hashlib.sha256(data).hexdigest()})
            manifest = dict(flavor='ACAD2023_NET48', compile='PASS', host='NOT_TESTED',
                            release='BLOCKED', source_sha='a'*40, files=files)
            def run(output):
                (compiled / 'COMPILE_MANIFEST.json').write_text(json.dumps(manifest))
                return subprocess.run(['pwsh', '-NoProfile', '-File',
                    str(ROOT/'compat/package_qa_bundle.ps1'), '-Flavor', 'ACAD2023_NET48',
                    '-CompiledDirectory', str(compiled), '-OutputDirectory', str(output)],
                    capture_output=True, text=True)
            result = run(root/'valid')
            self.assertEqual(result.returncode, 0, result.stdout+result.stderr)
            bundle = root/'valid/IMSATVolume_ACAD2023_NET48.bundle'
            self.assertEqual(len(list((bundle/'Contents/Windows').glob('*.dll'))), 5)
            self.assertEqual(ET.parse(bundle/'PackageContents.xml').getroot().find(
                'Components/ComponentEntry').get('ModuleName'),
                './Contents/Windows/MiningVolume2023.dll')
            for index, name in enumerate(['../escape.dll', 'evil.dll.exe', 'folder/evil.dll']):
                manifest['files'] = files + [{'name': name, 'sha256': '0'*64}]
                output = root/('invalid'+str(index))
                result = run(output)
                self.assertNotEqual(result.returncode, 0)
                self.assertIn('Unsafe DLL path', result.stdout+result.stderr)
                self.assertFalse((output/'IMSATVolume_ACAD2023_NET48.bundle').exists())

    def test_missing_or_green_ci_never_becomes_host_acceptance(self):
        spec=importlib.util.spec_from_file_location('qa_data', ROOT/'compat/make_qa_data.py')
        module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module)
        report=module.build(ROOT,{'source_sha':'a'*40,'run_id':1,'jobs':[
            {'name':'build','status':'completed','conclusion':'success','id':2}]})
        rows=report['matrix']
        self.assertEqual({r['year'] for r in rows},set(range(2012,2027)))
        self.assertEqual(len(rows),17)
        self.assertEqual(next(r for r in rows if r['year']==2023)['compile'],'COMPILE_PASS')
        self.assertTrue(all(r['host']=='NOT_TESTED' and r['functional']=='BLOCKED' for r in rows))
        self.assertTrue(all(r['compile']=='NOT_TESTED' for r in rows if r['year']<2015))

    def test_desktop_ui_framework_is_not_inherited_by_math_core(self):
        root = ET.parse(PROPS).getroot()
        for tag in ('UseWindowsForms', 'UseWPF'):
            props = list(root.iter(tag))
            self.assertEqual(len(props), 1)
            self.assertIn("'$(MSBuildProjectName)' == 'MiningVolume.Plugin2023'", props[0].get('Condition'))
        for file in (ROOT / 'src/MiningVolume.Core').rglob('*.cs'):
            if any(x.startswith(('obj', 'bin')) for x in file.parts): continue
            text=file.read_text('utf-8-sig')
            self.assertNotIn('using Autodesk.', text)
            self.assertNotIn('using System.Windows', text)

    def test_net45_sources_do_not_use_net46_empty_array_api(self):
        for file in (ROOT / 'src').rglob('*.cs'):
            if any(x.startswith(('obj', 'bin')) for x in file.parts):
                continue
            self.assertNotIn('Array.Empty<', file.read_text('utf-8-sig'), str(file))

    def test_compile_candidate_preserves_dependencies_and_provenance(self):
        data = (ROOT / 'compat/build_compat.ps1').read_text('utf-8')
        for required in ('COMPILE_MANIFEST.json', 'SourceRevisionId=$sha',
                         'MiningVolume.Cdt.Native.dll', 'System.ValueTuple.dll',
                         'IMSATVolume.Cad.$Flavor.dll', 'Get-FileHash'):
            self.assertIn(required, data)

    def test_five_supported_compile_flavors_are_unique(self):
        root = ET.parse(PROPS).getroot()
        props = [
            (n.attrib.get("Condition", ""), n.text.strip() if n.text else "")
            for n in root.iter() if n.tag == "IMSATCadPackageVersion"
        ]
        flavors = ("ACAD2015_NET45", "ACAD2016_NET45",
                   "ACAD2017_NET46", "ACAD2018_NET46",
                   "ACAD2019_NET47", "ACAD2020_NET47",
                   "ACAD2021_NET48", "ACAD2022_NET48",
                   "ACAD2023_NET48", "ACAD2024_NET48",
                   "ACAD2025_NET8", "ACAD2025_NET10",
                   "ACAD2026_NET8", "ACAD2026_NET10")
        versions = ("20.0.1", "20.1.0", "21.0.0", "22.0.0",
                    "23.0.0", "23.1.0",
                    "24.0.0", "24.1.51000", "24.2.0", "24.3.0",
                    "25.0.1", "25.0.2", "25.1.0", "25.1.1")
        self.assertEqual(len(props), 14)
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
