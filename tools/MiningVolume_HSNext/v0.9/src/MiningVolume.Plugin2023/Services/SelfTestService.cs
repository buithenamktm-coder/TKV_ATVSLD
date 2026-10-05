using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using MiningVolume.Core.Geometry;
using MiningVolume.Core.Reporting;
using MiningVolume.Core.Sections;
using MiningVolume.Core.Surface;
using MiningVolume.Core.Volumes;
using MiningVolume2023.UI;

namespace MiningVolume2023.Services
{
    public sealed class SelfTestResult
    {
        public bool Passed { get; set; }
        public List<string> Checks { get; } = new List<string>();
        public List<string> Errors { get; } = new List<string>();
        public string OutputFile { get; set; }
    }

    public static class SelfTestService
    {
        private const double Tol = 1e-6;

        public static SelfTestResult Run()
        {
            var r = new SelfTestResult();
            try
            {
                var existing = CreateFlatTin("SelfTest Existing", 10.0);
                var design = CreateFlatTin("SelfTest Design", 0.0);
                Check(r, existing.Triangles.Count == 2 && design.Triangles.Count == 2, "TIN tổng hợp 2 tam giác / bề mặt");

                var boundary = new List<Vec2>
                {
                    new Vec2(0,0), new Vec2(100,0), new Vec2(100,100), new Vec2(0,100)
                };
                var sys = new SectionSystemBuilder().Build(boundary, new Vec2(1, 0), new SectionGenerationOptions
                {
                    Spacing = 20.0,
                    AppendLastOffset = true,
                    NamePrefix = "MC-",
                    FirstNumber = 1
                });
                Check(r, sys.Lines.Count == 6, "Sinh 6 tuyến mặt cắt cho ranh 100 m / bước 20 m");

                var sampler = new TinSectionSampler();
                var profiles = sys.Lines.Select(x => sampler.BuildProfile(x, existing, design)).ToList();
                Check(r, profiles.All(x => x.HasData), "Tất cả mặt cắt lấy được dữ liệu từ hai TIN");
                Check(r, profiles.All(x => Near(x.CutArea, 1000.0) && Near(x.FillArea, 0.0)),
                    "Diện tích mỗi mặt cắt: đào 1.000 m2, đắp 0 m2");

                var volume = new SectionVolumeCalculator().Calculate(sys, profiles, existing, design,
                    new VolumeCalculationOptions
                    {
                        FromLevel = 0,
                        ToLevel = 10,
                        LevelStep = 5,
                        PreferPrismoidal = true
                    });
                Check(r, volume.Intervals.Count == 5, "Tính 5 khoảng khối lượng giữa 6 mặt cắt");
                Check(r, volume.Intervals.All(x => x.CutFormula == VolumeFormulaKind.Prismoidal),
                    "Các khoảng dùng prismoid với mặt cắt giữa thực");
                Check(r, Near(volume.TotalCutVolume, 100000.0, 1e-3) && Near(volume.TotalFillVolume, 0.0),
                    "Tổng khối chuẩn: đào 100.000 m3, đắp 0 m3");
                Check(r, volume.Levels.Count == 2 && Near(volume.Levels.Sum(x => x.CutVolume), 100000.0, 1e-3),
                    "Tổng theo 2 tầng bằng tổng khối chi tiết");

                string xlsx = Path.Combine(Path.GetTempPath(), "MiningVolume_v0104_selftest.xlsx");
                WriteSmokeWorkbook(xlsx, profiles, volume);
                Check(r, File.Exists(xlsx) && new FileInfo(xlsx).Length > 1000, "Sinh được XLSX độc lập Excel/COM");
                try { File.Delete(xlsx); } catch { }

                // Regression gate: construct the real palette content, including all pages
                // and their default controls. This catches WinForms initialization errors
                // such as invalid NumericUpDown min/max/value ordering before Setup can PASS.
                using (var ui = new MainPaletteControl())
                {
                    ui.Size = new System.Drawing.Size(580, 620);
                    ui.CreateControl();
                    ui.ShowPage(AppPage.Section);
                    ui.PerformLayout();

                    Check(r, ui.Controls.Count > 0, "Khởi tạo đầy đủ giao diện MiningVolume");

                    var drawSections = FindByName(ui, "btnDrawSections");
                    Check(r,
                        drawSections != null &&
                        drawSections.Visible &&
                        drawSections.Width > 80 &&
                        drawSections.Height > 20 &&
                        drawSections.Bounds.Right <= drawSections.Parent.ClientSize.Width &&
                        drawSections.Bounds.Bottom <= drawSections.Parent.ClientSize.Height,
                        "Nút XUẤT / VẼ MẶT CẮT hiển thị trong palette tối thiểu");
                }
                Check(r, string.IsNullOrWhiteSpace(EntryPoint.StartupUiError),
                    "Giao diện startup không phát sinh exception");

                // AutoCAD-host smoke test: the release is not allowed to PASS unless
                // MiningVolume can create real dedicated TIN layers and write/verify
                // the expected number of 3DFACE triangles in the active database.
                RunCadTinLayerSmoke(r, existing, design);
            }
            catch (Exception ex)
            {
                r.Errors.Add(ex.ToString());
            }

            r.Passed = r.Errors.Count == 0 && r.Checks.Count >= 14;
            r.OutputFile = ResolveOutputPath();
            WriteResultFile(r);
            return r;
        }

        private static void RunCadTinLayerSmoke(SelfTestResult r, TinSurface existing, TinSurface design)
        {
            var doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            if (doc == null)
            {
                r.Errors.Add("FAIL | AutoCAD không có bản vẽ hoạt động để kiểm tra layer TIN.");
                return;
            }

            const string existingLayer = "MV_SELFTEST_TIN_HIENTRANG";
            const string designLayer = "MV_SELFTEST_TIN_THIETKE";

            using (doc.LockDocument())
            {
                try
                {
                    TinCadRenderer.Clear(doc.Database, existingLayer);
                    TinCadRenderer.Clear(doc.Database, designLayer);

                    int writtenExisting = TinCadRenderer.Replace(doc.Database, existingLayer, existing, 1);
                    int writtenDesign = TinCadRenderer.Replace(doc.Database, designLayer, design, 3);

                    int countExisting = TinCadRenderer.CountFaces(doc.Database, existingLayer);
                    int countDesign = TinCadRenderer.CountFaces(doc.Database, designLayer);

                    Check(r,
                        TinCadRenderer.LayerExists(doc.Database, existingLayer) &&
                        writtenExisting == existing.Triangles.Count &&
                        countExisting == existing.Triangles.Count,
                        "AutoCAD tạo/ghi/đếm đúng layer TIN hiện trạng");

                    Check(r,
                        TinCadRenderer.LayerExists(doc.Database, designLayer) &&
                        writtenDesign == design.Triangles.Count &&
                        countDesign == design.Triangles.Count,
                        "AutoCAD tạo/ghi/đếm đúng layer TIN thiết kế");
                }
                finally
                {
                    try { TinCadRenderer.Clear(doc.Database, existingLayer); } catch { }
                    try { TinCadRenderer.Clear(doc.Database, designLayer); } catch { }
                    try { TinCadRenderer.DeleteLayerIfEmpty(doc.Database, existingLayer); } catch { }
                    try { TinCadRenderer.DeleteLayerIfEmpty(doc.Database, designLayer); } catch { }
                }
            }
        }

        private static System.Windows.Forms.Control FindByName(System.Windows.Forms.Control root, string name)
        {
            if (root == null) return null;
            if (string.Equals(root.Name, name, StringComparison.Ordinal)) return root;
            foreach (System.Windows.Forms.Control child in root.Controls)
            {
                var found = FindByName(child, name);
                if (found != null) return found;
            }
            return null;
        }

        private static TinSurface CreateFlatTin(string name, double z)
        {
            var a = new Vec3(0, 0, z);
            var b = new Vec3(100, 0, z);
            var c = new Vec3(100, 100, z);
            var d = new Vec3(0, 100, z);
            return new TinSurface(name, new[] { new Triangle3(a, b, c), new Triangle3(a, c, d) }, new ValidationIssue[0]);
        }

        private static void WriteSmokeWorkbook(string path, IReadOnlyList<SectionProfile> profiles, VolumeResult volume)
        {
            var report = new SpreadsheetReport { Title = "MiningVolume v0.10.4 Self-test", Creator = "MiningVolume HS-Next" };
            var sheet = new ReportSheet { Name = "SelfTest", Title = "MININGVOLUME V0.10.4 - SELF TEST" };
            sheet.Headers.AddRange(new[] { "Mục", "Giá trị" });
            sheet.Rows.Add(new[] { ReportCell.Text("Số mặt cắt"), ReportCell.Int(profiles.Count) });
            sheet.Rows.Add(new[] { ReportCell.Text("Tổng đào, m3"), ReportCell.N2(volume.TotalCutVolume) });
            sheet.Rows.Add(new[] { ReportCell.Text("Tổng đắp, m3"), ReportCell.N2(volume.TotalFillVolume) });
            report.Sheets.Add(sheet);
            SimpleXlsxWriter.Write(path, report);
        }

        private static void Check(SelfTestResult r, bool condition, string name)
        {
            if (condition) r.Checks.Add("PASS | " + name);
            else r.Errors.Add("FAIL | " + name);
        }

        private static bool Near(double a, double b, double tol = Tol)
        {
            return Math.Abs(a - b) <= tol;
        }

        private static string ResolveOutputPath()
        {
            string p = Environment.GetEnvironmentVariable("MININGVOLUME_SELFTEST_FILE");
            if (string.IsNullOrWhiteSpace(p)) p = Path.Combine(Path.GetTempPath(), "MiningVolume_v0104_selftest.txt");
            return p;
        }

        private static void WriteResultFile(SelfTestResult r)
        {
            try
            {
                var lines = new List<string>
                {
                    "MiningVolume HS-Next v0.10.4 runtime self-test",
                    "Timestamp=" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                    "Status=" + (r.Passed ? "PASS" : "FAIL")
                };
                lines.AddRange(r.Checks);
                lines.AddRange(r.Errors);
                File.WriteAllLines(r.OutputFile, lines.ToArray(), System.Text.Encoding.UTF8);
            }
            catch { }
        }
    }
}