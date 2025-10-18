using AutoFixture;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using PagueVeloz.Core.Application.Commands.Transactions;
using PagueVeloz.Core.Application.DTOs;
using PagueVeloz.Core.Application.DTOs.Transaction;
using PagueVeloz.Core.Application.Handlers.Transactions;
using PagueVeloz.Core.Domain.Entities;
using PagueVeloz.Core.Domain.Interfaces;

namespace PagueVeloz.UnitTests.TransactionsHandler
{
    [Trait("Transaction Handler", "Credit")]
    public class CreditCommandHandlerTests
    {
        private readonly Fixture _fixture;
        private readonly Mock<IAccountRepository> _accountRepositoryMock;
        private readonly Mock<ITransactionRepository> _transactionRepositoryMock;
        private readonly Mock<ILogger<CreditCommandHandler>> _loggerMock;
        private readonly CreditCommandHandler _handler;

        public CreditCommandHandlerTests()
        {
            _fixture = new Fixture();
            _fixture.Behaviors
                .OfType<ThrowingRecursionBehavior>()
                .ToList()
                .ForEach(b => _fixture.Behaviors.Remove(b));
            _fixture.Behaviors.Add(new OmitOnRecursionBehavior());

            _accountRepositoryMock = new Mock<IAccountRepository>();
            _transactionRepositoryMock = new Mock<ITransactionRepository>();
            _loggerMock = new Mock<ILogger<CreditCommandHandler>>();
            _handler = new CreditCommandHandler(
                _accountRepositoryMock.Object,
                _transactionRepositoryMock.Object,
                _loggerMock.Object
            );
        }

        [Fact]
        public async Task Handle_ShouldReturnRejected_WhenAmountIsLessThanOne()
        {
            // Arrange
            var command = _fixture.Build<CreditCommand>()
                .With(x => x.Amount, 0)
                .Create();

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.Status.Should().Be("rejected");
            result.ErrorMessage.Should().NotBeNullOrEmpty();
            result.TransactionId.Should().Contain("REJECTED");
        }

        [Fact]
        public async Task Handle_ShouldReturnFailed_WhenAccountNotFound()
        {
            // Arrange
            var command = _fixture.Build<CreditCommand>()
                .With(x => x.Amount, 100)
                .Create();

            _accountRepositoryMock
                .Setup(x => x.GetByIdAsync(command.AccountId))
                .ReturnsAsync(Response<Account>.Fail("Account not found"));

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.Status.Should().Be("failed");
            result.ErrorMessage.Should().Be("Account not found");
            result.TransactionId.Should().Contain("FAILED");
            result.Balance.Should().Be(0);
            result.ReservedBalance.Should().Be(0);
            result.AvailableBalance.Should().Be(0);
        }

        [Fact]
        public async Task Handle_ShouldReturnSuccess_WhenCreditIsProcessed()
        {
            // Arrange
            var account = _fixture.Build<Account>()
                .With(x => x.AccountId, "acc-123")
                .With(x => x.AvailableBalance, 1000)
                .With(x => x.ReservedBalance, 500)
                .Create();

            var command = _fixture.Build<CreditCommand>()
                .With(x => x.AccountId, account.AccountId)
                .With(x => x.Amount, 200)
                .With(x => x.Currency, "BRL")
                .Create();

            _accountRepositoryMock
                .Setup(x => x.GetByIdAsync(command.AccountId))
                .ReturnsAsync(Response<Account>.Ok(account));

            _accountRepositoryMock
                .Setup(x => x.UpdateAsync(It.IsAny<Account>()))
                .Returns(Task.CompletedTask);

            _transactionRepositoryMock
                .Setup(x => x.AddAsync(It.IsAny<Transaction>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.Status.Should().Be("success");
            result.TransactionId.Should().Contain("PROCESSED");
            result.Balance.Should().BeGreaterThanOrEqualTo(0);
            result.AvailableBalance.Should().BeGreaterThanOrEqualTo(0);
            result.ReservedBalance.Should().BeGreaterThanOrEqualTo(0);
            result.ErrorMessage.Should().BeNull();
        }

        [Fact]
        public async Task Handle_ShouldReturnFailed_WhenExceptionIsThrown()
        {
            // Arrange
            var account = _fixture.Build<Account>()
                .With(x => x.AccountId, "acc-123")
                .With(x => x.AvailableBalance, 1000)
                .Create();

            var command = _fixture.Build<CreditCommand>()
                .With(x => x.AccountId, account.AccountId)
                .With(x => x.Amount, 200)
                .Create();

            _accountRepositoryMock
                .Setup(x => x.GetByIdAsync(command.AccountId))
                .ThrowsAsync(new Exception("Database error"));

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.Status.Should().Be("failed");
            result.ErrorMessage.Should().Be("Database error");
            result.TransactionId.Should().Contain("FAILED");
            result.Balance.Should().Be(0);
            result.ReservedBalance.Should().Be(0);
            result.AvailableBalance.Should().Be(0);
        }

        [Fact]
        public async Task Handle_ShouldProcessManyCreditTransactions_ForSameAccount()
        {
            // Arrange
            var account = _fixture.Build<Account>()
                .With(x => x.AccountId, "acc-batch")
                .With(x => x.AvailableBalance, 1000)
                .With(x => x.ReservedBalance, 0)
                .Without(x => x.Transactions)
                .Create();

            var initialBalance = account.AvailableBalance;

            var amounts = Enumerable.Range(1, 50).Select(i => (long)(i * 100)).ToList();
            var commands = _fixture.Build<CreditCommand>()
                .With(x => x.AccountId, account.AccountId)
                .With(x => x.Currency, "BRL")
                .CreateMany(amounts.Count)
                .ToList();

            for (int creditCommand = 0; creditCommand < commands.Count; creditCommand++)
                commands[creditCommand].Amount = amounts[creditCommand];

            _accountRepositoryMock
                .Setup(x => x.GetByIdAsync(account.AccountId))
                .ReturnsAsync(Response<Account>.Ok(account));

            _accountRepositoryMock
                .Setup(x => x.UpdateAsync(It.IsAny<Account>()))
                .Returns(Task.CompletedTask);

            _transactionRepositoryMock
                .Setup(x => x.AddAsync(It.IsAny<Transaction>()))
                .Returns(Task.CompletedTask);

            // Act
            var results = new List<TransactionResponse>();
            foreach (var command in commands)
            {
                var result = await _handler.Handle(command, CancellationToken.None);
                results.Add(result);
            }

            // Assert
            results.Should().HaveCount(amounts.Count);
            foreach (var result in results)
            {
                result.Status.Should().Be("success");
                result.TransactionId.Should().Contain("PROCESSED");
                result.Balance.Should().BeGreaterThanOrEqualTo(1);
                result.AvailableBalance.Should().BeGreaterThanOrEqualTo(1);
                result.ErrorMessage.Should().BeNull();
            }

            account.AvailableBalance.Should().Be(initialBalance + amounts.Sum());
        }

        [Fact]
        public async Task Handle_ShouldReturnRejectedResponse_WhenAmountIsLessThanOne()
        {
            // Arrange
            var accountId = "acc-reject";
            var command = _fixture.Build<CreditCommand>()
                .With(x => x.AccountId, accountId)
                .With(x => x.Amount, 0)
                .With(x => x.Currency, "BRL")
                .Create();

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.Should().NotBeNull();
            result.TransactionId.Should().Be($"TXN-{accountId}-REJECTED");
            result.Status.Should().Be("rejected");
            result.ErrorMessage.Should().Be("O valor do crédito deve ser igual ou superior a 1 centavo (Amount >= 1).");
            result.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
            result.Balance.Should().Be(0);
            result.ReservedBalance.Should().Be(0);
            result.AvailableBalance.Should().Be(0);
            result.Balance.Should().Be(0);
            result.ReservedBalance.Should().Be(0);
            result.AvailableBalance.Should().Be(0);
        }

        [Fact]
        public async Task Handle_ShouldProcessManyCreditTransactions_ForDifferentManyAccounts()
        {
            // Arrange
            int accountCount = 50;
            int transactionsPerAccount = 30;
            var accounts = _fixture.Build<Account>()
                .Without(x => x.Transactions)
                .CreateMany(accountCount)
                .ToList();

            for (int account = 0; account < accounts.Count; account++)
            {
                accounts[account].AccountId = $"acc-{account + 1}";
                accounts[account].AvailableBalance = 1000;
                accounts[account].ReservedBalance = 10;
            }

            var commands = new List<(CreditCommand Command, Account Account, long Amount)>();
            foreach (var account in accounts)
            {
                for (int t = 1; t <= transactionsPerAccount; t++)
                {
                    long amount = t * 100;
                    var command = _fixture.Build<CreditCommand>()
                        .With(x => x.AccountId, account.AccountId)
                        .With(x => x.Amount, amount)
                        .With(x => x.Currency, "BRL")
                        .Create();
                    commands.Add((command, account, amount));
                }
            }

            foreach (var account in accounts)
            {
                _accountRepositoryMock
                    .Setup(x => x.GetByIdAsync(account.AccountId))
                    .ReturnsAsync(Response<Account>.Ok(account));

                _accountRepositoryMock
                    .Setup(x => x.UpdateAsync(It.Is<Account>(a => a.AccountId == account.AccountId)))
                    .Returns(Task.CompletedTask);

                _transactionRepositoryMock
                    .Setup(x => x.AddAsync(It.Is<Transaction>(t => t.AccountId == account.AccountId)))
                    .Returns(Task.CompletedTask);
            }

            // Act
            var results = new List<TransactionResponse>();
            foreach (var (command, _, _) in commands)
            {
                var result = await _handler.Handle(command, CancellationToken.None);
                results.Add(result);
            }

            // Assert
            results.Should().HaveCount(accountCount * transactionsPerAccount);
            foreach (var result in results)
            {
                result.Status.Should().Be("success");
                result.TransactionId.Should().Contain("PROCESSED");
                result.Balance.Should().BeGreaterThanOrEqualTo(1);
                result.AvailableBalance.Should().BeGreaterThanOrEqualTo(1);
                result.ReservedBalance.Should().BeGreaterThanOrEqualTo(1);
                result.ErrorMessage.Should().BeNull();
            }

            // Each account's available balance should be increased by the sum of its transaction amounts
            foreach (var account in accounts)
            {
                long expectedSum = Enumerable.Range(1, transactionsPerAccount).Select(x => x * 100L).Sum();
                account.AvailableBalance.Should().Be(1000 + expectedSum);
            }
        }
    }
}