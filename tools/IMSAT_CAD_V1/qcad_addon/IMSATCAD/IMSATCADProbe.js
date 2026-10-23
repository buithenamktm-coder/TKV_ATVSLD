/* IMSAT CAD native Windows acceptance probe.
 * Executed by QCAD -exec after add-on postInit; it tests real Qt widgets,
 * not the mocked JavaScript runtime. QCAD CE remains GPLv3.
 */
(function() {
    var findings = [];
    var status = "FAIL";
    function note(key, value) { findings.push(key + "=" + String(value)); }
    try {
        var app = RMainWindowQt.getMainWindow();
        var loaded = typeof IMSATCAD !== "undefined";
        note("AddonLoaded", loaded);
        if (!loaded) { throw new Error("IMSATCAD add-on was not initialized"); }
        note("RibbonCreated", IMSATCAD._ribbonCreated === true);
        if (IMSATCAD._ribbonCreated !== true) {
            throw new Error("Native Ribbon was not created");
        }
        var tabs = IMSATCAD._ribbonTabsWidget;
        if (isNull(tabs)) { throw new Error("Native Ribbon tabs are null"); }
        var count = IMSATCAD.ribbonTabCount(tabs);
        note("RibbonTabCount", count);
        if (count !== 6) { throw new Error("Expected 6 native Ribbon tabs"); }
        if (typeof tabs.tabText !== "function") {
            throw new Error("Qt bridge does not expose native tabText");
        }
        var expected = ["Trang chủ", "Chèn", "Ghi chú", "Hiển thị", "Quản lý", "Xuất bản"];
        var actual = [];
        for (var i = 0; i < count; i++) { actual.push(String(tabs.tabText(i))); }
        note("RibbonTabLabels", actual.join("|"));
        for (var t = 0; t < expected.length; t++) {
            if (actual[t] !== expected[t]) {
                throw new Error("Unexpected Ribbon tab " + t + ": " + actual[t]);
            }
        }
        var registered = Object.keys(IMSATCAD._registeredAliases).length;
        note("RegisteredAliasActions", registered);
        if (registered < 25) { throw new Error("Too few native CAD aliases registered"); }
        var line = RGuiAction.getByScriptFile("scripts/Draw/Line/Line2P/Line2P.js");
        note("NativeLineAction", !isNull(line));
        if (isNull(line)) { throw new Error("Native LINE action unavailable"); }
        note("MainWindow", !isNull(app));
        if (isNull(app)) { throw new Error("Native main window unavailable"); }
        status = "PASS";
    } catch (error) {
        note("Failure", String(error).replace(/[\r\n]+/g, " "));
    }
    findings.unshift("Status=" + status);
    var target = QDir.tempPath() + "/imsat-cad-native-probe.txt";
    var file = new QFile(target);
    if (!file.open(makeQIODeviceOpenMode(QIODevice.WriteOnly, QIODevice.Text))) {
        qWarning("IMSAT CAD native probe could not write " + target);
        return;
    }
    var stream = new QTextStream(file);
    for (var j = 0; j < findings.length; j++) {
        stream.writeString(findings[j] + "\n");
    }
    destr(stream);
    file.close();
})();
