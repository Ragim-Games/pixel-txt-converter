using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace PixelTxtConverter
{
    public static class ShellIntegration
    {
        public static readonly string[] ImageExtensions = { ".png", ".jpg", ".jpeg", ".jfif", ".bmp", ".gif", ".tif", ".tiff", ".ico" };
        public static string InstallDirectory { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PixelTxtConverter"); } }
        public static string InstalledExe { get { return Path.Combine(InstallDirectory, "PixelTxtConverter.exe"); } }
        private const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\PixelTxtConverter";
        private const string Associations = @"Software\Classes\SystemFileAssociations";

        [DllImport("shell32.dll")]
        private static extern void SHChangeNotify(uint eventId, uint flags, IntPtr item1, IntPtr item2);

        public static string Command(string executable, string action)
        {
            return "\"" + executable + "\" " + action + " \"%1\"";
        }

        // A root parameter allows tests to use an isolated temporary registry subtree.
        public static void RegisterVerbs(RegistryKey root, string executable)
        {
            foreach (string extension in ImageExtensions)
                RegisterVerb(root, extension, "PixelTxtConverter.ToTxt", "TXT Convert", executable, "--to-txt");
            RegisterVerb(root, ".txt", "PixelTxtConverter.ToImage", "TXT Restore (PNG)", executable, "--to-image");
        }

        private static void RegisterVerb(RegistryKey root, string extension, string name, string title, string executable, string action)
        {
            using (var key = root.CreateSubKey(extension + @"\shell\" + name))
            {
                key.SetValue("MUIVerb", title);
                key.SetValue("Icon", "\"" + executable + "\",0");
                key.SetValue("MultiSelectModel", "Single");
                key.SetValue("PixelTxtOwner", executable);
                using (var command = key.CreateSubKey("command")) command.SetValue("", Command(executable, action));
            }
        }

        public static void UnregisterVerbs(RegistryKey root, string executable)
        {
            foreach (string extension in ImageExtensions)
                RemoveOwnedVerb(root, extension + @"\shell\PixelTxtConverter.ToTxt", executable);
            RemoveOwnedVerb(root, @".txt\shell\PixelTxtConverter.ToImage", executable);
        }

        private static void RemoveOwnedVerb(RegistryKey root, string path, string executable)
        {
            using (var key = root.OpenSubKey(path))
            {
                if (key == null || !string.Equals(key.GetValue("PixelTxtOwner") as string, executable, StringComparison.OrdinalIgnoreCase)) return;
            }
            root.DeleteSubKeyTree(path, false);
        }

        public static bool IsInstalled()
        {
            using (var key = Registry.CurrentUser.OpenSubKey(Associations + @"\.png\shell\PixelTxtConverter.ToTxt\command"))
                return File.Exists(InstalledExe) && key != null && string.Equals(key.GetValue("") as string, Command(InstalledExe, "--to-txt"), StringComparison.OrdinalIgnoreCase);
        }

        public static void Install()
        {
            Directory.CreateDirectory(InstallDirectory);
            string source = Assembly.GetExecutingAssembly().Location;
            if (!string.Equals(Path.GetFullPath(source), Path.GetFullPath(InstalledExe), StringComparison.OrdinalIgnoreCase))
                File.Copy(source, InstalledExe, true);
            using (var root = Registry.CurrentUser.CreateSubKey(Associations)) RegisterVerbs(root, InstalledExe);
            using (var key = Registry.CurrentUser.CreateSubKey(UninstallKey))
            {
                key.SetValue("DisplayName", "Pixel TXT Converter");
                key.SetValue("DisplayVersion", "2.0.0");
                key.SetValue("Publisher", "Ragim-Games");
                key.SetValue("InstallLocation", InstallDirectory);
                key.SetValue("DisplayIcon", InstalledExe);
                key.SetValue("UninstallString", "\"" + InstalledExe + "\" --uninstall");
                key.SetValue("NoModify", 1);
                key.SetValue("NoRepair", 1);
            }
            SHChangeNotify(0x08000000, 0, IntPtr.Zero, IntPtr.Zero);
        }

        public static void Uninstall()
        {
            using (var root = Registry.CurrentUser.OpenSubKey(Associations, true))
                if (root != null) UnregisterVerbs(root, InstalledExe);
            Registry.CurrentUser.DeleteSubKeyTree(UninstallKey, false);
            SHChangeNotify(0x08000000, 0, IntPtr.Zero, IntPtr.Zero);
            // The running executable cannot delete itself on Windows. Leave only this file;
            // don't launch a delayed shell script or delete unrelated directory contents.
        }
    }
}
