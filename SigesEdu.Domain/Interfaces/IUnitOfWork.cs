using System.Data;

namespace SigesEdu.Domain.Interfaces;

/// <summary>
/// Contrato para coordinar transacciones atómicas explícitas (ACID) en SQLite (RN-04).
/// </summary>
public interface IUnitOfWork : IDisposable
{
    IDbConnection Connection { get; }
    IDbTransaction? CurrentTransaction { get; }
    IDbTransaction BeginTransaction();
    void Commit();
    void Rollback();
}