namespace PagueVeloz.Core.Application.DTOs
{
    public class Response<T>
    {
        public bool Success { get; set; } = true;
        public string ErrorMessage { get; set; }
        public T Data { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        public static Response<T> Ok(T data) => new Response<T> { Data = data, Success = true };
        public static Response<T> Fail(string errorMessage) => new Response<T> { Success = false, ErrorMessage = errorMessage };
    }
}
