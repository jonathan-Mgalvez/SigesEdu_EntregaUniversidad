using System;
using System.Security.Cryptography;
using System.Text;
using SigesEdu.Application.Services;

namespace SigesEdu.Infrastructure.Security;

public class PasswordHasher : IPasswordHasher
{
    private const int SaltSize = 16;       // 128 bits
    private const int KeySize = 32;        // 256 bits
    private const int Iterations = 10000;  // Costo computacional

    public (string Hash, string Salt) GenerarHash(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
            throw new ArgumentException("La contraseña no puede estar vacía.", nameof(password));

        byte[] saltBytes = RandomNumberGenerator.GetBytes(SaltSize);
        byte[] keyBytes = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            saltBytes,
            Iterations,
            HashAlgorithmName.SHA256,
            KeySize);

        return (Convert.ToBase64String(keyBytes), Convert.ToBase64String(saltBytes));
    }

    public bool VerificarHash(string password, string hashAlmacenado, string saltAlmacenada)
    {
        if (string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(hashAlmacenado) || string.IsNullOrWhiteSpace(saltAlmacenada))
            return false;

        byte[] saltBytes = Convert.FromBase64String(saltAlmacenada);
        byte[] keyBytes = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            saltBytes,
            Iterations,
            HashAlgorithmName.SHA256,
            KeySize);

        byte[] expectedHashBytes = Convert.FromBase64String(hashAlmacenado);
        return CryptographicOperations.FixedTimeEquals(keyBytes, expectedHashBytes);
    }
}