using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using MiningVolume.Core.Geometry;
using MiningVolume.Core.Model;

namespace MiningVolume.Core.Surface
{
    /// <summary>
    /// CÁCH DỰNG THỬ NGHIỆM, KHÔNG TÍNH KHỐI LƯỢNG:
    /// take every enabled vertex, merge coincident XY by arithmetic mean of ALL
    /// original Z samples. Constraints/breaklines are intentionally NOT retained:
    /// inconsistent cliffs cannot be represented by a single-valued 2.5D TIN.
    /// Never changes the model or the official surface build policy.
    /// </summary>
    public static class MeanZPreviewPreparer
    {
        /// <summary>Only applies to opt-in experimental TIN; never relax official TIN.</summary>
        public const double ReviewThresholdMetres = 3.0;

        /// <summary>The original editable vertex identity in ProjectState.Source.</summary>
        public sealed class Member
        {
            public string EntityId { get; internal set; }
            public int VertexIndex { get; internal set; }
            public string Handle { get; internal set; }
            public string Layer { get; internal set; }
            public SourceEntityType Type { get; internal set; }
            public double Z { get; internal set; }
        }
        private readonly struct Cell : IEquatable<Cell>
        {
            public readonly long X, Y;
            public Cell(long x,long y) { X=x; Y=y; }
            public bool Equals(Cell other)=>X==other.X&&Y==other.Y;
            public override bool Equals(object obj)=>obj is Cell&&Equals((Cell)obj);
            public override int GetHashCode(){unchecked{return X.GetHashCode()*397 ^ Y.GetHashCode();}}
        }
        private sealed class Group
        {
            internal Vec2 XY;
            internal int N;
            internal double AverageZ, MinZ, MaxZ;
            internal string FirstHandle, LastHandle;
            internal List<Member> Members = new List<Member>();
            internal void Add(double z,string handle,SourceEntity entity,int vertexIndex)
            {
                N++;
                AverageZ += (z-AverageZ)/N;
                if (z<MinZ) MinZ=z;
                if (z>MaxZ) MaxZ=z;
                LastHandle=handle;
                Members.Add(new Member { EntityId=entity.Id,VertexIndex=vertexIndex,
                    Handle=handle,Layer=entity.Layer,Type=entity.Type,Z=z });
            }
        }

        public sealed class ConflictMean
        {
            public double X { get; internal set; }
            public double Y { get; internal set; }
            public int VertexCount { get; internal set; }
            public double MinZ { get; internal set; }
            public double MaxZ { get; internal set; }
            public double MeanZ { get; internal set; }
            public string FirstHandle { get; internal set; }
            public string LastHandle { get; internal set; }
            public IReadOnlyList<Member> Members { get; internal set; }
            public double DeltaZ => MaxZ-MinZ;
            public bool NeedsReview => DeltaZ > ReviewThresholdMetres;
        }
        public sealed class Result
        {
            public PreparedSurfaceInput Prepared { get; internal set; }
            public IReadOnlyList<ConflictMean> Conflicts { get; internal set; }
            public IReadOnlyList<ConflictMean> ReviewGroups => Conflicts.Where(c=>c.NeedsReview).ToList();
            public bool HasBlockingConflicts => Conflicts.Any(c=>c.NeedsReview);
            public int OriginalVertexCount { get; internal set; }
            public int DuplicateGroupCount { get; internal set; }
            public int MergedVertexCount { get; internal set; }
            public int IgnoredBreaklineSegments { get; internal set; }
            public ConflictReport ToConflictReport(SurfaceModel source)
            {
                var report=new ConflictReport {ModelName=source.Name,SourceRevision=source.Revision,
                    XyTolerance=1e-6,ZTolerance=1e-4,InputVertices=OriginalVertexCount};
                int index=0;
                foreach(var item in Conflicts.OrderByDescending(c=>c.DeltaZ)) {
                    var group=new ConflictGroup {Id="XY-"+(++index).ToString("000"),Position=new Vec2(item.X,item.Y)};
                    foreach(var member in item.Members) group.Sources.Add(new ConflictSource {
                        EntityId=member.EntityId,Handle=member.Handle,Layer=member.Layer,Type=member.Type,
                        VertexIndex=member.VertexIndex,Z=member.Z,Generated=false,Origin="Đỉnh mô hình nguồn"});
                    report.Groups.Add(group);
                }
                return report;
            }

            public bool ForOfficialVolumes => false;
            public string Method => "TIN tham khảo: trung bình cộng Z của tất cả đỉnh trùng XY, tam giác hóa KHÔNG ràng buộc breakline";
        }

        public static Result Prepare(SurfaceModel source, double xyTolerance=1e-6,
            double zConflictTolerance=1e-4, CancellationToken token=default(CancellationToken),
            IReadOnlyList<Vec2> tinRegion=null)
        {
            if (source==null) throw new ArgumentNullException(nameof(source));
            if (!(xyTolerance>0) || double.IsInfinity(xyTolerance) || double.IsNaN(xyTolerance))
                throw new ArgumentOutOfRangeException(nameof(xyTolerance));
            if (zConflictTolerance<0 || double.IsNaN(zConflictTolerance) || double.IsInfinity(zConflictTolerance))
                throw new ArgumentOutOfRangeException(nameof(zConflictTolerance));

            // Use ORIGINAL model vertices so each review-table button maps to
            // a real editable entity/vertex, not a clipped synthetic segment.
            // Limit points to the same 10% support rectangle used for regional TIN.
            IReadOnlyList<Vec2> support=tinRegion!=null && tinRegion.Count>=3
                ? SurfaceRegionClipper.BuildSupportRectangle(tinRegion,0.10,xyTolerance)
                : null;
            var groups=new List<Group>();
            var cells=new Dictionary<Cell,List<int>>();
            int total=0,ignoredSegments=0;
            Cell At(double x,double y)
            {
                double cx=Math.Floor(x/xyTolerance),cy=Math.Floor(y/xyTolerance);
                if(cx<long.MinValue || cx>long.MaxValue || cy<long.MinValue || cy>long.MaxValue)
                    throw new InvalidOperationException("Tọa độ nguồn vượt phạm vi chỉ số lưới.");
                return new Cell((long)cx,(long)cy);
            }
            foreach(var entity in source.Entities)
            {
                token.ThrowIfCancellationRequested();
                if(!entity.IsEnabled)continue;
                // Even though edges are intentionally NOT constrained, account
                // for the ignored geometries and report this explicitly.
                if(entity.IsBreakline)
                    foreach(var segment in entity.ActiveBreaklineSegments())
                    {
                        token.ThrowIfCancellationRequested();
                        if(segment.Length2D>xyTolerance)ignoredSegments++;
                    }
                foreach(var vertex in entity.ActiveVertices())
                {
                    token.ThrowIfCancellationRequested();
                    Vec3 point=vertex.Position;
                    if(!Finite(point.X)||!Finite(point.Y)||!Finite(point.Z))
                        throw new InvalidOperationException("Đỉnh CAD chứa NaN/Infinity: "+entity.Handle);
                    if(support!=null && !SurfaceRegionClipper.ContainsInclusive(point.XY,support,xyTolerance))
                        continue;
                    total++;
                    var key=At(point.X,point.Y);
                    int found=-1;
                    for(long dx=-1;dx<=1&&found<0;dx++)
                        for(long dy=-1;dy<=1&&found<0;dy++)
                        {
                            if(!cells.TryGetValue(new Cell(key.X+dx,key.Y+dy),out var ids))continue;
                            foreach(int id in ids)
                            {
                                var xy=groups[id].XY;
                                double vx=xy.X-point.X,vy=xy.Y-point.Y;
                                if(vx*vx+vy*vy<=xyTolerance*xyTolerance){found=id;break;}
                            }
                        }
                    if(found>=0)
                    {
                        groups[found].Add(point.Z,entity.Handle,entity,vertex.Index);
                        continue;
                    }
                    var next=new Group
                    {
                        XY=new Vec2(point.X,point.Y),N=1,
                        AverageZ=point.Z,MinZ=point.Z,MaxZ=point.Z,
                        FirstHandle=entity.Handle,LastHandle=entity.Handle
                    };
                    next.Members.Add(new Member {EntityId=entity.Id,VertexIndex=vertex.Index,
                        Handle=entity.Handle,Layer=entity.Layer,Type=entity.Type,Z=point.Z});
                    int index=groups.Count;groups.Add(next);
                    if(!cells.TryGetValue(key,out var bucket))cells[key]=bucket=new List<int>();
                    bucket.Add(index);
                }
            }
            var prepared=new PreparedSurfaceInput();
            var conflicts=new List<ConflictMean>();
            int duplicates=0;
            foreach(var g in groups)
            {
                token.ThrowIfCancellationRequested();
                prepared.Sites.Add(new Vec3(g.XY.X,g.XY.Y,g.AverageZ));
                if(g.N<=1)continue;
                duplicates++;
                if(g.MaxZ-g.MinZ<=zConflictTolerance)continue;
                conflicts.Add(new ConflictMean {
                    X=g.XY.X,Y=g.XY.Y,VertexCount=g.N,
                    MinZ=g.MinZ,MaxZ=g.MaxZ,MeanZ=g.AverageZ,
                    FirstHandle=g.FirstHandle,LastHandle=g.LastHandle,
                    Members=g.Members.AsReadOnly()
                });
            }
            // Intentionally leave Breaklines empty. This is essential: passing
            // contradictory original edges would silently reintroduce invalid Z.
            prepared.Issues.Add(new ValidationIssue(ValidationSeverity.Warning,
                "MEAN_Z_PREVIEW_ONLY",
                "KHÔNG DÙNG TÍNH KHỐI LƯỢNG. Không bảo toàn đường breakline; cao độ trùng XY đã lấy trung bình."));
            return new Result {
                Prepared=prepared,Conflicts=conflicts,
                OriginalVertexCount=total,DuplicateGroupCount=duplicates,
                MergedVertexCount=total-groups.Count,
                IgnoredBreaklineSegments=ignoredSegments
            };
        }

        private static bool Finite(double x)=>!double.IsNaN(x)&&!double.IsInfinity(x);
    }

    public sealed class MeanZReviewRequiredException : InvalidOperationException
    {
        public MeanZPreviewPreparer.Result Analysis { get; }
        public MeanZReviewRequiredException(MeanZPreviewPreparer.Result analysis)
            : base("Có "+analysis.ReviewGroups.Count.ToString("n0")+
                " nhóm trùng XY có chênh lệch Z > 3,000 m. Hãy kiểm tra và loại/sửa đỉnh "+
                "ngay trên bảng rồi mới dựng TIN thử nghiệm.")
        {
            Analysis=analysis;
        }
    }
}
