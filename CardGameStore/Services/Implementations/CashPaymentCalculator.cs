using CardGameStore.Models.PostgreSQL;

namespace CardGameStore.Services.Implementations;

/// <summary>Valida e congela o numerário real entregue no caixa.</summary>
internal static class CashPaymentCalculator
{
    internal sealed record Result(int? ReceivedInCents, int ChangeInCents);

    internal static Result Calculate(
        int totalInCents, string primaryMethod, string? secondMethod,
        int secondAmountInCents, int? cashReceivedInCents)
    {
        var cashDue = primaryMethod == PaymentMethod.Dinheiro
            ? totalInCents - secondAmountInCents
            : secondMethod == PaymentMethod.Dinheiro ? secondAmountInCents : (int?)null;

        if (!cashDue.HasValue)
        {
            if (cashReceivedInCents.HasValue)
                throw new InvalidOperationException("Valor recebido em dinheiro foi informado, mas a venda não usa dinheiro.");
            return new Result(null, 0);
        }

        var received = cashReceivedInCents ?? cashDue.Value;
        if (received < cashDue.Value)
            throw new InvalidOperationException(
                $"Valor recebido em dinheiro (R$ {received / 100m:N2}) é menor que o valor devido em dinheiro (R$ {cashDue.Value / 100m:N2}).");

        return new Result(received, received - cashDue.Value);
    }
}
