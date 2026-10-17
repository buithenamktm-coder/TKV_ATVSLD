from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]

def read(path):
    return (ROOT / path).read_text(encoding="utf-8")

def test_local_stations_are_not_shifted_twice_during_cad_output():
    sampler = read("src/MiningVolume.Core/Sections/TinSectionSampler.cs")
    renderer = read("src/MiningVolume.Plugin2023/Services/SectionCadRenderer.cs")
    assert "double s0 = t0 - globalMinT, s1 = t1 - globalMinT" in sampler
    assert "AddProfilePolylines(ms, tr, p, x0," in renderer
    assert "AddProfilePolylines(ms,tr,profile,x0,y0,baseZ," in renderer
    assert "x0 - p.Line.MinT" not in renderer
    assert "x0-profile.Line.MinT" not in renderer

def test_empty_profiles_are_rejected_before_cad_transaction():
    workflow = read("src/MiningVolume.Plugin2023/Services/SectionWorkflowService.cs")
    assert 'Không có đường địa hình chung giữa hai TIN' in workflow
    assert workflow.index('Không có đường địa hình chung giữa hai TIN') < workflow.index("using (doc.LockDocument())", workflow.index("public static void DrawProfiles("))
