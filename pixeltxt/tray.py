"""Windows background watcher with an explicit tray exit command."""
import logging
import threading
import time

from PIL import Image, ImageDraw
import pystray

from .monitor import TxpMonitor
from .windows import WatcherLease, local_drives

LOG = logging.getLogger(__name__)


def run():
    lease = WatcherLease()
    if lease.existing:
        lease.close()
        return
    stop = threading.Event()
    icon_image = Image.new("RGB", (64, 64), "#6655d7")
    draw = ImageDraw.Draw(icon_image)
    for y in (12, 28, 44):
        for x in (12, 28, 44):
            draw.rectangle((x, y, x + 10, y + 10), fill="white")
    icon = pystray.Icon("PixelTxtPython", icon_image, "Pixel TXT · Python 자동 변환",
        pystray.Menu(pystray.MenuItem("이번 로그인 동안 자동 변환 종료", lambda: icon.stop())))
    last_notice = 0.0

    def completed(source, output, error):
        nonlocal last_notice
        if error:
            LOG.warning("Conversion failed for %s: %s", source, error)
        if time.monotonic() - last_notice > 3:
            last_notice = time.monotonic()
            icon.notify(str(error) if error else output.name, "TXP 변환 실패" if error else "TXP → PNG 완료")

    monitor = TxpMonitor(completed)

    def refresh():
        watched = 0
        for drive in local_drives():
            try:
                monitor.watch(drive)
                watched += 1
            except OSError:
                LOG.warning("Cannot watch %s", drive, exc_info=True)
        return watched

    def worker():
        last_refresh = time.monotonic()
        while not stop.wait(0.5):
            if lease.stopping():
                icon.stop()
                return
            if time.monotonic() - last_refresh > 10:
                refresh()
                last_refresh = time.monotonic()

    def setup(_icon):
        _icon.visible = True
        lease.mark_ready()

    thread = threading.Thread(target=worker, daemon=True)
    try:
        if not refresh():
            raise RuntimeError("감지할 수 있는 로컬 드라이브가 없습니다.")
        thread.start()
        icon.run(setup=setup)
    finally:
        stop.set()
        if thread.is_alive():
            thread.join()
        monitor.close()
        lease.close()
