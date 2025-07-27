using Modbuska.ModbusManager.Abstraction;

namespace Modbuska.ModbusManager.Results;

public class OperationResult : IOperationResult
{
    private OperationResult(bool success, string? errorMessage = null)
    {
        Success = success;
        ErrorMessage = errorMessage;
    }

    public bool Success { get; }
    public string? ErrorMessage { get; }

    public static OperationResult Ok()
    {
        return new OperationResult(true);
    }

    public static OperationResult Fail(string errorMessage)
    {
        return new OperationResult(false, errorMessage);
    }
}