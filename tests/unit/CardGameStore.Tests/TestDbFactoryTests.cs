// =============================================================================
// TestDbFactoryTests.cs — a decisão de dropar schema de execução anterior.
//
// Duas vezes a suíte quebrou em massa (292 falhas em testes que ninguém tocou)
// porque schemas órfãos de rodadas canceladas se acumularam até o handshake do
// Npgsql estourar. A varredura de início de execução resolve isso, mas erra
// caro se errar para o lado errado: dropar o schema de uma SEGUNDA suíte
// rodando ao mesmo tempo a mataria com "relation does not exist" — de novo um
// sintoma sem relação nenhuma com a causa.
//
// Por isso o que se testa aqui é o viés da decisão: na dúvida, não dropa.
// =============================================================================

namespace CardGameStore.Tests;

public class TestDbFactoryTests
{
    [Fact]
    public void DeExecucaoJaMorta_PidDesteProcesso_NaoEhConsideradoMorto()
    {
        // O caso mais destrutivo: varrer os schemas que a própria execução
        // acabou de criar.
        var schema = TestDbFactory.IsolatedSchemaName(nameof(DeExecucaoJaMorta_PidDesteProcesso_NaoEhConsideradoMorto));

        TestDbFactory.DeExecucaoJaMorta(schema).Should().BeFalse();
    }

    [Fact]
    public void DeExecucaoJaMorta_PidInexistente_EhConsideradoMorto()
    {
        // 0x7ffffffe está acima do teto de PID de Linux e Windows, então
        // nenhum processo real responde por ele.
        TestDbFactory.DeExecucaoJaMorta("test_7ffffffe_abc_1_qualquer").Should().BeTrue();
    }

    [Theory]
    [InlineData("test")]
    [InlineData("test_")]
    [InlineData("test_naoehhex_abc_1_x")]     // PID ilegível
    [InlineData("test_0_abc_1_x")]            // PID inválido
    [InlineData("test_7ffffffe_abc")]         // curto demais pro padrão da fábrica
    [InlineData("public")]
    [InlineData("tenant_loja_demo")]          // schema de tenant, não de teste
    public void DeExecucaoJaMorta_NomeForaDoPadrao_NaoEhTocado(string schema)
    {
        // Um nome que a fábrica não gerou não é lixo dela — pode ser schema de
        // outra ferramenta no mesmo banco.
        TestDbFactory.DeExecucaoJaMorta(schema).Should().BeFalse();
    }
}
