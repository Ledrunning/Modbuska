namespace Modbuska.ModbusManager.Abstraction;

/// <summary>
///     Represents the result of an operation, indicating success or failure with an optional error message.
/// </summary>
public interface IOperationResult
{
    /// <summary>
    ///     Gets a value indicating whether the operation succeeded.
    /// </summary>
    bool Success { get; }

    /// <summary>
    ///     Gets the error message if the operation failed, or <c>null</c> if it succeeded.
    /// </summary>
    string? ErrorMessage { get; }
}