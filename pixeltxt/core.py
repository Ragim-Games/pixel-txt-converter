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
        raise ValueError("이미지는 최대 4,194,304픽셀, 한 변은 16,384픽셀까지 지원합니다.")


def validate_input(path):
    path = Path(path)
    if path.stat().st_size > MAX_BYTES:
        raise ValueError("입력 파일은 최대 64 MiB까지 지원합니다.")
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
        raise FileExistsError("같은 이름의 출력 파일이 너무 많습니다.")
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
            raise ValueError("움직이는 WebP는 지원하지 않습니다. 정지 이미지 WebP를 사용해 주세요.")
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
                raise ValueError("RGB 항목이 너무 길거나 구분자가 없습니다.")
            yield field.strip()
        if len(carry) > 32:
            raise ValueError("RGB 항목이 너무 길거나 구분자가 없습니다.")
    yield carry.strip()


def to_image(source, output=None):
    source = validate_input(source)
    if output is None:
        output = source.with_suffix(".png") if source.suffix.lower() == ".txp" else source.with_name(source.stem + ".restored.png")
    with source.open("r", encoding="utf-8-sig", newline=None) as stream:
        line = stream.readline(101)
        match = HEADER.fullmatch(line.strip())
        if not line.endswith("\n") or not match:
            raise ValueError("첫 줄은 PIXELTXT/1/가로/세로 형식이어야 합니다.")
        width, height = map(int, match.groups())
        validate_size(width, height)
        count = width * height
        data = bytearray(count * 3)
        seen = 0
        for seen, token in enumerate(_tokens(stream), start=1):
            if seen > count:
                raise ValueError("픽셀 수가 너무 많거나 끝에 불필요한 /가 있습니다.")
            if not CHANNELS.fullmatch(token):
                raise ValueError(f"{seen}번째 픽셀은 0~255 범위의 R,G,B 값이어야 합니다.")
            channels = tuple(map(int, token.split(",")))
            if max(channels) > 255:
                raise ValueError(f"{seen}번째 픽셀의 RGB 값이 255를 초과합니다.")
            data[(seen - 1) * 3:seen * 3] = bytes(channels)
        if seen != count:
            raise ValueError("픽셀 수가 가로 × 세로보다 적습니다.")
    with Image.frombytes("RGB", (width, height), bytes(data)) as image:
        with new_output(output) as (actual, stream):
            image.save(stream, format="PNG")
    return actual
