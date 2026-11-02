import argparse
import logging
from logging.handlers import RotatingFileHandler
import os
from pathlib import Path
import subprocess
import sys

from . import __version__


def _message(text, error=False):
    import tkinter as tk
    from tkinter import messagebox
    root = tk.Tk()
    root.withdraw()
    (messagebox.showerror if error else messagebox.showinfo)("Pixel TXT Converter", str(text), parent=root)
    root.destroy()


def main(argv=None):
    parser = argparse.ArgumentParser(
        description="Convert images to RGB TXT and restore TXT/TXP files to PNG.",
        epilog="To install on Windows, run install.cmd from Command Prompt (CMD).",
    )
    parser.add_argument("--version", action="version", version=f"%(prog)s {__version__}")
    actions = parser.add_mutually_exclusive_group()
    actions.add_argument("--to-txt", metavar="IMAGE", help="save an image as RGB text")
    actions.add_argument("--to-image", metavar="TXT_OR_TXP", help="restore RGB text to PNG")
    actions.add_argument("--install", action="store_true", help="install Explorer integration and start automatic conversion")
    actions.add_argument("--uninstall", action="store_true", help="stop automatic conversion and remove Explorer integration")
    actions.add_argument("--watch", action="store_true", help="run the background watcher with a tray icon")
    actions.add_argument("--start-watcher", action="store_true", help="start the installed background watcher")
    actions.add_argument("--stop-watcher", action="store_true", help="stop automatic conversion for this login")
    parser.add_argument("--no-open", action="store_true", help="save the converted file without opening it")
    args = parser.parse_args(argv)
    log_dir = Path(os.environ.get("LOCALAPPDATA", str(Path.home()))) / "PixelTxtConverter"
    # File-only conversions and tests do not write outside the selected output directory.
    if args.watch:
        log_dir.mkdir(parents=True, exist_ok=True)
        logging.basicConfig(level=logging.INFO, handlers=[RotatingFileHandler(log_dir / "python.log", maxBytes=1_000_000, backupCount=1, encoding="utf-8")])
    try:
        if args.to_txt or args.to_image:
            from .core import to_image, to_text
            output = to_text(args.to_txt) if args.to_txt else to_image(args.to_image)
            if not args.no_open and os.name == "nt":
                if args.to_txt:
                    subprocess.Popen([str(Path(os.environ["WINDIR"]) / "System32" / "notepad.exe"), str(output)])
                else:
                    os.startfile(output)
            if sys.stdout:
                print(output)
        elif args.watch or args.install or args.uninstall or args.start_watcher or args.stop_watcher:
            if os.name != "nt":
                raise RuntimeError("Explorer integration and automatic conversion require Windows 10 or 11.")
            from .windows import SOURCE_DIR, install, uninstall, start_watcher, stop_watcher
            if args.install:
                install(Path(__file__).resolve().parent.parent)
                if sys.stdout:
                    print("Installation complete. TXP automatic conversion is running.")
            elif args.watch:
                from .tray import run
                run()
            elif args.start_watcher:
                script = SOURCE_DIR / "main.py"
                if not script.is_file():
                    raise RuntimeError("Run install.cmd before starting automatic conversion.")
                start_watcher(script)
                if sys.stdout:
                    print("TXP automatic conversion is running.")
            elif args.stop_watcher:
                stop_watcher()
                if sys.stdout:
                    print("Automatic conversion stopped for this login.")
            else:
                uninstall()
                if sys.stdout:
                    print("Automatic conversion, startup registration, and Explorer integration removed.")
                    print("Installed source files and your images, TXT files, and TXP files were kept.")
        else:
            parser.print_help()
    except Exception as error:
        logging.exception("Pixel TXT failed")
        if args.watch:
            return 1
        if args.no_open or sys.stderr:
            if sys.stderr:
                print(str(error), file=sys.stderr)
        else:
            _message(error, error=True)
        return 1
    return 0
