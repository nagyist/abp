# Understanding Transactions in ABP Unit of Work

![pic](./pic.png)

## Transaction Management Overview

One of the main responsibilities of Unit of Work is managing database transactions. It provides the following transaction management features:

- Automatically manages database connections and transaction scopes, developers don't need to manually control transaction start and commit
- Ensures business operation integrity, all database operations within a unit of work either succeed completely or roll back completely
- Supports configuration of transaction isolation levels and timeout periods
- Supports nested transactions and transaction propagation

## Transaction Behavior

### Default Transaction Settings

You can modify the default behavior through the following configuration:

```csharp
Configure<AbpUnitOfWorkDefaultOptions>(options =>
{
    /*
        Modify the default transaction behavior for all unit of work:
        - UnitOfWorkTransactionBehavior.Enabled: Always enable transactions, all requests will start a transaction
        - UnitOfWorkTransactionBehavior.Disabled: Always disable transactions, no requests will start a transaction
        - UnitOfWorkTransactionBehavior.Auto: Automatically decide whether to start a transaction based on HTTP request type
    */
    options.TransactionBehavior = UnitOfWorkTransactionBehavior.Disabled;
    
    // Set default timeout
    options.Timeout = TimeSpan.FromSeconds(30);
    
    // Set default isolation level
    options.IsolationLevel = IsolationLevel.ReadCommitted;
});
```

### Automatic Transaction Management

ABP framework implements automatic management of unit of work and transactions through middleware, MVC global filters, and interceptors. In most cases, you don't need to manage them manually

### Transaction Behavior for HTTP Requests

By default, the framework adopts an intelligent transaction management strategy for HTTP requests:
- `GET` requests won't start a transactional unit of work
- Other HTTP requests (`POST/PUT/DELETE` etc.) will start a transactional unit of work

### Manual Transaction Control

If you need to manually start a new unit of work, you can customize whether to start a transaction and set the transaction isolation level and timeout:

```csharp
// Start a transactional unit of work
using (var uow = _unitOfWorkManager.Begin(
    isTransactional: true,
    isolationLevel: IsolationLevel.RepeatableRead,
    timeout: 30
))
{
    // Execute database operations within transaction
    await uow.CompleteAsync();
}
```

```csharp
// Start a non-transactional unit of work
using (var uow = _unitOfWorkManager.Begin(
    isTransactional: false
))
{
    // Execute database operations without transaction
    await uow.CompleteAsync();
}
```

### Configuring Transactions Using `[UnitOfWork]` Attribute

You can customize transaction behavior by using the `UnitOfWorkAttribute` on methods, classes, or interfaces:

```csharp
[UnitOfWork(
    IsTransactional = true,
    IsolationLevel = IsolationLevel.RepeatableRead,
    Timeout = 30
)]
public virtual async Task ProcessOrderAsync(int orderId)
{
    // Execute database operations within transaction
}
```

### Non-Transactional Unit of Work

In some scenarios, you might not need transaction support. You can create a non-transactional unit of work by setting `IsTransactional = false`:

```csharp
public virtual async Task ImportDataAsync(List<DataItem> items)
{
    using (var uow = _unitOfWorkManager.Begin(
        isTransactional: false
    ))
    {
        foreach (var item in items)
        {
            await _repository.InsertAsync(item, autoSave: true);
            // Each InsertAsync will save to database immediately
            // If subsequent operations fail, saved data won't be rolled back
        }

        await uow.CompleteAsync();
    }
}
```

Applicable scenarios:
- Batch import data scenarios where partial success is allowed
- Read-only operations, such as queries
- Scenarios with low data consistency requirements

### Methods to Commit Transactions

#### In Transactional Unit of Work

Within a unit of work, there are several ways to commit changes to the database:

1. **IUnitOfWork.SaveChangesAsync**

```csharp
await _unitOfWorkManager.Current.SaveChangesAsync();
```

2. **autoSave parameter in repositories**

```csharp
await _repository.InsertAsync(entity, autoSave: true);
```

`autoSave` and `SaveChangesAsync` are actually equivalent, both commit changes in the current context to the database. However, these changes can still be rolled back before `CompleteAsync` is called. If the unit of work throws an exception or `CompleteAsync` is not called, the transaction will automatically roll back, and saved changes will be undone. Only after successfully calling `CompleteAsync` will the transaction be truly committed, and changes will be permanently saved to the database.

3. **CompleteAsync**
```csharp
using (var uow = _unitOfWorkManager.Begin())
{
    // Execute database operations
    await uow.CompleteAsync();
}
```

This method is used to commit the entire unit of work. It not only commits all database transactions but also:
- Executes and handles all pending domain events within the unit of work
- Executes all registered post-operations and cleanup work within the unit of work
- Releases all DbTransaction resources when the unit of work object is disposed

Therefore, `CompleteAsync` is a key step to ensure the unit of work completes correctly and must be called before the unit of work ends.

#### In Non-Transactional Unit of Work

In non-transactional unit of work, these methods behave differently:

`autoSave` and `SaveChangesAsync` will immediately save changes to the database, and they cannot be rolled back. Even in non-transactional unit of work, you still need to call the `CompleteAsync` method because it performs other important tasks.

Example:
```csharp
using (var uow = _unitOfWorkManager.Begin(isTransactional: false))
{
    // Immediately save to database, cannot be rolled back
    await _repository.InsertAsync(entity1, autoSave: true);
    
    // This operation will save separately, independent of the previous operation
    await _repository.InsertAsync(entity2, autoSave: true);
    
    await uow.CompleteAsync();
}
```

## Transaction Management Best Practices

### 1. Remember to Commit Transactions

When manually controlling transactions, remember to call the `CompleteAsync` method to commit the transaction after operations are complete. For conventional transactions, the framework will automatically commit the transaction:

```csharp
public async Task TransferMoneyAsync(TransferDto transfer)
{
    using (var uow = _unitOfWorkManager.Begin(
        requiresNew: true,
        isTransactional: true,
        isolationLevel: IsolationLevel.RepeatableRead
    ))
    {
        try
        {
            await _accountRepository.DeductMoneyAsync(transfer.FromAccount, transfer.Amount);
            await _accountRepository.AddMoneyAsync(transfer.ToAccount, transfer.Amount);
            await uow.CompleteAsync();
        }
        catch (Exception)
        {
            // Transaction will automatically roll back
            throw;
        }
    }
}
```

### 2. Pay Attention to Context

If a unit of work already exists in the current context, `UnitOfWorkManager.Begin` method and` UnitOfWorkAttribute` will **reuse it**. Specify `requiresNew: true` to force create a new unit of work.

```csharp
[UnitOfWork]
public async Task Method1()
{
    using (var uow = _unitOfWorkManager.Begin(
        requiresNew: true, 
        isTransactional: true,
        isolationLevel: IsolationLevel.RepeatableRead,
        timeout: 30
    ))
    {
        await Method2();
        await uow.CompleteAsync();
    }
}
```

### 3. Use `virtual` Methods

Remember to use the `virtual` modifier for methods in dependency injection class services, because ABP framework uses interceptors, and it cannot intercept non-`virtual` methods, thus unable to implement unit of work functionality.

### 4. Avoid Long Transactions

Enabling long-running transactions can lead to resource locking, excessive transaction log usage, and reduced concurrent performance, while rollback costs are high and may exhaust database connection resources. It's recommended to split into shorter transactions, reduce lock holding time, and optimize performance and reliability.

## Transaction-Related Recommendations

- Choose appropriate transaction isolation levels based on business requirements
- Avoid overly long transactions, long-running operations should be split into multiple small transactions
- Use the `requiresNew` parameter reasonably to control transaction boundaries
- Pay attention to setting appropriate transaction timeout periods
- Ensure transactions can properly roll back when exceptions occur
- For read-only operations, it's recommended to use non-transactional unit of work to improve performance

## References

- [ABP Unit of Work](https://abp.io/docs/latest/framework/architecture/domain-driven-design/unit-of-work)
- [EF Core Transactions](https://docs.microsoft.com/en-us/ef/core/saving/transactions)
- [Transaction Isolation Levels](https://docs.microsoft.com/en-us/dotnet/api/system.data.isolationlevel)
