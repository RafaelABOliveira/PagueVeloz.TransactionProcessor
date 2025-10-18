using MediatR;
using Microsoft.AspNetCore.Mvc;
using PagueVeloz.Core.Application.Commands.Bulk;
using PagueVeloz.Core.Application.DTOs.Transaction;

namespace PagueVeloz.WebApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TransactionsController : ControllerBase
    {
        private readonly IMediator _mediator;

        public TransactionsController(IMediator mediator)
        {
            _mediator = mediator;
        }

        // <summary>
        // Processa várias transções financeiras (crédito, débito, reserva, etc.).
        // maxDegreeOfParallelism ajustável conforme o DB no paralelismo feito
        // Para operações pesadas, considere usar filas (RabbitMQ, Azure etc.) para evitar sobrecarga no banco de dados.
        // </summary>
        [HttpPost]
        [ProducesResponseType(typeof(List<TransactionResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(List<TransactionResponse>), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> ProcessTransactionsParallel([FromBody] List<TransactionRequest> requests)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var bulkCommand = new ProcessTransactionsBulkCommand { Requests = requests };

            var responses = await _mediator.Send(bulkCommand);

            return Ok(responses);
        }
    }
}
