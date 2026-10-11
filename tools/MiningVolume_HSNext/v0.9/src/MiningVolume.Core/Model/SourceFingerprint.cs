using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace MiningVolume.Core.Model
{
    public static class SourceFingerprint
    {
        // Original sampled geometry, before exclusions/Z overrides. A changed sampling
        // algorithm deliberately changes this hash rather than reusing stale vertex indices.
        public static string Compute(SourceEntity entity)
        {
            if(entity==null)throw new ArgumentNullException(nameof(entity));
            using(var hash=SHA256.Create())
            using(var stream=new CryptoStream(Stream.Null,hash,CryptoStreamMode.Write))
            using(var writer=new BinaryWriter(stream,Encoding.UTF8,true)) {
                writer.Write("IMSAT-SOURCE-1");writer.Write(entity.Handle.ToUpperInvariant());
                writer.Write(entity.Layer);writer.Write((int)entity.Type);writer.Write(entity.Closed);
                writer.Write(entity.Vertices.Count);
                foreach(var v in entity.Vertices){writer.Write(v.Original.X);writer.Write(v.Original.Y);writer.Write(v.Original.Z);}
                writer.Flush();stream.FlushFinalBlock();
                return BitConverter.ToString(hash.Hash).Replace("-","").ToLowerInvariant();
            }
        }
    }
}
