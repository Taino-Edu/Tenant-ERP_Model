// =============================================================================
// IbptTabelaCsvImporter.cs — importa a tabela do "De Olho no Imposto" a partir
// do CSV que o próprio IBPT entrega, sem depender da API.
//
// Por que existe: a API (`apidoni.ibpt.org.br`) ficou fora do ar, e verificamos
// de três redes independentes — VPS, navegador do lojista e outro ambiente —
// que não era bloqueio nem rede local. Enquanto ela não volta, nenhum produto
// recebe transparência tributária e nenhuma NFC-e desses produtos é emitida.
//
// O arquivo, porém, o lojista já tem: o IBPT entrega um pacote por CNPJ com o
// `TabelaIBPTax{UF}{versao}.csv` dentro. Importar esse arquivo produz exatamente
// as mesmas linhas que a API produziria — e de uma vez, para os ~12 mil NCMs,
// em vez de uma consulta por NCM.
//
// Isso deixa de ser contorno e vira o caminho robusto: uma importação protege a
// loja de qualquer indisponibilidade futura, porque a tabela local passa a ser
// a fonte e a API vira só o mecanismo de atualização.
//
// Formato verificado no arquivo real (SP 26.1.L, 12.163 linhas):
//
//   codigo;ex;tipo;descricao;nacionalfederal;importadosfederal;estadual;
//   municipal;vigenciainicio;vigenciafim;chave;versao;fonte
//   61091000;;0;"Camisetas...";13.45;18.61;18.00;0.00;20/06/2026;31/08/2026;42CA5A;26.1.L;IBPT/...
//
// UTF-8, separador `;`, decimal com PONTO, data dd/MM/yyyy, descrição entre
// aspas (contém vírgulas). O CSV NÃO traz a UF — ela só existe no nome do
// arquivo, e é por isso que a validação de UF acontece fora daqui.
// =============================================================================

using System.Globalization;

namespace CardGameStore.Services.Implementations;

/// <summary>Uma linha útil da tabela, já convertida.</summary>
public sealed record LinhaTabelaIbpt(
    string Ncm,
    decimal NacionalFederal,
    decimal ImportadoFederal,
    decimal Estadual,
    decimal Municipal,
    DateTime? VigenciaInicio,
    DateTime? VigenciaFim,
    string? Chave,
    string? Versao,
    string? Fonte);

public sealed record ResultadoLeituraCsvIbpt(
    IReadOnlyList<LinhaTabelaIbpt> Linhas,
    string? Versao,
    DateTime? VigenciaInicio,
    DateTime? VigenciaFim,
    int LinhasIgnoradas);

public static class IbptTabelaCsvImporter
{
    private const string CabecalhoEsperado = "codigo";

    /// <summary>Só NCM interessa à NFC-e. O arquivo também traz NBS (serviços) e
    /// itens da LC 116, que não têm uso aqui.</summary>
    private const string TipoNcm = "0";

    /// <summary>
    /// Extrai a UF do nome do arquivo (`TabelaIBPTaxSP26.1.L.csv` → `SP`).
    ///
    /// A UF não está no conteúdo — só no nome. Sem esta checagem, uma loja de
    /// Minas poderia importar a tabela de São Paulo e passar a emitir com
    /// alíquota estadual errada, sem nada denunciando. Devolve null quando o
    /// nome não segue o padrão, e aí quem decide é o chamador.
    /// </summary>
    public static string? UfDoNomeDoArquivo(string? nomeArquivo)
    {
        if (string.IsNullOrWhiteSpace(nomeArquivo)) return null;

        var nome = Path.GetFileNameWithoutExtension(nomeArquivo);
        const string prefixo = "TabelaIBPTax";
        var i = nome.IndexOf(prefixo, StringComparison.OrdinalIgnoreCase);
        if (i < 0) return null;

        var resto = nome[(i + prefixo.Length)..];
        return resto.Length >= 2 && resto[..2].All(char.IsLetter)
            ? resto[..2].ToUpperInvariant()
            : null;
    }

    /// <summary>
    /// Lê o CSV inteiro em memória convertida. Linha malformada é ignorada, não
    /// aborta o arquivo: 12 mil linhas de terceiro sempre podem ter uma torta, e
    /// perder a tabela toda por causa de uma seria pior. A contagem de ignoradas
    /// volta no resultado para o lojista saber que houve.
    /// </summary>
    public static ResultadoLeituraCsvIbpt Ler(Stream conteudo)
    {
        using var leitor = new StreamReader(conteudo, System.Text.Encoding.UTF8);

        var cabecalho = leitor.ReadLine();
        if (cabecalho is null || !cabecalho.TrimStart('﻿').StartsWith(CabecalhoEsperado, StringComparison.OrdinalIgnoreCase))
            throw new IbptIntegrationException(
                "O arquivo não parece ser a tabela do IBPT. A primeira linha deve começar com " +
                "\"codigo;ex;tipo;descricao;...\" — use o TabelaIBPTax<UF><versao>.csv do pacote oficial.");

        var linhas = new List<LinhaTabelaIbpt>(13_000);
        var ignoradas = 0;

        while (leitor.ReadLine() is { } linha)
        {
            if (string.IsNullOrWhiteSpace(linha)) continue;

            var campos = SepararRespeitandoAspas(linha);
            if (campos.Length < 13) { ignoradas++; continue; }

            // Só NCM de 8 dígitos entra: é o que a NFC-e usa, e é o que o lookup
            // do produto procura.
            var codigo = new string(campos[0].Where(char.IsDigit).ToArray());
            if (codigo.Length != 8 || campos[2].Trim() != TipoNcm) { ignoradas++; continue; }

            // `ex` preenchido é exceção fiscal do NCM — regra própria, que este
            // motor não aplica. Importar como se fosse a linha comum daria
            // alíquota errada para quem usa o NCM sem exceção.
            if (!string.IsNullOrWhiteSpace(campos[1])) { ignoradas++; continue; }

            if (!TentarDecimal(campos[4], out var nacional) ||
                !TentarDecimal(campos[5], out var importado) ||
                !TentarDecimal(campos[6], out var estadual) ||
                !TentarDecimal(campos[7], out var municipal))
            {
                ignoradas++;
                continue;
            }

            linhas.Add(new LinhaTabelaIbpt(
                Ncm: codigo,
                NacionalFederal: nacional,
                ImportadoFederal: importado,
                Estadual: estadual,
                Municipal: municipal,
                VigenciaInicio: TentarData(campos[8]),
                VigenciaFim: TentarData(campos[9]),
                Chave: Limitar(campos[10], 50),
                Versao: Limitar(campos[11], 30),
                Fonte: Limitar(campos[12], 100)));
        }

        if (linhas.Count == 0)
            throw new IbptIntegrationException(
                "Nenhuma linha válida encontrada no arquivo. Confira se é o CSV da tabela, " +
                "e não o cartaz (.xlsx) ou um dos manuais (.pdf) do mesmo pacote.");

        return new ResultadoLeituraCsvIbpt(
            linhas,
            Versao: linhas[0].Versao,
            VigenciaInicio: linhas.Where(l => l.VigenciaInicio.HasValue).Select(l => l.VigenciaInicio).Min(),
            VigenciaFim: linhas.Where(l => l.VigenciaFim.HasValue).Select(l => l.VigenciaFim).Min(),
            LinhasIgnoradas: ignoradas);
    }

    /// <summary>
    /// A descrição vem entre aspas porque contém vírgulas — e, em alguns casos,
    /// ponto e vírgula. Um `Split(';')` cru quebraria essas linhas.
    /// </summary>
    private static string[] SepararRespeitandoAspas(string linha)
    {
        var campos = new List<string>(13);
        var atual = new System.Text.StringBuilder();
        var dentroDeAspas = false;

        foreach (var c in linha)
        {
            if (c == '"') { dentroDeAspas = !dentroDeAspas; continue; }
            if (c == ';' && !dentroDeAspas) { campos.Add(atual.ToString()); atual.Clear(); continue; }
            atual.Append(c);
        }
        campos.Add(atual.ToString());
        return campos.ToArray();
    }

    /// <summary>Decimal do arquivo usa PONTO — ler na cultura do processo daria
    /// 1345 onde se lê 13.45 numa máquina pt-BR.</summary>
    private static bool TentarDecimal(string valor, out decimal resultado) =>
        decimal.TryParse(valor.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out resultado);

    private static DateTime? TentarData(string valor) =>
        DateTime.TryParseExact(valor.Trim(), "dd/MM/yyyy",
            CultureInfo.InvariantCulture, DateTimeStyles.None, out var data)
            ? DateTime.SpecifyKind(data, DateTimeKind.Utc)
            : null;

    private static string? Limitar(string valor, int max)
    {
        var limpo = valor.Trim();
        return limpo.Length == 0 ? null : limpo[..Math.Min(limpo.Length, max)];
    }
}
