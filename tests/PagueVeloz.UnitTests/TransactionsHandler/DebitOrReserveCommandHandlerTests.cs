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
    [Trait("Transaction Handler", "DebitOrReserve")]
    public class DebitOrReserveCommandHandlerTests
    {
        private readonly Fixture _fixture;
        private readonly Mock<IAccountRepository> _accountRepositoryMock;
        private readonly Mock<ITransactionRepository> _transactionRepositoryMock;
        private readonly Mock<ILogger<DebitOrReserveCommandHandler>> _loggerMock;
        private readonly DebitOrReserveCommandHandler _handler;

        public DebitOrReserveCommandHandlerTests()
        {
            _fixture = new Fixture();
            _fixture.Behaviors
                .OfType<ThrowingRecursionBehavior>()
                .ToList()
                .ForEach(b => _fixture.Behaviors.Remove(b));
            _fixture.Behaviors.Add(new OmitOnRecursionBehavior());

            _accountRepositoryMock = new Mock<IAccountRepository>();
            _transactionRepositoryMock = new Mock<ITransactionRepository>();
            _loggerMock = new Mock<ILogger<DebitOrReserveCommandHandler>>();
            _handler = new DebitOrReserveCommandHandler(
                _accountRepositoryMock.Object,
                _transactionRepositoryMock.Object,
                _loggerMock.Object
            );
        }

        [Fact]
        public async Task Handle_ShouldReturnRejected_WhenAmountIsLessThanOne()
        {
            var command = _fixture.Build<DebitOrReserveCommand>()
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
            var command = _fixture.Build<DebitOrReserveCommand>()
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
        public async Task Handle_ShouldReturnFailed_WhenInsufficientBalance()
        {
            var account = _fixture.Build<Account>()
                .With(x => x.AccountId, "acc-123")
                .With(x => x.AvailableBalance, 100)
                .With(x => x.CreditLimit, 50)
                .Create();

            var command = _fixture.Build<DebitOrReserveCommand>()
                .With(x => x.AccountId, account.AccountId)
                .With(x => x.Amount, 200)
                .Create();

            _accountRepositoryMock
                .Setup(x => x.GetByIdAsync(command.AccountId))
                .ReturnsAsync(Response<Account>.Ok(account));

            var result = await _handler.Handle(command, CancellationToken.None);

            result.Status.Should().Be("failed");
            result.ErrorMessage.Should().Contain("Saldo insuficiente");
            result.TransactionId.Should().Contain("FAILED");
            result.Balance.Should().Be(account.AvailableBalance + account.ReservedBalance);
            result.AvailableBalance.Should().Be(account.AvailableBalance);
            result.ReservedBalance.Should().Be(account.ReservedBalance);
        }

        [Fact]
        public async Task Handle_ShouldReturnSuccess_WhenDebitIsProcessed()
        {
            var account = _fixture.Build<Account>()
                .With(x => x.AccountId, "acc-123")
                .With(x => x.AvailableBalance, 1000)
                .With(x => x.ReservedBalance, 500)
                .With(x => x.CreditLimit, 200)
                .Create();

            var command = _fixture.Build<DebitOrReserveCommand>()
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
            result.TransactionId.Should().Contain("PROCESSED");
            result.Balance.Should().BeGreaterThanOrEqualTo(0);
            result.AvailableBalance.Should().BeGreaterThanOrEqualTo(0);
            result.ReservedBalance.Should().BeGreaterThanOrEqualTo(0);
            result.ErrorMessage.Should().BeNull();
        }

        [Fact]
        public async Task Handle_ShouldReturnFailed_WhenExceptionIsThrown()
        {
            var account = _fixture.Build<Account>()
                .With(x => x.AccountId, "acc-123")
                .With(x => x.AvailableBalance, 1000)
                .Create();

            var command = _fixture.Build<DebitOrReserveCommand>()
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
        public async Task Handle_ShouldProcessManyDebitTransactionsAndVerifyAvailableBalance_ForSameAccount()
        {
            var account = _fixture.Build<Account>()
                .With(x => x.AccountId, "acc-batch")
                .With(x => x.AvailableBalance, 1000000000000)
                .With(x => x.ReservedBalance, 0)
                .Without(x => x.Transactions)
                .Create();

            var initialBalance = account.AvailableBalance;

            var amounts = Enumerable.Range(1, 50).Select(i => (long)(i * 100)).ToList();
            var commands = _fixture.Build<DebitOrReserveCommand>()
                .With(x => x.AccountId, account.AccountId)
                .CreateMany(amounts.Count)
                .ToList();

            for (int debitCommand = 0; debitCommand < commands.Count; debitCommand++)
                commands[debitCommand].Amount = amounts[debitCommand];

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
            long expectedAvailableBalance = initialBalance;
            for (int resultOperation = 0; resultOperation < results.Count; resultOperation++)
            {
                var result = results[resultOperation];
                if (expectedAvailableBalance >= amounts[resultOperation])
                {
                    result.Status.Should().Be("success");
                    result.TransactionId.Should().Contain("PROCESSED");
                    result.ErrorMessage.Should().BeNull();
                    expectedAvailableBalance -= amounts[resultOperation];
                }
                else
                {
                    result.Status.Should().Be("failed");
                    result.TransactionId.Should().Contain("FAILED");
                    result.ErrorMessage.Should().Contain("Saldo insuficiente");
                }
                result.AvailableBalance.Should().BeGreaterThanOrEqualTo(0);
                result.Balance.Should().BeGreaterThanOrEqualTo(0);
            }

            account.AvailableBalance.Should().Be(expectedAvailableBalance);
        }

        [Fact]
        public async Task Handle_ShouldProcessManyDebitTransactions_ForDifferentManyAccounts()
        {
            int accountCount = 20;
            int transactionsPerAccount = 3;
            var accounts = _fixture.Build<Account>()
                .Without(x => x.Transactions)
                .CreateMany(accountCount)
                .ToList();

            for (int account = 0; account < accounts.Count; account++)
            {
                accounts[account].AccountId = $"acc-{account + 1}";
                accounts[account].AvailableBalance = 10000;
                accounts[account].ReservedBalance = 10;
                accounts[account].CreditLimit = 1000;
            }

            var commands = new List<(DebitOrReserveCommand Command, Account Account, long Amount)>();
            foreach (var account in accounts)
            {
                for (int t = 1; t <= transactionsPerAccount; t++)
                {
                    long amount = t * 100;
                    var command = _fixture.Build<DebitOrReserveCommand>()
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
                result.TransactionId.Should().Contain("PROCESSED");
                result.Balance.Should().BeGreaterThanOrEqualTo(0);
                result.AvailableBalance.Should().BeGreaterThanOrEqualTo(0);
                result.ReservedBalance.Should().BeGreaterThanOrEqualTo(0);
                result.ErrorMessage.Should().BeNull();
            }

            foreach (var account in accounts)
            {
                long expectedSum = Enumerable.Range(1, transactionsPerAccount).Select(x => x * 100L).Sum();
                account.AvailableBalance.Should().Be(10000 - expectedSum);
            }
        }

        [Fact]
        public async Task Handle_ShouldReturnSuccess_WhenReservationIsProcessed()
        {
            var account = _fixture.Build<Account>()
                .With(x => x.AccountId, "acc-reserve")
                .With(x => x.AvailableBalance, 1000)
                .With(x => x.ReservedBalance, 100)
                .Create();

            var command = _fixture.Build<DebitOrReserveCommand>()
                .With(x => x.AccountId, account.AccountId)
                .With(x => x.Amount, 200)
                .With(x => x.IsReservation, true)
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
            result.TransactionId.Should().Contain("PROCESSED");
            result.Balance.Should().Be(account.AvailableBalance + account.ReservedBalance);
            result.AvailableBalance.Should().Be(account.AvailableBalance);
            result.ReservedBalance.Should().Be(account.ReservedBalance);
            result.ErrorMessage.Should().BeNull();
        }

        [Fact]
        public async Task Handle_ShouldReturnFailed_WhenReservationInsufficientAvailableBalance()
        {
            var account = _fixture.Build<Account>()
                .With(x => x.AccountId, "acc-reserve-fail")
                .With(x => x.AvailableBalance, 50)
                .With(x => x.ReservedBalance, 0)
                .With(x => x.CreditLimit, 0)
                .Create();

            var command = _fixture.Build<DebitOrReserveCommand>()
                .With(x => x.AccountId, account.AccountId)
                .With(x => x.Amount, 100)
                .With(x => x.IsReservation, true)
                .Create();

            _accountRepositoryMock
                .Setup(x => x.GetByIdAsync(command.AccountId))
                .ReturnsAsync(Response<Account>.Ok(account));

            var result = await _handler.Handle(command, CancellationToken.None);

            result.Status.Should().Be("failed");
            result.TransactionId.Should().Contain("FAILED");
            result.ErrorMessage.Should().Contain("Saldo insuficiente");
            result.AvailableBalance.Should().Be(account.AvailableBalance);
            result.ReservedBalance.Should().Be(account.ReservedBalance);
            result.Balance.Should().Be(account.AvailableBalance + account.ReservedBalance);
        }

        [Fact]
        public async Task Handle_ShouldProcessManyReservations_ForSameAccount()
        {
            var account = _fixture.Build<Account>()
                .With(x => x.AccountId, "acc-reserve-batch")
                .With(x => x.AvailableBalance, 1000)
                .With(x => x.ReservedBalance, 0)
                .Without(x => x.Transactions)
                .Create();

            var initialAvailable = account.AvailableBalance;
            var initialReserved = account.ReservedBalance;

            var amounts = new List<long> { 100, 200, 300, 400, 500 };
            var commands = amounts.Select(amount => _fixture.Build<DebitOrReserveCommand>()
                .With(x => x.AccountId, account.AccountId)
                .With(x => x.Amount, amount)
                .With(x => x.IsReservation, true)
                .Create()).ToList();

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
            long expectedAvailable = initialAvailable;
            long expectedReserved = initialReserved;
            foreach (var command in commands)
            {
                var result = await _handler.Handle(command, CancellationToken.None);
                results.Add(result);
                if (expectedAvailable >= command.Amount)
                {
                    expectedAvailable -= command.Amount;
                    expectedReserved += command.Amount;
                    result.Status.Should().Be("success");
                    result.ErrorMessage.Should().BeNull();
                }
                else
                {
                    result.Status.Should().Be("failed");
                    result.ErrorMessage.Should().Contain("Saldo insuficiente");
                }
                result.AvailableBalance.Should().BeGreaterThanOrEqualTo(0);
                result.ReservedBalance.Should().BeGreaterThanOrEqualTo(0);
            }
            account.AvailableBalance.Should().Be(expectedAvailable);
            account.ReservedBalance.Should().Be(expectedReserved);
        }

        [Fact]
        public async Task Handle_ShouldUseCreditLimit_WhenDebitExceedsAvailableBalance()
        {
            // Arrange
            var account = _fixture.Build<Account>()
                .With(x => x.AccountId, "acc-credit-limit")
                .With(x => x.AvailableBalance, 100)
                .With(x => x.CreditLimit, 200)
                .With(x => x.ReservedBalance, 0)
                .Create();

            var command = _fixture.Build<DebitOrReserveCommand>()
                .With(x => x.AccountId, account.AccountId)
                .With(x => x.Amount, 150) // Exceeds available balance, will use 50 from credit limit
                .With(x => x.IsReservation, false)
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

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.Status.Should().Be("success");
            result.TransactionId.Should().Contain("PROCESSED");
            result.ErrorMessage.Should().BeNull();
            result.AvailableBalance.Should().Be(0);
            result.ReservedBalance.Should().Be(0);
            account.CreditLimit.Should().Be(150);
            result.Balance.Should().Be(0);
        }
    }
}