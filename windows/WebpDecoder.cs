using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Text;

namespace PixelTxtConverter
{
    internal static class WebpDecoder
    {
        public static Image OpenImage(string path)
        {
            bool webp;
            using (var stream = File.OpenRead(path))
            {
                var header = new byte[12];
                webp = stream.Read(header, 0, 12) == 12 && Tag(header, 0) == "RIFF" && Tag(header, 8) == "WEBP";
            }
            if (!webp && !string.Equals(Path.GetExtension(path), ".webp", StringComparison.OrdinalIgnoreCase))
                return Image.FromFile(path);
            if (!webp) throw new InvalidDataException("올바른 WebP 파일이 아닙니다.");
            if (!Environment.Is64BitOperatingSystem) throw new PlatformNotSupportedException("WebP 변환은 Windows 10/11 64비트에서 지원됩니다.");

            string folder = Path.Combine(Path.GetTempPath(), "PixelTxt-WebP-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            string decoder = Path.Combine(folder, "dwebp.exe");
            string stagedInput = Path.Combine(folder, "input.webp");
            string output = Path.Combine(folder, "output.png");
            try
            {
                // Native tools receive ASCII relative paths, including on Korean Windows.
                File.Copy(path, stagedInput);
                ValidateHeader(stagedInput);
                using (var resource = Assembly.GetExecutingAssembly().GetManifestResourceStream("PixelTxt.dwebp.exe"))
                using (var file = new FileStream(decoder, FileMode.CreateNew, FileAccess.Write))
                {
                    if (resource == null) throw new InvalidOperationException("내장 WebP 디코더를 찾을 수 없습니다. 최신 EXE를 다시 받아 주세요.");
                    resource.CopyTo(file);
                }
                var start = new ProcessStartInfo(decoder, "-quiet input.webp -o output.png")
                {
                    WorkingDirectory = folder,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true
                };
                using (var process = Process.Start(start))
                {
                    var errors = process.StandardError.ReadToEndAsync();
                    if (!process.WaitForExit(60000))
                    {
                        process.Kill();
                        process.WaitForExit();
                        throw new InvalidDataException("WebP 변환 시간이 초과되었습니다.");
                    }
                    string detail = errors.GetAwaiter().GetResult();
                    if (process.ExitCode != 0 || !File.Exists(output))
                        throw new InvalidDataException("WebP를 읽을 수 없습니다. 손상된 파일인지 확인해 주세요.\n" + detail.Trim());
                }
                using (var decoded = Image.FromFile(output))
                {
                    Converter.ValidateSize(decoded.Width, decoded.Height);
                    return new Bitmap(decoded);
                }
            }
            finally
            {
                // Delete only the three files created by this conversion, never recurse.
                foreach (string file in new[] { output, stagedInput, decoder })
                {
                    try { File.Delete(file); } catch (IOException) { } catch (UnauthorizedAccessException) { }
                }
                try { Directory.Delete(folder, false); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            }
        }

        private static string Tag(byte[] bytes, int offset) { return Encoding.ASCII.GetString(bytes, offset, 4); }
        private static int UInt24(byte[] bytes, int offset) { return bytes[offset] | bytes[offset + 1] << 8 | bytes[offset + 2] << 16; }

        // Reject oversized images before the native decoder allocates its pixel buffer.
        internal static void ValidateHeader(string path)
        {
            using (var reader = new BinaryReader(File.OpenRead(path)))
            {
                byte[] riff = reader.ReadBytes(12);
                if (riff.Length != 12 || Tag(riff, 0) != "RIFF" || Tag(riff, 8) != "WEBP" || BitConverter.ToUInt32(riff, 4) + 8L != reader.BaseStream.Length)
                    throw new InvalidDataException("WebP 파일 헤더 또는 길이가 올바르지 않습니다.");
                bool foundImage = false;
                while (reader.BaseStream.Position + 8 <= reader.BaseStream.Length)
                {
                    string type = Encoding.ASCII.GetString(reader.ReadBytes(4));
                    uint length = reader.ReadUInt32();
                    long next = reader.BaseStream.Position + length + (length & 1);
                    if (next > reader.BaseStream.Length) throw new InvalidDataException("WebP 데이터가 잘렸습니다.");
                    byte[] bytes = reader.ReadBytes((int)Math.Min(length, 10u));
                    if (type == "VP8X")
                    {
                        if (bytes.Length < 10) throw new InvalidDataException("WebP VP8X 헤더가 잘못되었습니다.");
                        if ((bytes[0] & 2) != 0) throw new InvalidDataException("움직이는 WebP는 지원하지 않습니다. 정지 이미지 WebP를 사용해 주세요.");
                        Converter.ValidateSize(UInt24(bytes, 4) + 1, UInt24(bytes, 7) + 1);
                    }
                    else if (type == "VP8L")
                    {
                        if (bytes.Length < 5 || bytes[0] != 0x2f) throw new InvalidDataException("WebP VP8L 헤더가 잘못되었습니다.");
                        uint dimensions = BitConverter.ToUInt32(bytes, 1);
                        Converter.ValidateSize((int)(dimensions & 0x3fff) + 1, (int)((dimensions >> 14) & 0x3fff) + 1);
                        foundImage = true;
                    }
                    else if (type == "VP8 ")
                    {
                        if (bytes.Length < 10 || bytes[3] != 0x9d || bytes[4] != 1 || bytes[5] != 0x2a)
                            throw new InvalidDataException("WebP VP8 헤더가 잘못되었습니다.");
                        Converter.ValidateSize(BitConverter.ToUInt16(bytes, 6) & 0x3fff, BitConverter.ToUInt16(bytes, 8) & 0x3fff);
                        foundImage = true;
                    }
                    else if (type == "ANIM" || type == "ANMF") throw new InvalidDataException("움직이는 WebP는 지원하지 않습니다. 정지 이미지 WebP를 사용해 주세요.");
                    reader.BaseStream.Position = next;
                }
                if (!foundImage || reader.BaseStream.Position != reader.BaseStream.Length) throw new InvalidDataException("WebP 이미지 데이터가 없거나 손상되었습니다.");
            }
        }
    }
}
