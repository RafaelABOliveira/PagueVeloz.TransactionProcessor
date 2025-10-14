namespace PagueVeloz.Core.Domain.Enums
{
    public enum TransactionType : byte
    {
        Credit = 0,
        Debit = 1,
        Reserve = 2,
        Capture = 3,
        Reversal = 4,
        Transfer = 5
    }
}
