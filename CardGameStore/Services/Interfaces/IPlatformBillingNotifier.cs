namespace CardGameStore.Services.Interfaces;

/// <summary>Avisos de cobrança da plataforma para o lojista, por e-mail — o
/// único canal que existe hoje. Cada aviso sai uma vez só (TenantBillingNotice).
///
/// O Asaas já manda os lembretes da própria fatura (emitida, perto de vencer,
/// vencida). O que fica aqui é o que ele não tem como saber: que falta o
/// documento para a fatura existir, e que a loja vai ser (ou foi) suspensa.</summary>
public interface IPlatformBillingNotifier
{
    /// <summary>Dispara em segundo plano e volta na hora. Quem chama pode ser o
    /// webhook do gateway, que não pode esperar um SMTP lento: resposta demorada
    /// faz o Asaas contar falha de entrega e pausar a fila.</summary>
    void NotificarLojaSuspensa(Guid tenantId);

    /// <summary>Mesmo comportamento de <see cref="NotificarLojaSuspensa"/>.</summary>
    void NotificarLojaReativada(Guid tenantId);

    /// <summary>Avisos por prazo, rodados pelo job: dados de cobrança faltando
    /// perto do fim do teste, e suspensão chegando para fatura vencida.</summary>
    Task EnviarAvisosDePrazoAsync(CancellationToken ct = default);
}
