"""Saved DWG recovery: unrelated metadata warnings cannot veto intact TINs.

Both HT/TK source warnings still fail closed for derived volume restoration.
"""
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
P = (ROOT / "src/MiningVolume.Plugin2023/Services/ProjectPersistenceService.cs").read_text(
    encoding="utf-8"
)


def test_only_new_source_warnings_block_recomputed_profiles():
    restore = P.split("private static void Restore(ProjectSnapshot snap,", 1)[1].split(
        "private static void RestoreModel(", 1
    )[0]
    assert "int warningsBeforeSourceRestore = warnings.Count;" in restore
    assert restore.index("int warningsBeforeSourceRestore") < restore.index(
        "RestoreModel(ModelRole.Existing, snap.Existing, warnings)")
    assert restore.index("RestoreModel(ModelRole.Design, snap.Design, warnings);") < restore.index(
        "if (warnings.Count > warningsBeforeSourceRestore)")
    assert "rebuildDerived = false;" in restore
    assert "if (rebuildDerived)" in restore
    # Prior warnings from duplicate mineral body IDs / scale presets must
    # not abort the TIN restoration path.
    assert "if(warnings.Count>0)" not in restore


def test_changed_source_fingerprint_still_forbids_unverified_cad_recovery():
    model = P.split("private static void RestoreModel(", 1)[1].split(
        "private static void RebuildSavedTins(", 1
    )[0]
    assert "SourceFingerprint.ComputeModel(session.Source)" in model
    assert "session.CadRecoveryBlocked = true;" in model
    assert "warnings.Add(session.Name +" in model
