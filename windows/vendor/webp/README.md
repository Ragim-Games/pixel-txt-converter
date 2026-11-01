# Embedded WebP decoder

`dwebp.exe` is the unmodified **libwebp 1.6.0 Windows x64** decoder distributed by the WebM project / Google.

- Upstream: https://developers.google.com/speed/webp/download
- Archive: https://storage.googleapis.com/downloads.webmproject.org/releases/webp/libwebp-1.6.0-windows-x64.zip
- Archive entry: `libwebp-1.6.0-windows-x64/bin/dwebp.exe`
- SHA-256: `17c1488bf84b7834e9aa908bb40afd0be2e55d57567d210e96336c56bb6ae993`
- License and patent grant: `COPYING` and `PATENTS`, copied from upstream tag `v1.6.0`.

The compiler embeds the decoder and license files in PixelTxtConverter.exe. Installation writes the license files beside the installed app. Each WebP conversion extracts the decoder into a unique temporary directory, decodes to PNG using Windows' native WIC PNG encoder, and removes its own temporary files. No WebP system codec, network download, or separately installed executable is needed at runtime.

The decoder requires 64-bit Windows. Still lossy, lossless and alpha WebP are supported; animated WebP is explicitly rejected. Build scripts verify the decoder's checksum before compilation.
