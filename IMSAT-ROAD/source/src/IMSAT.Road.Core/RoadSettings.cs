using System;

namespace IMSAT.Road.Core
{
    /// <summary>Defaults agreed for ordinary roads. Mine roads use a separate profile.</summary>
    public sealed class RoadSettings
    {
        public string StandardCode { get; set; } = "TCVN 4054:2005";
        public string RoadClass { get; set; } = "III";
        public string TerrainType { get; set; } = "Dong bang";
        public bool CheckTechnicalCriteria { get; set; } = true;
        public bool IsMineRoad { get; set; }

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(StandardCode)) throw new ArgumentException("Chua khai bao tieu chuan thiet ke.");
            if (string.IsNullOrWhiteSpace(RoadClass)) throw new ArgumentException("Chua khai bao cap duong.");
            if (string.IsNullOrWhiteSpace(TerrainType)) throw new ArgumentException("Chua khai bao dia hinh.");
            if (IsMineRoad && StandardCode == "TCVN 4054:2005")
                throw new ArgumentException("Duong mo phai chon bo chi tieu chuyen nganh rieng; khong mac dinh ap TCVN 4054:2005.");
        }
    }
}
