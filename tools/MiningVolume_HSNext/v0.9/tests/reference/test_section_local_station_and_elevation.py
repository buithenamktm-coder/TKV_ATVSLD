"""Regression for real survey XY projected stations and wrong 0..+100 bands."""
from pathlib import Path
from math import floor, ceil

ROOT=Path(__file__).resolve().parents[2]
def read(path):
    return (ROOT/path).read_text(encoding="utf-8")


def test_projected_world_station_is_shifted_to_local_drawing_axes():
    cad=read("src/MiningVolume.Plugin2023/Services/SectionCadRenderer.cs")
    edit=read("src/MiningVolume.Plugin2023/Services/EditableSectionCadService.cs")
    assert "x0 - p.Line.MinT" in cad
    assert "x0-profile.Line.MinT" in cad
    assert "x0 + segment.S0" in cad
    assert "x0 + segment.S1" in cad
    # XData still stores the origin used by the line: decode restores
    # surveyed S (not the local distance!) after CAD Grip edits.
    assert "StampSection(a, section, \"HT\", run, x0" in cad
    assert "pt.X - x0" in edit
    cad_origin=472400.0
    world_station=2_399_000.0
    local_first=world_station
    local_last=world_station + 78.55
    shifted_origin=cad_origin-local_first
    assert abs((shifted_origin+local_first)-cad_origin)<1e-7
    assert abs((shifted_origin+local_last)-(cad_origin+78.55))<1e-7
    assert abs((cad_origin+world_station)-cad_origin)>1_000_000


def test_outside_tin_bands_are_corrected_or_explicitly_rejected():
    section=read("src/MiningVolume.Plugin2023/UI/SectionPage.cs")
    calc=read("src/MiningVolume.Core/Volumes/SectionVolumeCalculator.cs")
    assert "requestedHigh<minZ-1e-7 || requestedLow>maxZ+1e-7" in section
    assert "_fromLevel.Value=Clamp(_fromLevel,from);" in section
    assert "_toLevel.Value=Clamp(_toLevel,to);" in section
    assert "upper < minZ-options.Tolerance || lower > maxZ+options.Tolerance" in calc
    assert "không chứa cao độ TIN" in calc
    # Survey elevation 340.2..380.1 cannot possibly intersect levels 0..100.
    assert 100.0 < 340.2
    step=5.0
    assert floor(340.2/step)*step==340
    assert ceil(380.1/step)*step==385


def test_accepted_planar_section_profiles_not_globally_excluded_by_wall():
    calc=read("src/MiningVolume.Core/Volumes/SectionVolumeCalculator.cs")
    assert "VerticalConflictFootprint.TouchesStrip(" not in calc
    assert "VerticalConflictFootprint.TouchesSupportStrip(" not in calc
    assert "VerticalConflictFootprint.TouchesSection(" not in calc
    assert "message.Contains(\"thiếu giao TIN\")" in calc
    assert "skippedIntervals++" in calc
