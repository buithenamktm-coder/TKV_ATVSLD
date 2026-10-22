# MC-017 — preserve user files inside retired bundles

Baseline: `1b9b4254fa0250b7fbf7d02cab6bddbd4cce257d`.

The previous uninstall recursively deleted the installed bundle and both known
transaction backups. Successful upgrades also deleted the old backup. These
folders can contain user-created files; restricting deletion to a named bundle
is not sufficient evidence of data preservation.

Retired bundles are now renamed intact into unique folders below
`%ProgramData%/MiningVolume2023/Recovery`, outside Autodesk ApplicationPlugins.
This applies to uninstall, successful-upgrade backup cleanup, and preservation
of the target before recovering an interrupted install. The uninstaller shows
the recovery directory. Repeated versions never reuse the same destination.
No recovery archive is automatically deleted. Retention consumes disk space.
A failed rename leaves the source in place and reports failure; removal is not
used as a fallback. Rollback refuses to overwrite an existing target.

Windows regression tests exercise nested DWG/report/license byte preservation,
repeated retirement, absent bundles, blocked recovery destinations and rollback
collisions. The uninstall source guard prohibits recursive deletion in that
function. Existing rollback, registry, payload and version tests remain enabled.
Local Python: 331 tests + 4 subtests PASS. Windows results belong to the Actions
run on the commit containing this report, not to source inspection.

Recovery: copy needed user documents from the Recovery folder into a project
folder. Do not move the entire archived bundle back into ApplicationPlugins:
that would re-enable an old DLL. Use the tested installer to restore software.
Recovery files inherit the existing installation's access controls; this is not
an encrypted backup or a replacement for an external project backup.

Real AutoCAD install/upgrade/uninstall, power-loss injection, file locks and
multi-CAD coexistence remain NOT TESTED. This change does not implement the
missing multi-version installer or legacy adapters. Official release BLOCKED.
