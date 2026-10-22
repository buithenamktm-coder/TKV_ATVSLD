using System;
using System.Globalization;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace ImsatLicenseManager
{
    [DataContract]
    internal sealed class SignedLicensePayload
    {
        [DataMember(Order = 1)] public string LicenseId { get; set; }
        [DataMember(Order = 2)] public string Customer { get; set; }
        [DataMember(Order = 3)] public string MachineId { get; set; }
        [DataMember(Order = 4)] public string Mode { get; set; }
        [DataMember(Order = 5)] public DateTime NotBeforeUtc { get; set; }
        [DataMember(Order = 6)] public DateTime ExpiresUtc { get; set; }
    }

    /// <summary>
    /// OFFLINE ADMIN ISSUER. Run only on the designated licensing PC.
    /// It never embeds a universal backdoor/Super Key in a distributed product.
    /// Private signing material is DPAPI-protected under the admin Windows user.
    /// </summary>
    internal static class Program
    {
        private static readonly byte[] Entropy =
            Encoding.UTF8.GetBytes("IMSAT.MiningVolume.LicenseIssuer.Private.V1");
        private static readonly string Workdir =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "IMSAT", "MiningVolume", "LicenseManager");
        private static string PrivatePath => Path.Combine(Workdir,"issuer.private.dpapi");
        private static string PublicPath => Path.Combine(Workdir,"issuer.public.xml");
        private static string AuditPath => Path.Combine(Workdir,"audit.tsv");

        private static int Main(string[] args)
        {
            try
            {
                if (args.Length == 0) return Usage();
                switch (args[0].ToLowerInvariant())
                {
                    case "selftest":
                        SelfTest();
                        break;
                    case "backup":
                        if (args.Length != 2) return Usage();
                        BackupIssuer(args[1]);
                        break;
                    case "restore":
                        if (args.Length != 2) return Usage();
                        RestoreIssuer(args[1]);
                        break;
                    case "init":
                        if (args.Length != 1) return Usage();
                        Init();
                        break;
                    case "machine":
                        if (args.Length != 1) return Usage();
                        Console.WriteLine("Mã máy của đồng nghiệp được lấy từ IMSAT VOLUME → Bản quyền.");
                        break;
                    case "public":
                        if (args.Length != 1) return Usage();
                        Console.WriteLine(File.ReadAllText(PublicPath,Encoding.UTF8));
                        break;
                    case "fingerprint":
                        if (args.Length != 1) return Usage();
                        PrintPublicFingerprint();
                        break;
                    case "issue":
                        if (args.Length != 5) return Usage();
                        Issue(args[1],args[2],args[3],args[4]);
                        break;
                    case "provision":
                        if (args.Length != 2) return Usage();
                        ProvisionProductSource(args[1]);
                        break;
                    case "verify":
                        if (args.Length != 2) return Usage();
                        VerifyFile(args[1]);
                        break;
                    default: return Usage();
                }
                return 0;
            }
            catch (Exception e)
            {
                Console.Error.WriteLine("ERROR: "+e.Message);
                return 1;
            }
        }

        private static int Usage()
        {
            Console.WriteLine("IMSAT MiningVolume License Manager (offline admin only)");
            Console.WriteLine("  init");
            Console.WriteLine("  backup <absolute path to encrypted .imsbackup>");
            Console.WriteLine("  restore <absolute path to encrypted .imsbackup>");
            Console.WriteLine("  selftest");
            Console.WriteLine("  issue <MachineId64hex> <INTERNAL|UNIT> <Customer> <1M|3M|6M|1Y|YYYY-MM-DD|PERMANENT>");
            Console.WriteLine("  public");
            Console.WriteLine("  fingerprint  (SHA-256 of RSA public-only XML; no private key)");
            Console.WriteLine("  provision <full path to plugin LicenseService.cs>");
            Console.WriteLine("  verify <full path to .mvlic>");
            Console.WriteLine("All issuing keys stay in the admin Windows profile, never in the CAD plugin.");
            return 2;
        }

        private static void SelfTest()
        {
            var payload = new SignedLicensePayload
            {
                LicenseId = "MV2-SELFTEST", Customer = "IMSAT TEST", Mode = "UNIT",
                MachineId = new string('A', 64),
                NotBeforeUtc = DateTime.UtcNow.AddMinutes(-5),
                ExpiresUtc = DateTime.UtcNow.AddDays(30)
            };
            byte[] data;
            using (var ms = new MemoryStream())
            {
                new DataContractJsonSerializer(typeof(SignedLicensePayload)).WriteObject(ms, payload);
                data = ms.ToArray();
            }
            using (var issuer = new RSACryptoServiceProvider(3072))
            using (var verifier = new RSACryptoServiceProvider())
            {
                issuer.PersistKeyInCsp = false;
                verifier.PersistKeyInCsp = false;
                verifier.FromXmlString(issuer.ToXmlString(false));
                byte[] sig = issuer.SignData(data, CryptoConfig.MapNameToOID("SHA256"));
                if (!verifier.VerifyData(data, CryptoConfig.MapNameToOID("SHA256"), sig))
                    throw new InvalidOperationException("License RSA-SHA256 round trip FAIL");
                data[0] ^= 0x01;
                if (verifier.VerifyData(data, CryptoConfig.MapNameToOID("SHA256"), sig))
                    throw new InvalidOperationException("Tampered payload accepted");
            }
            IssuerBackup.SelfTest();
            Console.WriteLine("LICENSE MANAGER SELFTEST PASS");
        }

        private static void PrintPublicFingerprint()
        {
            if (!File.Exists(PublicPath))
                throw new FileNotFoundException("Chưa init issuer.public.xml trên máy quản trị.");
            using (var rsa = new RSACryptoServiceProvider())
            {
                rsa.PersistKeyInCsp = false;
                rsa.FromXmlString(File.ReadAllText(PublicPath, Encoding.UTF8).Trim());
                if (!rsa.PublicOnly || rsa.KeySize < 3072)
                    throw new InvalidOperationException("RSA public-only 3072-bit required.");
                byte[] canonical = Encoding.UTF8.GetBytes(rsa.ToXmlString(false));
                using (var sha = SHA256.Create())
                    Console.WriteLine("IMSAT_VOLUME_V2_RSA_PUBLIC_SHA256=" +
                        BitConverter.ToString(sha.ComputeHash(canonical)).Replace("-", "").ToLowerInvariant());
            }
        }

        private static void BackupIssuer(string destination)
        {
            destination=Path.GetFullPath(destination);
            if(File.Exists(destination))
                throw new IOException("Đã tồn tại tệp backup; không ghi đè.");
            if(!File.Exists(PrivatePath))
                throw new InvalidOperationException("Máy hiện tại không có RSA private key để sao lưu.");
            string pass=IssuerBackup.ReadPasswordFromConsole("Nhập mật khẩu backup (>=16 ký tự): ");
            string confirmation=IssuerBackup.ReadPasswordFromConsole("Nhập lại mật khẩu backup: ");
            if(!string.Equals(pass,confirmation,StringComparison.Ordinal))
                throw new InvalidOperationException("Hai mật khẩu backup không khớp.");
            byte[] raw;
            using(var rsa=LoadIssuer())
                raw=Encoding.UTF8.GetBytes(rsa.ToXmlString(true));
            try
            {
                byte[] cipher=IssuerBackup.Encrypt(raw,pass);
                string dir=Path.GetDirectoryName(destination);
                if(!Directory.Exists(dir))
                    throw new DirectoryNotFoundException("Thư mục chứa backup không tồn tại.");
                File.WriteAllBytes(destination,cipher);
                Console.WriteLine("Đã sao lưu key có mật khẩu (AES + HMAC) tới: "+destination);
                Console.WriteLine("LƯU TRỮ NGOẠI TUYẾN: không lưu mật khẩu cùng file backup, không gửi file này cho khách hàng.");
            }
            finally { Array.Clear(raw,0,raw.Length); }
        }

        private static void RestoreIssuer(string source)
        {
            if(File.Exists(PrivatePath))
                throw new InvalidOperationException("Máy quản trị đã có private issuer key. Không ghi đè.");
            source=Path.GetFullPath(source);
            string pass=IssuerBackup.ReadPasswordFromConsole("Nhập mật khẩu khôi phục: ");
            var encrypted=File.ReadAllBytes(source);
            var raw=IssuerBackup.Decrypt(encrypted,pass);
            string recoveredPublic;
            try
            {
                using(var rsa=new RSACryptoServiceProvider())
                {
                    rsa.PersistKeyInCsp=false;
                    rsa.FromXmlString(Encoding.UTF8.GetString(raw));
                    recoveredPublic=rsa.ToXmlString(false);
                }
                if(File.Exists(PublicPath) &&
                   !string.Equals(File.ReadAllText(PublicPath,Encoding.UTF8).Trim(),
                                  recoveredPublic,StringComparison.Ordinal))
                    throw new InvalidOperationException(
                        "Public key hiện tại khác backup; không thể ghi đè giấy phép đã cấp.");
                Directory.CreateDirectory(Workdir);
                var dpapi=ProtectedData.Protect(raw,Entropy,DataProtectionScope.CurrentUser);
                string temporary=PrivatePath+".restore.new";
                if(File.Exists(temporary))
                    throw new IOException("Còn file khôi phục dang dở; cần kiểm tra thủ công.");
                File.WriteAllBytes(temporary,dpapi);
                try
                {
                    if(!File.Exists(PublicPath))
                    {
                        string tempPublic=PublicPath+".restore.new";
                        File.WriteAllText(tempPublic,recoveredPublic,Encoding.UTF8);
                        try
                        {
                            File.Move(temporary,PrivatePath);
                            File.Move(tempPublic,PublicPath);
                        }
                        catch
                        {
                            if(File.Exists(PrivatePath))File.Delete(PrivatePath);
                            throw;
                        }
                        finally {if(File.Exists(tempPublic))File.Delete(tempPublic);}
                    }
                    else File.Move(temporary,PrivatePath);
                }
                finally {if(File.Exists(temporary))File.Delete(temporary);}
                Console.WriteLine("Đã khôi phục issuer key DPAPI trên tài khoản Windows hiện tại.");
                Console.WriteLine("Xác nhận public key/activation trước khi cấp giấy phép mới.");
            }
            finally { Array.Clear(raw,0,raw.Length); }
        }

        private static void Init()
        {
            Directory.CreateDirectory(Workdir);
            if (File.Exists(PrivatePath)||File.Exists(PublicPath))
                throw new InvalidOperationException("Khóa nhà phát hành đã tồn tại. Không tạo lại để tránh vô hiệu hóa license cũ.");

            using (var rsa=new RSACryptoServiceProvider(3072))
            {
                rsa.PersistKeyInCsp=false;
                byte[] raw=Encoding.UTF8.GetBytes(rsa.ToXmlString(true));
                byte[] encrypted=ProtectedData.Protect(raw,Entropy,DataProtectionScope.CurrentUser);
                string temp=PrivatePath+".new";
                File.WriteAllBytes(temp,encrypted);
                File.Move(temp,PrivatePath);
                File.WriteAllText(PublicPath,rsa.ToXmlString(false),Encoding.UTF8);
            }
            Console.WriteLine("Đã tạo RSA 3072-bit. PUBLIC: "+PublicPath);
            Console.WriteLine("PRIVATE: chỉ đọc được dưới tài khoản Windows quản trị hiện tại.");
            Console.WriteLine("PHẢI thiết lập phương án sao lưu an toàn trước khi phát hành cho đơn vị.");
        }

        private static RSACryptoServiceProvider LoadIssuer()
        {
            if (!File.Exists(PrivatePath))
                throw new InvalidOperationException("Không thấy private key. Chỉ máy quản trị mới được cấp license.");
            byte[] raw=ProtectedData.Unprotect(File.ReadAllBytes(PrivatePath),Entropy,
                DataProtectionScope.CurrentUser);
            var rsa=new RSACryptoServiceProvider();
            rsa.PersistKeyInCsp=false;
            rsa.FromXmlString(Encoding.UTF8.GetString(raw));
            Array.Clear(raw,0,raw.Length);
            return rsa;
        }

        private static void Issue(string machineId,string mode,string customer,string expiryText)
        {
            machineId=(machineId??"").Trim().ToUpperInvariant();
            mode=(mode??"").Trim().ToUpperInvariant();
            customer=(customer??"").Trim();
            if (!Regex.IsMatch(machineId,"^[0-9A-F]{64}$"))
                throw new ArgumentException("MachineId phải gồm 64 ký tự HEX từ trang Bản quyền.");
            if (mode!="INTERNAL" && mode!="UNIT")
                throw new ArgumentException("Giấy phép người dùng chỉ có INTERNAL hoặc UNIT.");
            if (customer.Length<3 || customer.Length>160)
                throw new ArgumentException("Tên đơn vị phải dài 3–160 ký tự.");
            var now=DateTime.UtcNow;
            DateTime expires;
            switch ((expiryText ?? "").Trim().ToUpperInvariant())
            {
                case "1M": expires=now.AddMonths(1); break;
                case "3M": expires=now.AddMonths(3); break;
                case "6M": expires=now.AddMonths(6); break;
                case "1Y": expires=now.AddYears(1); break;
                case "PERMANENT":
                    expires=new DateTime(9999,12,31,0,0,0,DateTimeKind.Utc);
                    break;
                default:
                    expires=DateTime.SpecifyKind(DateTime.ParseExact(expiryText,"yyyy-MM-dd",
                        CultureInfo.InvariantCulture,DateTimeStyles.None).AddDays(1),DateTimeKind.Utc);
                    break;
            }
            if(expires<=now)throw new ArgumentException("Ngày hết hạn phải ở tương lai.");

            var payload=new SignedLicensePayload
            {
                LicenseId="MV2-"+now.ToString("yyyyMMdd")+"-"+Guid.NewGuid().ToString("N").Substring(0,12).ToUpperInvariant(),
                Customer=customer,MachineId=machineId,Mode=mode,
                NotBeforeUtc=now.AddMinutes(-5),ExpiresUtc=expires
            };
            byte[] data;
            using(var ms=new MemoryStream())
            {
                new DataContractJsonSerializer(typeof(SignedLicensePayload)).WriteObject(ms,payload);
                data=ms.ToArray();
            }
            byte[] signature;
            using(var rsa=LoadIssuer())
                signature=rsa.SignData(data,CryptoConfig.MapNameToOID("SHA256"));

            string token="MV2."+Convert.ToBase64String(data)+"."+Convert.ToBase64String(signature);
            string path=Path.Combine(Workdir,payload.LicenseId+".mvlic");
            if(File.Exists(path))throw new IOException("Đã tồn tại license trùng mã.");
            File.WriteAllText(path,token,Encoding.ASCII);

            // Audit does not store the private key; token hash is for traceability.
            using(var sha=SHA256.Create())
            {
                string digest=BitConverter.ToString(sha.ComputeHash(Encoding.ASCII.GetBytes(token))).Replace("-","");
                File.AppendAllText(AuditPath,
                    now.ToString("O")+"\t"+payload.LicenseId+"\t"+mode+"\t"+
                    machineId+"\t"+expires.ToString("O")+"\t"+customer.Replace("\t"," ")+"\t"+digest+
                    Environment.NewLine,Encoding.UTF8);
            }
            Console.WriteLine("Đã cấp key máy đơn vị: "+payload.LicenseId);
            Console.WriteLine("File bàn giao: "+path);
        }

        private static void VerifyFile(string file)
        {
            var token=File.ReadAllText(file,Encoding.UTF8).Trim();
            var parts=token.Split('.');
            if(parts.Length!=3 || parts[0]!="MV2")
                throw new InvalidOperationException("Key không đúng định dạng.");
            var bytes=Convert.FromBase64String(parts[1]);
            var sig=Convert.FromBase64String(parts[2]);
            using(var rsa=new RSACryptoServiceProvider())
            {
                rsa.FromXmlString(File.ReadAllText(PublicPath,Encoding.UTF8));
                if(!rsa.VerifyData(bytes,CryptoConfig.MapNameToOID("SHA256"),sig))
                    throw new InvalidOperationException("Chữ ký key không hợp lệ.");
            }
            using(var ms=new MemoryStream(bytes))
            {
                var p=(SignedLicensePayload)new DataContractJsonSerializer(typeof(SignedLicensePayload)).ReadObject(ms);
                Console.WriteLine("VALID SIGNATURE: "+p.LicenseId+" | "+p.Customer+" | "+p.MachineId+" | "+p.Mode);
            }
        }

        private static void ProvisionProductSource(string filename)
        {
            if(!File.Exists(PrivatePath) || !File.Exists(PublicPath))
                throw new InvalidOperationException("Phải chạy init trên máy quản trị trước.");
            if(!File.Exists(filename))throw new FileNotFoundException("Không tìm thấy source LicenseService.cs.",filename);
            string source=File.ReadAllText(filename,Encoding.UTF8);
            const string placeholder="internal const string RsaPublicKeyXml = \"\";";
            if(!source.Contains(placeholder))
                throw new InvalidOperationException("Chỉ provision source CHƯA có public key; không ghi đè khóa đã phát hành.");
            string xml=File.ReadAllText(PublicPath,Encoding.UTF8).Trim();
            if(!xml.Contains("<RSAKeyValue>")||!xml.Contains("<Modulus>")||!xml.Contains("<Exponent>"))
                throw new InvalidOperationException("Public key không hợp lệ.");
            source=source.Replace(placeholder,"internal const string RsaPublicKeyXml = @\""+
                xml.Replace("\"","\"\"")+"\";");
            File.WriteAllText(filename,source,Encoding.UTF8);
            Console.WriteLine("Đã gắn CHỈ PUBLIC key vào plugin source; private key vẫn ở máy quản trị.");
            Console.WriteLine("Cần commit và build mã nguồn đã provision, kiểm thử end-to-end trước release.");
        }
    }
}
