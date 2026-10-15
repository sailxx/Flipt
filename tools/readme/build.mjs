// SVG-плашки README в стиле Okto Calc, но чёрно-белые: чёрный фон, белые свечения, точечный шрифт 5×7
// (как LED-матрица и логотип), Golos Text и JetBrains Mono (урезаются под каждый язык). Тексты — strings.json.
// Шрифты (OFL-1.1) положить в tools/readme/.font/: golos.ttf (Golos Text) и jbmono.ttf (JetBrains Mono).
// Запуск: cd tools/readme && npm install && node build.mjs
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { createKit, ICONS } from './kit.mjs';

const DIR = path.dirname(fileURLToPath(import.meta.url));
const REPO = path.resolve(DIR, '../..');
const OUT = path.join(REPO, 'assets/readme');
const STR = JSON.parse(fs.readFileSync(path.join(DIR, 'strings.json'), 'utf8'));
const VERSION = fs.readFileSync(path.join(REPO, 'Flipt.cs'), 'utf8').match(/AssemblyVersion\("(\d+\.\d+\.\d+)/)[1];

const K = createKit({
  golos: path.join(DIR, '.font/golos.ttf'),
  mono: path.join(DIR, '.font/jbmono.ttf'),
  accent: { hi: '#FFFFFF', grad: ['#FFFFFF', '#8C8C8C'], on: '#000000', glow: '#FFFFFF', glowOpacity: 0.09, glow2: '#FFFFFF', btn: ['#FFFFFF', '#D4D4D4'] },
});
const { P, T, card, chip, iconBox, lines, wrap, fitText, delay, svg } = K;

// ---------- точечный шрифт 5×7 ----------
const G = {
  A: '01110 10001 10001 11111 10001 10001 10001', B: '11110 10001 10001 11110 10001 10001 11110',
  C: '01110 10001 10000 10000 10000 10001 01110', D: '11110 10001 10001 10001 10001 10001 11110',
  E: '11111 10000 10000 11110 10000 10000 11111', F: '11111 10000 10000 11110 10000 10000 10000',
  G: '01110 10001 10000 10111 10001 10001 01111', H: '10001 10001 10001 11111 10001 10001 10001',
  I: '01110 00100 00100 00100 00100 00100 01110', J: '00111 00010 00010 00010 00010 10010 01100',
  K: '10001 10010 10100 11000 10100 10010 10001', L: '10000 10000 10000 10000 10000 10000 11111',
  M: '10001 11011 10101 10101 10001 10001 10001', N: '10001 10001 11001 10101 10011 10001 10001',
  O: '01110 10001 10001 10001 10001 10001 01110', P: '11110 10001 10001 11110 10000 10000 10000',
  Q: '01110 10001 10001 10001 10101 10010 01101', R: '11110 10001 10001 11110 10100 10010 10001',
  S: '01111 10000 10000 01110 00001 00001 11110', T: '11111 00100 00100 00100 00100 00100 00100',
  U: '10001 10001 10001 10001 10001 10001 01110', V: '10001 10001 10001 10001 10001 01010 00100',
  W: '10001 10001 10001 10101 10101 10101 01010', X: '10001 10001 01010 00100 01010 10001 10001',
  Y: '10001 10001 01010 00100 00100 00100 00100', Z: '11111 00001 00010 00100 01000 10000 11111',
  0: '01110 10001 10011 10101 11001 10001 01110', 1: '00100 01100 00100 00100 00100 00100 01110',
  2: '01110 10001 00001 00010 00100 01000 11111', 3: '11111 00010 00100 00010 00001 10001 01110',
  4: '00010 00110 01010 10010 11111 00010 00010', 5: '11111 10000 11110 00001 00001 10001 01110',
  6: '00110 01000 10000 11110 10001 10001 01110', 7: '11111 00001 00010 00100 01000 01000 01000',
  8: '01110 10001 10001 01110 10001 10001 01110', 9: '01110 10001 10001 01111 00001 00010 01100',
  ':': '00000 00100 00100 00000 00100 00100 00000', '.': '00000 00000 00000 00000 00000 00000 00100',
  '-': '00000 00000 00000 11111 00000 00000 00000', '~': '00000 00000 01000 10101 00010 00000 00000',
  '+': '00000 00100 00100 11111 00100 00100 00000', ' ': '00000 00000 00000 00000 00000 00000 00000',
  З: '01110 10001 00001 00110 00001 10001 01110', Ж: '10101 10101 10101 01110 10101 10101 10101',
  И: '10001 10001 10011 10101 11001 10001 10001', Ц: '10010 10010 10010 10010 10010 11111 00001',
  Ф: '00100 01110 10101 10101 01110 00100 00100', Л: '00111 01001 01001 01001 01001 01001 10001',
  Ш: '10101 10101 10101 10101 10101 10101 11111',
};
for (const [cy, lat] of Object.entries({ А: 'A', В: 'B', Е: 'E', К: 'K', М: 'M', Н: 'H', О: 'O', Р: 'P', С: 'C', Т: 'T', Х: 'X' })) G[cy] = G[lat];

/** Текст точками: pitch — шаг сетки, off — прозрачность погашенных точек (0 — не рисовать). */
function dots(x, y, s, { pitch = 4, r = 0.4, fill = '#fff', off = 0, cls = '' } = {}) {
  let out = '';
  const chars = [...String(s).toUpperCase()];
  chars.forEach((ch, i) => {
    const rows = (G[ch] || G[' ']).split(' ');
    rows.forEach((row, ry) => [...row].forEach((bit, rx) => {
      const lit = bit === '1';
      if (!lit && !off) return;
      out += `<circle cx="${(x + (i * 6 + rx) * pitch + pitch / 2).toFixed(2)}" cy="${(y + ry * pitch + pitch / 2).toFixed(2)}" r="${(pitch * r).toFixed(2)}"${lit ? '' : ` fill-opacity="${off}"`}/>`;
    }));
  });
  return { s: `<g fill="${fill}"${cls ? ` class="${cls}"` : ''}>${out}</g>`, w: (chars.length * 6 - 1) * pitch, h: 7 * pitch };
}

/** Метка-чип точками. */
function dotChip(x, y, label) {
  const d = dots(0, 0, label, { pitch: 2.4, r: 0.42 });
  const w = Math.round(d.w + 30);
  return { s: `<rect x="${x}" y="${y}" width="${w}" height="30" rx="8" fill="#141414" stroke="#333"/>` + dots(x + 15, y + 15 - d.h / 2, label, { pitch: 2.4, r: 0.42 }).s, w };
}

/** Заголовок раздела: чип точками + крупная строка. */
function head(label, title, { x = 40, maxW = 800, size = 32 } = {}) {
  const ls = wrap(title, maxW, size, 800);
  const lh = size * 1.2;
  const s = `<g class="r">${dotChip(x, 34, label).s}</g>
<g class="r" ${delay(1)}>${lines(x, 104, ls, { size, weight: 800, lh, spacing: '-.6' })}</g>`;
  return { s, y: 104 + (ls.length - 1) * lh + 28 };
}

// Логотип — как значок программы: плитка и сетка 3×3, горящие точки складываются в F
function logo(x, y, n) {
  const pat = [[1, 1, 1], [1, 1, 0], [1, 0, 0]];
  let s = `<rect x="${x}" y="${y}" width="${n}" height="${n}" rx="${n * 0.24}" fill="#1E1E22" stroke="#3a3a3a"/>`;
  const step = n * 0.225, r = n * 0.075;
  pat.forEach((row, j) => row.forEach((on, i) =>
    s += `<circle cx="${x + n / 2 + (i - 1) * step}" cy="${y + n / 2 + (j - 1) * step}" r="${r}" fill="#fff"${on ? '' : ' fill-opacity=".28"'}/>`));
  return s;
}

// Плитка flip-часов с цифрами точками
function tile(x, y, w, h, text, label) {
  const pitch = h / 12;
  const d = dots(0, 0, text, { pitch });
  let s = `<rect x="${x}" y="${y}" width="${w}" height="${h}" rx="${h * 0.06}" fill="#161619" stroke="#262629"/>`;
  s += `<rect x="${x}" y="${y}" width="${w}" height="${h / 2}" rx="${h * 0.06}" fill="#fff" fill-opacity=".025"/>`;
  s += dots(x + (w - d.w) / 2, y + (h - d.h) / 2, text, { pitch, r: 0.38, off: 0.06 }).s;
  s += `<rect x="${x}" y="${y + h / 2 - 1}" width="${w}" height="2" fill="#000"/>`;
  if (label) s += `<text class="m" x="${x + 12}" y="${y + 20}" font-size="10" font-weight="600" letter-spacing="1" fill="#777">${T(label)}</text>`;
  return s;
}

// ---------- HERO ----------
function hero(l) {
  const W = 880;
  const name = dots(90, 42, 'FLIPT', { pitch: 4.2, r: 0.42, off: 0.08 });
  let b = `<g class="r">${logo(40, 36, 40)}${name.s}</g>`;
  b += `<g class="r" ${delay(1)}>${chip(90 + name.w + 18, 41, 'v' + VERSION).s}</g>`;
  const ts = 34;
  const all = [...wrap(l.line1, 470, ts, 800), ...wrap('*' + l.line2 + '*', 470, ts, 800)];
  b += `<g class="r" ${delay(2)}>${lines(40, 144, all, { size: ts, weight: 800, lh: ts * 1.22, spacing: '-.8' })}</g>`;
  let y = 144 + (all.length - 1) * ts * 1.22 + 46;
  let cx = 40;
  l.chips.forEach((c, i) => {
    const w = chip(0, 0, c, { upper: false }).w;
    if (cx + w > 500) { cx = 40; y += 40; }
    b += `<g class="r" ${delay(3 + i * 0.5)}>${chip(cx, y, c, { upper: false }).s}</g>`;
    cx += w + 10;
  });
  const H = Math.max(y + 30 + 40, 360);
  const tw = 150, th = 136, gap = 10, x0 = 525, y0 = (H - th) / 2;
  b += `<g class="r" ${delay(3)}><ellipse cx="${x0 + tw + gap / 2}" cy="${H / 2}" rx="200" ry="150" fill="url(#g1)"/>
${tile(x0, y0, tw, th, '3', 'AM')}${tile(x0 + tw + gap, y0, tw, th, '46')}</g>`;
  return svg(W, H, l.alt, b);
}

// ---------- ВОЗМОЖНОСТИ ----------
const DOT9 = [0, 1, 2].flatMap((j) => [0, 1, 2].map((i) => `<circle cx="${6 + i * 6}" cy="${6 + j * 6}" r="1.6" fill="#fff" stroke="none"${i + j > 2 ? ' fill-opacity=".35"' : ''}/>`)).join('');
const FEAT_ICONS = [
  '<rect x="4" y="3.5" width="16" height="17" rx="3"/><path d="M4 12h16"/>',
  '<circle cx="12" cy="13.5" r="7.5"/><path d="M12 13.5V9.5M10 2.5h4"/>',
  '<circle cx="12" cy="12" r="8.5"/><path d="M12 3.5a8.5 8.5 0 0 0 0 17z" fill="#fff"/>',
  '<path d="M4 9V4h5M15 4h5v5M20 15v5h-5M9 20H4v-5"/>',
  DOT9,
  '<path d="M13 2l-8 12h7l-1 8 8-12h-7z"/>',
];
function features(l) {
  const h0 = head(l.featuresTitle, l.featuresHead);
  let b = h0.s;
  const cw = (800 - 2 * 12) / 3;
  const items = l.features.map(([t, d]) => [t, wrap(d, cw - 40, 14, 500)]);
  const ch = 92 + Math.max(...items.map(([, d]) => d.length)) * 20 + 4;
  items.forEach(([t, d], i) => {
    const x = 40 + (i % 3) * (cw + 12), y = h0.y + Math.floor(i / 3) * (ch + 12);
    b += `<g class="r" ${delay(2 + i * 0.6)}>${card(x, y, cw, ch)}${iconBox(x + 20, y + 20, FEAT_ICONS[i])}
${fitText(x + 72, y + 46, t, cw - 88, { size: 17, weight: 700 })}
${lines(x + 20, y + 92, d, { size: 14, weight: 500, lh: 20, fill: P.muted })}</g>`;
  });
  return svg(880, h0.y + 2 * ch + 12 + 36, l.featuresAlt, b);
}

// ---------- В ЦИФРАХ ----------
function numbers(l) {
  const h0 = head(l.numbersTitle, l.numbersHead);
  let b = h0.s;
  const w = (800 - 3 * 12) / 4;
  l.numbers.forEach(([n, unit, d], i) => {
    const x = 40 + i * (w + 12), y = h0.y;
    const num = dots(x + 22, y + 24, n, { pitch: 5.6, r: 0.42, off: 0.07 });
    b += `<g class="r" ${delay(2 + i)}>${card(x, y, w, 128)}${num.s}
${unit ? `<text x="${x + 22 + num.w + 10}" y="${y + 24 + num.h - 2}" font-size="17" font-weight="600" fill="${P.muted}">${T(unit)}</text>` : ''}
${fitText(x + 22, y + 102, d, w - 40, { size: 14, weight: 500, fill: P.muted })}</g>`;
  });
  return svg(880, h0.y + 128 + 36, l.numbersAlt, b);
}

// ---------- КЛАВИШИ ----------
function keycap(x, y, k) {
  const w = Math.max(34, Math.round(K.width(k, 13, 600, { mono: true }) + 22));
  return { s: `<rect x="${x}" y="${y}" width="${w}" height="32" rx="8" fill="#161616" stroke="#3a3a3a"/><rect x="${x + 1}" y="${y + 29}" width="${w - 2}" height="2" rx="1" fill="#000" fill-opacity=".6"/>` +
    `<text class="m" x="${x + w / 2}" y="${y + 21}" text-anchor="middle" font-size="13" font-weight="600" fill="#fff">${T(k)}</text>`, w };
}
function keys(l) {
  const h0 = head(l.keysTitle, l.keysHead);
  let b = h0.s;
  const cw = (800 - 12) / 2, rh = 56;
  l.keys.forEach(([ks, d], i) => {
    const x = 40 + (i % 2) * (cw + 12), y = h0.y + Math.floor(i / 2) * (rh + 10);
    let kx = x + 16, caps = '';
    ks.forEach((k) => { const c = keycap(kx, y + 12, k); caps += c.s; kx += c.w + 6; });
    b += `<g class="r" ${delay(2 + i * 0.4)}>${card(x, y, cw, rh, 14)}${caps}
${fitText(Math.max(kx + 10, x + 110), y + 33, d, cw - Math.max(kx + 10 - x, 110) - 16, { size: 15, weight: 500, fill: '#D6D6D6' })}</g>`;
  });
  return svg(880, h0.y + Math.ceil(l.keys.length / 2) * (rh + 10) - 10 + 36, l.keysAlt, b);
}

// ---------- сборка ----------
const langs = {};
for (const [lang, l] of Object.entries(STR)) {
  langs[lang] = () => ({
    [`hero-${lang}.svg`]: hero(l),
    [`cta-${lang}.svg`]: K.button(l.cta, ICONS.download, true),
    [`features-${lang}.svg`]: features(l),
    [`numbers-${lang}.svg`]: numbers(l),
    [`keys-${lang}.svg`]: keys(l),
  });
}
await K.build(OUT, langs, 'Fliptv.AM');
