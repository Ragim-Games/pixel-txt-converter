using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace PixelTxtConverter
{
    public static class Converter
    {
        public const long MaxFileBytes = 64L * 1024 * 1024;
        public const int MaxPixels = 4194304;

        public static void ValidateSize(int width, int height)
        {
            if (width < 1 || height < 1 || width > 16384 || height > 16384 || (long)width * height > MaxPixels)
                throw new InvalidDataException("이미지는 최대 4,194,304픽셀, 한 변은 16,384픽셀까지 지원합니다.");
        }

        private static void ValidateInput(string path)
        {
            var info = new FileInfo(path);
            if (!info.Exists) throw new FileNotFoundException("파일을 찾을 수 없습니다.", path);
            if (info.Length > MaxFileBytes) throw new InvalidDataException("입력 파일은 최대 64 MiB까지 지원합니다.");
        }

        // FileMode.CreateNew protects existing data, including simultaneous conversions.
        public static FileStream CreateOutput(string desiredPath, out string actualPath)
        {
            for (int suffix = 1; suffix < 10000; suffix++)
            {
                actualPath = suffix == 1 ? desiredPath : Path.Combine(Path.GetDirectoryName(desiredPath),
                    Path.GetFileNameWithoutExtension(desiredPath) + " (" + suffix + ")" + Path.GetExtension(desiredPath));
                try { return new FileStream(actualPath, FileMode.CreateNew, FileAccess.Write, FileShare.None); }
                catch (IOException) { if (!File.Exists(actualPath)) throw; }
            }
            throw new IOException("같은 이름의 파일이 너무 많습니다.");
        }

        public static string ToText(string input, string desiredOutput, Action<int> progress)
        {
            ValidateInput(input);
            using (var source = WebpDecoder.OpenImage(input))
            {
                ValidateSize(source.Width, source.Height);
                using (var bitmap = new Bitmap(source.Width, source.Height, PixelFormat.Format24bppRgb))
                {
                    using (var graphics = Graphics.FromImage(bitmap))
                    {
                        graphics.Clear(Color.White);
                        graphics.DrawImage(source, new Rectangle(0, 0, bitmap.Width, bitmap.Height),
                            0, 0, source.Width, source.Height, GraphicsUnit.Pixel);
                    }
                    var bits = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height),
                        ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
                    string actual = null;
                    bool created = false;
                    try
                    {
                        using (var stream = CreateOutput(desiredOutput, out actual))
                        {
                            created = true;
                            using (var writer = new StreamWriter(stream, new UTF8Encoding(false), 65536))
                            {
                                writer.Write("PIXELTXT/1/" + bitmap.Width.ToString(CultureInfo.InvariantCulture) + "/" + bitmap.Height.ToString(CultureInfo.InvariantCulture) + "\n");
                                var row = new byte[bitmap.Width * 3];
                                for (int y = 0; y < bitmap.Height; y++)
                                {
                                    Marshal.Copy(IntPtr.Add(bits.Scan0, y * bits.Stride), row, 0, row.Length);
                                    var line = new StringBuilder(bitmap.Width * 12);
                                    for (int x = 0; x < bitmap.Width; x++)
                                    {
                                        if (x != 0 || y != 0) line.Append('/');
                                        int i = x * 3;
                                        line.Append(row[i + 2].ToString(CultureInfo.InvariantCulture)).Append(',')
                                            .Append(row[i + 1].ToString(CultureInfo.InvariantCulture)).Append(',')
                                            .Append(row[i].ToString(CultureInfo.InvariantCulture));
                                    }
                                    writer.Write(line);
                                    if (progress != null) progress((y + 1) * 100 / bitmap.Height);
                                }
                            }
                        }
                        return actual;
                    }
                    catch { if (created) TryDeletePartial(actual); throw; }
                    finally { bitmap.UnlockBits(bits); }
                }
            }
        }

        private static string ReadToken(TextReader reader, char delimiter, int limit, out bool found)
        {
            var value = new StringBuilder();
            found = false;
            int next;
            while ((next = reader.Read()) != -1)
            {
                if ((char)next == delimiter) { found = true; break; }
                if (value.Length >= limit) throw new InvalidDataException("TXT 항목이 너무 길거나 구분자가 없습니다.");
                value.Append((char)next);
            }
            return value.ToString().Trim();
        }

        public static string ToImage(string input, string desiredOutput, Action<int> progress)
        {
            ValidateInput(input);
            using (var reader = new StreamReader(input, new UTF8Encoding(false, true), true, 65536))
            {
                bool found;
                string header = ReadToken(reader, '\n', 100, out found);
                string[] fields = header.Split('/');
                int width, height;
                if (!found || fields.Length != 4 || fields[0] != "PIXELTXT" || fields[1] != "1" ||
                    !int.TryParse(fields[2], NumberStyles.None, CultureInfo.InvariantCulture, out width) ||
                    !int.TryParse(fields[3], NumberStyles.None, CultureInfo.InvariantCulture, out height))
                    throw new InvalidDataException("TXT 첫 줄은 PIXELTXT/1/가로/세로 형식이어야 합니다.");
                ValidateSize(width, height);
                using (var bitmap = new Bitmap(width, height, PixelFormat.Format24bppRgb))
                {
                    var bits = bitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);
                    try
                    {
                        var row = new byte[width * 3];
                        for (int y = 0; y < height; y++)
                        {
                            for (int x = 0; x < width; x++)
                            {
                                int index = y * width + x;
                                string token = ReadToken(reader, '/', 32, out found);
                                string[] rgb = token.Split(',');
                                byte r, g, b;
                                if (rgb.Length != 3 || !ByteChannel(rgb[0], out r) || !ByteChannel(rgb[1], out g) || !ByteChannel(rgb[2], out b))
                                    throw new InvalidDataException((index + 1) + "번째 픽셀은 0~255 범위의 R,G,B 값이어야 합니다.");
                                if (index < width * height - 1 && !found) throw new InvalidDataException("픽셀 수가 가로 × 세로보다 적습니다.");
                                if (index == width * height - 1 && found) throw new InvalidDataException("픽셀 수가 너무 많거나 끝에 불필요한 /가 있습니다.");
                                row[x * 3] = b; row[x * 3 + 1] = g; row[x * 3 + 2] = r;
                            }
                            Marshal.Copy(row, 0, IntPtr.Add(bits.Scan0, y * bits.Stride), row.Length);
                            if (progress != null) progress((y + 1) * 100 / height);
                        }
                    }
                    finally { bitmap.UnlockBits(bits); }
                    string actual = null;
                    bool created = false;
                    try
                    {
                        using (var stream = CreateOutput(desiredOutput, out actual))
                        {
                            created = true;
                            bitmap.Save(stream, ImageFormat.Png);
                        }
                        return actual;
                    }
                    catch { if (created) TryDeletePartial(actual); throw; }
                }
            }
        }

        private static bool ByteChannel(string text, out byte value)
        {
            value = 0;
            if (text.Length < 1 || text.Length > 3) return false;
            foreach (char c in text) if (c < '0' || c > '9') return false;
            return byte.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value);
        }

        private static void TryDeletePartial(string path)
        {
            try { File.Delete(path); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }
}
