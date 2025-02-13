using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using Volo.Abp.DependencyInjection;

namespace Volo.Abp.RabbitMQ;

public class ConnectionPool : IConnectionPool, ISingletonDependency
{
    protected AbpRabbitMqOptions Options { get; }

    protected ConcurrentDictionary<string, Lazy<IConnection>> Connections { get; }

    private bool _isDisposed;

    public ConnectionPool(IOptions<AbpRabbitMqOptions> options)
    {
        Options = options.Value;
        Connections = new ConcurrentDictionary<string, Lazy<IConnection>>();
    }

    public virtual IConnection Get(string? connectionName = null)
    {
        connectionName ??= RabbitMqConnections.DefaultConnectionName;
        var connectionFactory = Options.Connections.GetOrDefault(connectionName);
        try
        {
            var connection = GetConnection(connectionName, connectionFactory);
        
            if (connection.IsOpen)
            {
                return connection;
            }
            
            connection.Dispose();
            Connections.TryRemove(connectionName, out _);
            return GetConnection(connectionName, connectionFactory);
        }
        catch (Exception)
        {
            Connections.TryRemove(connectionName, out _);
            throw;
        }
    }

    protected virtual IConnection GetConnection(string connectionName, ConnectionFactory connectionFactory)
    {
        return Connections.GetOrAdd(
            connectionName, () => new Lazy<IConnection>(() =>
            {
                var hostnames = connectionFactory.HostName.TrimEnd(';').Split(';');
                // Handle Rabbit MQ Cluster.
                return hostnames.Length == 1
                    ? connectionFactory.CreateConnection()
                    : connectionFactory.CreateConnection(hostnames);
            })
        ).Value;
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;

        foreach (var connection in Connections.Values)
        {
            try
            {
                connection.Value.Dispose();
            }
            catch
            {

            }
        }

        Connections.Clear();
    }
}
