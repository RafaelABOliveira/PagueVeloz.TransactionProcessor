using AutoFixture;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging;
using Moq;
using PagueVeloz.Core.Application.Commands.Bulk;
using PagueVeloz.Core.Application.DTOs.Transaction;
using PagueVeloz.Core.Application.Handlers.Bulk;

namespace PagueVeloz.UnitTests.PagueVeloz.Core.Handlers.Bulk
{
    [Trait("Bulk Handler", "ProcessTransactionsBulkHandler")]
    public class ProcessTransactionsBulkHandlerTests
    {
        private readonly Fixture _fixture;
        private readonly Mock<IMediator> _mediatorMock;
        private readonly Mock<ILogger<ProcessTransactionsBulkHandler>> _loggerMock;
        private readonly ProcessTransactionsBulkHandler _handler;

        public ProcessTransactionsBulkHandlerTests()
        {
            _fixture = new Fixture();
            _fixture.Behaviors
                .OfType<ThrowingRecursionBehavior>()
                .ToList()
                .ForEach(b => _fixture.Behaviors.Remove(b));
            _fixture.Behaviors.Add(new OmitOnRecursionBehavior());

            _mediatorMock = new Mock<IMediator>();
            _loggerMock = new Mock<ILogger<ProcessTransactionsBulkHandler>>();
            _handler = new ProcessTransactionsBulkHandler(_mediatorMock.Object, _loggerMock.Object);
        }

        [Fact]
        public async Task Handle_ShouldReturnResponses_ForValidBulkCreditAndDebit()
        {
            var requests = new List<TransactionRequest>
            {
                _fixture.Build<TransactionRequest>()
                    .With(x => x.AccountId, "ACC-001")
                    .With(x => x.Amount, 1000)
                    .With(x => x.Operation, "credit")
                    .With(x => x.ReferenceId, "ref-1")
                    .Create(),
                _fixture.Build<TransactionRequest>()
                    .With(x => x.AccountId, "ACC-001")
                    .With(x => x.Amount, 500)
                    .With(x => x.Operation, "debit")
                    .With(x => x.ReferenceId, "ref-2")
                    .Create()
            };

            _mediatorMock
                .Setup(m => m.Send(It.IsAny<IRequest<TransactionResponse>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((IRequest<TransactionResponse> cmd, CancellationToken _) =>
                {
                    var op = (cmd.GetType().Name.ToLower().Contains("credit")) ? "success" : "success";
                    return new TransactionResponse
                    {
                        TransactionId = $"TXN-{((dynamic)cmd).AccountId}-PROCESSED",
                        Status = op,
                        Balance = 1000,
                        ReservedBalance = 0,
                        AvailableBalance = 1000,
                        Timestamp = DateTime.UtcNow
                    };
                });

            var command = new ProcessTransactionsBulkCommand { Requests = requests };

            var result = await _handler.Handle(command, CancellationToken.None);

            result.Should().HaveCount(2);
            result.All(r => r.Status == "success").Should().BeTrue();
            result.All(r => r.TransactionId.Contains("TXN-")).Should().BeTrue();
        }

        [Fact]
        public async Task Handle_ShouldReturnFailed_WhenOperationIsInvalid()
        {
            var requests = new List<TransactionRequest>
            {
                _fixture.Build<TransactionRequest>()
                    .With(x => x.AccountId, "ACC-001")
                    .With(x => x.Amount, 100)
                    .With(x => x.Operation, "invalid")
                    .With(x => x.ReferenceId, "ref-invalid")
                    .Create()
            };

            var command = new ProcessTransactionsBulkCommand { Requests = requests };

            var result = await _handler.Handle(command, CancellationToken.None);

            result.Should().HaveCount(1);
            result[0].Status.Should().Be("failed");
            result[0].ErrorMessage.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public async Task Handle_ShouldReturnFailed_WhenMediatorThrowsException()
        {
            var requests = new List<TransactionRequest>
            {
                _fixture.Build<TransactionRequest>()
                    .With(x => x.AccountId, "ACC-001")
                    .With(x => x.Amount, 100)
                    .With(x => x.Operation, "credit")
                    .With(x => x.ReferenceId, "ref-ex")
                    .Create()
            };

            _mediatorMock
                .Setup(m => m.Send(It.IsAny<IRequest<TransactionResponse>>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Mediator error"));

            var command = new ProcessTransactionsBulkCommand { Requests = requests };

            var result = await _handler.Handle(command, CancellationToken.None);

            result.Should().HaveCount(1);
            result[0].Status.Should().Be("failed");
            result[0].ErrorMessage.Should().Contain("Mediator error");
        }

        [Fact]
        public async Task Handle_ShouldProcessMultipleAccountsInParallel()
        {
            var requests = new List<TransactionRequest>();
            for (int i = 0; i < 5; i++)
            {
                requests.Add(_fixture.Build<TransactionRequest>()
                    .With(x => x.AccountId, $"ACC-{i + 1}")
                    .With(x => x.Amount, 100 * (i + 1))
                    .With(x => x.Operation, "credit")
                    .With(x => x.ReferenceId, $"ref-{i + 1}")
                    .Create());
            }

            _mediatorMock
                .Setup(m => m.Send(It.IsAny<IRequest<TransactionResponse>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((IRequest<TransactionResponse> cmd, CancellationToken _) =>
                {
                    return new TransactionResponse
                    {
                        TransactionId = $"TXN-{((dynamic)cmd).AccountId}-PROCESSED",
                        Status = "success",
                        Balance = 1000,
                        ReservedBalance = 0,
                        AvailableBalance = 1000,
                        Timestamp = DateTime.UtcNow
                    };
                });

            var command = new ProcessTransactionsBulkCommand { Requests = requests };

            var result = await _handler.Handle(command, CancellationToken.None);

            result.Should().HaveCount(5);
            result.All(r => r.Status == "success").Should().BeTrue();
        }

        [Fact]
        public async Task Handle_ShouldReturnOrderedResponsesByTransactionId()
        {
            var requests = new List<TransactionRequest>
            {
                _fixture.Build<TransactionRequest>()
                    .With(x => x.AccountId, "ACC-002")
                    .With(x => x.Amount, 200)
                    .With(x => x.Operation, "credit")
                    .With(x => x.ReferenceId, "ref-2")
                    .Create(),
                _fixture.Build<TransactionRequest>()
                    .With(x => x.AccountId, "ACC-001")
                    .With(x => x.Amount, 100)
                    .With(x => x.Operation, "credit")
                    .With(x => x.ReferenceId, "ref-1")
                    .Create()
            };

            _mediatorMock
                .Setup(m => m.Send(It.IsAny<IRequest<TransactionResponse>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((IRequest<TransactionResponse> cmd, CancellationToken _) =>
                {
                    var accId = ((dynamic)cmd).AccountId;
                    return new TransactionResponse
                    {
                        TransactionId = $"TXN-{accId}-PROCESSED",
                        Status = "success",
                        Balance = 1000,
                        ReservedBalance = 0,
                        AvailableBalance = 1000,
                        Timestamp = DateTime.UtcNow
                    };
                });

            var command = new ProcessTransactionsBulkCommand { Requests = requests };

            var result = await _handler.Handle(command, CancellationToken.None);

            result.Should().BeInAscendingOrder(r => r.TransactionId);
        }

        [Fact]
        public async Task Handle_ShouldReturnSuccess_ForReserveOperation()
        {
            var request = _fixture.Build<TransactionRequest>()
                .With(x => x.AccountId, "ACC-001")
                .With(x => x.Amount, 500)
                .With(x => x.Operation, "reserve")
                .With(x => x.ReferenceId, "ref-reserve")
                .Create();

            _mediatorMock
                .Setup(m => m.Send(It.IsAny<IRequest<TransactionResponse>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new TransactionResponse
                {
                    TransactionId = "TXN-1-PROCESSED",
                    Status = "success",
                    Balance = 1500,
                    ReservedBalance = 500,
                    AvailableBalance = 1000,
                    Timestamp = System.DateTime.UtcNow
                });

            var command = new ProcessTransactionsBulkCommand { Requests = new List<TransactionRequest> { request } };
            var result = await _handler.Handle(command, CancellationToken.None);

            result.Should().HaveCount(1);
            result[0].Status.Should().Be("success");
            result[0].ReservedBalance.Should().Be(500);
        }

        [Fact]
        public async Task Handle_ShouldReturnFailed_ForReserveOperation_WhenMediatorThrows()
        {
            var request = _fixture.Build<TransactionRequest>()
                .With(x => x.AccountId, "ACC-001")
                .With(x => x.Amount, 500)
                .With(x => x.Operation, "reserve")
                .With(x => x.ReferenceId, "ref-reserve-fail")
                .Create();

            _mediatorMock
                .Setup(m => m.Send(It.IsAny<IRequest<TransactionResponse>>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new System.Exception("Reserve error"));

            var command = new ProcessTransactionsBulkCommand { Requests = new List<TransactionRequest> { request } };
            var result = await _handler.Handle(command, CancellationToken.None);

            result.Should().HaveCount(1);
            result[0].Status.Should().Be("failed");
            result[0].ErrorMessage.Should().Contain("Reserve error");
        }

        [Fact]
        public async Task Handle_ShouldReturnSuccess_ForCaptureOperation()
        {
            var request = _fixture.Build<TransactionRequest>()
                .With(x => x.AccountId, "ACC-002")
                .With(x => x.Amount, 200)
                .With(x => x.Operation, "capture")
                .With(x => x.ReferenceId, "ref-capture")
                .Create();

            _mediatorMock
                .Setup(m => m.Send(It.IsAny<IRequest<TransactionResponse>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new TransactionResponse
                {
                    TransactionId = "TXN-2-PROCESSED",
                    Status = "success",
                    ReservedBalance = 300,
                    AvailableBalance = 1200,
                    Balance = 1500,
                    Timestamp = System.DateTime.UtcNow
                });

            var command = new ProcessTransactionsBulkCommand { Requests = new List<TransactionRequest> { request } };
            var result = await _handler.Handle(command, CancellationToken.None);

            result.Should().HaveCount(1);
            result[0].Status.Should().Be("success");
            result[0].TransactionId.Should().Contain("PROCESSED");
        }

        [Fact]
        public async Task Handle_ShouldReturnFailed_ForCaptureOperation_WhenMediatorThrows()
        {
            var request = _fixture.Build<TransactionRequest>()
                .With(x => x.AccountId, "ACC-002")
                .With(x => x.Amount, 200)
                .With(x => x.Operation, "capture")
                .With(x => x.ReferenceId, "ref-capture-fail")
                .Create();

            _mediatorMock
                .Setup(m => m.Send(It.IsAny<IRequest<TransactionResponse>>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new System.Exception("Capture error"));

            var command = new ProcessTransactionsBulkCommand { Requests = new List<TransactionRequest> { request } };
            var result = await _handler.Handle(command, CancellationToken.None);

            result.Should().HaveCount(1);
            result[0].Status.Should().Be("failed");
            result[0].ErrorMessage.Should().Contain("Capture error");
        }

        [Fact]
        public async Task Handle_ShouldReturnSuccess_ForReversalOperation()
        {
            var request = _fixture.Build<TransactionRequest>()
                .With(x => x.AccountId, "ACC-003")
                .With(x => x.Amount, 100)
                .With(x => x.Operation, "reversal")
                .With(x => x.ReferenceId, "ref-reversal")
                .Create();

            _mediatorMock
                .Setup(m => m.Send(It.IsAny<IRequest<TransactionResponse>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new TransactionResponse
                {
                    TransactionId = "TXN-003-PROCESSED",
                    Status = "success",
                    Balance = 1000,
                    ReservedBalance = 0,
                    AvailableBalance = 1000,
                    Timestamp = System.DateTime.UtcNow
                });

            var command = new ProcessTransactionsBulkCommand { Requests = new List<TransactionRequest> { request } };
            var result = await _handler.Handle(command, CancellationToken.None);

            result.Should().HaveCount(1);
            result[0].Status.Should().Be("success");
            result[0].TransactionId.Should().Contain("PROCESSED");
        }

        [Fact]
        public async Task Handle_ShouldReturnFailed_ForReversalOperation_WhenMediatorThrows()
        {
            var request = _fixture.Build<TransactionRequest>()
                .With(x => x.AccountId, "ACC-003")
                .With(x => x.Amount, 100)
                .With(x => x.Operation, "reversal")
                .With(x => x.ReferenceId, "ref-reversal-fail")
                .Create();

            _mediatorMock
                .Setup(m => m.Send(It.IsAny<IRequest<TransactionResponse>>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new System.Exception("Reversal error"));

            var command = new ProcessTransactionsBulkCommand { Requests = new List<TransactionRequest> { request } };
            var result = await _handler.Handle(command, CancellationToken.None);

            result.Should().HaveCount(1);
            result[0].Status.Should().Be("failed");
            result[0].ErrorMessage.Should().Contain("Reversal error");
        }

        [Fact]
        public async Task Handle_ShouldReturnSuccess_ForTransferOperation()
        {
            var request = _fixture.Build<TransactionRequest>()
                .With(x => x.Amount, 300)
                .With(x => x.Operation, "transfer")
                .With(x => x.SourceAccountId, "ACC-001")
                .With(x => x.TargetAccountId, "ACC-002")
                .With(x => x.ReferenceId, "ref-transfer")
                .Create();

            _mediatorMock
                .Setup(m => m.Send(It.IsAny<IRequest<TransactionResponse>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new TransactionResponse
                {
                    TransactionId = "TXN-001-PROCESSED|TXN-002-PROCESSED",
                    Status = "success",
                    Balance = 700,
                    ReservedBalance = 0,
                    AvailableBalance = 700,
                    Timestamp = System.DateTime.UtcNow
                });

            var command = new ProcessTransactionsBulkCommand { Requests = new List<TransactionRequest> { request } };
            var result = await _handler.Handle(command, CancellationToken.None);

            result.Should().HaveCount(1);
            result[0].Status.Should().Be("success");
            result[0].TransactionId.Should().Contain("PROCESSED");
        }

        [Fact]
        public async Task Handle_ShouldReturnFailed_ForTransferOperation_WhenMissingSourceOrTargetAccountId()
        {
            var requestMissingSource = _fixture.Build<TransactionRequest>()
                .With(x => x.Amount, 300)
                .With(x => x.Operation, "transfer")
                .With(x => x.SourceAccountId, null as string)
                .With(x => x.TargetAccountId, "ACC-002")
                .With(x => x.ReferenceId, "ref-transfer-missing-source")
                .Create();

            var commandMissingSource = new ProcessTransactionsBulkCommand { Requests = new List<TransactionRequest> { requestMissingSource } };

            var resultSource = await _handler.Handle(commandMissingSource, CancellationToken.None);
            resultSource.Should().HaveCount(1);
            resultSource[0].Status.Should().Be("failed");
            resultSource[0].ErrorMessage.Should().Contain("SourceAccountId is required");

            var requestMissingTarget = _fixture.Build<TransactionRequest>()
                .With(x => x.Amount, 300)
                .With(x => x.Operation, "transfer")
                .With(x => x.SourceAccountId, "ACC-001")
                .With(x => x.TargetAccountId, null as string)
                .With(x => x.ReferenceId, "ref-transfer-missing-target")
                .Create();

            var commandMissingTarget = new ProcessTransactionsBulkCommand { Requests = new List<TransactionRequest> { requestMissingTarget } };

            var resultTarget = await _handler.Handle(commandMissingTarget, CancellationToken.None);
            resultTarget.Should().HaveCount(1);
            resultTarget[0].Status.Should().Be("failed");
            resultTarget[0].ErrorMessage.Should().Contain("TargetAccountId is required");
        }
    }
}