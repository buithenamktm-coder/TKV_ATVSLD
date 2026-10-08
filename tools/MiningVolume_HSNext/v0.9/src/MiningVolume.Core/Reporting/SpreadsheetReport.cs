using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml;

namespace MiningVolume.Core.Reporting
{
    public enum ReportCellKind
    {
        Text,
        Integer,
        Number2,
        Number3,
        DateTime
    }

    public sealed class ReportCell
    {
        public ReportCell(object value, ReportCellKind kind = ReportCellKind.Text, bool bold = false)
        {
            Value = value;
            Kind = kind;
            Bold = bold;
        }
        public object Value { get; }
        public ReportCellKind Kind { get; }
        public bool Bold { get; }

        public static ReportCell Text(object value, bool bold = false) => new ReportCell(value, ReportCellKind.Text, bold);
        public static ReportCell Int(object value, bool bold = false) => new ReportCell(value, ReportCellKind.Integer, bold);
        public static ReportCell N2(object value, bool bold = false) => new ReportCell(value, ReportCellKind.Number2, bold);
        public static ReportCell N3(object value, bool bold = false) => new ReportCell(value, ReportCellKind.Number3, bold);
        public static ReportCell Date(object value, bool bold = false) => new ReportCell(value, ReportCellKind.DateTime, bold);
    }

    public sealed class ReportSheet
    {
        public string Name { get; set; }
        public string Title { get; set; }
        public List<string> Notes { get; } = new List<string>();
        public List<string> Headers { get; } = new List<string>();
        public List<IReadOnlyList<ReportCell>> Rows { get; } = new List<IReadOnlyList<ReportCell>>();
        public List<double> ColumnWidths { get; } = new List<double>();
        public bool FreezeHeader { get; set; } = true;
        public bool AutoFilter { get; set; } = true;
        public bool Landscape { get; set; } = true;
    }

    public sealed class SpreadsheetReport
    {
        public string Title { get; set; } = "MiningVolume 2023";
        public string Creator { get; set; } = "MiningVolume 2023";
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public List<ReportSheet> Sheets { get; } = new List<ReportSheet>();
    }

    /// <summary>
    /// Lightweight XLSX writer without Excel COM or third-party spreadsheet runtime.
    /// The generated workbook uses Arial 12, no background fills, and thin borders.
    /// </summary>
    public static class SimpleXlsxWriter
    {
        private const int StyleText = 0;
        private const int StyleTitle = 1;
        private const int StyleNote = 2;
        private const int StyleHeader = 3;
        private const int StyleBodyText = 4;
        private const int StyleBodyInteger = 5;
        private const int StyleBodyNumber2 = 6;
        private const int StyleBodyNumber3 = 7;
        private const int StyleBodyDate = 8;
        private const int StyleBodyTextBold = 9;
        private const int StyleBodyIntegerBold = 10;
        private const int StyleBodyNumber2Bold = 11;
        private const int StyleBodyNumber3Bold = 12;

        public static void Write(string filePath, SpreadsheetReport report)
        {
            if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentException("Đường dẫn file Excel không hợp lệ.", nameof(filePath));
            if (report == null) throw new ArgumentNullException(nameof(report));
            if (report.Sheets.Count == 0) throw new InvalidOperationException("Báo cáo chưa có sheet để xuất.");

            var dir = Path.GetDirectoryName(Path.GetFullPath(filePath));
            if (!string.IsNullOrWhiteSpace(dir)) Directory.CreateDirectory(dir);
            if (File.Exists(filePath)) File.Delete(filePath);

            var normalizedNames = NormalizeSheetNames(report.Sheets.Select(x => x.Name));
            using (var fs = new FileStream(filePath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
            using (var zip = new ZipArchive(fs, ZipArchiveMode.Create, leaveOpen: false, entryNameEncoding: Encoding.UTF8))
            {
                WriteContentTypes(zip, report.Sheets.Count);
                WriteRootRelationships(zip);
                WriteCoreProperties(zip, report);
                WriteAppProperties(zip, normalizedNames);
                WriteWorkbook(zip, normalizedNames);
                WriteWorkbookRelationships(zip, report.Sheets.Count);
                WriteStyles(zip);
                for (int i = 0; i < report.Sheets.Count; i++)
                    WriteWorksheet(zip, i + 1, report.Sheets[i], normalizedNames[i]);
            }
        }

        private static List<string> NormalizeSheetNames(IEnumerable<string> names)
        {
            var result = new List<string>();
            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var raw in names)
            {
                var name = string.IsNullOrWhiteSpace(raw) ? "Sheet" : raw.Trim();
                foreach (var bad in new[] { ':', '\\', '/', '?', '*', '[', ']' }) name = name.Replace(bad, '_');
                if (name.Length > 31) name = name.Substring(0, 31);
                var baseName = name;
                var n = 2;
                while (used.Contains(name))
                {
                    var suffix = "_" + n.ToString(CultureInfo.InvariantCulture);
                    var keep = Math.Max(1, 31 - suffix.Length);
                    name = (baseName.Length > keep ? baseName.Substring(0, keep) : baseName) + suffix;
                    n++;
                }
                used.Add(name);
                result.Add(name);
            }
            return result;
        }

        private static void WriteContentTypes(ZipArchive zip, int sheetCount)
        {
            using (var x = CreateXml(zip, "[Content_Types].xml"))
            {
                x.WriteStartDocument(true);
                x.WriteStartElement("Types", "http://schemas.openxmlformats.org/package/2006/content-types");
                WriteDefault(x, "rels", "application/vnd.openxmlformats-package.relationships+xml");
                WriteDefault(x, "xml", "application/xml");
                WriteOverride(x, "/xl/workbook.xml", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml");
                WriteOverride(x, "/xl/styles.xml", "application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml");
                WriteOverride(x, "/docProps/core.xml", "application/vnd.openxmlformats-package.core-properties+xml");
                WriteOverride(x, "/docProps/app.xml", "application/vnd.openxmlformats-officedocument.extended-properties+xml");
                for (int i = 1; i <= sheetCount; i++)
                    WriteOverride(x, "/xl/worksheets/sheet" + i.ToString(CultureInfo.InvariantCulture) + ".xml", "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml");
                x.WriteEndElement();
            }
        }

        private static void WriteDefault(XmlWriter x, string extension, string type)
        {
            x.WriteStartElement("Default"); x.WriteAttributeString("Extension", extension); x.WriteAttributeString("ContentType", type); x.WriteEndElement();
        }
        private static void WriteOverride(XmlWriter x, string part, string type)
        {
            x.WriteStartElement("Override"); x.WriteAttributeString("PartName", part); x.WriteAttributeString("ContentType", type); x.WriteEndElement();
        }

        private static void WriteRootRelationships(ZipArchive zip)
        {
            using (var x = CreateXml(zip, "_rels/.rels"))
            {
                x.WriteStartDocument(true);
                x.WriteStartElement("Relationships", "http://schemas.openxmlformats.org/package/2006/relationships");
                WriteRelationship(x, "rId1", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument", "xl/workbook.xml");
                WriteRelationship(x, "rId2", "http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties", "docProps/core.xml");
                WriteRelationship(x, "rId3", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/extended-properties", "docProps/app.xml");
                x.WriteEndElement();
            }
        }

        private static void WriteCoreProperties(ZipArchive zip, SpreadsheetReport report)
        {
            using (var x = CreateXml(zip, "docProps/core.xml"))
            {
                x.WriteStartDocument(true);
                x.WriteStartElement("cp", "coreProperties", "http://schemas.openxmlformats.org/package/2006/metadata/core-properties");
                x.WriteAttributeString("xmlns", "dc", null, "http://purl.org/dc/elements/1.1/");
                x.WriteAttributeString("xmlns", "dcterms", null, "http://purl.org/dc/terms/");
                x.WriteAttributeString("xmlns", "xsi", null, "http://www.w3.org/2001/XMLSchema-instance");
                x.WriteElementString("dc", "title", "http://purl.org/dc/elements/1.1/", report.Title ?? "MiningVolume 2023");
                x.WriteElementString("dc", "creator", "http://purl.org/dc/elements/1.1/", report.Creator ?? "MiningVolume 2023");
                x.WriteStartElement("dcterms", "created", "http://purl.org/dc/terms/");
                x.WriteAttributeString("xsi", "type", "http://www.w3.org/2001/XMLSchema-instance", "dcterms:W3CDTF");
                x.WriteString(report.CreatedAt.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture));
                x.WriteEndElement();
                x.WriteEndElement();
            }
        }

        private static void WriteAppProperties(ZipArchive zip, IReadOnlyList<string> sheetNames)
        {
            using (var x = CreateXml(zip, "docProps/app.xml"))
            {
                x.WriteStartDocument(true);
                x.WriteStartElement("Properties", "http://schemas.openxmlformats.org/officeDocument/2006/extended-properties");
                x.WriteAttributeString("xmlns", "vt", null, "http://schemas.openxmlformats.org/officeDocument/2006/docPropsVTypes");
                x.WriteElementString("Application", "MiningVolume 2023");
                x.WriteStartElement("TitlesOfParts");
                x.WriteStartElement("vt", "vector", "http://schemas.openxmlformats.org/officeDocument/2006/docPropsVTypes");
                x.WriteAttributeString("size", sheetNames.Count.ToString(CultureInfo.InvariantCulture));
                x.WriteAttributeString("baseType", "lpstr");
                foreach (var name in sheetNames) x.WriteElementString("vt", "lpstr", "http://schemas.openxmlformats.org/officeDocument/2006/docPropsVTypes", name);
                x.WriteEndElement(); x.WriteEndElement();
                x.WriteEndElement();
            }
        }

        private static void WriteWorkbook(ZipArchive zip, IReadOnlyList<string> sheetNames)
        {
            using (var x = CreateXml(zip, "xl/workbook.xml"))
            {
                x.WriteStartDocument(true);
                x.WriteStartElement("workbook", "http://schemas.openxmlformats.org/spreadsheetml/2006/main");
                x.WriteAttributeString("xmlns", "r", null, "http://schemas.openxmlformats.org/officeDocument/2006/relationships");
                x.WriteStartElement("sheets");
                for (int i = 0; i < sheetNames.Count; i++)
                {
                    x.WriteStartElement("sheet");
                    x.WriteAttributeString("name", sheetNames[i]);
                    x.WriteAttributeString("sheetId", (i + 1).ToString(CultureInfo.InvariantCulture));
                    x.WriteAttributeString("r", "id", "http://schemas.openxmlformats.org/officeDocument/2006/relationships", "rId" + (i + 1).ToString(CultureInfo.InvariantCulture));
                    x.WriteEndElement();
                }
                x.WriteEndElement(); x.WriteEndElement();
            }
        }

        private static void WriteWorkbookRelationships(ZipArchive zip, int sheetCount)
        {
            using (var x = CreateXml(zip, "xl/_rels/workbook.xml.rels"))
            {
                x.WriteStartDocument(true);
                x.WriteStartElement("Relationships", "http://schemas.openxmlformats.org/package/2006/relationships");
                for (int i = 1; i <= sheetCount; i++)
                    WriteRelationship(x, "rId" + i.ToString(CultureInfo.InvariantCulture), "http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet", "worksheets/sheet" + i.ToString(CultureInfo.InvariantCulture) + ".xml");
                WriteRelationship(x, "rId" + (sheetCount + 1).ToString(CultureInfo.InvariantCulture), "http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles", "styles.xml");
                x.WriteEndElement();
            }
        }

        private static void WriteRelationship(XmlWriter x, string id, string type, string target)
        {
            x.WriteStartElement("Relationship"); x.WriteAttributeString("Id", id); x.WriteAttributeString("Type", type); x.WriteAttributeString("Target", target); x.WriteEndElement();
        }

        private static void WriteStyles(ZipArchive zip)
        {
            using (var x = CreateXml(zip, "xl/styles.xml"))
            {
                x.WriteStartDocument(true);
                x.WriteStartElement("styleSheet", "http://schemas.openxmlformats.org/spreadsheetml/2006/main");

                x.WriteStartElement("numFmts"); x.WriteAttributeString("count", "3");
                NumFmt(x, 164, "0"); NumFmt(x, 165, "0.00"); NumFmt(x, 166, "0.000"); x.WriteEndElement();

                x.WriteStartElement("fonts"); x.WriteAttributeString("count", "2");
                Font(x, false); Font(x, true); x.WriteEndElement();

                x.WriteStartElement("fills"); x.WriteAttributeString("count", "2");
                x.WriteStartElement("fill"); x.WriteStartElement("patternFill"); x.WriteAttributeString("patternType", "none"); x.WriteEndElement(); x.WriteEndElement();
                x.WriteStartElement("fill"); x.WriteStartElement("patternFill"); x.WriteAttributeString("patternType", "gray125"); x.WriteEndElement(); x.WriteEndElement();
                x.WriteEndElement();

                x.WriteStartElement("borders"); x.WriteAttributeString("count", "2"); Border(x, false); Border(x, true); x.WriteEndElement();
                x.WriteStartElement("cellStyleXfs"); x.WriteAttributeString("count", "1"); Xf(x, 0, 0, 0, 0, false, null); x.WriteEndElement();

                x.WriteStartElement("cellXfs"); x.WriteAttributeString("count", "13");
                Xf(x, 0, 0, 0, 0, false, null);                                    // 0
                Xf(x, 0, 1, 0, 1, true, "center");                                 // 1 title
                Xf(x, 0, 0, 0, 0, false, "left");                                  // 2 note
                Xf(x, 0, 1, 0, 1, true, "center");                                 // 3 header
                Xf(x, 0, 0, 0, 1, true, "left");                                   // 4 text
                Xf(x, 164, 0, 0, 1, true, null);                                    // 5 int
                Xf(x, 165, 0, 0, 1, true, null);                                    // 6 n2
                Xf(x, 166, 0, 0, 1, true, null);                                    // 7 n3
                Xf(x, 14, 0, 0, 1, true, null);                                     // 8 date
                Xf(x, 0, 1, 0, 1, true, "left");                                   // 9 text bold
                Xf(x, 164, 1, 0, 1, true, null);                                    // 10 int bold
                Xf(x, 165, 1, 0, 1, true, null);                                    // 11 n2 bold
                Xf(x, 166, 1, 0, 1, true, null);                                    // 12 n3 bold
                x.WriteEndElement();

                x.WriteStartElement("cellStyles"); x.WriteAttributeString("count", "1");
                x.WriteStartElement("cellStyle"); x.WriteAttributeString("name", "Normal"); x.WriteAttributeString("xfId", "0"); x.WriteAttributeString("builtinId", "0"); x.WriteEndElement(); x.WriteEndElement();
                x.WriteEndElement();
            }
        }

        private static void NumFmt(XmlWriter x, int id, string code)
        {
            x.WriteStartElement("numFmt"); x.WriteAttributeString("numFmtId", id.ToString(CultureInfo.InvariantCulture)); x.WriteAttributeString("formatCode", code); x.WriteEndElement();
        }
        private static void Font(XmlWriter x, bool bold)
        {
            x.WriteStartElement("font"); if (bold) x.WriteElementString("b", string.Empty); x.WriteStartElement("sz"); x.WriteAttributeString("val", "12"); x.WriteEndElement(); x.WriteStartElement("name"); x.WriteAttributeString("val", "Arial"); x.WriteEndElement(); x.WriteStartElement("family"); x.WriteAttributeString("val", "2"); x.WriteEndElement(); x.WriteEndElement();
        }
        private static void Border(XmlWriter x, bool thin)
        {
            x.WriteStartElement("border");
            foreach (var side in new[] { "left", "right", "top", "bottom" }) { x.WriteStartElement(side); if (thin) x.WriteAttributeString("style", "thin"); x.WriteEndElement(); }
            x.WriteElementString("diagonal", string.Empty); x.WriteEndElement();
        }
        private static void Xf(XmlWriter x, int numFmt, int font, int fill, int border, bool wrap, string hAlign)
        {
            x.WriteStartElement("xf"); x.WriteAttributeString("numFmtId", numFmt.ToString(CultureInfo.InvariantCulture)); x.WriteAttributeString("fontId", font.ToString(CultureInfo.InvariantCulture)); x.WriteAttributeString("fillId", fill.ToString(CultureInfo.InvariantCulture)); x.WriteAttributeString("borderId", border.ToString(CultureInfo.InvariantCulture)); x.WriteAttributeString("xfId", "0");
            if (numFmt != 0) x.WriteAttributeString("applyNumberFormat", "1"); if (font != 0) x.WriteAttributeString("applyFont", "1"); if (border != 0) x.WriteAttributeString("applyBorder", "1");
            if (wrap || hAlign != null) { x.WriteAttributeString("applyAlignment", "1"); x.WriteStartElement("alignment"); if (wrap) x.WriteAttributeString("wrapText", "1"); if (hAlign != null) x.WriteAttributeString("horizontal", hAlign); x.WriteAttributeString("vertical", "center"); x.WriteEndElement(); }
            x.WriteEndElement();
        }

        private static void WriteWorksheet(ZipArchive zip, int sheetIndex, ReportSheet sheet, string normalizedName)
        {
            int colCount = Math.Max(1, Math.Max(sheet.Headers.Count, Math.Max(sheet.ColumnWidths.Count, sheet.Rows.Count == 0 ? 0 : sheet.Rows.Max(r => r?.Count ?? 0))));
            int titleRows = string.IsNullOrWhiteSpace(sheet.Title) ? 0 : 1;
            int noteRows = sheet.Notes.Count;
            int headerRow = sheet.Headers.Count > 0 ? titleRows + noteRows + 1 : 0;
            int firstDataRow = titleRows + noteRows + (sheet.Headers.Count > 0 ? 1 : 0) + 1;
            int lastDataRow = firstDataRow + Math.Max(0, sheet.Rows.Count - 1);

            using (var x = CreateXml(zip, "xl/worksheets/sheet" + sheetIndex.ToString(CultureInfo.InvariantCulture) + ".xml"))
            {
                x.WriteStartDocument(true);
                x.WriteStartElement("worksheet", "http://schemas.openxmlformats.org/spreadsheetml/2006/main");
                x.WriteStartElement("sheetViews"); x.WriteStartElement("sheetView"); x.WriteAttributeString("workbookViewId", "0");
                if (sheet.FreezeHeader && headerRow > 0)
                {
                    x.WriteStartElement("pane"); x.WriteAttributeString("ySplit", headerRow.ToString(CultureInfo.InvariantCulture)); x.WriteAttributeString("topLeftCell", "A" + (headerRow + 1).ToString(CultureInfo.InvariantCulture)); x.WriteAttributeString("activePane", "bottomLeft"); x.WriteAttributeString("state", "frozen"); x.WriteEndElement();
                }
                x.WriteEndElement(); x.WriteEndElement();
                x.WriteStartElement("sheetFormatPr"); x.WriteAttributeString("defaultRowHeight", "18"); x.WriteEndElement();

                x.WriteStartElement("cols");
                for (int c = 1; c <= colCount; c++)
                {
                    double width = c <= sheet.ColumnWidths.Count ? sheet.ColumnWidths[c - 1] : 15.0;
                    if (width < 6) width = 6; if (width > 45) width = 45;
                    x.WriteStartElement("col"); x.WriteAttributeString("min", c.ToString(CultureInfo.InvariantCulture)); x.WriteAttributeString("max", c.ToString(CultureInfo.InvariantCulture)); x.WriteAttributeString("width", width.ToString("0.##", CultureInfo.InvariantCulture)); x.WriteAttributeString("customWidth", "1"); x.WriteEndElement();
                }
                x.WriteEndElement();

                x.WriteStartElement("sheetData");
                int row = 1;
                if (!string.IsNullOrWhiteSpace(sheet.Title))
                {
                    WriteRowStart(x, row, 24); WriteCell(x, 1, row, ReportCell.Text(sheet.Title, true), StyleTitle); x.WriteEndElement(); row++;
                }
                foreach (var note in sheet.Notes)
                {
                    WriteRowStart(x, row, 20); WriteCell(x, 1, row, ReportCell.Text(note), StyleNote); x.WriteEndElement(); row++;
                }
                if (sheet.Headers.Count > 0)
                {
                    WriteRowStart(x, row, 30);
                    for (int c = 0; c < sheet.Headers.Count; c++) WriteCell(x, c + 1, row, ReportCell.Text(sheet.Headers[c], true), StyleHeader);
                    x.WriteEndElement(); row++;
                }
                foreach (var dataRow in sheet.Rows)
                {
                    WriteRowStart(x, row, 20);
                    if (dataRow != null) for (int c = 0; c < dataRow.Count; c++) WriteCell(x, c + 1, row, dataRow[c] ?? ReportCell.Text(string.Empty), StyleFor(dataRow[c]));
                    x.WriteEndElement(); row++;
                }
                x.WriteEndElement();

                // OOXML worksheet schema requires autoFilter before mergeCells.
                // Writing these in the opposite order can make Excel repair/reject the workbook.
                if (sheet.AutoFilter && sheet.Headers.Count > 0 && lastDataRow >= headerRow)
                {
                    x.WriteStartElement("autoFilter"); x.WriteAttributeString("ref", "A" + headerRow.ToString(CultureInfo.InvariantCulture) + ":" + ColumnName(colCount) + Math.Max(headerRow, lastDataRow).ToString(CultureInfo.InvariantCulture)); x.WriteEndElement();
                }
                if (colCount > 1 && titleRows + noteRows > 0)
                {
                    x.WriteStartElement("mergeCells"); x.WriteAttributeString("count", (titleRows + noteRows).ToString(CultureInfo.InvariantCulture));
                    int mr = 1;
                    if (titleRows > 0) { Merge(x, mr, colCount); mr++; }
                    for (int i = 0; i < noteRows; i++, mr++) Merge(x, mr, colCount);
                    x.WriteEndElement();
                }
                x.WriteStartElement("pageMargins"); x.WriteAttributeString("left", "0.3"); x.WriteAttributeString("right", "0.3"); x.WriteAttributeString("top", "0.5"); x.WriteAttributeString("bottom", "0.5"); x.WriteAttributeString("header", "0.2"); x.WriteAttributeString("footer", "0.2"); x.WriteEndElement();
                x.WriteStartElement("pageSetup"); x.WriteAttributeString("orientation", sheet.Landscape ? "landscape" : "portrait"); x.WriteAttributeString("fitToWidth", "1"); x.WriteAttributeString("fitToHeight", "0"); x.WriteEndElement();
                x.WriteEndElement();
            }
        }

        private static void Merge(XmlWriter x, int row, int colCount)
        {
            x.WriteStartElement("mergeCell"); x.WriteAttributeString("ref", "A" + row.ToString(CultureInfo.InvariantCulture) + ":" + ColumnName(colCount) + row.ToString(CultureInfo.InvariantCulture)); x.WriteEndElement();
        }
        private static void WriteRowStart(XmlWriter x, int row, int height)
        {
            x.WriteStartElement("row"); x.WriteAttributeString("r", row.ToString(CultureInfo.InvariantCulture)); x.WriteAttributeString("ht", height.ToString(CultureInfo.InvariantCulture)); x.WriteAttributeString("customHeight", "1");
        }

        private static void WriteCell(XmlWriter x, int col, int row, ReportCell cell, int style)
        {
            if (cell == null) cell = ReportCell.Text(string.Empty);
            x.WriteStartElement("c"); x.WriteAttributeString("r", ColumnName(col) + row.ToString(CultureInfo.InvariantCulture)); x.WriteAttributeString("s", style.ToString(CultureInfo.InvariantCulture));
            if (cell.Value == null) { x.WriteEndElement(); return; }
            if (cell.Kind == ReportCellKind.Text)
            {
                x.WriteAttributeString("t", "inlineStr"); x.WriteStartElement("is"); x.WriteStartElement("t"); x.WriteAttributeString("xml", "space", "http://www.w3.org/XML/1998/namespace", "preserve"); x.WriteString(Convert.ToString(cell.Value, CultureInfo.CurrentCulture) ?? string.Empty); x.WriteEndElement(); x.WriteEndElement();
            }
            else if (cell.Kind == ReportCellKind.DateTime)
            {
                var dt = cell.Value is DateTime d ? d : Convert.ToDateTime(cell.Value, CultureInfo.CurrentCulture);
                x.WriteElementString("v", dt.ToOADate().ToString("0.###############", CultureInfo.InvariantCulture));
            }
            else
            {
                var number = Convert.ToDouble(cell.Value, CultureInfo.InvariantCulture);
                x.WriteElementString("v", number.ToString("0.###############", CultureInfo.InvariantCulture));
            }
            x.WriteEndElement();
        }

        private static int StyleFor(ReportCell cell)
        {
            if (cell == null) return StyleBodyText;
            if (cell.Bold)
            {
                switch (cell.Kind)
                {
                    case ReportCellKind.Integer: return StyleBodyIntegerBold;
                    case ReportCellKind.Number2: return StyleBodyNumber2Bold;
                    case ReportCellKind.Number3: return StyleBodyNumber3Bold;
                    default: return StyleBodyTextBold;
                }
            }
            switch (cell.Kind)
            {
                case ReportCellKind.Integer: return StyleBodyInteger;
                case ReportCellKind.Number2: return StyleBodyNumber2;
                case ReportCellKind.Number3: return StyleBodyNumber3;
                case ReportCellKind.DateTime: return StyleBodyDate;
                default: return StyleBodyText;
            }
        }

        private static string ColumnName(int index)
        {
            if (index <= 0) return "A";
            var s = string.Empty;
            while (index > 0) { index--; s = (char)('A' + index % 26) + s; index /= 26; }
            return s;
        }

        private static XmlWriter CreateXml(ZipArchive zip, string path)
        {
            var entry = zip.CreateEntry(path, CompressionLevel.Optimal);
            var stream = entry.Open();
            return XmlWriter.Create(stream, new XmlWriterSettings { Encoding = new UTF8Encoding(false), Indent = false, CloseOutput = true, OmitXmlDeclaration = false });
        }
    }
}