using CardGameStore.Models.PostgreSQL;
using NFe.Classes.Informacoes.Detalhe;
using IbsCbsTotal = NFe.Classes.Informacoes.Total.IbsCbs.IBSCBSTot;

namespace CardGameStore.Services.Implementations;

/// <summary>
/// Fronteira entre emissão (certificado, assinatura, SEFAZ) e cálculo tributário.
/// Uma implementação futura pode resolver outro provedor por regime/tenant sem
/// reimplementar o transporte da NFC-e.
/// </summary>
internal interface IFiscalTaxEngine
{
    // regraIbsCbs: regra vigente do catálogo versionado (RTC-001). Nula significa
    // não destacar IBS/CBS neste documento — quem decide isso é a vigência e o
    // perfil do contribuinte, não uma condição de ano dentro do motor.
    det MontarItem(
        NfceEmissionService.ItemFiscal item, int numero, int descontoCentavos,
        RegraIbsCbs? regraIbsCbs, RegimeTributario regime);
    NfceEmissionService.TotaisIcms SomarTotaisIcms(IEnumerable<det> itens);
    IbsCbsTotal MontarTotaisIbsCbs(IEnumerable<det> itens);
}

internal sealed class ConfigurableFiscalTaxEngine : IFiscalTaxEngine
{
    public det MontarItem(
        NfceEmissionService.ItemFiscal item, int numero, int descontoCentavos,
        RegraIbsCbs? regraIbsCbs, RegimeTributario regime) =>
        NfceEmissionService.MontarItem(item, numero, descontoCentavos, regraIbsCbs, regime);

    public NfceEmissionService.TotaisIcms SomarTotaisIcms(IEnumerable<det> itens) =>
        NfceEmissionService.SomarTotaisIcms(itens);

    public IbsCbsTotal MontarTotaisIbsCbs(IEnumerable<det> itens) =>
        NfceEmissionService.MontarTotaisIbsCbs(itens);
}
