using AutoFixture;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using PagueVeloz.Core.Application.Commands.Transactions;
using PagueVeloz.Core.Application.DTOs;
using PagueVeloz.Core.Application.DTOs.Transaction;
using PagueVeloz.Core.Application.Handlers.Transactions;
using PagueVeloz.Core.Domain.Entities;
using PagueVeloz.Core.Domain.Enums;
using PagueVeloz.Core.Domain.Interfaces;

namespace PagueVeloz.UnitTests.TransactionsHandler
{
    [Trait("Transaction Handler", "Reversal")]
    public class ReversalCommandHandlerTests
    {
        private readonly Fixture _fixture;
        private readonly Mock<IAccountRepository> _accountRepositoryMock;
        private readonly Mock<ITransactionRepository> _transactionRepositoryMock;
        private readonly Mock<ILogger<ReversalCommandHandler>> _loggerMock;
        private readonly ReversalCommandHandler _handler;

        public ReversalCommandHandlerTests()
        {
            _fixture = new Fixture();
            _fixture.Behaviors
                .OfType<ThrowingRecursionBehavior>()
                .ToList()
                .ForEach(b => _fixture.Behaviors.Remove(b));
            _fixture.Behaviors.Add(new OmitOnRecursionBehavior());

            _accountRepositoryMock = new Mock<IAccountRepository>();
            _transactionRepositoryMock = new Mock<ITransactionRepository>();
            _loggerMock = new Mock<ILogger<ReversalCommandHandler>>();
            _handler = new ReversalCommandHandler(
                _accountRepositoryMock.Object,
                _transactionRepositoryMock.Object,
                _loggerMock.Object
            );
        }

        [Fact]
        public async Task Handle_ShouldReturnFailed_WhenOriginalTransactionNotFound()
        {
            var command = _fixture.Build<ReversalCommand>()
                .With(x => x.Amount, 100)
                .Create();

            _transactionRepositoryMock
                .Setup(x => x.GetByReferenceIdAsync(command.ReferenceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((Transaction)null);

            var result = await _handler.Handle(command, CancellationToken.None);

            result.Status.Should().Be("failed");
            result.ErrorMessage.Should().Contain("Transação original não encontrada");
            result.TransactionId.Should().Contain("PROCESSED");
            result.Balance.Should().Be(0);
            result.ReservedBalance.Should().Be(0);
            result.AvailableBalance.Should().Be(0);
        }

        [Fact]
        public async Task Handle_ShouldReturnFailed_WhenAccountNotFound()
        {
            var command = _fixture.Build<ReversalCommand>()
                .With(x => x.Amount, 100)
                .Create();

            _transactionRepositoryMock
                .Setup(x => x.GetByReferenceIdAsync(command.ReferenceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Transaction { Type = TransactionType.Debit, Amount = 100 });

            _accountRepositoryMock
                .Setup(x => x.GetByIdAsync(command.AccountId))
                .ReturnsAsync(Response<Account>.Fail("Account not found"));

            var result = await _handler.Handle(command, CancellationToken.None);

            result.Status.Should().Be("failed");
            result.ErrorMessage.Should().Be("Account not found");
            result.TransactionId.Should().Contain("PROCESSED");
            result.Balance.Should().Be(0);
            result.ReservedBalance.Should().Be(0);
            result.AvailableBalance.Should().Be(0);
        }

        [Fact]
        public async Task Handle_ShouldReturnRejected_WhenAmountIsZero()
        {
            var command = _fixture.Build<ReversalCommand>()
                .With(x => x.Amount, 0)
                .Create();

            var result = await _handler.Handle(command, CancellationToken.None);

            result.Status.Should().Be("failed");
            result.ErrorMessage.Should().NotBeNullOrEmpty();
            result.TransactionId.Should().Contain("PROCESSED");
        }

        [Fact]
        public async Task Handle_ShouldReturnFailed_WhenReversalCreditAndInsufficientBalance()
        {
            var account = _fixture.Build<Account>()
                .With(x => x.AccountId, "acc-123")
                .With(x => x.AvailableBalance, 50)
                .With(x => x.ReservedBalance, 0)
                .Create();

            var command = _fixture.Build<ReversalCommand>()
                .With(x => x.AccountId, account.AccountId)
                .With(x => x.Amount, 100)
                .Create();

            _transactionRepositoryMock
                .Setup(x => x.GetByReferenceIdAsync(command.ReferenceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Transaction { Type = TransactionType.Credit, Amount = 100 });

            _accountRepositoryMock
                .Setup(x => x.GetByIdAsync(command.AccountId))
                .ReturnsAsync(Response<Account>.Ok(account));

            var result = await _handler.Handle(command, CancellationToken.None);

            result.Status.Should().Be("failed");
            result.ErrorMessage.Should().Contain("Saldo insuficiente para estorno de crédito");
            result.TransactionId.Should().Contain("PROCESSED");
            result.AvailableBalance.Should().Be(account.AvailableBalance);
            result.ReservedBalance.Should().Be(account.ReservedBalance);
            result.Balance.Should().Be(account.AvailableBalance + account.ReservedBalance);
        }

        [Fact]
        public async Task Handle_ShouldReturnFailed_WhenReversalCaptureAndInsufficientBalance()
        {
            var account = _fixture.Build<Account>()
                .With(x => x.AccountId, "acc-123")
                .With(x => x.AvailableBalance, 50)
                .With(x => x.ReservedBalance, 0)
                .Create();

            var command = _fixture.Build<ReversalCommand>()
                .With(x => x.AccountId, account.AccountId)
                .With(x => x.Amount, 100)
                .Create();

            _transactionRepositoryMock
                .Setup(x => x.GetByReferenceIdAsync(command.ReferenceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Transaction { Type = TransactionType.Capture, Amount = 100 });

            _accountRepositoryMock
                .Setup(x => x.GetByIdAsync(command.AccountId))
                .ReturnsAsync(Response<Account>.Ok(account));

            var result = await _handler.Handle(command, CancellationToken.None);

            result.Status.Should().Be("failed");
            result.ErrorMessage.Should().Contain("Saldo insuficiente para estorno de captura de reserva");
            result.TransactionId.Should().Contain("PROCESSED");
            result.AvailableBalance.Should().Be(account.AvailableBalance);
            result.ReservedBalance.Should().Be(account.ReservedBalance);
            result.Balance.Should().Be(account.AvailableBalance + account.ReservedBalance);
        }

        [Theory]
        [InlineData(TransactionType.Credit, 1000, 500, 200, 800, 500)]
        [InlineData(TransactionType.Debit, 1000, 500, 200, 1200, 500)]
        [InlineData(TransactionType.Reserve, 1000, 500, 200, 1200, 300)]
        [InlineData(TransactionType.Capture, 1000, 500, 200, 800, 700)]
        public async Task Handle_ShouldReturnSuccess_WhenReversalIsProcessed(
            TransactionType originalType,
            long initialAvailable,
            long initialReserved,
            long amount,
            long expectedAvailable,
            long expectedReserved)
        {
            var account = _fixture.Build<Account>()
                .With(x => x.AccountId, "acc-123")
                .With(x => x.AvailableBalance, initialAvailable)
                .With(x => x.ReservedBalance, initialReserved)
                .Create();

            var command = _fixture.Build<ReversalCommand>()
                .With(x => x.AccountId, account.AccountId)
                .With(x => x.Amount, amount)
                .Create();

            var originalTransaction = new Transaction
            {
                Type = originalType,
                Amount = amount,
                AccountId = account.AccountId
            };

            _transactionRepositoryMock
                .Setup(x => x.GetByReferenceIdAsync(command.ReferenceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(originalTransaction);

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
            result.Balance.Should().Be(expectedAvailable + expectedReserved);
            result.AvailableBalance.Should().Be(expectedAvailable);
            result.ReservedBalance.Should().Be(expectedReserved);
            result.ErrorMessage.Should().BeNull();
        }

        [Fact]
        public async Task Handle_ShouldReturnFailed_WhenOriginalTransactionTypeIsNotSupported()
        {
            var account = _fixture.Build<Account>()
                .With(x => x.AccountId, "acc-123")
                .With(x => x.AvailableBalance, 1000)
                .With(x => x.ReservedBalance, 500)
                .Create();

            var command = _fixture.Build<ReversalCommand>()
                .With(x => x.AccountId, account.AccountId)
                .With(x => x.Amount, 200)
                .Create();

            var originalTransaction = new Transaction
            {
                Type = (TransactionType)9, // Tipo não suportado
                Amount = command.Amount,
                AccountId = account.AccountId
            };

            _transactionRepositoryMock
                .Setup(x => x.GetByReferenceIdAsync(command.ReferenceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(originalTransaction);

            _accountRepositoryMock
                .Setup(x => x.GetByIdAsync(command.AccountId))
                .ReturnsAsync(Response<Account>.Ok(account));

            var result = await _handler.Handle(command, CancellationToken.None);

            result.Status.Should().Be("failed");
            result.ErrorMessage.Should().Contain("Tipo de transação original não suportado");
            result.TransactionId.Should().Contain("PROCESSED");
        }

        [Fact]
        public async Task Handle_ShouldReturnFailed_WhenExceptionIsThrown()
        {
            var account = _fixture.Build<Account>()
                .With(x => x.AccountId, "acc-err")
                .With(x => x.AvailableBalance, 1000)
                .With(x => x.ReservedBalance, 100)
                .Create();

            var command = _fixture.Build<ReversalCommand>()
                .With(x => x.AccountId, account.AccountId)
                .With(x => x.Amount, 200)
                .Create();

            _transactionRepositoryMock
                .Setup(x => x.GetByReferenceIdAsync(command.ReferenceId, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new System.Exception("Database error"));

            var result = await _handler.Handle(command, CancellationToken.None);

            result.Status.Should().Be("failed");
            result.ErrorMessage.Should().Be("Database error");
            result.TransactionId.Should().Contain("PROCESSED");
            result.Balance.Should().Be(0);
            result.ReservedBalance.Should().Be(0);
            result.AvailableBalance.Should().Be(0);
        }

        [Fact]
        public async Task Handle_ShouldProcessManyReversals_ForSameAccount()
        {
            var account = _fixture.Build<Account>()
                .With(x => x.AccountId, "acc-batch")
                .With(x => x.AvailableBalance, 10000)
                .With(x => x.ReservedBalance, 5000)
                .Without(x => x.Transactions)
                .Create();

            var initialAvailable = account.AvailableBalance;
            var initialReserved = account.ReservedBalance;

            var amounts = new List<long> { 100, 200, 300, 400, 500 };
            var commands = amounts.Select(amount => _fixture.Build<ReversalCommand>()
                .With(x => x.AccountId, account.AccountId)
                .With(x => x.Amount, amount)
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

            _transactionRepositoryMock
                .Setup(x => x.GetByReferenceIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Transaction { Type = TransactionType.Debit, Amount = 100 });

            var results = new List<TransactionResponse>();
            long expectedAvailable = initialAvailable;
            foreach (var command in commands)
            {
                var result = await _handler.Handle(command, CancellationToken.None);
                results.Add(result);
                expectedAvailable += 100;
                result.Status.Should().Be("success");
                result.ErrorMessage.Should().BeNull();
                result.AvailableBalance.Should().BeGreaterThanOrEqualTo(0);
                result.ReservedBalance.Should().BeGreaterThanOrEqualTo(0);
            }
            account.AvailableBalance.Should().Be(expectedAvailable);
        }

        [Fact]
        public async Task Handle_ShouldProcessManyReversals_ForDifferentManyAccounts()
        {
            int accountCount = 5;
            int reversalsPerAccount = 2;
            var accounts = _fixture.Build<Account>()
                .Without(x => x.Transactions)
                .CreateMany(accountCount)
                .ToList();

            for (int i = 0; i < accounts.Count; i++)
            {
                accounts[i].AccountId = $"acc-{i + 1}";
                accounts[i].AvailableBalance = 10000;
                accounts[i].ReservedBalance = 1000;
            }

            var commands = new List<(ReversalCommand Command, Account Account, long Amount)>();
            foreach (var account in accounts)
            {
                for (int t = 1; t <= reversalsPerAccount; t++)
                {
                    long amount = t * 100;
                    var command = _fixture.Build<ReversalCommand>()
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

                _transactionRepositoryMock
                    .Setup(x => x.GetByReferenceIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new Transaction { Type = TransactionType.Debit, Amount = 100 });
            }

            var results = new List<TransactionResponse>();
            foreach (var (command, _, _) in commands)
            {
                var result = await _handler.Handle(command, CancellationToken.None);
                results.Add(result);
            }

            results.Should().HaveCount(accountCount * reversalsPerAccount);
            foreach (var result in results)
            {
                result.Status.Should().Be("success");
                result.TransactionId.Should().Contain("testTransaction");
                result.Balance.Should().BeGreaterThanOrEqualTo(0);
                result.AvailableBalance.Should().BeGreaterThanOrEqualTo(0);
                result.ReservedBalance.Should().BeGreaterThanOrEqualTo(0);
                result.ErrorMessage.Should().BeNull();
            }
        }
    }
}