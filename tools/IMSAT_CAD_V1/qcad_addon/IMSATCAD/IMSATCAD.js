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
IMSATCAD.getUtilitiesToolBar = function() {
    return EAction.getToolBar(qsTr("IMSAT CAD - Utilities"), "IMSATCADUtilitiesToolBar", Qt.TopToolBarArea);
};
IMSATCAD.init = function() {
    IMSATCAD.getMenu();
    IMSATCAD.getDrawToolBar();
    IMSATCAD.getModifyToolBar();
    IMSATCAD.getAnnotationToolBar();
    IMSATCAD.getLayerToolBar();
    IMSATCAD.getUtilitiesToolBar();
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
IMSATCAD.populateToolBar = function(toolbar, scripts) {
    var installed = 0;
    for (var i = 0; i < scripts.length; ++i) {
        var action = RGuiAction.getByScriptFile(scripts[i]);
        if (isNull(action)) {
            qWarning("IMSAT CAD: toolbar action not found: " + scripts[i]);
            continue;
        }
        // RGuiAction is registered with QCAD; reuse its QAction in our toolbar.
        toolbar.addAction(action);
        IMSATCAD.getMenu().addAction(action);
        installed++;
    }
    return installed;
};

IMSATCAD._aliases = [
    ["scripts/Draw/Line/Line2P/Line2P.js", ["line", "ln", "l"]],
    ["scripts/Draw/Polyline/DrawPolyline/DrawPolyline.js", ["polyline", "pline", "pl"]],
    ["scripts/Draw/Circle/CircleCR/CircleCR.js", ["circle", "c"]],
    ["scripts/Draw/Arc/Arc3P/Arc3P.js", ["arc", "a"]],
    ["scripts/Draw/Ellipse/EllipseCPP/EllipseCPP.js", ["ellipse", "el"]],
    ["scripts/Draw/Shape/ShapePolygonCP/ShapePolygonCP.js", ["polygon", "pol"]],
    ["scripts/Draw/Shape/ShapeRectanglePP/ShapeRectanglePP.js", ["rectangle", "rectang", "rec"]],
    ["scripts/Modify/Trim/Trim.js", ["trim", "extend", "tr", "ex"]],
    ["scripts/Modify/Offset/Offset.js", ["offset", "o"]],
    ["scripts/Modify/Round/Round.js", ["fillet", "f"]],
    ["scripts/Modify/Bevel/Bevel.js", ["chamfer", "cha"]],
    ["scripts/Modify/Translate/Translate.js", ["move", "m", "mv", "copy", "co", "cp"]],
    ["scripts/Modify/Rotate/Rotate.js", ["rotate", "ro"]],
    ["scripts/Modify/Scale/Scale.js", ["scale", "sc"]],
    ["scripts/Modify/Mirror/Mirror.js", ["mirror", "mi"]],
    ["scripts/Modify/Explode/Explode.js", ["explode", "x"]],
    ["scripts/Edit/Delete/Delete.js", ["erase", "e"]],
    ["scripts/Draw/Text/Text.js", ["text", "mtext", "mt"]],
    ["scripts/Draw/Dimension/DimHorizontal/DimHorizontal.js", ["dimlinear", "dli"]],
    ["scripts/Draw/Dimension/DimAligned/DimAligned.js", ["dimaligned", "dal"]],
    ["scripts/Draw/Dimension/DimAngular/DimAngular.js", ["dimangular", "dan"]],
    ["scripts/Layer/AddLayer/AddLayer.js", ["layernew"]],
    ["scripts/File/Save/Save.js", ["qsave"]],
    ["scripts/File/SaveAs/SaveAs.js", ["saveas"]]
];

IMSATCAD.postInit = function() {
    var win = RMainWindowQt.getMainWindow();
    var success = 0;
    for (var i=0; i<IMSATCAD._aliases.length; ++i) {
        var mapping=IMSATCAD._aliases[i];
        var action=RGuiAction.getByScriptFile(mapping[0]);
        if (isNull(action)) {
            qWarning("IMSAT CAD: native action not found: " + mapping[0]);
            continue;
        }
        action.setDefaultCommands(mapping[1]);
        success++;
    }
    var drawCount = IMSATCAD.populateToolBar(IMSATCAD.getDrawToolBar(), IMSATCAD._drawScripts);
    var modifyCount = IMSATCAD.populateToolBar(IMSATCAD.getModifyToolBar(), IMSATCAD._modifyScripts);
    var annotationCount = IMSATCAD.populateToolBar(IMSATCAD.getAnnotationToolBar(), IMSATCAD._annotationScripts);
    var layerCount = IMSATCAD.populateToolBar(IMSATCAD.getLayerToolBar(), IMSATCAD._layerScripts);
    var utilityCount = IMSATCAD.populateToolBar(IMSATCAD.getUtilitiesToolBar(), IMSATCAD._utilityScripts);
    if (!isNull(win)) {
        win.handleUserMessage("IMSAT CAD: " + success + "/" + IMSATCAD._aliases.length +
                              " aliases, " + drawCount + " draw tools, " + modifyCount + " modify tools, " + annotationCount + " annotation, " + layerCount + " layer, " + utilityCount + " utilities");
    }
};
// COPY/CO/CP invoke QCAD Translate; select its copy mode in the tool options.
// IMPORTANT: QCAD editing behavior differs from AutoCAD for some commands.
// This prototype never claims command parity without interactive tests.
