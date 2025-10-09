namespace Cars.Models
{
    public class ApiResponse<T>
    {
        public bool Success { get; set; }
        public string Message { get; set; } = "";
        public T? Data { get; set; }

        public static ApiResponse<T> Ok(T data, string message = "成功")
            => new ApiResponse<T> { Success = true, Message = message, Data = data };

        public static ApiResponse<T> Fail(string message)
            => new ApiResponse<T> { Success = false, Message = message, Data = default };
    }

    public static class ApiResponse
    {
        public static ApiResponse<T> Ok<T>(T data, string message = "成功")
            => ApiResponse<T>.Ok(data, message);

        public static ApiResponse<T> Fail<T>(string message)
            => ApiResponse<T>.Fail(message);
    }
}
