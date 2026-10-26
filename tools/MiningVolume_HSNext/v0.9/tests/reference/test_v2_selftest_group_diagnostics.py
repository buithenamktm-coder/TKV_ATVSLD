"""Source contracts only; native CAD fixtures require AutoCAD 2023."""
from pathlib import Path
ROOT = Path(__file__).resolve().parents[2]

def test_cad_selftest_collects_independent_group_errors_and_retains_inner_trace():
    source = (ROOT / "src/MiningVolume.Plugin2023/Services/SectionCadRegression.cs").read_text(encoding="utf-8")
    run = source.split("internal static void Run(", 1)[1].split("private static void RunGroup", 1)[0]
    assert run.count("RunGroup(") == 8
    assert 'RunGroup("Stored CAD TIN degenerate-face safety", VerifyStoredCadTinDegenerateFaceSafety, errors, reportPassed)' in run
    assert "TinCadRenderer.ReadStoredTin(db, \"QA_TIN_REDUNDANT\"" in source
    assert "TinCadRenderer.ReadStoredTin(db, \"QA_TIN_UNSAFE\"" in source
    assert "throw new AggregateException" in run
    group = source.split("private static void RunGroup", 1)[1].split("private static void VerifySectionLayoutAndCancellation", 1)[0]
    assert 'errors.Add(new InvalidOperationException("CAD group: " + name, ex))' in group
    assert "return;" in group.split("catch (Exception ex)", 1)[1].split("reportPassed", 1)[0]
    service = (ROOT / "src/MiningVolume.Plugin2023/Services/SelfTestService.cs").read_text(encoding="utf-8")
    assert 'SectionCadRegression.Run(name => Check(r, true, "CAD: " + name))' in service
    assert "r.Errors.Add(ex.ToString())" in service
    assert "r.Passed = r.Errors.Count == 0" in service

def test_legacy_dwg_restores_only_own_tin_without_reconstructing_unsaved_sections():
    entry = (ROOT / "src/MiningVolume.Plugin2023/EntryPoint.cs").read_text(encoding="utf-8")
    reader = (ROOT / "src/MiningVolume.Plugin2023/Services/TinCadRenderer.cs").read_text(encoding="utf-8")
    assert "TryRecognizeLegacyCadTin(doc);" in entry
    legacy = entry.split("private static void TryRecognizeLegacyCadTin(", 1)[1].split(
        "private static string CurrentFingerprint()", 1)[0]
    assert "SurfaceWorkflowService.TryRestoreTinFromCad(role)" in legacy
    assert "EditableSectionCadService.HasOfficialSectionLayers(doc.Database)" in legacy
    assert "không tự tái tạo tuyến" in legacy
    assert "var unproven = new HashSet<ExactStoredEdge>();" in reader
    assert "HasExact3DEdge" not in reader
    assert "zeroEdges.Count > 32" not in reader
    assert "Không chứng minh được việc bỏ qua là an toàn" in reader
    assert "verifiedRedundantCadFaces: zeroEdges.Count" in reader
