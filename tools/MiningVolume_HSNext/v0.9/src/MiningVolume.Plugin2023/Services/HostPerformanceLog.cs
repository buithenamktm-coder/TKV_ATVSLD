using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace MiningVolume2023.Services
{
    /// <summary>
    /// Measurements from the REAL AutoCAD host, not the Linux/.NET Core
    /// benchmark. Never throw from telemetry: failed filesystem access must
    /// not turn an accepted CAD transaction into a user-visible failure.
    /// No DWG filename, user name, geometry coordinates or source data leaves
    /// the local workstation.
    /// </summary>
    internal static class HostPerformanceLog
    {
        private static readonly object Sync = new object();

        internal static string FilePath
        {
            get
            {
                string root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                if (string.IsNullOrWhiteSpace(root)) root = Path.GetTempPath();
                return Path.Combine(root, "IMSAT_VOLUME", "Logs", "autocad2023-perf.tsv");
            }
        }

        internal static void Record(string operation, string outcome, Guid drawingFingerprint,
            int items, int completed, int geometryCount, double totalMs,
            IReadOnlyList<double> batches = null)
        {
            try
            {
                // Stable, machine-parseable, invariant numeric formatting:
                // one line per completed, canceled or failed operation.
                string sourceSha = ReadSourceSha();
                string batchTimes = batches == null
                    ? ""
                    : string.Join(";", batches.Select(x => x.ToString("F3", CultureInfo.InvariantCulture)));
                var line = string.Join("\t", new[]
                {
                    DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                    operation,
                    outcome,
                    drawingFingerprint.ToString("D"),
                    sourceSha,
                    typeof(HostPerformanceLog).Assembly.ManifestModule.ModuleVersionId.ToString("D"),
                    items.ToString(CultureInfo.InvariantCulture),
                    completed.ToString(CultureInfo.InvariantCulture),
                    geometryCount.ToString(CultureInfo.InvariantCulture),
                    totalMs.ToString("F3", CultureInfo.InvariantCulture),
                    batchTimes
                });
                lock (Sync)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                    if (!File.Exists(FilePath) || new FileInfo(FilePath).Length == 0)
                        File.AppendAllText(FilePath,
                            "utc\toperation\tstatus\tdrawing_fingerprint\tsource_sha\tassembly_mvid\titems\tcompleted\tgeometry_count\telapsed_ms\tbatch_ms" +
                            Environment.NewLine);
                    File.AppendAllText(FilePath, line + Environment.NewLine);
                }
            }
            catch
            {
                // Telemetry must not change official volumes, CAD geometry,
                // transaction atomicity or user experience.
            }
        }

        private static string ReadSourceSha()
        {
            string root = Path.GetDirectoryName(typeof(HostPerformanceLog).Assembly.Location);
            string file = Path.Combine(root, "BUILD_IDENTITY.txt");
            if (!File.Exists(file)) return "UNKNOWN";
            foreach (var line in File.ReadLines(file))
                if (line.StartsWith("SOURCE_SHA=", StringComparison.Ordinal))
                {
                    string sha = line.Substring("SOURCE_SHA=".Length).Trim();
                    return sha.Length == 40 && sha.All(Uri.IsHexDigit) ? sha : "UNKNOWN";
                }
            return "UNKNOWN";
        }
    }
}
