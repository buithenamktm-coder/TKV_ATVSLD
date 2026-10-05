using System;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using MiningVolume.Core.Volumes;
using MiningVolume2023.Services;

namespace MiningVolume2023.UI
{
    public sealed class VolumePage : UserControl
    {
        private readonly Label _scope;
        private readonly Label _formula;
        private readonly Label _totals;
        private readonly Label _status;
        private readonly DataGridView _detail;
        private readonly DataGridView _levels;
        private readonly Button _calculate;

        public VolumePage()
        {
            Font = new Font("Arial", 9F); BackColor = Color.White;
            Controls.Add(new Label { Text = "TÍNH KHỐI LƯỢNG", Dock = DockStyle.Top, Height = 32, Font = new Font("Arial", 11F, FontStyle.Bold) });

            var info = new TableLayoutPanel { Dock = DockStyle.Top, Height = 84, ColumnCount = 2, RowCount = 3, Padding = new Padding(5) };
            info.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 145)); info.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            _scope = InfoLabel(); _formula = InfoLabel(); _totals = InfoLabel(true);
            AddInfo(info, 0, "Phạm vi tính", _scope);
            AddInfo(info, 1, "Công thức", _formula);
            AddInfo(info, 2, "Tổng khối", _totals);
            Controls.Add(info); info.BringToFront();

            var tabs = new TabControl { Dock = DockStyle.Fill };
            _detail = Grid(new[] { "Đoạn", "MC đầu", "MC cuối", "L, m", "Fđ đầu", "Fđ giữa", "Fđ cuối", "V đào, m³", "Fđp đầu", "Fđp giữa", "Fđp cuối", "V đắp, m³", "Công thức" });
            _levels = Grid(new[] { "Tầng / mức", "Cao độ dưới", "Cao độ trên", "V đào, m³", "V đắp, m³", "Chênh lệch, m³" });
            var p1 = new TabPage("Chi tiết giữa các mặt cắt"); p1.Controls.Add(_detail); tabs.TabPages.Add(p1);
            var p2 = new TabPage("Tổng hợp theo tầng"); p2.Controls.Add(_levels); tabs.TabPages.Add(p2);
            Controls.Add(tabs); tabs.BringToFront();

            var bottom = new TableLayoutPanel { Dock = DockStyle.Bottom, Height = 66, ColumnCount = 2, Padding = new Padding(4) };
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 245));
            _status = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.DimGray, Padding = new Padding(2, 0, 0, 0), Text = "Chưa tính khối lượng." };
            _calculate = new Button { Text = "TÍNH / CẬP NHẬT KHỐI LƯỢNG", Dock = DockStyle.Top, Height = 34, Font = new Font("Arial", 9F, FontStyle.Bold) };
            _calculate.Click += CalculateClicked;
            bottom.Controls.Add(_status, 0, 0); bottom.Controls.Add(_calculate, 1, 0);
            Controls.Add(bottom); bottom.BringToFront();

            ProjectState.Current.Changed += OnStateChanged;
            RefreshView();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) ProjectState.Current.Changed -= OnStateChanged;
            base.Dispose(disposing);
        }

        private async void CalculateClicked(object sender, EventArgs e)
        {
            var st = ProjectState.Current;
            try
            {
                _calculate.Enabled = false;
                UseWaitCursor = true;
                _status.Text = "Đang tính diện tích theo tầng và khối lượng giữa các mặt cắt...";
                // Hard gate: calculation is allowed only when BOTH TINs are current
                // and their CAD layers are synchronized with the verified core surfaces.
                SurfaceWorkflowService.EnsureBothTinsReady(synchronizeCadLayers: true);

                double from = st.FromLevel, to = st.ToLevel, step = st.LevelStep;
                var system = st.SectionSystem;
                var profiles = st.SectionProfiles.ToArray();
                var existing = st.Existing.Tin;
                var design = st.Design.Tin;
                var result = await Task.Run(() => VolumeWorkflowService.CalculateCore(system, profiles, existing, design, from, to, step));
                VolumeWorkflowService.Commit(result, from, to, step);
                BindResult(result);
                _status.Text = result.Warnings.Count == 0
                    ? $"Hoàn thành: {result.Intervals.Count:n0} khoảng mặt cắt; {result.Levels.Count:n0} tầng/mức."
                    : $"Hoàn thành, có {result.Warnings.Count:n0} cảnh báo kiểm soát.";
            }
            catch (Exception ex)
            {
                _status.Text = "Chưa tính được khối lượng.";
                MessageBox.Show(ex.Message, "Tính khối lượng", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally { UseWaitCursor = false; _calculate.Enabled = true; }
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
            _formula.Text = "Prismoid V=L/6(F1+4Fm+F2); chỉ fallback V=L/2(F1+F2) khi không lấy được mặt cắt giữa";
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
                string method = x.CutFormula == VolumeFormulaKind.Prismoidal ? "Prismoid" : "TB hai đầu";
                _detail.Rows.Add(x.Index, x.StartSection, x.EndSection, F3(x.Distance), F2(x.CutAreaStart), F2(x.CutAreaMid), F2(x.CutAreaEnd), F2(x.CutVolume),
                    F2(x.FillAreaStart), F2(x.FillAreaMid), F2(x.FillAreaEnd), F2(x.FillVolume), method);
            }

            _levels.Rows.Clear();
            foreach (var x in r.Levels)
                _levels.Rows.Add(x.Band.Name, F3(x.Band.LowerZ), F3(x.Band.UpperZ), F2(x.CutVolume), F2(x.FillVolume), F2(x.NetVolume));

            _totals.Text = $"Đào: {r.TotalCutVolume:n2} m³   |   Đắp: {r.TotalFillVolume:n2} m³   |   Chênh: {r.NetVolume:n2} m³";
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
        private static string FmtLevel(double z) => z > 0 ? "+" + z.ToString("0.###") : z.ToString("0.###");
    }
}