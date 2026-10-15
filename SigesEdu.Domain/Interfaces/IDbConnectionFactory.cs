using System.Data;

namespace SigesEdu.Domain.Interfaces;

public interface IDbConnectionFactory
{
    IDbConnection CreateConnection();
}