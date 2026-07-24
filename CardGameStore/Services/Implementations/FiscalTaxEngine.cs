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
    det MontarItem(NfceEmissionService.ItemFiscal item, int numero, int descontoCentavos, bool incluirIbsCbs);
    NfceEmissionService.TotaisIcms SomarTotaisIcms(IEnumerable<det> itens);
    IbsCbsTotal MontarTotaisIbsCbs2026(IEnumerable<det> itens);
}

internal sealed class ConfigurableFiscalTaxEngine : IFiscalTaxEngine
{
    public det MontarItem(
        NfceEmissionService.ItemFiscal item, int numero, int descontoCentavos, bool incluirIbsCbs) =>
        NfceEmissionService.MontarItem(item, numero, descontoCentavos, incluirIbsCbs);

    public NfceEmissionService.TotaisIcms SomarTotaisIcms(IEnumerable<det> itens) =>
        NfceEmissionService.SomarTotaisIcms(itens);

    public IbsCbsTotal MontarTotaisIbsCbs2026(IEnumerable<det> itens) =>
        NfceEmissionService.MontarTotaisIbsCbs2026(itens);
}
