from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]

def read(p):
    return (ROOT / p).read_text(encoding="utf-8")


def test_many_seams_are_stably_named_and_paired_before_activation():
    s = read("src/MiningVolume.Plugin2023/Services/MineralBody.cs")
    state = read("src/MiningVolume.Plugin2023/Services/ProjectState.cs")
    assert 'Guid.NewGuid().ToString("N")' in s
    assert "public void Rename(" in s
    assert "ExistingOutcropHandles" in s
    assert "DesignOutcropHandles" in s
    assert "HasPairedOutcrops" in s
    assert "target.Contains(value)" in s
    assert "Thiếu một bình đồ – chỉ tính đào đắp" in s
    assert "MineralBodies.Any(b => b.HasPairedOutcrops)" in state


def test_miners_are_saved_in_current_dwg_without_format_migration():
    snap = read("src/MiningVolume.Plugin2023/Services/ProjectSnapshot.cs")
    persistence = read("src/MiningVolume.Plugin2023/Services/ProjectPersistenceService.cs")
    assert "FormatVersion { get; set; } = 2" in snap
    assert "MineralBodySnapshot" in snap
    assert "DensityTonPerCubicMetre" in snap
    assert "ExistingOutcropHandles" in snap
    assert "DesignOutcropHandles" in snap
    assert "MineralBodies.Add(item)" in persistence
    assert "state.MineralBodies.Add(restored)" in persistence
    assert "seenIds.Add(id)" in persistence


def test_polyline_outcrops_selectable_for_existing_and_design():
    selection = read("src/MiningVolume.Plugin2023/Services/SelectionService.cs")
    page = read("src/MiningVolume.Plugin2023/UI/MineralPage.cs")
    assert "PickMineralOutcropHandles" in selection
    assert '"LINE"' in selection
    assert '"LWPOLYLINE"' in selection
    assert '"POLYLINE"' in selection
    assert "body.AddTraces(role, handles)" in page
    assert "body.ClearTraces(role)" in page
    assert "Thể trọng (t/m³)" in page


def test_exposes_mineral_page_without_making_earthworks_dependent_on_minerals():
    palette = read("src/MiningVolume.Plugin2023/UI/MainPaletteControl.cs")
    entry = read("src/MiningVolume.Plugin2023/EntryPoint.cs")
    assert "AppPage.Mineral" in palette
    assert "new MineralPage()" in palette
    assert 'CommandMethod("MV_MINERAL"' in entry
    pure = read("src/MiningVolume.Plugin2023/Services/VolumeWorkflowService.cs")
    assert "HasMineralInput" not in pure
