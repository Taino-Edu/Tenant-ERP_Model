// =============================================================================
// RegrasIbsCbs.cs — Catálogo versionado das regras de IBS/CBS (RTC-001).
//
// O que este arquivo substitui: uma condição fixa de ano espalhada no motor
// (`ano >= 2027` → exceção) que, na virada do calendário, derrubaria TODA a
// emissão do sistema até alguém alterar o código. Um cronograma tributário que
// muda por Nota Técnica não pode ter o poder de parar o caixa de todo mundo.
//
// O que ele coloca no lugar: uma tabela com vigência, perfil do contribuinte,
// alíquotas, fonte oficial e data de consulta. Três consequências:
//
//   • a passagem do tempo nunca causa parada geral — a última faixa é ABERTA
//     (sem fim), então sempre existe regra aplicável;
//   • mudar a regra é editar dados versionados em git, com fonte e data ao lado,
//     e não caçar literais dentro do motor fiscal;
//   • perfis diferentes (Simples, excesso de sublimite, opção pelo regime
//     regular, regime normal) podem receber regras diferentes sem tocar em uma
//     linha do código de montagem do XML.
//
// O catálogo mora em código, e não em tabela por tenant, de propósito: alíquota
// de IBS/CBS é legislação nacional. Deixar cada loja preencher a sua seria
// transformar erro de digitação em erro fiscal — e não haveria como revisar.
// O que é do contribuinte (regime, sublimite, opção pelo regime regular) vive na
// FiscalConfig e entra aqui só como critério de seleção.
//
// ATUALIZAÇÃO: ao acrescentar uma faixa, feche a anterior com VigenciaFim,
// preencha FonteOficial/ConsultadoEm e mantenha a última faixa aberta.
// =============================================================================

using CardGameStore.Models.PostgreSQL;

namespace CardGameStore.Services.Implementations;

/// <summary>
/// Perfil do contribuinte para fins de IBS/CBS. Separado de
/// <see cref="RegimeTributario"/> porque as duas coisas não coincidem: um
/// optante do Simples pode estar no regime regular do IBS/CBS por opção, ou
/// fora do Simples por excesso de sublimite, sem que o regime declarado mude.
/// </summary>
public enum PerfilIbsCbs
{
    /// <summary>Simples Nacional, dentro do sublimite, sem opção pelo regime regular.</summary>
    SimplesNacional,

    /// <summary>Optante do Simples que excedeu o sublimite estadual.</summary>
    SimplesExcessoSublimite,

    /// <summary>Optante do Simples que fez a opção pelo regime regular de IBS/CBS.</summary>
    SimplesRegimeRegular,

    /// <summary>Lucro Presumido ou Lucro Real.</summary>
    RegimeNormal,
}

/// <summary>
/// Uma faixa de vigência do IBS/CBS. <see cref="VigenciaFim"/> nulo significa
/// faixa aberta — é o que garante que virar o ano não deixe o motor sem regra.
/// </summary>
public sealed record RegraIbsCbs(
    string Versao,
    DateOnly VigenciaInicio,
    DateOnly? VigenciaFim,
    IReadOnlyList<PerfilIbsCbs> Perfis,
    decimal AliquotaIbsUf,
    decimal AliquotaIbsMun,
    decimal AliquotaCbs,
    // CstSuportados: CSTs que o motor sabe calcular nesta faixa. Um CST fora da
    // lista exige provedor de cálculo próprio e é recusado antes de reservar
    // numeração — recusar é melhor do que emitir valor inventado.
    IReadOnlyList<string> CstSuportados,
    // DestaqueObrigatorio: se o destaque de IBS/CBS já é exigido no documento de
    // produção nesta faixa. Enquanto for false, os grupos só saem em homologação
    // — é a diferença entre "existe regra publicada" e "o documento já tem que
    // carregá-la". Virar isso é edição de dado, não alteração de motor.
    bool DestaqueObrigatorio,
    string FonteOficial,
    DateOnly ConsultadoEm,
    string Observacao)
{
    public bool VigenteEm(DateOnly data) =>
        data >= VigenciaInicio && (VigenciaFim is null || data <= VigenciaFim);

    public bool AplicaA(PerfilIbsCbs perfil) => Perfis.Contains(perfil);

    public bool SuportaCst(string? cst) =>
        cst is not null && CstSuportados.Contains(cst, StringComparer.Ordinal);

    /// <summary>Faixa sem data de término — a que impede que o tempo passe e o
    /// motor fique sem regra aplicável.</summary>
    public bool EhAberta => VigenciaFim is null;
}

public static class CatalogoRegrasIbsCbs
{
    /// <summary>
    /// Fase de transição de 2026: destaque informativo de IBS/CBS, com alíquotas
    /// de teste (IBS 0,1% estadual e CBS 0,9%), sem recolhimento. Vale para todos
    /// os perfis — a diferenciação entre eles só produz alíquotas distintas a
    /// partir das regras de 2027, que ainda não estão publicadas em definitivo.
    ///
    /// A faixa é ABERTA de propósito. Fechá-la em 31/12/2026 sem ter a faixa
    /// seguinte publicada recriaria exatamente o defeito que RTC-001 corrige:
    /// a virada do ano deixaria o motor sem regra. Enquanto a próxima não for
    /// publicada, o comportamento conhecido continua — e o sistema avisa que a
    /// regra precisa ser revisada (ver <see cref="RevisaoRecomendadaEm"/>).
    /// </summary>
    private static readonly RegraIbsCbs Transicao2026 = new(
        Versao: "2026.1-transicao",
        VigenciaInicio: new DateOnly(2026, 1, 1),
        VigenciaFim: null,
        Perfis: new[]
        {
            PerfilIbsCbs.SimplesNacional,
            PerfilIbsCbs.SimplesExcessoSublimite,
            PerfilIbsCbs.SimplesRegimeRegular,
            PerfilIbsCbs.RegimeNormal,
        },
        AliquotaIbsUf: 0.1m,
        AliquotaIbsMun: 0m,
        AliquotaCbs: 0.9m,
        CstSuportados: new[] { "000" },
        // Em 2026 o destaque é informativo e há dispensa de penalidades pela
        // omissão. Manter false preserva o comportamento já homologado (grupos
        // só em homologação) — ligar isso é decisão fiscal, com data e fonte,
        // não efeito colateral de a regra existir no catálogo.
        DestaqueObrigatorio: false,
        FonteOficial: "NT 2025.002 (RTC) + orientações RFB/CGIBS para o período de adaptação de 2026",
        ConsultadoEm: new DateOnly(2026, 8, 4),
        Observacao:
            "Período de adaptação: destaque informativo, sem recolhimento. As alíquotas de 2027 " +
            "em diante dependem de publicação oficial e de nova faixa neste catálogo.");

    private static readonly IReadOnlyList<RegraIbsCbs> Regras = new[] { Transicao2026 };

    /// <summary>
    /// Data a partir da qual a última faixa deixa de ser confiável e precisa ser
    /// reconferida contra a legislação vigente. Não bloqueia nada — alimenta o
    /// alerta operacional (CON-002), que é o lugar certo para cobrar uma revisão
    /// documental que ninguém consegue fazer no meio de uma venda.
    /// </summary>
    public static readonly DateOnly RevisaoRecomendadaEm = new(2027, 1, 1);

    public static IReadOnlyList<RegraIbsCbs> Todas => Regras;

    /// <summary>
    /// Regra aplicável à emissão. Devolve null quando o catálogo não cobre a data
    /// — nunca lança: ficar sem regra é assunto de configuração e de alerta, não
    /// motivo para impedir a venda de ser documentada.
    /// </summary>
    public static RegraIbsCbs? Para(DateOnly dataEmissao, PerfilIbsCbs perfil) =>
        Regras
            .Where(r => r.VigenteEm(dataEmissao) && r.AplicaA(perfil))
            .OrderByDescending(r => r.VigenciaInicio)
            .FirstOrDefault();

    /// <summary>
    /// Traduz o cadastro do contribuinte no perfil que seleciona a regra. As duas
    /// condições que o regime declarado não revela — excesso de sublimite e opção
    /// pelo regime regular — são informadas pelo contador na configuração fiscal,
    /// porque não há como o sistema inferi-las.
    /// </summary>
    public static PerfilIbsCbs PerfilDe(
        RegimeTributario regime, bool excedeuSublimite, bool optouRegimeRegular) =>
        regime != RegimeTributario.SimplesNacional ? PerfilIbsCbs.RegimeNormal
        : optouRegimeRegular                       ? PerfilIbsCbs.SimplesRegimeRegular
        : excedeuSublimite                         ? PerfilIbsCbs.SimplesExcessoSublimite
        : PerfilIbsCbs.SimplesNacional;

    public static PerfilIbsCbs PerfilDe(FiscalConfig cfg) =>
        PerfilDe(cfg.RegimeTributario, cfg.ExcedeuSublimiteSimples, cfg.OptouRegimeRegularIbsCbs);
}
