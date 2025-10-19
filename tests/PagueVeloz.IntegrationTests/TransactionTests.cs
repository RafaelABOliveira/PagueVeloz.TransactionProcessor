//using Microsoft.Extensions.Configuration;
//using Microsoft.Extensions.Logging;
//using Microsoft.Extensions.Logging.Abstractions;
//using PagueVeloz.Core.Domain.Entities;
//using PagueVeloz.Core.Domain.Enums;
//using PagueVeloz.Infrastructure.Persistence;
//using PagueVeloz.Infrastructure.Persistence.Repositories;

//public class TransactionTests
//{
//    private readonly TransactionRepository _repository;
//    private readonly ILogger<TransactionRepository> _logger;

//    public TransactionTests()
//    {
//        var basePath = AppContext.BaseDirectory;
//        var jsonPath = Path.Combine(basePath, "appsettings.json");

//        var configurationBuilder = new ConfigurationBuilder()
//            .AddJsonFile(jsonPath, optional: true, reloadOnChange: false)
//            .AddEnvironmentVariables();

//        var configuration = configurationBuilder.Build();

//        var connectionFactory = new ConnectionFactory(configuration);

//        _logger = NullLogger<TransactionRepository>.Instance;
//        _repository = new TransactionRepository(connectionFactory, _logger);
//    }

//    [Fact]
//    public async Task AddAsyncTransactionRegistry_ShouldPersistTransaction()
//    {
//        var transaction = new Transaction
//        {
//            AccountId = "acc-integration",
//            Type = TransactionType.Credit,
//            Amount = 150,
//            Description = "Teste integração Polly",
//            ReferenceId = Guid.NewGuid().ToString()
//        };

//        var transactionId = await _repository.AddAsyncTransactionRegistry(transaction);

//        Assert.False(string.IsNullOrEmpty(transactionId));
//    }

//    [Fact]
//    public async Task GetByReferenceIdAsync_ShouldReturnTransaction()
//    {
//        var referenceId = Guid.NewGuid().ToString();
//        var transaction = new Transaction
//        {
//            AccountId = "acc-integration",
//            Type = TransactionType.Debit,
//            Amount = 75,
//            Description = "Busca por ReferenceId",
//            ReferenceId = referenceId
//        };

//        await _repository.AddAsyncTransactionRegistry(transaction);
//        var result = await _repository.GetByReferenceIdAsync(referenceId, default);

//        Assert.NotNull(result);
//        Assert.Equal(referenceId, result.ReferenceId);
//        Assert.Equal("acc-integration", result.AccountId);
//        Assert.Equal(TransactionType.Debit, result.Type);
//        Assert.Equal(75, result.Amount);
//    }
//}
//using Microsoft.Extensions.Configuration;
//using Microsoft.Extensions.Logging;
//using Microsoft.Extensions.Logging.Abstractions;
//using PagueVeloz.Core.Domain.Entities;
//using PagueVeloz.Core.Domain.Enums;
//using PagueVeloz.Infrastructure.Persistence;
//using PagueVeloz.Infrastructure.Persistence.Repositories;

//public class TransactionTests
//{
//    private readonly TransactionRepository _repository;
//    private readonly ILogger<TransactionRepository> _logger;

//    public TransactionTests()
//    {
//        var basePath = AppContext.BaseDirectory;
//        var jsonPath = Path.Combine(basePath, "appsettings.json");

//        var configurationBuilder = new ConfigurationBuilder()
//            .AddJsonFile(jsonPath, optional: true, reloadOnChange: false)
//            .AddEnvironmentVariables();

//        var configuration = configurationBuilder.Build();

//        var connectionFactory = new ConnectionFactory(configuration);

//        _logger = NullLogger<TransactionRepository>.Instance;
//        _repository = new TransactionRepository(connectionFactory, _logger);
//    }

//    [Fact]
//    public async Task AddAsyncTransactionRegistry_ShouldPersistTransaction()
//    {
//        var transaction = new Transaction
//        {
//            AccountId = "acc-integration",
//            Type = TransactionType.Credit,
//            Amount = 150,
//            Description = "Teste integração Polly",
//            ReferenceId = Guid.NewGuid().ToString()
//        };

//        var transactionId = await _repository.AddAsyncTransactionRegistry(transaction);

//        Assert.False(string.IsNullOrEmpty(transactionId));
//    }

//    [Fact]
//    public async Task GetByReferenceIdAsync_ShouldReturnTransaction()
//    {
//        var referenceId = Guid.NewGuid().ToString();
//        var transaction = new Transaction
//        {
//            AccountId = "acc-integration",
//            Type = TransactionType.Debit,
//            Amount = 75,
//            Description = "Busca por ReferenceId",
//            ReferenceId = referenceId
//        };

//        await _repository.AddAsyncTransactionRegistry(transaction);
//        var result = await _repository.GetByReferenceIdAsync(referenceId, default);

//        Assert.NotNull(result);
//        Assert.Equal(referenceId, result.ReferenceId);
//        Assert.Equal("acc-integration", result.AccountId);
//        Assert.Equal(TransactionType.Debit, result.Type);
//        Assert.Equal(75, result.Amount);
//    }
//}