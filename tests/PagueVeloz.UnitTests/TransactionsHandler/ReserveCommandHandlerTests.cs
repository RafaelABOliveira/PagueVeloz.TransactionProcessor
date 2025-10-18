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
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace PagueVeloz.UnitTests.TransactionsHandler
{
    [Trait("Transaction Handler", "Reserve")]
    public class ReserveCommandHandlerTests
    {
        private readonly Fixture _fixture;
        private readonly Mock<IAccountRepository> _accountRepositoryMock;
        private readonly Mock<ITransactionRepository> _transactionRepositoryMock;
        private readonly Mock<ILogger<ReserveCommandHandler>> _loggerMock;
        private readonly ReserveCommandHandler _handler;

        public ReserveCommandHandlerTests()
        {
            _fixture = new Fixture();
            _fixture.Behaviors
                .OfType<ThrowingRecursionBehavior>()
                .ToList()
                .ForEach(b => _fixture.Behaviors.Remove(b));
            _fixture.Behaviors.Add(new OmitOnRecursionBehavior());

            _accountRepositoryMock = new Mock<IAccountRepository>();
            _transactionRepositoryMock = new Mock<ITransactionRepository>();
            _loggerMock = new Mock<ILogger<ReserveCommandHandler>>();
            _handler = new ReserveCommandHandler(
                _accountRepositoryMock.Object,
                _transactionRepositoryMock.Object,
                _loggerMock.Object
            );
        }

        [Fact]
        public async Task Handle_ShouldReturnRejected_WhenAmountIsLessThanOne()
        {
            var command = _fixture.Build<ReserveCommand>()
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
            var command = _fixture.Build<ReserveCommand>()
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
        public async Task Handle_ShouldReturnFailed_WhenReservationInsufficientAvailableBalance()
        {
            var account = _fixture.Build<Account>()
                .With(x => x.AccountId, "acc-reserve-fail")
                .With(x => x.AvailableBalance, 50)
                .With(x => x.ReservedBalance, 0)
                .With(x => x.CreditLimit, 0)
                .Create();

            var command = _fixture.Build<ReserveCommand>()
                .With(x => x.AccountId, account.AccountId)
                .With(x => x.Amount, 100)
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
        public async Task Handle_ShouldReturnSuccess_WhenReservationIsProcessed()
        {
            var account = _fixture.Build<Account>()
                .With(x => x.AccountId, "acc-reserve")
                .With(x => x.AvailableBalance, 1000)
                .With(x => x.ReservedBalance, 100)
                .Create();

            var command = _fixture.Build<ReserveCommand>()
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
            result.Balance.Should().Be(account.AvailableBalance + account.ReservedBalance);
            result.AvailableBalance.Should().Be(account.AvailableBalance);
            result.ReservedBalance.Should().Be(account.ReservedBalance);
            result.ErrorMessage.Should().BeNull();
        }

        [Fact]
        public async Task Handle_ShouldReturnFailed_WhenExceptionIsThrown()
        {
            var account = _fixture.Build<Account>()
                .With(x => x.AccountId, "acc-err")
                .With(x => x.AvailableBalance, 1000)
                .Create();

            var command = _fixture.Build<ReserveCommand>()
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
            var commands = amounts.Select(amount => _fixture.Build<ReserveCommand>()
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
        public async Task Handle_ShouldReturnRejected_WhenAmountIsNegative()
        {
            var command = _fixture.Build<ReserveCommand>()
                .With(x => x.Amount, -100)
                .Create();

            var result = await _handler.Handle(command, CancellationToken.None);

            result.Status.Should().Be("rejected");
            result.ErrorMessage.Should().NotBeNullOrEmpty();
            result.TransactionId.Should().Contain("REJECTED");
        }
    }
}