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
            if (args.Length == 0) { Application.Run(new SetupForm()); return 0; }
            if (args.Length == 1 && args[0] == "--uninstall")
            {
                if (MessageBox.Show("파일 탐색기의 TXT Convert / TXT Restore 메뉴 연결을 해제할까요?\n이미지와 TXT 파일은 유지됩니다.",
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
            Text = "Pixel TXT Converter 설치";
            ClientSize = new Size(500, 350);
            Font = new Font("Malgun Gothic", 10);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleMode = AutoScaleMode.Dpi;
            var title = new Label { Text = "이미지 우클릭 → TXT Convert", Location = new Point(24, 24), Size = new Size(450, 36), Font = new Font(Font.FontFamily, 17, FontStyle.Bold) };
            var description = new Label { Text = "이미지 옆에 RGB TXT를 저장하고 메모장으로 엽니다.\nTXT 우클릭 → TXT Restore (PNG)로 이미지도 복원합니다.\n\nWindows 11: 우클릭 → 더 많은 옵션에서 표시됩니다.\n현재 사용자에게만 설치되며 관리자 권한은 필요 없습니다.", Location = new Point(24, 76), Size = new Size(450, 134) };
            state.SetBounds(24, 216, 450, 38);
            RefreshState();
            var install = new Button { Text = "설치 / 메뉴 연결", Location = new Point(24, 270), Size = new Size(210, 44) };
            var uninstall = new Button { Text = "메뉴 연결 해제", Location = new Point(250, 270), Size = new Size(210, 44) };
            install.Click += delegate
            {
                try { ShellIntegration.Install(); RefreshState(); MessageBox.Show("설치가 완료되었습니다.\n파일 탐색기에서 이미지를 우클릭하고 TXT Convert를 선택하세요.", "Pixel TXT Converter"); }
                catch (Exception error) { Program.ShowError(error); }
            };
            uninstall.Click += delegate
            {
                try { ShellIntegration.Uninstall(); RefreshState(); }
                catch (Exception error) { Program.ShowError(error); }
            };
            Controls.AddRange(new Control[] { title, description, state, install, uninstall });
        }

        private void RefreshState() { state.Text = ShellIntegration.IsInstalled() ? "상태: 파일 탐색기 메뉴에 연결되어 있습니다." : "상태: 설치되지 않았습니다."; }
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
                string desired = toText ? fullPath + ".txt" : Path.Combine(Path.GetDirectoryName(fullPath), Path.GetFileNameWithoutExtension(fullPath) + ".restored.png");
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
