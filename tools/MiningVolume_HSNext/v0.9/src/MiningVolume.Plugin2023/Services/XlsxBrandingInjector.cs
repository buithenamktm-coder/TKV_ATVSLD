using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml;
using MiningVolume.Core.Reporting;

namespace MiningVolume2023.Services
{
    /// <summary>
    /// Adds the official IMSAT logo to the report-facing Excel sheets after the
    /// lightweight workbook writer has created a valid XLSX package.
    /// </summary>
    internal static class XlsxBrandingInjector
    {
        private const string MainNs = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        private const string RelNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        private const string PackageRelNs = "http://schemas.openxmlformats.org/package/2006/relationships";
        private const string ContentTypesNs = "http://schemas.openxmlformats.org/package/2006/content-types";
        private const string DrawingNs = "http://schemas.openxmlformats.org/drawingml/2006/spreadsheetDrawing";
        private const string DrawingMainNs = "http://schemas.openxmlformats.org/drawingml/2006/main";

        public static void Apply(string filePath, SpreadsheetReport report)
        {
            if (report == null) throw new ArgumentNullException(nameof(report));
            string logoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "imsat_logo_64.png");
            if (!File.Exists(logoPath))
                throw new FileNotFoundException("Thiếu logo IMSAT để đóng dấu nhận diện vào báo cáo Excel.", logoPath);

            var targets = report.Sheets
                .Select((sheet, index) => new { Sheet = sheet, Index = index + 1 })
                .Where(x => string.Equals(x.Sheet.Name, "Thông tin", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(x.Sheet.Name, "Tổng khối", StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (targets.Count == 0) return;

            using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            using (var zip = new ZipArchive(fs, ZipArchiveMode.Update, leaveOpen: false, entryNameEncoding: Encoding.UTF8))
            {
                ReplaceBinary(zip, "xl/media/imsat_logo.png", File.ReadAllBytes(logoPath));
                UpdateContentTypes(zip, targets.Select(x => x.Index));

                foreach (var target in targets)
                {
                    int colCount = Math.Max(1, Math.Max(target.Sheet.Headers.Count,
                        target.Sheet.Rows.Count == 0 ? 0 : target.Sheet.Rows.Max(r => r?.Count ?? 0)));
                    int anchorCol = Math.Max(0, colCount - 1);
                    AddDrawingReferenceToWorksheet(zip, target.Index);
                    WriteWorksheetRelationships(zip, target.Index);
                    WriteDrawing(zip, target.Index, anchorCol);
                    WriteDrawingRelationships(zip, target.Index);
                }
            }
        }

        private static void UpdateContentTypes(ZipArchive zip, IEnumerable<int> sheetIndexes)
        {
            const string path = "[Content_Types].xml";
            var doc = LoadXml(zip, path);
            var root = doc.DocumentElement;
            bool hasPng = root.ChildNodes.Cast<XmlNode>().Any(n =>
                n.LocalName == "Default" &&
                string.Equals(n.Attributes?["Extension"]?.Value, "png", StringComparison.OrdinalIgnoreCase));
            if (!hasPng)
            {
                var d = doc.CreateElement("Default", ContentTypesNs);
                d.SetAttribute("Extension", "png");
                d.SetAttribute("ContentType", "image/png");
                root.AppendChild(d);
            }

            foreach (int index in sheetIndexes)
            {
                string part = "/xl/drawings/drawingMV" + index + ".xml";
                bool exists = root.ChildNodes.Cast<XmlNode>().Any(n =>
                    n.LocalName == "Override" &&
                    string.Equals(n.Attributes?["PartName"]?.Value, part, StringComparison.OrdinalIgnoreCase));
                if (exists) continue;
                var o = doc.CreateElement("Override", ContentTypesNs);
                o.SetAttribute("PartName", part);
                o.SetAttribute("ContentType", "application/vnd.openxmlformats-officedocument.drawing+xml");
                root.AppendChild(o);
            }
            SaveXml(zip, path, doc);
        }

        private static void AddDrawingReferenceToWorksheet(ZipArchive zip, int sheetIndex)
        {
            string path = "xl/worksheets/sheet" + sheetIndex + ".xml";
            var doc = LoadXml(zip, path);
            var root = doc.DocumentElement;

            bool already = root.ChildNodes.Cast<XmlNode>().Any(n => n.LocalName == "drawing");
            if (!already)
            {
                var drawing = doc.CreateElement("drawing", MainNs);
                var id = doc.CreateAttribute("r", "id", RelNs);
                id.Value = "rIdMVLogo";
                drawing.Attributes.Append(id);
                root.AppendChild(drawing);
            }
            SaveXml(zip, path, doc);
        }

        private static void WriteWorksheetRelationships(ZipArchive zip, int sheetIndex)
        {
            string path = "xl/worksheets/_rels/sheet" + sheetIndex + ".xml.rels";
            var doc = NewRelationshipsDocument();
            var rel = doc.CreateElement("Relationship", PackageRelNs);
            rel.SetAttribute("Id", "rIdMVLogo");
            rel.SetAttribute("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/drawing");
            rel.SetAttribute("Target", "../drawings/drawingMV" + sheetIndex + ".xml");
            doc.DocumentElement.AppendChild(rel);
            SaveXml(zip, path, doc);
        }

        private static void WriteDrawing(ZipArchive zip, int sheetIndex, int col)
        {
            string path = "xl/drawings/drawingMV" + sheetIndex + ".xml";
            var settings = new XmlWriterSettings { Encoding = new UTF8Encoding(false), Indent = false, OmitXmlDeclaration = false };
            ReplaceText(zip, path, writer =>
            {
                using (var x = XmlWriter.Create(writer, settings))
                {
                    x.WriteStartDocument(true);
                    x.WriteStartElement("xdr", "wsDr", DrawingNs);
                    x.WriteAttributeString("xmlns", "a", null, DrawingMainNs);
                    x.WriteAttributeString("xmlns", "r", null, RelNs);

                    x.WriteStartElement("xdr", "oneCellAnchor", DrawingNs);
                    x.WriteStartElement("xdr", "from", DrawingNs);
                    x.WriteElementString("xdr", "col", DrawingNs, col.ToString());
                    x.WriteElementString("xdr", "colOff", DrawingNs, "0");
                    x.WriteElementString("xdr", "row", DrawingNs, "0");
                    x.WriteElementString("xdr", "rowOff", DrawingNs, "0");
                    x.WriteEndElement();

                    x.WriteStartElement("xdr", "ext", DrawingNs);
                    x.WriteAttributeString("cx", "304800");
                    x.WriteAttributeString("cy", "304800");
                    x.WriteEndElement();

                    x.WriteStartElement("xdr", "pic", DrawingNs);
                    x.WriteStartElement("xdr", "nvPicPr", DrawingNs);
                    x.WriteStartElement("xdr", "cNvPr", DrawingNs);
                    x.WriteAttributeString("id", "1");
                    x.WriteAttributeString("name", "IMSAT Logo");
                    x.WriteEndElement();
                    x.WriteElementString("xdr", "cNvPicPr", DrawingNs, string.Empty);
                    x.WriteEndElement();

                    x.WriteStartElement("xdr", "blipFill", DrawingNs);
                    x.WriteStartElement("a", "blip", DrawingMainNs);
                    x.WriteAttributeString("r", "embed", RelNs, "rId1");
                    x.WriteEndElement();
                    x.WriteStartElement("a", "stretch", DrawingMainNs);
                    x.WriteElementString("a", "fillRect", DrawingMainNs, string.Empty);
                    x.WriteEndElement();
                    x.WriteEndElement();

                    x.WriteStartElement("xdr", "spPr", DrawingNs);
                    x.WriteStartElement("a", "prstGeom", DrawingMainNs);
                    x.WriteAttributeString("prst", "rect");
                    x.WriteElementString("a", "avLst", DrawingMainNs, string.Empty);
                    x.WriteEndElement();
                    x.WriteEndElement();

                    x.WriteEndElement(); // pic
                    x.WriteElementString("xdr", "clientData", DrawingNs, string.Empty);
                    x.WriteEndElement(); // anchor
                    x.WriteEndElement(); // wsDr
                }
            });
        }

        private static void WriteDrawingRelationships(ZipArchive zip, int sheetIndex)
        {
            string path = "xl/drawings/_rels/drawingMV" + sheetIndex + ".xml.rels";
            var doc = NewRelationshipsDocument();
            var rel = doc.CreateElement("Relationship", PackageRelNs);
            rel.SetAttribute("Id", "rId1");
            rel.SetAttribute("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/image");
            rel.SetAttribute("Target", "../media/imsat_logo.png");
            doc.DocumentElement.AppendChild(rel);
            SaveXml(zip, path, doc);
        }

        private static XmlDocument NewRelationshipsDocument()
        {
            var doc = new XmlDocument();
            var declaration = doc.CreateXmlDeclaration("1.0", "UTF-8", "yes");
            doc.AppendChild(declaration);
            doc.AppendChild(doc.CreateElement("Relationships", PackageRelNs));
            return doc;
        }

        private static XmlDocument LoadXml(ZipArchive zip, string path)
        {
            var entry = zip.GetEntry(path);
            if (entry == null) throw new InvalidDataException("XLSX thiếu thành phần: " + path);
            var doc = new XmlDocument { PreserveWhitespace = true };
            using (var stream = entry.Open()) doc.Load(stream);
            return doc;
        }

        private static void SaveXml(ZipArchive zip, string path, XmlDocument doc)
        {
            var old = zip.GetEntry(path);
            old?.Delete();
            var entry = zip.CreateEntry(path, CompressionLevel.Optimal);
            using (var stream = entry.Open())
            using (var x = XmlWriter.Create(stream, new XmlWriterSettings { Encoding = new UTF8Encoding(false), Indent = false, CloseOutput = false }))
                doc.Save(x);
        }

        private static void ReplaceBinary(ZipArchive zip, string path, byte[] bytes)
        {
            var old = zip.GetEntry(path);
            old?.Delete();
            var entry = zip.CreateEntry(path, CompressionLevel.Optimal);
            using (var stream = entry.Open()) stream.Write(bytes, 0, bytes.Length);
        }

        private static void ReplaceText(ZipArchive zip, string path, Action<Stream> write)
        {
            var old = zip.GetEntry(path);
            old?.Delete();
            var entry = zip.CreateEntry(path, CompressionLevel.Optimal);
            using (var stream = entry.Open()) write(stream);
        }
    }
}
