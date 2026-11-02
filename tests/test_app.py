from contextlib import redirect_stderr, redirect_stdout
import io
import os
from pathlib import Path
from types import SimpleNamespace
import unittest
from unittest.mock import Mock, patch

from pixeltxt.app import main


class ConsoleTests(unittest.TestCase):
    def test_no_arguments_prints_cmd_setup_help(self):
        output = io.StringIO()
        with redirect_stdout(output), patch("pixeltxt.app._message") as dialog:
            self.assertEqual(main([]), 0)
        self.assertIn("install.cmd", output.getvalue())
        self.assertIn("--to-txt", output.getvalue())
        dialog.assert_not_called()

    @unittest.skipUnless(os.name == "nt", "Windows console integration")
    def test_install_and_uninstall_use_console_without_dialogs(self):
        shell = SimpleNamespace(
            SOURCE_DIR=Path("installed"), install=Mock(), uninstall=Mock(),
            start_watcher=Mock(), stop_watcher=Mock(),
        )
        output = io.StringIO()
        with patch.dict("sys.modules", {"pixeltxt.windows": shell}), redirect_stdout(output), \
                patch("tkinter.Tk", side_effect=AssertionError("Setup must not open a GUI")):
            self.assertEqual(main(["--install"]), 0)
            self.assertEqual(main(["--uninstall"]), 0)
        shell.install.assert_called_once_with(Path(__file__).resolve().parent.parent)
        shell.uninstall.assert_called_once_with()
        self.assertIn("Installation complete", output.getvalue())
        self.assertIn("Explorer integration removed", output.getvalue())

    @unittest.skipUnless(os.name == "nt", "Windows console integration")
    def test_install_failure_is_reported_with_nonzero_exit(self):
        shell = SimpleNamespace(
            SOURCE_DIR=Path("installed"), install=Mock(side_effect=RuntimeError("Setup failed")),
            uninstall=Mock(), start_watcher=Mock(), stop_watcher=Mock(),
        )
        output = io.StringIO()
        with patch.dict("sys.modules", {"pixeltxt.windows": shell}), redirect_stderr(output), \
                patch("pixeltxt.app.logging.exception"), patch("pixeltxt.app._message") as dialog:
            self.assertEqual(main(["--install"]), 1)
        self.assertIn("Setup failed", output.getvalue())
        dialog.assert_not_called()
