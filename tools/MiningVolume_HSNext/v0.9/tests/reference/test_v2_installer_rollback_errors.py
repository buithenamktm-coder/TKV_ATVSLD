from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]


def test_install_failures_do_not_claim_unverified_rollback():
    source = (ROOT / 'installer/main.go').read_text(encoding='utf-8')
    install = source.split('func install() {', 1)[1].split('func validatePrebuiltBundle(', 1)[0]
    for stage in ('copyBundle', 'validateInstalledBundle', 'captureRegistration'):
        assert f'reportInstallRollback("{stage}", err, target, backup)' in install
    assert '_ = restoreBackup(' not in install
    assert 'restoreErr := rollbackBundle(target, backup, os.RemoveAll, restoreBackup)' in install
    helper = source.split('func reportInstallRollback(', 1)[1].split('func restoreBackup(', 1)[0]
    assert 'if restoreErr != nil' in helper
    assert 'writePersistentLog("install_error"' in helper
    assert 'Backup=%s' in helper
