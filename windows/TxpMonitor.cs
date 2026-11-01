using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Security.Principal;
using System.Threading;
using System.Windows.Forms;

namespace PixelTxtConverter
{
    // Watches events only: existing files are not scanned or automatically converted.
    public sealed class TxpMonitor : IDisposable
    {
        private sealed class Pending { public DateTime Due; public int Attempts; }
        private readonly object gate = new object();
        private readonly Dictionary<string, FileSystemWatcher> watchers = new Dictionary<string, FileSystemWatcher>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Pending> pending = new Dictionary<string, Pending>(StringComparer.OrdinalIgnoreCase);
        private readonly Action<string, string, Exception> completed;
        private readonly System.Threading.Timer timer;
        private bool disposed;
        private int working;

        public TxpMonitor(Action<string, string, Exception> completed)
        {
            this.completed = completed;
            timer = new System.Threading.Timer(ProcessPending, null, 200, 200);
        }

        public void Watch(string directory)
        {
            directory = Path.GetFullPath(directory);
            lock (gate)
            {
                if (disposed || watchers.ContainsKey(directory)) return;
                var watcher = new FileSystemWatcher(directory, "*.txp")
                {
                    IncludeSubdirectories = true,
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName,
                    InternalBufferSize = 65536
                };
                watcher.Created += (sender, e) => Queue(e.FullPath);
                watcher.Renamed += (sender, e) => Queue(e.FullPath);
                watcher.Error += (sender, e) =>
                {
                    lock (gate) { watchers.Remove(directory); watcher.Dispose(); }
                    Report(directory, null, new IOException("파일 감지에 문제가 발생했습니다. 자동으로 다시 연결합니다. 누락된 TXP는 더블클릭해 변환해 주세요.", e.GetException()));
                };
                try { watcher.EnableRaisingEvents = true; watchers.Add(directory, watcher); }
                catch { watcher.Dispose(); throw; }
            }
        }

        public void Queue(string path)
        {
            if (!string.Equals(Path.GetExtension(path), ".txp", StringComparison.OrdinalIgnoreCase)) return;
            lock (gate)
            {
                if (disposed) return;
                // Multiple notifications for the same rename share one conversion.
                if (!pending.ContainsKey(path)) pending.Add(path, new Pending { Due = DateTime.UtcNow.AddMilliseconds(600) });
            }
        }

        private void ProcessPending(object unused)
        {
            if (Interlocked.Exchange(ref working, 1) != 0) return;
            try
            {
                string path = null;
                Pending entry = null;
                lock (gate)
                {
                    if (disposed) return;
                    foreach (var pair in pending)
                        if (pair.Value.Due <= DateTime.UtcNow) { path = pair.Key; entry = pair.Value; break; }
                }
                if (path == null) return;
                try
                {
                    if (Directory.Exists(path)) { Finish(path); return; }
                    // Hold a read lease so writers/renames cannot change the input mid-conversion.
                    using (var lease = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        string output = Converter.ToImage(path, Path.ChangeExtension(path, ".png"), null);
                        Finish(path);
                        Report(path, output, null);
                    }
                }
                catch (Exception error)
                {
                    lock (gate)
                    {
                        entry.Attempts++;
                        if (entry.Attempts < 10 && (error is IOException || error is UnauthorizedAccessException))
                        { entry.Due = DateTime.UtcNow.AddMilliseconds(500); return; }
                        pending.Remove(path);
                    }
                    Report(path, null, error);
                }
            }
            finally { Interlocked.Exchange(ref working, 0); }
        }

        private void Finish(string path) { lock (gate) pending.Remove(path); }
        private void Report(string path, string output, Exception error)
        {
            lock (gate) { if (disposed) return; }
            if (completed != null) completed(path, output, error);
        }

        public void Dispose()
        {
            lock (gate)
            {
                if (disposed) return;
                disposed = true;
                foreach (var watcher in watchers.Values) watcher.Dispose();
                watchers.Clear(); pending.Clear();
            }
            using (var finished = new ManualResetEvent(false))
            { if (timer.Dispose(finished)) finished.WaitOne(); }
        }
    }

    internal sealed class WatcherApplication : ApplicationContext
    {
        private readonly TxpMonitor monitor;
        private readonly NotifyIcon tray;
        private readonly Control dispatcher = new Control();
        private readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
        private readonly EventWaitHandle stop;
        private readonly EventWaitHandle ready;
        private DateTime lastRefresh = DateTime.MinValue;
        private DateTime lastNotice = DateTime.MinValue;
        private static string UserSuffix { get { return WindowsIdentity.GetCurrent().User.Value; } }
        private static string MutexName { get { return @"Local\PixelTxtConverter.Watch." + UserSuffix; } }
        private static string StopName { get { return @"Local\PixelTxtConverter.Stop." + UserSuffix; } }
        private static string ReadyName { get { return @"Local\PixelTxtConverter.Ready." + UserSuffix; } }

        public static void RunWatcher()
        {
            bool created;
            using (var mutex = new Mutex(false, MutexName, out created))
            {
                if (!created) return;
                using (var context = new WatcherApplication()) Application.Run(context);
            }
        }

        public static void StopWatcher()
        {
            try { using (var signal = EventWaitHandle.OpenExisting(StopName)) signal.Set(); }
            catch (WaitHandleCannotBeOpenedException) { return; }
            for (int i = 0; i < 100; i++)
            {
                Mutex existing;
                if (!Mutex.TryOpenExisting(MutexName, out existing)) return;
                existing.Dispose(); Thread.Sleep(100);
            }
            throw new IOException("자동 변환을 종료하는 중입니다. 잠시 후 다시 설치해 주세요.");
        }

        public static void StartWatcher()
        {
            Process.Start(new ProcessStartInfo(ShellIntegration.InstalledExe, "--watch") { UseShellExecute = false, CreateNoWindow = true });
            for (int i = 0; i < 100; i++)
            {
                try { using (var signal = EventWaitHandle.OpenExisting(ReadyName)) if (signal.WaitOne(0)) return; }
                catch (WaitHandleCannotBeOpenedException) { }
                Thread.Sleep(100);
            }
            throw new IOException("자동 변환 프로그램을 시작하지 못했습니다. 설치 화면에서 다시 시작해 주세요.");
        }

        private WatcherApplication()
        {
            var handle = dispatcher.Handle;
            stop = new EventWaitHandle(false, EventResetMode.ManualReset, StopName);
            ready = new EventWaitHandle(false, EventResetMode.ManualReset, ReadyName);
            var menu = new ContextMenuStrip();
            menu.Items.Add("TXP → PNG 자동 변환 중").Enabled = false;
            menu.Items.Add("이번 로그인 동안 자동 변환 종료", null, (sender, e) => ExitThread());
            tray = new NotifyIcon { Icon = SystemIcons.Application, Text = "Pixel TXT · TXP 자동 변환", Visible = true, ContextMenuStrip = menu };
            monitor = new TxpMonitor((path, output, error) =>
            {
                if (dispatcher.IsDisposed) return;
                dispatcher.BeginInvoke(new Action(() =>
                {
                    if (DateTime.UtcNow - lastNotice < TimeSpan.FromSeconds(3)) return;
                    lastNotice = DateTime.UtcNow;
                    tray.ShowBalloonTip(3000, error == null ? "TXP → PNG 완료" : "TXP 자동 변환 안내",
                        error == null ? Path.GetFileName(output) : Path.GetFileName(path) + "\n" + error.Message,
                        error == null ? ToolTipIcon.Info : ToolTipIcon.Warning);
                }));
            });
            RefreshDrives();
            timer.Interval = 500;
            timer.Tick += (sender, e) =>
            {
                if (stop.WaitOne(0)) { ExitThread(); return; }
                if (DateTime.UtcNow - lastRefresh > TimeSpan.FromSeconds(10)) RefreshDrives();
            };
            timer.Start();
            ready.Set();
        }

        private void RefreshDrives()
        {
            lastRefresh = DateTime.UtcNow;
            foreach (var drive in DriveInfo.GetDrives())
            {
                try
                {
                    if ((drive.DriveType == DriveType.Fixed || drive.DriveType == DriveType.Removable) && drive.IsReady)
                        monitor.Watch(drive.RootDirectory.FullName);
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                timer.Dispose(); monitor.Dispose(); tray.Visible = false; tray.ContextMenuStrip.Dispose(); tray.Dispose();
                dispatcher.Dispose(); stop.Dispose(); ready.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
