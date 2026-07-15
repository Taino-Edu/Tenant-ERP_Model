// =============================================================================
// ImportControllerTests.cs — Testa a importação self-service contra Postgres
// real: linhas válidas entram, inválidas viram erro com motivo (não é
// tudo-ou-nada), e o caso mais sensível — crediário nunca cria cliente novo
// só pra pendurar dívida, só resolve contra quem já existe.
// =============================================================================

using System.Security.Claims;
using System.Text;
using Xunit;
using CardGameStore.Controllers;
using CardGameStore.Data;
using CardGameStore.DTOs;
using CardGameStore.Models.PostgreSQL;
using CardGameStore.Services.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace CardGameStore.Tests.Controllers;

public class ImportControllerTests
{
    private static AppDbContext CreateDb(string name) => TestDbFactory.Create(name);

    // Os endpoints retornam Ok(result) — isso popula ActionResult<T>.Result
    // (ObjectResult), não .Value (só populado no "return result" direto).
    private static ImportResultDto Unwrap(ActionResult<ImportResultDto> actionResult) =>
        (ImportResultDto)((OkObjectResult)actionResult.Result!).Value!;

    private static ImportController CreateController(AppDbContext db, Guid? adminId = null)
    {
        var controller = new ImportController(db, new Mock<IAuditService>().Object);
        var claims = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("sub", (adminId ?? Guid.NewGuid()).ToString())], "test"));
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = claims },
        };
        return controller;
    }

    private static IFormFile CsvFile(string content)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        return new FormFile(new MemoryStream(bytes), 0, bytes.Length, "arquivo", "import.csv")
        {
            Headers = new HeaderDictionary(),
            ContentType = "text/csv",
        };
    }

    // ── Produtos ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task ImportProdutos_LinhasValidas_Importa()
    {
        var db = CreateDb(nameof(ImportProdutos_LinhasValidas_Importa));
        var controller = CreateController(db);
        var csv = "Nome;Categoria;PrecoVenda;PrecoCusto;Estoque\r\n" +
                  "Produto A;Bebida;10,50;5,00;20\r\n" +
                  "Produto B;Salgado;7,00;3,00;15\r\n";

        var result = Unwrap(await controller.ImportProdutos(CsvFile(csv)));

        result.TotalLinhas.Should().Be(2);
        result.Importados.Should().Be(2);
        result.Erros.Should().BeEmpty();

        var produtos = await db.Products.ToListAsync();
        produtos.Should().HaveCount(2);
        produtos.Should().Contain(p => p.Name == "Produto A" && p.PriceInCents == 1050 && p.CostPriceInCents == 500);
    }

    [Fact]
    public async Task ImportProdutos_NomeDuplicadoNoBanco_VaiPraErro()
    {
        var db = CreateDb(nameof(ImportProdutos_NomeDuplicadoNoBanco_VaiPraErro));
        db.Products.Add(new Product { Id = Guid.NewGuid(), Name = "Já Existe", Category = "X", PriceInCents = 100, StockQuantity = 1, MinimumStock = 1 });
        await db.SaveChangesAsync();
        var controller = CreateController(db);

        var csv = "Nome;Categoria;PrecoVenda\r\nJá Existe;Bebida;10,00\r\n";
        var result = Unwrap(await controller.ImportProdutos(CsvFile(csv)));

        result.Importados.Should().Be(0);
        result.Erros.Should().ContainSingle(e => e.Motivo.Contains("Já existe"));
    }

    [Fact]
    public async Task ImportProdutos_NomeDuplicadoDentroDoArquivo_SoAPrimeiraEntra()
    {
        var db = CreateDb(nameof(ImportProdutos_NomeDuplicadoDentroDoArquivo_SoAPrimeiraEntra));
        var controller = CreateController(db);

        var csv = "Nome;Categoria;PrecoVenda\r\nRepetido;Bebida;10,00\r\nRepetido;Bebida;12,00\r\n";
        var result = Unwrap(await controller.ImportProdutos(CsvFile(csv)));

        result.Importados.Should().Be(1);
        result.Erros.Should().ContainSingle();
        (await db.Products.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task ImportProdutos_MisturaLinhaValidaEInvalida_ImportaSoAValida()
    {
        var db = CreateDb(nameof(ImportProdutos_MisturaLinhaValidaEInvalida_ImportaSoAValida));
        var controller = CreateController(db);

        var csv = "Nome;Categoria;PrecoVenda\r\n" +
                  "Valido;Bebida;10,00\r\n" +
                  ";Bebida;10,00\r\n" +          // sem nome
                  "SemPreco;Bebida;0\r\n";        // preço zero

        var result = Unwrap(await controller.ImportProdutos(CsvFile(csv)));

        result.TotalLinhas.Should().Be(3);
        result.Importados.Should().Be(1);
        result.Erros.Should().HaveCount(2);
        result.Erros[0].Linha.Should().Be(3); // linha 1 = cabeçalho, linha 2 = "Valido" (ok), linha 3 = erro
    }

    // ── Clientes ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task ImportClientes_LinhaValida_Importa()
    {
        var db = CreateDb(nameof(ImportClientes_LinhaValida_Importa));
        var controller = CreateController(db);

        var csv = "Nome;Email;CPF\r\nAna Silva;ana@test.com;111.444.777-35\r\n";
        var result = Unwrap(await controller.ImportClientes(CsvFile(csv)));

        result.Importados.Should().Be(1);
        var user = await db.Users.FirstAsync();
        user.Name.Should().Be("Ana Silva");
        user.Cpf.Should().Be("11144477735");
        user.Role.Should().Be(UserRole.Customer);
    }

    [Fact]
    public async Task ImportClientes_CpfInvalido_VaiPraErro()
    {
        var db = CreateDb(nameof(ImportClientes_CpfInvalido_VaiPraErro));
        var controller = CreateController(db);

        var csv = "Nome;CPF\r\nAna;111.111.111-11\r\n"; // dígitos repetidos, sempre inválido
        var result = Unwrap(await controller.ImportClientes(CsvFile(csv)));

        result.Importados.Should().Be(0);
        result.Erros.Should().ContainSingle(e => e.Motivo.Contains("CPF inválido"));
    }

    [Fact]
    public async Task ImportClientes_EmailJaCadastrado_VaiPraErro()
    {
        var db = CreateDb(nameof(ImportClientes_EmailJaCadastrado_VaiPraErro));
        db.Users.Add(new User { Id = Guid.NewGuid(), Name = "Já Existe", Email = "existe@test.com", Role = UserRole.Customer, PasswordHash = "h" });
        await db.SaveChangesAsync();
        var controller = CreateController(db);

        var csv = "Nome;Email\r\nOutra Pessoa;existe@test.com\r\n";
        var result = Unwrap(await controller.ImportClientes(CsvFile(csv)));

        result.Importados.Should().Be(0);
        result.Erros.Should().ContainSingle(e => e.Motivo.Contains("já cadastrado"));
    }

    // ── Crediário (caso sensível) ─────────────────────────────────────────────

    [Fact]
    public async Task ImportCrediario_ClienteExistentePorCpf_Importa()
    {
        var db = CreateDb(nameof(ImportCrediario_ClienteExistentePorCpf_Importa));
        var user = new User { Id = Guid.NewGuid(), Name = "Devedor", Cpf = "11144477735", Role = UserRole.Customer, PasswordHash = "h" };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var controller = CreateController(db);

        var csv = "ClienteCPF;ValorTotal;ValorPago;DataVencimento\r\n" +
                  "111.444.777-35;100,00;30,00;2026-12-31\r\n";
        var result = Unwrap(await controller.ImportCrediario(CsvFile(csv)));

        result.Importados.Should().Be(1);
        var crediario = await db.Crediarios.FirstAsync();
        crediario.UserId.Should().Be(user.Id);
        crediario.ValorEmCentavos.Should().Be(10000);
        crediario.ValorPagoEmCentavos.Should().Be(3000);
        crediario.Status.Should().Be(CrediariosStatus.Aberto);
    }

    [Fact]
    public async Task ImportCrediario_ClienteNaoExiste_NuncaCriaClienteNovo()
    {
        var db = CreateDb(nameof(ImportCrediario_ClienteNaoExiste_NuncaCriaClienteNovo));
        var controller = CreateController(db);

        var csv = "ClienteCPF;ValorTotal;DataVencimento\r\n111.444.777-35;100,00;2026-12-31\r\n";
        var result = Unwrap(await controller.ImportCrediario(CsvFile(csv)));

        result.Importados.Should().Be(0);
        result.Erros.Should().ContainSingle(e => e.Motivo.Contains("não encontrado"));
        (await db.Users.CountAsync()).Should().Be(0, "importar crediário não pode criar cliente novo — a linha tinha que ter virado erro");
        (await db.Crediarios.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task ImportCrediario_ValorPagoIgualAoTotal_MarcaComoPago()
    {
        var db = CreateDb(nameof(ImportCrediario_ValorPagoIgualAoTotal_MarcaComoPago));
        var user = new User { Id = Guid.NewGuid(), Name = "Quitado", Email = "quitado@test.com", Role = UserRole.Customer, PasswordHash = "h" };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var controller = CreateController(db);

        var csv = "ClienteEmail;ValorTotal;ValorPago;DataVencimento\r\nquitado@test.com;50,00;50,00;2026-01-01\r\n";
        var result = Unwrap(await controller.ImportCrediario(CsvFile(csv)));

        result.Importados.Should().Be(1);
        var crediario = await db.Crediarios.FirstAsync();
        crediario.Status.Should().Be(CrediariosStatus.Pago);
    }

    [Fact]
    public async Task ImportCrediario_ValorPagoMaiorQueTotal_VaiPraErro()
    {
        var db = CreateDb(nameof(ImportCrediario_ValorPagoMaiorQueTotal_VaiPraErro));
        var user = new User { Id = Guid.NewGuid(), Name = "X", Email = "x@test.com", Role = UserRole.Customer, PasswordHash = "h" };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var controller = CreateController(db);

        var csv = "ClienteEmail;ValorTotal;ValorPago;DataVencimento\r\nx@test.com;10,00;99,00;2026-01-01\r\n";
        var result = Unwrap(await controller.ImportCrediario(CsvFile(csv)));

        result.Importados.Should().Be(0);
        result.Erros.Should().ContainSingle();
    }
}
