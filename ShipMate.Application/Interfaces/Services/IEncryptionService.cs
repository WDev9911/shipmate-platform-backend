namespace ShipMate.Application.Interfaces.Services;

public interface IEncryptionService
{
    string Encrypt(string plaintext);
    string Decrypt(string encoded);
}
