namespace Modbuska.ModbusManager.Abstraction;

public interface IOperationResult
{
    bool Success { get; }
    string? ErrorMessage { get; }
}