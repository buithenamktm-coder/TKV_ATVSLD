using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace MiningVolume.Core.Model
{
    public static class SourceFingerprint
    {
        // A stable SHA-256 of every selected original CAD source entity, in
        // handle order. This fingerprints entire HT/TK model context and detects
        // changes to entities that were not individually edited.
        // Only Original coordinates are read: model-side Z overrides/exclusions
        // must not make an otherwise identical DWG fail to restore.
        public static string ComputeModel(SurfaceModel model)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            using (var hash = SHA256.Create())
            using (var stream = new CryptoStream(Stream.Null, hash, CryptoStreamMode.Write))
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
            {
                writer.Write("IMSAT-SOURCE-MODEL-1");
                writer.Write(model.Entities.Count);
                foreach (var entity in model.Entities.OrderBy(x => x.Handle, StringComparer.OrdinalIgnoreCase))
                    writer.Write(Compute(entity));
                writer.Flush();
                stream.FlushFinalBlock();
                return BitConverter.ToString(hash.Hash).Replace("-", "").ToLowerInvariant();
            }
        }

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
