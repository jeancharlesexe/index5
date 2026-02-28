namespace Index5.Application.DTOs;

public class ApiResponse<T>
{
    public int Status { get; set; }
    public string Message { get; set; } = string.Empty;
    public T? Data { get; set; }
    public string Timestamp { get; set; } = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss");

    public static ApiResponse<T> Success(T data, string message = "Success", int status = 200)
    {
        return new ApiResponse<T> { Status = status, Message = message, Data = data };
    }

    public static ApiResponse<T> Created(T data, string message = "Created")
    {
        return new ApiResponse<T> { Status = 201, Message = message, Data = data };
    }

    public static ApiResponse<object> Error(string message, string code, int status = 400)
    {
        return new ApiResponse<object>
        {
            Status = status,
            Message = message,
            Data = new { code = code }
        };
    }
}
