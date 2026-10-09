from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]

def read(rel):
    return (ROOT / rel).read_text(encoding="utf-8")

def test_ribbon_is_direct_gui_not_command_string_dispatch():
    s = read("src/MiningVolume.Plugin2023/RibbonBuilder.cs")
    assert "SendStringToExecute" not in s
    assert "EntryPoint.Open(AppPage.Data)" in s
    assert "EntryPoint.Open(AppPage.Model)" in s
    assert "EntryPoint.Open(AppPage.Section)" in s
    assert "EntryPoint.Open(AppPage.Volume)" in s
    assert "EntryPoint.Open(AppPage.Export)" in s

def test_user_pages_have_real_controls():
    data = read("src/MiningVolume.Plugin2023/UI/DataPage.cs")
    model = read("src/MiningVolume.Plugin2023/UI/ModelPage.cs")
    section = read("src/MiningVolume.Plugin2023/UI/SectionPage.cs")
    volume = read("src/MiningVolume.Plugin2023/UI/VolumePage.cs")
    export = read("src/MiningVolume.Plugin2023/UI/ExportPage.cs")
    assert "ComboBox" in data and "LoadModel" in data
    assert "DataGridView" in model and "BuildTinAsync" in model
    assert "NumericUpDown" in section and "Preview" in section and "BuildProfiles" in section
    assert "TÍNH / CẬP NHẬT KHỐI LƯỢNG" in volume
    assert "SaveFileDialog" in export and "XUẤT EXCEL" in export

def test_cli_commands_are_not_used_by_ribbon():
    ribbon = read("src/MiningVolume.Plugin2023/RibbonBuilder.cs")
    for cmd in ["MV_DATA", "MV_MODEL", "MV_SECTION", "MV_VOLUME", "MV_EXPORT", "MVOPEN"]:
        assert cmd not in ribbon


def test_bundle_is_startup_driven_not_command_registered():
    xml = read("bundle/MiningVolume2023.bundle/PackageContents.xml")
    assert 'LoadOnAutoCADStartup="True"' in xml
    assert "<Commands" not in xml
    assert 'Global="MV_DATA"' not in xml
    assert 'Global="MV_MODEL"' not in xml
    assert 'Global="MV_SECTION"' not in xml
    assert 'Global="MV_VOLUME"' not in xml
    assert 'Global="MV_EXPORT"' not in xml


def test_data_page_supports_layer_or_direct_cad_selection():
    data = read("src/MiningVolume.Plugin2023/UI/DataPage.cs")
    selection = read("src/MiningVolume.Plugin2023/Services/SelectionService.cs")
    workflow = read("src/MiningVolume.Plugin2023/Services/SurfaceWorkflowService.cs")
    reader = read("src/MiningVolume.Cad2023/CadLayerSurfaceReader.cs")
    assert '"Nạp layer"' in data
    assert '"Chọn trên CAD"' in data
    assert "SelectModel(ModelRole.Existing)" in data
    assert "SelectModel(ModelRole.Design)" in data
    assert "PickSurfaceEntities" in selection
    for token in ['"POINT"', '"LINE"', '"LWPOLYLINE"', '"POLYLINE"']:
        assert token in selection
    assert "LoadSelection(ModelRole role" in workflow
    assert "Read(Database db, IEnumerable<ObjectId> objectIds" in reader


def test_manual_selection_is_persisted_by_entity_handle():
    state = read("src/MiningVolume.Plugin2023/Services/ProjectState.cs")
    snapshot = read("src/MiningVolume.Plugin2023/Services/ProjectSnapshot.cs")
    persistence = read("src/MiningVolume.Plugin2023/Services/ProjectPersistenceService.cs")
    assert "SourceSelectionMode" in state
    assert "SelectedHandles" in state
    assert "SelectedHandles" in snapshot
    assert "LoadSelectedHandles" in persistence


def test_cad_reader_skips_legacy_invalid_entities_instead_of_aborting_layer_load():
    reader = read("src/MiningVolume.Cad2023/CadLayerSurfaceReader.cs")
    assert "TryConvertEntity" in reader
    assert "catch (Autodesk.AutoCAD.Runtime.Exception)" in reader
    assert "falling back to the segment chord" in reader
