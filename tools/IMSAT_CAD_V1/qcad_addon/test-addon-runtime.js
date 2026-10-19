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
function QWidget(parent) { this.parent = parent; }
function QHBoxLayout(page) {
  this.page = page;
  this.widgets = [];
  this.addWidget = button => this.widgets.push(button);
  this.addStretch = () => {};
}
function QToolButton(parent) {
  this.parent = parent;
  this.setDefaultAction = action => { this.action = action; };
}
function QTabWidget(parent) {
  this.parent = parent;
  this.addTab = (page, label) => ribbonPages.push({ page, label });
}
const context = {
  include() {}, EAction, qsTr: x => x, Qt: { TopToolBarArea: 1, ToolButtonTextUnderIcon: 2 },
  QWidget, QHBoxLayout, QToolButton, QTabWidget,
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
assert.deepStrictEqual(ribbonPages.map(x => x.label), ["Home", "Insert", "Annotate", "View", "Manage"]);
assert(ribbonPages.every(x => x.page && x.page.parent), "Ribbon page not created");
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
assert.strictEqual(warnings.length, 0, "Unexpected runtime warning: " + warnings.join("; "));
assert(messages.length >= 1, "Missing initialization status");
console.log("IMSAT CAD mocked runtime PASS:", toolbars.size, "toolbars,", menuCount, "unique menu actions");
