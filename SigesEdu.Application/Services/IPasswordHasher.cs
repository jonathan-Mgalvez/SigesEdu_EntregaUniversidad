namespace SigesEdu.Application.Services;

public interface IPasswordHasher
{
    (string Hash, string Salt) GenerarHash(string password);
    bool VerificarHash(string password, string hashAlmacenado, string saltAlmacenada);
}