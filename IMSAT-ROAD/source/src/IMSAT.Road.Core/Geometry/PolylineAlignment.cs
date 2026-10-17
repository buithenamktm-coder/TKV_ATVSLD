using System;
using System.Collections.Generic;
using IMSAT.Road.Core.Geometry;

namespace IMSAT.Road.Core.Geometry
{
    /// <summary>
    /// Early-stage centreline built from CAD Polyline vertices.
    /// Does not yet solve circular/transition curves (the VNRoad CN phase).
    /// Stations are measured in plan, metres, from the chosen start vertex.
    /// </summary>
    public sealed class PolylineAlignment
    {
        private readonly PlanPoint[] _vertices;
        private readonly double[] _chainages;
        public double StartStation { get; }
        public double Length { get; }
        public IReadOnlyList<PlanPoint> Vertices => Array.AsReadOnly(_vertices);

        public PolylineAlignment(IReadOnlyList<PlanPoint> vertices, double startStation = 0)
        {
            if (vertices == null) throw new ArgumentNullException(nameof(vertices));
            if (vertices.Count < 2) throw new ArgumentException("Tim tuyen can toi thieu hai dinh.", nameof(vertices));
            if (double.IsNaN(startStation) || double.IsInfinity(startStation))
                throw new ArgumentOutOfRangeException(nameof(startStation));
            _vertices = new PlanPoint[vertices.Count];
            _chainages = new double[vertices.Count];
            StartStation = startStation;
            for (int i = 0; i < vertices.Count; i++)
            {
                _vertices[i] = vertices[i];
                if (i == 0) continue;
                var ds = vertices[i - 1].DistanceTo(vertices[i]);
                if (ds < 1e-8)
                    throw new ArgumentException("Tim tuyen co hai dinh lien tiep trung XY tai vi tri " + i + ".", nameof(vertices));
                _chainages[i] = _chainages[i - 1] + ds;
            }
            Length = _chainages[_chainages.Length - 1];
        }

        /// <summary>Return XY and left-hand unit normal for a station.</summary>
        public PlanPoint PointAt(double station) => PositionAt(station).Point;

        public PlanPosition PositionAt(double station)
        {
            double local = station - StartStation;
            if (double.IsNaN(local) || double.IsInfinity(local) || local < -1e-7 || local > Length + 1e-7)
                throw new ArgumentOutOfRangeException(nameof(station), "Ly trinh nam ngoai pham vi tuyen.");
            local = Math.Max(0, Math.Min(Length, local));
            int segment = _vertices.Length - 2;
            for (int i = 0; i < _vertices.Length - 1; i++)
            {
                if (local < _chainages[i + 1] - 1e-9)
                {
                    segment = i;
                    break;
                }
            }
            double dx = _vertices[segment + 1].X - _vertices[segment].X;
            double dy = _vertices[segment + 1].Y - _vertices[segment].Y;
            double length = _chainages[segment + 1] - _chainages[segment];
            double t = (local - _chainages[segment]) / length;
            var point = new PlanPoint(_vertices[segment].X + t * dx, _vertices[segment].Y + t * dy);
            return new PlanPosition(point, new PlanPoint(-dy / length, dx / length));
        }
    }

    public readonly struct PlanPosition
    {
        public PlanPosition(PlanPoint point, PlanPoint leftNormal)
        {
            Point = point;
            LeftNormal = leftNormal;
        }
        public PlanPoint Point { get; }
        public PlanPoint LeftNormal { get; }
    }
}
