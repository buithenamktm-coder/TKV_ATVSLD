using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ImsatLicenseManager
{
    /// <summary>
    /// Offline encrypted backup envelope for the admin RSA private issuer key.
    /// AES-256-CBC + HMAC-SHA256 encrypt-then-MAC with independently derived
    /// keys from 250,000 PBKDF2-HMAC-SHA256 iterations and 128-bit random salt.
    /// Restore verifies MAC before attempting any decryption.
    /// File format: UTF8 magic, salt16, iv16, ciphertext length (int32 LE),
    /// ciphertext bytes, mac32 over all bytes preceding the tag.
    /// </summary>
    internal static class IssuerBackup
    {
        private static readonly byte[] Magic=Encoding.ASCII.GetBytes("IMSBKP01");
        private const int SaltSize=16;
        private const int IvSize=16;
        private const int MacSize=32;
        private const int MaxPayload=65536;
        private const int Iterations=250000;

        internal static byte[] Encrypt(byte[] privateXml, string passphrase)
        {
            if(privateXml==null||privateXml.Length==0||privateXml.Length>MaxPayload)
                throw new ArgumentException("Khóa RSA nguồn không hợp lệ.");
            CheckPassphrase(passphrase);
            var salt=new byte[SaltSize];var iv=new byte[IvSize];
            using(var rng=RandomNumberGenerator.Create())
            {
                rng.GetBytes(salt);rng.GetBytes(iv);
            }
            byte[] keys=Derive(passphrase,salt);
            try
            {
                byte[] cipher;
                using(var aes=Aes.Create())
                {
                    aes.KeySize=256;aes.BlockSize=128;aes.Mode=CipherMode.CBC;
                    aes.Padding=PaddingMode.PKCS7;
                    aes.Key=Slice(keys,0,32);aes.IV=iv;
                    using(var tx=aes.CreateEncryptor())
                        cipher=tx.TransformFinalBlock(privateXml,0,privateXml.Length);
                }
                byte[] envelope;
                using(var memory=new MemoryStream())
                using(var writer=new BinaryWriter(memory,Encoding.UTF8,true))
                {
                    writer.Write(Magic);writer.Write(salt);writer.Write(iv);
                    writer.Write(cipher.Length);writer.Write(cipher);
                    writer.Flush();envelope=memory.ToArray();
                }
                using(var hmac=new HMACSHA256(Slice(keys,32,32)))
                {
                    byte[] signature=hmac.ComputeHash(envelope);
                    using(var final=new MemoryStream())
                    {
                        final.Write(envelope,0,envelope.Length);
                        final.Write(signature,0,signature.Length);
                        return final.ToArray();
                    }
                }
            }
            finally {Array.Clear(keys,0,keys.Length);}
        }

        internal static byte[] Decrypt(byte[] protectedBackup,string passphrase)
        {
            if(protectedBackup==null ||
                protectedBackup.Length<Magic.Length+SaltSize+IvSize+4+16+MacSize ||
                protectedBackup.Length>MaxPayload+512)
                throw new InvalidDataException("File backup IMSAT không hợp lệ hoặc quá lớn.");
            CheckPassphrase(passphrase);
            byte[] salt,iv,cipher;
            using(var memory=new MemoryStream(protectedBackup,false))
            using(var reader=new BinaryReader(memory,Encoding.UTF8,true))
            {
                var magic=reader.ReadBytes(Magic.Length);
                if(!EqualConstantTime(magic,Magic))
                    throw new InvalidDataException("Không đúng định dạng backup IMSAT.");
                salt=reader.ReadBytes(SaltSize);iv=reader.ReadBytes(IvSize);
                int length=reader.ReadInt32();
                if(length<16 || length>MaxPayload+16 || length%16!=0 ||
                   memory.Length-memory.Position!=length+MacSize)
                    throw new InvalidDataException("Độ dài backup sai.");
                cipher=reader.ReadBytes(length);
            }
            var keys=Derive(passphrase,salt);
            try
            {
                int signedLength=protectedBackup.Length-MacSize;
                using(var hmac=new HMACSHA256(Slice(keys,32,32)))
                {
                    byte[] actual=hmac.ComputeHash(protectedBackup,0,signedLength);
                    byte[] expected=Slice(protectedBackup,signedLength,MacSize);
                    if(!EqualConstantTime(actual,expected))
                        throw new CryptographicException("Mật khẩu sai hoặc file backup đã bị chỉnh sửa.");
                }
                using(var aes=Aes.Create())
                {
                    aes.KeySize=256;aes.BlockSize=128;aes.Mode=CipherMode.CBC;
                    aes.Padding=PaddingMode.PKCS7;aes.Key=Slice(keys,0,32);aes.IV=iv;
                    using(var tx=aes.CreateDecryptor())
                        return tx.TransformFinalBlock(cipher,0,cipher.Length);
                }
            }
            finally {Array.Clear(keys,0,keys.Length);}
        }

        internal static void SelfTest()
        {
            const string pass="Internal-Admin-Backup-RoundTrip-StrongPassphrase!";
            byte[] key=Encoding.UTF8.GetBytes("<RSAKeyValue>SECRETMATERIAL</RSAKeyValue>");
            byte[] envelope=Encrypt(key,pass);
            var recovered=Decrypt(envelope,pass);
            if(!EqualConstantTime(key,recovered))
                throw new InvalidOperationException("Private-key backup roundtrip FAIL");
            envelope[envelope.Length-17]^=0x01;
            bool tamperBlocked=false;
            try{Decrypt(envelope,pass);}
            catch(CryptographicException){tamperBlocked=true;}
            if(!tamperBlocked)
                throw new InvalidOperationException("Tampered issuer backup was accepted!");
            Console.WriteLine("ISSUER BACKUP SELFTEST PASS");
        }

        internal static string ReadPasswordFromConsole(string prompt)
        {
            Console.Write(prompt);
            var text=new StringBuilder();
            while(true)
            {
                var key=Console.ReadKey(intercept:true);
                if(key.Key==ConsoleKey.Enter)break;
                if(key.Key==ConsoleKey.Backspace)
                {
                    if(text.Length>0)text.Length--;
                    continue;
                }
                if(!char.IsControl(key.KeyChar)&&text.Length<512)text.Append(key.KeyChar);
            }
            Console.WriteLine();
            return text.ToString();
        }

        private static byte[] Derive(string passphrase,byte[] salt)
        {
            using(var pbkdf=new Rfc2898DeriveBytes(passphrase,salt,Iterations,HashAlgorithmName.SHA256))
                return pbkdf.GetBytes(64);
        }

        private static void CheckPassphrase(string passphrase)
        {
            if(string.IsNullOrWhiteSpace(passphrase)||passphrase.Length<16)
                throw new ArgumentException("Mật khẩu backup phải dài ít nhất 16 ký tự.");
        }

        private static byte[] Slice(byte[] input,int offset,int length)
        {
            var output=new byte[length];
            Buffer.BlockCopy(input,offset,output,0,length);
            return output;
        }

        private static bool EqualConstantTime(byte[] a,byte[] b)
        {
            if(a==null||b==null)return false;
            int unequal=a.Length^b.Length;
            int n=Math.Min(a.Length,b.Length);
            for(int i=0;i<n;i++)unequal|=a[i]^b[i];
            return unequal==0;
        }
    }
}
