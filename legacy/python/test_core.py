import tempfile
import unittest
from pathlib import Path
from core import parse_app_id, download_cmd, install_cmd, device_list_cmd


class CoreTest(unittest.TestCase):
    def test_raw_id(self):
        self.assertEqual(parse_app_id(" 123456789 "), "123456789")

    def test_app_store_link(self):
        self.assertEqual(parse_app_id("https://apps.apple.com/ru/app/example/id123456789?l=ru"), "123456789")

    def test_reject_injection(self):
        for raw in ("1; rm -rf /", "hello", "https://example.org/id123456789", "1"):
            with self.assertRaises(ValueError):
                parse_app_id(raw)

    def test_download_command(self):
        cmd = download_cmd("/usr/local/bin/ipatool", "123456789", Path("/tmp/123456789.ipa"))
        self.assertEqual(cmd, ["/usr/local/bin/ipatool", "download", "--app-id", "123456789", "--output", str(Path("/tmp/123456789.ipa"))])

    def test_install_command(self):
        with tempfile.TemporaryDirectory() as td:
            path = Path(td) / "a.ipa"
            path.touch()
            cmd = install_cmd(path, "00008110-00A123456B12301E")
            self.assertIn("--udid", cmd)
            self.assertEqual(cmd[-1], str(path))
            with self.assertRaises(ValueError):
                install_cmd(path, "hi; rm")

    def test_device_list(self):
        self.assertEqual(device_list_cmd()[-2:], ["usbmux", "list"])


if __name__ == "__main__":
    unittest.main()
