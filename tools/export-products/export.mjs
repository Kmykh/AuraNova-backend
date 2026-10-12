// Exporta el catálogo actual (productos, categorías, campañas) y descarga las imágenes
// de Supabase Storage para migrarlos a otra base de datos / storage.
//
// Uso:
//   cd tools/export-products && npm install
//   SOURCE_DATABASE_URL="postgresql://user:pass@host:5432/postgres" \
//   SUPABASE_PUBLIC_BASE="https://<project>.supabase.co/storage/v1/object/public/payment-evidence" \
//   node export.mjs [carpeta-destino]
//
// Solo ejecuta SELECT. No modifica la base de datos de origen.

import pg from 'pg';
import { createHash } from 'node:crypto';
import { mkdir, writeFile } from 'node:fs/promises';
import path from 'node:path';

const dbUrl = process.env.SOURCE_DATABASE_URL;
const publicBase = (process.env.SUPABASE_PUBLIC_BASE ?? '').replace(/\/+$/, '');
if (!dbUrl || !publicBase) {
  console.error('Faltan SOURCE_DATABASE_URL y/o SUPABASE_PUBLIC_BASE.');
  process.exit(1);
}

const stamp = new Date().toISOString().slice(0, 10);
const outDir = path.resolve(process.argv[2] ?? `../../../exports/catalogo-${stamp}`);
const imagesDir = path.join(outDir, 'images');
await mkdir(imagesDir, { recursive: true });

// Las tablas que forman el catálogo, en orden de dependencia (FK) para el seed.
const TABLES = [
  ['Categories', 'ORDER BY "CreatedAt"'],
  ['Products', 'ORDER BY "CreatedAt"'],
  ['Campaigns', 'ORDER BY "StartDate"'],
  ['CampaignStages', 'ORDER BY "CampaignId", "StartDate"'],
  ['CampaignProducts', 'ORDER BY "CampaignId"'],
  ['CampaignProductStagePrices', 'ORDER BY "CampaignProductId"'],
];

const client = new pg.Client({ connectionString: dbUrl, ssl: { rejectUnauthorized: false } });
await client.connect();
await client.query('SET default_transaction_read_only = on');

const data = {};
for (const [table, order] of TABLES) {
  const { rows } = await client.query(`SELECT * FROM "${table}" ${order}`);
  data[table] = rows;
  console.log(`${table}: ${rows.length} filas`);
}
await client.end();

// --- Imágenes ---
const isAbsolute = (u) => /^https?:\/\//i.test(u);
const manifest = [];
for (const p of data.Products) {
  if (!p.ImageUrl) continue;
  const url = isAbsolute(p.ImageUrl) ? p.ImageUrl : `${publicBase}/${p.ImageUrl.replace(/^\/+/, '')}`;
  const entry = { productId: p.Id, productName: p.Name, storagePath: p.ImageUrl, url };
  try {
    const res = await fetch(url);
    if (!res.ok) throw new Error(`HTTP ${res.status}`);
    const buf = Buffer.from(await res.arrayBuffer());
    const ext = path.extname(new URL(url).pathname) || '.bin';
    const file = `${p.Id}${ext}`;
    await writeFile(path.join(imagesDir, file), buf);
    Object.assign(entry, {
      file: `images/${file}`,
      bytes: buf.length,
      contentType: res.headers.get('content-type'),
      cacheControl: res.headers.get('cache-control'),
      sha256: createHash('sha256').update(buf).digest('hex'),
    });
  } catch (err) {
    entry.error = String(err.message ?? err);
  }
  manifest.push(entry);
  console.log(`${entry.error ? 'ERROR' : 'ok   '} ${p.Name} ${entry.bytes ?? ''} ${entry.error ?? ''}`);
}

// --- Salidas ---
const json = (v) => JSON.stringify(v, null, 2);
const categoriesById = Object.fromEntries(data.Categories.map((c) => [c.Id, c]));
const products = data.Products.map((p) => ({
  ...p,
  AvailableColors: safeJson(p.AvailableColors),
  AvailableFlowerTypes: safeJson(p.AvailableFlowerTypes),
  Category: p.CategoryId ? categoriesById[p.CategoryId] ?? null : null,
  Image: manifest.find((m) => m.productId === p.Id) ?? null,
}));

await writeFile(path.join(outDir, 'products.json'), json(products));
await writeFile(path.join(outDir, 'catalog-raw.json'), json(data));
await writeFile(path.join(outDir, 'images-manifest.json'), json(manifest));
await writeFile(path.join(outDir, 'products.csv'), toCsv(products));
await writeFile(path.join(outDir, 'seed.sql'), toSeedSql(data));

const totalBytes = manifest.reduce((s, m) => s + (m.bytes ?? 0), 0);
console.log(`\nExportado en ${outDir}`);
console.log(`Productos: ${products.length} | Imágenes: ${manifest.filter((m) => m.bytes).length} | ${(totalBytes / 1024 / 1024).toFixed(2)} MB`);

function safeJson(v) {
  if (typeof v !== 'string') return v ?? [];
  try { return JSON.parse(v); } catch { return []; }
}

function toCsv(items) {
  const cols = ['Id', 'Name', 'Description', 'Price', 'Stock', 'IsAvailable', 'Audience', 'CategoryName',
    'AvailableColors', 'AvailableFlowerTypes', 'AllowsLights', 'AllowsButterfly', 'AllowsPhraseCard',
    'ImageUrl', 'ImageFile', 'ImageBytes', 'CreatedAt'];
  const cell = (v) => {
    if (v === null || v === undefined) return '';
    const s = v instanceof Date ? v.toISOString() : Array.isArray(v) ? v.join('|') : String(v);
    return /[",\n]/.test(s) ? `"${s.replace(/"/g, '""')}"` : s;
  };
  const lines = items.map((p) => [p.Id, p.Name, p.Description, p.Price, p.Stock, p.IsAvailable, p.Audience,
    p.Category?.Name, p.AvailableColors, p.AvailableFlowerTypes, p.AllowsLights, p.AllowsButterfly,
    p.AllowsPhraseCard, p.ImageUrl, p.Image?.file, p.Image?.bytes, p.CreatedAt].map(cell).join(','));
  return '﻿' + [cols.join(','), ...lines].join('\n');
}

// Genera INSERTs compatibles con el esquema de las migraciones EF (PostgreSQL).
function toSeedSql(tables) {
  const lit = (v) => {
    if (v === null || v === undefined) return 'NULL';
    if (typeof v === 'boolean') return v ? 'TRUE' : 'FALSE';
    if (typeof v === 'number') return String(v);
    if (v instanceof Date) return `'${v.toISOString()}'`;
    if (typeof v === 'object') return `'${JSON.stringify(v).replace(/'/g, "''")}'`;
    return `'${String(v).replace(/'/g, "''")}'`;
  };
  const out = ['-- Seed del catálogo AuraNova', `-- Generado: ${new Date().toISOString()}`, 'BEGIN;'];
  for (const [table] of TABLES) {
    for (const row of tables[table]) {
      const cols = Object.keys(row);
      out.push(`INSERT INTO "${table}" (${cols.map((c) => `"${c}"`).join(', ')}) VALUES (${cols.map((c) => lit(row[c])).join(', ')}) ON CONFLICT ("Id") DO NOTHING;`);
    }
  }
  out.push('COMMIT;');
  return out.join('\n') + '\n';
}
