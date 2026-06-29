using System.Security.Cryptography;
using System.Text;

namespace CardGameStore.Services.Implementations;

/// <summary>
/// Criptografia simétrica AES-256-GCM para dados sensíveis em repouso.
/// Formato armazenado: Base64(nonce[12] + tag[16] + ciphertext)
/// Chave configurada em Encryption:Key (32 bytes em Base64) via variável de ambiente.
/// </summary>
public class EncryptionService
{
    private readonly byte[] _key;

    public EncryptionService(IConfiguration config, IWebHostEnvironment env)
    {
        var keyBase64 = config["Encryption:Key"];

        if (string.IsNullOrWhiteSpace(keyBase64))
        {
            if (env.IsDevelopment())
            {
                // Chave fixa apenas em desenvolvimento — NUNCA usar em produção
                _key = new byte[32];
                return;
            }
            throw new InvalidOperationException(
                "Encryption:Key não configurado. " +
                "Gere uma chave com: dotnet run --project CardGameStore -- gen-key");
        }

        _key = Convert.FromBase64String(keyBase64);

        if (_key.Length != 32)
            throw new InvalidOperationException(
                "Encryption:Key deve ter exatamente 32 bytes (256 bits) em Base64.");
    }

    public string Encrypt(string plaintext)
    {
        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        var nonce          = new byte[AesGcm.NonceByteSizes.MaxSize];   // 12 bytes
        var tag            = new byte[AesGcm.TagByteSizes.MaxSize];     // 16 bytes
        var ciphertext     = new byte[plaintextBytes.Length];

        RandomNumberGenerator.Fill(nonce);

        using var aes = new AesGcm(_key, AesGcm.TagByteSizes.MaxSize);
        aes.Encrypt(nonce, plaintextBytes, ciphertext, tag);

        // Concatena nonce + tag + ciphertext em Base64
        var result = new byte[nonce.Length + tag.Length + ciphertext.Length];
        Buffer.BlockCopy(nonce, 0, result, 0,                            nonce.Length);
        Buffer.BlockCopy(tag,   0, result, nonce.Length,                 tag.Length);
        Buffer.BlockCopy(ciphertext, 0, result, nonce.Length + tag.Length, ciphertext.Length);

        return Convert.ToBase64String(result);
    }

    public string Decrypt(string cipherBase64)
    {
        var data      = Convert.FromBase64String(cipherBase64);
        var nonceSize = AesGcm.NonceByteSizes.MaxSize;
        var tagSize   = AesGcm.TagByteSizes.MaxSize;

        var nonce      = data[..nonceSize];
        var tag        = data[nonceSize..(nonceSize + tagSize)];
        var ciphertext = data[(nonceSize + tagSize)..];
        var plaintext  = new byte[ciphertext.Length];

        using var aes = new AesGcm(_key, AesGcm.TagByteSizes.MaxSize);
        aes.Decrypt(nonce, ciphertext, tag, plaintext);

        return Encoding.UTF8.GetString(plaintext);
    }

    public string?  EncryptNullable(string? value) => value is null ? null : Encrypt(value);
    public string?  DecryptNullable(string? value) => value is null ? null : Decrypt(value);

    /// <summary>Gera uma nova chave aleatória de 256 bits em Base64 (uso: setup inicial).</summary>
    public static string GenerateKey() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
}
