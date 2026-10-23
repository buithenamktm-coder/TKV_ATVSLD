using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace MiningVolume2023.Services
{
    internal static class RuntimeBuildIdentity
    {
        // Show the actual installed bundle SHA, not a hardcoded product label.
        // This lets operators identify a stale DLL or a duplicate installation
        // from the palette itself, without needing to find its EXE filename.
        internal static string ShortTag()
        {
            try
            {
                string dir = Path.GetDirectoryName(typeof(RuntimeBuildIdentity).Assembly.Location);
                string file = Path.Combine(dir, "BUILD_IDENTITY.txt");
                if (!File.Exists(file)) return "BUILD UNVERIFIED";
                foreach (string line in File.ReadLines(file))
                {
                    if (!line.StartsWith("SOURCE_SHA=", StringComparison.Ordinal)) continue;
                    string sha = line.Substring("SOURCE_SHA=".Length).Trim();
                    if (sha.Length == 40 && sha.All(Uri.IsHexDigit))
                        return "BUILD " + sha.Substring(0, 8).ToLowerInvariant();
                }
            }
            catch { }
            return "BUILD UNVERIFIED";
        }

        internal static string Describe()
        {
            var result=new StringBuilder("IMSAT MiningVolume V2.0 — Bùi Thế Nam — ĐT/Zalo: 0967280686\r\n");
            string root=Path.GetDirectoryName(typeof(RuntimeBuildIdentity).Assembly.Location);
            string manifest=Path.Combine(root,"BUILD_IDENTITY.txt");
            result.AppendLine("Loaded plugin directory: "+root);
            string source=null;
            if(File.Exists(manifest)) {
                foreach(var line in File.ReadAllLines(manifest)) {
                    if(line.StartsWith("SOURCE_SHA=")){source=line.Substring(11);result.AppendLine(line);continue;}
                    var fields=line.Split(new[]{' '},StringSplitOptions.RemoveEmptyEntries);
                    if(fields.Length!=2||fields[0].Length!=64||Path.GetFileName(fields[1])!=fields[1])continue;
                    var path=Path.Combine(root,fields[1]);
                    result.AppendLine((File.Exists(path)&&Sha(path).Equals(fields[0],StringComparison.OrdinalIgnoreCase)?"FILE MATCH ":"FILE MISMATCH ")+fields[1]);
                }
            } else result.AppendLine("BUILD MANIFEST MISSING — cannot verify this installation.");
            foreach(var assembly in AppDomain.CurrentDomain.GetAssemblies().Where(x=>x.GetName().Name.StartsWith("MiningVolume",StringComparison.OrdinalIgnoreCase))) {
                string location=assembly.IsDynamic?"dynamic":assembly.Location;
                string version=assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion??"unknown";
                result.AppendLine("LOADED "+assembly.GetName().Name+" | "+version+" | MVID "+assembly.ManifestModule.ModuleVersionId+" | "+location);
                if(Path.GetDirectoryName(location)!=root)result.AppendLine("WARNING: DLL loaded from a different directory. Close AutoCAD and resolve duplicate add-in installations.");
                if(source!=null&&!version.Contains(source))result.AppendLine("WARNING: loaded assembly does not identify this source SHA; restart AutoCAD after upgrade and verify the bundle path.");
            }
            return result.ToString();
        }
        private static string Sha(string file) {
            using(var hash=SHA256.Create())using(var stream=File.OpenRead(file))
                return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-","").ToLowerInvariant();
        }
    }
}
