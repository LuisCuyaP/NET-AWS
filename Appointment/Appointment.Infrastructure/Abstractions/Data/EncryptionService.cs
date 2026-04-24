using System.Security.Cryptography;
using Azure.Identity;
using Azure.Security.KeyVault.Keys;
using Azure.Security.KeyVault.Keys.Cryptography;
using Appointment.Application.Abstractions.Data;
using Microsoft.Extensions.Configuration;

namespace Appointment.Infrastructure.Abstractions.Data;

internal sealed class EncryptionService : IEncryptionService
{
    private readonly CryptographyClient _cryptographyClient;
    private const string _keyName = "CMKKey"; // nombre de la clave RSA en Key Vault
    public EncryptionService(IConfiguration configuration)
    {
        // configuration["Vault"] => nombre del Key Vault (sin .vault.azure.net)
        var keyClient = new KeyClient(
            new Uri($"https://{configuration["Vault"]}.vault.azure.net/"),
            new DefaultAzureCredential());
        _cryptographyClient = keyClient.GetCryptographyClient(_keyName);
    }
    public (byte[] encryptedData, byte[] encryptedKey, byte[] iv) Encrypt(byte[] data)
    {
        // 1) Generar clave simétrica (AES-256)
        using var aes = Aes.Create();
        aes.KeySize = 256;              // (alternativas: 128, 192)
        aes.GenerateKey();
        aes.GenerateIV();               // IV aleatorio
        // 2) Cifrar datos con AES
        byte[] encryptedData;
        using (var encryptor = aes.CreateEncryptor())
        {
            encryptedData = encryptor.TransformFinalBlock(data, 0, data.Length);
        }
        // 3) Cifrar la clave AES con la CMK (RSA OAEP) en Key Vault
        var encryptedKey = _cryptographyClient
            .Encrypt(EncryptionAlgorithm.RsaOaep, aes.Key)
            .Ciphertext;
        return (encryptedData, encryptedKey, aes.IV);
    }
    public byte[] Decrypt(byte[] encryptedData, byte[] encryptedKey, byte[] iv)
    {
        // 1) Descifrar la clave AES con la CMK (RSA OAEP)
        var decryptResult = _cryptographyClient.Decrypt(EncryptionAlgorithm.RsaOaep, encryptedKey);
        var aesKey = decryptResult.Plaintext;
        // 2) Descifrar los datos con AES (misma IV)
        using var aes = Aes.Create();
        aes.Key = aesKey;
        aes.IV = iv;
        using var decryptor = aes.CreateDecryptor();
        return decryptor.TransformFinalBlock(encryptedData, 0, encryptedData.Length);
    }
}
