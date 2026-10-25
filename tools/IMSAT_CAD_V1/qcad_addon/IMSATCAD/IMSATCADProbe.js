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
        // Native GUI document and real QCAD LINE tool action. Coordinates are
        // dispatched as QCAD coordinate events (not physical mouse clicks).
        include("scripts/File/NewFile/NewFile.js");
        include("scripts/simple.js");
        var mdi = NewFile.createMdiChild();
        if (isNull(mdi)) { throw new Error("Native GUI document creation failed"); }
        var drawing = app.getDocumentInterface();
        if (isNull(drawing)) { throw new Error("No active native CAD GUI document"); }
        function countLines(doc) {
            var ids = doc.queryAllEntities();
            var n = typeof ids.length === "number" ? ids.length :
                (typeof ids.size === "function" ? ids.size() : -1);
            if (n < 0) { throw new Error("Native GUI entity collection not countable"); }
            var lines = 0;
            for (var k = 0; k < n; ++k) {
                var entity = doc.queryEntity(ids[k]);
                if (!isNull(entity) && entity.getType() === RS.EntityLine) { lines++; }
            }
            return lines;
        }
        var initialLines = countLines(drawing.getDocument());
        note("GuiLineCountBefore", initialLines);
        if (!RGuiAction.triggerByCommand("line")) {
            throw new Error("Native LINE alias failed to activate the GUI CAD tool");
        }
        QCoreApplication.processEvents();
        var view = drawing.getLastKnownViewWithFocus();
        if (isNull(view) || isNull(view.getScene())) {
            throw new Error("No active CAD viewport to dispatch LINE coordinates");
        }
        var scene = view.getScene();
        var graphicsView = getRGraphicsView(view);
        drawing.coordinateEvent(new RCoordinateEvent(new RVector(10, 20), scene, graphicsView));
        drawing.coordinateEvent(new RCoordinateEvent(new RVector(90, 50), scene, graphicsView));
        var createdLines = countLines(drawing.getDocument());
        note("GuiLineCountAfter", createdLines);
        if (createdLines !== initialLines + 1) {
            throw new Error("Native GUI LINE command did not create exactly one line");
        }
        var guiDXF = QDir.tempPath() + "/imsat-cad-gui-line-roundtrip.dxf";
        var previous = new QFile(guiDXF);
        if (previous.exists() && !previous.remove()) { throw new Error("Stale GUI DXF could not be cleared"); }
        if (drawing.exportFile(guiDXF, "") !== true) { throw new Error("Native GUI LINE DXF export failed"); }
        var reopened = createDocumentInterface();
        if (isNull(reopened) ||
                reopened.importFile(guiDXF) !== RDocumentInterface.IoErrorNoError) {
            throw new Error("Native GUI LINE DXF reimport failed");
        }
        var reopenedLines = countLines(reopened.getDocument());
        note("GuiDXFReopenedLines", reopenedLines);
        note("GuiDXFPath", guiDXF);
        if (reopenedLines !== createdLines) { throw new Error("Native GUI LINE DXF line count changed"); }
        note("NativeGuiLineActionGeometry", "PASS");
        note("NativeGuiLineDXFRoundtrip", "PASS");
        // The real GUI LINE geometry test above leaves LINE active.
        // Terminate it before verifying independent command dispatch aliases.
        drawing.terminateCurrentAction();
        var aliasChecks = [
            { command: "l", script: "scripts/Draw/Line/Line2P/Line2P.js", suffix: "Line2P.js" },
            { command: "c", script: "scripts/Draw/Circle/CircleCR/CircleCR.js", suffix: "CircleCR.js" }
        ];
        for (var ac = 0; ac < aliasChecks.length; ++ac) {
            var check = aliasChecks[ac];
            var guiAction = RGuiAction.getByScriptFile(check.script);
            if (isNull(guiAction) || !guiAction.isEnabled()) {
                throw new Error("Native CAD alias action unavailable: " + check.command);
            }
            var registeredCommands = guiAction.getCommands();
            if (isNull(registeredCommands) || !registeredCommands.contains(check.command)) {
                throw new Error("Native CAD alias not registered: " + check.command);
            }
            if (!RGuiAction.triggerByCommand(check.command)) {
                throw new Error("Native CAD alias did not dispatch: " + check.command);
            }
            QCoreApplication.processEvents();
            var active = drawing.getCurrentAction();
            if (isNull(active) || isNull(active.getGuiAction())) {
                throw new Error("Native CAD command has no active action: " + check.command);
            }
            var activeScript = String(active.getGuiAction().getScriptFile());
            note("Alias_" + check.command + "_ActiveScript", activeScript);
            if (activeScript.indexOf(check.suffix) < 0) {
                throw new Error("Wrong native action for alias " + check.command + ": " + activeScript);
            }
            drawing.terminateCurrentAction();
        }
        note("NativeAliasDispatch", "PASS");
        // Expose QCAD's native command input to Windows UI Automation.
        var commandDock = app.findChild("CommandLineDock");
        var commandBox = !isNull(commandDock) ? commandDock.findChild("CommandEdit") : null;
        var commandReady = !isNull(commandDock) && !isNull(commandBox);
        note("NativeGuiCanvasCoordinateSource", "WindowsUIAutomation");
        note("NativeCommandInputReady", commandReady);
        if (commandReady) {
            commandDock.visible = true;
            commandBox.clear();
            commandBox.setFocus();
        }
        note("NativeSpaceCommandKey", "NOT_TESTED");
        note("PhysicalMouseAndKeyboard", "NOT_TESTED");
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
