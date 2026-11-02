"""Pure conversion functions, independent of Explorer and the background watcher."""
from contextlib import contextmanager
from pathlib import Path
import re

from PIL import Image

MAX_PIXELS = 4_194_304
MAX_BYTES = 64 * 1024 * 1024
MAX_SIDE = 16_384
CHANNELS = re.compile(r"[0-9]{1,3},[0-9]{1,3},[0-9]{1,3}\Z")
HEADER = re.compile(r"PIXELTXT/1/([1-9][0-9]*)/([1-9][0-9]*)\Z")


def validate_size(width, height):
    if not (1 <= width <= MAX_SIDE and 1 <= height <= MAX_SIDE and width * height <= MAX_PIXELS):
        raise ValueError("Images support at most 4,194,304 pixels and 16,384 pixels per side.")


def validate_input(path):
    path = Path(path)
    if path.stat().st_size > MAX_BYTES:
        raise ValueError("Input files must not exceed 64 MiB.")
    return path


@contextmanager
def new_output(desired):
    """Reserve with exclusive creation; never overwrite even during concurrent conversions."""
    desired = Path(desired)
    for suffix in range(1, 10000):
        actual = desired if suffix == 1 else desired.with_name(f"{desired.stem} ({suffix}){desired.suffix}")
        try:
            stream = actual.open("xb")
            break
        except FileExistsError:
            continue
    else:
        raise FileExistsError("Too many output files already use this name.")
    try:
        with stream:
            yield actual, stream
    except BaseException:
        actual.unlink(missing_ok=True)  # Only the file reserved by this call.
        raise


def to_text(source, output=None):
    source = validate_input(source)
    output = Path(output) if output is not None else Path(str(source) + ".txt")
    with Image.open(source, formats=["PNG", "JPEG", "BMP", "GIF", "TIFF", "ICO", "WEBP"]) as original:
        validate_size(*original.size)
        if original.format == "WEBP" and getattr(original, "is_animated", False):
            raise ValueError("Animated WebP is not supported. Use a still WebP image.")
        original.seek(0)
        with original.convert("RGBA") as rgba, Image.new("RGBA", original.size, "white") as white:
            with Image.alpha_composite(white, rgba).convert("RGB") as image:
                width, height = image.size
                pixels = image.load()
                with new_output(output) as (actual, stream):
                    stream.write(f"PIXELTXT/1/{width}/{height}\n".encode("ascii"))
                    for y in range(height):
                        row = "/".join(",".join(map(str, pixels[x, y])) for x in range(width))
                        stream.write((("/" if y else "") + row).encode("ascii"))
    return actual


def _tokens(stream):
    carry = ""
    while chunk := stream.read(8192):
        fields = (carry + chunk).split("/")
        carry = fields.pop()
        for field in fields:
            if len(field) > 32:
                raise ValueError("An RGB entry is too long or a separator is missing.")
            yield field.strip()
        if len(carry) > 32:
            raise ValueError("An RGB entry is too long or a separator is missing.")
    yield carry.strip()


def to_image(source, output=None):
    source = validate_input(source)
    if output is None:
        output = source.with_suffix(".png") if source.suffix.lower() == ".txp" else source.with_name(source.stem + ".restored.png")
    with source.open("r", encoding="utf-8-sig", newline=None) as stream:
        line = stream.readline(101)
        match = HEADER.fullmatch(line.strip())
        if not line.endswith("\n") or not match:
            raise ValueError("The first line must use the format PIXELTXT/1/width/height.")
        width, height = map(int, match.groups())
        validate_size(width, height)
        count = width * height
        data = bytearray(count * 3)
        seen = 0
        for seen, token in enumerate(_tokens(stream), start=1):
            if seen > count:
                raise ValueError("Too many pixels or an unexpected trailing / separator.")
            if not CHANNELS.fullmatch(token):
                raise ValueError(f"Pixel {seen} must contain R,G,B integers between 0 and 255.")
            channels = tuple(map(int, token.split(",")))
            if max(channels) > 255:
                raise ValueError(f"An RGB value in pixel {seen} exceeds 255.")
            data[(seen - 1) * 3:seen * 3] = bytes(channels)
        if seen != count:
            raise ValueError("The pixel count is less than width times height.")
    with Image.frombytes("RGB", (width, height), bytes(data)) as image:
        with new_output(output) as (actual, stream):
            image.save(stream, format="PNG")
    return actual
