import fs from 'node:fs/promises';
import fssync from 'node:fs';
import path from 'node:path';
import zlib from 'node:zlib';
import { SOURCES } from './download-all.mjs';

const OUT_DIR = path.resolve('data/historical-archive');
const RAW_DIR = path.join(OUT_DIR, 'raw');
const CSV_DIR = path.join(OUT_DIR, 'csv');
const JSON_DIR = path.join(OUT_DIR, 'json');
const COMPILED_DIR = path.join(OUT_DIR, 'compiled');

// --- Minimal, dependency-free ZIP reader using Central Directory ---
function unzipBuffer(buf) {
  const entries = new Map();
  let eocdOffset = -1;
  const minEocd = Math.max(0, buf.length - 65557);
  for (let i = buf.length - 22; i >= minEocd; i--) {
    if (buf.readUInt32LE(i) === 0x06054b50) {
      eocdOffset = i;
      break;
    }
  }
  if (eocdOffset === -1) {
    throw new Error('Invalid ZIP: EOCD not found');
  }

  const cdCount = buf.readUInt16LE(eocdOffset + 10);
  const cdOffset = buf.readUInt32LE(eocdOffset + 16);

  let ptr = cdOffset;
  for (let i = 0; i < cdCount && ptr < eocdOffset; i++) {
    if (buf.readUInt32LE(ptr) !== 0x02014b50) break;
    const method = buf.readUInt16LE(ptr + 10);
    const compSize = buf.readUInt32LE(ptr + 20);
    const nameLen = buf.readUInt16LE(ptr + 28);
    const extraLen = buf.readUInt16LE(ptr + 30);
    const commentLen = buf.readUInt16LE(ptr + 32);
    const localHeaderOffset = buf.readUInt32LE(ptr + 42);
    const name = buf.toString('utf8', ptr + 46, ptr + 46 + nameLen);

    ptr += 46 + nameLen + extraLen + commentLen;

    if (name.endsWith('/')) continue;
    if (localHeaderOffset + 30 > buf.length) continue;
    const lhNameLen = buf.readUInt16LE(localHeaderOffset + 26);
    const lhExtraLen = buf.readUInt16LE(localHeaderOffset + 28);
    const dataStart = localHeaderOffset + 30 + lhNameLen + lhExtraLen;
    const slice = buf.subarray(dataStart, dataStart + compSize);

    if (method === 0) {
      entries.set(name, slice);
    } else if (method === 8) {
      try {
        entries.set(name, zlib.inflateRawSync(slice));
      } catch {
        // ignore corrupt entry
      }
    }
  }
  return entries;
}

function decodeXmlEntities(str) {
  if (!str) return '';
  return str
    .replace(/&#x([0-9a-fA-F]+);/g, (_, hex) => String.fromCodePoint(parseInt(hex, 16)))
    .replace(/&#(\d+);/g, (_, dec) => String.fromCodePoint(parseInt(dec, 10)))
    .replace(/&quot;/g, '"')
    .replace(/&apos;/g, "'")
    .replace(/&lt;/g, '<')
    .replace(/&gt;/g, '>')
    .replace(/&amp;/g, '&')
    .replace(/_x000D_/g, '');
}

function colLettersToIndex(letters) {
  let col = 0;
  for (let i = 0; i < letters.length; i++) {
    col = col * 26 + (letters.charCodeAt(i) - 64);
  }
  return col - 1;
}

function parseSharedStrings(xml) {
  const strings = [];
  if (!xml) return strings;
  const siRegex = /<si\b[^>]*>([\s\S]*?)<\/si>/g;
  let match;
  while ((match = siRegex.exec(xml)) !== null) {
    const inner = match[1];
    const tRegex = /<t(?:\s+[^>]*)?>([\s\S]*?)<\/t>/g;
    let tMatch;
    let combined = '';
    while ((tMatch = tRegex.exec(inner)) !== null) {
      combined += decodeXmlEntities(tMatch[1]);
    }
    strings.push(combined);
  }
  return strings;
}

const BUILTIN_NUM_FMTS = new Map([
  [0, 'General'],
  [1, '0'],
  [2, '0.00'],
  [3, '#,##0'],
  [4, '#,##0.00'],
  [9, '0%'],
  [10, '0.00%'],
  [14, 'm/d/yyyy'],
  [15, 'd-mmm-yy'],
  [16, 'd-mmm'],
  [17, 'mmm-yy'],
  [18, 'h:mm am/pm'],
  [19, 'h:mm:ss am/pm'],
  [20, 'h:mm'],
  [21, 'h:mm:ss'],
  [22, 'm/d/yyyy h:mm'],
  [45, 'mm:ss'],
  [46, '[h]:mm:ss'],
  [47, 'mm:ss.0'],
  [49, '@'],
]);

function parseStyles(stylesXml) {
  const numFmts = new Map(BUILTIN_NUM_FMTS);
  const cellXfs = [];
  if (!stylesXml) return { numFmts, cellXfs };

  const numFmtRegex = /<numFmt\b([^>]+)\/?>/g;
  let m;
  while ((m = numFmtRegex.exec(stylesXml)) !== null) {
    const attrs = m[1];
    const idM = /\bnumFmtId="(\d+)"/.exec(attrs);
    const codeM = /\bformatCode="([^"]+)"/.exec(attrs);
    if (idM && codeM) {
      numFmts.set(parseInt(idM[1], 10), decodeXmlEntities(codeM[1]));
    }
  }

  const cellXfsBlock = /<cellXfs\b[^>]*>([\s\S]*?)<\/cellXfs>/.exec(stylesXml);
  if (cellXfsBlock) {
    const xfRegex = /<xf\b([^>]*?)(?:\/>|>[\s\S]*?<\/xf>)/g;
    let xm;
    while ((xm = xfRegex.exec(cellXfsBlock[1])) !== null) {
      const attrs = xm[1];
      const nfM = /\bnumFmtId="(\d+)"/.exec(attrs);
      const numFmtId = nfM ? parseInt(nfM[1], 10) : 0;
      const formatCode = numFmts.get(numFmtId) || 'General';
      cellXfs.push({ numFmtId, formatCode });
    }
  }

  return { numFmts, cellXfs };
}

const MONTHS_SHORT = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];
const MONTHS_FULL = ['January', 'February', 'March', 'April', 'May', 'June', 'July', 'August', 'September', 'October', 'November', 'December'];
const DAYS_FULL = ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday'];

function excelSerialToParts(serial) {
  const totalSeconds = Math.round(serial * 86400);
  const wholeDays = Math.floor(totalSeconds / 86400);
  const daySeconds = ((totalSeconds % 86400) + 86400) % 86400;
  // Excel 1900 epoch: 25569 is 1970-01-01 UTC
  const utcMs = (wholeDays - 25569) * 86400000;
  const dt = new Date(utcMs);
  const year = dt.getUTCFullYear();
  const month = dt.getUTCMonth() + 1;
  const day = dt.getUTCDate();
  const dow = dt.getUTCDay();
  const hours24 = Math.floor(daySeconds / 3600);
  const minutes = Math.floor((daySeconds % 3600) / 60);
  const seconds = daySeconds % 60;
  const ampm = hours24 >= 12 ? 'PM' : 'AM';
  const hours12 = hours24 % 12 || 12;
  return { year, month, day, dow, hours24, hours12, minutes, seconds, ampm, totalSeconds };
}

function formatNumericCell(rawV, styleInfo) {
  const num = Number(rawV);
  if (!Number.isFinite(num)) return rawV;

  const fmt = styleInfo ? styleInfo.formatCode : 'General';
  const numFmtId = styleInfo ? styleInfo.numFmtId : 0;

  // 1. Check elapsed duration / time formats first
  // e.g. [h]:mm:ss (numFmtId 46), h":"mm":"ss, hh":"mm":"ss, hh:mm:ss
  if (
    numFmtId === 46 ||
    fmt === '[h]:mm:ss' ||
    fmt === 'h":"mm":"ss' ||
    fmt === 'hh":"mm":"ss' ||
    (fmt === 'hh:mm:ss' && num < 100)
  ) {
    const sign = num < 0 ? '-' : '';
    const totalSec = Math.round(Math.abs(num) * 86400);
    const h = Math.floor(totalSec / 3600);
    const m = Math.floor((totalSec % 3600) / 60);
    const s = totalSec % 60;
    const hStr = fmt.startsWith('hh') ? String(h).padStart(2, '0') : String(h);
    return `${sign}${hStr}:${String(m).padStart(2, '0')}:${String(s).padStart(2, '0')}`;
  }

  // mm:ss or mm":"ss
  if (numFmtId === 45 || fmt === 'mm:ss' || fmt === 'mm":"ss') {
    const sign = num < 0 ? '-' : '';
    const totalSec = Math.round(Math.abs(num) * 86400);
    const m = Math.floor(totalSec / 60);
    const s = totalSec % 60;
    return `${sign}${String(m).padStart(2, '0')}:${String(s).padStart(2, '0')}`;
  }

  // ":"ss (Average Life in seconds, e.g. :25)
  if (fmt === '":"ss') {
    const totalSec = Math.round(Math.abs(num) * 86400);
    if (totalSec >= 60) {
      const m = Math.floor(totalSec / 60);
      const s = totalSec % 60;
      return `${m}:${String(s).padStart(2, '0')}`;
    }
    return `:${String(totalSec).padStart(2, '0')}`;
  }

  // 2. Date & Time-of-Day formats
  if (fmt === 'h:mm am/pm' || numFmtId === 18) {
    const p = excelSerialToParts(num);
    return `${p.hours12}:${String(p.minutes).padStart(2, '0')} ${p.ampm}`;
  }
  if (fmt === 'h:mm:ss am/pm' || numFmtId === 19) {
    const p = excelSerialToParts(num);
    return `${p.hours12}:${String(p.minutes).padStart(2, '0')}:${String(p.seconds).padStart(2, '0')} ${p.ampm}`;
  }
  if ((fmt === 'm/d/yyyy' || fmt === 'M/d/yyyy' || numFmtId === 14) && num > 1000 && num < 80000) {
    const p = excelSerialToParts(num);
    return `${p.month}/${p.day}/${p.year}`;
  }
  if (fmt === 'm-d-yyyy' && num > 1000 && num < 80000) {
    const p = excelSerialToParts(num);
    return `${p.month}-${p.day}-${p.year}`;
  }
  if (fmt === 'm/d' && num > 1000 && num < 80000) {
    const p = excelSerialToParts(num);
    return `${p.month}/${p.day}`;
  }
  if (fmt === 'm-d' && num > 1000 && num < 80000) {
    const p = excelSerialToParts(num);
    return `${p.month}-${p.day}`;
  }
  if (fmt === 'd-m' && num > 1000 && num < 80000) {
    const p = excelSerialToParts(num);
    return `${p.day}-${p.month}`;
  }
  if (fmt === 'mmmm d' && num > 1000 && num < 80000) {
    const p = excelSerialToParts(num);
    return `${MONTHS_FULL[p.month - 1]} ${p.day}`;
  }
  if (fmt === 'dddd mmm d' && num > 1000 && num < 80000) {
    const p = excelSerialToParts(num);
    return `${DAYS_FULL[p.dow]} ${MONTHS_SHORT[p.month - 1]} ${p.day}`;
  }
  if (fmt === 'm/d h:mm am/pm' && num > 1000 && num < 80000) {
    const p = excelSerialToParts(num);
    return `${p.month}/${p.day} ${p.hours12}:${String(p.minutes).padStart(2, '0')} ${p.ampm}`;
  }
  if (fmt === 'm/d/yy h:mm am/pm' && num > 1000 && num < 80000) {
    const p = excelSerialToParts(num);
    const yy = String(p.year).slice(-2);
    return `${p.month}/${p.day}/${yy} ${p.hours12}:${String(p.minutes).padStart(2, '0')} ${p.ampm}`;
  }
  if ((fmt === 'm/d/yyyy h:mm:ss' || fmt === 'm/d/yyyy h:mm' || numFmtId === 22) && num > 1000 && num < 80000) {
    const p = excelSerialToParts(num);
    return `${p.month}/${p.day}/${p.year} ${p.hours24}:${String(p.minutes).padStart(2, '0')}:${String(p.seconds).padStart(2, '0')}`;
  }

  // 3. Zero-padded integer format "000000000"
  if (fmt === '000000000' && Number.isInteger(num) && num >= 0) {
    return String(Math.round(num)).padStart(9, '0');
  }

  // 4. Clean up Google Sheets .xlsx integer trailing ".0" (e.g. "21.0" -> "21")
  if (/^-?\d+\.0$/.test(rawV)) {
    return rawV.slice(0, -2);
  }

  // 5. Round floating point IEEE-754 noise like 12.300000000000001
  if (/^-?\d+\.\d{9,}$/.test(rawV)) {
    const rounded = Number(num.toPrecision(12));
    if (!Number.isNaN(rounded)) return rounded.toString();
  }

  return rawV;
}

function parseWorkbookSheets(workbookXml, relsXml) {
  const relMap = new Map();
  if (relsXml) {
    const tagRegex = /<Relationship\b([^>]+)\/?>/g;
    let m;
    while ((m = tagRegex.exec(relsXml)) !== null) {
      const attrs = m[1];
      const idM = /\bId="([^"]+)"/.exec(attrs);
      const targetM = /\bTarget="([^"]+)"/.exec(attrs);
      if (idM && targetM) relMap.set(idM[1], targetM[1]);
    }
  }

  const sheets = [];
  const sheetTagRegex = /<sheet\b([^>]+)\/?>/g;
  let m;
  let idx = 1;
  while ((m = sheetTagRegex.exec(workbookXml)) !== null) {
    const attrs = m[1];
    const nameM = /\bname="([^"]+)"/.exec(attrs);
    const rIdM = /\br:id="([^"]+)"/.exec(attrs);
    const stateM = /\bstate="([^"]+)"/.exec(attrs);
    const name = nameM ? decodeXmlEntities(nameM[1]) : `Sheet${idx}`;
    const hidden = stateM ? stateM[1] === 'hidden' || stateM[1] === 'veryHidden' : false;
    let target = (rIdM && relMap.get(rIdM[1])) || `worksheets/sheet${idx}.xml`;
    target = target.replace(/^\/+/, '');
    if (!target.startsWith('xl/')) target = `xl/${target}`;
    sheets.push({ name, path: target, hidden });
    idx++;
  }
  return sheets;
}

function parseWorksheetXml(xml, sharedStrings, styles) {
  const rows = [];
  const rowRegex = /<row\b[^>]*>([\s\S]*?)<\/row>/g;
  let rMatch;
  let maxCols = 0;

  while ((rMatch = rowRegex.exec(xml)) !== null) {
    const rowContent = rMatch[1];
    const rowCells = [];
    const cellRegex = /<c\b([^>]*?)(?:\/>|>([\s\S]*?)<\/c>)/g;
    let cMatch;
    let nextCol = 0;

    while ((cMatch = cellRegex.exec(rowContent)) !== null) {
      const attrs = cMatch[1];
      const inner = cMatch[2] || '';
      const rAttr = /\br="([A-Z]+)\d+"/.exec(attrs);
      const tAttr = /\bt="([^"]+)"/.exec(attrs);
      const sAttr = /\bs="(\d+)"/.exec(attrs);
      const colIdx = rAttr ? colLettersToIndex(rAttr[1]) : nextCol;
      nextCol = colIdx + 1;

      const styleIdx = sAttr ? parseInt(sAttr[1], 10) : -1;
      const styleInfo = styleIdx >= 0 && styles ? styles.cellXfs[styleIdx] : null;

      let val = '';
      const type = tAttr ? tAttr[1] : '';
      if (type === 'inlineStr') {
        const tRegex = /<t(?:\s+[^>]*)?>([\s\S]*?)<\/t>/g;
        let tMatch;
        let combined = '';
        while ((tMatch = tRegex.exec(inner)) !== null) {
          combined += decodeXmlEntities(tMatch[1]);
        }
        val = combined;
      } else {
        const vM = /<v>([\s\S]*?)<\/v>/.exec(inner);
        if (vM) {
          const rawV = vM[1];
          if (type === 's') {
            const sIdx = parseInt(rawV, 10);
            val = sharedStrings[sIdx] ?? rawV;
          } else if (type === 'b') {
            val = rawV === '1' ? 'TRUE' : 'FALSE';
          } else if (type === 'str' || type === 'e') {
            val = decodeXmlEntities(rawV);
          } else {
            val = formatNumericCell(decodeXmlEntities(rawV), styleInfo);
          }
        }
      }

      while (rowCells.length < colIdx) rowCells.push('');
      rowCells[colIdx] = val;
    }

    // Trim trailing empty cells
    while (rowCells.length > 0 && (rowCells[rowCells.length - 1] === '' || rowCells[rowCells.length - 1] == null)) {
      rowCells.pop();
    }
    if (rowCells.some(c => c !== '' && c != null)) {
      if (rowCells.length > maxCols) maxCols = rowCells.length;
      rows.push(rowCells);
    }
  }

  // Post-process unstyled serial timestamps in column 0 if header is "Timestamp" (e.g. Late League MatchH tabs)
  if (rows.length > 1 && rows[0][0] === 'Timestamp') {
    for (let r = 1; r < rows.length; r++) {
      const cell0 = rows[r][0];
      if (cell0 && /^\d{5}\.\d+$/.test(cell0)) {
        const num = Number(cell0);
        if (num > 40000 && num < 50000) {
          const p = excelSerialToParts(num);
          rows[r][0] = `${p.month}/${p.day}/${p.year} ${p.hours24}:${String(p.minutes).padStart(2, '0')}:${String(p.seconds).padStart(2, '0')}`;
        }
      }
    }
  }

  return { rows, rowCount: rows.length, colCount: maxCols };
}

function parseXlsxBuffer(buf) {
  const entries = unzipBuffer(buf);
  const sharedStringsXml = entries.has('xl/sharedStrings.xml')
    ? entries.get('xl/sharedStrings.xml').toString('utf8')
    : '';
  const sharedStrings = parseSharedStrings(sharedStringsXml);

  const stylesXml = entries.has('xl/styles.xml')
    ? entries.get('xl/styles.xml').toString('utf8')
    : '';
  const styles = parseStyles(stylesXml);

  const workbookXml = entries.has('xl/workbook.xml')
    ? entries.get('xl/workbook.xml').toString('utf8')
    : '';
  const relsXml = entries.has('xl/_rels/workbook.xml.rels')
    ? entries.get('xl/_rels/workbook.xml.rels').toString('utf8')
    : '';

  let sheetDefs = parseWorkbookSheets(workbookXml, relsXml);
  if (sheetDefs.length === 0) {
    for (const key of entries.keys()) {
      if (key.startsWith('xl/worksheets/sheet') && key.endsWith('.xml')) {
        sheetDefs.push({ name: path.basename(key, '.xml'), path: key, hidden: false });
      }
    }
  }

  const sheets = [];
  for (const def of sheetDefs) {
    const entry = entries.get(def.path);
    if (!entry) continue;
    const xml = entry.toString('utf8');
    const parsed = parseWorksheetXml(xml, sharedStrings, styles);
    sheets.push({
      name: def.name,
      hidden: Boolean(def.hidden),
      rowCount: parsed.rowCount,
      colCount: parsed.colCount,
      rows: parsed.rows,
    });
  }
  return sheets;
}

function stripTags(html) {
  return decodeXmlEntities(
    html
      .replace(/<br\s*\/?>/gi, ' ')
      .replace(/<[^>]+>/g, '')
      .replace(/\s+/g, ' ')
      .trim()
  );
}

function parseHtmlTables(html) {
  const sheets = [];
  const tabNames = [];
  const tabRegex = /<li\b[^>]*id="sheet-button-[^"]*"[^>]*>([\s\S]*?)<\/li>/gi;
  let tm;
  while ((tm = tabRegex.exec(html)) !== null) {
    tabNames.push(stripTags(tm[1]));
  }

  const tableRegex = /<table\b[^>]*>([\s\S]*?)<\/table>/gi;
  let tMatch;
  let idx = 0;
  while ((tMatch = tableRegex.exec(html)) !== null) {
    const tableInner = tMatch[1];
    const rows = [];
    let maxCols = 0;
    const trRegex = /<tr\b[^>]*>([\s\S]*?)<\/tr>/gi;
    let trMatch;
    while ((trMatch = trRegex.exec(tableInner)) !== null) {
      const trInner = trMatch[1];
      const cells = [];
      const tdRegex = /<(?:td|th)\b[^>]*>([\s\S]*?)<\/(?:td|th)>/gi;
      let tdMatch;
      while ((tdMatch = tdRegex.exec(trInner)) !== null) {
        cells.push(stripTags(tdMatch[1]));
      }
      while (cells.length > 0 && cells[cells.length - 1] === '') cells.pop();
      if (cells.some(c => c !== '')) {
        if (cells.length > maxCols) maxCols = cells.length;
        rows.push(cells);
      }
    }
    if (rows.length > 0) {
      sheets.push({
        name: tabNames[idx] || `Table_${idx + 1}`,
        hidden: false,
        rowCount: rows.length,
        colCount: maxCols,
        rows,
      });
    }
    idx++;
  }
  return sheets;
}

function toCsv(rows) {
  return rows
    .map(row =>
      row
        .map(cell => {
          const s = String(cell ?? '');
          if (s.includes(',') || s.includes('"') || s.includes('\n') || s.includes('\r')) {
            return `"${s.replace(/"/g, '""')}"`;
          }
          return s;
        })
        .join(',')
    )
    .join('\n');
}

function safeName(name) {
  return name.replace(/[^a-zA-Z0-9._-]+/g, '_').replace(/^_+|_+$/g, '').slice(0, 60) || 'sheet';
}

async function main() {
  await fs.mkdir(CSV_DIR, { recursive: true });
  await fs.mkdir(JSON_DIR, { recursive: true });
  await fs.mkdir(COMPILED_DIR, { recursive: true });

  const catalog = [];
  let totalWorkbooks = 0;
  let totalSheets = 0;
  let totalRows = 0;

  for (const src of SOURCES) {
    const base = path.join(RAW_DIR, src.id);
    const xlsxPath = `${base}.xlsx`;
    const htmlPath = `${base}.html`;
    const wbPath = `${base}.wayback.html`;
    const rssPath = `${base}.rss.xml`;

    const entry = {
      id: src.id,
      category: src.category,
      year: src.year,
      title: src.title,
      url: src.url,
      type: src.type,
      status: 'missing',
      sourceFormat: null,
      sheets: [],
    };

    try {
      if (fssync.existsSync(xlsxPath)) {
        const buf = await fs.readFile(xlsxPath);
        const sheets = parseXlsxBuffer(buf);
        entry.status = 'recovered';
        entry.sourceFormat = 'xlsx';
        entry.bytes = buf.length;
        totalWorkbooks++;

        const catCsvDir = path.join(CSV_DIR, src.category, src.id);
        await fs.mkdir(catCsvDir, { recursive: true });

        for (let i = 0; i < sheets.length; i++) {
          const sh = sheets[i];
          totalSheets++;
          totalRows += sh.rowCount;
          const csvFileName = `${String(i + 1).padStart(2, '0')}_${safeName(sh.name)}.csv`;
          let csvFilePath = null;
          if (sh.rowCount > 0) {
            csvFilePath = `csv/${src.category}/${src.id}/${csvFileName}`;
            await fs.writeFile(path.join(catCsvDir, csvFileName), toCsv(sh.rows), 'utf8');
          }
          entry.sheets.push({
            index: i + 1,
            name: sh.name,
            hidden: Boolean(sh.hidden),
            rowCount: sh.rowCount,
            colCount: sh.colCount,
            sampleHeader: sh.rows[0]?.slice(0, 12) || [],
            csvFile: csvFilePath,
          });
        }

        const catJsonDir = path.join(JSON_DIR, src.category);
        await fs.mkdir(catJsonDir, { recursive: true });
        await fs.writeFile(
          path.join(catJsonDir, `${src.id}.json`),
          JSON.stringify({ ...entry, fullSheets: sheets }, null, 2),
          'utf8'
        );
      } else if (fssync.existsSync(htmlPath) || fssync.existsSync(wbPath)) {
        const chosenPath = fssync.existsSync(wbPath) ? wbPath : htmlPath;
        const html = await fs.readFile(chosenPath, 'utf8');
        const sheets = parseHtmlTables(html);
        const titleMatch = /<title[^>]*>([\s\S]*?)<\/title>/i.exec(html);
        entry.pageTitle = titleMatch ? stripTags(titleMatch[1]) : null;

        if (fssync.existsSync(rssPath)) {
          const rss = await fs.readFile(rssPath, 'utf8');
          const videos = [];
          const entryRegex = /<entry>([\s\S]*?)<\/entry>/g;
          let em;
          while ((em = entryRegex.exec(rss)) !== null) {
            const block = em[1];
            const vid = /<yt:videoId>([^<]+)<\/yt:videoId>/.exec(block)?.[1];
            const vTitle = /<title>([\s\S]*?)<\/title>/.exec(block)?.[1];
            const pub = /<published>([^<]+)<\/published>/.exec(block)?.[1];
            const views = /<media:statistics views="(\d+)"/.exec(block)?.[1];
            videos.push({
              videoId: vid,
              title: decodeXmlEntities(vTitle),
              published: pub,
              views: views ? Number(views) : null,
              url: vid ? `https://www.youtube.com/watch?v=${vid}` : null,
            });
          }
          entry.status = 'recovered';
          entry.sourceFormat = 'youtube-rss';
          entry.videos = videos;
          const catJsonDir = path.join(JSON_DIR, src.category);
          await fs.mkdir(catJsonDir, { recursive: true });
          await fs.writeFile(path.join(catJsonDir, `${src.id}.json`), JSON.stringify(entry, null, 2), 'utf8');
        } else if (sheets.length > 0) {
          entry.status = 'recovered';
          entry.sourceFormat = chosenPath === wbPath ? 'wayback-html' : 'html';
          totalWorkbooks++;
          const catCsvDir = path.join(CSV_DIR, src.category, src.id);
          await fs.mkdir(catCsvDir, { recursive: true });
          for (let i = 0; i < sheets.length; i++) {
            const sh = sheets[i];
            totalSheets++;
            totalRows += sh.rowCount;
            const csvFileName = `${String(i + 1).padStart(2, '0')}_${safeName(sh.name)}.csv`;
            let csvFilePath = null;
            if (sh.rowCount > 0) {
              csvFilePath = `csv/${src.category}/${src.id}/${csvFileName}`;
              await fs.writeFile(path.join(catCsvDir, csvFileName), toCsv(sh.rows), 'utf8');
            }
            entry.sheets.push({
              index: i + 1,
              name: sh.name,
              hidden: false,
              rowCount: sh.rowCount,
              colCount: sh.colCount,
              sampleHeader: sh.rows[0]?.slice(0, 12) || [],
              csvFile: csvFilePath,
            });
          }
          const catJsonDir = path.join(JSON_DIR, src.category);
          await fs.mkdir(catJsonDir, { recursive: true });
          await fs.writeFile(
            path.join(catJsonDir, `${src.id}.json`),
            JSON.stringify({ ...entry, fullSheets: sheets }, null, 2),
            'utf8'
          );
        } else {
          entry.status = chosenPath === wbPath ? 'recovered-web-snapshot' : 'web-page';
          entry.sourceFormat = chosenPath === wbPath ? 'wayback-html' : 'html';
          const textPreview = stripTags(html.replace(/<script[\s\S]*?<\/script>/gi, '').replace(/<style[\s\S]*?<\/style>/gi, '')).slice(0, 2000);
          entry.textPreview = textPreview;
          const catJsonDir = path.join(JSON_DIR, src.category);
          await fs.mkdir(catJsonDir, { recursive: true });
          await fs.writeFile(path.join(catJsonDir, `${src.id}.json`), JSON.stringify(entry, null, 2), 'utf8');
        }
      }
    } catch (err) {
      entry.status = 'error';
      entry.error = err.message;
    }

    catalog.push(entry);
  }

  await fs.writeFile(path.join(COMPILED_DIR, 'catalog.json'), JSON.stringify({
    generatedAt: new Date().toISOString(),
    totalSources: SOURCES.length,
    recoveredWorkbooks: totalWorkbooks,
    totalSheets,
    totalRows,
    sources: catalog,
  }, null, 2), 'utf8');

  console.log(`Extracted ${totalWorkbooks} workbooks, ${totalSheets} sheets, ${totalRows} non-empty rows.`);
}

main().catch(err => {
  console.error(err);
  process.exit(1);
});
