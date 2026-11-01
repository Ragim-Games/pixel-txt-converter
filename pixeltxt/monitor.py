"""Watch local file events. No initial scan and no modification of source files."""
from pathlib import Path
import logging
import threading
import time

from watchdog.events import FileSystemEventHandler
from watchdog.observers import Observer

from .core import to_image

LOG = logging.getLogger(__name__)


class TxpMonitor(FileSystemEventHandler):
    def __init__(self, completed=None):
        self.completed = completed or (lambda source, output, error: None)
        self._pending = {}
        self._lock = threading.Lock()
        self._stop = threading.Event()
        self._observer = Observer()
        self._directories = set()
        self._worker = threading.Thread(target=self._process, daemon=True, name="txp-converter")
        self._observer.start()
        self._worker.start()

    def watch(self, directory):
        directory = str(Path(directory).resolve())
        if directory not in self._directories:
            self._observer.schedule(self, directory, recursive=True)
            self._directories.add(directory)

    def on_created(self, event):
        if not event.is_directory:
            self.queue(event.src_path)

    def on_moved(self, event):
        if not event.is_directory:
            self.queue(event.dest_path)

    def queue(self, path):
        path = Path(path)
        if path.suffix.lower() != ".txp":
            return
        with self._lock:
            if not self._stop.is_set():
                self._pending.setdefault(path, (time.monotonic() + 0.6, 0))

    def _process(self):
        while not self._stop.wait(0.1):
            with self._lock:
                item = next(((p, state) for p, state in self._pending.items() if state[0] <= time.monotonic()), None)
            if item is None:
                continue
            path, (_, attempts) = item
            output, error = None, None
            try:
                output = to_image(path)
            except (OSError, ValueError) as caught:
                if attempts < 9:
                    with self._lock:
                        self._pending[path] = (time.monotonic() + 0.5, attempts + 1)
                    continue
                error = caught
            except Exception as caught:
                LOG.exception("TXP conversion failed: %s", path)
                error = caught
            with self._lock:
                self._pending.pop(path, None)
            try:
                self.completed(path, output, error)
            except Exception:
                LOG.exception("Notification failed")

    def close(self):
        self._stop.set()
        self._observer.stop()
        self._observer.join()
        self._worker.join()

    def __enter__(self):
        return self

    def __exit__(self, *args):
        self.close()
