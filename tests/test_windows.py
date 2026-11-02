import os
from pathlib import Path
import unittest
import uuid


@unittest.skipUnless(os.name == "nt" and os.environ.get("PIXELTXT_REGISTRY_TEST") == "1", "Opt-in isolated Windows registry test")
class WindowsTests(unittest.TestCase):
    def test_registration_restore_and_missing_menu_repair(self):
        import winreg
        from pixeltxt import windows as shell
        path = rf"Software\PixelTxtPython.Tests\{uuid.uuid4().hex}"
        script = Path("C:/\u2603 folder/python/main.py")
        try:
            with winreg.CreateKey(winreg.HKEY_CURRENT_USER, path) as root:
                shell._set(root, ".txp", {"": "Original.TxpApp"})
                shell.register(root, script)
                shell.register(root, script)
                self.assertTrue(shell.registered(root, script))
                self.assertIn('"%1"', shell.command(script, "--to-txt"))
                self.assertIn("main.py", shell.command(script, "--to-txt"))
                self.assertIn("pythonw.exe", shell.command(script, "--to-txt"))
                uninstall_command = shell.command(script, "--uninstall", False, windowless=False)
                self.assertIn("python.exe", uninstall_command)
                self.assertNotIn("pythonw.exe", uninstall_command)
                self.assertNotIn('"%1"', uninstall_command)
                missing = r"SystemFileAssociations\.webp\shell\PixelTxtConverter.ToTxt"
                shell._delete_tree(root, missing)
                self.assertFalse(shell.registered(root, script))
                shell.register(root, script)
                self.assertTrue(shell.registered(root, script))
                shell._set(root, r"SystemFileAssociations\.png\shell\Unrelated", {"": "KEEP"})
                shell.unregister(root, Path(r"C:\wrong\main.py"))
                self.assertTrue(shell.registered(root, script))
                shell.unregister(root, script)
                self.assertFalse(shell.registered(root, script))
                self.assertEqual(shell._value(root, ".txp"), "Original.TxpApp")
                self.assertEqual(shell._value(root, r"SystemFileAssociations\.png\shell\Unrelated"), "KEEP")
        finally:
            shell._delete_tree(winreg.HKEY_CURRENT_USER, path)
