#!/usr/bin/env node
// Checks that a running site sends browsers exactly what the repo built (docs/reproducible-builds.md):
//
//   node verify-site.mjs [https://grifball.xyz] [--path /seasons ...] [--manifest build-manifest.json]
//
// 1. Fetches the site's /build-manifest.json and has the GitHub CLI check its attestation: signed by this repo's
//    docker-publish.yml, on master. That gives the commit it was built from. (--manifest instead: a local manifest,
//    attestation not checked; for CI.)
// 2. Fetches every file in the manifest from the site and compares sha256s.
// 3. Fetches the pages (/ and each --path) and checks that every script they run is the build's: <script src> and
//    stylesheets/preloads are files in the manifest, inline scripts are the build's (manifest inlineScripts) or one of
//    the two the server writes per page (the loader from src/app/afterFirstPaint.ts and Angular's event replay
//    bootstrap), and the only inline event handler is the stylesheet swap (onload="this.media='all'").
//
// It proves what this machine was sent. A server could send someone else something else; anyone can run this.
import { execFileSync } from 'node:child_process';
import { createHash } from 'node:crypto';
import { mkdtempSync, readFileSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { inlineScripts } from './build-manifest.mjs';

const REPO = 'PlayGrifball/GrifballWebApp';
const WORKFLOW = `${REPO}/.github/workflows/docker-publish.yml`;

const sha256 = data => createHash('sha256').update(data).digest('hex');

export function parseArgs(argv) {
  const options = { site: 'https://grifball.xyz', paths: ['/'], manifest: null };
  for (let i = 0; i < argv.length; i++) {
    if (argv[i] === '--path') options.paths.push(argv[++i]);
    else if (argv[i] === '--manifest') options.manifest = argv[++i];
    else options.site = argv[i];
  }
  options.site = options.site.replace(/\/+$/, '');
  return options;
}

// The loader src/app/afterFirstPaint.ts writes in place of the module scripts: its text with each import(...) of a
// script swapped for a placeholder has to be exactly the loader's, or it isn't the loader.
const LOADER_IMPORT = /import\(new URL\(("(?:[^"\\]|\\.)*"), document\.baseURI\)\.href\)/g;
const LOADER_START = 'let started = false; const start = () => { if (!started) { started = true; ';
const LOADER_END = '; } }; requestAnimationFrame(() => setTimeout(start)); setTimeout(start, 1000);';

export function loaderSources(script) {
  const sources = [...script.matchAll(LOADER_IMPORT)].map(match => JSON.parse(match[1]));
  if (sources.length === 0) return null;
  const shape = LOADER_START + sources.map((_, i) => (i === 0 ? '@' : '.then(() => @)')).join('') + LOADER_END;
  return script.replace(LOADER_IMPORT, '@') === shape ? sources : null;
}

// Angular's event replay (withEventReplay) starts recording clicks with a call naming the page's event types: data only.
const EVENT_LIST = String.raw`\[\s*(?:"[a-z]+"\s*(?:,\s*"[a-z]+"\s*)*)?\]`;
const JSACTION_BOOTSTRAP = new RegExp(
  String.raw`^\s*window\.__jsaction_bootstrap\(\s*document\.body\s*,\s*"[\w-]+"\s*,\s*${EVENT_LIST}\s*,\s*${EVENT_LIST}\s*\);?\s*$`);

const ALLOWED_HANDLERS = new Set(["this.media='all'"]);

/** What in `html` the build didn't make: one message per problem, none when it's all the build's. */
export function checkPage(html, manifest, site) {
  const problems = [];
  const files = new Set(Object.keys(manifest.files));
  const inline = new Set(manifest.inlineScripts);
  const origin = new URL(site).origin;
  const local = (url, what) => {
    const resolved = new URL(url, site + '/');
    if (resolved.origin !== origin) {
      problems.push(`${what} from another site: ${resolved.href}`);
      return;
    }
    const path = decodeURIComponent(resolved.pathname.replace(/^\/+/, ''));
    if (!files.has(path)) problems.push(`${what} not in the build: /${path}`);
  };

  for (const [, attributes] of html.matchAll(/<script\b([^>]*)>/gi)) {
    const src = /\bsrc\s*=\s*["']([^"']+)["']/i.exec(attributes);
    if (src) local(src[1], 'script');
  }
  for (const [tag] of html.matchAll(/<link\b[^>]*>/gi)) {
    const rel = /\brel\s*=\s*["']([^"']+)["']/i.exec(tag)?.[1].toLowerCase() ?? '';
    const href = /\bhref\s*=\s*["']([^"']+)["']/i.exec(tag)?.[1];
    if (href && /\b(stylesheet|modulepreload|preload|prefetch)\b/.test(rel)) local(href, `${rel} link`);
  }
  for (const body of inlineScripts(html)) {
    if (inline.has(sha256(body))) continue;
    const sources = loaderSources(body);
    if (sources) {
      for (const src of sources) local(src, 'script the loader imports');
      continue;
    }
    if (JSACTION_BOOTSTRAP.test(body)) continue;
    problems.push(`inline script not in the build (sha256 ${sha256(body)}): ${body.slice(0, 120)}`);
  }
  for (const [, name, value] of html.matchAll(/\s(on[a-z]+)\s*=\s*"([^"]*)"/gi)) {
    if (!ALLOWED_HANDLERS.has(value)) problems.push(`inline ${name} handler not in the build: ${value.slice(0, 120)}`);
  }
  if (/\b(?:href|src|action)\s*=\s*["']\s*javascript:/i.test(html)) problems.push('a javascript: URL');
  return problems;
}

async function get(url) {
  const response = await fetch(url, { redirect: 'follow' });
  if (!response.ok) throw new Error(`${url}: HTTP ${response.status}`);
  return Buffer.from(await response.arrayBuffer());
}

function attestedCommit(manifestPath) {
  const out = execFileSync('gh', ['attestation', 'verify', manifestPath, '--repo', REPO,
    '--signer-workflow', WORKFLOW, '--source-ref', 'refs/heads/master', '--format', 'json'], { encoding: 'utf8' });
  return JSON.parse(out)[0]?.verificationResult?.signature?.certificate?.sourceRepositoryDigest ?? 'unknown';
}

async function main() {
  const options = parseArgs(process.argv.slice(2));
  const problems = [];

  let manifestBytes;
  if (options.manifest) {
    manifestBytes = readFileSync(options.manifest);
    console.log(`Manifest: ${options.manifest} (its attestation not checked)`);
    const served = await get(`${options.site}/build-manifest.json`);
    if (sha256(served) !== sha256(manifestBytes)) problems.push('the site serves another build-manifest.json');
  } else {
    manifestBytes = await get(`${options.site}/build-manifest.json`);
    const path = join(mkdtempSync(join(tmpdir(), 'verify-site-')), 'build-manifest.json');
    writeFileSync(path, manifestBytes);
    const commit = attestedCommit(path);
    console.log(`Manifest attested: built by ${WORKFLOW} on master, commit ${commit}`);
    console.log(`  https://github.com/${REPO}/commit/${commit}`);
  }
  const manifest = JSON.parse(manifestBytes.toString('utf8'));

  const entries = Object.entries(manifest.files);
  let next = 0;
  await Promise.all(Array.from({ length: 8 }, async () => {
    while (next < entries.length) {
      const [path, hash] = entries[next++];
      try {
        const body = await get(`${options.site}/${path.split('/').map(encodeURIComponent).join('/')}`);
        if (sha256(body) !== hash) problems.push(`/${path} differs from the build`);
      } catch (error) {
        problems.push(`/${path}: ${error.message}`);
      }
    }
  }));
  console.log(`Files: ${entries.length} checked`);

  for (const path of options.paths) {
    const html = (await get(options.site + path)).toString('utf8');
    const found = checkPage(html, manifest, options.site);
    problems.push(...found.map(problem => `${path}: ${problem}`));
    console.log(`Page ${path}: ${found.length === 0 ? 'only the build\'s scripts' : `${found.length} problems`}`);
  }

  if (problems.length > 0) {
    console.error(`\nNOT the build (${problems.length}):`);
    for (const problem of problems) console.error(`  ${problem}`);
    process.exit(1);
  }
  console.log('\nVerified: everything checked is what the attested build produced.');
}

if (import.meta.url === `file://${process.argv[1]}`) {
  main().catch(error => {
    console.error(error.message);
    process.exit(1);
  });
}
