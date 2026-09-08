namespace CardGameStore.Common;

/// <summary>
/// Gera a mesma chave de advisory lock em todos os fluxos que alteram o
/// crediário de um cliente. Locks do PostgreSQL são por banco e por cliente.
/// </summary>
internal static class CrediarioLockKey
{
    public static long ForUser(Guid userId) => BitConverter.ToInt64(userId.ToByteArray(), 0);
}
