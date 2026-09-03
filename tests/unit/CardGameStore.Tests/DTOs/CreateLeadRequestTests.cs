// =============================================================================
// CreateLeadRequestTests.cs — Trava o contrato do único formulário que capta
// cliente novo (POST /api/leads, público).
//
// O `kind` nasceu para o registro de privacidade e o frontend o manda como
// texto ("Institucional"/"Afiliados"). Só que a API não registra um
// JsonStringEnumConverter global: sem o atributo no enum, o System.Text.Json
// aceita apenas o número, e o [ApiController] responde 400 antes da action —
// "The JSON value could not be converted to CardGameStore.DTOs.LeadKind.
// Path: $.kind". Nenhum lead entrava, nem da landing nem do Programa de
// Afiliados, e o site mostrava esse texto de erro cru para o visitante.
//
// Por isso o teste desserializa o corpo COMO O NAVEGADOR MANDA (camelCase,
// JsonSerializerDefaults.Web — o mesmo padrão que o ASP.NET usa no binding),
// e não a versão PascalCase que passaria mesmo com o bug.
// =============================================================================

using System.Text.Json;
using CardGameStore.DTOs;

namespace CardGameStore.Tests.DTOs;

public sealed class CreateLeadRequestTests
{
    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    [Theory]
    [InlineData("Institucional", LeadKind.Institucional)]
    [InlineData("Afiliados", LeadKind.Afiliados)]
    public void Kind_AceitaONomeDoEnumComoTexto(string enviado, LeadKind esperado)
    {
        var request = JsonSerializer.Deserialize<CreateLeadRequest>(
            $$"""
            {"nome":"Taino","telefone":"1799","email":"contato@exemplo.com",
             "mensagem":"eu tenho uma loja de informatica","kind":"{{enviado}}",
             "privacyNoticeAcknowledged":true,"privacyNoticeVersion":"2.2"}
            """, WebJson);

        request.Should().NotBeNull();
        request!.Kind.Should().Be(esperado);
        request.PrivacyNoticeAcknowledged.Should().BeTrue();
    }

    [Fact]
    public void Kind_AusenteCaiNoInstitucional()
    {
        var request = JsonSerializer.Deserialize<CreateLeadRequest>(
            """{"nome":"Taino","telefone":"1799","privacyNoticeAcknowledged":true}""", WebJson);

        request!.Kind.Should().Be(LeadKind.Institucional);
    }
}
