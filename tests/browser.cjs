// Optional integration test: npm install --no-save playwright && npx playwright install chromium
// Run: node tests/browser.cjs
const { chromium } = require('playwright');
const assert = require('node:assert/strict');
const fs = require('node:fs/promises');
const path = require('node:path');
const { pathToFileURL } = require('node:url');

(async () => {
  const browser = await chromium.launch({ headless: true, ...(process.env.BROWSER_CHANNEL ? { channel: process.env.BROWSER_CHANNEL } : {}) });
  try {
    const page = await browser.newPage({ viewport: { width: 1280, height: 1080 } });
    const errors = [];
    page.on('pageerror', error => errors.push(error.message));
    await page.goto(pathToFileURL(path.resolve(__dirname, '../index.html')).href);
    assert.equal(await page.locator('#download-txt').isDisabled(), true);
    const pngURL = await page.evaluate(() => {
      const c = document.createElement('canvas'); c.width = 2; c.height = 2;
      c.getContext('2d').putImageData(new ImageData(new Uint8ClampedArray([255,0,0,255,0,255,0,255,0,0,255,255,255,255,255,255]), 2, 2), 0, 0);
      return c.toDataURL('image/png');
    });
    await page.locator('#file-input').setInputFiles({ name: 'four-colors.png', mimeType: 'image/png', buffer: Buffer.from(pngURL.split(',')[1], 'base64') });
    await page.waitForFunction(() => !document.getElementById('download-txt').disabled);
    const downloadEvent = page.waitForEvent('download');
    await page.locator('#download-txt').click();
    const download = await downloadEvent;
    assert.equal(download.suggestedFilename(), 'four-colors.txt');
    const txt = await fs.readFile(await download.path(), 'utf8');
    assert.equal(txt, 'PIXELTXT/1/2/2\n255,0,0/0,255,0/0,0,255/255,255,255');
    await page.locator('#text-tab').click();
    await page.locator('#file-input').setInputFiles({ name: 'roundtrip.txt', mimeType: 'text/plain', buffer: Buffer.from(txt) });
    await page.waitForFunction(() => document.getElementById('file-name').textContent === 'roundtrip.txt' && !document.getElementById('download-txt').disabled);
    const pixels = await page.locator('canvas').evaluate(c => [...c.getContext('2d').getImageData(0, 0, 2, 2).data]);
    assert.deepEqual(pixels, [255,0,0,255,0,255,0,255,0,0,255,255,255,255,255,255]);
    await page.locator('canvas').click({ position: { x: 2, y: 2 } });
    assert.match(await page.locator('#pixel-value').innerText(), /R 255.*G 0.*B 0/);
    const pngEvent = page.waitForEvent('download');
    await page.locator('#download-png').click();
    const png = await pngEvent;
    assert.equal(png.suggestedFilename(), 'roundtrip.png');
    const pngBytes = await fs.readFile(await png.path());
    assert.equal(pngBytes.subarray(1, 4).toString(), 'PNG');
    await page.locator('#file-input').setInputFiles({ name: 'bad.txt', mimeType: 'text/plain', buffer: Buffer.from('PIXELTXT/1/1/1\n999,0,0') });
    await page.waitForFunction(() => document.getElementById('status').classList.contains('error'));
    assert.match(await page.locator('#status').innerText(), /255/);
    assert.equal(await page.locator('#download-txt').isDisabled(), false);
    await page.locator('#image-tab').click();
    // Browser-level alpha compositing must match the exported preview.
    const transparent = await page.evaluate(() => {
      const c = document.createElement('canvas'); c.width = 1; c.height = 1;
      return c.toDataURL('image/png');
    });
    await page.locator('#file-input').setInputFiles({ name: 'transparent.png', mimeType: 'image/png', buffer: Buffer.from(transparent.split(',')[1], 'base64') });
    await page.waitForFunction(() => document.getElementById('file-name').textContent === 'transparent.png');
    assert.deepEqual(await page.locator('canvas').evaluate(c => [...c.getContext('2d').getImageData(0, 0, 1, 1).data]), [255,255,255,255]);
    await page.reload();
    await page.locator('#demo-button').click();
    await page.waitForFunction(() => !document.getElementById('download-txt').disabled);
    await fs.mkdir(path.resolve(__dirname, '../artifacts'), { recursive: true });
    await page.screenshot({ path: path.resolve(__dirname, '../artifacts/desktop.png'), fullPage: true });
    await page.setViewportSize({ width: 390, height: 844 });
    assert.equal(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), true);
    await page.screenshot({ path: path.resolve(__dirname, '../artifacts/mobile.png'), fullPage: true });
    assert.deepEqual(errors, []);
    console.log('PASS: real image import → TXT download → TXT import → exact pixels → PNG download; validation, alpha, mobile layout.');
  } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });
