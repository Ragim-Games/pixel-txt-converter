# Pixel TXT Converter

A Python app for Windows 10/11 that converts images to editable RGB text through Explorer. Rename a `.txt` file to `.txp` to automatically restore it as a PNG in the same folder.

Installation runs entirely in **Command Prompt (CMD)**. Normal use stays in Explorer and the notification area; there is no graphical setup window or separate WebP executable.

## Install from CMD

1. Install **Python 3.11 or later** with pip and Tcl/Tk (tkinter). Enable **Add Python to PATH** in the Python installer.
2. Download and extract this repository to a folder you intend to keep.
3. Open **Command Prompt**, switch to that folder, and run:

```bat
cd /d "C:\Tools\pixel-txt-converter"
install.cmd
```

Replace the example path with your extracted folder. Setup creates `.venv`, installs the dependencies, registers Explorer commands and the TXP file association for the current user, enables startup at Windows login, and starts automatic conversion. Administrator privileges are not required. Internet access is needed to download dependencies.

Setup uses `python` from PATH, falling back to `py -3`, or reuses an existing `.venv`. Errors appear in CMD and return a nonzero exit code. Fix the reported problem and rerun `install.cmd` if setup fails.

The app source is copied to `%LOCALAPPDATA%\PixelTxtConverter\python`. Registered commands use the Python interpreter in the original folder's `.venv`, so **keep that folder, its `.venv`, and the base Python installation in place**. To update, replace the project source and run `install.cmd` again.

If the previous C# version is installed, setup stops its watcher and replaces its registrations. After successful startup, it removes only the app's old executable and decoder license files.

## Use in Explorer

1. Right-click an image and select **TXT Convert**. The app saves `photo.png.txt` and opens it in Notepad.
2. Edit the RGB values if needed, keeping the size header and pixel count intact.
3. Change the final `.txt` extension to `.txp`. For example, rename `photo.png.txt` to `photo.png.txp`.
4. The background watcher creates `photo.png.png` in the same folder. For a simpler output name, rename the text file to `photo.txp` to produce `photo.png`.

You can also right-click TXT/TXP files and select **TXT Restore (PNG)** or **TXP Restore (PNG)**, or double-click a TXP file.

On Windows 11, the commands appear under **Show more options**. Enable file name extensions in Explorer so you can change the final extension: `photo.txp.txt` is still a TXT file and will not trigger automatic conversion.

Source images and TXT/TXP files are preserved. Existing output files are never overwritten; the app adds ` (2)`, ` (3)`, and so on when a name is already taken. Manual TXT restoration uses `photo.restored.png`; TXP restoration uses `photo.png`.

## Automatic conversion

- Starts after installation and at subsequent Windows logins.
- Watches newly created or renamed TXP files on local fixed and removable drives. It does not scan existing files.
- Detects newly attached drives within about 10 seconds. Use manual restoration for network folders.
- Retries briefly when a file is still being written. Failures appear in notifications and `%LOCALAPPDATA%\PixelTxtConverter\python.log`.
- Select **Stop automatic conversion for this login** from the tray icon to stop the watcher. Startup at the next login remains enabled.
- Restore files manually if they were created while the watcher was stopped or missed during a large file operation.

To restart the installed watcher from CMD:

```bat
.venv\Scripts\python.exe main.py --start-watcher
```

## Uninstall from CMD

From the original project folder, run:

```bat
uninstall.cmd
```

This stops the watcher and removes the app's login startup entry, Explorer commands, and TXP association, restoring the previous association where possible. Installed source files and all user images, TXT files, and TXP files remain. After a successful uninstall, you may delete the project folder and `%LOCALAPPDATA%\PixelTxtConverter` if no longer needed.

The app also registers an uninstall entry in Windows Settings. Uninstallation uses the console and does not require a setup dialog.

## TXT / TXP format

TXT and TXP contain the same UTF-8 text; only the extension changes.

```text
PIXELTXT/1/2/2
255,0,0/0,255,0/0,0,255/255,255,255
```

The first line is `PIXELTXT/1/width/height`. The remaining data consists of `R,G,B` entries separated by `/`, ordered left to right and top to bottom. Do not add a trailing `/`. Each channel must be an integer from 0 to 255.

The reader accepts a UTF-8 BOM, CRLF line endings, and whitespace around RGB entries. Invalid dimensions, channel values, and pixel counts produce an error.

## Supported images and limits

- PNG, JPG/JPEG/JFIF, BMP, GIF, TIFF, ICO, and still WebP (lossy, lossless, and transparent).
- WebP decoding uses Pillow; no separate `dwebp.exe` is required.
- Transparency is composited onto white. Animated WebP is rejected; GIF and TIFF use the first frame.
- Maximum image size: 4,194,304 pixels total and 16,384 pixels on either side. Maximum input file size: 64 MiB. Images are never silently resized.
- The text format stores RGB pixels, not original compressed bytes, metadata, or color profiles.
- Explorer integration and the tray watcher require Windows 10/11. Conversion functions are independent of Windows-specific APIs.

Dependencies: Pillow for image decoding, watchdog for file events, and pystray for the notification-area icon.

## Command line

Run these commands from the project folder in CMD after installing:

```bat
.venv\Scripts\python.exe main.py --help
.venv\Scripts\python.exe main.py --version
.venv\Scripts\python.exe main.py --to-txt "C:\Pictures\photo.webp"
.venv\Scripts\python.exe main.py --to-image "C:\Pictures\photo.txp"
.venv\Scripts\python.exe main.py --to-image "C:\Pictures\photo.txp" --no-open
.venv\Scripts\python.exe main.py --stop-watcher
.venv\Scripts\python.exe main.py --start-watcher
```

`--no-open` saves the output without opening Notepad or the default image viewer. Running `main.py` without arguments prints help. `--install` and `--uninstall` expose the registration steps for scripting; `install.cmd` also prepares the Python environment and dependencies.

## Tests

Stop an installed watcher before testing so it cannot process test files a second time:

```bat
.venv\Scripts\python.exe main.py --stop-watcher
.venv\Scripts\python.exe -m unittest discover -s tests -v
.venv\Scripts\python.exe main.py --start-watcher
```

Optional registry tests use a temporary key and do not alter real Explorer associations. Enable them in CMD with:

```bat
set "PIXELTXT_REGISTRY_TEST=1"
.venv\Scripts\python.exe -m unittest discover -s tests -v
set "PIXELTXT_REGISTRY_TEST="
```

GitHub Actions runs the tests on Windows with Python 3.11, 3.12, and 3.13. The suite covers exact RGB round trips, Unicode paths, WebP, malformed input, real file rename events, console commands, and isolated registration/removal. The project pins watchdog 6.0.0.

## Project layout

| Path | Purpose |
| --- | --- |
| `install.cmd` | CMD environment setup, dependency installation, and app registration |
| `uninstall.cmd` | CMD removal of automatic conversion and Explorer integration |
| `main.py` | Entry point |
| `pixeltxt/core.py` | Image and RGB TXT/TXP conversion |
| `pixeltxt/monitor.py` | TXP creation and rename detection |
| `pixeltxt/windows.py` | Source installation, file associations, and login startup |
| `pixeltxt/tray.py` | Tray icon and watcher lifecycle |
| `pixeltxt/app.py` | Console commands and conversion error dialogs |
| `requirements.txt` | Python dependencies |
| `tests/` | Conversion, watcher, CLI, and registry tests |
