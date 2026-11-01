"""Per-user Explorer registration and source installation. Windows only."""
import ctypes
from ctypes import wintypes
import hashlib
import os
from pathlib import Path
import shutil
import subprocess
import sys
import time
import winreg

from . import __version__

EXTENSIONS = (".png", ".jpg", ".jpeg", ".jfif", ".bmp", ".gif", ".tif", ".tiff", ".ico", ".webp")
CLASSES = r"Software\Classes"
ASSOCIATIONS = r"SystemFileAssociations"
PROG_ID = "RagimGames.PixelTxt.1"
RUN = r"Software\Microsoft\Windows\CurrentVersion\Run"
UNINSTALL = r"Software\Microsoft\Windows\CurrentVersion\Uninstall\PixelTxtConverter"
INSTALL_DIR = Path(os.environ["LOCALAPPDATA"]) / "PixelTxtConverter"
SOURCE_DIR = INSTALL_DIR / "python"
OWNER = "PixelTxtOwner"
_suffix = hashlib.sha256(os.environ["USERPROFILE"].casefold().encode()).hexdigest()[:16]
_kernel = ctypes.WinDLL("kernel32", use_last_error=True)
for _name, _argtypes, _restype in (
    ("CreateMutexW", [wintypes.LPVOID, wintypes.BOOL, wintypes.LPCWSTR], wintypes.HANDLE),
    ("OpenMutexW", [wintypes.DWORD, wintypes.BOOL, wintypes.LPCWSTR], wintypes.HANDLE),
    ("CreateEventW", [wintypes.LPVOID, wintypes.BOOL, wintypes.BOOL, wintypes.LPCWSTR], wintypes.HANDLE),
    ("OpenEventW", [wintypes.DWORD, wintypes.BOOL, wintypes.LPCWSTR], wintypes.HANDLE),
    ("SetEvent", [wintypes.HANDLE], wintypes.BOOL),
    ("WaitForSingleObject", [wintypes.HANDLE, wintypes.DWORD], wintypes.DWORD),
    ("CloseHandle", [wintypes.HANDLE], wintypes.BOOL),
    ("GetLogicalDrives", [], wintypes.DWORD),
    ("GetDriveTypeW", [wintypes.LPCWSTR], wintypes.UINT),
):
    getattr(_kernel, _name).argtypes = _argtypes
    getattr(_kernel, _name).restype = _restype


def _name(kind):
    return f"Local\\PixelTxtPython.{kind}.{_suffix}"


def python_windowless():
    candidate = Path(sys.executable).with_name("pythonw.exe")
    return candidate if candidate.exists() else Path(sys.executable)


def command(script, action, file_argument=True):
    result = subprocess.list2cmdline([str(python_windowless()), str(script), action])
    return result + (' "%1"' if file_argument else "")


def _value(root, path, name="", default=None):
    try:
        with winreg.OpenKey(root, path) as key:
            return winreg.QueryValueEx(key, name)[0]
    except FileNotFoundError:
        return default


def _set(root, path, values):
    with winreg.CreateKey(root, path) as key:
        for name, value in values.items():
            winreg.SetValueEx(key, name, 0, winreg.REG_DWORD if isinstance(value, int) else winreg.REG_SZ, value)


def _delete_value(root, path, name):
    try:
        with winreg.OpenKey(root, path, 0, winreg.KEY_SET_VALUE) as key:
            winreg.DeleteValue(key, name)
    except FileNotFoundError:
        pass


def _delete_tree(root, path):
    # Caller checks ownership before deleting an app-specific subtree.
    try:
        with winreg.OpenKey(root, path, 0, winreg.KEY_READ | winreg.KEY_WRITE) as key:
            children = []
            for i in range(winreg.QueryInfoKey(key)[0]):
                children.append(winreg.EnumKey(key, i))
            for child in children:
                _delete_tree(key, child)
        winreg.DeleteKey(root, path)
    except FileNotFoundError:
        pass


def _verb(root, extension, name, title, script, action):
    path = f"{ASSOCIATIONS}\\{extension}\\shell\\{name}"
    _set(root, path, {"MUIVerb": title, "MultiSelectModel": "Single", OWNER: str(script)})
    # Remove the icon pointing at the retired C# executable on upgrade.
    _delete_value(root, path, "Icon")
    _set(root, path + r"\command", {"": command(script, action)})


def register(root, script):
    """Root is a Classes key, or a temporary test key with the same layout."""
    for extension in EXTENSIONS:
        _verb(root, extension, "PixelTxtConverter.ToTxt", "TXT Convert", script, "--to-txt")
    for extension in (".txt", ".txp"):
        _verb(root, extension, "PixelTxtConverter.ToImage", "TXP Restore (PNG)" if extension == ".txp" else "TXT Restore (PNG)", script, "--to-image")
    previous = _value(root, ".txp", "PixelTxtPreviousProgId")
    if previous is None:
        previous = _value(root, ".txp", default="")
    _set(root, ".txp", {"": PROG_ID, OWNER: str(script), "PixelTxtPreviousProgId": previous})
    _set(root, PROG_ID, {"": "TXP RGB Image", OWNER: str(script)})
    _set(root, PROG_ID + r"\DefaultIcon", {"": f'"{python_windowless()}",0'})
    _set(root, PROG_ID + r"\shell", {"": "open"})
    _set(root, PROG_ID + r"\shell\open", {"": "PNG 이미지로 복원"})
    _set(root, PROG_ID + r"\shell\open\command", {"": command(script, "--to-image")})


def registered(root, script):
    for extension in EXTENSIONS + (".txt", ".txp"):
        action = "--to-txt" if extension in EXTENSIONS else "--to-image"
        verb = "ToTxt" if extension in EXTENSIONS else "ToImage"
        if _value(root, f"{ASSOCIATIONS}\\{extension}\\shell\\PixelTxtConverter.{verb}\\command") != command(script, action):
            return False
    return _value(root, ".txp") == PROG_ID and _value(root, PROG_ID + r"\shell\open\command") == command(script, "--to-image")


def unregister(root, script):
    for extension in EXTENSIONS + (".txt", ".txp"):
        verb = "ToTxt" if extension in EXTENSIONS else "ToImage"
        path = f"{ASSOCIATIONS}\\{extension}\\shell\\PixelTxtConverter.{verb}"
        if _value(root, path, OWNER) == str(script):
            _delete_tree(root, path)
    if _value(root, ".txp", OWNER) == str(script):
        if _value(root, ".txp") == PROG_ID:
            previous = _value(root, ".txp", "PixelTxtPreviousProgId", "")
            if previous:
                _set(root, ".txp", {"": previous})
            else:
                _delete_value(root, ".txp", "")
        _delete_value(root, ".txp", OWNER)
        _delete_value(root, ".txp", "PixelTxtPreviousProgId")
    if _value(root, PROG_ID, OWNER) == str(script):
        _delete_tree(root, PROG_ID)


class WatcherLease:
    def __init__(self):
        self.mutex = _kernel.CreateMutexW(None, False, _name("Mutex"))
        if not self.mutex:
            raise ctypes.WinError(ctypes.get_last_error())
        self.existing = ctypes.get_last_error() == 183
        self.stop = self.ready = None
        if not self.existing:
            self.stop = _kernel.CreateEventW(None, True, False, _name("Stop"))
            self.ready = _kernel.CreateEventW(None, True, False, _name("Ready"))

    def stopping(self):
        return _kernel.WaitForSingleObject(self.stop, 0) == 0

    def mark_ready(self):
        _kernel.SetEvent(self.ready)

    def close(self):
        for handle in (self.ready, self.stop, self.mutex):
            if handle:
                _kernel.CloseHandle(handle)


def stop_watcher():
    signal = _kernel.OpenEventW(2, False, _name("Stop"))
    if not signal:
        return
    _kernel.SetEvent(signal)
    _kernel.CloseHandle(signal)
    for _ in range(150):
        mutex = _kernel.OpenMutexW(0x100000, False, _name("Mutex"))
        if not mutex:
            return
        _kernel.CloseHandle(mutex)
        time.sleep(0.1)
    raise RuntimeError("자동 변환 종료가 지연되고 있습니다. 잠시 후 다시 시도해 주세요.")


def start_watcher(script):
    subprocess.Popen([str(python_windowless()), str(script), "--watch"], creationflags=subprocess.CREATE_NO_WINDOW)
    for _ in range(150):
        signal = _kernel.OpenEventW(0x100000, False, _name("Ready"))
        if signal:
            ready = _kernel.WaitForSingleObject(signal, 0) == 0
            _kernel.CloseHandle(signal)
            if ready:
                return
        time.sleep(0.1)
    raise RuntimeError(f"자동 변환 시작 실패. {INSTALL_DIR / 'python.log'} 파일을 확인해 주세요.")


def local_drives():
    drives = _kernel.GetLogicalDrives()
    return [f"{chr(65 + i)}:\\" for i in range(26) if drives & (1 << i) and _kernel.GetDriveTypeW(f"{chr(65 + i)}:\\") in (2, 3)]


def install(project):
    project = Path(project).resolve()
    stop_watcher()
    legacy = INSTALL_DIR / "PixelTxtConverter.exe"
    # Stop the app-specific 2.x watcher, not other processes or Python interpreters.
    if legacy.exists():
        subprocess.run([str(legacy), "--stop-watcher"], check=True, timeout=20, creationflags=subprocess.CREATE_NO_WINDOW)
    SOURCE_DIR.mkdir(parents=True, exist_ok=True)
    if project != SOURCE_DIR.resolve():
        shutil.copy2(project / "main.py", SOURCE_DIR / "main.py")
        shutil.copy2(project / "requirements.txt", SOURCE_DIR / "requirements.txt")
        (SOURCE_DIR / "pixeltxt").mkdir(exist_ok=True)
        for file in (project / "pixeltxt").glob("*.py"):
            shutil.copy2(file, SOURCE_DIR / "pixeltxt" / file.name)
    script = SOURCE_DIR / "main.py"
    with winreg.CreateKey(winreg.HKEY_CURRENT_USER, CLASSES) as root:
        register(root, script)
        if not registered(root, script):
            raise RuntimeError("탐색기 메뉴 연결을 확인하지 못했습니다.")
    _set(winreg.HKEY_CURRENT_USER, RUN, {"PixelTxtConverter": command(script, "--watch", False)})
    _set(winreg.HKEY_CURRENT_USER, UNINSTALL, {
        "DisplayName": "Pixel TXT Converter (Python)", "DisplayVersion": __version__,
        "Publisher": "Ragim-Games", "InstallLocation": str(SOURCE_DIR),
        "DisplayIcon": str(python_windowless()), "UninstallString": command(script, "--uninstall", False),
        "NoModify": 1, "NoRepair": 1,
    })
    ctypes.windll.shell32.SHChangeNotify(0x08000000, 0, None, None)
    start_watcher(script)
    # Migration succeeded: retire only the exact legacy files owned by this app.
    for name in ("PixelTxtConverter.exe", "WebP-LICENSE.txt", "WebP-PATENTS.txt"):
        try:
            (INSTALL_DIR / name).unlink(missing_ok=True)
        except PermissionError:
            pass  # An old setup window may still have its EXE open.


def uninstall():
    stop_watcher()
    script = SOURCE_DIR / "main.py"
    with winreg.CreateKey(winreg.HKEY_CURRENT_USER, CLASSES) as root:
        unregister(root, script)
    if _value(winreg.HKEY_CURRENT_USER, RUN, "PixelTxtConverter") == command(script, "--watch", False):
        _delete_value(winreg.HKEY_CURRENT_USER, RUN, "PixelTxtConverter")
    if _value(winreg.HKEY_CURRENT_USER, UNINSTALL, "InstallLocation") == str(SOURCE_DIR):
        _delete_tree(winreg.HKEY_CURRENT_USER, UNINSTALL)
    ctypes.windll.shell32.SHChangeNotify(0x08000000, 0, None, None)
