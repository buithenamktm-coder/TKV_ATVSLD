from pathlib import Path

ROOT=Path(__file__).resolve().parents[2]

def text(rel): return (ROOT/rel).read_text(encoding='utf-8')

def test_data_page_loads_real_cad_reader():
    s=text('src/MiningVolume.Plugin2023/UI/DataPage.cs')
    assert 'SurfaceWorkflowService.LoadLayer' in s
    assert 'POINT' in s and 'LINE' in s and '3D POLYLINE' in s

def test_model_grid_is_virtualized_and_xyz_present():
    s=text('src/MiningVolume.Plugin2023/UI/ModelPage.cs')
    assert 'VirtualMode = true' in s
    for token in ['"X"','"Y"','"Z"','Loại điểm','Loại đối tượng','TẠO / CẬP NHẬT TIN']:
        assert token in s

def test_model_build_runs_core_off_ui_thread():
    s=text('src/MiningVolume.Plugin2023/UI/ModelPage.cs')
    assert 'Task.Run(() =>' in s
    assert 'SurfaceWorkflowService.BuildCoreDetailed(' in s
    assert 'SurfaceWorkflowService.DrawTin(role, build)' in s

def test_tin_renderer_replaces_faces_on_dedicated_layer():
    s=text('src/MiningVolume.Plugin2023/Services/TinCadRenderer.cs')
    assert 'new Face(' in s
    assert 'ent.Erase()' in s
    assert 'ltr.IsOff = !visible' in s

def test_autocad_2023_bundle_is_strictly_r242():
    s=text('bundle/MiningVolume2023.bundle/PackageContents.xml')
    assert 'SeriesMin="R24.2"' in s
    assert 'SeriesMax="R24.2"' in s
    assert 'LoadOnAutoCADStartup="True"' in s

def test_ui_ribbon_opens_specific_pages():
    s=text('src/MiningVolume.Plugin2023/RibbonBuilder.cs')
    for token in [
        'EntryPoint.Open(AppPage.Data)',
        'EntryPoint.Open(AppPage.Model)',
        'EntryPoint.Open(AppPage.Section)',
        'EntryPoint.Open(AppPage.Volume)',
        'EntryPoint.Open(AppPage.Export)'
    ]:
        assert token in s

def test_surface_preprocessor_uses_spatial_indexes():
    s=text('src/MiningVolume.Core/Surface/SurfaceInputPreparer.cs')
    assert 'PointGridIndex' in s
    assert 'SegmentGridIndex' in s
    assert 'foreach (int siteIndex in pointIndex.Query' in s