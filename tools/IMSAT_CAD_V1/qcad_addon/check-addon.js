// Static integration gate for IMSAT CAD add-on. No QCAD runtime is simulated.
"use strict";
const fs = require("fs");
const path = require("path");
const assert = require("assert");
const source = fs.readFileSync(path.join(__dirname, "IMSATCAD", "IMSATCAD.js"), "utf8");
const groups = ["draw", "modify", "annotation", "layer", "utility", "block", "selection", "file"];
const all = [];
const ownership = new Map();
for (const group of groups) {
  const regex = new RegExp("IMSATCAD\\._" + group + "Scripts\\s*=\\s*\\[([\\s\\S]*?)\\];");
  const match = source.match(regex);
  assert(match, "Missing toolbar group: " + group);
  const paths = [...match[1].matchAll(/"(scripts\/[^"]+\.js)"/g)].map(m => m[1]);
  assert(paths.length >= 2, "Insufficient actions for " + group);
  assert.strictEqual(paths.length, new Set(paths).size, "Duplicate actions in " + group);
  for (const script of paths) {
    assert(!ownership.has(script), "Ribbon command appears in multiple groups: " + script);
    ownership.set(script, group);
  }
  all.push(...paths);
}
// A command should appear once in the Ribbon and in its task-appropriate group.
for (const [script, group] of [
  ["scripts/Draw/Hatch/HatchFromSelection/HatchFromSelection.js", "draw"],
  ["scripts/Draw/Text/Text.js", "annotation"],
  ["scripts/Edit/Undo/Undo.js", "modify"],
  ["scripts/Edit/Redo/Redo.js", "modify"],
  ["scripts/File/Print/Print.js", "file"]
]) {
  assert.strictEqual(ownership.get(script), group, "Incorrect Ribbon group: " + script);
}
for (const [script, group] of ownership) {
  if (group === "utility") assert(script.startsWith("scripts/View/"), "View group contains unrelated command: " + script);
}
const aliases = [...source.matchAll(/\["(scripts\/[^"]+\.js)",\s*\[([^\]]+)\]\]/g)];
assert(aliases.length >= 15, "Too few native command aliases");
const seen = new Map();
for (const [_, script, commandList] of aliases) {
  all.push(script);
  const commands = [...commandList.matchAll(/"([^"]+)"/g)].map(m => m[1].toLowerCase());
  for (const command of commands) {
    assert(!seen.has(command), "Conflicting alias " + command + ": " + seen.get(command) + " / " + script);
    seen.set(command, script);
  }
}
for (const unsafe of ["copy", "co", "cp", "extend", "ex"]) {
  assert(!seen.has(unsafe), "Unimplemented AutoCAD-equivalent command advertised: " + unsafe);
}
assert(source.includes("IMSATCAD._installedActions"), "Repeated init guard missing");
console.log("IMSAT CAD static integration PASS:", groups.length, "toolbars,", all.length, "script references,", seen.size, "aliases");
