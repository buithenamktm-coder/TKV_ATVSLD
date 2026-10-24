using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;

namespace MiningVolume2023.Services
{
    public enum LicenseState
    {
        Trial, Activated, TrialExpired, LicenseExpired, ClockRollback, InvalidLicense, StorageError
    }

    public sealed class LicenseStatus
    {
        public LicenseState State { get; internal set; }
        public bool CanCalculate => State == LicenseState.Trial || State == LicenseState.Activated;
        public int DaysRemaining { get; internal set; }
        public string Customer { get; internal set; }
        public string Description { get; internal set; }
        public string Tier { get; internal set; }
        public DateTime? ActivatedUtc { get; internal set; }
        public DateTime? ExpiresUtc { get; internal set; }
    }

    [DataContract]
    internal sealed class TrialLedger
    {
        [DataMember(Order = 1)] public DateTime FirstUseUtc { get; set; }
        [DataMember(Order = 2)] public DateTime LastUseUtc { get; set; }
    }

    // Only for reading the user's existing IMSAT trial before rebranding.
    // No MachineId is calculated, verified or collected by NAMVECTA licensing.
    [DataContract]
    internal sealed class LegacyTrialLedger
    {
        [DataMember(Order = 1)] public string MachineId { get; set; }
        [DataMember(Order = 2)] public DateTime FirstUseUtc { get; set; }
        [DataMember(Order = 3)] public DateTime LastUseUtc { get; set; }
    }

    [DataContract]
    internal sealed class UsedPass
    {
        [DataMember(Order = 1)] public string Tier { get; set; }
        [DataMember(Order = 2)] public DateTime FirstActivationUtc { get; set; }
    }

    [DataContract]
    internal sealed class ActivationLedger
    {
        [DataMember(Order = 1)] public string ActiveTier { get; set; }
        [DataMember(Order = 2)] public DateTime LastSeenUtc { get; set; }
        [DataMember(Order = 3)] public List<UsedPass> History { get; set; }
    }

    /// <summary>
    /// User-approved offline model: five fixed passes, no MachineId, network,
    /// server, license file, or RSA issuer. Hash constants avoid storing the
    /// actual passwords in source. They do NOT make a fixed offline pass
    /// tamper-proof: a shared pass can be used on different computers.
    /// The first activation timestamp per pass is kept when re-entering it.
    /// </summary>
    public static class LicenseService
    {
        private const string RegistryPath = @"Software\NAMVECTA\VOLUME\License";
        private const string LegacyRegistryPath = @"Software\IMSAT\MiningVolume\License";
        private const string RegistryTrialValue = "TrialLedger";
        private const string RegistryActivationValue = "ActivationLedger";
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("NAMVECTA.VOLUME.2.OfflinePass.v1");
        private static readonly byte[] LegacyEntropy = Encoding.UTF8.GetBytes("IMSAT.MiningVolume.V2.0.OfflineLicense.v1");
        private static readonly object Sync = new object();
        private static readonly string[] PassHashes =
        {
            "840101A2E1C31516A53C2919D691C73DAEBDC2964BD9F38A615A8E18AB4532DF",
            "4BBB2B48F26FE5FF92E972AC039A8D7882F2F6ED3655C1A08E7539E9E81979CD",
            "0211CC119D3BB75670FAC029F3EF846967A742A13074BEC0B3BADBCC0A463637",
            "F8B7A5C1E840DF2C9B086181AE3DC2E8237234F31D0B584559741E114AF55626",
            "5FC0C20BF81664AD89AF90A4052FE16A06DF66E6157836E815EA518B40179CDA"
        };
        private static readonly string[] TierNames =
            { "1 tháng", "3 tháng", "6 tháng", "1 năm", "Vĩnh viễn" };
        private static readonly int[] TierMonths = { 1, 3, 6, 12, 0 };

        private static string RootDirectory =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "NAMVECTA", "Volume", "License");
        private static string TrialFile => Path.Combine(RootDirectory, "trial.dat");
        private static string ActivationFile => Path.Combine(RootDirectory, "activation.dat");

        public static LicenseStatus GetStatus()
        {
            lock (Sync)
            {
                try
                {
                    DateTime now = DateTime.UtcNow;
                    var activation = LoadActivation();
                    if (activation != null)
                    {
                        if (IsClockRollback(now, activation.LastSeenUtc))
                            return new LicenseStatus
                            {
                                State = LicenseState.ClockRollback,
                                Description = "Đồng hồ Windows lùi quá 1 giờ. Hãy kiểm tra ngày giờ."
                            };
                        int tier = Array.IndexOf(TierNames, activation.ActiveTier);
                        var used = activation.History?.FirstOrDefault(x => x.Tier == activation.ActiveTier);
                        if (tier < 0 || used == null || used.FirstActivationUtc == default(DateTime))
                            return new LicenseStatus { State = LicenseState.InvalidLicense,
                                Description = "Dữ liệu kích hoạt không hợp lệ." };
                        activation.LastSeenUtc = Max(now, activation.LastSeenUtc);
                        Persist(activation, ActivationFile, RegistryActivationValue);
                        DateTime expiry = TierMonths[tier] == 0
                            ? DateTime.MaxValue : used.FirstActivationUtc.AddMonths(TierMonths[tier]);
                        bool valid = TierMonths[tier] == 0 || now < expiry;
                        return new LicenseStatus
                        {
                            State = valid ? LicenseState.Activated : LicenseState.LicenseExpired,
                            Tier = activation.ActiveTier,
                            Customer = "Bản quyền NAMVECTA VOLUME",
                            ActivatedUtc = used.FirstActivationUtc,
                            ExpiresUtc = TierMonths[tier] == 0 ? (DateTime?)null : expiry,
                            DaysRemaining = TierMonths[tier] == 0 ? int.MaxValue :
                                Math.Max(0, (int)Math.Ceiling((expiry - now).TotalDays)),
                            Description = valid
                                ? "Đã kích hoạt: " + activation.ActiveTier
                                : "Giấy phép " + activation.ActiveTier + " đã hết hạn. Nhập Pass hợp lệ để tiếp tục."
                        };
                    }

                    TrialLedger trial = LoadTrial();
                    if (trial == null)
                        trial = ReadLegacyTrial() ?? new TrialLedger { FirstUseUtc = now, LastUseUtc = now };
                    if (trial.FirstUseUtc == default(DateTime) || trial.LastUseUtc < trial.FirstUseUtc)
                        throw new InvalidDataException("Dữ liệu dùng thử không hợp lệ.");
                    if (IsClockRollback(now, trial.LastUseUtc))
                        return new LicenseStatus { State = LicenseState.ClockRollback,
                            Description = "Đồng hồ Windows lùi quá 1 giờ. Hãy kiểm tra ngày giờ." };
                    trial.LastUseUtc = Max(now, trial.LastUseUtc);
                    Persist(trial, TrialFile, RegistryTrialValue);
                    double daysLeft = (trial.FirstUseUtc.AddDays(15) - now).TotalDays;
                    if (daysLeft <= 0)
                        return new LicenseStatus { State = LicenseState.TrialExpired, DaysRemaining = 0,
                            Description = "Đã hết 15 ngày dùng thử. Hãy nhập Pass để tính khối lượng." };
                    int days = (int)Math.Ceiling(daysLeft);
                    return new LicenseStatus { State = LicenseState.Trial, DaysRemaining = days,
                        Description = "Dùng thử: còn " + days + " ngày." };
                }
                catch (Exception ex)
                {
                    return new LicenseStatus { State = LicenseState.StorageError,
                        Description = "Không đọc được thông tin bản quyền offline: " + ex.Message };
                }
            }
        }

        public static void RequireCalculationAccess()
        {
            var status = GetStatus();
            if (!status.CanCalculate)
                throw new InvalidOperationException(status.Description +
                    Environment.NewLine + "Dữ liệu bản vẽ DWG không bị xóa hoặc khóa.");
        }

        public static string Activate(string pass)
        {
            if (string.IsNullOrWhiteSpace(pass))
                throw new ArgumentException("Hãy nhập Pass kích hoạt.");
            int tier = IdentifyPass(pass.Trim());
            if (tier < 0) throw new InvalidOperationException("Pass không hợp lệ.");
            lock (Sync)
            {
                DateTime now = DateTime.UtcNow;
                var ledger = LoadActivation() ?? new ActivationLedger
                {
                    History = new List<UsedPass>(), LastSeenUtc = now
                };
                if (IsClockRollback(now, ledger.LastSeenUtc))
                    throw new InvalidOperationException("Ngày giờ hệ thống không hợp lệ.");
                if (ledger.History == null) ledger.History = new List<UsedPass>();
                string tierName = TierNames[tier];
                var used = ledger.History.FirstOrDefault(x => x.Tier == tierName);
                if (used == null)
                {
                    used = new UsedPass { Tier = tierName, FirstActivationUtc = now };
                    ledger.History.Add(used);
                }
                DateTime expiry = TierMonths[tier] == 0 ?
                    DateTime.MaxValue : used.FirstActivationUtc.AddMonths(TierMonths[tier]);
                if (TierMonths[tier] != 0 && now >= expiry)
                    throw new InvalidOperationException(
                        "Pass " + tierName + " đã sử dụng và hết hạn. Nhập lại không gia hạn thời hạn.");
                ledger.ActiveTier = tierName;
                ledger.LastSeenUtc = Max(now, ledger.LastSeenUtc);
                Persist(ledger, ActivationFile, RegistryActivationValue);
                return "Đã kích hoạt offline loại " + tierName +
                    (TierMonths[tier] == 0 ? " (không hết hạn)." :
                        ". Hết hạn: " + expiry.ToLocalTime().ToString("dd/MM/yyyy HH:mm") + ".");
            }
        }

        private static int IdentifyPass(string value)
        {
            byte[] hash;
            using (var sha = SHA256.Create())
                hash = sha.ComputeHash(Encoding.UTF8.GetBytes(value));
            string actual = BitConverter.ToString(hash).Replace("-", "");
            // Compare all five options without an early matching exit.
            int match = -1;
            for (int i = 0; i < PassHashes.Length; i++)
            {
                int different = 0;
                for (int j = 0; j < actual.Length; j++)
                    different |= actual[j] ^ PassHashes[i][j];
                if (different == 0) match = i;
            }
            return match;
        }

        private static bool IsClockRollback(DateTime now, DateTime last) =>
            last != default(DateTime) && now < last.AddHours(-1);
        private static DateTime Max(DateTime a, DateTime b) => a >= b ? a : b;

        private static TrialLedger LoadTrial() => LoadProtected<TrialLedger>(TrialFile, RegistryTrialValue);
        private static ActivationLedger LoadActivation() =>
            LoadProtected<ActivationLedger>(ActivationFile, RegistryActivationValue);

        private static T LoadProtected<T>(string file, string registryValue) where T : class
        {
            byte[] fromRegistry = ReadRegistry(RegistryPath, registryValue);
            byte[] fromFile = File.Exists(file) ? File.ReadAllBytes(file) : null;
            if (fromRegistry == null && fromFile == null) return null;
            byte[] input = fromRegistry ?? fromFile;
            // Each store uses per-user DPAPI, never a license MachineId.
            var value = Decode<T>(input, Entropy);
            if (fromRegistry != null && fromFile != null)
            {
                var other = Decode<T>(fromFile, Entropy);
                if (other == null) throw new InvalidDataException("Thông tin bản quyền lưu trên đĩa bị lỗi.");
            }
            return value;
        }

        private static TrialLedger ReadLegacyTrial()
        {
            // Retain the earlier trial first-use date where possible.
            // A corrupt legacy record must not be silently trusted.
            byte[] raw = ReadRegistry(LegacyRegistryPath, RegistryTrialValue);
            if (raw == null) return null;
            var legacy = Decode<LegacyTrialLedger>(raw, LegacyEntropy);
            if (legacy == null) return null;
            return new TrialLedger { FirstUseUtc = legacy.FirstUseUtc, LastUseUtc = legacy.LastUseUtc };
        }

        private static T Decode<T>(byte[] encrypted, byte[] entropy) where T : class
        {
            byte[] json = ProtectedData.Unprotect(encrypted, entropy, DataProtectionScope.CurrentUser);
            using (var stream = new MemoryStream(json))
                return new DataContractJsonSerializer(typeof(T)).ReadObject(stream) as T;
        }

        private static void Persist<T>(T record, string file, string registryValue)
        {
            byte[] json;
            using (var stream = new MemoryStream())
            {
                new DataContractJsonSerializer(typeof(T)).WriteObject(stream, record);
                json = stream.ToArray();
            }
            byte[] protectedBytes = ProtectedData.Protect(json, Entropy, DataProtectionScope.CurrentUser);
            Directory.CreateDirectory(RootDirectory);
            string pending = file + ".tmp";
            File.WriteAllBytes(pending, protectedBytes);
            if (File.Exists(file)) File.Replace(pending, file, null);
            else File.Move(pending, file);
            using (var key = Registry.CurrentUser.CreateSubKey(RegistryPath))
                key.SetValue(registryValue, protectedBytes, RegistryValueKind.Binary);
        }

        private static byte[] ReadRegistry(string keyPath, string valueName)
        {
            using (var key = Registry.CurrentUser.OpenSubKey(keyPath, false))
                return key?.GetValue(valueName) as byte[];
        }
    }
}