from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]

def read(rel):
    return (ROOT / rel).read_text(encoding='utf-8')

def test_surface_builder_has_no_nettopologysuite_dependency():
    s = read('src/MiningVolume.Surface/ConformingTinBuilder.cs')
    p = read('src/MiningVolume.Surface/MiningVolume.Surface.csproj')
    assert 'NetTopologySuite' not in s
    assert 'NetTopologySuite' not in p
    for token in ['BowyerWatson', 'RecoverConstraint', 'TryFlip', 'BuildAdjacency', 'InCircumcircle']:
        assert token in s

def test_breakline_recovery_locks_constraints_and_rejects_crossing_locked_edges():
    s = read('src/MiningVolume.Surface/ConformingTinBuilder.cs')
    assert 'var locked = new HashSet<EdgeKey>()' in s
    assert 'locked.Add(new EdgeKey(a, b))' in s
    assert 'locked.Contains(e)' in s
    assert 'ProperIntersection' in s
    assert 'Không tạo cạnh mới cắt một breakline đã khóa' in s

def test_autocad2023_official_nuget_reference_is_pinned_for_ci():
    for rel in [
        'src/MiningVolume.Cad2023/MiningVolume.Cad2023.csproj',
        'src/MiningVolume.Plugin2023/MiningVolume.Plugin2023.csproj',
    ]:
        s = read(rel)
        assert 'AutoCAD.NET" Version="24.2.0"' in s
        assert 'UseAutoCADNuGet' in s
        assert 'ExcludeAssets="runtime"' in s
    for rel in [
        'src/MiningVolume.Core/MiningVolume.Core.csproj',
        'src/MiningVolume.Surface/MiningVolume.Surface.csproj',
        'src/MiningVolume.Cad2023/MiningVolume.Cad2023.csproj',
        'src/MiningVolume.Plugin2023/MiningVolume.Plugin2023.csproj',
    ]:
        assert 'Microsoft.NETFramework.ReferenceAssemblies.net48' in read(rel)

def test_release_policy_bans_autocad_and_old_tin_dlls_from_bundle():
    s = read('release/verify_release.ps1')
    for dll in ['AcMgd.dll','AcDbMgd.dll','AcCoreMgd.dll','AcWindows.dll','AdWindows.dll','NetTopologySuite.dll']:
        assert dll in s

def test_installer_rolls_back_on_install_or_registration_failure():
    s = read('installer/main.go')
    assert 'restoreBackup(backup, target)' in s
    assert 'registerUninstall(target)' in s
    assert 'os.RemoveAll(target)' in s
    assert 'runAutoCADSelfTest' not in s
