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
        // Verify the three Quick Access QAction registrations on REAL native Qt;
        // a merely rendered label is not evidence of a working CAD command.
        var quickAccess = IMSATCAD.getQuickAccessToolBar();
        note("QuickAccessToolbar", !isNull(quickAccess));
        if (isNull(quickAccess)) { throw new Error("Native Quick Access toolbar unavailable"); }
        var quickActions = IMSATCAD._quickAccessScripts;
        var confirmedQuickActions = 0;
        for (var qa = 0; qa < quickActions.length; ++qa) {
            if (IMSATCAD._installedActions["IMSATCADQuickAccessToolBar|" + quickActions[qa]] !== true) {
                throw new Error("Native Quick Access action not installed: " + quickActions[qa]);
            }
            confirmedQuickActions++;
        }
        note("QuickAccessNativeActions", confirmedQuickActions);
        if (confirmedQuickActions !== 3) { throw new Error("Expected Save, Undo, Redo native actions"); }
        note("MainWindow", !isNull(app));
        if (isNull(app)) { throw new Error("Native main window unavailable"); }
        // Exercise both locales against REAL QCAD Qt widgets, not the Node mock.
        var selector = IMSATCAD._ribbonLanguageButton;
        note("LanguageSelector", !isNull(selector));
        if (isNull(selector) || String(selector.objectName) !== "IMSATCADRibbonLanguageSwitch") {
            throw new Error("Native Ribbon language selector ID missing");
        }
        var firstButton = IMSATCAD._ribbonButtonCaptions[0].widget;
        note("StableActionControl", !isNull(firstButton) && String(firstButton.objectName).indexOf("IMSATCADRibbonAction_") === 0);
        if (isNull(firstButton) || String(firstButton.objectName).indexOf("IMSATCADRibbonAction_") !== 0) {
            throw new Error("Native Ribbon command control ID missing");
        }
        if (!IMSATCAD.setRibbonLanguage("en")) { throw new Error("Native English Ribbon switch failed"); }
        var english = ["Home", "Insert", "Annotate", "View", "Manage", "Output"];
        var actualEnglish = [];
        for (var e = 0; e < count; e++) { actualEnglish.push(String(tabs.tabText(e))); }
        note("EnglishRibbonTabs", actualEnglish.join("|"));
        for (var et = 0; et < english.length; et++) {
            if (actualEnglish[et] !== english[et]) {
                throw new Error("Native English Ribbon tab mismatch at " + et);
            }
        }
        if (!IMSATCAD.setRibbonLanguage("vi")) { throw new Error("Native Vietnamese Ribbon restore failed"); }
        var restored = [];
        for (var v = 0; v < count; v++) { restored.push(String(tabs.tabText(v))); }
        note("VietnameseRibbonRestored", restored.join("|"));
        for (var vt = 0; vt < expected.length; vt++) {
            if (restored[vt] !== expected[vt]) {
                throw new Error("Native Vietnamese Ribbon restore mismatch at " + vt);
            }
        }
        note("BilingualNativeQtSwitch", "PASS");
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
