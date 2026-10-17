using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MiningVolume.Core.Geometry;
using MiningVolume.Core.Surface;

namespace MiningVolume.Surface
{
    /// <summary>
    /// Danh sách tam giác TIN lưu nhị phân theo tile trên đĩa. RAM chỉ giữ
    /// metadata + một cache nhỏ các tile đang được truy vấn.
    /// Mỗi record = 9 double = 72 byte.
    /// </summary>
    public sealed class FileBackedTiledTriangleList : ITiledTriangleSource
    {
        public const int BytesPerTriangle = 9 * sizeof(double);
        private readonly string _path;
        private readonly List<TinTileInfo> _tiles;
        private readonly long[] _starts;
        private readonly object _sync = new object();
        private readonly Dictionary<int, Triangle3[]> _cache = new Dictionary<int, Triangle3[]>();
        private readonly LinkedList<int> _lru = new LinkedList<int>();
        private readonly int _cacheTiles;
        private bool _disposed;
        private readonly int _count;

        public FileBackedTiledTriangleList(string path, IEnumerable<TinTileInfo> tiles, int cacheTiles = 6)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentNullException(nameof(path));
            if (!File.Exists(path)) throw new FileNotFoundException("Không tìm thấy kho TIN phân ô.", path);
            _path = path;
            _tiles = (tiles ?? throw new ArgumentNullException(nameof(tiles))).OrderBy(x => x.Index).ToList();
            _cacheTiles = Math.Max(1, cacheTiles);
            _starts = new long[_tiles.Count];

            long total = 0;
            for (int i = 0; i < _tiles.Count; i++)
            {
                _starts[i] = total;
                total += _tiles[i].TriangleCount;
            }
            if (total > int.MaxValue)
                throw new InvalidOperationException("TIN có quá nhiều tam giác cho chỉ số 32-bit hiện tại.");
            _count = (int)total;
        }

        public int Count => _count;
        public bool IsFileBacked => true;
        public IReadOnlyList<TinTileInfo> Tiles => _tiles;

        public Triangle3 this[int index]
        {
            get
            {
                if (index < 0 || index >= Count) throw new ArgumentOutOfRangeException(nameof(index));
                int tile = FindTile(index);
                int local = (int)(index - _starts[tile]);
                return ReadTile(tile)[local];
            }
        }

        public IReadOnlyList<Triangle3> ReadTile(int tileIndex)
        {
            if (tileIndex < 0 || tileIndex >= _tiles.Count)
                throw new ArgumentOutOfRangeException(nameof(tileIndex));
            ThrowIfDisposed();

            lock (_sync)
            {
                Triangle3[] cached;
                if (_cache.TryGetValue(tileIndex, out cached))
                {
                    Touch(tileIndex);
                    return cached;
                }

                var info = _tiles[tileIndex];
                var items = new Triangle3[info.TriangleCount];
                using (var fs = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.Read,
                    1 << 20, FileOptions.SequentialScan))
                using (var br = new BinaryReader(fs))
                {
                    fs.Seek(info.ByteOffset, SeekOrigin.Begin);
                    for (int i = 0; i < items.Length; i++)
                        items[i] = ReadTriangle(br);
                }

                _cache[tileIndex] = items;
                _lru.AddLast(tileIndex);
                while (_cache.Count > _cacheTiles)
                {
                    int oldest = _lru.First.Value;
                    _lru.RemoveFirst();
                    _cache.Remove(oldest);
                }
                return items;
            }
        }

        public IEnumerable<int> QueryTiles(double minX, double minY, double maxX, double maxY)
        {
            ThrowIfDisposed();
            for (int i = 0; i < _tiles.Count; i++)
                if (_tiles[i].Intersects(minX, minY, maxX, maxY))
                    yield return i;
        }

        public IEnumerator<Triangle3> GetEnumerator()
        {
            ThrowIfDisposed();
            using (var fs = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.Read,
                4 << 20, FileOptions.SequentialScan))
            using (var br = new BinaryReader(fs))
            {
                for (int i = 0; i < Count; i++)
                    yield return ReadTriangle(br);
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public void Dispose()
        {
            lock (_sync)
            {
                if (_disposed) return;
                _disposed = true;
                _cache.Clear();
                _lru.Clear();
            }
            try { File.Delete(_path); } catch { }
        }

        private int FindTile(int index)
        {
            int lo = 0, hi = _starts.Length - 1;
            while (lo <= hi)
            {
                int mid = lo + ((hi - lo) >> 1);
                long start = _starts[mid];
                long end = start + _tiles[mid].TriangleCount;
                if (index < start) hi = mid - 1;
                else if (index >= end) lo = mid + 1;
                else return mid;
            }
            throw new InvalidOperationException("Không ánh xạ được chỉ số tam giác vào tile.");
        }

        private void Touch(int tileIndex)
        {
            var node = _lru.Find(tileIndex);
            if (node == null) return;
            _lru.Remove(node);
            _lru.AddLast(node);
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(FileBackedTiledTriangleList));
        }

        public static void WriteTriangle(BinaryWriter bw, Triangle3 t)
        {
            bw.Write(t.A.X); bw.Write(t.A.Y); bw.Write(t.A.Z);
            bw.Write(t.B.X); bw.Write(t.B.Y); bw.Write(t.B.Z);
            bw.Write(t.C.X); bw.Write(t.C.Y); bw.Write(t.C.Z);
        }

        private static Triangle3 ReadTriangle(BinaryReader br)
        {
            return new Triangle3(
                new Vec3(br.ReadDouble(), br.ReadDouble(), br.ReadDouble()),
                new Vec3(br.ReadDouble(), br.ReadDouble(), br.ReadDouble()),
                new Vec3(br.ReadDouble(), br.ReadDouble(), br.ReadDouble()));
        }
    }
}
