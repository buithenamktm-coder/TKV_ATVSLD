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
                    if (!ltr.IsDependent) result.Add(ltr.Name);
                }
                tr.Commit();
            }
            result.Sort(StringComparer.CurrentCultureIgnoreCase);
            return result;
        }
    }
}