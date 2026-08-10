const { cpSync, existsSync } = require('node:fs')
const { resolve } = require('node:path')

const projectRoot = resolve(__dirname, '..')
const standaloneRoot = resolve(projectRoot, '.next', 'standalone')
const serverEntry = resolve(standaloneRoot, 'server.js')

if (!existsSync(serverEntry)) {
  throw new Error('Build standalone não encontrado. Execute "npm run build" primeiro.')
}

// Replica as duas cópias feitas pelo Dockerfile da imagem de produção.
cpSync(resolve(projectRoot, 'public'), resolve(standaloneRoot, 'public'), {
  recursive: true,
  force: true,
})
cpSync(resolve(projectRoot, '.next', 'static'), resolve(standaloneRoot, '.next', 'static'), {
  recursive: true,
  force: true,
})

process.chdir(standaloneRoot)
require(serverEntry)
