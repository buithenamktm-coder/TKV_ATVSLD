/* IMSAT CAD native branding patch for the pinned QCAD CE v3.32.2.0 build.
 * Changes only the application executable's Qt identity, Windows VERSIONINFO,
 * and icon. QCAD's GPLv3 notices, copyrights, and engine source stay intact.
 * The script fails closed if upstream source anchors have changed.
 */
"use strict";
const fs = require("fs");
const path = require("path");

function replaceOnce(text, original, replacement, description) {
    const at = text.indexOf(original);
    if (at < 0 || text.indexOf(original, at + original.length) >= 0) {
        throw new Error("Pinned QCAD source mismatch: " + description);
    }
    return text.slice(0, at) + replacement + text.slice(at + original.length);
}

function patch(root, iconPath) {
    const mainPath = path.join(root, "src", "run", "main.cpp");
    const resourcePath = path.join(root, "src", "run", "qcad.rc");
    const sharedPath = path.join(root, "shared.rc");
    let main = fs.readFileSync(mainPath, "utf8");
    let resource = fs.readFileSync(resourcePath, "utf8");
    let shared = fs.readFileSync(sharedPath, "utf8");

    main = replaceOnce(main, 'qApp->setOrganizationName("QCAD");',
        'qApp->setOrganizationName("IMSAT");', "Qt organization");
    main = replaceOnce(main, 'qApp->setOrganizationDomain("QCAD.org");',
        'qApp->setOrganizationDomain("");', "Qt organization domain");
    main = replaceOnce(main, 'qApp->setApplicationName("QCAD");',
        'qApp->setApplicationName("IMSAT CAD");', "Qt application name");
    main = replaceOnce(main, 'qDebug() << "QCAD version " << R_QCAD_VERSION_STRING;',
        'qDebug() << "IMSAT CAD (QCAD CE GPLv3 engine) version " << R_QCAD_VERSION_STRING;',
        "startup provenance");

    resource = replaceOnce(resource, '#define VER_ORIGINALFILENAME_STR    "qcad.exe"',
        '#define VER_ORIGINALFILENAME_STR    "IMSATCAD.exe"\n#define IMSAT_CAD_BRANDED_EXE 1',
        "Windows executable resource identity");

    // Shared resource metadata is used by multiple QCAD DLLs. Change the
    // executable only; do not mislabel upstream library binaries as IMSAT.
    const branding = [
        "#ifdef IMSAT_CAD_BRANDED_EXE",
        "#undef VER_COMPANYNAME_STR",
        '#define VER_COMPANYNAME_STR "IMSAT"',
        "#undef VER_FILEDESCRIPTION_STR",
        '#define VER_FILEDESCRIPTION_STR "IMSAT CAD V1.0 (QCAD CE GPLv3)"',
        "#undef VER_INTERNALNAME_STR",
        '#define VER_INTERNALNAME_STR "IMSATCAD"',
        "#undef VER_PRODUCTNAME_STR",
        '#define VER_PRODUCTNAME_STR "IMSAT CAD"',
        "#undef VER_PRODUCTVERSION",
        "#define VER_PRODUCTVERSION 1,0,0,0",
        "#undef VER_PRODUCTVERSION_STR",
        '#define VER_PRODUCTVERSION_STR "1.0.0"',
        "#endif",
        ""
    ].join("\n");
    shared = replaceOnce(shared, "VS_VERSION_INFO VERSIONINFO",
        branding + "\nVS_VERSION_INFO VERSIONINFO", "Windows VERSIONINFO");

    if (!fs.existsSync(iconPath) || fs.statSync(iconPath).size < 1024) {
        throw new Error("IMSAT native icon missing or invalid: " + iconPath);
    }
    // Validate every anchor and icon before writing any modified source.
    fs.writeFileSync(mainPath, main);
    fs.writeFileSync(resourcePath, resource);
    fs.writeFileSync(sharedPath, shared);
    fs.copyFileSync(iconPath, path.join(root, "src", "run", "qcad.ico"));
    process.stdout.write("IMSAT CAD native executable branding patch PASS (GPLv3 upstream attribution preserved)\n");
}

if (require.main === module) {
    if (process.argv.length !== 4) {
        throw new Error("Usage: node brand-native-qcad.js <pinned-qcad-root> <imsatcad.ico>");
    }
    patch(path.resolve(process.argv[2]), path.resolve(process.argv[3]));
}
module.exports = { replaceOnce, patch };
