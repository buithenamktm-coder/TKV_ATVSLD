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
    toolbars.set(id, { objectName: id, actions: [], addAction(a) { this.actions.push(a); } });
  }
  return toolbars.get(id);
};
const context = {
  include() {}, EAction, qsTr: x => x, Qt: { TopToolBarArea: 1 },
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
assert.strictEqual(toolbars.size, 8, "Incorrect toolbar count");
const first = [...toolbars.values()].map(x => x.actions.length);
assert(first.every(x => x >= 2), "Empty toolbar");
assert.strictEqual(menu.actions.length, new Set(menu.actions).size, "Duplicate menu actions");
const menuCount = menu.actions.length;
context.IMSATCAD.postInit();
assert.deepStrictEqual([...toolbars.values()].map(x => x.actions.length), first, "Repeated init duplicated toolbar actions");
assert.strictEqual(menu.actions.length, menuCount, "Repeated init duplicated menu actions");
assert.strictEqual(warnings.length, 0, "Unexpected runtime warning: " + warnings.join("; "));
assert(messages.length >= 1, "Missing initialization status");
console.log("IMSAT CAD mocked runtime PASS:", toolbars.size, "toolbars,", menuCount, "unique menu actions");
