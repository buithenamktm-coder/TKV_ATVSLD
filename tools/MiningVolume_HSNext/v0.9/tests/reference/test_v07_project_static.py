from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
PLUG = ROOT / 'src' / 'MiningVolume.Plugin2023'


def read(rel):
    return (ROOT / rel).read_text(encoding='utf-8')


def test_project_page_is_real_palette_page():
    main = read('src/MiningVolume.Plugin2023/UI/MainPaletteControl.cs')
    page = read('src/MiningVolume.Plugin2023/UI/ProjectPage.cs')
    assert 'AppPage { Project, Data, Model, Section, Volume, Grid, Mineral, Export, License, QuickSection }' in main
    assert 'AddNav(nav, AppPage.Model, "1", "Dữ liệu")' in main
    assert '[AppPage.Project] = new ProjectPage()' in main
    assert 'Lưu dự án vào DWG' in page
    assert 'Nạp lại dự án' in page
    assert 'Xóa dự án đã lưu' in page


def test_project_stored_inside_dwg_nod_xrecord():
    s = read('src/MiningVolume.Plugin2023/Services/ProjectPersistenceService.cs')
    assert 'NamedObjectsDictionaryId' in s
    assert 'DBDictionary' in s
    assert 'Xrecord' in s
    assert 'MININGVOLUME_HSNEXT' in s
    assert 'PROJECT_V1' in s
    assert 'DxfCode.Text' in s


def test_snapshot_compressed_and_versioned():
    s = read('src/MiningVolume.Plugin2023/Services/ProjectSnapshot.cs')
    assert 'FormatVersion' in s
    assert 'DataContractJsonSerializer' in s
    assert 'GZipStream' in s
    assert 'Convert.ToBase64String' in s
    assert 'Convert.FromBase64String' in s


def test_snapshot_covers_required_project_parameters():
    s = read('src/MiningVolume.Plugin2023/Services/ProjectSnapshot.cs')
    for token in [
        'Existing', 'Design', 'BoundaryHandle', 'HasDirection', 'SectionSpacing',
        'LevelStep', 'FromLevel', 'ToLevel', 'HorizontalScale', 'VerticalScale',
        'DeveloperName', 'DeveloperContact', 'SectionLines', 'HadProfiles', 'HadVolumeResult'
    ]:
        assert token in s


def test_model_edits_are_delta_not_duplicate_full_xyz():
    s = read('src/MiningVolume.Plugin2023/Services/ProjectPersistenceService.cs')
    snap = read('src/MiningVolume.Plugin2023/Services/ProjectSnapshot.cs')
    assert 'EntityEditSnapshot' in snap and 'VertexEditSnapshot' in snap
    assert 'Handle' in snap and 'HasZOverride' in snap and 'Enabled' in snap
    assert 'Math.Abs(v.Position.Z - v.Original.Z)' in s
    assert 'if (!edit.Enabled || edit.Vertices.Count > 0) s.Edits.Add(edit);' in s


def test_restore_reloads_source_then_reapplies_user_edits():
    s = read('src/MiningVolume.Plugin2023/Services/ProjectPersistenceService.cs')
    assert 'SurfaceWorkflowService.LoadLayer(role, snap.Layer, types, clearPreviousCadTin: false)' in s
    assert 'entity.SetEnabled(edit.Enabled)' in s
    assert 'entity.Vertices[ve.Index].SetEnabled(ve.Enabled)' in s
    assert 'entity.Vertices[ve.Index].OverrideZ(ve.Z)' in s


def test_restore_reuses_stored_tin_profiles_and_volume():
    s = read('src/MiningVolume.Plugin2023/Services/ProjectPersistenceService.cs')
    assert 'SurfaceWorkflowService.TryRestoreTinFromCad(role)' in s
    assert 'SurfaceWorkflowService.DrawTin(role, build)' not in s
    assert 'SectionWorkflowService.BuildProfiles()' in s
    assert 'VolumeWorkflowService.CalculateCore' in s


def test_section_manual_lines_are_persisted_exactly():
    s = read('src/MiningVolume.Plugin2023/Services/ProjectPersistenceService.cs')
    assert 'SectionLineSnapshot' in s
    assert 'SectionSpanSnapshot' in s
    assert 'new SectionLine(x.Index, x.Name, x.Offset' in s
    assert 'new SectionSpan(y.StartT, y.EndT)' in s


def test_allowed_source_types_are_persisted():
    state = read('src/MiningVolume.Plugin2023/Services/ProjectState.cs')
    surface = read('src/MiningVolume.Plugin2023/Services/SurfaceWorkflowService.cs')
    persistence = read('src/MiningVolume.Plugin2023/Services/ProjectPersistenceService.cs')
    assert 'HashSet<SourceEntityType> AllowedTypes' in state
    assert 'session.AllowedTypes.Clear()' in surface
    assert 'session.AllowedTypes.Add(t)' in surface
    assert 'AllowedTypes' in persistence


def test_project_autoload_on_palette_open_once_per_drawing():
    s = read('src/MiningVolume.Plugin2023/EntryPoint.cs')
    assert 'EnsureCurrentDrawingProjectLoaded();' in s
    assert '_loadedDrawingFingerprint' in s
    assert 'ProjectPersistenceService.HasSavedProject()' in s
    assert 'ProjectPersistenceService.LoadCurrentProject(true)' in s


def test_ribbon_exposes_only_toggle_and_software_information():
    s = read('src/MiningVolume.Plugin2023/RibbonBuilder.cs')
    assert 'AddPanel(tab, "NAMVECTA VOLUME"' in s
    assert 'Btn("Ẩn / hiện phần mềm", RibbonAction.TogglePalette)' in s
    assert 'Btn("Thông tin phần mềm", RibbonAction.About)' in s
    assert 'EntryPoint.Open(AppPage.License)' in s
    assert 'Btn("Lưu dự án", RibbonAction.SaveProject)' not in s


def test_project_scale_is_persisted_but_report_identity_is_fixed():
    state = read('src/MiningVolume.Plugin2023/Services/ProjectState.cs')
    section = read('src/MiningVolume.Plugin2023/UI/SectionPage.cs')
    export = read('src/MiningVolume.Plugin2023/UI/ExportPage.cs')
    service = read('src/MiningVolume.Plugin2023/Services/ExportWorkflowService.cs')
    for token in ['HorizontalScale', 'VerticalScale']:
        assert token in state
    assert 'st.HorizontalScale = (double)_hScale.Value;' in section
    assert 'st.VerticalScale = (double)_vScale.Value;' in section
    assert '_hScale.Enabled = custom;' in section
    assert '_vScale.Enabled = custom;' in section
    assert 'ExportOptions.FixedDeveloperName' in export
    assert 'ExportOptions.FixedDeveloperContact' in export
    assert 'FixedDeveloperName = "Bùi Thế Nam"' in service
    assert 'FixedDeveloperContact = "ĐT/Zalo: 0967280686"' in service


def test_runtime_serialization_references_present():
    csproj = read('src/MiningVolume.Plugin2023/MiningVolume.Plugin2023.csproj')
    assert 'System.Runtime.Serialization' in csproj
    assert 'System.IO.Compression' in csproj