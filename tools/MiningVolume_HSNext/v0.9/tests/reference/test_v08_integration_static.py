from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[2]


def read(rel):
    return (ROOT / rel).read_text(encoding='utf-8')


def test_package_is_v010_autocad2023_only():
    p = read('bundle/MiningVolume2023.bundle/PackageContents.xml')
    assert 'AppVersion="0.10.2"' in p
    assert 'SeriesMin="R24.2"' in p and 'SeriesMax="R24.2"' in p
    assert 'LoadOnAutoCADStartup="True"' in p


def test_runtime_selftest_is_registered():
    entry = read('src/MiningVolume.Plugin2023/EntryPoint.cs')
    service = read('src/MiningVolume.Plugin2023/Services/SelfTestService.cs')
    assert '[CommandMethod("MVSELFTEST", CommandFlags.Session)]' in entry
    for token in ['CreateFlatTin', 'SectionSystemBuilder', 'TinSectionSampler', 'SectionVolumeCalculator', 'SimpleXlsxWriter.Write']:
        assert token in service


def test_selftest_has_known_numeric_ground_truth():
    s = read('src/MiningVolume.Plugin2023/Services/SelfTestService.cs')
    assert '100000.0' in s
    assert '1000.0' in s
    assert 'Spacing = 20.0' in s
    assert 'FromLevel = 0' in s and 'ToLevel = 10' in s and 'LevelStep = 5' in s
    assert 'Status=' in s and 'PASS' in s and 'FAIL' in s


def test_dev_build_script_is_autocad2023_only_and_no_external_tin_runtime():
    s = read('build_autocad2023.cmd')
    assert 'AutoCAD 2023' in s
    assert 'AcMgd.dll' in s
    assert 'UseAutoCADNuGet=false' in s
    assert 'NetTopologySuite.dll' in s  # only deletion guard is allowed
    assert 'del /q "%OUT%\\NetTopologySuite.dll"' in s
    assert 'nuget\\packages\\nettopologysuite' not in s.lower()


def test_all_package_commands_exist_as_command_methods():
    pkg = read('bundle/MiningVolume2023.bundle/PackageContents.xml')
    entry = read('src/MiningVolume.Plugin2023/EntryPoint.cs')
    cmds = re.findall(r'<Command Global="([^"]+)"', pkg)
    declared = set(re.findall(r'\[CommandMethod\("([^"]+)"', entry))
    missing = [c for c in cmds if c not in declared]
    assert not missing, missing


def test_v010_installer_is_prebuilt_only_not_end_user_compiler():
    s = read('installer/main.go')
    assert 'validatePrebuiltBundle' in s
    assert 'MiningVolume2023.dll' in s
    assert 'runBuild(' not in s
    assert 'msbuild' not in s.lower()
    assert 'exec.Command("msbuild' not in s
    assert 'runBuild(' not in s
    assert 'Build Tools' in s  # success message explicitly says it is not required


def test_release_pipeline_generates_payload_and_setup():
    w = read('.github/workflows/build-autocad2023-release.yml')
    assert 'UseAutoCADNuGet=true' in w
    assert 'AutoCAD.NET' not in w  # dependency is pinned in csproj, not shell-downloaded
    assert 'Compress-Archive -Path bundle -DestinationPath installer/payload.zip' in w
    assert 'MiningVolume_HSNext_AutoCAD2023_Setup_v0.10.2.exe' in w