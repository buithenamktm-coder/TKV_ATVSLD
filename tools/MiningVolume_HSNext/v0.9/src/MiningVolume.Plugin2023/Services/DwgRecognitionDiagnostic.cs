using System;
using System.Collections.Generic;
using System.Text;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Runtime;

namespace MiningVolume2023.Services
{
    /// <summary>
    /// Read-only discovery of native CAD evidence in an older drawing.
    /// Discovery is NOT adoption, source validation, section reconstruction,
    /// or permission to replace user-edited geometry.
    /// </summary>
    internal static class DwgRecognitionDiagnostic
    {
        internal static string Inspect(Database db, bool hasSavedProject)
        {
            if (db == null) throw new ArgumentNullException(nameof(db));
            int htTin = 0, tkTin = 0, htSections = 0, tkSections = 0;
            int unexpectedHt = 0, unexpectedTk = 0, planBlocks = 0;
            int namedStationLines = 0, editedSectionPaths = 0;
            using (var tr = db.TransactionManager.StartOpenCloseTransaction())
            {
                var layers = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                ObjectId htTinId = LayerId(layers, "MV_TIN_HIENTRANG");
                ObjectId tkTinId = LayerId(layers, "MV_TIN_THIETKE");
                ObjectId htSectionId = LayerId(layers, "MV_MC_HIENTRANG");
                ObjectId tkSectionId = LayerId(layers, "MV_MC_THIETKE");
                var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace],
                    OpenMode.ForRead);
                var faceClass = RXObject.GetClass(typeof(Face));
                var polyClass = RXObject.GetClass(typeof(Polyline));
                var blockClass = RXObject.GetClass(typeof(BlockReference));
                var lineClass = RXObject.GetClass(typeof(Line));
                foreach (ObjectId id in ms)
                {
                    if (id.ObjectClass.IsDerivedFrom(faceClass))
                    {
                        var face = tr.GetObject(id, OpenMode.ForRead) as Face;
                        if (face == null) continue;
                        if (face.LayerId == htTinId) htTin++;
                        else if (face.LayerId == tkTinId) tkTin++;
                        continue;
                    }
                    if (id.ObjectClass.IsDerivedFrom(polyClass))
                    {
                        var poly = tr.GetObject(id, OpenMode.ForRead) as Polyline;
                        if (poly == null) continue;
                        if (poly.LayerId == htSectionId) htSections++;
                        if (poly.LayerId == tkSectionId) tkSections++;
                        if (poly.LayerId == htSectionId || poly.LayerId == tkSectionId)
                        {
                            using (var data = poly.GetXDataForApplication(
                                SectionCadRenderer.EditRegApp))
                                if (data != null) editedSectionPaths++;
                        }
                        continue;
                    }
                    if (id.ObjectClass.IsDerivedFrom(blockClass))
                    {
                        var block = tr.GetObject(id, OpenMode.ForRead) as BlockReference;
                        if (block == null) continue;
                        var def = tr.GetObject(block.BlockTableRecord, OpenMode.ForRead)
                            as BlockTableRecord;
                        if (def == null ||
                            !string.Equals(def.Name, "NAMVECTA_MC_HE_TUYEN",
                                StringComparison.OrdinalIgnoreCase)) continue;
                        planBlocks++;
                        if (planBlocks == 1)
                            foreach (ObjectId child in def)
                                if (child.ObjectClass.IsDerivedFrom(lineClass))
                                    namedStationLines++;
                        continue;
                    }
                    // Foreign entities on dedicated output layers are evidence of
                    // a damaged TIN, not permission to overwrite or erase it.
                    if (id.ObjectClass.IsDerivedFrom(RXObject.GetClass(typeof(Entity))))
                    {
                        var entity = tr.GetObject(id, OpenMode.ForRead) as Entity;
                        if (entity == null) continue;
                        if (entity.LayerId == htTinId) unexpectedHt++;
                        if (entity.LayerId == tkTinId) unexpectedTk++;
                    }
                }
            }
            var report = new StringBuilder();
            report.AppendLine("NAMVECTA VOLUME — ĐỐI CHIẾU DỮ LIỆU DWG CŨ");
            report.AppendLine("Project Xrecord: " +
                (hasSavedProject ? "CÓ" : "KHÔNG CÓ / CHƯA TÌM THẤY"));
            report.AppendLine("TIN hiện trạng: " + htTin + " mặt 3DFACE");
            report.AppendLine("TIN thiết kế: " + tkTin + " mặt 3DFACE");
            report.AppendLine("Đối tượng khác trên layer TIN: HT " + unexpectedHt +
                ", TK " + unexpectedTk);
            report.AppendLine("Đường mặt cắt CAD: HT " + htSections +
                ", TK " + tkSections + " (có ID: " + editedSectionPaths + ")");
            report.AppendLine("Block hệ tuyến mặt cắt: " + planBlocks +
                " (đoạn LINE trong block: " + namedStationLines + ")");
            if (!hasSavedProject && (htTin > 0 || tkTin > 0 ||
                htSections > 0 || tkSections > 0 || planBlocks > 0))
                report.AppendLine("CẢNH BÁO: Có đối tượng CAD nhưng thiếu Project " +
                    "để xác minh nguồn TIN, thứ tự tuyến và thông số khối lượng. " +
                    "KHÔNG tự tạo lại hay ghi đè. Cần khôi phục Project/Xrecord " +
                    "hoặc đối chiếu thủ công nguồn DWG.");
            if (unexpectedHt > 0 || unexpectedTk > 0)
                report.AppendLine("CẢNH BÁO: layer TIN chứa đối tượng khác 3DFACE. " +
                    "Không chấp nhận làm TIN tính toán.");
            report.AppendLine("Đây là báo cáo NHẬN DIỆN CAD chỉ đọc, " +
                "không phải chứng nhận khối lượng. DWG không bị thay đổi.");
            return report.ToString();
        }

        private static ObjectId LayerId(LayerTable table, string name)
            => table.Has(name) ? table[name] : ObjectId.Null;
    }
}
