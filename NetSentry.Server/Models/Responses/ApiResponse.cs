namespace NetSentry.Server.Models.Responses
{
    public class ApiResponse<T>
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public T? Data { get; set; }

        public static ApiResponse<T> Ok(T data, string message = "Success")
        {
            return new ApiResponse<T> { Success = true, Message = message, Data = data };
        }

        public static ApiResponse<T> Error(string message)
        {
            return new ApiResponse<T> { Success = false, Message = message };
        }
    }
}