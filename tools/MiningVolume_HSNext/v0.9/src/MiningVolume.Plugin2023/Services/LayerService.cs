using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;

namespace MiningVolume2023.Services
{
    public static class LayerService
    {
        public static List<string> GetLayers()
        {
            return GetLayers(includeMiningVolumeOutputs: true);
        }

        public static List<string> GetSourceLayers()
        {
            return GetLayers(includeMiningVolumeOutputs: false);
        }

        private static List<string> GetLayers(bool includeMiningVolumeOutputs)
        {
            var result = new List<string>();
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return result;
            using (doc.LockDocument())
            using (var tr = doc.Database.TransactionManager.StartOpenCloseTransaction())
            {
                var lt = (LayerTable)tr.GetObject(doc.Database.LayerTableId, OpenMode.ForRead);
                foreach (ObjectId id in lt)
                {
                    var ltr = (LayerTableRecord)tr.GetObject(id, OpenMode.ForRead);
                    if (ltr.IsDependent) continue;
                    if (!includeMiningVolumeOutputs && IsMiningVolumeOutputLayer(ltr.Name)) continue;
                    result.Add(ltr.Name);
                }
                tr.Commit();
            }
            result.Sort(StringComparer.CurrentCultureIgnoreCase);
            return result;
        }

        private static bool IsMiningVolumeOutputLayer(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            return name.StartsWith("MV_TIN_", StringComparison.OrdinalIgnoreCase) ||
                   name.StartsWith("MV_MC_", StringComparison.OrdinalIgnoreCase) ||
                   name.StartsWith("MV_SELFTEST_", StringComparison.OrdinalIgnoreCase);
        }
    }
}