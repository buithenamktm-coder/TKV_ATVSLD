using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using MiningVolume.Core.Sections;

namespace MiningVolume2023.Services
{
    /// <summary>
    /// Reads the actual user-edited CAD polylines, not stale computed TIN
    /// projections. All candidate paths are validated before caller commits.
    /// CAD source geometry is never modified by this reader.
    /// </summary>
    internal static class EditableSectionCadService
    {
        private sealed class Tagged
        {
            public string Section;
            public string Role;
            public EditedSectionPath Path;
        }

        internal static bool HasEditableProfiles(Database db)
        {
            using (var tr = db.TransactionManager.StartOpenCloseTransaction())
            {
                var ms = GetModelSpace(db, tr);
                foreach (ObjectId id in ms)
                {
                    var poly = tr.GetObject(id, OpenMode.ForRead) as Polyline;
                    if (poly == null) continue;
                    using (var data = poly.GetXDataForApplication(SectionCadRenderer.EditRegApp))
                        if (data != null) return true;
                }
                return false;
            }
        }

        internal static IReadOnlyList<SectionProfile> ReadEditedProfiles(
            Database db, IReadOnlyList<SectionProfile> originals)
        {
            if (originals == null || originals.Count == 0)
                throw new InvalidOperationException("Chưa có mặt cắt được tính từ TIN.");
            var grouped = new Dictionary<string, List<Tagged>>(StringComparer.OrdinalIgnoreCase);
            using (var tr = db.TransactionManager.StartOpenCloseTransaction())
            {
                var ms = GetModelSpace(db, tr);
                foreach (ObjectId id in ms)
                {
                    var poly = tr.GetObject(id, OpenMode.ForRead) as Polyline;
                    if (poly == null) continue;
                    using (var data = poly.GetXDataForApplication(SectionCadRenderer.EditRegApp))
                    {
                        if (data == null) continue;
                        var record = Decode(poly, data);
                        if (!grouped.TryGetValue(record.Section, out var list))
                            grouped.Add(record.Section, list = new List<Tagged>());
                        list.Add(record);
                    }
                }
            }
            var result = new List<SectionProfile>(originals.Count);
            foreach (var orig in originals)
            {
                if (orig == null || !orig.HasData) { result.Add(orig); continue; }
                if (!grouped.TryGetValue(orig.Line.Name, out var paths))
                    throw new InvalidOperationException("Không tìm thấy đường CAD hiệu chỉnh của " +
                        orig.Line.Name + ". Hãy xuất lại mặt cắt hoặc khôi phục đường bị xóa.");
                var ht = paths.Where(x => x.Role == "HT").Select(x => x.Path).ToList();
                var tk = paths.Where(x => x.Role == "TK").Select(x => x.Path).ToList();
                var updated = EditedSectionProfileBuilder.Rebuild(orig, ht, tk);
                result.Add(updated);
            }
            return result;
        }

        internal static void UpdateAreaLabels(Database db, IReadOnlyList<SectionProfile> profiles)
        {
            var areas = profiles.Where(x => x != null && x.HasData)
                .ToDictionary(x => x.Line.Name, StringComparer.OrdinalIgnoreCase);
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var ms = GetModelSpace(db, tr);
                var labels = new List<DBText>();
                var titles = new List<DBText>();
                foreach (ObjectId id in ms)
                {
                    var text = tr.GetObject(id, OpenMode.ForRead) as DBText;
                    if (text == null || text.Layer != "MV_MC_TEXT") continue;
                    if (text.TextString.StartsWith("F đào =", StringComparison.Ordinal))
                        labels.Add(text);
                    using (var data = text.GetXDataForApplication(SectionCadRenderer.FrameRegApp))
                        if (data != null) titles.Add(text);
                }
                foreach (var title in titles)
                {
                    using (var data = title.GetXDataForApplication(SectionCadRenderer.FrameRegApp))
                    {
                        var values = data?.AsArray();
                        if (values == null || values.Length < 2) continue;
                        var section = Convert.ToString(values[1].Value);
                        if (!areas.TryGetValue(section, out var p)) continue;
                        var nearest = labels.FirstOrDefault(x =>
                            Math.Abs(x.Position.X - title.Position.X) < 0.01 &&
                            Math.Abs(x.Position.Y - (title.Position.Y - 4.5)) < 0.01);
                        if (nearest == null)
                            throw new InvalidOperationException("Thiếu nhãn diện tích của " + section +
                                "; dữ liệu khối lượng chưa được ghi đè.");
                        nearest.UpgradeOpen();
                        nearest.TextString = "F đào = " + p.CutArea.ToString("0.00") +
                            " m²   F đắp = " + p.FillArea.ToString("0.00") + " m²";
                    }
                }
                tr.Commit();
            }
        }

        private static Tagged Decode(Polyline poly, ResultBuffer buf)
        {
            var v = buf.AsArray();
            if (v.Length != 8 ||
                v[0].TypeCode != (int)DxfCode.ExtendedDataRegAppName ||
                v[1].TypeCode != (int)DxfCode.ExtendedDataAsciiString ||
                v[2].TypeCode != (int)DxfCode.ExtendedDataAsciiString)
                throw new InvalidOperationException("Metadata đường mặt cắt bị hỏng: " + poly.Handle);
            string section = Convert.ToString(v[1].Value);
            string role = Convert.ToString(v[2].Value);
            int run = Convert.ToInt32(v[3].Value);
            double x0 = Convert.ToDouble(v[4].Value);
            double y0 = Convert.ToDouble(v[5].Value);
            double baseZ = Convert.ToDouble(v[6].Value);
            double verticalExaggeration = Convert.ToDouble(v[7].Value);
            string expectedLayer = role == "HT" ? "MV_MC_HIENTRANG" : "MV_MC_THIETKE";
            if (string.IsNullOrWhiteSpace(section) || (role != "HT" && role != "TK") ||
                !string.Equals(poly.Layer, expectedLayer, StringComparison.OrdinalIgnoreCase) ||
                run < 0 || double.IsNaN(verticalExaggeration) ||
                double.IsInfinity(verticalExaggeration) || verticalExaggeration <= 0 ||
                poly.Closed || poly.NumberOfVertices < 2)
                throw new InvalidOperationException("Đường mặt cắt không đúng loại/layer hoặc bị khép kín: " + poly.Handle);
            var vertices = new List<EditedSectionVertex>(poly.NumberOfVertices);
            for (int i = 0; i < poly.NumberOfVertices; i++)
            {
                if (Math.Abs(poly.GetBulgeAt(i)) > 1e-10)
                    throw new InvalidOperationException("Mặt cắt đang có cung tròn. Hãy chuyển thành các đoạn thẳng: " + poly.Handle);
                Point3d pt = poly.GetPoint3dAt(i);
                if (Math.Abs(pt.Z) > 1e-6)
                    throw new InvalidOperationException("Đường mặt cắt đã ra khỏi mặt phẳng XY: " + poly.Handle);
                vertices.Add(new EditedSectionVertex(pt.X - x0,
                    baseZ + (pt.Y - y0) / verticalExaggeration));
            }
            return new Tagged { Section = section, Role = role,
                Path = new EditedSectionPath(run, vertices) };
        }

        private static BlockTableRecord GetModelSpace(Database db, Transaction tr)
        {
            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            return (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
        }
    }
}
