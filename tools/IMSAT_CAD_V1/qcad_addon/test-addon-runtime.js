// Mocked QCAD API contract test; does not replace GUI acceptance.
"use strict";
const fs = require("fs");
const vm = require("vm");
const assert = require("assert");
const path = require("path");
const source = fs.readFileSync(path.join(__dirname, "IMSATCAD", "IMSATCAD.js"), "utf8");
const toolbars = new Map();
const menu = { actions: [], addAction(a) { this.actions.push(a); } };
const actions = new Map();
const warnings = [];
const messages = [];
function EAction() {}
EAction.getMenu = () => menu;
EAction.getToolBar = (label, id) => {
  if (!toolbars.has(id)) {
    toolbars.set(id, { objectName: id, actions: [], widgets: [], addAction(a) { this.actions.push(a); }, addWidget(w) { this.widgets.push(w); } });
  }
  return toolbars.get(id);
};
const ribbonPages = [];
const ribbonButtons = [];
const ribbonCaptions = [];
function QWidget(parent) { this.parent = parent; }
function QHBoxLayout(page) {
  this.page = page;
  this.widgets = [];
  this.addWidget = button => this.widgets.push(button);
  this.addStretch = () => {};
}
function QVBoxLayout(page) { QHBoxLayout.call(this, page); }
const gridPositions = [];
function QGridLayout(page) { this.page = page; this.addWidget = (button, row, col) => gridPositions.push({ button, row, col }); }
function QLabel(label, parent) { this.label = label; this.parent = parent; ribbonCaptions.push(label); }
function QToolButton(parent) {
  this.parent = parent;
  this.setDefaultAction = action => { this.action = action; };
  this.setText = label => { this.text = label; };
  this.setToolTip = tooltip => { this.tooltip = tooltip; };
  this.setMinimumWidth = width => { this.minimumWidth = width; };
  ribbonButtons.push(this);
}
function QTabWidget(parent) {
  this.parent = parent;
  this.addTab = (page, label) => ribbonPages.push({ page, label });
  Object.defineProperty(this, 'count', { get: () => ribbonPages.length });
}
const context = {
  include() {}, EAction, qsTr: x => x, Qt: { TopToolBarArea: 1, ToolButtonTextUnderIcon: 2 },
  QWidget, QHBoxLayout, QVBoxLayout, QGridLayout, QLabel, QToolButton, QTabWidget,
  isNull: x => x == null, qWarning: x => warnings.push(x),
  RMainWindowQt: { getMainWindow: () => ({ handleUserMessage: x => messages.push(x) }) },
  RGuiAction: { getByScriptFile(script) {
    if (!actions.has(script)) actions.set(script, { script, setDefaultCommands(commands) { this.commands = commands; } });
    return actions.get(script);
  } }
};
vm.runInNewContext(source, context, { filename: "IMSATCAD.js" });
assert(context.IMSATCAD, "Add-on not loaded");
context.IMSATCAD.init();
context.IMSATCAD.postInit();
assert.strictEqual(toolbars.size, 9, "Expected eight legacy toolbars and IMSAT Ribbon");
assert.strictEqual(ribbonPages.length, 6, "Missing IMSAT Ribbon tabs");
assert(ribbonButtons.length > 30, "Expected actual Ribbon buttons");
assert.strictEqual(gridPositions.length, ribbonButtons.length, "Ribbon grid must contain all command buttons");
assert(gridPositions.every(p => p.row >= 0 && p.row < 3 && p.col >= 0), "Ribbon command grid exceeded three rows");
assert(ribbonButtons.every(b => b.text && !b.text.endsWith(".js")), "Ribbon button label must not expose script filename");
assert(ribbonButtons.every(b => b.minimumWidth >= 84), "Ribbon button minimum width missing");
assert(ribbonButtons.every(b => b.action), "Ribbon button missing native action");
assert.deepStrictEqual(ribbonPages.map(x => x.label), ["Home / Trang chủ", "Insert / Chèn", "Annotate / Ghi chú", "View / Hiển thị", "Manage / Quản lý", "Output / Xuất bản"]);
assert(ribbonPages.every(x => x.page && x.page.parent), "Ribbon page not created");
const expectedGroupCaptions = [
  "Draw / Vẽ", "Modify / Hiệu chỉnh", "Layers / Lớp",
  "Blocks / Khối", "Dimensions / Kích thước",
  "View & tools / Hiển thị và công cụ", "Selection / Lựa chọn",
  "File & plot / Tệp và in"
];
assert.deepStrictEqual(ribbonCaptions, expectedGroupCaptions,
  "Ribbon panel captions must retain bilingual labels");
assert.strictEqual(new Set(ribbonCaptions).size, ribbonCaptions.length,
  "Duplicate Ribbon panel captions");
assert(context.IMSATCAD._ribbonCreated, "Native ribbon creation flag missing");
assert(context.IMSATCAD._ribbonHost, "Native ribbon host not retained");
assert.strictEqual(toolbars.get("IMSATCADRibbonToolBar").widgets.length, 1, "Ribbon duplicated");
const first = [...toolbars.values()].map(x => x.actions.length);
assert(first.filter(x => x >= 2).length === 8, "Legacy CAD toolbar missing");
assert.strictEqual(menu.actions.length, new Set(menu.actions).size, "Duplicate menu actions");
const menuCount = menu.actions.length;
context.IMSATCAD.postInit();
assert.deepStrictEqual([...toolbars.values()].map(x => x.actions.length), first, "Repeated init duplicated toolbar actions");
assert.strictEqual(menu.actions.length, menuCount, "Repeated init duplicated menu actions");
assert.strictEqual(ribbonPages.length, 6, "Repeated init duplicated ribbon tabs");
// Verify aliases actually reach the native action objects, not only documentation.
const expectedAliases = {
  "scripts/Draw/Line/Line2P/Line2P.js": ["line", "l", "ln"],
  "scripts/Draw/Polyline/DrawPolyline/DrawPolyline.js": ["pline", "pl", "polyline"],
  "scripts/Draw/Circle/CircleCR/CircleCR.js": ["circle", "c"],
  "scripts/Modify/Trim/Trim.js": ["trim", "tr"],
  "scripts/Modify/Offset/Offset.js": ["offset", "o"],
  "scripts/Modify/Translate/Translate.js": ["move", "m", "mv"],
  "scripts/Edit/Delete/Delete.js": ["erase", "e"],
  "scripts/File/Save/Save.js": ["qsave"]
};
for (const [script, expected] of Object.entries(expectedAliases)) {
  assert(actions.has(script), "Alias action not registered: " + script);
  assert.deepStrictEqual(Array.from(actions.get(script).commands || []), expected, "Incorrect aliases: " + script);
}
const allAliases = [...actions.values()].flatMap(action => Array.from(action.commands || []));
assert.strictEqual(new Set(allAliases).size, allAliases.length, "Ambiguous duplicate command aliases");
for (const unsafe of ["copy", "co", "cp", "extend", "ex"]) {
  assert(!allAliases.includes(unsafe), "Unsupported AutoCAD command incorrectly advertised: " + unsafe);
}
assert(warnings.every(x => !x.includes("BLOCKED") && !x.includes("FAIL")), "Native Ribbon failure: " + warnings.join("; "));
assert(messages.length >= 1, "Missing initialization status");
// A missing registered CAD command must prevent any partial Ribbon from being mounted.
const missingScript = "scripts/Draw/Line/Line2P/Line2P.js";
const missingWarnings = [];
const missingHostWidgets = [];
const missingContext = Object.assign({}, context, {
  qWarning: x => missingWarnings.push(x),
  RGuiAction: { getByScriptFile(script) { return script === missingScript ? null : actions.get(script); } }
});
// A distinct toolbar host lets us prove that failure happens before Qt construction.
function MissingEAction() {}
MissingEAction.getMenu = EAction.getMenu;
MissingEAction.getToolBar = (label, id) => ({ addWidget(widget) { missingHostWidgets.push(widget); } });
missingContext.EAction = MissingEAction;
vm.runInNewContext(source, missingContext, { filename: "IMSATCAD.js" });
assert.strictEqual(missingContext.IMSATCAD.createRibbon(), false, "Missing CAD action must block Ribbon");
assert.strictEqual(missingHostWidgets.length, 0, "Incomplete Ribbon must not mount Qt widgets");
assert(missingWarnings.some(x => x.includes("RIBBON BLOCKED") && x.includes(missingScript)),
  "Missing CAD action must produce actionable diagnostic");
// A missing native Qt widget must be surfaced as a blocker, never silently passed.
const missingWidgetWarnings = [];
const blockedContext = Object.assign({}, context, {
  QVBoxLayout: undefined,
  qWarning: x => missingWidgetWarnings.push(x)
});
vm.runInNewContext(source, blockedContext, { filename: "IMSATCAD.js" });
assert.strictEqual(blockedContext.IMSATCAD.createRibbon(), false, "Missing Qt widget must fail Ribbon creation");
assert(missingWidgetWarnings.some(x => x.includes("RIBBON BLOCKED") && x.includes("Qt widgets unavailable")),
  "Missing Qt widget must emit its own explicit blocker diagnostic");
console.log("IMSAT CAD mocked runtime PASS:", toolbars.size, "toolbars,", menuCount, "unique menu actions");
