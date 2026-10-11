using System;
using System.Collections.Generic;
using System.Linq;
using MiningVolume.Core.Volumes;

namespace MiningVolume2023.Services
{
    internal static class MineralVolumeWorkflowService
    {
        /// <summary>
        /// Read all edited CAD polygons NOW, validate cut envelope and mutual
        /// overlap, then calculate end-section-based seam volumes.
        /// Do not re-use geometry cached before AutoCAD grip edits.
        /// </summary>
        public static MineralVolumeSummary Recalculate()
        {
            var timer=System.Diagnostics.Stopwatch.StartNew();
            var st=ProjectState.Current;
            if (st.VolumeResult == null || st.VolumeResult.Intervals.Count == 0)
                throw new InvalidOperationException("Hãy tính lại khối lượng đào/đắp theo mặt cắt trước.");
            if (st.MineralBodies.Any(b=>!b.IsDeleted&&!b.HasPairedOutcrops))
                throw new InvalidOperationException(
                    "Có vỉa đã khai báo nhưng thiếu cặp đường lộ vỉa. "+
                    "Cần hoàn thiện liên kết hoặc xóa mềm vỉa chưa sử dụng.");
            var bodies=st.MineralBodies.Where(b=>b.HasPairedOutcrops).ToList();
            if (bodies.Count==0)
                throw new InvalidOperationException("Chưa có vỉa nào được gán đủ lộ vỉa hiện trạng và thiết kế.");

            // UpdateAll reads Polyline coordinates from the current DWG,
            // checks that polygons are inside excavation and do not overlap.
            var shapes=MineralSectionRegionService.UpdateAll();
            var entries=new List<MineralSectionInput>();
            foreach(var seam in bodies)
            {
                var bySection=shapes.Where(a=>a.BodyId==seam.Id)
                    .GroupBy(a=>a.SectionName,StringComparer.OrdinalIgnoreCase);
                foreach(var group in bySection)
                {
                    if(seam.ConfirmedAbsentSections.Any(x=>string.Equals(x,group.Key,StringComparison.OrdinalIgnoreCase)))
                        throw new InvalidOperationException(
                            seam.Name+" tại "+group.Key+" đã xác nhận vắng mặt nhưng đang có Polyline khoáng sản.");
                    entries.Add(new MineralSectionInput
                    {
                        BodyId=seam.Id,SectionName=group.Key,
                        AreaSquareMetres=group.Sum(x=>x.AreaSquareMetres)
                    });
                }
                foreach(string name in seam.ConfirmedAbsentSections)
                {
                    if (entries.Any(x=>x.BodyId==seam.Id &&
                        string.Equals(x.SectionName,name,StringComparison.OrdinalIgnoreCase)))
                        continue;
                    entries.Add(new MineralSectionInput
                    {
                        BodyId=seam.Id,SectionName=name,AreaSquareMetres=0,
                        AbsentConfirmed=true
                    });
                }
            }
            var mineralInputs=bodies.Select(b=>new MineralSeamInput
            {
                Id=b.Id,Name=b.Name,DensityTonPerCubicMetre=b.DensityTonPerCubicMetre
            }).ToArray();
            var result=MineralVolumeCalculator.Calculate(st.VolumeResult,mineralInputs,entries);
            timer.Stop();
            result.CalculatedUtc=DateTime.UtcNow;
            result.CalculationSeconds=timer.Elapsed.TotalSeconds;
            return result;
        }
    }
}
