import fs from 'node:fs/promises';
import path from 'node:path';
import zlib from 'node:zlib';

const BASE_DIR = path.resolve('data/historical-archive');
const RAW_DIR = path.join(BASE_DIR, 'raw');
const COMPILED_DIR = path.join(BASE_DIR, 'compiled');

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
  if (eocdOffset === -1) throw new Error('Invalid ZIP');
  const cdCount = buf.readUInt16LE(eocdOffset + 10);
  const cdOffset = buf.readUInt32LE(eocdOffset + 16);
  let ptr = cdOffset;
  const unzipErrors = [];
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
    const lhNameLen = buf.readUInt16LE(localHeaderOffset + 26);
    const lhExtraLen = buf.readUInt16LE(localHeaderOffset + 28);
    const dataStart = localHeaderOffset + 30 + lhNameLen + lhExtraLen;
    const slice = buf.subarray(dataStart, dataStart + compSize);
    if (method === 0) entries.set(name, slice);
    else if (method === 8) {
      try {
        entries.set(name, zlib.inflateRawSync(slice));
      } catch (e) {
        unzipErrors.push({ name, error: e.message });
      }
    }
  }
  return { entries, unzipErrors };
}

function parseStyles(stylesXml) {
  if (!stylesXml) return { numFmts: new Map(), cellXfs: [] };
  const numFmts = new Map([
    [14, 'yyyy-mm-dd'],
    [15, 'd-mmm-yy'],
    [16, 'd-mmm'],
    [17, 'mmm-yy'],
    [18, 'h:mm AM/PM'],
    [19, 'h:mm:ss AM/PM'],
    [20, 'h:mm'],
    [21, 'h:mm:ss'],
    [22, 'm/d/yy h:mm'],
    [45, 'mm:ss'],
    [46, '[h]:mm:ss'],
    [47, 'mmss.0'],
  ]);
  const nfRegex = /<numFmt\b([^>]+)\/?>/g;
  let m;
  while ((m = nfRegex.exec(stylesXml)) !== null) {
    const idM = /\bnumFmtId="(\d+)"/.exec(m[1]);
    const codeM = /\bformatCode="([^"]+)"/.exec(m[1]);
    if (idM && codeM) numFmts.set(Number(idM[1]), codeM[1]);
  }
  const cellXfs = [];
  const xfsBlock = /<cellXfs\b[^>]*>([\s\S]*?)<\/cellXfs>/.exec(stylesXml);
  if (xfsBlock) {
    const xfRegex = /<xf\b([^>]*?)(?:\/>|>[\s\S]*?<\/xf>)/g;
    while ((m = xfRegex.exec(xfsBlock[1])) !== null) {
      const nfM = /\bnumFmtId="(\d+)"/.exec(m[1]);
      const nfId = nfM ? Number(nfM[1]) : 0;
      cellXfs.push({ numFmtId: nfId, formatCode: numFmts.get(nfId) || '' });
    }
  }
  return { numFmts, cellXfs };
}

function isDateOrTimeFormat(numFmtId, formatCode) {
  if ([14, 15, 16, 17, 18, 19, 20, 21, 22, 45, 46, 47].includes(numFmtId)) return true;
  if (!formatCode) return false;
  const cleaned = formatCode.replace(/"[^"]*"/g, '').replace(/\\./g, '').replace(/\[[^\]]*\]/g, (b) => {
    return /^\[[hmsHMS]+\]$/.test(b) ? 'h' : '';
  });
  return /[ymdhsYMDHS]/.test(cleaned) && !/[#0]/.test(cleaned);
}

async function main() {
  const files = await fs.readdir(RAW_DIR);
  const xlsxFiles = files.filter(f => f.endsWith('.xlsx')).sort();

  const report = {
    totalXlsxFiles: xlsxFiles.length,
    unzipErrors: [],
    dateOrTimeFormattedCellsCount: 0,
    dateOrTimeWorkbooks: [],
    trailingDotZeroCount: 0,
    multiTInlineStrCount: 0,
    formulaErrorCells: [],
    discoveredUrlsAndSheetIds: [],
    hiddenSheets: [],
  };

  for (const file of xlsxFiles) {
    const id = file.replace(/\.xlsx$/, '');
    const buf = await fs.readFile(path.join(RAW_DIR, file));
    const { entries, unzipErrors } = unzipBuffer(buf);
    if (unzipErrors.length > 0) {
      report.unzipErrors.push({ id, unzipErrors });
    }

    const stylesXml = entries.has('xl/styles.xml') ? entries.get('xl/styles.xml').toString('utf8') : '';
    const { cellXfs } = parseStyles(stylesXml);

    const workbookXml = entries.has('xl/workbook.xml') ? entries.get('xl/workbook.xml').toString('utf8') : '';
    const relsXml = entries.has('xl/_rels/workbook.xml.rels') ? entries.get('xl/_rels/workbook.xml.rels').toString('utf8') : '';

    // Check hidden sheets
    const sheetTagRegex = /<sheet\b([^>]+)\/?>/g;
    let sm;
    const relMap = new Map();
    const relRegex = /<Relationship\b([^>]+)\/?>/g;
    while ((sm = relRegex.exec(relsXml)) !== null) {
      const idM = /\bId="([^"]+)"/.exec(sm[1]);
      const targetM = /\bTarget="([^"]+)"/.exec(sm[1]);
      if (idM && targetM) relMap.set(idM[1], targetM[1]);
    }

    const sheets = [];
    let idx = 1;
    while ((sm = sheetTagRegex.exec(workbookXml)) !== null) {
      const attrs = sm[1];
      const nameM = /\bname="([^"]+)"/.exec(attrs);
      const stateM = /\bstate="([^"]+)"/.exec(attrs);
      const rIdM = /\br:id="([^"]+)"/.exec(attrs);
      const name = nameM ? nameM[1] : `Sheet${idx}`;
      const state = stateM ? stateM[1] : 'visible';
      if (state !== 'visible') {
        report.hiddenSheets.push({ workbook: id, sheet: name, state });
      }
      let target = (rIdM && relMap.get(rIdM[1])) || `worksheets/sheet${idx}.xml`;
      target = target.replace(/^\/+/, '');
      if (!target.startsWith('xl/')) target = `xl/${target}`;
      sheets.push({ name, path: target, state });
      idx++;
    }

    let wbDateCells = 0;
    const wbDateSamples = [];

    for (const sh of sheets) {
      const entry = entries.get(sh.path);
      if (!entry) continue;
      const xml = entry.toString('utf8');

      // Search for URLs / Google Sheet IDs in XML (formulas, hyperlinks, etc.)
      const urlRegex = /https?:\/\/[^\s"<>&]+/g;
      let um;
      while ((um = urlRegex.exec(xml)) !== null) {
        const u = um[0];
        if (!u.includes('schemas.openxmlformats.org') && !u.includes('purl.org') && !u.includes('w3.org') && !u.includes('microsoft.com')) {
          report.discoveredUrlsAndSheetIds.push({ workbook: id, sheet: sh.name, url: u });
        }
      }

      // Scan cells
      const cellRegex = /<c\b([^>]*?)(?:\/>|>([\s\S]*?)<\/c>)/g;
      let cm;
      while ((cm = cellRegex.exec(xml)) !== null) {
        const attrs = cm[1];
        const inner = cm[2] || '';
        const rAttr = /\br="([A-Z]+\d+)"/.exec(attrs);
        const tAttr = /\bt="([^"]+)"/.exec(attrs);
        const sAttr = /\bs="(\d+)"/.exec(attrs);
        const ref = rAttr ? rAttr[1] : '?';
        const type = tAttr ? tAttr[1] : '';
        const styleIdx = sAttr ? Number(sAttr[1]) : -1;

        if (type === 'inlineStr') {
          const tMatches = inner.match(/<t(?:\s+[^>]*)?>/g);
          if (tMatches && tMatches.length > 1) {
            report.multiTInlineStrCount++;
          }
        }

        const vM = /<v>([\s\S]*?)<\/v>/.exec(inner);
        const fM = /<f\b[^>]*>([\s\S]*?)<\/f>/.exec(inner);
        const rawV = vM ? vM[1] : '';

        if (type === 'e' || /^#(REF!|DIV\/0!|VALUE!|NAME\?|N\/A|NUM!|NULL!)$/.test(rawV)) {
          report.formulaErrorCells.push({
            workbook: id,
            sheet: sh.name,
            cell: ref,
            error: rawV,
            formula: fM ? fM[1].slice(0, 120) : null,
          });
        }

        if (!type && rawV) {
          if (/^-?\d+\.0$/.test(rawV)) {
            report.trailingDotZeroCount++;
          }
          if (styleIdx >= 0 && cellXfs[styleIdx]) {
            const xf = cellXfs[styleIdx];
            if (isDateOrTimeFormat(xf.numFmtId, xf.formatCode)) {
              wbDateCells++;
              report.dateOrTimeFormattedCellsCount++;
              if (wbDateSamples.length < 5) {
                wbDateSamples.push({ sheet: sh.name, cell: ref, rawV, numFmtId: xf.numFmtId, formatCode: xf.formatCode });
              }
            }
          }
        }
      }
    }

    if (wbDateCells > 0) {
      report.dateOrTimeWorkbooks.push({ workbook: id, count: wbDateCells, samples: wbDateSamples });
    }
  }

  // Deduplicate discovered URLs
  const uniqueUrls = [];
  const seenUrlKey = new Set();
  for (const item of report.discoveredUrlsAndSheetIds) {
    const k = `${item.workbook}|${item.sheet}|${item.url}`;
    if (!seenUrlKey.has(k)) {
      seenUrlKey.add(k);
      uniqueUrls.push(item);
    }
  }
  report.discoveredUrlsAndSheetIds = uniqueUrls;

  // Summarize formula errors by workbook+sheet+error
  const errSummary = {};
  for (const e of report.formulaErrorCells) {
    const k = `${e.workbook} :: ${e.sheet} :: ${e.error}`;
    if (!errSummary[k]) errSummary[k] = { count: 0, sampleCell: e.cell, sampleFormula: e.formula };
    errSummary[k].count++;
  }

  console.log(JSON.stringify({
    totalXlsxFiles: report.totalXlsxFiles,
    unzipErrors: report.unzipErrors,
    dateOrTimeFormattedCellsCount: report.dateOrTimeFormattedCellsCount,
    dateOrTimeWorkbooksCount: report.dateOrTimeWorkbooks.length,
    dateOrTimeWorkbooks: report.dateOrTimeWorkbooks,
    trailingDotZeroCount: report.trailingDotZeroCount,
    multiTInlineStrCount: report.multiTInlineStrCount,
    hiddenSheetsCount: report.hiddenSheets.length,
    hiddenSheets: report.hiddenSheets,
    formulaErrorsTotalCells: report.formulaErrorCells.length,
    formulaErrorsSummary: errSummary,
    discoveredUrlsAndSheetIds: report.discoveredUrlsAndSheetIds,
  }, null, 2));
}

main().catch(err => {
  console.error(err);
  process.exit(1);
});
