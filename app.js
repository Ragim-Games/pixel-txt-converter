/* global PixelTXT */
'use strict';
const $ = id => document.getElementById(id);
let mode = 'image';
let busy = false;
let current = null;
const canvas = $('preview-canvas');
const context = canvas.getContext('2d', { willReadFrequently: true });

function status(message, error = false) {
  $('status').textContent = message;
  $('status').classList.toggle('error', error);
}

function setBusy(value) {
  busy = value;
  for (const id of ['image-tab', 'text-tab', 'file-input', 'demo-button']) $(id).disabled = value;
  $('download-txt').disabled = value || !current;
  $('download-png').disabled = value || !current;
  $('progress').hidden = !value;
  $('dropzone').classList.toggle('busy', value);
}

function switchMode(next) {
  if (busy) return;
  mode = next;
  for (const key of ['image', 'text']) {
    $(key + '-tab').classList.toggle('active', key === mode);
    $(key + '-tab').setAttribute('aria-pressed', String(key === mode));
  }
  $('file-input').accept = mode === 'image' ? 'image/*' : '.txt,text/plain';
  $('file-input').setAttribute('aria-label', mode === 'image' ? '이미지 파일 선택' : 'TXT 파일 선택');
  $('upload-title').textContent = mode === 'image' ? '이미지를 여기에 놓으세요' : 'TXT 파일을 여기에 놓으세요';
  $('upload-help').textContent = mode === 'image' ? 'PNG, JPG, WebP 등 · 최대 4,194,304픽셀' : 'PIXELTXT 형식의 .txt 파일 · 최대 64 MiB';
}

const progress = fraction => { $('progress').value = fraction; };
const fileSize = bytes => bytes < 1024 ? `${bytes} B` : bytes < 1048576 ? `${(bytes / 1024).toFixed(1)} KiB` : `${(bytes / 1048576).toFixed(1)} MiB`;

async function readImage(file) {
  const url = URL.createObjectURL(file);
  try {
    const img = new Image();
    img.src = url;
    try { await img.decode(); } catch { throw new Error('이미지를 열 수 없습니다. PNG, JPG, WebP 등 지원되는 이미지인지 확인해 주세요.'); }
    PixelTXT.validateSize(img.naturalWidth, img.naturalHeight);
    const temporary = document.createElement('canvas');
    temporary.width = img.naturalWidth;
    temporary.height = img.naturalHeight;
    const ctx = temporary.getContext('2d', { willReadFrequently: true });
    ctx.fillStyle = '#ffffff';
    ctx.fillRect(0, 0, temporary.width, temporary.height);
    ctx.drawImage(img, 0, 0);
    return ctx.getImageData(0, 0, temporary.width, temporary.height);
  } finally { URL.revokeObjectURL(url); }
}

async function displayImage(image, filename) {
  status('모든 픽셀을 순서대로 기록하고 있습니다…');
  const parts = await PixelTXT.encode(image, progress);
  const blob = new Blob(parts, { type: 'text/plain;charset=utf-8' });
  canvas.width = image.width;
  canvas.height = image.height;
  canvas.style.width = `${Math.min(640, image.width * 12, 420 * image.width / image.height)}px`;
  context.putImageData(new ImageData(image.data, image.width, image.height), 0, 0);
  current = { blob, name: filename.replace(/\.[^.]+$/, '') || 'pixels' };
  canvas.hidden = false;
  $('empty-state').hidden = true;
  $('dimensions').textContent = `${image.width.toLocaleString()} × ${image.height.toLocaleString()} px`;
  $('file-info').hidden = false;
  $('file-name').textContent = filename;
  $('file-name').title = filename;
  $('file-size').textContent = `${(image.width * image.height).toLocaleString()}픽셀`;
  const snippet = (parts[0] + parts[1]).slice(0, 320);
  $('text-preview').textContent = snippet + (blob.size > 320 ? '…' : '');
  $('text-caption').textContent = `TXT 미리보기 · ${fileSize(blob.size)} · 전체 데이터는 다운로드에 포함됩니다`;
  $('pixel-position').textContent = '픽셀을 클릭해 RGB 값을 확인하세요';
  $('pixel-value').textContent = 'R —   G —   B —';
  status('변환이 완료되었습니다. TXT 또는 PNG로 저장하세요.');
}

async function loadFile(file) {
  if (!file || busy) return;
  setBusy(true);
  progress(0);
  status('파일을 읽고 있습니다…');
  try {
    if (file.size > PixelTXT.MAX_TEXT_BYTES) throw new Error('파일은 최대 64 MiB까지 지원합니다.');
    const image = mode === 'text' ? await PixelTXT.decode(await file.text(), progress) : await readImage(file);
    await displayImage(image, file.name);
  } catch (error) {
    status(error.message + (current ? ' 이전 변환 결과는 유지됩니다.' : ''), true);
  } finally {
    setBusy(false);
    $('file-input').value = '';
  }
}

function download(blob, filename) {
  const url = URL.createObjectURL(blob);
  const anchor = document.createElement('a');
  anchor.href = url;
  anchor.download = filename;
  document.body.append(anchor);
  anchor.click();
  anchor.remove();
  setTimeout(() => URL.revokeObjectURL(url), 60000);
}

$('image-tab').addEventListener('click', () => switchMode('image'));
$('text-tab').addEventListener('click', () => switchMode('text'));
$('file-input').addEventListener('change', event => loadFile(event.target.files[0]));
for (const eventName of ['dragenter', 'dragover']) {
  $('dropzone').addEventListener(eventName, event => {
    event.preventDefault();
    if (!busy) $('dropzone').classList.add('dragging');
  });
}
for (const eventName of ['dragleave', 'drop']) {
  $('dropzone').addEventListener(eventName, event => {
    event.preventDefault();
    $('dropzone').classList.remove('dragging');
    if (eventName === 'drop') {
      if (event.dataTransfer.files.length !== 1) status('한 번에 파일 하나를 선택해 주세요.', true);
      else loadFile(event.dataTransfer.files[0]);
    }
  });
}
// Dropping outside the picker must not navigate away from the app.
window.addEventListener('dragover', event => event.preventDefault());
window.addEventListener('drop', event => event.preventDefault());
$('download-txt').addEventListener('click', () => { if (current && !busy) download(current.blob, current.name + '.txt'); });
$('download-png').addEventListener('click', () => {
  if (!current || busy) return;
  const filename = current.name + '.png';
  setBusy(true);
  canvas.toBlob(blob => {
    if (blob) download(blob, filename);
    else status('PNG 저장에 실패했습니다. 다시 시도해 주세요.', true);
    setBusy(false);
  }, 'image/png');
});

canvas.addEventListener('click', event => {
  if (busy || !current) return;
  const rect = canvas.getBoundingClientRect();
  const x = Math.min(canvas.width - 1, Math.floor((event.clientX - rect.left) * canvas.width / rect.width));
  const y = Math.min(canvas.height - 1, Math.floor((event.clientY - rect.top) * canvas.height / rect.height));
  const [r, g, b] = context.getImageData(x, y, 1, 1).data;
  $('pixel-position').textContent = `X ${x} · Y ${y} · ${(y * canvas.width + x + 1).toLocaleString()}번째 픽셀`;
  $('pixel-value').textContent = `R ${r}   G ${g}   B ${b}`;
});

$('demo-button').addEventListener('click', async () => {
  if (busy) return;
  setBusy(true);
  progress(0);
  try {
    const width = 32, height = 32;
    const data = new Uint8ClampedArray(width * height * 4);
    const colors = [[246, 232, 202], [206, 216, 192], [124, 157, 133], [73, 109, 99], [234, 174, 129], [151, 121, 165]];
    for (let y = 0; y < height; y++) {
      for (let x = 0; x < width; x++) {
        const color = Math.floor(x / 8) + Math.floor(y / 8);
        data.set([...colors[color % colors.length], 255], (y * width + x) * 4);
      }
    }
    await displayImage({ width, height, data }, 'pixel-sample.png');
  } catch (error) { status(error.message, true); }
  finally { setBusy(false); }
});
