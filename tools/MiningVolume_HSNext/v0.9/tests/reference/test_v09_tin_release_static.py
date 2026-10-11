from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]

def read(rel):
    return (ROOT / rel).read_text(encoding='utf-8')


def test_surface_builder_has_no_nettopologysuite_dependency():
    s = read('src/MiningVolume.Surface/ConformingTinBuilder.cs')
    p = read('src/MiningVolume.Surface/MiningVolume.Surface.csproj')
    assert 'NetTopologySuite' not in s
    assert 'NetTopologySuite' not in p
    for token in ['mv_cdt_build', 'mv_cdt_free', 'CancellationToken', 'Dictionary<XY, int>']:
        assert token in s


def test_native_constraints_and_sites_are_verified_before_export():
    s = read('src/MiningVolume.Surface/Native/bridge.cpp')
    for token in ['cdt.fixedEdges', 'meshEdges.count(edge)', 'pieceToOriginals',
                  'splitOriginals.count(edge)', 'CDT omitted an input site',
                  'IntersectingConstraintEdges::NotAllowed', 'observer.isAbortCalculation()']:
        assert token in s
    managed = read('src/MiningVolume.Surface/ConformingTinBuilder.cs')
    assert 'new LegacyConformingTinBuilder' not in managed
    assert 'RETAIN_SMALL_TRIANGLES' in managed
    assert 'finally { if (mesh != IntPtr.Zero) mv_cdt_free(mesh); }' in managed


def test_native_backend_is_pinned_licensed_and_packaged():
    assert '3765e08ec4bbef0c6b721204d7c0e45d279084e9' in read('src/MiningVolume.Surface/Native/NOTICE.txt')
    assert 'Mozilla Public License' in read('src/MiningVolume.Surface/Native/LICENSE-CDT.txt')
    assert 'BuildNativeCdt' in read('src/MiningVolume.Surface/MiningVolume.Surface.csproj')
    for path in ['release/verify_release.ps1', 'installer/main.go']:
        for name in ['MiningVolume.Cdt.Native.dll','LICENSE-CDT.txt','NOTICE.txt']:
            assert name in read(path)


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


def test_installer_does_not_run_autocad_selftest_on_end_user_install():
    s = read('installer/main.go')
    install_block = s[s.index('func install()'):s.index('func validatePrebuiltBundle')]
    assert 'runAutoCADSelfTest(cad)' not in install_block
    assert 'Phần mềm đang được tiếp tục cải tiến và hoàn thiện.' in install_block
    assert 'Bùi Thế Nam' in install_block and '0967280686' in install_block
    assert 'restoreBackup(backup, target)' in s