namespace Appointment.Application.Abstractions.Data;

public interface IEncryptionService
{
    (byte[] encryptedData, byte[] encryptedKey, byte[] iv) Encrypt(byte[] data);
    byte[] Decrypt(byte[] encryptedData, byte[] encryptedKey, byte[] iv);
}
