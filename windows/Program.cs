using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PixelTxtConverter
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            if (args.Length == 1 && args[0] == "--watch") { WatcherApplication.RunWatcher(); return 0; }
            if (args.Length == 1 && args[0] == "--stop-watcher") { WatcherApplication.StopWatcher(); return 0; }
            if (args.Length == 1 && args[0] == "--install")
            {
                try { ShellIntegration.Install(); return 0; }
                catch (Exception error) { ShowError(error); return 1; }
            }
            if (args.Length == 0) { Application.Run(new SetupForm()); return 0; }
            if (args.Length == 1 && args[0] == "--uninstall")
            {
                if (MessageBox.Show("TXP 자동 변환, 로그인 자동 시작 및 파일 연결을 해제할까요?\n이미지와 TXT/TXP 파일은 유지됩니다.",
                    "Pixel TXT Converter", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return 0;
                try
                {
                    ShellIntegration.Uninstall();
                    MessageBox.Show("메뉴 연결을 해제했습니다.\n남아 있는 실행 파일은 필요 없다면 직접 삭제할 수 있습니다.\n" + ShellIntegration.InstallDirectory, "Pixel TXT Converter");
                    return 0;
                }
                catch (Exception error) { ShowError(error); return 1; }
            }
            if (args.Length == 2 && (args[0] == "--to-txt" || args[0] == "--to-image"))
            {
                var form = new ConvertForm(args[0] == "--to-txt", args[1]);
                Application.Run(form);
                return form.ExitCode;
            }
            MessageBox.Show("사용법:\nPixelTxtConverter.exe\nPixelTxtConverter.exe --to-txt \"이미지 경로\"\nPixelTxtConverter.exe --to-image \"TXT 경로\"", "Pixel TXT Converter");
            return 1;
        }

        internal static void ShowError(Exception error)
        {
            MessageBox.Show("처리하지 못했습니다.\n\n" + error.Message, "Pixel TXT Converter", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    internal sealed class SetupForm : Form
    {
        private readonly Label state = new Label();

        public SetupForm()
        {
            Text = "Pixel TXT Converter 2.2 · 설치 / 복구";
            ClientSize = new Size(500, 390);
            Font = new Font("Malgun Gothic", 10);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleMode = AutoScaleMode.Dpi;
            var title = new Label { Text = "이미지 우클릭 → TXT Convert", Location = new Point(24, 24), Size = new Size(450, 36), Font = new Font(Font.FontFamily, 17, FontStyle.Bold) };
            var description = new Label { Text = "이미지 우클릭 → TXT Convert → RGB TXT 저장\nTXT 확장자를 .txp로 변경 → 같은 폴더에 PNG 자동 생성\n\n설치하면 로그인 시 자동 변환 프로그램이 실행됩니다.\n로컬 드라이브의 새 TXP를 감지하며 알림 영역에서 종료합니다.\nWindows 10: 우클릭 / Windows 11: 더 많은 옵션\nWebP 포함 · Windows 10/11 64비트 지원", Location = new Point(24, 76), Size = new Size(450, 170) };
            state.SetBounds(24, 258, 450, 38);
            RefreshState();
            var install = new Button { Text = "설치 / 자동 변환 시작", Location = new Point(24, 316), Size = new Size(210, 44) };
            var uninstall = new Button { Text = "자동 변환 / 연결 해제", Location = new Point(250, 316), Size = new Size(210, 44) };
            install.Click += delegate
            {
                try { ShellIntegration.Install(); RefreshState(); MessageBox.Show("설치가 완료되었습니다.\nRGB TXT의 확장자를 .txp로 바꾸면 같은 폴더에 PNG가 생성됩니다.", "Pixel TXT Converter"); }
                catch (Exception error) { Program.ShowError(error); }
            };
            uninstall.Click += delegate
            {
                try { ShellIntegration.Uninstall(); RefreshState(); }
                catch (Exception error) { Program.ShowError(error); }
            };
            Controls.AddRange(new Control[] { title, description, state, install, uninstall });
        }

        private void RefreshState() { state.Text = ShellIntegration.IsInstalled() ? "상태: 메뉴 · TXP 연결 · 자동 시작이 등록되어 있습니다." : "상태: 설치 또는 업데이트가 필요합니다."; }
    }

    internal sealed class ConvertForm : Form
    {
        private readonly bool toText;
        private readonly string input;
        private readonly ProgressBar progress = new ProgressBar();
        private bool running = true;
        public int ExitCode { get; private set; }

        public ConvertForm(bool toText, string input)
        {
            this.toText = toText;
            this.input = input;
            Text = toText ? "TXT Convert" : "TXT Restore";
            ClientSize = new Size(440, 125);
            Font = new Font("Malgun Gothic", 10);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleMode = AutoScaleMode.Dpi;
            var label = new Label { Text = Path.GetFileName(input) + "\n" + (toText ? "RGB 값을 TXT로 저장하고 있습니다…" : "TXT에서 PNG 이미지를 복원하고 있습니다…"), AutoEllipsis = true, Location = new Point(20, 16), Size = new Size(400, 55) };
            progress.SetBounds(20, 85, 400, 16);
            Controls.AddRange(new Control[] { label, progress });
            Shown += async delegate { await RunConversion(); };
            FormClosing += delegate(object sender, FormClosingEventArgs e) { if (running) e.Cancel = true; };
        }

        private async Task RunConversion()
        {
            try
            {
                string fullPath = Path.GetFullPath(input);
                string desired = toText ? fullPath + ".txt" : string.Equals(Path.GetExtension(fullPath), ".txp", StringComparison.OrdinalIgnoreCase)
                    ? Path.ChangeExtension(fullPath, ".png") : Path.Combine(Path.GetDirectoryName(fullPath), Path.GetFileNameWithoutExtension(fullPath) + ".restored.png");
                int previous = -1;
                Action<int> report = value =>
                {
                    if (value == previous) return;
                    previous = value;
                    BeginInvoke(new Action(() => progress.Value = value));
                };
                string output = await Task.Run(() => toText ? Converter.ToText(fullPath, desired, report) : Converter.ToImage(fullPath, desired, report));
                try
                {
                    if (toText)
                        Process.Start(new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", "notepad.exe"), "\"" + output + "\"") { UseShellExecute = false });
                    else Process.Start(new ProcessStartInfo(output) { UseShellExecute = true });
                }
                catch (Exception error)
                {
                    MessageBox.Show("파일은 저장했지만 자동으로 열지 못했습니다.\n" + output + "\n\n" + error.Message, "Pixel TXT Converter");
                }
            }
            catch (Exception error) { ExitCode = 1; Program.ShowError(error); }
            finally { running = false; Close(); }
        }
    }
}
