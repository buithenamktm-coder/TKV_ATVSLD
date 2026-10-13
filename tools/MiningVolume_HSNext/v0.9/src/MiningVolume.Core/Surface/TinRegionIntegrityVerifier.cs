using System;
using System.Collections.Generic;
using System.Threading;
using MiningVolume.Core.Geometry;

namespace MiningVolume.Core.Surface
{
    /// <summary>
    /// Last safety gate for all 3DFACE geometry, not only source-point clipping.
    /// A user-selected TIN area MUST also bound file-backed triangles and
    /// zero-XY-area upright 3D faces. Never accept/expose a partial outside mesh.
    /// </summary>
    public static class TinRegionIntegrityVerifier
    {
        public static void Verify(TinSurface surface,
            IReadOnlyList<Vec2> boundary, double tolerance = 1e-6,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (surface == null) throw new ArgumentNullException(nameof(surface));
            if (boundary == null || boundary.Count < 3)
                throw new ArgumentException("Chưa chọn vùng giới hạn TIN hợp lệ.", nameof(boundary));
            double minX = double.PositiveInfinity, minY = double.PositiveInfinity;
            double maxX = double.NegativeInfinity, maxY = double.NegativeInfinity;
            foreach (var p in boundary)
            {
                minX = Math.Min(minX, p.X); maxX = Math.Max(maxX, p.X);
                minY = Math.Min(minY, p.Y); maxY = Math.Max(maxY, p.Y);
            }
            long checkedFaces = 0;
            foreach (var triangle in surface.Triangles)
            {
                if ((++checkedFaces & 4095) == 0)
                    cancellationToken.ThrowIfCancellationRequested();
                Check(triangle.A, "tam giác", checkedFaces);
                Check(triangle.B, "tam giác", checkedFaces);
                Check(triangle.C, "tam giác", checkedFaces);
                // Detect triangles bridging the exterior of a concave notch.
                Check(new Vec3((triangle.A.X + triangle.B.X + triangle.C.X) / 3,
                               (triangle.A.Y + triangle.B.Y + triangle.C.Y) / 3, 0),
                    "trọng tâm tam giác", checkedFaces);
            }
            foreach (var face in surface.VerticalFaces)
            {
                if ((++checkedFaces & 4095) == 0)
                    cancellationToken.ThrowIfCancellationRequested();
                Check(face.A, "mặt đứng", checkedFaces);
                Check(face.B, "mặt đứng", checkedFaces);
                Check(face.C, "mặt đứng", checkedFaces);
            }

            void Check(Vec3 p, string type, long number)
            {
                if (!double.IsNaN(p.X) && !double.IsNaN(p.Y) &&
                    p.X >= minX - tolerance && p.X <= maxX + tolerance &&
                    p.Y >= minY - tolerance && p.Y <= maxY + tolerance &&
                    SurfaceRegionClipper.ContainsInclusive(p.XY, boundary, tolerance))
                    return;
                throw new InvalidOperationException(
                    $"TIN chứa {type} số {number:n0} nằm ngoài vùng chọn " +
                    $"(X={p.X:R}; Y={p.Y:R}). Đã chặn ghi mô hình sai phạm vi xuống CAD.");
            }
        }
    }
}
