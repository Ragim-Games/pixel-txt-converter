using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using System.Windows.Forms;
using System.Threading;
using System.Collections.Concurrent;
using Microsoft.Win32;
using PixelTxtConverter;

internal static class Tests
{
    private static int assertions;
    private static void Assert(bool condition, string message)
    {
        assertions++;
        if (!condition) throw new Exception(message);
    }

    private static int Main(string[] args)
    {
        try
        {
            string directory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "test-data", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            string image = Path.Combine(directory, "한글 공백 & image.png");
            Color[] colors = { Color.Red, Color.Lime, Color.Blue, Color.White };
            using (var bitmap = new Bitmap(2, 2))
            {
                for (int i = 0; i < colors.Length; i++) bitmap.SetPixel(i % 2, i / 2, colors[i]);
                bitmap.Save(image, ImageFormat.Png);
            }
            string expected = "PIXELTXT/1/2/2\n255,0,0/0,255,0/0,0,255/255,255,255";
            int progress = 0;
            string txt = Converter.ToText(image, image + ".txt", value => progress = value);
            Assert(File.ReadAllText(txt) == expected, "Row-major RGB serialization differs");
            Assert(progress == 100, "Progress did not finish");
            Assert(File.ReadAllBytes(txt)[0] == (byte)'P', "Unexpected BOM");
            string png = Converter.ToImage(txt, Path.Combine(directory, "restored.png"), null);
            using (var restored = new Bitmap(png))
            {
                Assert(restored.Width == 2 && restored.Height == 2, "Dimensions differ");
                for (int i = 0; i < colors.Length; i++) Assert(restored.GetPixel(i % 2, i / 2).ToArgb() == colors[i].ToArgb(), "Pixel differs");
            }
            string second = Converter.ToText(image, image + ".txt", null);
            Assert(second != txt && second.EndsWith(" (2).txt"), "Output was overwritten");
            Assert(File.ReadAllText(txt) == expected, "Existing text changed");
            string secondPng = Converter.ToImage(txt, png, null);
            Assert(secondPng != png, "Existing PNG was overwritten");

            string alpha = Path.Combine(directory, "alpha.png");
            using (var bitmap = new Bitmap(3, 1))
            {
                bitmap.SetPixel(0, 0, Color.FromArgb(0, 80, 40, 20));
                bitmap.SetPixel(1, 0, Color.FromArgb(128, 255, 0, 0));
                bitmap.SetPixel(2, 0, Color.FromArgb(255, 11, 22, 33));
                bitmap.Save(alpha, ImageFormat.Png);
            }
            Assert(File.ReadAllText(Converter.ToText(alpha, alpha + ".txt", null)) == "PIXELTXT/1/3/1\n255,255,255/255,127,127/11,22,33", "Alpha flattening differs");

            string large = Path.Combine(directory, "non-square.png");
            using (var bitmap = new Bitmap(137, 83))
            {
                for (int y = 0; y < bitmap.Height; y++)
                    for (int x = 0; x < bitmap.Width; x++) bitmap.SetPixel(x, y, Color.FromArgb(x % 256, y % 256, (x + y) % 256));
                bitmap.Save(large, ImageFormat.Png);
            }
            string largeTxt = Converter.ToText(large, large + ".txt", null);
            string largePng = Converter.ToImage(largeTxt, large + ".restored.png", null);
            using (var bitmap = new Bitmap(largePng))
                for (int y = 0; y < bitmap.Height; y++)
                    for (int x = 0; x < bitmap.Width; x++) Assert(bitmap.GetPixel(x, y).ToArgb() == Color.FromArgb(x % 256, y % 256, (x + y) % 256).ToArgb(), "Non-square or padded-stride pixel differs");

            string windowsTxt = Path.Combine(directory, "bom.txt");
            File.WriteAllText(windowsTxt, "PIXELTXT/1/2/1\r\n 0,1,2 / 253,254,255\r\n", new UTF8Encoding(true));
            using (var bitmap = new Bitmap(Converter.ToImage(windowsTxt, windowsTxt + ".png", null)))
                Assert(bitmap.GetPixel(1, 0).ToArgb() == Color.FromArgb(253, 254, 255).ToArgb(), "BOM or CRLF import failed");

            string[] bad = { "", "255,0,0", "PIXELTXT/2/1/1\n1,2,3", "PIXELTXT/1/0/1\n1,2,3", "PIXELTXT/1/9999999999/1\n1,2,3", "PIXELTXT/1/2049/2049\n1,2,3", "PIXELTXT/1/1/1\n256,0,0", "PIXELTXT/1/1/1\n-1,0,0", "PIXELTXT/1/1/1\n1.5,0,0", "PIXELTXT/1/1/1\n1,2", "PIXELTXT/1/1/1\n1,2,3/", "PIXELTXT/1/2/1\n1,2,3", "PIXELTXT/1/1/1\n1,2,3/4,5,6", "PIXELTXT/1/1/1\n" + new string('0', 100) };
            for (int i = 0; i < bad.Length; i++)
            {
                string malformed = Path.Combine(directory, "bad-" + i + ".txt");
                string output = malformed + ".png";
                File.WriteAllText(malformed, bad[i]);
                bool rejected = false;
                try { Converter.ToImage(malformed, output, null); } catch (InvalidDataException) { rejected = true; }
                Assert(rejected, "Malformed text accepted: " + i);
                Assert(!File.Exists(output), "Invalid input left a partial output");
            }
            Assert(ShellIntegration.Command(@"C:\folder with spaces\app.exe", "--to-txt") == "\"C:\\folder with spaces\\app.exe\" --to-txt \"%1\"", "Shell quoting failed");
            WebpTests(directory);
            TxpTests(directory);
            if (Array.IndexOf(args, "--registry") >= 0) RegistryTest();
            if (Array.IndexOf(args, "--render") >= 0)
            {
                Application.EnableVisualStyles();
                using (var form = (Form)Activator.CreateInstance(typeof(Converter).Assembly.GetType("PixelTxtConverter.SetupForm"), true))
                using (var screenshot = new Bitmap(form.ClientSize.Width, form.ClientSize.Height))
                {
                    using (var graphics = Graphics.FromImage(screenshot)) graphics.Clear(form.BackColor);
                    foreach (Control control in form.Controls) control.DrawToBitmap(screenshot, control.Bounds);
                    string screenshotPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "setup-preview.png");
                    screenshot.Save(screenshotPath, ImageFormat.Png);
                    Console.WriteLine("Preview: " + screenshotPath);
                }
            }
            Console.WriteLine("PASS: " + assertions + " assertions; pixel order, RGB round trip, alpha, padded stride, Unicode paths, no overwrite, validation and shell quoting.");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }

    private static void RegistryTest()
    {
        string testKey = @"Software\PixelTxtConverter.Tests\" + Guid.NewGuid().ToString("N");
        string executable = @"C:\test folder\PixelTxtConverter.exe";
        try
        {
            using (var root = Registry.CurrentUser.CreateSubKey(testKey))
            {
                ShellIntegration.RegisterVerbs(root, executable);
                Assert(ShellIntegration.HasAllVerbs(root, executable), "Complete registration not recognized");
                root.DeleteSubKeyTree(@".webp\shell\PixelTxtConverter.ToTxt");
                Assert(!ShellIntegration.HasAllVerbs(root, executable), "Old install missing WebP incorrectly reported complete");
                ShellIntegration.RegisterVerbs(root, executable);
                Assert(ShellIntegration.HasAllVerbs(root, executable), "Reinstall did not repair WebP menu");
                foreach (string extension in ShellIntegration.ImageExtensions)
                {
                    using (var key = root.OpenSubKey(extension + @"\shell\PixelTxtConverter.ToTxt"))
                    {
                        Assert((string)key.GetValue("MUIVerb") == "TXT Convert", "Wrong menu title");
                        using (var command = key.OpenSubKey("command"))
                            Assert((string)command.GetValue("") == ShellIntegration.Command(executable, "--to-txt"), "Wrong shell command");
                    }
                }
                using (var key = root.OpenSubKey(@".txt\shell\PixelTxtConverter.ToImage\command"))
                    Assert((string)key.GetValue("") == ShellIntegration.Command(executable, "--to-image"), "Restore registration missing");
                using (var other = root.CreateSubKey(@".png\shell\UnrelatedApp")) other.SetValue("", "Keep me");
                ShellIntegration.UnregisterVerbs(root, @"C:\wrong\app.exe");
                using (var key = root.OpenSubKey(@".png\shell\PixelTxtConverter.ToTxt")) Assert(key != null, "Removed another install's menu");
                ShellIntegration.UnregisterVerbs(root, executable);
                using (var key = root.OpenSubKey(@".png\shell\PixelTxtConverter.ToTxt")) Assert(key == null, "Menu not removed");
                using (var key = root.OpenSubKey(@".webp\shell\PixelTxtConverter.ToTxt")) Assert(key == null, "WebP menu not removed");
                using (var key = root.OpenSubKey(@".txt\shell\PixelTxtConverter.ToImage")) Assert(key == null, "Restore menu not removed");
                using (var key = root.OpenSubKey(@".png\shell\UnrelatedApp")) Assert(key != null, "Removed unrelated menu");
                using (var classes = root.CreateSubKey("Classes"))
                {
                    using (var extension = classes.CreateSubKey(".txp")) extension.SetValue("", "Previous.TxpApp");
                    ShellIntegration.RegisterTxp(classes, executable);
                    ShellIntegration.RegisterTxp(classes, executable);
                    Assert(ShellIntegration.HasTxp(classes, executable), "TXP association missing");
                    using (var command = classes.OpenSubKey(ShellIntegration.TxpProgId + @"\shell\open\command"))
                        Assert((string)command.GetValue("") == ShellIntegration.Command(executable, "--to-image"), "TXP open command incorrect");
                    ShellIntegration.UnregisterTxp(classes, @"C:\other\app.exe");
                    Assert(ShellIntegration.HasTxp(classes, executable), "Wrong owner removed TXP association");
                    ShellIntegration.UnregisterTxp(classes, executable);
                    using (var extension = classes.OpenSubKey(".txp")) Assert((string)extension.GetValue("") == "Previous.TxpApp", "Uninstall did not restore previous TXP association");
                    using (var type = classes.OpenSubKey(ShellIntegration.TxpProgId)) Assert(type == null, "TXP ProgID was not removed");
                }
            }
            Console.WriteLine("PASS: isolated registry menu registration and removal.");
        }
        finally { Registry.CurrentUser.DeleteSubKeyTree(testKey, false); }
    }

    private static void WebpTests(string directory)
    {
        string fixtures = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "windows", "fixtures"));
        string korean = Path.Combine(directory, "한글 공백 & 무손실.WEBP");
        File.Copy(Path.Combine(fixtures, "colors.webp"), korean);
        string txt = Converter.ToText(korean, korean + ".txt", null);
        Assert(File.ReadAllText(txt) == "PIXELTXT/1/2/2\n255,0,0/0,255,0/0,0,255/255,255,255", "Lossless WebP or Unicode paths differ");
        string restored = Converter.ToImage(txt, korean + ".png", null);
        using (var bitmap = new Bitmap(restored)) Assert(bitmap.GetPixel(0, 1).ToArgb() == Color.Blue.ToArgb(), "WebP to TXT to PNG round trip failed");
        string alpha = Converter.ToText(Path.Combine(fixtures, "alpha.webp"), Path.Combine(directory, "webp-alpha.txt"), null);
        Assert(File.ReadAllText(alpha) == "PIXELTXT/1/3/1\n255,255,255/255,127,127/11,22,33", "WebP transparency differs");
        string lossy = Converter.ToText(Path.Combine(fixtures, "lossy.webp"), Path.Combine(directory, "webp-lossy.txt"), null);
        string lossyText = File.ReadAllText(lossy);
        Assert(lossyText.StartsWith("PIXELTXT/1/8/4\n"), "Lossy VP8 dimensions differ");
        Assert(lossyText.Substring(lossyText.IndexOf('\n') + 1).Split('/').Length == 32, "Lossy WebP pixel count differs");
        string png = Converter.ToImage(lossy, lossy + ".png", null);
        using (var bitmap = new Bitmap(png))
        {
            Color color = bitmap.GetPixel(0, 0);
            Assert(color.R > 240 && color.G < 10 && color.B < 10, "Lossy WebP decoded incorrectly");
        }
        foreach (string kind in new[] { "oversized", "animated", "corrupt" })
        {
            string invalid = Path.Combine(directory, kind + ".webp");
            if (kind == "corrupt") File.WriteAllText(invalid, "not a WebP image");
            else using (var writer = new BinaryWriter(File.Create(invalid)))
            {
                writer.Write(Encoding.ASCII.GetBytes("RIFF")); writer.Write(22u);
                writer.Write(Encoding.ASCII.GetBytes("WEBPVP8X")); writer.Write(10u);
                writer.Write((byte)(kind == "animated" ? 2 : 0)); writer.Write(new byte[3]);
                writer.Write(kind == "oversized" ? new byte[] { 255, 255, 255, 255, 255, 255 } : new byte[6]);
            }
            bool rejected = false;
            try { Converter.ToText(invalid, invalid + ".txt", null); }
            catch (InvalidDataException error)
            {
                rejected = true;
                if (kind == "animated") Assert(error.Message.Contains("움직이는"), "Animated WebP error is unclear");
                if (kind == "oversized") Assert(error.Message.Contains("4,194,304"), "Oversized WebP not rejected before decode");
            }
            Assert(rejected && !File.Exists(invalid + ".txt"), "Invalid WebP created an output");
        }
        Console.WriteLine("PASS: embedded WebP decoder (lossless, lossy, alpha, Unicode, uppercase extension, malformed/oversized/animated input).");
    }

    private static void WaitUntil(Func<bool> condition, string message, int milliseconds = 10000)
    {
        DateTime deadline = DateTime.UtcNow.AddMilliseconds(milliseconds);
        while (!condition() && DateTime.UtcNow < deadline) Thread.Sleep(50);
        Assert(condition(), message);
    }

    private static void TxpTests(string directory)
    {
        string watched = Path.Combine(directory, "watch");
        string nested = Path.Combine(watched, "한글 하위 폴더");
        Directory.CreateDirectory(nested);
        string text = "PIXELTXT/1/2/2\n255,0,0/0,255,0/0,0,255/255,255,255";
        File.WriteAllText(Path.Combine(watched, "preexisting.txp"), text);
        var outputs = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var failures = new ConcurrentDictionary<string, Exception>(StringComparer.OrdinalIgnoreCase);
        using (var monitor = new TxpMonitor((path, output, error) =>
        { if (error == null) outputs[path] = output; else failures[path] = error; }))
        {
            monitor.Watch(watched);
            string txt = Path.Combine(nested, "이름 변경.txt");
            string txp = Path.ChangeExtension(txt, ".TXP");
            File.WriteAllText(txt, text);
            File.Move(txt, txp);
            WaitUntil(() => outputs.ContainsKey(txp), "Renaming TXT to TXP did not automatically generate PNG");
            Assert(outputs[txp] == Path.ChangeExtension(txp, ".png"), "Automatic PNG filename incorrect");
            using (var bitmap = new Bitmap(outputs[txp])) Assert(bitmap.GetPixel(0, 1).ToArgb() == Color.Blue.ToArgb(), "Automatic TXP pixel order incorrect");
            Assert(File.ReadAllText(txp) == text, "Original TXP changed");
            Assert(!File.Exists(Path.Combine(watched, "preexisting.png")), "Watcher unexpectedly scanned old files");

            string collision = Path.Combine(watched, "existing.txp");
            File.WriteAllText(Path.ChangeExtension(collision, ".png"), "KEEP");
            File.WriteAllText(collision, text);
            WaitUntil(() => outputs.ContainsKey(collision), "New TXP was not detected");
            Assert(outputs[collision].EndsWith("existing (2).png"), "Existing PNG was overwritten");
            Assert(File.ReadAllText(Path.ChangeExtension(collision, ".png")) == "KEEP", "Existing PNG content changed");

            string locked = Path.Combine(watched, "writing.txp");
            using (var writer = new StreamWriter(new FileStream(locked, FileMode.CreateNew, FileAccess.Write, FileShare.None)))
            { Thread.Sleep(900); writer.Write(text); }
            WaitUntil(() => outputs.ContainsKey(locked), "Locked/incomplete TXP was not retried");

            string bad = Path.Combine(watched, "bad.txp");
            File.WriteAllText(bad, "not pixel data");
            WaitUntil(() => failures.ContainsKey(bad), "Malformed TXP did not report an error");
            Assert(!File.Exists(Path.ChangeExtension(bad, ".png")), "Invalid TXP created an image");
            File.WriteAllText(Path.Combine(watched, "ignored.txp.txt"), text);
            Thread.Sleep(900);
            Assert(outputs.Count == 3, "Duplicate notifications generated extra outputs");
            Assert(Directory.GetFiles(nested, "*.png").Length == 1, "Rename created duplicate PNG files");
        }
        File.WriteAllText(Path.Combine(watched, "after-stop.txp"), text);
        Thread.Sleep(800);
        Assert(!File.Exists(Path.Combine(watched, "after-stop.png")), "Stopped watcher still converts files");
        Console.WriteLine("PASS: actual TXT→TXP rename, nested folders, Unicode, new files, write retries, collision protection, invalid input and watcher shutdown.");
    }
}
