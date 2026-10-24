"""Protect a field-visible V2 QA build identity.

GitHub QA produced distinct V2 DLL bytes, but the UI title, About dialog
and installer completion message previously gave no reliable version/build
evidence, making updates look identical to an old build.
"""
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]


def source(rel):
    return (ROOT / rel).read_text(encoding="utf-8-sig")


def test_installed_v2_build_is_visible_in_palette_and_about():
    identity = source("src/MiningVolume.Plugin2023/Services/RuntimeBuildIdentity.cs")
    palette = source("src/MiningVolume.Plugin2023/UI/MainPaletteControl.cs")
    entry = source("src/MiningVolume.Plugin2023/EntryPoint.cs")
    ribbon = source("src/MiningVolume.Plugin2023/RibbonBuilder.cs")
    assert "internal static string ShortTag()" in identity
    assert "SOURCE_SHA=" in identity
    assert 'sha.Substring(0, 8)' in identity
    assert "BUILD UNVERIFIED" in identity
    assert 'const string title = "NAMVECTA VOLUME";' in palette
    assert '"V2.0 QA · " + RuntimeBuildIdentity.ShortTag()' in palette
    assert "RuntimeBuildIdentity.ShortTag()" in palette
    assert "args.Graphics.DrawString(buildLabel" in palette
    assert '"IMSAT VOLUME", new Guid(' in entry
    assert "MVBUILDINFO" in entry
    assert "RuntimeBuildIdentity.ShortTag()" in ribbon
    assert "MVBUILDINFO" in ribbon


def test_setup_verifies_exact_installed_bundle_and_logs_identity():
    installer = source("installer/main.go")
    section = installer.split("func install() {", 1)[1].split(
        "func validatePrebuiltBundle(", 1
    )[0]
    assert "if err := validatePrebuiltBundle(source)" in section
    assert "if err := copyDir(source, target)" in section
    assert "if err := validatePrebuiltBundle(target)" in section
    assert section.index("copyDir(source, target)") < section.index(
        "validatePrebuiltBundle(target)"
    ) < section.index("registerUninstall(target, cad)")
    assert "sourceSHA := installedBundleSourceSHA(target)" in section
    assert '"\\r\\nSourceSHA="+sourceSHA' in section
    assert "shortSHA(sourceSHA)" in section
    assert "MVBUILDINFO" in section
    assert "func installedBundleSourceSHA(bundleDir string) string" in installer
    assert '"BUILD_IDENTITY.txt"' in installer
    assert "if len(sha)!=40" in installer
    assert "hex.DecodeString(sha)" in installer


def test_v2_qa_embeds_real_sha_not_hardcoded_one():
    pack = source("installer/build_v2_qa.ps1")
    workflow = (ROOT.parents[2] / ".github/workflows/build-miningvolume-autocad2023.yml").read_text(encoding="utf-8")
    assert "Source SHA:" in pack
    assert "main.version=2.0.0" in pack
    assert "SOURCE_SHA=$env:SOURCE_SHA" in workflow
    assert "V2_SHA256SUMS.txt" in workflow
