/**
 * patch-next.js
 *
 * Corrige um bug do Node.js v24+ com caminhos Unicode no Windows.
 * O ESM dynamic import() falha quando o caminho contém caracteres especiais
 * como "Área de Trabalho" (convertido para %C3%81rea na URL do arquivo).
 *
 * Este script substitui o import() dinâmico pelo require() do CommonJS,
 * que não tem esse problema de resolução de caminho Unicode.
 *
 * Executado automaticamente pelo postinstall do npm.
 */

const fs = require('fs');
const path = require('path');

const nextBinPath = path.join(__dirname, '..', 'node_modules', 'next', 'dist', 'bin', 'next');

if (!fs.existsSync(nextBinPath)) {
  console.log('[patch-next] Arquivo next/dist/bin/next não encontrado, pulando patch.');
  process.exit(0);
}

let content = fs.readFileSync(nextBinPath, 'utf8');

// Verifica se o patch já foi aplicado
if (content.includes('Promise.resolve(require("../cli/next-dev.js"))')) {
  console.log('[patch-next] Patch já aplicado. OK.');
  process.exit(0);
}

// Aplica o patch: substitui import() ESM por require() CJS para next-dev
const original = 'import("../cli/next-dev.js").then((mod)=>mod.nextDev(options, portSource, directory))';
const patched  = 'Promise.resolve(require("../cli/next-dev.js")).then((mod)=>mod.nextDev(options, portSource, directory))';

if (!content.includes(original)) {
  console.log('[patch-next] AVISO: Trecho original não encontrado. Versão do Next.js pode ter mudado.');
  console.log('[patch-next] Patch não aplicado.');
  process.exit(0);
}

content = content.replace(original, patched);
fs.writeFileSync(nextBinPath, content, 'utf8');
console.log('[patch-next] Patch aplicado com sucesso! (import -> require para next-dev)');
