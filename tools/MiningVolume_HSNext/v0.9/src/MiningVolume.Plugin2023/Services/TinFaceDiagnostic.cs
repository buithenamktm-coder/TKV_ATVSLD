using System;
using System.Globalization;
using System.Text;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace MiningVolume2023.Services
{
    /// <summary>
    /// Forensic, read-only 3DFACE inspection on an existing DWG. A geometric
    /// diagnosis is never permission to change a user's saved CAD entities.
    /// The acceptance decision remains in TinCadRenderer.ReadStoredTin().
    /// </summary>
    internal static class TinFaceDiagnostic
    {
        internal static string Inspect(Database db, string hexHandle)
        {
            if (db == null) throw new ArgumentNullException(nameof(db));
            if (string.IsNullOrWhiteSpace(hexHandle))
                throw new ArgumentException("Chưa nhập Handle 3DFACE.", nameof(hexHandle));
            long value;
            if (!long.TryParse(hexHandle.Trim(), NumberStyles.HexNumber,
                CultureInfo.InvariantCulture, out value) || value <= 0)
                throw new ArgumentException("Handle phải là số hexadecimal dương.");

            var sb = new StringBuilder();
            sb.AppendLine("NAMVECTA VOLUME — CHẨN ĐOÁN 3DFACE CHỈ ĐỌC");
            sb.AppendLine("Handle: " + hexHandle.Trim().ToUpperInvariant());
            string layer;
            double minEdge, area;
            using (var tr = db.TransactionManager.StartOpenCloseTransaction())
            {
                ObjectId id;
                try { id = db.GetObjectId(false, new Handle(value), 0); }
                catch (System.Exception e)
                {
                    throw new InvalidOperationException("Không tìm thấy Handle " +
                        hexHandle + " trong DWG: " + e.Message, e);
                }
                var face = tr.GetObject(id, OpenMode.ForRead) as Face;
                if (face == null)
                    throw new InvalidOperationException("Handle " + hexHandle +
                        " không phải đối tượng 3DFACE.");

                layer = face.Layer;
                var p0 = face.GetVertexAt(0);
                var p1 = face.GetVertexAt(1);
                var p2 = face.GetVertexAt(2);
                var p3 = face.GetVertexAt(3);
                var points = new[] { p0, p1, p2, p3 };
                sb.AppendLine("Layer: " + layer);
                for (int i = 0; i < points.Length; i++)
                    sb.AppendLine("P" + (i + 1) + ": X=" + F(points[i].X) +
                        "; Y=" + F(points[i].Y) + "; Z=" + F(points[i].Z));
                double d01 = p0.DistanceTo(p1), d12 = p1.DistanceTo(p2),
                    d20 = p2.DistanceTo(p0);
                minEdge = Math.Min(d01, Math.Min(d12, d20));
                area = Math.Abs((p1.X - p0.X) * (p2.Y - p0.Y) -
                    (p1.Y - p0.Y) * (p2.X - p0.X)) * 0.5;
                sb.AppendLine("Cạnh 3D P1-P2: " + F(d01) + " m");
                sb.AppendLine("Cạnh 3D P2-P3: " + F(d12) + " m");
                sb.AppendLine("Cạnh 3D P3-P1: " + F(d20) + " m");
                sb.AppendLine("P3-P4: " + F(p2.DistanceTo(p3)) + " m");
                sb.AppendLine("Diện tích chiếu XY: " + F(area) + " m2");
                sb.AppendLine("Cạnh ngắn nhất: " + F(minEdge) + " m");
                sb.AppendLine(minEdge <= 1e-9
                    ? "Cảnh báo: có hai đỉnh gần trùng 3D ở ngưỡng kiểm tra 1e-9 m."
                    : "Không có cạnh 3D <= 1e-9 m.");
                if (area <= 1e-12)
                    sb.AppendLine("Cảnh báo: hình chiếu XY suy biến hoặc gần đứng. " +
                        "Không được coi đây là mặt phủ XY.");
                if (p2.DistanceTo(p3) > 1e-7)
                    sb.AppendLine("Cảnh báo: đỉnh P4 khác P3; 3DFACE có thể là tứ giác.");
                // A neighborhood count is evidence of neighboring CAD faces,
                // NOT a topological coverage guarantee.
                int near = 0, sharedEdges = 0;
                var example = new StringBuilder();
                var blocks = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                var ms = (BlockTableRecord)tr.GetObject(
                    blocks[BlockTableRecord.ModelSpace], OpenMode.ForRead);
                foreach (ObjectId candidateId in ms)
                {
                    if (candidateId == id ||
                        !candidateId.ObjectClass.IsDerivedFrom(
                            Autodesk.AutoCAD.Runtime.RXObject.GetClass(typeof(Face))))
                        continue;
                    var other = tr.GetObject(candidateId, OpenMode.ForRead) as Face;
                    if (other == null || other.LayerId != face.LayerId) continue;
                    var q = new[] { other.GetVertexAt(0), other.GetVertexAt(1),
                        other.GetVertexAt(2) };
                    int shared = 0;
                    foreach (var p in new[] { p0, p1, p2 })
                    {
                        bool found = false;
                        foreach (var x in q)
                            if (p.DistanceTo(x) <= 1e-9) { found = true; break; }
                        if (found) shared++;
                    }
                    if (shared == 0) continue;
                    near++;
                    if (shared >= 2) sharedEdges++;
                    if (near <= 10)
                        example.Append(other.Handle.ToString()).Append(" (")
                            .Append(shared).Append(" đỉnh); ");
                }
                sb.AppendLine("Mặt lân cận cùng layer có đỉnh gần trùng: " + near +
                    "; có từ 2 đỉnh chung: " + sharedEdges);
                if (example.Length > 0)
                    sb.AppendLine("Handle lân cận (tối đa 10): " + example);
            }

            // Use precisely the production recovery gate rather than an
            // independent permissive tolerance that might certify a bad face.
            try
            {
                var tin = TinCadRenderer.ReadStoredTin(db, layer, "CHẨN ĐOÁN");
                if (tin == null)
                    sb.AppendLine("TIN: không có mô hình 3DFACE hợp lệ để kiểm chứng.");
                else
                {
                    sb.AppendLine("Kiểm tra TIN cả layer: đọc được " +
                        tin.Triangles.Count + " tam giác phủ XY; " +
                        tin.VerticalFaces.Count + " mặt đứng; " +
                        tin.VerifiedRedundantCadFaces + " mặt suy biến có cạnh " +
                        "được chứng minh dư thừa.");
                    sb.AppendLine("Giới hạn: kết quả đọc layer KHÔNG chứng minh " +
                        "kín vùng phủ, hợp lệ toàn bộ topology hoặc khối lượng chuẩn.");
                }
            }
            catch (System.Exception e)
            {
                sb.AppendLine("TIN KHÔNG ĐỦ ĐIỀU KIỆN PHỤC HỒI/TÍNH: " + e.Message);
                sb.AppendLine("Không tự bỏ qua hoặc xóa 3DFACE theo kết quả chẩn đoán.");
            }
            sb.AppendLine("Bản vẽ được đọc nguyên trạng, không lưu/ghi/xóa đối tượng.");
            return sb.ToString();
        }

        private static string F(double x)
            => x.ToString("R", CultureInfo.InvariantCulture);
    }
}
