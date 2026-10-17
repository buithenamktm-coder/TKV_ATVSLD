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

IMSATCAD.init = function() {
    IMSATCAD.getMenu();
};

IMSATCAD._aliases = [
    ["scripts/Draw/Line/Line2P/Line2P.js", ["line", "ln", "l"]],
    ["scripts/Draw/Polyline/DrawPolyline/DrawPolyline.js", ["polyline", "pline", "pl"]],
    ["scripts/Draw/Circle/CircleCR/CircleCR.js", ["circle", "c"]],
    ["scripts/Draw/Shape/ShapeRectanglePP/ShapeRectanglePP.js", ["rectangle", "rectang", "rec"]],
    ["scripts/Modify/Trim/Trim.js", ["trim", "extend", "tr", "ex"]],
    ["scripts/Modify/Offset/Offset.js", ["offset", "o"]],
    ["scripts/Modify/Translate/Translate.js", ["move", "m", "mv", "copy", "co", "cp"]],
    ["scripts/Modify/Rotate/Rotate.js", ["rotate", "ro"]],
    ["scripts/Modify/Scale/Scale.js", ["scale", "sc"]],
    ["scripts/Modify/Mirror/Mirror.js", ["mirror", "mi"]],
    ["scripts/Modify/Explode/Explode.js", ["explode", "x"]],
    ["scripts/Edit/Delete/Delete.js", ["erase", "e"]]
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
    if (!isNull(win)) {
        win.handleUserMessage("IMSAT CAD aliases: " + success + "/" + IMSATCAD._aliases.length);
    }
};
// COPY/CO/CP invoke QCAD Translate; select its copy mode in the tool options.
// IMPORTANT: QCAD editing behavior differs from AutoCAD for some commands.
// This prototype never claims command parity without interactive tests.
