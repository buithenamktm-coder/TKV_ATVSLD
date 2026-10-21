/* IMSAT CAD — community CAD engine integration prototype.
 * This file is ORIGINAL code. It uses QCAD's documented add-on APIs.
 * QCAD Community Edition itself remains GPLv3 and carries upstream notices.
 * This add-on is not a certified AutoCAD-equivalent implementation.
 */
include("scripts/EAction.js");

function IMSATCAD(guiAction) {
    EAction.call(this, guiAction);
}
IMSATCAD.prototype = new EAction();

IMSATCAD.getMenu = function() {
    return EAction.getMenu("IMSAT CAD", "IMSATCADMenu");
};

IMSATCAD.getDrawToolBar = function() {
    return EAction.getToolBar(qsTr("IMSAT CAD - Draw"), "IMSATCADDrawToolBar", Qt.TopToolBarArea);
};
IMSATCAD.getModifyToolBar = function() {
    return EAction.getToolBar(qsTr("IMSAT CAD - Modify"), "IMSATCADModifyToolBar", Qt.TopToolBarArea);
};
IMSATCAD.getAnnotationToolBar = function() {
    return EAction.getToolBar(qsTr("IMSAT CAD - Annotation"), "IMSATCADAnnotationToolBar", Qt.TopToolBarArea);
};
IMSATCAD.getLayerToolBar = function() {
    return EAction.getToolBar(qsTr("IMSAT CAD - Layers"), "IMSATCADLayerToolBar", Qt.TopToolBarArea);
};
IMSATCAD.getFileToolBar = function() {
    return EAction.getToolBar(qsTr("IMSAT CAD - File"), "IMSATCADFileToolBar", Qt.TopToolBarArea);
};
IMSATCAD.getBlocksToolBar = function() {
    return EAction.getToolBar(qsTr("IMSAT CAD - Blocks"), "IMSATCADBlocksToolBar", Qt.TopToolBarArea);
};
IMSATCAD.getSelectionToolBar = function() {
    return EAction.getToolBar(qsTr("IMSAT CAD - Selection"), "IMSATCADSelectionToolBar", Qt.TopToolBarArea);
};
IMSATCAD.getUtilitiesToolBar = function() {
    return EAction.getToolBar(qsTr("IMSAT CAD - Utilities"), "IMSATCADUtilitiesToolBar", Qt.TopToolBarArea);
};
// IMSAT task-based workspace. Native QCAD toolbars remain dockable;
// their captions and action groups provide a compact ribbon-like layout.
IMSATCAD.configureWorkspace = function() {
    var bars = [
        IMSATCAD.getDrawToolBar(),
        IMSATCAD.getModifyToolBar(),
        IMSATCAD.getAnnotationToolBar(),
        IMSATCAD.getLayerToolBar(),
        IMSATCAD.getUtilitiesToolBar(),
        IMSATCAD.getBlocksToolBar(),
        IMSATCAD.getSelectionToolBar(),
        IMSATCAD.getFileToolBar()
    ];
    for (var i = 0; i < bars.length; ++i) {
        var bar = bars[i];
        if (isNull(bar)) {
            continue;
        }
        if (typeof bar.setToolButtonStyle === "function" &&
                typeof Qt.ToolButtonTextUnderIcon !== "undefined") {
            bar.setToolButtonStyle(Qt.ToolButtonTextUnderIcon);
        }
    }
};
IMSATCAD.init = function() {
    IMSATCAD.getMenu();
    IMSATCAD.getDrawToolBar();
    IMSATCAD.getModifyToolBar();
    IMSATCAD.getAnnotationToolBar();
    IMSATCAD.getLayerToolBar();
    IMSATCAD.getUtilitiesToolBar();
    IMSATCAD.getBlocksToolBar();
    IMSATCAD.getSelectionToolBar();
    IMSATCAD.getFileToolBar();
    IMSATCAD.configureWorkspace();
};
// Reuse QCAD actions without duplicating geometry algorithms.
IMSATCAD._drawScripts = [
    "scripts/Draw/Line/Line2P/Line2P.js",
    "scripts/Draw/Polyline/DrawPolyline/DrawPolyline.js",
    "scripts/Draw/Circle/CircleCR/CircleCR.js",
    "scripts/Draw/Shape/ShapeRectanglePP/ShapeRectanglePP.js",
    "scripts/Draw/Arc/Arc3P/Arc3P.js",
    "scripts/Draw/Ellipse/EllipseCPP/EllipseCPP.js",
    "scripts/Draw/Shape/ShapePolygonCP/ShapePolygonCP.js"
];
IMSATCAD._modifyScripts = [
    "scripts/Modify/Trim/Trim.js",
    "scripts/Modify/Offset/Offset.js",
    "scripts/Modify/Round/Round.js",
    "scripts/Modify/Bevel/Bevel.js",
    "scripts/Modify/Translate/Translate.js",
    "scripts/Modify/Rotate/Rotate.js",
    "scripts/Modify/Scale/Scale.js",
    "scripts/Modify/Mirror/Mirror.js",
    "scripts/Modify/Explode/Explode.js",
    "scripts/Edit/Delete/Delete.js"
];
// These QCAD categories expose the upstream annotation and layer actions.
IMSATCAD._annotationScripts = [
    "scripts/Draw/Dimension/DimHorizontal/DimHorizontal.js",
    "scripts/Draw/Dimension/DimVertical/DimVertical.js",
    "scripts/Draw/Dimension/DimAligned/DimAligned.js",
    "scripts/Draw/Dimension/DimAngular/DimAngular.js",
    "scripts/Draw/Dimension/DimRadial/DimRadial.js",
    "scripts/Draw/Dimension/DimDiametric/DimDiametric.js",
    "scripts/Draw/Dimension/Leader/Leader.js"
];
IMSATCAD._layerScripts = [
    "scripts/Layer/AddLayer/AddLayer.js",
    "scripts/Layer/EditLayer/EditLayer.js",
    "scripts/Layer/RemoveLayer/RemoveLayer.js",
    "scripts/Layer/ShowAllLayers/ShowAllLayers.js",
    "scripts/Layer/HideAllLayers/HideAllLayers.js",
    "scripts/Layer/LockAllLayers/LockAllLayers.js",
    "scripts/Layer/UnlockAllLayers/UnlockAllLayers.js"
];
IMSATCAD._utilityScripts = [
    "scripts/Draw/Text/Text.js",
    "scripts/Draw/Hatch/HatchFromSelection/HatchFromSelection.js",
    "scripts/Edit/Undo/Undo.js",
    "scripts/Edit/Redo/Redo.js",
    "scripts/View/Zoom/AutoZoom/AutoZoom.js",
    "scripts/View/Zoom/WindowZoom/WindowZoom.js",
    "scripts/View/Zoom/PanZoom/PanZoom.js",
    "scripts/File/Print/Print.js"
];
IMSATCAD._fileScripts = [
    "scripts/File/Save/Save.js",
    "scripts/File/SaveAs/SaveAs.js",
    "scripts/File/Print/Print.js"
];
IMSATCAD._blockScripts = [
    "scripts/Block/CreateBlock/CreateBlock.js",
    "scripts/Block/InsertBlock/InsertBlock.js",
    "scripts/Block/EditBlock/EditBlock.js",
    "scripts/Block/RemoveBlock/RemoveBlock.js"
];
IMSATCAD._selectionScripts = [
    "scripts/Select/SelectAll/SelectAll.js",
    "scripts/Select/DeselectAll/DeselectAll.js",
    "scripts/Select/InvertSelection/InvertSelection.js",
    "scripts/Select/SelectRectangle/SelectRectangle.js"
];
IMSATCAD._installedActions = {};
IMSATCAD.populateToolBar = function(toolbar, scripts) {
    if (isNull(toolbar)) {
        qWarning("IMSAT CAD: toolbar unavailable");
        return 0;
    }
    var installed = 0;
    var missing = 0;
    var menu = IMSATCAD.getMenu();
    for (var i = 0; i < scripts.length; ++i) {
        var action = RGuiAction.getByScriptFile(scripts[i]);
        if (isNull(action)) {
            qWarning("IMSAT CAD: toolbar action not found: " + scripts[i]);
            missing++;
            continue;
        }
        // RGuiAction is registered with QCAD; reuse its QAction in our toolbar.
        // Keep initialization idempotent if QCAD re-runs postInit.
        var key = toolbar.objectName + "|" + scripts[i];
        if (IMSATCAD._installedActions[key] === true) {
            continue;
        }
        toolbar.addAction(action);
        if (!isNull(menu) && IMSATCAD._installedActions["menu|" + scripts[i]] !== true) {
            menu.addAction(action);
            IMSATCAD._installedActions["menu|" + scripts[i]] = true;
        }
        IMSATCAD._installedActions[key] = true;
        installed++;
    }
    if (missing > 0) {
        qWarning("IMSAT CAD: " + missing + " toolbar actions unavailable; check native QCAD script initialization");
    }
    return installed;
};

// Keyboard policy used by the IMSAT command-input integration.
// Return a semantic intent instead of synthesizing keystrokes globally:
// global Space interception would break MText, dimensions and dialogs.
IMSATCAD.resolveCommandKey = function(key, state) {
    state = state || {};
    if (state.textEditing || state.dialogOpen) {
        return "pass";
    }
    if (key === "Escape" || key === "Esc") {
        return "cancel";
    }
    if (key !== "Enter" && key !== "Return" && key !== "Space") {
        return "pass";
    }
    if (state.commandText && state.commandText.replace(/^\\s+|\\s+$/g, "").length > 0) {
        return "execute";
    }
    if (state.actionActive) {
        return "confirm";
    }
    if (state.lastCommand) {
        return "repeat";
    }
    return "pass";
};
IMSATCAD._aliases = [
    ["scripts/Draw/Line/Line2P/Line2P.js", ["line", "l", "ln"]],
    ["scripts/Draw/Polyline/DrawPolyline/DrawPolyline.js", ["pline", "pl", "polyline"]],
    ["scripts/Draw/Circle/CircleCR/CircleCR.js", ["circle", "c"]],
    ["scripts/Draw/Arc/Arc3P/Arc3P.js", ["arc", "a"]],
    ["scripts/Draw/Ellipse/EllipseCPP/EllipseCPP.js", ["ellipse", "el"]],
    ["scripts/Draw/Shape/ShapePolygonCP/ShapePolygonCP.js", ["polygon", "pol"]],
    ["scripts/Draw/Shape/ShapeRectanglePP/ShapeRectanglePP.js", ["rectang", "rec", "rectangle"]],
    ["scripts/Modify/Trim/Trim.js", ["trim", "tr"]],
    ["scripts/Modify/Offset/Offset.js", ["offset", "o"]],
    ["scripts/Modify/Round/Round.js", ["fillet", "f"]],
    ["scripts/Modify/Bevel/Bevel.js", ["chamfer", "cha"]],
    ["scripts/Modify/Translate/Translate.js", ["move", "m", "mv"]],
    ["scripts/Modify/Rotate/Rotate.js", ["rotate", "ro"]],
    ["scripts/Modify/Scale/Scale.js", ["scale", "sc"]],
    ["scripts/Modify/Mirror/Mirror.js", ["mirror", "mi"]],
    ["scripts/Modify/Explode/Explode.js", ["explode", "x"]],
    ["scripts/Edit/Delete/Delete.js", ["erase", "e"]],
    ["scripts/Draw/Text/Text.js", ["text", "mtext", "mt"]],
    ["scripts/Draw/Dimension/DimHorizontal/DimHorizontal.js", ["dimlinear", "dli"]],
    ["scripts/Draw/Dimension/DimAligned/DimAligned.js", ["dimaligned", "dal"]],
    ["scripts/Draw/Dimension/DimAngular/DimAngular.js", ["dimangular", "dan"]],
    ["scripts/Draw/Dimension/DimRadial/DimRadial.js", ["dimradius", "dra"]],
    ["scripts/Draw/Dimension/DimDiametric/DimDiametric.js", ["dimdiameter", "ddi"]],
    ["scripts/Draw/Dimension/Leader/Leader.js", ["leader", "le"]],
    ["scripts/View/Zoom/AutoZoom/AutoZoom.js", ["zoomextents", "ze"]],
    ["scripts/View/Zoom/WindowZoom/WindowZoom.js", ["zoomwindow", "zw"]],
    ["scripts/Edit/Undo/Undo.js", ["undo", "u"]],
    ["scripts/Edit/Redo/Redo.js", ["redo"]],
    ["scripts/Layer/AddLayer/AddLayer.js", ["layernew"]],
    ["scripts/File/Save/Save.js", ["qsave"]],
    ["scripts/File/SaveAs/SaveAs.js", ["saveas"]]
];

// Native Qt command surface. All controls reuse registered CAD actions;
// a missing Qt bridge is a release blocker, not evidence of a working Ribbon.
IMSATCAD._ribbonCreated = false;
IMSATCAD._ribbonHost = null;
// Bilingual IMSAT Ribbon captions (does not replace upstream QCAD translations).
IMSATCAD._ribbonVietnamese = {
    "Home": "Trang chủ", "Insert": "Chèn", "Annotate": "Ghi chú",
    "View": "Hiển thị", "Manage": "Quản lý", "Output": "Xuất bản",
    "Draw": "Vẽ", "Modify": "Hiệu chỉnh", "Layers": "Lớp",
    "Blocks": "Khối", "Dimensions": "Kích thước",
    "View & tools": "Hiển thị và công cụ",
    "Selection": "Lựa chọn", "File & plot": "Tệp và in"
};
IMSATCAD._ribbonTabs = [
    ["Home", [["Draw", "_drawScripts"], ["Modify", "_modifyScripts"], ["Layers", "_layerScripts"]]],
    ["Insert", [["Blocks", "_blockScripts"]]],
    ["Annotate", [["Dimensions", "_annotationScripts"]]],
    ["View", [["View & tools", "_utilityScripts"]]],
    ["Manage", [["Selection", "_selectionScripts"]]],
    ["Output", [["File & plot", "_fileScripts"]]]
];
IMSATCAD.createRibbon = function() {
    if (IMSATCAD._ribbonCreated) { return true; }
    if (typeof QTabWidget === "undefined" || typeof QToolButton === "undefined" ||
        typeof QWidget === "undefined" || typeof QHBoxLayout === "undefined" ||
        typeof QVBoxLayout === "undefined" || typeof QGridLayout === "undefined" || typeof QLabel === "undefined") {
        qWarning("IMSAT CAD RIBBON BLOCKED: required Qt widgets unavailable");
        return false;
    }
    // Preflight every configured action before constructing Qt widgets. An incomplete
    // Ribbon must not appear usable or leave orphan tabs after a failed retry.
    var missingRibbonActions = [];
    for (var t0 = 0; t0 < IMSATCAD._ribbonTabs.length; ++t0) {
        var groups0 = IMSATCAD._ribbonTabs[t0][1];
        for (var g0 = 0; g0 < groups0.length; ++g0) {
            var scripts0 = IMSATCAD[groups0[g0][1]];
            for (var i0 = 0; i0 < scripts0.length; ++i0) {
                if (isNull(RGuiAction.getByScriptFile(scripts0[i0]))) {
                    missingRibbonActions.push(scripts0[i0]);
                }
            }
        }
    }
    if (missingRibbonActions.length > 0) {
        qWarning("IMSAT CAD RIBBON BLOCKED: missing registered actions: " + missingRibbonActions.join(", "));
        return false;
    }
    try {
        var host = EAction.getToolBar("IMSAT CAD", "IMSATCADRibbonToolBar", Qt.TopToolBarArea);
        if (isNull(host)) { qWarning("IMSAT CAD RIBBON BLOCKED: no toolbar host"); return false; }
        var tabs = new QTabWidget(host);
        tabs.objectName = "IMSATCADRibbonTabs";
        var totalActions = 0;
        for (var t = 0; t < IMSATCAD._ribbonTabs.length; ++t) {
            var spec = IMSATCAD._ribbonTabs[t];
            var page = new QWidget(tabs);
            var row = new QHBoxLayout(page);
            var panels = spec[1];
            var tabActions = 0;
            for (var g = 0; g < panels.length; ++g) {
                var panel = new QWidget(page);
                var column = new QVBoxLayout(panel);
                var buttons = new QWidget(panel);
                var buttonGrid = new QGridLayout(buttons);
                var scripts = IMSATCAD[panels[g][1]];
                var groupActions = 0;
                for (var i = 0; i < scripts.length; ++i) {
                    var action = RGuiAction.getByScriptFile(scripts[i]);
                    if (isNull(action)) { continue; }
                    var button = new QToolButton(buttons);
                    button.setDefaultAction(action);
                    button.toolButtonStyle = Qt.ToolButtonTextUnderIcon;
                    // Avoid truncated script aliases in the native Ribbon.
                    // Native QAction text is often an abbreviated command name;
                    // use a full descriptive label derived from the action script.
                    var filename = scripts[i].split("/").pop().replace(/\.js$/, "");
                    var label = filename.replace(/([a-z])([A-Z])/g, "$1 $2");
                    if (typeof button.setText === "function") { button.setText(label); }
                    if (typeof button.setToolTip === "function") { button.setToolTip(label); }
                    if (typeof button.setMinimumWidth === "function") { button.setMinimumWidth(84); }
                    // Ribbon groups use three compact rows, expanding horizontally as needed.
                    // This avoids very tall panels when a group has many actions.
                    buttonGrid.addWidget(button, groupActions % 3, Math.floor(groupActions / 3));
                    groupActions++;
                }
                if (groupActions === 0) { continue; }
                column.addWidget(buttons);
                var groupName = panels[g][0];
                var groupVi = IMSATCAD._ribbonVietnamese[groupName];
                var caption = new QLabel(groupVi ? groupName + " / " + groupVi : groupName, panel);
                column.addWidget(caption);
                row.addWidget(panel);
                tabActions += groupActions;
            }
            row.addStretch(1);
            if (tabActions === 0) {
                qWarning("IMSAT CAD RIBBON BLOCKED: no registered actions in tab " + spec[0]);
                return false;
            }
            var tabVi = IMSATCAD._ribbonVietnamese[spec[0]];
            tabs.addTab(page, tabVi ? spec[0] + " / " + tabVi : spec[0]);
            totalActions += tabActions;
        }
        host.addWidget(tabs);
        IMSATCAD._ribbonHost = host;
        IMSATCAD._ribbonCreated = true;
        qWarning("IMSAT CAD Ribbon initialized: " + tabs.count + " tabs, " + totalActions + " actions");
        return true;
    } catch (error) {
        qWarning("IMSAT CAD RIBBON BLOCKED: " + error);
        return false;
    }
};

// Keep native actions available but hide duplicated IMSAT legacy toolbars
// only after the Ribbon has been constructed successfully.
IMSATCAD.hideDuplicateToolBars = function() {
    var bars = [
        IMSATCAD.getDrawToolBar(), IMSATCAD.getModifyToolBar(),
        IMSATCAD.getAnnotationToolBar(), IMSATCAD.getLayerToolBar(),
        IMSATCAD.getUtilitiesToolBar(), IMSATCAD.getBlocksToolBar(),
        IMSATCAD.getSelectionToolBar(), IMSATCAD.getFileToolBar()
    ];
    for (var i = 0; i < bars.length; ++i) {
        if (!isNull(bars[i]) && typeof bars[i].setVisible === "function") {
            bars[i].setVisible(false);
        }
    }
};

IMSATCAD.postInit = function() {
    var win = RMainWindowQt.getMainWindow();
    var success = 0;
    var missingAliases = [];
    for (var i=0; i<IMSATCAD._aliases.length; ++i) {
        var mapping=IMSATCAD._aliases[i];
        var action=RGuiAction.getByScriptFile(mapping[0]);
        if (isNull(action)) {
            qWarning("IMSAT CAD: native action not found: " + mapping[0]);
            missingAliases.push(mapping[0]);
            continue;
        }
        action.setDefaultCommands(mapping[1]);
        success++;
    }
    if (missingAliases.length > 0) {
        qWarning("IMSAT CAD: missing native alias actions: " + missingAliases.join(", "));
    }
    var ribbonReady = IMSATCAD.createRibbon();
    if (!ribbonReady) { qWarning("IMSAT CAD: native Ribbon acceptance gate FAIL"); }
    var drawCount = IMSATCAD.populateToolBar(IMSATCAD.getDrawToolBar(), IMSATCAD._drawScripts);
    var modifyCount = IMSATCAD.populateToolBar(IMSATCAD.getModifyToolBar(), IMSATCAD._modifyScripts);
    var annotationCount = IMSATCAD.populateToolBar(IMSATCAD.getAnnotationToolBar(), IMSATCAD._annotationScripts);
    var layerCount = IMSATCAD.populateToolBar(IMSATCAD.getLayerToolBar(), IMSATCAD._layerScripts);
    var utilityCount = IMSATCAD.populateToolBar(IMSATCAD.getUtilitiesToolBar(), IMSATCAD._utilityScripts);
    var blockCount = IMSATCAD.populateToolBar(IMSATCAD.getBlocksToolBar(), IMSATCAD._blockScripts);
    var selectionCount = IMSATCAD.populateToolBar(IMSATCAD.getSelectionToolBar(), IMSATCAD._selectionScripts);
    var fileCount = IMSATCAD.populateToolBar(IMSATCAD.getFileToolBar(), IMSATCAD._fileScripts);
    if (ribbonReady) {
        IMSATCAD.hideDuplicateToolBars();
        // QCAD's original toolbars remain available from View > Toolbars.
        // Hide only on first IMSAT workspace activation; do not remove actions.
        if (!IMSATCAD._nativeWorkspaceConfigured) {
            IMSATCAD._nativeWorkspaceConfigured = true;
            var nativeBars = ["DrawToolBar", "ModifyToolBar", "DimensionToolBar"];
            for (var n = 0; n < nativeBars.length; ++n) {
                var nativeBar = !isNull(win) && typeof win.findChild === "function" ? win.findChild("QToolBar", nativeBars[n]) : null;
                if (!isNull(nativeBar) && typeof nativeBar.setVisible === "function") {
                    nativeBar.setVisible(false);
                }
            }
        }
    }
    if (!isNull(win)) {
        win.handleUserMessage("IMSAT CAD: " + success + "/" + IMSATCAD._aliases.length +
                              " aliases, " + drawCount + " draw tools, " + modifyCount + " modify tools, " + annotationCount + " annotation, " + layerCount + " layer, " + utilityCount + " utilities, " + blockCount + " blocks, " + selectionCount + " selection, " + fileCount + " file");
    }
};
// AutoCAD-style aliases map to the closest native QCAD action; command prompts
// and geometry behavior still require Windows interactive acceptance tests.
// COPY and EXTEND aliases are intentionally withheld: QCAD Translate and Trim
// require different options and are not drop-in AutoCAD COPY/EXTEND commands.
// IMPORTANT: QCAD editing behavior differs from AutoCAD for some commands.
// This prototype never claims command parity without interactive tests.
