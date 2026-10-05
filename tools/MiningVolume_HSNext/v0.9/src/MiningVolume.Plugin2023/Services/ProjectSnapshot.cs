using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using MiningVolume.Core.Model;

namespace MiningVolume2023.Services
{
    [DataContract]
    public sealed class ProjectSnapshot
    {
        [DataMember(Order = 1)] public int FormatVersion { get; set; } = 1;
        [DataMember(Order = 2)] public string ProductVersion { get; set; } = "0.10.4";
        [DataMember(Order = 3)] public DateTime SavedUtc { get; set; }
        [DataMember(Order = 4)] public ModelSnapshot Existing { get; set; }
        [DataMember(Order = 5)] public ModelSnapshot Design { get; set; }
        [DataMember(Order = 6)] public string BoundaryHandle { get; set; }
        [DataMember(Order = 7)] public bool HasDirection { get; set; }
        [DataMember(Order = 8)] public double DirectionX { get; set; }
        [DataMember(Order = 9)] public double DirectionY { get; set; }
        [DataMember(Order = 10)] public double SectionSpacing { get; set; }
        [DataMember(Order = 11)] public double LevelStep { get; set; }
        [DataMember(Order = 12)] public double FromLevel { get; set; }
        [DataMember(Order = 13)] public double ToLevel { get; set; }
        [DataMember(Order = 14)] public double HorizontalScale { get; set; }
        [DataMember(Order = 15)] public double VerticalScale { get; set; }
        [DataMember(Order = 16)] public string DeveloperName { get; set; }
        [DataMember(Order = 17)] public string DeveloperContact { get; set; }
        [DataMember(Order = 18)] public List<Point2Snapshot> Boundary { get; set; } = new List<Point2Snapshot>();
        [DataMember(Order = 19)] public List<SectionLineSnapshot> SectionLines { get; set; } = new List<SectionLineSnapshot>();
        [DataMember(Order = 20)] public bool HadProfiles { get; set; }
        [DataMember(Order = 21)] public bool HadVolumeResult { get; set; }
    }

    [DataContract]
    public sealed class ModelSnapshot
    {
        [DataMember(Order = 1)] public string Layer { get; set; }
        [DataMember(Order = 2)] public bool TinVisible { get; set; } = true;
        [DataMember(Order = 3)] public List<int> AllowedTypes { get; set; } = new List<int>();
        [DataMember(Order = 4)] public List<EntityEditSnapshot> Edits { get; set; } = new List<EntityEditSnapshot>();
        [DataMember(Order = 5)] public bool HadTin { get; set; }
    }

    [DataContract]
    public sealed class EntityEditSnapshot
    {
        [DataMember(Order = 1)] public string Handle { get; set; }
        [DataMember(Order = 2)] public bool Enabled { get; set; } = true;
        [DataMember(Order = 3)] public List<VertexEditSnapshot> Vertices { get; set; } = new List<VertexEditSnapshot>();
    }

    [DataContract]
    public sealed class VertexEditSnapshot
    {
        [DataMember(Order = 1)] public int Index { get; set; }
        [DataMember(Order = 2)] public bool Enabled { get; set; } = true;
        [DataMember(Order = 3)] public bool HasZOverride { get; set; }
        [DataMember(Order = 4)] public double Z { get; set; }
    }

    [DataContract]
    public sealed class Point2Snapshot
    {
        [DataMember(Order = 1)] public double X { get; set; }
        [DataMember(Order = 2)] public double Y { get; set; }
    }

    [DataContract]
    public sealed class SectionLineSnapshot
    {
        [DataMember(Order = 1)] public int Index { get; set; }
        [DataMember(Order = 2)] public string Name { get; set; }
        [DataMember(Order = 3)] public double Offset { get; set; }
        [DataMember(Order = 4)] public double DirectionX { get; set; }
        [DataMember(Order = 5)] public double DirectionY { get; set; }
        [DataMember(Order = 6)] public double NormalX { get; set; }
        [DataMember(Order = 7)] public double NormalY { get; set; }
        [DataMember(Order = 8)] public List<SectionSpanSnapshot> Spans { get; set; } = new List<SectionSpanSnapshot>();
    }

    [DataContract]
    public sealed class SectionSpanSnapshot
    {
        [DataMember(Order = 1)] public double StartT { get; set; }
        [DataMember(Order = 2)] public double EndT { get; set; }
    }

    public static class ProjectSnapshotCodec
    {
        public static string Encode(ProjectSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            var serializer = new DataContractJsonSerializer(typeof(ProjectSnapshot));
            using (var json = new MemoryStream())
            {
                serializer.WriteObject(json, snapshot);
                json.Position = 0;
                using (var compressed = new MemoryStream())
                {
                    using (var gzip = new GZipStream(compressed, CompressionLevel.Optimal, true))
                        json.CopyTo(gzip);
                    return Convert.ToBase64String(compressed.ToArray());
                }
            }
        }

        public static ProjectSnapshot Decode(string encoded)
        {
            if (string.IsNullOrWhiteSpace(encoded)) return null;
            var bytes = Convert.FromBase64String(encoded);
            using (var compressed = new MemoryStream(bytes))
            using (var gzip = new GZipStream(compressed, CompressionMode.Decompress))
            using (var json = new MemoryStream())
            {
                gzip.CopyTo(json);
                json.Position = 0;
                var serializer = new DataContractJsonSerializer(typeof(ProjectSnapshot));
                return (ProjectSnapshot)serializer.ReadObject(json);
            }
        }
    }
}