using Microsoft.Extensions.Logging;
using PagueVeloz.Core.Application.DTOs.Transaction;

public static class TransactionValidationHelper
{

    public static TransactionResponse ValidateMinimumAmount(string accountId, long amount, ILogger logger, string operationType)
    {
        if (amount < 1)
        {
            string errorMessage = $"O valor de {operationType} deve ser igual ou superior a 1 centavo (Amount >= 1).";
            logger.LogWarning("{Operation} transaction rejected: Amount invalid for AccountId {AccountId}. Amount: {Amount}", operationType, accountId, amount);

            return new TransactionResponse
            {
                TransactionId = $"TXN-{accountId}-REJECTED",
                Status = "rejected",
                ErrorMessage = errorMessage,
                Timestamp = DateTime.UtcNow
            };
        }
        return null;
    }
}