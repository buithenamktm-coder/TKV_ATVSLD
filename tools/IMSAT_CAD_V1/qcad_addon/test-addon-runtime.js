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
    toolbars.set(id, { objectName: id, actions: [], widgets: [],
      addAction(a) { this.actions.push(a); },
      addWidget(w) { this.widgets.push(w); },
      setToolButtonStyle(style) { this.buttonStyle = style; },
      setAccessibleName(label) { this.accessibleName = label; }
    });
  }
  return toolbars.get(id);
};
const ribbonPages = [];
const ribbonButtons = [];
const ribbonCaptions = [];
const preferences = new Map();
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
  this.setMaximumHeight = height => { this.maximumHeight = height; };
  this.addTab = (page, label) => ribbonPages.push({ page, label });
  this.setTabText = (index, label) => { ribbonPages[index].label = label; };
  Object.defineProperty(this, 'count', { get: () => ribbonPages.length });
}
const context = {
  RSettings: {
    setValue(key, value) { preferences.set(key, value); },
    getStringValue(key, fallback) { return preferences.has(key) ? preferences.get(key) : fallback; },
    getBoolValue(key, fallback) { return preferences.has(key) ? preferences.get(key) : fallback; }
  },
  include() {}, EAction, qsTr: x => x, Qt: { TopToolBarArea: 1, ToolButtonTextUnderIcon: 2, ToolButtonIconOnly: 3, ToolButtonTextBesideIcon: 4 },
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
assert.strictEqual(toolbars.size, 10, "Expected eight legacy toolbars, Quick Access and IMSAT Ribbon");
assert.strictEqual(ribbonPages.length, 6, "Missing IMSAT Ribbon tabs");
const actionButtons = ribbonButtons.filter(b => b.action);
assert(actionButtons.length > 30, "Expected actual Ribbon command buttons");
assert.strictEqual(gridPositions.length, actionButtons.length, "Ribbon grid must contain all command buttons");
assert(gridPositions.every(p => p.row >= 0 && p.row < 3 && p.col >= 0), "Ribbon command grid exceeded three rows");
assert(actionButtons.every(b => b.text && !b.text.endsWith(".js")), "Ribbon button label must not expose script filename");
assert(actionButtons.every(b => b.minimumWidth >= 84), "Ribbon button minimum width missing");
assert(actionButtons.every(b => b.action), "Ribbon button missing native action");
assert.strictEqual(context.IMSATCAD._ribbonButtonCaptions.length, 49, "Missing localized Ribbon commands");
assert(actionButtons.every(b => b.text && b.tooltip.indexOf(b.text) === 0),
  "Native command tooltip must start with the localized command label");
assert(actionButtons.every(b => b.text !== b.action.script.split("/").pop().replace(/\.js$/, "")),
  "Ribbon must not show internal QCAD script identifiers");
const localizedCommands = context.IMSATCAD._ribbonButtonCaptions;
assert(localizedCommands.every(x => x.widget.tooltip === context.IMSATCAD.commandToolTip(x.script, "vi")),
  "Vietnamese tooltips must include only actual registered aliases");
assert.strictEqual(context.IMSATCAD.commandToolTip("scripts/Draw/Line/Line2P/Line2P.js", "vi"),
  "Đường thẳng (LINE, L, LN)");
assert.strictEqual(context.IMSATCAD.commandToolTip("scripts/Draw/Hatch/HatchFromSelection/HatchFromSelection.js", "vi"),
  "Tô mặt cắt", "Commands without registered aliases must not invent shortcuts");
assert(!context.IMSATCAD.commandToolTip("scripts/Draw/Hatch/HatchFromSelection/HatchFromSelection.js", "vi").includes("COPY"),
  "Do not advertise unsupported AutoCAD aliases");
const vietnameseCommandLabels = localizedCommands.map(x => x.widget.text);
const originalCommandActions = localizedCommands.map(x => x.widget.action);
assert.deepStrictEqual(ribbonPages.map(x => x.label), ["Trang chủ", "Chèn", "Ghi chú", "Hiển thị", "Quản lý", "Xuất bản"]);
assert(ribbonPages.every(x => x.page && x.page.parent), "Ribbon page not created");
const expectedGroupCaptions = [
  "Vẽ", "Hiệu chỉnh", "Lớp", "Khối", "Kích thước",
  "Điều hướng", "Lựa chọn", "Tệp và in"
];
assert.deepStrictEqual(ribbonCaptions.map(x => x.label), expectedGroupCaptions,
  "Ribbon panel captions must retain bilingual labels");
assert.strictEqual(new Set(ribbonCaptions.map(x => x.label)).size, ribbonCaptions.length,
  "Duplicate Ribbon panel captions");
assert(context.IMSATCAD._ribbonCreated, "Native ribbon creation flag missing");
assert(context.IMSATCAD._ribbonHost, "Native ribbon host not retained");
const ribbonHostWidgets = toolbars.get("IMSATCADRibbonToolBar").widgets;
assert.strictEqual(ribbonHostWidgets.length, 1,
  "Ribbon tabs and language selector must mount atomically as one toolbar widget");
assert.strictEqual(ribbonHostWidgets[0].objectName, "IMSATCADRibbonShell");
assert.strictEqual(context.IMSATCAD._ribbonTabsWidget.parent, ribbonHostWidgets[0],
  "Ribbon tabs must belong to the single toolbar shell");
assert.strictEqual(context.IMSATCAD._ribbonLanguageButton.objectName, "IMSATCADRibbonLanguageSwitch");
assert.strictEqual(ribbonPages[0].page.objectName, "IMSATCADRibbonPage_Home");
assert(actionButtons.every(b => /^IMSATCADRibbonAction_scripts_/.test(b.objectName)),
  "Native CAD command controls require stable UI Automation identifiers");
assert.strictEqual(new Set(actionButtons.map(b => b.objectName)).size, actionButtons.length,
  "Native Ribbon action identifiers must be unique");
assert.strictEqual(context.IMSATCAD._ribbonLanguageButton.parent, ribbonHostWidgets[0],
  "Language selector must belong to the same toolbar shell");
// The visible Qt selector must switch both tabs and panel labels, twice.
const languageButton = context.IMSATCAD._ribbonLanguageButton;
assert(languageButton && languageButton.onClick, "Ribbon language selector must be interactive");
assert.strictEqual(languageButton.text, "English", "Vietnamese must be the initial Ribbon language");
assert.strictEqual(languageButton.tooltip, "Switch Ribbon to English");
languageButton.click();
assert.strictEqual(context.IMSATCAD._ribbonLanguage, "en");
assert.deepStrictEqual(ribbonPages.map(x => x.label), ["Home", "Insert", "Annotate", "View", "Manage", "Output"]);
assert.deepStrictEqual(ribbonCaptions.map(x => x.label), ["Draw", "Modify", "Layers", "Blocks", "Dimensions", "Navigation", "Selection", "File & plot"]);
assert.strictEqual(languageButton.text, "Tiếng Việt");
assert(localizedCommands.every(x => x.widget.text === context.IMSATCAD.commandLabel(x.script, "en")),
  "Switch to English must translate every command button");
assert(localizedCommands.every(x => x.widget.tooltip === context.IMSATCAD.commandToolTip(x.script, "en")),
  "English tooltips must show registered aliases with translated labels");
assert.strictEqual(context.IMSATCAD.commandToolTip("scripts/Draw/Line/Line2P/Line2P.js", "en"),
  "Line (LINE, L, LN)");
assert.strictEqual(languageButton.tooltip, "Chuyển Ribbon sang tiếng Việt");
assert.strictEqual(context.IMSATCAD.setRibbonLanguage("xx"), false, "Unsupported locale must be rejected");
languageButton.click();
assert.deepStrictEqual(ribbonPages.map(x => x.label), ["Trang chủ", "Chèn", "Ghi chú", "Hiển thị", "Quản lý", "Xuất bản"]);
assert.deepStrictEqual(ribbonCaptions.map(x => x.label), expectedGroupCaptions);
assert.strictEqual(languageButton.text, "English");
assert.strictEqual(languageButton.tooltip, "Switch Ribbon to English");
assert.deepStrictEqual(localizedCommands.map(x => x.widget.text), vietnameseCommandLabels,
  "Switch back to Vietnamese must restore all 49 command captions");
assert(localizedCommands.every(x => x.widget.tooltip === context.IMSATCAD.commandToolTip(x.script, "vi")),
  "Switch back must restore Vietnamese command tooltips and registered aliases");
assert(localizedCommands.every((x, i) => x.widget.action === originalCommandActions[i]),
  "Language switching must not replace native CAD actions");
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
// A broken command button API must not partially change the active language.
const firstCommand = localizedCommands[0].widget;
const originalTooltipSetter = firstCommand.setToolTip;
firstCommand.setToolTip = undefined;
assert.strictEqual(context.IMSATCAD.setRibbonLanguage("en"), false,
  "Missing native command tooltip API must reject the language switch");
assert.strictEqual(context.IMSATCAD._ribbonLanguage, "vi");
assert.deepStrictEqual(localizedCommands.map(x => x.widget.text), vietnameseCommandLabels);
firstCommand.setToolTip = originalTooltipSetter;
// A setter can throw after API preflight; rollback must restore the entire Ribbon.
const originalCommandTextSetter = firstCommand.setText;
firstCommand.setText = function(label) {
  if (label === "Line") throw new Error("mock Qt setter failure");
  return originalCommandTextSetter(label);
};
assert.strictEqual(context.IMSATCAD.setRibbonLanguage("en"), false,
  "Native setter exceptions must be reported as failed language switches");
assert.strictEqual(context.IMSATCAD._ribbonLanguage, "vi",
  "Qt setter exception must restore the previous locale");
assert.deepStrictEqual(ribbonPages.map(x => x.label),
  ["Trang chủ", "Chèn", "Ghi chú", "Hiển thị", "Quản lý", "Xuất bản"]);
assert.deepStrictEqual(ribbonCaptions.map(x => x.label), expectedGroupCaptions);
assert.deepStrictEqual(localizedCommands.map(x => x.widget.text), vietnameseCommandLabels,
  "Native setter failure must restore every command caption");
assert(localizedCommands.every(x => x.widget.tooltip === context.IMSATCAD.commandToolTip(x.script, "vi")),
  "Native setter failure must restore all command tooltips");
assert.strictEqual(languageButton.text, "English");
firstCommand.setText = originalCommandTextSetter;
// Qt bridges can expose QTabWidget.count as count() instead of a property.
const actualTabWidget = context.IMSATCAD._ribbonTabsWidget;
const callableCountTabs = {
  count() { return actualTabWidget.count; },
  setTabText(index, label) { actualTabWidget.setTabText(index, label); }
};
context.IMSATCAD._ribbonTabsWidget = callableCountTabs;
assert.strictEqual(context.IMSATCAD.setRibbonLanguage("en"), true,
  "Callable native Qt tab count must allow English locale");
assert.deepStrictEqual(ribbonPages.map(x => x.label),
  ["Home", "Insert", "Annotate", "View", "Manage", "Output"]);
assert.strictEqual(context.IMSATCAD.setRibbonLanguage("vi"), true,
  "Callable native Qt tab count must allow Vietnamese locale");
assert.deepStrictEqual(ribbonPages.map(x => x.label),
  ["Trang chủ", "Chèn", "Ghi chú", "Hiển thị", "Quản lý", "Xuất bản"]);
context.IMSATCAD._ribbonTabsWidget = {
  count() { throw new Error("native count failed"); },
  setTabText() { throw new Error("must not update tabs"); }
};
assert.strictEqual(context.IMSATCAD.setRibbonLanguage("en"), false,
  "Qt tab count exception must block partial language updates");
assert.strictEqual(context.IMSATCAD._ribbonLanguage, "vi");
context.IMSATCAD._ribbonTabsWidget = actualTabWidget;
// QTabWidget.count is a non-configurable getter in the Qt mock.
// Do not mutate it; the native count guard is exercised by integration checks.
const first = [...toolbars.values()].map(x => x.actions.length);
assert(first.filter(x => x >= 2).length === 9, "CAD toolbars and Quick Access toolbar missing");
const quickAccess = toolbars.get("IMSATCADQuickAccessToolBar");
assert(quickAccess, "Native Quick Access toolbar missing");
assert.strictEqual(quickAccess.buttonStyle, context.Qt.ToolButtonIconOnly,
  "Quick Access must remain compact and icon-only");
assert.strictEqual(quickAccess.accessibleName, "IMSAT CAD Quick Access",
  "Quick Access needs stable native accessibility metadata");
assert.deepStrictEqual(quickAccess.actions.map(x => x.script), [
  "scripts/File/Save/Save.js",
  "scripts/Edit/Undo/Undo.js",
  "scripts/Edit/Redo/Redo.js"
], "Quick Access must reuse exactly three existing QCAD commands");
assert(quickAccess.actions.every(x => menu.actions.includes(x)),
  "Quick Access actions must share their native QCAD action with the menu");
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
// Regression: one throwing native QCAD alias lookup must not interrupt the
// remaining command registrations or Ribbon/workspace initialization.
const badAlias = "scripts/Draw/Line/Line2P/Line2P.js";
const laterAlias = "scripts/Draw/Polyline/DrawPolyline/DrawPolyline.js";
const originalLookup = context.RGuiAction.getByScriptFile;
const originalCreateRibbon = context.IMSATCAD.createRibbon;
const originalPopulateToolBar = context.IMSATCAD.populateToolBar;
const originalHideToolBars = context.IMSATCAD.hideDuplicateToolBars;
const laterAction = actions.get(laterAlias);
const originalLaterSetter = laterAction.setDefaultCommands;
let laterRegistrations = 0;
let ribbonReached = false;
laterAction.setDefaultCommands = function(commands) {
  laterRegistrations++;
  return originalLaterSetter.call(this, commands);
};
context.RGuiAction.getByScriptFile = function(script) {
  if (script === badAlias) throw new Error("simulated native alias lookup failure");
  return originalLookup(script);
};
context.IMSATCAD.createRibbon = function() { ribbonReached = true; return true; };
context.IMSATCAD.populateToolBar = function() { return 0; };
context.IMSATCAD.hideDuplicateToolBars = function() {};
const warningsBeforeAliasError = warnings.length;
context.IMSATCAD.postInit();
assert(ribbonReached, "One native alias failure must not block Ribbon initialization");
assert.strictEqual(laterRegistrations, 1, "Later aliases must still be registered");
assert(warnings.slice(warningsBeforeAliasError).some(x =>
  x.includes(badAlias) && x.includes("simulated native alias lookup failure")),
  "Native alias exception must include script path and original error");
assert(messages.some(x => x.includes((context.IMSATCAD._aliases.length - 1) +
  "/" + context.IMSATCAD._aliases.length + " aliases")),
  "Startup status must report partial alias registration accurately");
// The Ribbon must not advertise shortcuts when native QCAD registration failed.
assert.strictEqual(context.IMSATCAD.commandToolTip(badAlias, "vi"), "Đường thẳng",
  "Failed native alias must disappear from the command hint");
assert.strictEqual(localizedCommands.find(x => x.script === badAlias).widget.tooltip, "Đường thẳng",
  "Already mounted Ribbon must refresh stale hints after alias failure");
assert.strictEqual(context.IMSATCAD.commandToolTip(laterAlias, "vi"),
  "Đa tuyến (PLINE, PL, POLYLINE)",
  "Successful later aliases must remain visible");
context.RGuiAction.getByScriptFile = originalLookup;
context.IMSATCAD.createRibbon = originalCreateRibbon;
context.IMSATCAD.populateToolBar = originalPopulateToolBar;
context.IMSATCAD.hideDuplicateToolBars = originalHideToolBars;
laterAction.setDefaultCommands = originalLaterSetter;
// Regression: an exception in one native toolbar or menu operation must not
// interrupt subsequent CAD commands, and retry must not duplicate actions.
const recoveryLookup = "scripts/Test/Lookup/Lookup.js";
const recoveryToolbar = "scripts/Test/ToolbarRetry/ToolbarRetry.js";
const recoveryMenu = "scripts/Test/MenuRetry/MenuRetry.js";
const recoveryBar = {
  objectName: "IMSATCADRecoveryToolBar",
  actions: [],
  failedOnce: false,
  addAction(action) {
    if (action.script === recoveryToolbar && !this.failedOnce) {
      this.failedOnce = true;
      throw new Error("mock toolbar insertion failure");
    }
    this.actions.push(action);
  }
};
const originalMenuAdd = menu.addAction;
const originalRecoveryLookup = context.RGuiAction.getByScriptFile;
let menuFailedOnce = false;
menu.addAction = function(action) {
  if (action.script === recoveryMenu && !menuFailedOnce) {
    menuFailedOnce = true;
    throw new Error("mock menu insertion failure");
  }
  return originalMenuAdd.call(this, action);
};
context.RGuiAction.getByScriptFile = function(script) {
  if (script === recoveryLookup) throw new Error("mock toolbar lookup failure");
  return originalRecoveryLookup(script);
};
const recoveryScripts = [recoveryLookup, recoveryToolbar, recoveryMenu];
const recoveryWarningStart = warnings.length;
assert.strictEqual(context.IMSATCAD.populateToolBar(recoveryBar, recoveryScripts), 1,
  "Toolbar should continue after lookup and insertion failures");
assert.strictEqual(recoveryBar.actions.length, 1);
assert(!menu.actions.some(x => x.script === recoveryMenu),
  "Failed menu insertion must not be marked as successful");
assert.strictEqual(context.IMSATCAD.populateToolBar(recoveryBar, recoveryScripts), 1,
  "Retry must insert only the previously failed toolbar action");
assert.strictEqual(recoveryBar.actions.length, 2,
  "Retry must not duplicate already inserted toolbar actions");
assert.strictEqual(menu.actions.filter(x => x.script === recoveryMenu).length, 1,
  "Retry must recover menu insertion without duplicating toolbar actions");
assert.strictEqual(menu.actions.filter(x => x.script === recoveryToolbar).length, 1,
  "Recovered toolbar action must appear in the menu once");
assert.strictEqual(context.IMSATCAD.populateToolBar(recoveryBar, recoveryScripts), 0,
  "Repeated initialization must be idempotent");
assert.strictEqual(recoveryBar.actions.length, 2);
assert.strictEqual(menu.actions.filter(x => x.script === recoveryMenu).length, 1);
assert(warnings.slice(recoveryWarningStart).some(x =>
  x.includes(recoveryLookup) && x.includes("mock toolbar lookup failure")),
  "Lookup exception must identify the failing CAD action");
assert(warnings.slice(recoveryWarningStart).some(x =>
  x.includes(recoveryToolbar) && x.includes("mock toolbar insertion failure")),
  "Toolbar insertion exception must identify the failing CAD action");
assert(warnings.slice(recoveryWarningStart).some(x =>
  x.includes(recoveryMenu) && x.includes("mock menu insertion failure")),
  "Menu insertion exception must identify the failing CAD action");
menu.addAction = originalMenuAdd;
context.RGuiAction.getByScriptFile = originalRecoveryLookup;
console.log("IMSAT CAD mocked runtime PASS:", toolbars.size, "toolbars,", menuCount, "unique menu actions");

// Preferences survive a new shell and invalid locales never replace a valid one.
assert(context.IMSATCAD.setRibbonLanguage("en"));
assert.strictEqual(preferences.get("IMSATCAD/RibbonLanguage"), "en");
assert.strictEqual(context.IMSATCAD.setRibbonLanguage("bad"), false);
assert.strictEqual(preferences.get("IMSATCAD/RibbonLanguage"), "en");
const collapse = context.IMSATCAD._ribbonCollapseButton;
collapse.click();
assert.strictEqual(context.IMSATCAD._ribbonTabsWidget.maximumHeight, 36);
assert.strictEqual(preferences.get("IMSATCAD/RibbonCollapsed"), true);
collapse.click();
assert.strictEqual(context.IMSATCAD._ribbonTabsWidget.maximumHeight, 16777215);
assert.strictEqual(preferences.get("IMSATCAD/RibbonCollapsed"), false);
assert(context.IMSATCAD.setRibbonLanguage("vi"));
console.log("Ribbon locale persistence and collapse/expand PASS (mocked API)");
