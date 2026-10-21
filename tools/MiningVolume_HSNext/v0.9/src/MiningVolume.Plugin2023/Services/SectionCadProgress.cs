using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Autodesk.AutoCAD.Runtime;

namespace MiningVolume2023.Services
{
    // Host UI thread only. Never pump DoEvents while a drawing transaction is open.
    internal sealed class SectionCadProgress : IDisposable
    {
        [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
        private readonly ProgressMeter _meter = new ProgressMeter();
        private readonly Stopwatch _paint = Stopwatch.StartNew();
        private int _completed;
        internal SectionCadProgress(int total)
        {
            _meter.Start("IMSAT: xuất mặt cắt — Esc để hủy");
            _meter.SetLimit(Math.Max(1, total));
        }
        internal void Poll()
        {
            if ((GetAsyncKeyState(0x1B) & 0x8000) != 0)
                throw new OperationCanceledException("Đã hủy xuất mặt cắt.");
            if (_paint.ElapsedMilliseconds >= 250)
            {
                Autodesk.AutoCAD.ApplicationServices.Application.UpdateScreen();
                _paint.Restart();
            }
        }
        internal void Report(int completed)
        {
            Poll();
            while (_completed < completed) { _meter.MeterProgress(); _completed++; }
        }
        public void Dispose() { try { _meter.Stop(); } finally { _meter.Dispose(); } }
    }
}
