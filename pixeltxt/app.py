import argparse
import logging
from logging.handlers import RotatingFileHandler
import os
from pathlib import Path
import subprocess
import sys

from . import __version__
from .core import to_image, to_text


def _message(text, error=False):
    import tkinter as tk
    from tkinter import messagebox
    root = tk.Tk()
    root.withdraw()
    (messagebox.showerror if error else messagebox.showinfo)("Pixel TXT Converter", str(text), parent=root)
    root.destroy()


def _setup():
    import tkinter as tk
    from tkinter import ttk, messagebox
    from .windows import install, uninstall
    root = tk.Tk()
    root.title(f"Pixel TXT Converter {__version__} · Python")
    root.resizable(False, False)
    frame = ttk.Frame(root, padding=24)
    frame.pack(fill="both", expand=True)
    ttk.Label(frame, text="이미지 ↔ RGB TXT / TXP", font=("Malgun Gothic", 16, "bold")).pack(anchor="w")
    ttk.Label(frame, text="이미지 우클릭 → TXT Convert → RGB TXT 저장\n.txt를 .txp로 변경 → 같은 폴더에 PNG 자동 생성\n\n설치하면 Python 자동 변환 프로그램이 로그인 시 실행됩니다.\n로컬 드라이브의 새 TXP를 감지하며 알림 영역에서 종료합니다.\nWindows 11 우클릭 메뉴는 ‘더 많은 옵션’에 표시됩니다.", padding=(0, 18)).pack(anchor="w")
    state = tk.StringVar(value="Python 소스 버전 · PNG/JPG/WebP 등 지원")
    ttk.Label(frame, textvariable=state).pack(anchor="w", pady=(0, 12))

    def action(remove=False):
        if remove and not messagebox.askyesno("연결 해제", "자동 변환과 파일 연결을 해제할까요? 원본 파일은 유지됩니다.", parent=root):
            return
        try:
            if remove:
                uninstall()
            else:
                install(Path(__file__).resolve().parent.parent)
            state.set("연결을 해제했습니다." if remove else "설치 완료 · TXP 자동 변환이 실행 중입니다.")
        except Exception as error:
            messagebox.showerror("처리 실패", str(error), parent=root)

    buttons = ttk.Frame(frame)
    buttons.pack(fill="x")
    ttk.Button(buttons, text="설치 / 자동 변환 시작", command=action).pack(side="left", padx=(0, 12))
    ttk.Button(buttons, text="자동 변환 / 연결 해제", command=lambda: action(True)).pack(side="left")
    root.mainloop()


def main(argv=None):
    parser = argparse.ArgumentParser(description="이미지 ↔ RGB TXT / TXP 변환기 (Python 소스)")
    actions = parser.add_mutually_exclusive_group()
    actions.add_argument("--to-txt", metavar="IMAGE")
    actions.add_argument("--to-image", metavar="TXT_OR_TXP")
    actions.add_argument("--install", action="store_true")
    actions.add_argument("--uninstall", action="store_true")
    actions.add_argument("--watch", action="store_true")
    actions.add_argument("--stop-watcher", action="store_true")
    parser.add_argument("--no-open", action="store_true", help="변환 결과를 저장만 하고 열지 않습니다")
    args = parser.parse_args(argv)
    log_dir = Path(os.environ.get("LOCALAPPDATA", str(Path.home()))) / "PixelTxtConverter"
    # File-only conversions and tests do not write outside the selected output directory.
    if args.watch:
        log_dir.mkdir(parents=True, exist_ok=True)
        logging.basicConfig(level=logging.INFO, handlers=[RotatingFileHandler(log_dir / "python.log", maxBytes=1_000_000, backupCount=1, encoding="utf-8")])
    try:
        if args.to_txt or args.to_image:
            output = to_text(args.to_txt) if args.to_txt else to_image(args.to_image)
            if not args.no_open and os.name == "nt":
                if args.to_txt:
                    subprocess.Popen([str(Path(os.environ["WINDIR"]) / "System32" / "notepad.exe"), str(output)])
                else:
                    os.startfile(output)
            if sys.stdout:
                print(output)
        elif args.watch:
            from .tray import run
            run()
        elif args.install or args.uninstall or args.stop_watcher:
            from .windows import install, uninstall, stop_watcher
            if args.install:
                install(Path(__file__).resolve().parent.parent)
            elif args.stop_watcher:
                stop_watcher()
            else:
                from tkinter import messagebox
                if messagebox.askyesno("Pixel TXT Converter", "자동 변환과 파일 연결을 해제할까요? 원본 파일은 유지됩니다."):
                    uninstall()
        else:
            _setup()
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
