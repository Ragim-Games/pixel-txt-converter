from pathlib import Path
import tempfile
import unittest
from PIL import Image
from pixeltxt.core import to_image, to_text, validate_size, MAX_PIXELS

TEXT = "PIXELTXT/1/2/2\n255,0,0/0,255,0/0,0,255/255,255,255"
COLORS = [(255, 0, 0), (0, 255, 0), (0, 0, 255), (255, 255, 255)]
FIXTURES = Path(__file__).parent / "fixtures"


class CoreTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)

    def test_exact_pixel_order_and_roundtrip(self):
        source = self.root / "한글 공백 & 사진.png"
        with Image.new("RGB", (2, 2)) as image:
            image.putdata(COLORS)
            image.save(source)
        text = to_text(source)
        self.assertEqual(text.read_text(), TEXT)
        with Image.open(to_image(text)) as image:
            self.assertEqual(image.size, (2, 2))
            self.assertEqual(image.tobytes(), bytes(channel for color in COLORS for channel in color))

    def test_txp_name_and_existing_output_preserved(self):
        source = self.root / "그림.TXP"
        source.write_text(TEXT)
        expected = source.with_suffix(".png")
        expected.write_bytes(b"keep me")
        output = to_image(source)
        self.assertEqual(output.name, "그림 (2).png")
        self.assertEqual(expected.read_bytes(), b"keep me")
        self.assertEqual(source.read_text(), TEXT)

    def test_all_non_square_pixels_across_chunks(self):
        source = self.root / "non-square.png"
        colors = [(x % 256, y % 256, (x + y) % 256) for y in range(83) for x in range(137)]
        with Image.new("RGB", (137, 83)) as image:
            image.putdata(colors)
            image.save(source)
        with Image.open(to_image(to_text(source))) as image:
            self.assertEqual(image.tobytes(), bytes(channel for color in colors for channel in color))

    def test_webp_lossless_alpha_and_lossy(self):
        self.assertEqual(to_text(FIXTURES / "colors.webp", self.root / "colors.txt").read_text(), TEXT)
        alpha = to_text(FIXTURES / "alpha.webp", self.root / "alpha.txt")
        self.assertEqual(alpha.read_text(), "PIXELTXT/1/3/1\n255,255,255/255,127,127/11,22,33")
        with Image.open(to_image(to_text(FIXTURES / "lossy.webp", self.root / "lossy.txt"))) as image:
            self.assertEqual(image.size, (8, 4))
            red, green, blue = image.getpixel((0, 0))
            self.assertGreater(red, 240)
            self.assertLess(max(green, blue), 10)

    def test_bom_crlf_whitespace(self):
        source = self.root / "bom.txt"
        source.write_text("PIXELTXT/1/2/1\r\n 0,1,2 / 253,254,255\r\n", encoding="utf-8-sig")
        with Image.open(to_image(source)) as image:
            self.assertEqual(image.tobytes(), bytes([0, 1, 2, 253, 254, 255]))

    def test_malformed_inputs_leave_no_output(self):
        invalid = ["", "255,0,0", "PIXELTXT/2/1/1\n1,2,3", "PIXELTXT/1/0/1\n1,2,3",
            "PIXELTXT/1/9999999999/1\n1,2,3", "PIXELTXT/1/2049/2049\n1,2,3",
            "PIXELTXT/1/1/1\n256,0,0", "PIXELTXT/1/1/1\n-1,0,0", "PIXELTXT/1/1/1\n1.5,0,0",
            "PIXELTXT/1/1/1\n1,2", "PIXELTXT/1/1/1\n1,2,3/", "PIXELTXT/1/2/1\n1,2,3",
            "PIXELTXT/1/1/1\n1,2,3/4,5,6", "PIXELTXT/1/1/1\n" + "0" * 100]
        for i, content in enumerate(invalid):
            with self.subTest(content=content):
                source = self.root / f"bad-{i}.txp"
                source.write_text(content)
                with self.assertRaises(ValueError):
                    to_image(source)
                self.assertFalse(source.with_suffix(".png").exists())

    def test_size_limits(self):
        validate_size(2048, 2048)
        for size in [(0, 1), (MAX_PIXELS, 1), (2049, 2049)]:
            with self.assertRaises(ValueError):
                validate_size(*size)
