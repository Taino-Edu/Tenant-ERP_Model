'use client'
// =============================================================================
// ImportarDados.tsx — Importação de CSV, reutilizável.
//
// POR QUE ESTE ARQUIVO EXISTE: a importação nasceu dentro de
// admin/lgpd/page.tsx, como contraparte do export de portabilidade. Isso faz
// sentido jurídico e nenhum sentido comercial — quem quer trazer o catálogo do
// sistema antigo não procura "LGPD". Ele abre Estoque, vê "Novo Produto" e
// conclui que precisa digitar os 800 itens na mão. O endpoint existia pronto no
// backend e era inalcançável na prática, do mesmo jeito que o push VAPID ficou
// implementado ponta a ponta sem nunca funcionar em produção.
//
// Ganhou também o modelo em branco: a instrução anterior era "use as mesmas
// colunas do export acima", que é circular numa loja recém-criada — ela não tem
// nada pra exportar ainda. Sem o modelo, a primeira importação depende de
// adivinhar nome de coluna.
//
// As colunas espelham CardGameStore/Controllers/ImportController.cs. Mudou lá,
// muda aqui — não há contrato publicado entre os dois.
// =============================================================================

import { useRef, useState } from 'react'
import { api, ImportResultDto, getErrorMessage } from '@/lib/api'
import { FileUp, Loader2, Package, Users, Wallet, Download, ChevronDown, ChevronUp } from 'lucide-react'
import toast from 'react-hot-toast'

export type TipoImportacao = 'produtos' | 'clientes' | 'crediario'

type Coluna = { nome: string; obrigatoria?: boolean; ajuda?: string }

const TIPOS: Record<TipoImportacao, {
  label: string
  icon: typeof Package
  colunas: Coluna[]
  nota?: string
}> = {
  produtos: {
    label: 'Produtos',
    icon: Package,
    colunas: [
      { nome: 'Nome',                         obrigatoria: true, ajuda: 'não pode repetir produto já cadastrado' },
      { nome: 'Categoria',                    obrigatoria: true },
      { nome: 'PrecoVenda',                   obrigatoria: true, ajuda: 'maior que zero — aceita 10,50 ou 10.50' },
      { nome: 'Descricao' },
      { nome: 'CodigoBarras' },
      { nome: 'PrecoCusto',                   ajuda: 'padrão 0' },
      { nome: 'PrecoPromocional' },
      { nome: 'Estoque',                      ajuda: 'padrão 0' },
      { nome: 'EstoqueMinimo',                ajuda: 'padrão 5' },
      { nome: 'NCM',                          ajuda: '8 dígitos' },
      { nome: 'CEST',                         ajuda: '7 dígitos' },
      { nome: 'TributosFederaisPercentual',   ajuda: '0 a 100' },
      { nome: 'TributosEstaduaisPercentual',  ajuda: '0 a 100' },
      { nome: 'TributosMunicipaisPercentual', ajuda: '0 a 100' },
      { nome: 'FonteTributos',                ajuda: 'até 100 caracteres' },
      { nome: 'Ativo',                        ajuda: 'padrão sim' },
      { nome: 'Destaque',                     ajuda: 'padrão não' },
    ],
  },
  clientes: {
    label: 'Clientes',
    icon: Users,
    colunas: [
      { nome: 'Nome',          obrigatoria: true },
      { nome: 'CPF',           ajuda: 'validado por dígito verificador; não pode já existir' },
      { nome: 'Email',         ajuda: 'não pode já existir' },
      { nome: 'WhatsApp' },
      { nome: 'SaldoPontos',   ajuda: 'padrão 0' },
      { nome: 'SaldoCashback', ajuda: 'padrão 0' },
      { nome: 'Ativo',         ajuda: 'padrão sim' },
    ],
  },
  crediario: {
    label: 'Crediário em aberto',
    icon: Wallet,
    nota: 'Importe os clientes primeiro — cada linha precisa achar um cliente já cadastrado pelo CPF ou e-mail.',
    colunas: [
      { nome: 'ClienteCPF',     obrigatoria: true, ajuda: 'ou ClienteEmail — um dos dois precisa achar o cliente' },
      { nome: 'ClienteEmail',   obrigatoria: true, ajuda: 'ou ClienteCPF' },
      { nome: 'ValorTotal',     obrigatoria: true, ajuda: 'maior que zero' },
      { nome: 'DataVencimento', obrigatoria: true, ajuda: 'formato AAAA-MM-DD' },
      { nome: 'ValorPago',      ajuda: 'padrão 0; não pode passar do ValorTotal' },
      { nome: 'DataAbertura',   ajuda: 'padrão hoje' },
      { nome: 'Observacao' },
    ],
  },
}

/** Larguras estáticas — Tailwind não gera classe montada em tempo de execução. */
const GRID = ['', 'grid-cols-1', 'grid-cols-1 sm:grid-cols-2', 'grid-cols-1 sm:grid-cols-3']

export default function ImportarDados({
  tipos = ['produtos', 'clientes', 'crediario'],
  titulo = 'Importar dados',
  descricao = 'Traga o que já existe no seu sistema atual em vez de digitar item por item. Linhas válidas entram — as com erro aparecem listadas pra você corrigir e reenviar só essas.',
  className = '',
}: {
  tipos?: TipoImportacao[]
  titulo?: string
  descricao?: string
  className?: string
}) {
  const [enviando, setEnviando]   = useState<TipoImportacao | null>(null)
  const [resultado, setResultado] = useState<{ tipo: TipoImportacao; data: ImportResultDto } | null>(null)
  const [colunasAbertas, setColunasAbertas] = useState<TipoImportacao | null>(null)
  const inputRefs = useRef<Record<string, HTMLInputElement | null>>({})

  async function enviar(tipo: TipoImportacao, file: File) {
    setEnviando(tipo)
    setResultado(null)
    try {
      const form = new FormData()
      form.append('arquivo', file)
      const { data } = await api.post<ImportResultDto>(`/api/import/${tipo}`, form, {
        headers: { 'Content-Type': 'multipart/form-data' },
      })
      setResultado({ tipo, data })
      if (data.importados > 0) toast.success(`${data.importados} de ${data.totalLinhas} linha(s) importada(s)!`)
      if (data.erros.length === 0 && data.importados === 0) toast('Arquivo vazio — nada pra importar.', { icon: 'ℹ️' })
    } catch (err) {
      toast.error(getErrorMessage(err, 'Erro ao importar dados'))
    } finally {
      setEnviando(null)
    }
  }

  /** Modelo só com o cabeçalho — a loja recém-criada não tem o que exportar,
   *  então "use as mesmas colunas do export" não serve de referência aqui.
   *  O BOM vem de fromCharCode e não como caractere no meio da string: literal
   *  ele fica invisível no editor e o primeiro formatador que passar o remove
   *  sem ninguém notar — e aí o Excel abre "Descrição" como "DescriÃ§Ã£o". */
  function baixarModelo(tipo: TipoImportacao) {
    const bomExcel = String.fromCharCode(0xfeff)
    const header = TIPOS[tipo].colunas.map(c => c.nome).join(',')
    const blob = new Blob([`${bomExcel}${header}\n`], { type: 'text/csv;charset=utf-8' })
    const url = URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url
    a.download = `modelo-${tipo}.csv`
    a.click()
    URL.revokeObjectURL(url)
  }

  return (
    <div className={`bg-surface-800 rounded-2xl border border-surface-500 p-4 ${className}`}>
      <div className="flex items-center gap-2 mb-1">
        <FileUp className="w-4 h-4 text-brand-400" />
        <h2 className="text-sm font-bold text-white">{titulo}</h2>
      </div>
      <p className="text-xs text-gray-400 mb-3">{descricao}</p>

      <div className={`grid gap-3 ${GRID[Math.min(tipos.length, 3)]}`}>
        {tipos.map(tipo => {
          const { label, icon: Icon, colunas, nota } = TIPOS[tipo]
          const obrigatorias = colunas.filter(c => c.obrigatoria)
          return (
            <div key={tipo} className="rounded-xl border border-surface-500 p-3">
              <input
                ref={el => { inputRefs.current[tipo] = el }}
                type="file" accept=".csv" className="hidden"
                onChange={e => { const f = e.target.files?.[0]; if (f) enviar(tipo, f); e.target.value = '' }}
              />
              <button
                onClick={() => inputRefs.current[tipo]?.click()}
                disabled={enviando !== null}
                className="w-full flex items-center gap-2.5 rounded-lg px-2 py-1.5 text-left transition-colors hover:bg-surface-700 disabled:opacity-50"
              >
                {enviando === tipo
                  ? <Loader2 className="w-4 h-4 text-brand-400 animate-spin shrink-0" />
                  : <Icon className="w-4 h-4 text-brand-400 shrink-0" />}
                <span className="min-w-0 flex-1">
                  <span className="block text-sm font-medium text-white">{label}</span>
                  <span className="block text-xs text-gray-500">Escolher arquivo CSV</span>
                </span>
              </button>

              {nota && <p className="mt-2 text-xs text-amber-400/90">{nota}</p>}

              <p className="mt-2 text-xs text-gray-500">
                Obrigatórias: <span className="text-gray-300">{obrigatorias.map(c => c.nome).join(', ')}</span>
              </p>

              <div className="mt-2 flex flex-wrap items-center gap-x-3 gap-y-1">
                <button
                  onClick={() => baixarModelo(tipo)}
                  className="inline-flex items-center gap-1 text-xs text-brand-400 hover:text-brand-300"
                >
                  <Download className="w-3 h-3" /> Baixar modelo
                </button>
                <button
                  onClick={() => setColunasAbertas(prev => (prev === tipo ? null : tipo))}
                  className="inline-flex items-center gap-1 text-xs text-gray-400 hover:text-gray-200"
                  aria-expanded={colunasAbertas === tipo}
                >
                  {colunasAbertas === tipo
                    ? <><ChevronUp className="w-3 h-3" /> Ocultar colunas</>
                    : <><ChevronDown className="w-3 h-3" /> Ver colunas aceitas</>}
                </button>
              </div>

              {colunasAbertas === tipo && (
                <ul className="mt-2 space-y-1 border-t border-surface-600 pt-2">
                  {colunas.map(c => (
                    <li key={c.nome} className="text-xs text-gray-400">
                      <span className={c.obrigatoria ? 'font-semibold text-white' : 'text-gray-300'}>{c.nome}</span>
                      {c.obrigatoria && <span className="text-brand-400"> *</span>}
                      {c.ajuda && <span className="text-gray-500"> — {c.ajuda}</span>}
                    </li>
                  ))}
                </ul>
              )}
            </div>
          )
        })}
      </div>

      {resultado && (
        <div className="mt-3 pt-3 border-t border-surface-600">
          <p className="text-xs text-gray-300 mb-2">
            <span className="text-white font-semibold">{TIPOS[resultado.tipo].label}:</span>{' '}
            {resultado.data.importados} de {resultado.data.totalLinhas} linha(s) importada(s)
            {resultado.data.erros.length > 0 && `, ${resultado.data.erros.length} com erro`}.
          </p>
          {resultado.data.erros.length > 0 && (
            <div className="bg-surface-900 rounded-lg p-2.5 max-h-40 overflow-y-auto space-y-1">
              {resultado.data.erros.map((e, i) => (
                <p key={i} className="text-xs text-red-400">
                  <span className="text-gray-500">Linha {e.linha}:</span> {e.motivo}
                </p>
              ))}
            </div>
          )}
        </div>
      )}
    </div>
  )
}
