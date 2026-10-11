# Executable geometry regression gate

`python tests/core/run_core.py` compiles the actual Core and Surface C# source files
with .NET SDK 8 and executes the regression runner. It does not create another
add-in project or require AutoCAD. `--dotnet-root PATH` selects a specific SDK.
The Windows hosted build runs this gate before compiling the net48 add-in.

The 32 cases cover area, input-site coverage, constraint retention, crossings,
overlaps, point-on-edge splitting, duplicate XY policies, large coordinates,
deterministic repeat, collinear rejection, source immutability, interpolated
boundary clipping, cancellation, and tiled duplicate elevation choices.
Additional regressions cover full/partial overlapping breaklines sharing a
third crossing, both Z policies and both input orders, plus an L-shaped region
with support tiles entirely outside the polygon.
They are not proof of all requirements in TIN_AUDIT_20261010.md: shared tile
seams, multi-million-point completion and AutoCAD runtime remain separate gates.

Before the Z-policy fix, the original 19-case suite had two failing cases:
mixed-type duplicates silently chose the higher-priority entity, and a 1 cm
Z conflict silently chose the first value. Both now stop for an explicit policy.

## Private DXF fixture adapter

`dxf_fixture.py` requires ezdxf. It writes binary input fixtures for this runner,
without modifying the drawing. It reads WCS points, lines and polylines; samples
LW bulges at 0.5 m arc-length intervals; and counts unsupported entity types.
It does not establish AutoCAD API equivalence or treat SPLINE as a supported
source type. Do not commit private DXF files, fixtures or coordinate-bearing logs.

Example:

```
python tests/core/dxf_fixture.py drawing.dxf /private/fixtures --layers ht "- nam4" --boundary LO_TINHKL
python tests/core/run_core.py /private/fixtures/existing.bin /private/fixtures/boundary.bin 120
```

The timeout argument is a cooperative cancellation deadline. The runner prints
JSON lines for stages and failures. Exit 0 means completion, 1 regression failure,
2 fixture error/conflict/cancellation. A conflict report is not a computed volume.
Reported conflict counts are from the first failing tile, not a whole-file census.
The region stage uses the current production support rectangle plus padding;
it does not claim exact polygon clipping has been implemented.

## 2026-10-11: shared elevation nodes and empty region tiles

Six of eight overlap/Z/order cases failed before canonical node propagation;
all eight pass after every split edge endpoint is mapped to the chosen shared
node before deduplication. The concave-region regression also failed before
local support triangulation was separated from final core-region filtering.

For diagnostic fixtures, append `UseUpper` or `UseLower` after the timeout.
These are alternative test scenarios, not an approved production elevation choice.
A successful subset run does not establish whole-drawing completion.
