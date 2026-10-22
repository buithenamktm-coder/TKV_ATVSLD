using System;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using MiningVolume.Core.Volumes;

namespace MiningVolume2023.Services
{
    /// <summary>
    /// Một Block cho mỗi lần xuất, chứa ba TABLE gốc có thể chỉnh qua BEDIT.
    /// Không tự xóa bảng cũ: kết quả do người dùng có thể đã hiệu chỉnh/bàn giao.
    /// </summary>
    internal static class CadVolumeTableService
    {
        internal const string OutputLayer = "MV_KL_BANG";
        internal const string BlockPrefix = "IMSAT_KL_";
        private const double RowHeight = 8.0;
        private const double TableGap = 28.0;

        public static void Insert(Database database, VolumeResult result, Point3d insertion,
            Action pollCancellation = null, Action<int> reportProgress = null)
        {
            if (database == null) throw new ArgumentNullException(nameof(database));
            if (result == null) throw new InvalidOperationException("Chưa có kết quả khối lượng để xuất CAD.");
            if (result.Intervals == null || result.Levels == null)
                throw new InvalidOperationException("Kết quả khối lượng chưa có đầy đủ dữ liệu chi tiết.");
            if (result.Intervals.Count > 1500 || result.Levels.Count > 1500)
                throw new InvalidOperationException("Bảng quá 1.500 dòng; hãy xuất Excel hoặc chia phạm vi CAD để tránh quá tải bản vẽ.");
            if (!IsFinite(result.TotalCutVolume) || !IsFinite(result.TotalFillVolume))
                throw new InvalidOperationException("Kết quả khối lượng không hợp lệ (NaN/Infinity).");

            if (!IsFinite(insertion.X) || !IsFinite(insertion.Y) || !IsFinite(insertion.Z))
                throw new InvalidOperationException("Điểm chèn bảng không hợp lệ (NaN/Infinity).");

            using (var tr = database.TransactionManager.StartTransaction())
            using (var block = new BlockTableRecord {
                Name = BlockPrefix + result.CalculationId + "_" + Guid.NewGuid().ToString("N"),
                Origin = Point3d.Origin })
            {
                pollCancellation?.Invoke();
                var layer = EnsureOutputLayer(database, tr);
                var arial = EnsureArialTextStyle(database, tr);
                var bt = (BlockTable)tr.GetObject(database.BlockTableId, OpenMode.ForRead);
                var model = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

                bt.UpgradeOpen();
                ObjectId blockId = bt.Add(block);
                tr.AddNewlyCreatedDBObject(block, true);
                // Local coordinates keep grouping independent of insertion;
                // this remains the existing 2D table output at Z=0.
                double x = 0;
                double y = 0;

                const int summaryRows = 9;
                using (var summary = NewTable(database, layer, summaryRows, 2,
                    new Point3d(x, y, 0), 68))
                {
                    summary.MergeCells(CellRange.Create(summary, 0, 0, 0, 1));
                    summary.Cells[0, 0].TextString = "IMSAT VOLUME – TỔNG HỢP KHỐI LƯỢNG";
                    Set(summary, 1, "Hạng mục", "Giá trị");
                    Set(summary, 2, "Thời điểm xuất", DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"));
                    Set(summary, 3, "Tổng khối lượng đào, m³", F(result.TotalCutVolume));
                    Set(summary, 4, "Tổng khối lượng đắp, m³", F(result.TotalFillVolume));
                    Set(summary, 5, "Chênh lệch đào − đắp, m³", F(result.NetVolume));
                    Set(summary, 6, "Số khoảng mặt cắt", result.Intervals.Count.ToString());
                    Set(summary, 7, "Phát triển", "Bùi Thế Nam · 0967280686");
                    Set(summary, 8, "Mã phiên tính (CalculationId)", result.CalculationId);
                    StyleSimpleTable(summary, arial, summaryRows, 2, pollCancellation);
                    Append(block, tr, summary);
                }
                reportProgress?.Invoke(1);
                y -= RowHeight * summaryRows + TableGap;

                // Match the accepted CAD sample: native editable TABLE, full
                // outer frame, merged title and grouped multi-level headers.
                // MineVolume has earthwork F/V, not invented road-surface layers.
                const int columns = 11;
                const int topRows = 3;
                int totalRow = result.Intervals.Count + topRows;
                using (var interval = NewTable(database, layer, totalRow + 1, columns,
                    new Point3d(x, y, 0), 31))
                {
                    interval.MergeCells(CellRange.Create(interval, 0, 0, 0, columns - 1));
                    interval.MergeCells(CellRange.Create(interval, 1, 0, 1, 3));
                    interval.MergeCells(CellRange.Create(interval, 1, 4, 1, 7));
                    interval.MergeCells(CellRange.Create(interval, 1, 8, 1, 10));
                    interval.Cells[0, 0].TextString =
                        "BẢNG TỔNG HỢP KHỐI LƯỢNG THEO MẶT CẮT";
                    interval.Cells[1, 0].TextString = "Thông tin tuyến";
                    interval.Cells[1, 4].TextString = "Diện tích (m²)";
                    interval.Cells[1, 8].TextString = "Khối lượng (m³)";
                    Set(interval, 2, "Thứ tự", "MC đầu", "MC cuối", "Khoảng cách L",
                        "F đào đầu", "F đào cuối", "F đắp đầu", "F đắp cuối",
                        "V đào", "V đắp", "Chênh lệch");
                    for (int i = 0; i < result.Intervals.Count; i++)
                    {
                        pollCancellation?.Invoke();
                        var v = result.Intervals[i];
                        Validate(v.CutVolume, v.FillVolume, v.Distance,
                            v.CutAreaStart, v.CutAreaEnd, v.FillAreaStart, v.FillAreaEnd);
                        Set(interval, i + topRows, (i + 1).ToString(),
                            v.StartSection, v.EndSection, F(v.Distance),
                            F(v.CutAreaStart), F(v.CutAreaEnd),
                            F(v.FillAreaStart), F(v.FillAreaEnd),
                            F(v.CutVolume), F(v.FillVolume),
                            F(v.CutVolume - v.FillVolume));
                        reportProgress?.Invoke(i + 2);
                    }
                    interval.MergeCells(CellRange.Create(interval, totalRow, 0, totalRow, 7));
                    interval.Cells[totalRow, 0].TextString = "Tổng";
                    interval.Cells[totalRow, 8].TextString = F(result.TotalCutVolume);
                    interval.Cells[totalRow, 9].TextString = F(result.TotalFillVolume);
                    interval.Cells[totalRow, 10].TextString = F(result.NetVolume);
                    StyleTable(interval, arial, totalRow, pollCancellation);
                    Append(block, tr, interval);
                }
                y -= RowHeight * (totalRow + 1) + TableGap;

                // Native AutoCAD TABLE style may auto-merge the very first
                // row as its TITLE. Placing six column names in row 0 therefore
                // rendered ONLY "STT" across the whole table in field QA.
                // Reserve title row 0 explicitly and put all six headings in
                // the actual header row 1, before any numerical data.
                const int levelHeaderRows = 2;
                int levelsTotalRow = result.Levels.Count + levelHeaderRows;
                using (var levels = NewTable(database, layer, levelsTotalRow + 1, 6,
                    new Point3d(x, y, 0), 43))
                {
                    levels.MergeCells(CellRange.Create(levels, 0, 0, 0, 5));
                    levels.Cells[0, 0].TextString = "BẢNG KHỐI LƯỢNG ĐÀO ĐẮP THEO TẦNG";
                    Set(levels, 1, "Thứ tự", "Tầng/mức", "Cao độ dưới (m)",
                        "Cao độ trên (m)", "Khối lượng đào (m³)", "Khối lượng đắp (m³)");
                    double cutLevels = 0, fillLevels = 0;
                    for (int i = 0; i < result.Levels.Count; i++)
                    {
                        pollCancellation?.Invoke();
                        var level = result.Levels[i];
                        Validate(level.CutVolume, level.FillVolume, level.Band.LowerZ, level.Band.UpperZ);
                        Set(levels, i + levelHeaderRows, (i + 1).ToString(), level.Band.Name,
                            F(level.Band.LowerZ), F(level.Band.UpperZ),
                            F(level.CutVolume), F(level.FillVolume));
                        cutLevels += level.CutVolume;
                        fillLevels += level.FillVolume;
                        reportProgress?.Invoke(result.Intervals.Count + i + 2);
                    }
                    levels.MergeCells(CellRange.Create(levels, levelsTotalRow, 0,
                        levelsTotalRow, 3));
                    levels.Cells[levelsTotalRow, 0].TextString = "Tổng theo các tầng";
                    levels.Cells[levelsTotalRow, 4].TextString = F(cutLevels);
                    levels.Cells[levelsTotalRow, 5].TextString = F(fillLevels);
                    StyleSimpleTable(levels, arial, levelsTotalRow + 1, 6, pollCancellation);
                    Append(block, tr, levels);
                }
                using (var reference = new BlockReference(
                    new Point3d(insertion.X, insertion.Y, 0), blockId) { LayerId = layer })
                {
                    model.AppendEntity(reference);
                    tr.AddNewlyCreatedDBObject(reference, true);
                }
                // A cancel or callback failure rolls back the definition,
                // reference and all three native tables, including
                // tables already appended earlier in this transaction.
                reportProgress?.Invoke(result.Intervals.Count + result.Levels.Count + 2);
                pollCancellation?.Invoke();
                tr.Commit();
            }
        }

        private static ObjectId EnsureArialTextStyle(Database db, Transaction tr)
        {
            const string style = "MV_ARIAL";
            var table = (TextStyleTable)tr.GetObject(db.TextStyleTableId, OpenMode.ForRead);
            if (table.Has(style)) return table[style];
            table.UpgradeOpen();
            var record = new TextStyleTableRecord { Name = style, FileName = "arial.ttf" };
            ObjectId id = table.Add(record);
            tr.AddNewlyCreatedDBObject(record, true);
            return id;
        }

        private static void StyleSimpleTable(Autodesk.AutoCAD.DatabaseServices.Table table,
            ObjectId arial, int rows, int columns, Action pollCancellation)
        {
            for (int row = 0; row < rows; row++)
            {
                pollCancellation?.Invoke();
                for (int col = 0; col < columns; col++)
                {
                    // Do not touch covered native TABLE cells after merge:
                    // summary/level title spans all columns; level total
                    // merges the first four columns.
                    if (row == 0 && col > 0) continue;
                    if (columns == 6 && row == rows - 1 && col >= 1 && col <= 3)
                        continue;
                    table.Cells[row, col].TextStyleId = arial;
                    table.Cells[row, col].TextHeight = 2.6;
                    table.Cells[row, col].Alignment = CellAlignment.MiddleCenter;
                }
            }
        }

        private static void StyleTable(Autodesk.AutoCAD.DatabaseServices.Table table,
            ObjectId arial, int totalRow, Action pollCancellation)
        {
            // Native TABLE cells keep merged headers, grids and text editable.
            for (int row = 0; row <= totalRow; row++)
            {
                pollCancellation?.Invoke();
                for (int col = 0; col < 11; col++)
                {
                    if (row == 0 && col > 0) continue;
                    if (row == 1 && col != 0 && col != 4 && col != 8) continue;
                    if (row == totalRow && col > 0 && col < 8) continue;
                    table.Cells[row, col].TextStyleId = arial;
                    table.Cells[row, col].TextHeight = row <= 2 ? 2.8 : 2.6;
                    table.Cells[row, col].Alignment = CellAlignment.MiddleCenter;
                }
            }
        }

        private static void Validate(params double[] values)
        {
            foreach (double value in values)
                if (!IsFinite(value)) throw new InvalidOperationException("Bảng khối lượng chứa giá trị không hữu hạn.");
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private static string F(double value) => value.ToString("0.00");

        private static Autodesk.AutoCAD.DatabaseServices.Table NewTable(Database db, ObjectId layer, int rows, int columns, Point3d point, double width)
        {
            var table = new Autodesk.AutoCAD.DatabaseServices.Table();
            try
            {
                table.LayerId = layer;
                table.TableStyle = db.Tablestyle;
                table.Position = point;
                table.SetSize(rows, columns);
                table.SetRowHeight(RowHeight);
                table.SetColumnWidth(width);
                return table;
            }
            catch { table.Dispose(); throw; }
        }

        private static void Set(Autodesk.AutoCAD.DatabaseServices.Table table, int row, params string[] cells)
        {
            for (int col = 0; col < cells.Length; col++)
                table.Cells[row, col].TextString = cells[col] ?? string.Empty;
        }

        private static void Append(BlockTableRecord model, Transaction tr, Autodesk.AutoCAD.DatabaseServices.Table table)
        {
            // Để nguyên đối tượng TABLE native: người dùng vẫn chỉnh được cell trong CAD.
            table.GenerateLayout();
            model.AppendEntity(table);
            tr.AddNewlyCreatedDBObject(table, true);
        }

        private static ObjectId EnsureOutputLayer(Database db, Transaction tr)
        {
            var layers = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
            if (layers.Has(OutputLayer))
            {
                var existing = (LayerTableRecord)tr.GetObject(layers[OutputLayer], OpenMode.ForRead);
                if (existing.IsLocked || existing.IsFrozen || existing.IsOff)
                    throw new InvalidOperationException("Layer MV_KL_BANG đang khóa/đóng băng/tắt. Hãy bật layer trước khi xuất.");
                return layers[OutputLayer];
            }
            layers.UpgradeOpen();
            var record = new LayerTableRecord
            {
                Name = OutputLayer,
                Color = Color.FromColorIndex(ColorMethod.ByAci, 7)
            };
            var id = layers.Add(record);
            tr.AddNewlyCreatedDBObject(record, true);
            return id;
        }
    }
}
