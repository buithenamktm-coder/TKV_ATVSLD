using System;
using System.Runtime.InteropServices;
using Autodesk.AutoCAD.Runtime;

namespace MiningVolume2023.Services
{
    // UI-thread only. No DoEvents/re-entrant model edits during a CAD transaction.
    // Esc aborts the transaction, preserving the previous layer on cancellation.
    internal sealed class TinCadProgress : IDisposable
    {
        [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
        private readonly ProgressMeter _meter=new ProgressMeter();
        private int _last;
        private readonly System.Diagnostics.Stopwatch _paintWatch=System.Diagnostics.Stopwatch.StartNew();
        internal TinCadProgress(int faces, string layerName, bool allowCancellation = true) {
            _meter.Start("IMSAT: ghi " + layerName +
                (allowCancellation ? " — Esc để hủy" :
                    " — giai đoạn ghi bảo toàn dữ liệu (không hủy)"));
            _meter.SetLimit(Math.Max(1,(faces+1023)/1024));
        }
        internal void Poll(int written,bool writing) {
            if((GetAsyncKeyState(0x1B)&0x8000)!=0)throw new OperationCanceledException("Đã hủy ghi TIN; giữ layer trước thao tác.");
            if(writing&&written/1024>_last){_last=written/1024;_meter.MeterProgress();}
            // Poll Esc at every chunk; repaint at most four times a second.
            // Repainting every 1024 faces/scanned entities wastes host UI time.
            if(_paintWatch.ElapsedMilliseconds>=250) {
                Autodesk.AutoCAD.ApplicationServices.Application.UpdateScreen();
                _paintWatch.Restart();
            }
        }
        public void Dispose(){try{_meter.Stop();}finally{_meter.Dispose();}}
    }
}
