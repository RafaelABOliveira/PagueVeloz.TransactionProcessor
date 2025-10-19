using AutoFixture;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using PagueVeloz.Core.Application.Commands.Transactions;
using PagueVeloz.Core.Application.DTOs;
using PagueVeloz.Core.Application.Handlers.Transactions;
using PagueVeloz.Core.Domain.Entities;
using PagueVeloz.Core.Domain.Interfaces;

namespace PagueVeloz.UnitTests.TransactionsHandler
{
    [Trait("Transaction Handler", "Transfer")]
    public class TransferCommandHandlerTests
    {
        private readonly Fixture _fixture;
        private readonly Mock<IAccountRepository> _accountRepositoryMock;
        private readonly Mock<ITransactionRepository> _transactionRepositoryMock;
        private readonly Mock<ILogger<TransferCommandHandler>> _loggerMock;
        private readonly TransferCommandHandler _handler;

        public TransferCommandHandlerTests()
        {
            _fixture = new Fixture();
            _fixture.Behaviors
                .OfType<ThrowingRecursionBehavior>()
                .ToList()
                .ForEach(b => _fixture.Behaviors.Remove(b));
            _fixture.Behaviors.Add(new OmitOnRecursionBehavior());

            _accountRepositoryMock = new Mock<IAccountRepository>();
            _transactionRepositoryMock = new Mock<ITransactionRepository>();
            _loggerMock = new Mock<ILogger<TransferCommandHandler>>();
            _handler = new TransferCommandHandler(
                _accountRepositoryMock.Object,
                _transactionRepositoryMock.Object,
                _loggerMock.Object
            );
        }

        [Fact]
        public async Task Handle_ShouldReturnRejected_WhenAmountIsZero()
        {
            var command = _fixture.Build<TransferCommand>()
                .With(x => x.Amount, 0)
                .Create();

            var result = await _handler.Handle(command, CancellationToken.None);

            result.Status.Should().Be("rejected");
            result.ErrorMessage.Should().NotBeNullOrEmpty();
            result.TransactionId.Should().Contain("REJECTED");
        }

        [Fact]
        public async Task Handle_ShouldReturnFailed_WhenSourceAccountNotFound()
        {
            var command = _fixture.Build<TransferCommand>()
                .With(x => x.Amount, 100)
                .Create();

            _accountRepositoryMock
                .Setup(x => x.GetByIdAsync(command.SourceAccountId))
                .ReturnsAsync(Response<Account>.Fail("Conta de origem não encontrada"));

            var result = await _handler.Handle(command, CancellationToken.None);

            result.Status.Should().Be("failed");
            result.ErrorMessage.Should().Contain("Conta de origem não encontrada");
            result.TransactionId.Should().Contain("FAILED");
            result.Balance.Should().Be(0);
            result.ReservedBalance.Should().Be(0);
            result.AvailableBalance.Should().Be(0);
        }

        [Fact]
        public async Task Handle_ShouldReturnFailed_WhenTargetAccountNotFound()
        {
            var sourceAccount = _fixture.Build<Account>()
                .With(x => x.AccountId, "acc-source")
                .With(x => x.AvailableBalance, 1000)
                .With(x => x.ReservedBalance, 0)
                .Create();

            var command = _fixture.Build<TransferCommand>()
                .With(x => x.SourceAccountId, sourceAccount.AccountId)
                .With(x => x.TargetAccountId, "acc-target")
                .With(x => x.Amount, 100)
                .Create();

            _accountRepositoryMock
                .Setup(x => x.GetByIdAsync(command.SourceAccountId))
                .ReturnsAsync(Response<Account>.Ok(sourceAccount));

            _accountRepositoryMock
                .Setup(x => x.GetByIdAsync(command.TargetAccountId))
                .ReturnsAsync(Response<Account>.Fail("Conta de destino não encontrada"));

            var result = await _handler.Handle(command, CancellationToken.None);

            result.Status.Should().Be("failed");
            result.ErrorMessage.Should().Contain("Conta de destino não encontrada");
            result.TransactionId.Should().Contain("FAILED");
            result.Balance.Should().Be(0);
            result.ReservedBalance.Should().Be(0);
            result.AvailableBalance.Should().Be(0);
        }

        [Fact]
        public async Task Handle_ShouldReturnFailed_WhenInsufficientBalance()
        {
            var sourceAccount = _fixture.Build<Account>()
                .With(x => x.AccountId, "acc-source")
                .With(x => x.AvailableBalance, 50)
                .With(x => x.ReservedBalance, 0)
                .Create();

            var targetAccount = _fixture.Build<Account>()
                .With(x => x.AccountId, "acc-target")
                .With(x => x.AvailableBalance, 1000)
                .With(x => x.ReservedBalance, 0)
                .Create();

            var command = _fixture.Build<TransferCommand>()
                .With(x => x.SourceAccountId, sourceAccount.AccountId)
                .With(x => x.TargetAccountId, targetAccount.AccountId)
                .With(x => x.Amount, 100)
                .Create();

            _accountRepositoryMock
                .Setup(x => x.GetByIdAsync(command.SourceAccountId))
                .ReturnsAsync(Response<Account>.Ok(sourceAccount));

            _accountRepositoryMock
                .Setup(x => x.GetByIdAsync(command.TargetAccountId))
                .ReturnsAsync(Response<Account>.Ok(targetAccount));

            var result = await _handler.Handle(command, CancellationToken.None);

            result.Status.Should().Be("failed");
            result.ErrorMessage.Should().Contain("Saldo insuficiente");
            result.TransactionId.Should().Contain("FAILED");
            result.AvailableBalance.Should().Be(sourceAccount.AvailableBalance);
            result.ReservedBalance.Should().Be(sourceAccount.ReservedBalance);
            result.Balance.Should().Be(sourceAccount.AvailableBalance + sourceAccount.ReservedBalance);
        }

        [Fact]
        public async Task Handle_ShouldReturnSuccess_WhenTransferIsProcessed()
        {
            var sourceAccount = _fixture.Build<Account>()
                .With(x => x.AccountId, "acc-source")
                .With(x => x.AvailableBalance, 1000)
                .With(x => x.ReservedBalance, 0)
                .Create();

            var targetAccount = _fixture.Build<Account>()
                .With(x => x.AccountId, "acc-target")
                .With(x => x.AvailableBalance, 500)
                .With(x => x.ReservedBalance, 0)
                .Create();

            var command = _fixture.Build<TransferCommand>()
                .With(x => x.SourceAccountId, sourceAccount.AccountId)
                .With(x => x.TargetAccountId, targetAccount.AccountId)
                .With(x => x.Amount, 200)
                .With(x => x.Description, "Pagamento de serviço")
                .With(x => x.ReferenceId, Guid.NewGuid().ToString())
                .Create();

            _accountRepositoryMock
                .Setup(x => x.GetByIdAsync(command.SourceAccountId))
                .ReturnsAsync(Response<Account>.Ok(sourceAccount));

            _accountRepositoryMock
                .Setup(x => x.GetByIdAsync(command.TargetAccountId))
                .ReturnsAsync(Response<Account>.Ok(targetAccount));

            _accountRepositoryMock
                .Setup(x => x.UpdateAsync(It.IsAny<Account>()))
                .Returns(Task.CompletedTask);

            _transactionRepositoryMock
                .Setup(x => x.AddAsyncTransactionRegistry(It.IsAny<Transaction>()))
                .ReturnsAsync("testTransaction");

            var result = await _handler.Handle(command, CancellationToken.None);

            result.Status.Should().Be("success");
            result.TransactionId.Should().Contain("testTransaction");
            result.Balance.Should().Be(sourceAccount.AvailableBalance + sourceAccount.ReservedBalance);
            result.AvailableBalance.Should().Be(sourceAccount.AvailableBalance);
            result.ReservedBalance.Should().Be(sourceAccount.ReservedBalance);
            result.ErrorMessage.Should().BeNull();
        }

        [Fact]
        public async Task Handle_ShouldReturnFailed_WhenExceptionIsThrown()
        {
            var sourceAccount = _fixture.Build<Account>()
                .With(x => x.AccountId, "acc-source")
                .With(x => x.AvailableBalance, 1000)
                .With(x => x.ReservedBalance, 0)
                .Create();

            var targetAccount = _fixture.Build<Account>()
                .With(x => x.AccountId, "acc-target")
                .With(x => x.AvailableBalance, 500)
                .With(x => x.ReservedBalance, 0)
                .Create();

            var command = _fixture.Build<TransferCommand>()
                .With(x => x.SourceAccountId, sourceAccount.AccountId)
                .With(x => x.TargetAccountId, targetAccount.AccountId)
                .With(x => x.Amount, 200)
                .Create();

            _accountRepositoryMock
                .Setup(x => x.GetByIdAsync(command.SourceAccountId))
                .ThrowsAsync(new System.Exception("Database error"));

            var result = await _handler.Handle(command, CancellationToken.None);

            result.Status.Should().Be("failed");
            result.ErrorMessage.Should().Be("Database error");
            result.TransactionId.Should().Contain("FAILED");
            result.Balance.Should().Be(0);
            result.ReservedBalance.Should().Be(0);
            result.AvailableBalance.Should().Be(0);
        }
    }
}