// =============================================================================
// TabelaPrecosSincronizadaTests.cs — trava a tabela de preços nos dois lados.
//
// O preço vive em dois arquivos, e isso é deliberado: a página de vendas não
// pode depender da API estar de pé pra mostrar quanto custa. Buscar preço por
// endpoint transformaria uma queda de API em plano sem valor na hora da venda.
//
// O que era problema não é a duplicação em si, é a divergência silenciosa —
// alguém sobe o preço no site e esquece do backend, e toda loja nova entra
// cobrando o valor antigo. Foi exatamente o que aconteceu antes, por outra
// porta: o painel oferecia planos ("Mar", "Lagoa") que não existiam na tabela
// do backend, e cada loja criada nascia com mensalidade zero.
//
// Este teste lê o catálogo do frontend como texto e compara com o dicionário do
// backend. Não é elegante, mas é o único ponto onde os dois se encontram dentro
// do CI — e falhar aqui é muito mais barato que descobrir pelo faturamento.
// =============================================================================

using System.Globalization;
using System.Text.RegularExpressions;
using Xunit;
using CardGameStore.Multitenancy;
using FluentAssertions;

namespace CardGameStore.Tests;

public class TabelaPrecosSincronizadaTests
{
    /// <summary>Sobe da pasta de saída dos testes até a raiz do repositório
    /// (a que contém `frontend/`), pra achar o arquivo em qualquer máquina.</summary>
    private static string AcharRaizDoRepo()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "frontend")))
            dir = dir.Parent;

        dir.Should().NotBeNull("os testes precisam rodar dentro do repositório para achar frontend/lib/planos.ts");
        return dir!.FullName;
    }

    /// <summary>Nome do plano → (mensalidade, taxa de implantação) lidos do
    /// catálogo do frontend.</summary>
    private static Dictionary<string, (decimal Preco, decimal Implantacao)> LerCatalogoDoFrontend()
    {
        var caminho = Path.Combine(AcharRaizDoRepo(), "frontend", "lib", "planos.ts");
        File.Exists(caminho).Should().BeTrue($"o catálogo do frontend deveria estar em {caminho}");

        var conteudo = File.ReadAllText(caminho);

        // Casa `nome: 'X',` ... `preco: N,` ... `taxaImplantacao: N,` de cada
        // item do array. `Singleline` faz `.` atravessar linha; o `[^}]*?`
        // impede que um item vaze pro seguinte.
        var matches = Regex.Matches(
            conteudo,
            @"nome:\s*'(?<nome>[^']+)'[^}]*?preco:\s*(?<preco>\d+(?:\.\d+)?)[^}]*?taxaImplantacao:\s*(?<implantacao>\d+(?:\.\d+)?)",
            RegexOptions.Singleline);

        return matches.ToDictionary(
            m => m.Groups["nome"].Value,
            m => (
                decimal.Parse(m.Groups["preco"].Value, CultureInfo.InvariantCulture),
                decimal.Parse(m.Groups["implantacao"].Value, CultureInfo.InvariantCulture)),
            StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void CatalogoDoFrontend_DeveTerOsMesmosPlanosDaTabelaDoBackend()
    {
        var frontend = LerCatalogoDoFrontend();

        frontend.Should().NotBeEmpty("se o parser não achou nada, o formato do arquivo mudou e este teste virou decoração");
        frontend.Keys.Should().BeEquivalentTo(
            TenantProvisioningService.TabelaPrecos.Keys,
            "plano que existe num lado e não no outro faz a loja nascer com mensalidade zero");
    }

    [Fact]
    public void CatalogoDoFrontend_DeveTerOsMesmosPrecosDaTabelaDoBackend()
    {
        var frontend = LerCatalogoDoFrontend();

        foreach (var (plano, precoBackend) in TenantProvisioningService.TabelaPrecos)
            frontend[plano].Preco.Should().Be(precoBackend,
                $"o site anuncia o preço de \"{plano}\" e o backend cobra por ele — os dois têm que dizer o mesmo");
    }

    /// <summary>A implantação não tem tabela própria no backend: ela é derivada
    /// em ApplyCommercialTerms. O buraco que este teste tapa é justamente esse
    /// — a regra do Mar ("gratuita") vivia só no C#, então quando o catálogo do
    /// frontend passou a cobrar R$974 nenhum teste reclamou, e toda loja Mar
    /// continuou nascendo com implantação zero.</summary>
    [Fact]
    public void CatalogoDoFrontend_DeveTerAMesmaTaxaDeImplantacaoQueOBackendProvisiona()
    {
        var frontend = LerCatalogoDoFrontend();

        foreach (var plano in TenantProvisioningService.TabelaPrecos.Keys)
        {
            var tenant = new Tenant { PlanName = plano, CreatedAt = DateTime.UtcNow };
            TenantProvisioningService.ApplyCommercialTerms(tenant);

            tenant.SetupFee.Should().Be(frontend[plano].Implantacao,
                $"o site anuncia a implantação de \"{plano}\" e o provisionamento é quem gera a cobrança");
        }
    }
}
