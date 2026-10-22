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
function QLabel(label, parent) {
  this.label = label; this.parent = parent;
  this.setText = text => { this.label = text; };
  ribbonCaptions.push(this);
}
function QToolButton(parent) {
  this.parent = parent;
  this.setDefaultAction = action => { this.action = action; };
  this.setText = label => { this.text = label; };
  this.setToolTip = tooltip => { this.tooltip = tooltip; };
  this.setMinimumWidth = width => { this.minimumWidth = width; };
  this.clicked = { connect: callback => { this.onClick = callback; } };
  this.click = () => { if (this.onClick) this.onClick(); };
  ribbonButtons.push(this);
}
function QTabWidget(parent) {
  this.parent = parent;
  this.addTab = (page, label) => ribbonPages.push({ page, label });
  this.setTabText = (index, label) => { ribbonPages[index].label = label; };
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
const actionButtons = ribbonButtons.filter(b => b.action);
assert(actionButtons.length > 30, "Expected actual Ribbon command buttons");
assert.strictEqual(gridPositions.length, actionButtons.length, "Ribbon grid must contain all command buttons");
assert(gridPositions.every(p => p.row >= 0 && p.row < 3 && p.col >= 0), "Ribbon command grid exceeded three rows");
assert(actionButtons.every(b => b.text && !b.text.endsWith(".js")), "Ribbon button label must not expose script filename");
assert(actionButtons.every(b => b.minimumWidth >= 84), "Ribbon button minimum width missing");
assert(actionButtons.every(b => b.action), "Ribbon button missing native action");
assert.deepStrictEqual(ribbonPages.map(x => x.label), ["Trang chủ", "Chèn", "Ghi chú", "Hiển thị", "Quản lý", "Xuất bản"]);
assert(ribbonPages.every(x => x.page && x.page.parent), "Ribbon page not created");
const expectedGroupCaptions = [
  "Vẽ", "Hiệu chỉnh", "Lớp", "Khối", "Kích thước",
  "Hiển thị và công cụ", "Lựa chọn", "Tệp và in"
];
assert.deepStrictEqual(ribbonCaptions.map(x => x.label), expectedGroupCaptions,
  "Ribbon panel captions must retain bilingual labels");
assert.strictEqual(new Set(ribbonCaptions.map(x => x.label)).size, ribbonCaptions.length,
  "Duplicate Ribbon panel captions");
assert(context.IMSATCAD._ribbonCreated, "Native ribbon creation flag missing");
assert(context.IMSATCAD._ribbonHost, "Native ribbon host not retained");
assert.strictEqual(toolbars.get("IMSATCADRibbonToolBar").widgets.length, 2, "Ribbon or language selector duplicated");
// The visible Qt selector must switch both tabs and panel labels, twice.
const languageButton = context.IMSATCAD._ribbonLanguageButton;
assert(languageButton && languageButton.onClick, "Ribbon language selector must be interactive");
assert.strictEqual(languageButton.text, "English", "Vietnamese must be the initial Ribbon language");
assert.strictEqual(languageButton.tooltip, "Switch Ribbon to English");
languageButton.click();
assert.strictEqual(context.IMSATCAD._ribbonLanguage, "en");
assert.deepStrictEqual(ribbonPages.map(x => x.label), ["Home", "Insert", "Annotate", "View", "Manage", "Output"]);
assert.deepStrictEqual(ribbonCaptions.map(x => x.label), ["Draw", "Modify", "Layers", "Blocks", "Dimensions", "View & tools", "Selection", "File & plot"]);
assert.strictEqual(languageButton.text, "Tiếng Việt");
assert.strictEqual(languageButton.tooltip, "Chuyển Ribbon sang tiếng Việt");
assert.strictEqual(context.IMSATCAD.setRibbonLanguage("xx"), false, "Unsupported locale must be rejected");
languageButton.click();
assert.deepStrictEqual(ribbonPages.map(x => x.label), ["Trang chủ", "Chèn", "Ghi chú", "Hiển thị", "Quản lý", "Xuất bản"]);
assert.deepStrictEqual(ribbonCaptions.map(x => x.label), expectedGroupCaptions);
assert.strictEqual(languageButton.text, "English");
assert.strictEqual(languageButton.tooltip, "Switch Ribbon to English");
// A broken Qt language control must not leave the Ribbon half-translated.
const beforeLocale = context.IMSATCAD._ribbonLanguage;
const beforeTabLabels = ribbonPages.map(x => x.label);
const beforePanelLabels = ribbonCaptions.map(x => x.label);
const firstCaption = context.IMSATCAD._ribbonCaptions[0].widget;
const savedSetText = firstCaption.setText;
firstCaption.setText = undefined;
assert.strictEqual(context.IMSATCAD.setRibbonLanguage("en"), false,
  "Language switch must reject a missing panel label API");
assert.strictEqual(context.IMSATCAD._ribbonLanguage, beforeLocale,
  "Failed language switch must not change locale state");
assert.deepStrictEqual(ribbonPages.map(x => x.label), beforeTabLabels,
  "Failed language switch must not modify tab labels");
assert.deepStrictEqual(ribbonCaptions.map(x => x.label), beforePanelLabels,
  "Failed language switch must not modify panel labels");
firstCaption.setText = savedSetText;
const tabsWidget = context.IMSATCAD._ribbonTabsWidget;
const savedCount = tabsWidget.count;
Object.defineProperty(tabsWidget, "count", { configurable: true, value: savedCount - 1 });
assert.strictEqual(context.IMSATCAD.setRibbonLanguage("en"), false,
  "Language switch must reject an incomplete Ribbon tab set");
assert.strictEqual(context.IMSATCAD._ribbonLanguage, beforeLocale,
  "Tab count mismatch must not change locale");
assert.deepStrictEqual(ribbonPages.map(x => x.label), beforeTabLabels,
  "Tab count mismatch must preserve existing tab labels");
Object.defineProperty(tabsWidget, "count", { configurable: true, value: savedCount });
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
assert(messages.some(x => x.includes("Ribbon=CREATED_NOT_GUI_TESTED")), "Startup message must expose Ribbon readiness");
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
// A native API exception during lookup must fail safely with an actionable error.
const thrownWarnings = [];
const thrownContext = Object.assign({}, context, {
  qWarning: x => thrownWarnings.push(x),
  RGuiAction: { getByScriptFile(script) {
    if (script === missingScript) throw new Error("mock native lookup failure");
    return actions.get(script);
  } }
});
vm.runInNewContext(source, thrownContext, { filename: "IMSATCAD.js" });
assert.strictEqual(thrownContext.IMSATCAD.createRibbon(), false,
  "Native lookup exception must not escape or create Ribbon");
assert(thrownWarnings.some(x => x.includes("RIBBON BLOCKED") && x.includes(missingScript) && x.includes("mock native lookup failure")),
  "Native lookup exception must identify failing command");
// A toolbar host without Qt widget insertion must block the Ribbon cleanly.
const brokenHostWarnings = [];
const brokenHostContext = Object.assign({}, context, {
  qWarning: x => brokenHostWarnings.push(x),
  EAction: Object.assign(function BrokenEAction() {}, EAction, {
    getToolBar: (label, id) => id === "IMSATCADRibbonToolBar" ? {} : EAction.getToolBar(label, id)
  })
});
brokenHostContext.EAction.prototype = EAction.prototype;
vm.runInNewContext(source, brokenHostContext, { filename: "IMSATCAD.js" });
assert.strictEqual(brokenHostContext.IMSATCAD.createRibbon(), false,
  "Ribbon must reject a toolbar host without addWidget");
assert(brokenHostWarnings.some(x => x.includes("toolbar host cannot accept widgets")),
  "Broken toolbar host must emit a diagnostic");
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
