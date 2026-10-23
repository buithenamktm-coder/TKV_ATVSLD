/* IMSAT CAD native geometry and DXF persistence acceptance test.
 * Executes in the installed QCAD CE v3.32.2.0 JavaScript runtime, never Node.
 * This is original IMSAT integration test code; QCAD CE remains GPLv3.
 * Scope: off-screen native CAD geometry + save/reimport, NOT GUI mouse drawing.
 */
(function() {
    var findings = [];
    var status = "FAIL";
    var reportPath = QDir.tempPath() + "/imsat-cad-dxf-roundtrip-probe.txt";
    var dxfPath = QDir.tempPath() + "/imsat-cad-dxf-roundtrip.dxf";
    function note(key, value) { findings.push(key + "=" + String(value)); }
    function inspectEntities(document) {
        var ids = document.queryAllEntities();
        if (isNull(ids)) { throw new Error("Native document entity query returned null"); }
        var count = typeof ids.length === "number" ? ids.length :
            (typeof ids.size === "function" ? ids.size() : -1);
        if (count < 0) { throw new Error("Native entity ID collection is not countable"); }
        var lineCount = 0;
        var circleCount = 0;
        for (var i = 0; i < count; ++i) {
            var entity = document.queryEntity(ids[i]);
            if (isNull(entity)) { throw new Error("Native entity lookup failed at index " + i); }
            if (entity.getType() === RS.EntityLine) { lineCount++; }
            if (entity.getType() === RS.EntityCircle) { circleCount++; }
        }
        return { count: count, lines: lineCount, circles: circleCount };
    }
    try {
        include("scripts/simple.js");
        if (typeof createDocumentInterface !== "function" ||
                typeof startTransaction !== "function" ||
                typeof addLine !== "function" ||
                typeof addCircle !== "function") {
            throw new Error("Pinned QCAD simple CAD creation API unavailable");
        }
        var nativeDocument = createDocumentInterface();
        if (isNull(nativeDocument)) { throw new Error("Cannot create native off-screen CAD document"); }
        startTransaction(nativeDocument);
        addLine(0, 0, 100, 0);
        addCircle(50, 30, 12);
        endTransaction();
        var before = inspectEntities(nativeDocument.getDocument());
        note("NativeCreatedEntities", before.count);
        note("NativeCreatedLines", before.lines);
        note("NativeCreatedCircles", before.circles);
        if (before.count !== 2 || before.lines !== 1 || before.circles !== 1) {
            throw new Error("Native geometry mismatch before export");
        }
        var oldFile = new QFile(dxfPath);
        if (oldFile.exists() && !oldFile.remove()) {
            throw new Error("Cannot remove stale DXF round-trip result");
        }
        if (nativeDocument.exportFile(dxfPath, "") !== true) {
            throw new Error("Native QCAD DXF exporter failed");
        }
        var dxf = new QFileInfo(dxfPath);
        note("DXFPath", dxfPath);
        note("DXFBytes", dxf.size());
        if (!dxf.exists() || dxf.size() < 200) {
            throw new Error("Exported native DXF missing or unexpectedly small");
        }
        var reopened = createDocumentInterface();
        if (isNull(reopened)) { throw new Error("Cannot create DXF reopen document"); }
        var importResult = reopened.importFile(dxfPath);
        note("NativeDXFImportResult", importResult);
        if (importResult !== RDocumentInterface.IoErrorNoError) {
            throw new Error("Native QCAD DXF importer failed with code " + importResult);
        }
        var after = inspectEntities(reopened.getDocument());
        note("DXFReopenedEntities", after.count);
        note("DXFReopenedLines", after.lines);
        note("DXFReopenedCircles", after.circles);
        if (after.count !== 2 || after.lines !== 1 || after.circles !== 1) {
            throw new Error("Native DXF round-trip changed CAD entity types or counts");
        }
        note("NativeCADGeometryAndDXFPersistence", "PASS");
        status = "PASS";
    } catch (error) {
        note("Failure", String(error).replace(/[\r\n]+/g, " "));
        qWarning("IMSAT CAD native DXF probe FAIL: " + error);
    }
    findings.unshift("Status=" + status);
    var report = new QFile(reportPath);
    if (!report.open(makeQIODeviceOpenMode(QIODevice.WriteOnly, QIODevice.Text))) {
        qWarning("IMSAT CAD native DXF probe could not write report: " + reportPath);
        return;
    }
    var writer = new QTextStream(report);
    for (var j = 0; j < findings.length; ++j) {
        writer.writeString(findings[j] + "\n");
    }
    destr(writer);
    report.close();
    qWarning("IMSAT CAD native DXF probe: " + status);
})();
