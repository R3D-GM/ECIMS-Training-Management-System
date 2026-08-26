// Lightweight canvas signature capture. No external library required.
// Usage: initSignaturePad('canvasId', 'hiddenInputId', 'clearBtnId')
function initSignaturePad(canvasId, hiddenInputId, clearBtnId) {
  const canvas = document.getElementById(canvasId);
  if (!canvas) return;
  const input = document.getElementById(hiddenInputId);
  const clearBtn = document.getElementById(clearBtnId);
  const ctx = canvas.getContext('2d');

  function resize() {
    const ratio = Math.max(window.devicePixelRatio || 1, 1);
    const rect = canvas.getBoundingClientRect();
    canvas.width = rect.width * ratio;
    canvas.height = rect.height * ratio;
    ctx.scale(ratio, ratio);
    ctx.lineWidth = 2;
    ctx.lineCap = 'round';
    ctx.strokeStyle = '#1f1213';
  }
  resize();
  window.addEventListener('resize', resize);

  let drawing = false;
  let last = { x: 0, y: 0 };

  function pos(e) {
    const rect = canvas.getBoundingClientRect();
    const point = e.touches ? e.touches[0] : e;
    return { x: point.clientX - rect.left, y: point.clientY - rect.top };
  }

  function start(e) {
    drawing = true;
    last = pos(e);
    e.preventDefault();
  }
  function move(e) {
    if (!drawing) return;
    const p = pos(e);
    ctx.beginPath();
    ctx.moveTo(last.x, last.y);
    ctx.lineTo(p.x, p.y);
    ctx.stroke();
    last = p;
    if (input) input.value = canvas.toDataURL('image/png');
    e.preventDefault();
  }
  function end() { drawing = false; }

  canvas.addEventListener('mousedown', start);
  canvas.addEventListener('mousemove', move);
  window.addEventListener('mouseup', end);
  canvas.addEventListener('touchstart', start, { passive: false });
  canvas.addEventListener('touchmove', move, { passive: false });
  canvas.addEventListener('touchend', end);

  if (clearBtn) {
    clearBtn.addEventListener('click', function () {
      ctx.clearRect(0, 0, canvas.width, canvas.height);
      if (input) input.value = '';
    });
  }
}
