using System;
using System.Data;
using SigesEdu.Domain.Interfaces;

namespace SigesEdu.Infrastructure.Persistence;

public class UnitOfWork : IUnitOfWork
{
    private readonly IDbConnection _connection;
    private IDbTransaction? _transaction;

    public UnitOfWork(IDbConnectionFactory connectionFactory)
    {
        _connection = connectionFactory.CreateConnection();
    }

    public IDbConnection Connection => _connection;

    public IDbTransaction? CurrentTransaction => _transaction;

    public IDbTransaction BeginTransaction()
    {
        if (_connection.State != ConnectionState.Open)
        {
            _connection.Open();
        }

        _transaction = _connection.BeginTransaction();
        return _transaction;
    }

    public void Commit()
    {
        try
        {
            _transaction?.Commit();
        }
        finally
        {
            _transaction?.Dispose();
            _transaction = null;
        }
    }

    public void Rollback()
    {
        try
        {
            _transaction?.Rollback();
        }
        finally
        {
            _transaction?.Dispose();
            _transaction = null;
        }
    }

    public void Dispose()
    {
        _transaction?.Dispose();
        _connection?.Dispose();
    }
}