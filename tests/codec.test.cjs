const { test } = require('node:test');
const assert = require('node:assert/strict');
const { encode, decode, validateSize, MAX_PIXELS } = require('../codec.js');

test('top-left to right, then next row; exact 2×2 format and round trip', async () => {
  const image = { width: 2, height: 2, data: new Uint8ClampedArray([255,0,0,255, 0,255,0,255, 0,0,255,255, 255,255,255,255]) };
  const text = (await encode(image)).join('');
  assert.equal(text, 'PIXELTXT/1/2/2\n255,0,0/0,255,0/0,0,255/255,255,255');
  assert.deepEqual(await decode(text), image);
});

test('single pixel and transparent RGBA are flattened onto white', async () => {
  for (const [rgba, expected] of [
    [[23,45,67,255], '23,45,67'],
    [[23,45,67,0], '255,255,255'],
    [[255,0,0,128], '255,127,127'],
  ]) {
    const parts = await encode({ width: 1, height: 1, data: new Uint8ClampedArray(rgba) });
    assert.equal(parts.join(''), `PIXELTXT/1/1/1\n${expected}`);
    assert.equal((await decode(parts.join(''))).data[3], 255);
  }
});

test('non-square multi-chunk image preserves every channel and reports progress', async () => {
  const width = 137, height = 83;
  const data = Uint8ClampedArray.from({ length: width * height * 4 }, (_, i) => i % 4 === 3 ? 255 : (i * 37) % 256);
  const encoding = [], decoding = [];
  const parts = await encode({ width, height, data }, value => encoding.push(value));
  assert.ok(parts.length > 2);
  assert.deepEqual(await decode(parts.join(''), value => decoding.push(value)), { width, height, data });
  assert.equal(encoding.at(-1), 1);
  assert.equal(decoding.at(-1), 1);
});

test('UTF-8 BOM, Windows line endings and surrounding whitespace', async () => {
  const image = await decode('\uFEFFPIXELTXT/1/2/1\r\n 0,1,2 / 253,254,255\r\n');
  assert.deepEqual([...image.data], [0,1,2,255,253,254,255,255]);
});

test('rejects malformed headers, dimensions, RGB values and incorrect pixel counts', async () => {
  const invalid = [
    '', '255,0,0', 'PIXELTXT/2/1/1\n1,2,3', 'PIXELTXT/1/0/1\n1,2,3',
    'PIXELTXT/1/-1/1\n1,2,3', 'PIXELTXT/1/1.5/1\n1,2,3',
    'PIXELTXT/1/9999999999999999999/1\n1,2,3', 'PIXELTXT/1/2049/2049\n1,2,3',
    'PIXELTXT/1/1/1\n256,0,0', 'PIXELTXT/1/1/1\n-1,0,0',
    'PIXELTXT/1/1/1\n1.5,0,0', 'PIXELTXT/1/1/1\n1e2,0,0',
    'PIXELTXT/1/1/1\n1,2', 'PIXELTXT/1/1/1\n1,2,3,4',
    'PIXELTXT/1/1/1\n1,2,3/', 'PIXELTXT/1/2/1\n1,2,3',
    'PIXELTXT/1/1/1\n1,2,3/4,5,6', 'PIXELTXT/1/2/1\n1,2,3//4,5,6',
    'PIXELTXT/1/1/1\n' + '0'.repeat(100),
  ];
  for (const text of invalid) await assert.rejects(decode(text), Error, text);
});

test('bounds and RGBA length validation', async () => {
  assert.doesNotThrow(() => validateSize(2048, 2048));
  assert.throws(() => validateSize(MAX_PIXELS, 1));
  assert.throws(() => validateSize(1, NaN));
  await assert.rejects(encode({ width: 2, height: 2, data: new Uint8ClampedArray(4) }));
});
