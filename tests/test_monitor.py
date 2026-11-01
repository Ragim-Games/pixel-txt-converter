from pathlib import Path
import tempfile
import time
import unittest
from PIL import Image
from pixeltxt.monitor import TxpMonitor

TEXT = "PIXELTXT/1/2/2\n255,0,0/0,255,0/0,0,255/255,255,255"


class MonitorTests(unittest.TestCase):
    def wait_for(self, condition, timeout=10):
        deadline = time.monotonic() + timeout
        while not condition() and time.monotonic() < deadline:
            time.sleep(0.05)
        self.assertTrue(condition())

    def test_actual_rename_creation_retry_and_shutdown(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp).resolve()
            nested = root / "한글 폴더"
            nested.mkdir()
            (root / "old.txp").write_text(TEXT)
            results, errors = {}, {}
            def completed(source, output, error):
                (errors if error else results)[source] = error or output
            with TxpMonitor(completed) as monitor:
                monitor.watch(root)
                txt = nested / "그림.txt"
                txp = txt.with_suffix(".TXP")
                txt.write_text(TEXT)
                txt.rename(txp)
                self.wait_for(lambda: txp in results)
                self.assertEqual(results[txp], txp.with_suffix(".png"))
                with Image.open(results[txp]) as image:
                    self.assertEqual(image.getpixel((0, 1)), (0, 0, 255))
                self.assertTrue(txp.exists())
                self.assertFalse((root / "old.png").exists())
                collision = root / "same.txp"
                (root / "same.png").write_bytes(b"KEEP")
                collision.write_text(TEXT)
                self.wait_for(lambda: collision in results)
                self.assertEqual(results[collision].name, "same (2).png")
                self.assertEqual((root / "same.png").read_bytes(), b"KEEP")
                delayed = root / "delayed.txp"
                delayed.write_text("PIXELTXT/1/2/2\n")
                time.sleep(0.8)
                delayed.write_text(TEXT)
                self.wait_for(lambda: delayed in results)
                bad = root / "bad.txp"
                bad.write_text("not pixel text")
                self.wait_for(lambda: bad in errors)
                self.assertFalse(bad.with_suffix(".png").exists())
                (root / "ignored.txp.txt").write_text(TEXT)
                time.sleep(0.8)
                self.assertEqual(len(results), 3)
                self.assertEqual(len(list(nested.glob("*.png"))), 1)
            (root / "stopped.txp").write_text(TEXT)
            time.sleep(0.7)
            self.assertFalse((root / "stopped.png").exists())
