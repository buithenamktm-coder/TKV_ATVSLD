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

// AutoCAD-style Quick Access toolbar: native QCAD QAction objects, never copied
// drawing implementations. The toolbar stays visible when legacy bars are hidden.
IMSATCAD.getQuickAccessToolBar = function() {
    return EAction.getToolBar(qsTr("IMSAT CAD - Quick Access"), "IMSATCADQuickAccessToolBar", Qt.TopToolBarArea);
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
    IMSATCAD.getQuickAccessToolBar();
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
    "scripts/Draw/Shape/ShapePolygonCP/ShapePolygonCP.js",
    "scripts/Draw/Hatch/HatchFromSelection/HatchFromSelection.js"
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
    "scripts/Edit/Delete/Delete.js",
    "scripts/Edit/Undo/Undo.js",
    "scripts/Edit/Redo/Redo.js"
];
// These QCAD categories expose the upstream annotation and layer actions.
IMSATCAD._annotationScripts = [
    "scripts/Draw/Dimension/DimHorizontal/DimHorizontal.js",
    "scripts/Draw/Dimension/DimVertical/DimVertical.js",
    "scripts/Draw/Dimension/DimAligned/DimAligned.js",
    "scripts/Draw/Dimension/DimAngular/DimAngular.js",
    "scripts/Draw/Dimension/DimRadial/DimRadial.js",
    "scripts/Draw/Dimension/DimDiametric/DimDiametric.js",
    "scripts/Draw/Dimension/Leader/Leader.js",
    "scripts/Draw/Text/Text.js"
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
// View-only navigation commands; drawing, annotation, edit history and
// printing live in their respective Ribbon groups without duplicate buttons.
IMSATCAD._utilityScripts = [
    "scripts/View/Zoom/AutoZoom/AutoZoom.js",
    "scripts/View/Zoom/WindowZoom/WindowZoom.js",
    "scripts/View/Zoom/PanZoom/PanZoom.js"
];
IMSATCAD._fileScripts = [
    "scripts/File/Save/Save.js",
    "scripts/File/SaveAs/SaveAs.js",
    "scripts/File/Print/Print.js"
];
// Compact, always-accessible Save / Undo / Redo commands use the very same
// native actions as the Ribbon and keyboard command aliases.
IMSATCAD._quickAccessScripts = [
    "scripts/File/Save/Save.js",
    "scripts/Edit/Undo/Undo.js",
    "scripts/Edit/Redo/Redo.js"
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
    var incomplete = 0;
    var menu = IMSATCAD.getMenu();
    for (var i = 0; i < scripts.length; ++i) {
        var script = scripts[i];
        var action;
        try {
            action = RGuiAction.getByScriptFile(script);
        } catch (lookupError) {
            qWarning("IMSAT CAD: toolbar action lookup failed for " + script + ": " + lookupError);
            incomplete++;
            continue;
        }
        if (isNull(action)) {
            qWarning("IMSAT CAD: toolbar action not found: " + script);
            incomplete++;
            continue;
        }
        // A toolbar insertion and a menu insertion are independent operations.
        // Mark each as installed only after its own native Qt API succeeds.
        // If menu insertion fails after toolbar insertion, postInit can retry
        // the menu without adding a duplicate toolbar button.
        var key = toolbar.objectName + "|" + script;
        if (IMSATCAD._installedActions[key] !== true) {
            try {
                toolbar.addAction(action);
                IMSATCAD._installedActions[key] = true;
                installed++;
            } catch (toolbarError) {
                qWarning("IMSAT CAD: toolbar insertion failed for " + script + ": " + toolbarError);
                incomplete++;
                continue;
            }
        }
        var menuKey = "menu|" + script;
        if (!isNull(menu) && IMSATCAD._installedActions[menuKey] !== true) {
            try {
                menu.addAction(action);
                IMSATCAD._installedActions[menuKey] = true;
            } catch (menuError) {
                qWarning("IMSAT CAD: menu insertion failed for " + script + ": " + menuError);
                incomplete++;
            }
        }
    }
    if (incomplete > 0) {
        qWarning("IMSAT CAD: " + incomplete +
                 " toolbar/menu registrations incomplete; check native QCAD action initialization");
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
    if (state.commandText && state.commandText.replace(/^\s+|\s+$/g, "").length > 0) {
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
// Only show shortcuts that the native QCAD action accepted during initialization.
IMSATCAD._registeredAliases = {};
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
IMSATCAD._ribbonLanguage = "vi";
IMSATCAD._ribbonTabsWidget = null;
IMSATCAD._ribbonCaptions = [];
IMSATCAD._ribbonLanguageButton = null;
IMSATCAD._ribbonButtonCaptions = [];
// Bilingual IMSAT Ribbon captions (does not replace upstream QCAD translations).
IMSATCAD._ribbonVietnamese = {
    "Home": "Trang chủ", "Insert": "Chèn", "Annotate": "Ghi chú",
    "View": "Hiển thị", "Manage": "Quản lý", "Output": "Xuất bản",
    "Draw": "Vẽ", "Modify": "Hiệu chỉnh", "Layers": "Lớp",
    "Blocks": "Khối", "Dimensions": "Kích thước",
    "Navigation": "Điều hướng",
    "Selection": "Lựa chọn", "File & plot": "Tệp và in"
};
// Command labels are owned by IMSAT, not by upstream QAction translations.
// Preserve the native action and change only the visible Ribbon button text.
IMSATCAD._commandLabels = {
    "Line2P": { en: "Line", vi: "Đường thẳng" },
    "DrawPolyline": { en: "Polyline", vi: "Đa tuyến" },
    "CircleCR": { en: "Circle", vi: "Đường tròn" },
    "ShapeRectanglePP": { en: "Rectangle", vi: "Hình chữ nhật" },
    "Arc3P": { en: "Arc", vi: "Cung tròn" },
    "EllipseCPP": { en: "Ellipse", vi: "Elip" },
    "ShapePolygonCP": { en: "Polygon", vi: "Đa giác" },
    "HatchFromSelection": { en: "Hatch", vi: "Tô mặt cắt" },
    "Trim": { en: "Trim", vi: "Xén" },
    "Offset": { en: "Offset", vi: "Đường song song" },
    "Round": { en: "Fillet", vi: "Bo góc" },
    "Bevel": { en: "Chamfer", vi: "Vát góc" },
    "Translate": { en: "Move", vi: "Di chuyển" },
    "Rotate": { en: "Rotate", vi: "Xoay" },
    "Scale": { en: "Scale", vi: "Tỷ lệ" },
    "Mirror": { en: "Mirror", vi: "Đối xứng" },
    "Explode": { en: "Explode", vi: "Phá khối" },
    "Delete": { en: "Erase", vi: "Xóa" },
    "Undo": { en: "Undo", vi: "Hoàn tác" },
    "Redo": { en: "Redo", vi: "Làm lại" },
    "DimHorizontal": { en: "Horizontal dimension", vi: "Kích thước ngang" },
    "DimVertical": { en: "Vertical dimension", vi: "Kích thước đứng" },
    "DimAligned": { en: "Aligned dimension", vi: "Kích thước xiên" },
    "DimAngular": { en: "Angular dimension", vi: "Kích thước góc" },
    "DimRadial": { en: "Radius dimension", vi: "Kích thước bán kính" },
    "DimDiametric": { en: "Diameter dimension", vi: "Kích thước đường kính" },
    "Leader": { en: "Leader", vi: "Đường dẫn chú thích" },
    "Text": { en: "Text", vi: "Văn bản" },
    "AddLayer": { en: "New layer", vi: "Lớp mới" },
    "EditLayer": { en: "Edit layer", vi: "Sửa lớp" },
    "RemoveLayer": { en: "Delete layer", vi: "Xóa lớp" },
    "ShowAllLayers": { en: "Show layers", vi: "Hiện tất cả lớp" },
    "HideAllLayers": { en: "Hide layers", vi: "Ẩn tất cả lớp" },
    "LockAllLayers": { en: "Lock layers", vi: "Khóa tất cả lớp" },
    "UnlockAllLayers": { en: "Unlock layers", vi: "Mở khóa các lớp" },
    "AutoZoom": { en: "Zoom extents", vi: "Hiển thị toàn bộ" },
    "WindowZoom": { en: "Window zoom", vi: "Phóng theo khung" },
    "PanZoom": { en: "Pan", vi: "Dịch chuyển khung nhìn" },
    "Save": { en: "Save", vi: "Lưu" },
    "SaveAs": { en: "Save as", vi: "Lưu thành" },
    "Print": { en: "Print", vi: "In" },
    "CreateBlock": { en: "Create block", vi: "Tạo khối" },
    "InsertBlock": { en: "Insert block", vi: "Chèn khối" },
    "EditBlock": { en: "Edit block", vi: "Sửa khối" },
    "RemoveBlock": { en: "Remove block", vi: "Xóa khối" },
    "SelectAll": { en: "Select all", vi: "Chọn tất cả" },
    "DeselectAll": { en: "Deselect all", vi: "Bỏ chọn tất cả" },
    "InvertSelection": { en: "Invert selection", vi: "Đảo vùng chọn" },
    "SelectRectangle": { en: "Rectangle selection", vi: "Chọn theo khung" }
};
IMSATCAD.commandLabel = function(script, language) {
    var filename = script.split("/").pop().replace(/\.js$/, "");
    var pair = IMSATCAD._commandLabels[filename];
    return pair ? pair[language || IMSATCAD._ribbonLanguage] : filename;
};
// Show the actual registered command aliases, not speculative AutoCAD shortcuts.
// The tooltip is a view-only hint; the underlying QCAD QAction is unchanged.
IMSATCAD.commandAliases = function(script) {
    var aliases = IMSATCAD._registeredAliases[script];
    return aliases ? aliases.join(", ").toUpperCase() : "";
};
IMSATCAD.commandToolTip = function(script, language) {
    var label = IMSATCAD.commandLabel(script, language);
    var aliases = IMSATCAD.commandAliases(script);
    return aliases.length > 0 ? label + " (" + aliases + ")" : label;
};
// Give real Qt Ribbon controls accessible names for screen readers and
// Windows UI Automation. The QCAD QtScript bridge differs by version, so
// accessibility metadata must never prevent native CAD actions from loading.
IMSATCAD.setAccessibleLabel = function(widget, label) {
    if (isNull(widget)) { return false; }
    try {
        if (typeof widget.setAccessibleName === "function") {
            widget.setAccessibleName(label);
            return true;
        }
        if (typeof widget.accessibleName !== "undefined") {
            widget.accessibleName = label;
            return true;
        }
    } catch (error) {
        qWarning("IMSAT CAD: native accessibility label unavailable: " + error);
    }
    return false;
};
IMSATCAD._ribbonTabs = [
    ["Home", [["Draw", "_drawScripts"], ["Modify", "_modifyScripts"], ["Layers", "_layerScripts"]]],
    ["Insert", [["Blocks", "_blockScripts"]]],
    ["Annotate", [["Dimensions", "_annotationScripts"]]],
    ["View", [["Navigation", "_utilityScripts"]]],
    ["Manage", [["Selection", "_selectionScripts"]]],
    ["Output", [["File & plot", "_fileScripts"]]]
];
// Localize IMSAT tabs and panels without altering upstream CAD translations.
IMSATCAD.ribbonLabel = function(name) {
    return IMSATCAD._ribbonLanguage === "vi" && IMSATCAD._ribbonVietnamese[name] ?
        IMSATCAD._ribbonVietnamese[name] : name;
};
// Qt bridges expose QTabWidget.count as either a property or a method.
IMSATCAD.ribbonTabCount = function(tabs) {
    if (isNull(tabs)) { return null; }
    try {
        var count = typeof tabs.count === "function" ? tabs.count() : tabs.count;
        return typeof count === "number" && isFinite(count) && count >= 0 ? count : null;
    } catch (countError) {
        qWarning("IMSAT CAD: cannot read native Ribbon tab count: " + countError);
        return null;
    }
};
IMSATCAD.setRibbonLanguage = function(language) {
    if (language !== "vi" && language !== "en") { return false; }
    // Validate the live widgets before changing state to avoid a partially
    // translated Ribbon when the native Qt bridge is unavailable.
    var currentTabs = IMSATCAD._ribbonTabsWidget;
    if (!isNull(currentTabs) &&
            (typeof currentTabs.setTabText !== "function" ||
             IMSATCAD.ribbonTabCount(currentTabs) !== IMSATCAD._ribbonTabs.length)) {
        qWarning("IMSAT CAD: Ribbon language switch blocked: tab API or tab count mismatch");
        return false;
    }
    for (var k = 0; k < IMSATCAD._ribbonCaptions.length; ++k) {
        if (isNull(IMSATCAD._ribbonCaptions[k].widget) ||
                typeof IMSATCAD._ribbonCaptions[k].widget.setText !== "function") {
            qWarning("IMSAT CAD: Ribbon language switch blocked: panel caption API unavailable");
            return false;
        }
    }
    for (var b = 0; b < IMSATCAD._ribbonButtonCaptions.length; ++b) {
        var control = IMSATCAD._ribbonButtonCaptions[b].widget;
        if (isNull(control) || typeof control.setText !== "function" ||
                typeof control.setToolTip !== "function") {
            qWarning("IMSAT CAD: Ribbon language switch blocked: command label API unavailable");
            return false;
        }
    }
    if (!isNull(IMSATCAD._ribbonLanguageButton) &&
            (typeof IMSATCAD._ribbonLanguageButton.setText !== "function" ||
             typeof IMSATCAD._ribbonLanguageButton.setToolTip !== "function")) {
        qWarning("IMSAT CAD: Ribbon language switch blocked: button API unavailable");
        return false;
    }
    // A native Qt setter may throw despite passing API preflight.
    // Keep the locale and visible captions consistent after such failures.
    var previousLanguage = IMSATCAD._ribbonLanguage;
    var tabs = IMSATCAD._ribbonTabsWidget;
    function applyCaptions(locale) {
        IMSATCAD._ribbonLanguage = locale;
        if (!isNull(tabs)) {
            for (var t = 0; t < IMSATCAD._ribbonTabs.length; ++t) {
                tabs.setTabText(t, IMSATCAD.ribbonLabel(IMSATCAD._ribbonTabs[t][0]));
            }
        }
        for (var c = 0; c < IMSATCAD._ribbonCaptions.length; ++c) {
            var caption = IMSATCAD._ribbonCaptions[c];
            caption.widget.setText(IMSATCAD.ribbonLabel(caption.name));
        }
        for (var b2 = 0; b2 < IMSATCAD._ribbonButtonCaptions.length; ++b2) {
            var entry = IMSATCAD._ribbonButtonCaptions[b2];
            entry.widget.setText(IMSATCAD.commandLabel(entry.script, locale));
            entry.widget.setToolTip(IMSATCAD.commandToolTip(entry.script, locale));
            IMSATCAD.setAccessibleLabel(entry.widget, "IMSAT CAD: " + IMSATCAD.commandToolTip(entry.script, locale));
        }
        if (!isNull(IMSATCAD._ribbonLanguageButton)) {
            IMSATCAD._ribbonLanguageButton.setText(locale === "vi" ? "English" : "Tiếng Việt");
            IMSATCAD._ribbonLanguageButton.setToolTip(locale === "vi" ?
                "Switch Ribbon to English" : "Chuyển Ribbon sang tiếng Việt");
            IMSATCAD.setAccessibleLabel(IMSATCAD._ribbonLanguageButton,
                locale === "vi" ? "IMSAT CAD: Switch to English" : "IMSAT CAD: Chuyển sang tiếng Việt");
        }
    }
    try {
        applyCaptions(language);
    } catch (translationError) {
        qWarning("IMSAT CAD: Ribbon language switch failed: " + translationError);
        // Best-effort restoration: one broken widget must not prevent the
        // remaining controls from returning to the previous language.
        IMSATCAD._ribbonLanguage = previousLanguage;
        if (!isNull(tabs)) {
            for (var rt = 0; rt < IMSATCAD._ribbonTabs.length; ++rt) {
                try { tabs.setTabText(rt, IMSATCAD.ribbonLabel(IMSATCAD._ribbonTabs[rt][0])); }
                catch (restoreTabError) { qWarning("IMSAT CAD: restore tab failed: " + restoreTabError); }
            }
        }
        for (var rc = 0; rc < IMSATCAD._ribbonCaptions.length; ++rc) {
            try {
                var oldCaption = IMSATCAD._ribbonCaptions[rc];
                oldCaption.widget.setText(IMSATCAD.ribbonLabel(oldCaption.name));
            } catch (restorePanelError) { qWarning("IMSAT CAD: restore panel failed: " + restorePanelError); }
        }
        for (var rb = 0; rb < IMSATCAD._ribbonButtonCaptions.length; ++rb) {
            var oldEntry = IMSATCAD._ribbonButtonCaptions[rb];
            try { oldEntry.widget.setText(IMSATCAD.commandLabel(oldEntry.script, previousLanguage)); }
            catch (restoreCommandError) { qWarning("IMSAT CAD: restore command failed: " + restoreCommandError); }
            try { oldEntry.widget.setToolTip(IMSATCAD.commandToolTip(oldEntry.script, previousLanguage)); }
            catch (restoreTooltipError) { qWarning("IMSAT CAD: restore tooltip failed: " + restoreTooltipError); }
        }
        if (!isNull(IMSATCAD._ribbonLanguageButton)) {
            try {
                IMSATCAD._ribbonLanguageButton.setText(previousLanguage === "vi" ? "English" : "Tiếng Việt");
                IMSATCAD._ribbonLanguageButton.setToolTip(previousLanguage === "vi" ?
                    "Switch Ribbon to English" : "Chuyển Ribbon sang tiếng Việt");
            } catch (restoreSelectorError) { qWarning("IMSAT CAD: restore language selector failed: " + restoreSelectorError); }
        }
        return false;
    }
    IMSATCAD.saveRibbonPreference("RibbonLanguage", language);
    if (!isNull(IMSATCAD._ribbonCollapseButton)) {
        IMSATCAD.setRibbonCollapsed(IMSATCAD._ribbonCollapsed === true);
    }
    return true;
};
// User preferences affect only the IMSAT shell, never upstream CAD state.
IMSATCAD.saveRibbonPreference = function(key, value) {
    try {
        if (typeof RSettings !== "undefined") { RSettings.setValue("IMSATCAD/" + key, value); }
    } catch (error) { qWarning("IMSAT CAD: preference could not be saved: " + error); }
};
IMSATCAD.setRibbonCollapsed = function(collapsed) {
    var tabs = IMSATCAD._ribbonTabsWidget;
    if (isNull(tabs) || typeof tabs.setMaximumHeight !== "function") { return false; }
    tabs.setMaximumHeight(collapsed ? 36 : 16777215);
    IMSATCAD._ribbonCollapsed = collapsed;
    if (!isNull(IMSATCAD._ribbonCollapseButton)) {
        IMSATCAD._ribbonCollapseButton.setText(collapsed ? "▾" : "▴");
        IMSATCAD._ribbonCollapseButton.setToolTip(IMSATCAD._ribbonLanguage === "vi" ?
            (collapsed ? "Mở rộng Ribbon" : "Thu gọn Ribbon") :
            (collapsed ? "Expand Ribbon" : "Minimize Ribbon"));
    }
    IMSATCAD.saveRibbonPreference("RibbonCollapsed", collapsed);
    return true;
};
IMSATCAD.createRibbon = function() {
    if (IMSATCAD._ribbonCreated) { return true; }
    try {
        if (typeof RSettings !== "undefined") {
            var savedLanguage = RSettings.getStringValue("IMSATCAD/RibbonLanguage", "vi");
            IMSATCAD._ribbonLanguage = savedLanguage === "en" ? "en" : "vi";
        }
    } catch (preferenceError) { qWarning("IMSAT CAD: using default Ribbon language"); }
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
                try {
                    if (isNull(RGuiAction.getByScriptFile(scripts0[i0]))) {
                        missingRibbonActions.push(scripts0[i0]);
                    }
                } catch (lookupError) {
                    qWarning("IMSAT CAD RIBBON BLOCKED: action lookup failed for " +
                             scripts0[i0] + ": " + lookupError);
                    return false;
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
        if (isNull(host) || typeof host.addWidget !== "function") {
            qWarning("IMSAT CAD RIBBON BLOCKED: toolbar host cannot accept widgets");
            return false;
        }
        // Mount Ribbon tabs and language selector as one native toolbar widget.
        // A two-widget mount could leave an orphan Ribbon when the second
        // QToolBar.addWidget call fails, causing duplicate tabs on retry.
        var ribbonShell = new QWidget(host);
        ribbonShell.objectName = "IMSATCADRibbonShell";
        var shellLayout = new QHBoxLayout(ribbonShell);
        var tabs = new QTabWidget(ribbonShell);
        tabs.objectName = "IMSATCADRibbonTabs";
        IMSATCAD.setAccessibleLabel(tabs, "IMSAT CAD Ribbon");
        var totalActions = 0;
        var panelCaptions = [];
        var commandCaptions = [];
        for (var t = 0; t < IMSATCAD._ribbonTabs.length; ++t) {
            var spec = IMSATCAD._ribbonTabs[t];
            var page = new QWidget(tabs);
            // Stable native widget IDs survive locale changes and allow UIA targeting.
            page.objectName = "IMSATCADRibbonPage_" + spec[0];
            var row = new QHBoxLayout(page);
            var panels = spec[1];
            var tabActions = 0;
            for (var g = 0; g < panels.length; ++g) {
                var panel = new QWidget(page);
                panel.objectName = "IMSATCADRibbonPanel_" + spec[0] + "_" + panels[g][0].replace(/[^A-Za-z0-9]/g, "_");
                var column = new QVBoxLayout(panel);
                var buttons = new QWidget(panel);
                var buttonGrid = new QGridLayout(buttons);
                var scripts = IMSATCAD[panels[g][1]];
                var groupActions = 0;
                for (var i = 0; i < scripts.length; ++i) {
                    var action = RGuiAction.getByScriptFile(scripts[i]);
                    if (isNull(action)) { continue; }
                    var button = new QToolButton(buttons);
                    button.objectName = "IMSATCADRibbonAction_" + scripts[i].replace(/[^A-Za-z0-9]/g, "_");
                    button.setDefaultAction(action);
                    // Three rows of compact commands preserve canvas height.
                    button.toolButtonStyle = Qt.ToolButtonTextBesideIcon;
                    if (typeof button.setMinimumHeight === "function") { button.setMinimumHeight(28); }
                    // Avoid truncated script aliases in the native Ribbon.
                    // Native QAction text is often an abbreviated command name;
                    // use a full descriptive label derived from the action script.
                    var label = IMSATCAD.commandLabel(scripts[i]);
                    commandCaptions.push({ widget: button, script: scripts[i] });
                    if (typeof button.setText === "function") { button.setText(label); }
                    if (typeof button.setToolTip === "function") { button.setToolTip(IMSATCAD.commandToolTip(scripts[i])); }
                    IMSATCAD.setAccessibleLabel(button, "IMSAT CAD: " + IMSATCAD.commandToolTip(scripts[i]));
                    if (typeof button.setMinimumWidth === "function") { button.setMinimumWidth(84); }
                    // Ribbon groups use three compact rows, expanding horizontally as needed.
                    // This avoids very tall panels when a group has many actions.
                    buttonGrid.addWidget(button, groupActions % 3, Math.floor(groupActions / 3));
                    groupActions++;
                }
                if (groupActions === 0) { continue; }
                column.addWidget(buttons);
                var groupName = panels[g][0];
                var caption = new QLabel(IMSATCAD.ribbonLabel(groupName), panel);
                panelCaptions.push({ widget: caption, name: groupName });
                column.addWidget(caption);
                row.addWidget(panel);
                tabActions += groupActions;
            }
            row.addStretch(1);
            if (tabActions === 0) {
                qWarning("IMSAT CAD RIBBON BLOCKED: no registered actions in tab " + spec[0]);
                return false;
            }
            tabs.addTab(page, IMSATCAD.ribbonLabel(spec[0]));
            totalActions += tabActions;
        }
        // Reject a decorative selector with no native Qt signal.
        var languageButton = new QToolButton(ribbonShell);
        languageButton.objectName = "IMSATCADRibbonLanguageSwitch";
        if (isNull(languageButton.clicked) ||
                typeof languageButton.clicked.connect !== "function") {
            qWarning("IMSAT CAD RIBBON BLOCKED: language button has no Qt click signal");
            return false;
        }
        languageButton.setText(IMSATCAD._ribbonLanguage === "vi" ? "English" : "Tiếng Việt");
        languageButton.setToolTip(IMSATCAD._ribbonLanguage === "vi" ?
            "Switch Ribbon to English" : "Chuyển Ribbon sang tiếng Việt");
        IMSATCAD.setAccessibleLabel(languageButton,
            IMSATCAD._ribbonLanguage === "vi" ? "IMSAT CAD: Switch to English" : "IMSAT CAD: Chuyển sang tiếng Việt");
        languageButton.clicked.connect(function() {
            IMSATCAD.setRibbonLanguage(IMSATCAD._ribbonLanguage === "vi" ? "en" : "vi");
        });
        // Add widgets only after both Ribbon pages and language signal are valid.
        shellLayout.addWidget(tabs);
        shellLayout.addWidget(languageButton);
        var collapseButton = new QToolButton(ribbonShell);
        collapseButton.objectName = "IMSATCADRibbonCollapse";
        collapseButton.setText("▴");
        collapseButton.clicked.connect(function() {
            IMSATCAD.setRibbonCollapsed(!IMSATCAD._ribbonCollapsed);
        });
        shellLayout.addWidget(collapseButton);
        host.addWidget(ribbonShell);
        IMSATCAD._ribbonCollapseButton = collapseButton;
        IMSATCAD._ribbonTabsWidget = tabs;
        IMSATCAD._ribbonCaptions = panelCaptions;
        IMSATCAD._ribbonButtonCaptions = commandCaptions;
        IMSATCAD._ribbonLanguageButton = languageButton;
        IMSATCAD._ribbonHost = host;
        IMSATCAD._ribbonCreated = true;
        var collapsed = false;
        if (typeof RSettings !== "undefined") {
            collapsed = RSettings.getBoolValue("IMSATCAD/RibbonCollapsed", false);
        }
        IMSATCAD.setRibbonCollapsed(collapsed);
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
    // Rebuild from confirmed registrations: a failed native action must never
    // leave an advertised shortcut from an earlier postInit invocation.
    IMSATCAD._registeredAliases = {};
    for (var i=0; i<IMSATCAD._aliases.length; ++i) {
        var mapping=IMSATCAD._aliases[i];
        try {
            var action=RGuiAction.getByScriptFile(mapping[0]);
            if (isNull(action) || typeof action.setDefaultCommands !== "function") {
                qWarning("IMSAT CAD: native alias action unavailable: " + mapping[0]);
                missingAliases.push(mapping[0]);
                continue;
            }
            action.setDefaultCommands(mapping[1]);
            IMSATCAD._registeredAliases[mapping[0]] = mapping[1].slice(0);
            success++;
        } catch (error) {
            qWarning("IMSAT CAD: alias registration failed for " + mapping[0] + ": " + error);
            missingAliases.push(mapping[0]);
        }
    }
    if (missingAliases.length > 0) {
        qWarning("IMSAT CAD: missing native alias actions: " + missingAliases.join(", "));
    }
    var ribbonReady = IMSATCAD.createRibbon();
    if (!ribbonReady) { qWarning("IMSAT CAD: native Ribbon acceptance gate FAIL"); }
    // QCAD can invoke postInit again while the Ribbon is already mounted.
    // Synchronize existing button hints with the latest confirmed registrations.
    if (ribbonReady) {
        for (var hint = 0; hint < IMSATCAD._ribbonButtonCaptions.length; ++hint) {
            var entry = IMSATCAD._ribbonButtonCaptions[hint];
            try {
                if (!isNull(entry.widget) && typeof entry.widget.setToolTip === "function") {
                    entry.widget.setToolTip(IMSATCAD.commandToolTip(entry.script));
                }
            } catch (hintError) {
                qWarning("IMSAT CAD: failed to refresh command tooltip for " +
                         entry.script + ": " + hintError);
            }
        }
    }
    var drawCount = IMSATCAD.populateToolBar(IMSATCAD.getDrawToolBar(), IMSATCAD._drawScripts);
    var modifyCount = IMSATCAD.populateToolBar(IMSATCAD.getModifyToolBar(), IMSATCAD._modifyScripts);
    var annotationCount = IMSATCAD.populateToolBar(IMSATCAD.getAnnotationToolBar(), IMSATCAD._annotationScripts);
    var layerCount = IMSATCAD.populateToolBar(IMSATCAD.getLayerToolBar(), IMSATCAD._layerScripts);
    var utilityCount = IMSATCAD.populateToolBar(IMSATCAD.getUtilitiesToolBar(), IMSATCAD._utilityScripts);
    var blockCount = IMSATCAD.populateToolBar(IMSATCAD.getBlocksToolBar(), IMSATCAD._blockScripts);
    var selectionCount = IMSATCAD.populateToolBar(IMSATCAD.getSelectionToolBar(), IMSATCAD._selectionScripts);
    var fileCount = IMSATCAD.populateToolBar(IMSATCAD.getFileToolBar(), IMSATCAD._fileScripts);
    var quickAccessBar = IMSATCAD.getQuickAccessToolBar();
    var quickAccessCount = IMSATCAD.populateToolBar(quickAccessBar, IMSATCAD._quickAccessScripts);
    // Keep the high-frequency native actions compact, as in AutoCAD's Quick Access.
    // Use guarded QtScript calls because bridge versions expose different APIs.
    if (!isNull(quickAccessBar)) {
        if (typeof quickAccessBar.setToolButtonStyle === "function" &&
                typeof Qt.ToolButtonIconOnly !== "undefined") {
            quickAccessBar.setToolButtonStyle(Qt.ToolButtonIconOnly);
        }
        IMSATCAD.setAccessibleLabel(quickAccessBar, "IMSAT CAD Quick Access");
    }
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
                              " aliases, Ribbon=" + (ribbonReady ? "CREATED_NOT_GUI_TESTED" : "BLOCKED") + ", " + drawCount + " draw tools, " + modifyCount + " modify tools, " + annotationCount + " annotation, " + layerCount + " layer, " + utilityCount + " utilities, " + blockCount + " blocks, " + selectionCount + " selection, " + fileCount + " file, " + quickAccessCount + " quick access");
    }
};
// AutoCAD-style aliases map to the closest native QCAD action; command prompts
// and geometry behavior still require Windows interactive acceptance tests.
// COPY and EXTEND aliases are intentionally withheld: QCAD Translate and Trim
// require different options and are not drop-in AutoCAD COPY/EXTEND commands.
// IMPORTANT: QCAD editing behavior differs from AutoCAD for some commands.
// This prototype never claims command parity without interactive tests.
