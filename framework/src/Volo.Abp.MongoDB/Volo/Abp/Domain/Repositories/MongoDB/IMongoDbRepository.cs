using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using Volo.Abp.Domain.Entities;

namespace Volo.Abp.Domain.Repositories.MongoDB;

public interface IMongoDbRepository<TEntity> : IRepository<TEntity>
    where TEntity : class, IEntity
{
    [Obsolete("Use GetDatabaseAsync method.")]
    IMongoDatabase Database { get; }

    Task<IMongoDatabase> GetDatabaseAsync(CancellationToken cancellationToken = default);

    [Obsolete("Use GetCollectionAsync method.")]
    IMongoCollection<TEntity> Collection { get; }

    Task<IMongoCollection<TEntity>> GetCollectionAsync(CancellationToken cancellationToken = default);

    Task<IQueryable<TEntity>> GetQueryableAsync(CancellationToken cancellationToken = default, AggregateOptions? options = null);

    Task<IAggregateFluent<TEntity>> GetAggregateAsync(CancellationToken cancellationToken = default, AggregateOptions? options = null);
}

public interface IMongoDbRepository<TEntity, TKey> : IMongoDbRepository<TEntity>, IRepository<TEntity, TKey>
    where TEntity : class, IEntity<TKey>
{

}
