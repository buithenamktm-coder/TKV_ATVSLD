using System;
using System.Collections.Generic;
using System.Linq;

namespace MiningVolume2023.Services
{
    /// <summary>
    /// Vỉa than / thân quặng. Mã ổn định, không phụ thuộc tên hiển thị.
    /// Chọn đủ vết lộ ở hai bình đồ mới kích hoạt dữ liệu khoáng sản của vỉa.
    /// Bề mặt 3D hay vùng khoáng sản trên mặt cắt vẫn phải có trước khi tính tấn.
    /// </summary>
    public sealed class MineralBody
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Name { get; set; }
        public double DensityTonPerCubicMetre { get; set; }
        public string DensitySource { get; set; }
        public bool IsDeleted { get; set; }
        public List<string> ExistingOutcropHandles { get; } = new List<string>();
        public List<string> DesignOutcropHandles { get; } = new List<string>();

        public bool HasPairedOutcrops =>
            !IsDeleted && ExistingOutcropHandles.Count > 0 && DesignOutcropHandles.Count > 0;

        public string StateText
        {
            get
            {
                if (IsDeleted) return "Đã xóa mềm";
                if (HasPairedOutcrops) return "Đã chọn đủ 2 bình đồ – chờ kiểm tra hình học";
                if (ExistingOutcropHandles.Count == 0 && DesignOutcropHandles.Count == 0)
                    return "Chưa chọn lộ vỉa";
                return "Thiếu một bình đồ – chỉ tính đào đắp";
            }
        }

        public void AddTraces(ModelRole role, IEnumerable<string> handles)
        {
            if (handles == null) return;
            var target = role == ModelRole.Existing ? ExistingOutcropHandles : DesignOutcropHandles;
            foreach (var raw in handles)
            {
                var value = (raw ?? string.Empty).Trim().ToUpperInvariant();
                if (value.Length > 0 && !target.Contains(value))
                    target.Add(value);
            }
        }

        public void ClearTraces(ModelRole role)
        {
            (role == ModelRole.Existing ? ExistingOutcropHandles : DesignOutcropHandles).Clear();
        }

        public static MineralBody AddNew(IList<MineralBody> bodies, string name)
        {
            if (bodies == null) throw new ArgumentNullException(nameof(bodies));
            string normalized = ValidateName(bodies, name, null);
            var body = new MineralBody { Name = normalized };
            bodies.Add(body);
            return body;
        }

        public void Rename(IReadOnlyList<MineralBody> bodies, string newName)
        {
            Name = ValidateName(bodies, newName, Id);
        }

        private static string ValidateName(IEnumerable<MineralBody> bodies, string name, string excludeId)
        {
            string value = (name ?? string.Empty).Trim();
            if (value.Length == 0 || value.Length > 100)
                throw new ArgumentException("Tên vỉa/thân quặng bắt buộc, tối đa 100 ký tự.");
            if (bodies.Any(b => b != null && !b.IsDeleted && b.Id != excludeId &&
                string.Equals(b.Name, value, StringComparison.OrdinalIgnoreCase)))
                throw new ArgumentException("Tên vỉa/thân quặng đã tồn tại; hãy dùng tên khác.");
            return value;
        }
    }
}
