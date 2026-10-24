using System;
using System.Drawing;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using System.Windows.Forms;
using MiningVolume.Core.Volumes;
using MiningVolume2023.Services;

namespace MiningVolume2023.UI
{
    public sealed class VolumePage : UserControl
    {
        private readonly Label _scope;
        private readonly ProfessionalFormulaPanel _formula;
        private readonly Label _totals;
        private readonly Label _status;
        private readonly DataGridView _detail;
        private readonly DataGridView _levels;
        private readonly Button _calculate;
        private CancellationTokenSource _calculationCts;

        public VolumePage()
        {
            Font = new Font("Arial", 9F); BackColor = Color.White;
            Controls.Add(new Label { Text = "TÍNH KHỐI LƯỢNG", Dock = DockStyle.Top, Height = 32, Font = new Font("Arial", 11F, FontStyle.Bold) });

            var info = new TableLayoutPanel { Dock = DockStyle.Top, Height = 218, ColumnCount = 2, RowCount = 3,
                Padding = new Padding(10, 7, 10, 10), Margin = new Padding(0, 4, 0, 10) };
            info.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            info.RowStyles.Add(new RowStyle(SizeType.Absolute, 126));
            info.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            info.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160)); info.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            _scope = InfoLabel(); _formula = new ProfessionalFormulaPanel(); _totals = InfoLabel(true);
            AddInfo(info, 0, "Phạm vi tính", _scope);
            AddInfo(info, 1, "Công thức", _formula);
            AddInfo(info, 2, "Tổng khối lượng", _totals);
            // Keep mathematical transparency available on demand, but do not
            // overwhelm the default workflow with a large formula pane.
            var formulaToggle = new CheckBox
            {
                Text = "Xem công thức tính", AutoSize = true,
                Dock = DockStyle.Top, Checked = false,
                Padding = new Padding(10, 3, 0, 3)
            };
            info.RowStyles[1].Height = 0;
            info.Height = 94;
            formulaToggle.CheckedChanged += (sender, args) =>
            {
                info.RowStyles[1].Height = formulaToggle.Checked ? 126 : 0;
                info.Height = formulaToggle.Checked ? 218 : 94;
            };
            Controls.Add(info); info.BringToFront();
            Controls.Add(formulaToggle); formulaToggle.BringToFront();

            var tabs = new TabControl { Dock = DockStyle.Fill };
            _detail = Grid(new[] { "Đoạn", "MC đầu", "MC cuối", "L, m", "Fđ đầu", "Fđ giữa", "Fđ cuối", "V đào, m³", "Fđp đầu", "Fđp giữa", "Fđp cuối", "V đắp, m³", "Công thức" });
            _levels = Grid(new[] { "Tầng / mức", "Cao độ dưới", "Cao độ trên", "V đào, m³", "V đắp, m³", "Chênh lệch, m³" });
            var p1 = new TabPage("Chi tiết giữa các mặt cắt"); p1.Controls.Add(_detail); tabs.TabPages.Add(p1);
            var p2 = new TabPage("Tổng hợp theo tầng"); p2.Controls.Add(_levels); tabs.TabPages.Add(p2);
            Controls.Add(tabs); tabs.BringToFront();

            var bottom = new TableLayoutPanel { Dock = DockStyle.Bottom, Height = 48, ColumnCount = 2, Padding = new Padding(4) };
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 245));
            _status = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.DimGray, Padding = new Padding(2, 0, 0, 0), Text = "Chưa tính khối lượng." };
            _calculate = new Button { Text = "Tính / cập nhật khối lượng", Dock = DockStyle.Top,
                Height = 30, Margin = new Padding(5, 3, 5, 3),
                Font = new Font("Arial", 9.5F, FontStyle.Bold) };
            _calculate.Click += CalculateClicked;
            bottom.Controls.Add(_status, 0, 0); bottom.Controls.Add(_calculate, 1, 0);
            Controls.Add(bottom); bottom.BringToFront();

            ProjectState.Current.Changed += OnStateChanged;
            RefreshView();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { _calculationCts?.Cancel(); ProjectState.Current.Changed -= OnStateChanged; }
            base.Dispose(disposing);
        }

        private async void CalculateClicked(object sender, EventArgs e)
        {
            if (_calculationCts != null) { _calculationCts.Cancel(); return; }
            if (!LicenseUiGuard.Check()) return;
            var cts = new CancellationTokenSource();
            _calculationCts = cts;
            var token = cts.Token;
            var st = ProjectState.Current;
            try
            {
                _calculate.Text = "Hủy tính khối lượng";
                UseWaitCursor = true;
                _status.Text = "Đang tính diện tích theo tầng và khối lượng giữa các mặt cắt...";
                // Hard gate: calculation is allowed only when BOTH TINs are current
                // and their CAD layers are synchronized with the verified core surfaces.
                SurfaceWorkflowService.EnsureBothTinsReady(synchronizeCadLayers: true);

                var doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
                if (doc == null)
                    throw new InvalidOperationException("Không có bản vẽ AutoCAD đang hoạt động.");
                double from = st.FromLevel, to = st.ToLevel, step = st.LevelStep;
                double calculationFrom = from, calculationTo = to;
                string autoLevelNote = "";
                // Keep candidate levels local: cancel/failure must preserve the accepted result.
                // Repair an old/stale Project default (e.g. 0..+100) without
                // requiring the operator to redraw all CAD sections first.
                // Change only if the complete requested range misses the
                // measured TIN elevation extent; ordinary custom bands remain.
                var measured=st.SectionProfiles.Where(p=>p!=null && p.HasData).ToArray();
                if (measured.Length>0 && step>0 &&
                    !double.IsNaN(step) && !double.IsInfinity(step))
                {
                    double minZ=measured.Min(p=>p.MinZ);
                    double maxZ=measured.Max(p=>p.MaxZ);
                    double low=Math.Min(calculationFrom,calculationTo);
                    double high=Math.Max(calculationFrom,calculationTo);
                    if (high<minZ-1e-7 || low>maxZ+1e-7 ||
                        Math.Abs(calculationFrom-calculationTo)<1e-7)
                    {
                        calculationFrom=Math.Floor(minZ/step)*step;
                        calculationTo=Math.Ceiling(maxZ/step)*step;
                        if(calculationTo<=calculationFrom)calculationTo=calculationFrom+step;
                        autoLevelNote=$"Đã điều chỉnh mức tính theo TIN: {calculationFrom:0.###} đến {calculationTo:0.###} m. ";
                    }
                }
                var system = st.SectionSystem;
                var profiles = st.SectionProfiles.ToArray();
                var existing = st.Existing.Tin;
                var design = st.Design.Tin;
                long revisionExisting = st.Existing.Source.Revision;
                long revisionDesign = st.Design.Source.Revision;
                var watch = Stopwatch.StartNew();
                var result = await Task.Run(() => VolumeWorkflowService.CalculateCore(system, profiles, existing, design, calculationFrom, calculationTo, step, token));
                token.ThrowIfCancellationRequested();
                // Never commit an asynchronous volume result to a different DWG,
                // TIN revision, section geometry or level parameter set.
                if (!ReferenceEquals(doc, Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument) ||
                    !ReferenceEquals(system, st.SectionSystem) ||
                    !ReferenceEquals(existing, st.Existing.Tin) ||
                    !ReferenceEquals(design, st.Design.Tin) ||
                    !st.Existing.IsTinCurrent || !st.Design.IsTinCurrent ||
                    revisionExisting != st.Existing.Source.Revision ||
                    revisionDesign != st.Design.Source.Revision ||
                    from != st.FromLevel || to != st.ToLevel || step != st.LevelStep ||
                    profiles.Length != st.SectionProfiles.Count ||
                    profiles.Where((p, i) => !ReferenceEquals(p, st.SectionProfiles[i])).Any())
                    throw new InvalidOperationException(
                        "DWG, dữ liệu TIN hoặc mặt cắt đã thay đổi khi tính. Đã giữ nguyên kết quả cũ; hãy tính lại.");
                VolumeWorkflowService.Commit(result, calculationFrom, calculationTo, step);
                watch.Stop();
                st.VolumeCalculatedUtc = DateTime.UtcNow;
                st.VolumeCalculationSeconds = watch.Elapsed.TotalSeconds;
                // Commit notifies RefreshView; do not rebuild both tables a second time.
                _status.Text = !result.IsComplete
                    ? autoLevelNote+$"CHƯA ĐẦY ĐỦ: {result.ExcludedIntervals:n0} khoảng chưa xác nhận; chỉ hiển thị phần khối lượng hợp lệ."
                    : result.Warnings.Count == 0
                        ? autoLevelNote + "Đã tính khối lượng thành công."
                        : autoLevelNote + "Đã tính khối lượng, cần kiểm tra cảnh báo.";
            }
            catch (OperationCanceledException)
            {
                if (!IsDisposed) _status.Text = "Đã hủy; không ghi kết quả tính dở.";
            }
            catch (Exception ex)
            {
                if (IsDisposed) return;
                _status.Text = "Chưa tính được khối lượng.";
                MessageBox.Show(ex.Message, "Tính khối lượng", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _calculationCts = null;
                cts.Dispose();
                if (!IsDisposed)
                {
                    UseWaitCursor = false;
                    _calculate.Text = "Tính / cập nhật khối lượng";
                    _calculate.Enabled = true;
                }
            }
        }

        private void OnStateChanged(object sender, EventArgs e)
        {
            if (!IsHandleCreated) return;
            if (InvokeRequired) BeginInvoke(new Action(RefreshView)); else RefreshView();
        }

        private void RefreshView()
        {
            var st = ProjectState.Current;
            _scope.Text = $"{FmtLevel(st.FromLevel)} → {FmtLevel(st.ToLevel)}; bước tầng {st.LevelStep:0.###} m";
            // The live formula panel typesets exact stacked fractions/case labels.
            if (st.VolumeResult == null)
            {
                _totals.Text = "Chưa có kết quả";
                _detail.Rows.Clear(); _levels.Rows.Clear();
                return;
            }
            BindResult(st.VolumeResult);
        }

        private void BindResult(VolumeResult r)
        {
            _detail.Rows.Clear();
            foreach (var x in r.Intervals)
            {
                string method = FormulaName(x.CutFormula) + " / " + FormulaName(x.FillFormula);
                _detail.Rows.Add(x.Index, x.StartSection, x.EndSection, F3(x.Distance), F2(x.CutAreaStart), F2(x.CutAreaMid), F2(x.CutAreaEnd), F2(x.CutVolume),
                    F2(x.FillAreaStart), F2(x.FillAreaMid), F2(x.FillAreaEnd), F2(x.FillVolume), method);
            }

            _levels.Rows.Clear();
            foreach (var x in r.Levels)
                _levels.Rows.Add(x.Band.Name, F3(x.Band.LowerZ), F3(x.Band.UpperZ), F2(x.CutVolume), F2(x.FillVolume), F2(x.NetVolume));

            _totals.Text = (r.IsComplete ? "" : "CHỈ PHẦN HỢP LỆ – CHƯA CHỐT  |  ") +
                $"Đào: {r.TotalCutVolume:n2} m³   |   Đắp: {r.TotalFillVolume:n2} m³   |   Chênh: {r.NetVolume:n2} m³";
        }

        private static DataGridView Grid(string[] columns)
        {
            var g = new DataGridView
            {
                Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false,
                RowHeadersVisible = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells
            };
            foreach (var c in columns) g.Columns.Add(c, c);
            return g;
        }

        private static Label InfoLabel(bool bold = false) => new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Font = new Font("Arial", 9F, bold ? FontStyle.Bold : FontStyle.Regular) };
        private static void AddInfo(TableLayoutPanel p, int row, string name, Control value)
        {
            p.Controls.Add(new Label { Text = name, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Font = new Font("Arial", 9F, FontStyle.Bold) }, 0, row);
            p.Controls.Add(value, 1, row);
        }
        private static string F2(double v) => v.ToString("0.00");
        private static string F3(double v) => v.ToString("0.###");
        private static string FormulaName(VolumeFormulaKind kind)
        {
            switch (kind)
            {
                case VolumeFormulaKind.Pyramid: return "Hình chóp";
                case VolumeFormulaKind.Frustum: return "Hình chóp cụt";
                case VolumeFormulaKind.Prismoidal: return "Prismoid (cũ)";
                default: return "TB hai đầu";
            }
        }

        private static string FmtLevel(double z) => z > 0 ? "+" + z.ToString("0.###") : z.ToString("0.###");
    }
}