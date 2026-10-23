"""Portable rollback tests + Windows cross-compilation, NOT a Setup runtime test."""
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import zipfile

root = Path(__file__).resolve().parents[3]
go = sys.argv[1] if len(sys.argv) > 1 else 'go'
source = (root / 'installer/main.go').read_text(encoding='utf-8')
tests = (root / 'installer/main_test.go').read_text(encoding='utf-8')
env = dict(os.environ, GO111MODULE='off')
with tempfile.TemporaryDirectory(prefix='imsat-installer-') as tmp:
    work = Path(tmp)
    for path in (root / 'installer').glob('*.go'):
        shutil.copyfile(path, work / path.name)
    with zipfile.ZipFile(work / 'payload.zip', 'w') as z:
        z.writestr('compile-only.txt', 'Not an installable payload')
    subprocess.run([go, 'test', '-c', '-o', str(work / 'installer.test.exe')],
                   cwd=work, env=dict(env, GOOS='windows', GOARCH='amd64'), check=True)
    print('COMPILE_PASS: full Windows installer test binary (placeholder payload; NOT RUN)', flush=True)
    pure = work / 'portable'
    pure.mkdir()
    code = 'package main\nimport ("fmt"; "os")\n'
    for name, following in [('rollbackBundle', 'reportInstallRollback'), ('restoreBackup', 'exists')]:
        code += 'func ' + name + '(' + source.split('func ' + name + '(', 1)[1].split('func ' + following + '(', 1)[0]
    (pure / 'rollback.go').write_text(code, encoding='utf-8')
    (pure / 'rollback_test.go').write_text(
        'package main\nimport ("os"; "strings"; "testing"; "path/filepath")\nfunc '
        + 'TestRollbackBundleReportsCleanupAndRestoreFailures'
        + tests.split('func TestRollbackBundleReportsCleanupAndRestoreFailures', 1)[1], encoding='utf-8')
    subprocess.run([go, 'test', '-v', '-count=1', '.'], cwd=pure, env=env, check=True)
    print('Portable tests execute extracted production functions unchanged; Windows Setup runtime remains BLOCKED.', flush=True)
