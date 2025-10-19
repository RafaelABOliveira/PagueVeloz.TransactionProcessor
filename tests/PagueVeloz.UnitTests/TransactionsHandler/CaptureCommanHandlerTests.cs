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
    [Trait("Transaction Handler", "Capture")]
    public class CaptureCommandHandlerTests
    {
        private readonly Fixture _fixture;
        private readonly Mock<IAccountRepository> _accountRepositoryMock;
        private readonly Mock<ITransactionRepository> _transactionRepositoryMock;
        private readonly Mock<ILogger<CaptureCommandHandler>> _loggerMock;
        private readonly CaptureCommandHandler _handler;

        public CaptureCommandHandlerTests()
        {
            _fixture = new Fixture();
            _fixture.Behaviors
                .OfType<ThrowingRecursionBehavior>()
                .ToList()
                .ForEach(b => _fixture.Behaviors.Remove(b));
            _fixture.Behaviors.Add(new OmitOnRecursionBehavior());

            _accountRepositoryMock = new Mock<IAccountRepository>();
            _transactionRepositoryMock = new Mock<ITransactionRepository>();
            _loggerMock = new Mock<ILogger<CaptureCommandHandler>>();
            _handler = new CaptureCommandHandler(
                _accountRepositoryMock.Object,
                _transactionRepositoryMock.Object,
                _loggerMock.Object
            );
        }

        [Fact]
        public async Task Handle_ShouldReturnRejected_WhenAmountIsLessThanOne()
        {
            var command = _fixture.Build<CaptureCommand>()
                .With(x => x.Amount, 0)
                .Create();

            var result = await _handler.Handle(command, CancellationToken.None);

            result.Status.Should().Be("rejected");
            result.ErrorMessage.Should().NotBeNullOrEmpty();
            result.TransactionId.Should().Contain("REJECTED");
        }

        [Fact]
        public async Task Handle_ShouldReturnFailed_WhenAccountNotFound()
        {
            var command = _fixture.Build<CaptureCommand>()
                .With(x => x.Amount, 100)
                .Create();

            _accountRepositoryMock
                .Setup(x => x.GetByIdAsync(command.AccountId))
                .ReturnsAsync(Response<Account>.Fail("Account not found"));

            var result = await _handler.Handle(command, CancellationToken.None);

            result.Status.Should().Be("failed");
            result.ErrorMessage.Should().Be("Account not found");
            result.TransactionId.Should().Contain("FAILED");
            result.Balance.Should().Be(0);
            result.ReservedBalance.Should().Be(0);
            result.AvailableBalance.Should().Be(0);
        }

        [Fact]
        public async Task Handle_ShouldReturnFailed_WhenInsufficientReservedBalance()
        {
            var account = _fixture.Build<Account>()
                .With(x => x.AccountId, "acc-123")
                .With(x => x.ReservedBalance, 50)
                .With(x => x.AvailableBalance, 100)
                .Create();

            var command = _fixture.Build<CaptureCommand>()
                .With(x => x.AccountId, account.AccountId)
                .With(x => x.Amount, 200)
                .Create();

            _accountRepositoryMock
                .Setup(x => x.GetByIdAsync(command.AccountId))
                .ReturnsAsync(Response<Account>.Ok(account));

            var result = await _handler.Handle(command, CancellationToken.None);

            result.Status.Should().Be("failed");
            result.ErrorMessage.Should().Contain("Saldo reservado insuficiente");
            result.TransactionId.Should().Contain("FAILED");
            result.Balance.Should().Be(account.AvailableBalance + account.ReservedBalance);
            result.AvailableBalance.Should().Be(account.AvailableBalance);
            result.ReservedBalance.Should().Be(account.ReservedBalance);
        }

        [Fact]
        public async Task Handle_ShouldReturnSuccess_WhenCaptureIsProcessed()
        {
            var account = _fixture.Build<Account>()
                .With(x => x.AccountId, "acc-123")
                .With(x => x.ReservedBalance, 500)
                .With(x => x.AvailableBalance, 1000)
                .Create();

            var command = _fixture.Build<CaptureCommand>()
                .With(x => x.AccountId, account.AccountId)
                .With(x => x.Amount, 200)
                .Create();

            _accountRepositoryMock
                .Setup(x => x.GetByIdAsync(command.AccountId))
                .ReturnsAsync(Response<Account>.Ok(account));

            _accountRepositoryMock
                .Setup(x => x.UpdateAsync(It.IsAny<Account>()))
                .Returns(Task.CompletedTask);

            _transactionRepositoryMock
                .Setup(x => x.AddAsyncTransactionRegistry(It.IsAny<Transaction>()))
                .ReturnsAsync("testTransaction");

            var result = await _handler.Handle(command, CancellationToken.None);

            result.Status.Should().Be("success");
            result.TransactionId.Should().Contain("testTransaction");
            result.Balance.Should().Be(account.AvailableBalance + account.ReservedBalance);
            result.AvailableBalance.Should().Be(account.AvailableBalance);
            result.ReservedBalance.Should().Be(account.ReservedBalance);
            result.ErrorMessage.Should().BeNull();
        }

        [Fact]
        public async Task Handle_ShouldReturnFailed_WhenExceptionIsThrown()
        {
            var account = _fixture.Build<Account>()
                .With(x => x.AccountId, "acc-123")
                .With(x => x.ReservedBalance, 500)
                .With(x => x.AvailableBalance, 1000)
                .Create();

            var command = _fixture.Build<CaptureCommand>()
                .With(x => x.AccountId, account.AccountId)
                .With(x => x.Amount, 200)
                .Create();

            _accountRepositoryMock
                .Setup(x => x.GetByIdAsync(command.AccountId))
                .ThrowsAsync(new Exception("Database error"));

            var result = await _handler.Handle(command, CancellationToken.None);

            result.Status.Should().Be("failed");
            result.ErrorMessage.Should().Be("Database error");
            result.TransactionId.Should().Contain("FAILED");
            result.Balance.Should().Be(0);
            result.ReservedBalance.Should().Be(0);
            result.AvailableBalance.Should().Be(0);
        }

        [Fact]
        public async Task Handle_ShouldProcessManyCaptureTransactionsAndVerifyReservedBalance_ForSameAccount()
        {
            var account = _fixture.Build<Account>()
                .With(x => x.AccountId, "acc-batch")
                .With(x => x.AvailableBalance, 1000)
                .With(x => x.ReservedBalance, 100000)
                .Without(x => x.Transactions)
                .Create();

            var initialReserved = account.ReservedBalance;
            var amounts = Enumerable.Range(1, 20).Select(i => (long)(i * 100)).ToList();
            var commands = _fixture.Build<CaptureCommand>()
                .With(x => x.AccountId, account.AccountId)
                .CreateMany(amounts.Count)
                .ToList();

            for (int i = 0; i < commands.Count; i++)
                commands[i].Amount = amounts[i];

            _accountRepositoryMock
                .Setup(x => x.GetByIdAsync(account.AccountId))
                .ReturnsAsync(Response<Account>.Ok(account));

            _accountRepositoryMock
                .Setup(x => x.UpdateAsync(It.IsAny<Account>()))
                .Returns(Task.CompletedTask);

            _transactionRepositoryMock
                .Setup(x => x.AddAsyncTransactionRegistry(It.Is<Transaction>(t => t.AccountId == account.AccountId)))
                .ReturnsAsync("testTransaction");

            var results = new List<TransactionResponse>();
            foreach (var command in commands)
            {
                var result = await _handler.Handle(command, CancellationToken.None);
                results.Add(result);
            }

            results.Should().HaveCount(amounts.Count);
            long expectedReserved = initialReserved;
            for (int i = 0; i < results.Count; i++)
            {
                var result = results[i];
                if (expectedReserved >= amounts[i])
                {
                    result.Status.Should().Be("success");
                    result.TransactionId.Should().Contain("testTransaction");
                    result.ErrorMessage.Should().BeNull();
                    expectedReserved -= amounts[i];
                }
                else
                {
                    result.Status.Should().Be("failed");
                    result.TransactionId.Should().Contain("FAILED");
                    result.ErrorMessage.Should().Contain("Saldo reservado insuficiente");
                }
                result.ReservedBalance.Should().BeGreaterThanOrEqualTo(0);
                result.Balance.Should().BeGreaterThanOrEqualTo(0);
            }
            account.ReservedBalance.Should().Be(expectedReserved);
        }

        [Fact]
        public async Task Handle_ShouldProcessManyCaptureTransactions_ForDifferentManyAccounts()
        {
            int accountCount = 10;
            int transactionsPerAccount = 2;
            var accounts = _fixture.Build<Account>()
                .Without(x => x.Transactions)
                .CreateMany(accountCount)
                .ToList();

            for (int i = 0; i < accounts.Count; i++)
            {
                accounts[i].AccountId = $"acc-{i + 1}";
                accounts[i].AvailableBalance = 1000;
                accounts[i].ReservedBalance = 10000;
            }

            var commands = new List<(CaptureCommand Command, Account Account, long Amount)>();
            foreach (var account in accounts)
            {
                for (int t = 1; t <= transactionsPerAccount; t++)
                {
                    long amount = t * 100;
                    var command = _fixture.Build<CaptureCommand>()
                        .With(x => x.AccountId, account.AccountId)
                        .With(x => x.Amount, amount)
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
                    .Setup(x => x.AddAsyncTransactionRegistry(It.Is<Transaction>(t => t.AccountId == account.AccountId)))
                    .ReturnsAsync("testTransaction");
            }

            var results = new List<TransactionResponse>();
            foreach (var (command, _, _) in commands)
            {
                var result = await _handler.Handle(command, CancellationToken.None);
                results.Add(result);
            }

            results.Should().HaveCount(accountCount * transactionsPerAccount);
            foreach (var result in results)
            {
                result.Status.Should().Be("success");
                result.TransactionId.Should().Contain("testTransaction");
                result.Balance.Should().BeGreaterThanOrEqualTo(0);
                result.AvailableBalance.Should().BeGreaterThanOrEqualTo(0);
                result.ReservedBalance.Should().BeGreaterThanOrEqualTo(0);
                result.ErrorMessage.Should().BeNull();
            }

            foreach (var account in accounts)
            {
                long expectedSum = Enumerable.Range(1, transactionsPerAccount).Select(x => x * 100L).Sum();
                account.ReservedBalance.Should().Be(10000 - expectedSum);
            }
        }

        [Fact]
        public async Task Handle_ShouldReturnFailed_WhenReservedBalanceIsZero()
        {
            var account = _fixture.Build<Account>()
                .With(x => x.AccountId, "acc-zero")
                .With(x => x.ReservedBalance, 0)
                .With(x => x.AvailableBalance, 1000)
                .Create();

            var command = _fixture.Build<CaptureCommand>()
                .With(x => x.AccountId, account.AccountId)
                .With(x => x.Amount, 100)
                .Create();

            _accountRepositoryMock
                .Setup(x => x.GetByIdAsync(command.AccountId))
                .ReturnsAsync(Response<Account>.Ok(account));

            var result = await _handler.Handle(command, CancellationToken.None);

            result.Status.Should().Be("failed");
            result.ErrorMessage.Should().Contain("Saldo reservado insuficiente");
            result.TransactionId.Should().Contain("FAILED");
            result.ReservedBalance.Should().Be(0);
        }
    }
}