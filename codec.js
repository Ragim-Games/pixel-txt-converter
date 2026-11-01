(function (root) {
  'use strict';
  const MAX_PIXELS = 4194304;
  const MAX_SIDE = 16384;
  const MAX_TEXT_BYTES = 64 * 1024 * 1024;
  const CHUNK = 8192;
  const pause = () => new Promise(resolve => setTimeout(resolve, 0));

  function validateSize(width, height) {
    if (!Number.isSafeInteger(width) || !Number.isSafeInteger(height) || width < 1 || height < 1) {
      throw new Error('가로와 세로는 1 이상의 정수여야 합니다.');
    }
    if (width > MAX_SIDE || height > MAX_SIDE || width * height > MAX_PIXELS) {
      throw new Error('이미지는 최대 4,194,304픽셀, 한 변은 최대 16,384픽셀까지 지원합니다.');
    }
  }

  // Canvas RGBA is traversed left-to-right, then top-to-bottom.
  // Flatten alpha onto white so RGB-only files have a defined appearance.
  async function encode({ width, height, data }, onProgress = () => {}) {
    validateSize(width, height);
    const count = width * height;
    if (!(data instanceof Uint8ClampedArray || data instanceof Uint8Array) || data.length !== count * 4) {
      throw new Error('이미지의 크기와 RGBA 데이터 길이가 일치하지 않습니다.');
    }
    const parts = [`PIXELTXT/1/${width}/${height}\n`];
    for (let start = 0; start < count; start += CHUNK) {
      const end = Math.min(start + CHUNK, count);
      const pixels = [];
      for (let p = start; p < end; p++) {
        const i = p * 4;
        const alpha = data[i + 3] / 255;
        const rgb = [0, 1, 2].map(c => Math.round(data[i + c] * alpha + 255 * (1 - alpha)));
        pixels.push(rgb.join(','));
      }
      parts.push((start ? '/' : '') + pixels.join('/'));
      onProgress(end / count);
      await pause();
    }
    return parts;
  }

  async function decode(input, onProgress = () => {}) {
    if (typeof input !== 'string' || input.length > MAX_TEXT_BYTES) {
      throw new Error('TXT 파일은 최대 64 MiB까지 지원합니다.');
    }
    const text = input.replace(/^\uFEFF/, '').trim();
    const newline = text.indexOf('\n');
    const header = text.slice(0, newline).trim();
    const match = /^PIXELTXT\/1\/([1-9]\d*)\/([1-9]\d*)$/.exec(header);
    if (newline < 0 || !match) {
      throw new Error('TXT 첫 줄은 PIXELTXT/1/가로/세로 형식이어야 합니다.');
    }
    const width = Number(match[1]);
    const height = Number(match[2]);
    validateSize(width, height);
    const count = width * height;
    const data = new Uint8ClampedArray(count * 4);
    let cursor = newline + 1;
    for (let pixel = 0; pixel < count; pixel++) {
      const slash = text.indexOf('/', cursor);
      const end = slash < 0 ? text.length : slash;
      // Check length before slicing untrusted input.
      if (end - cursor > 32) throw new Error(`${pixel + 1}번째 픽셀의 RGB 형식이 올바르지 않습니다.`);
      const token = text.slice(cursor, end).trim();
      if (!/^\d{1,3},\d{1,3},\d{1,3}$/.test(token)) {
        throw new Error(`${pixel + 1}번째 픽셀은 0~255 범위의 R,G,B 값이어야 합니다.`);
      }
      const channels = token.split(',').map(Number);
      if (channels.some(c => c > 255)) throw new Error(`${pixel + 1}번째 픽셀의 RGB 값이 255를 초과합니다.`);
      data.set([...channels, 255], pixel * 4);
      if (pixel === count - 1) {
        if (slash >= 0) throw new Error('가로 × 세로보다 픽셀 값이 많거나 끝에 불필요한 /가 있습니다.');
      } else if (slash < 0) {
        throw new Error('가로 × 세로에 필요한 픽셀 값이 부족합니다.');
      }
      cursor = end + 1;
      if ((pixel + 1) % CHUNK === 0) {
        onProgress((pixel + 1) / count);
        await pause();
      }
    }
    onProgress(1);
    return { width, height, data };
  }

  const api = { encode, decode, validateSize, MAX_PIXELS, MAX_SIDE, MAX_TEXT_BYTES };
  if (typeof module !== 'undefined' && module.exports) module.exports = api;
  else root.PixelTXT = api;
})(globalThis);
