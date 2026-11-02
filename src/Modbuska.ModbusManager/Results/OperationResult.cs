using Modbuska.ModbusManager.Abstraction;

namespace Modbuska.ModbusManager.Results;

/// <summary>
///     Represents the concrete implementation of <see cref="IOperationResult" />, indicating success or failure of an
///     operation.
/// </summary>
public class OperationResult : IOperationResult
{
    private OperationResult(bool success, string? errorMessage = null)
    {
        Success = success;
        ErrorMessage = errorMessage;
    }

    /// <summary>
    ///     Gets a value indicating whether the operation succeeded.
    /// </summary>
    public bool Success { get; }

    /// <summary>
    ///     Gets the error message if the operation failed, or <c>null</c> if it succeeded.
    /// </summary>
    public string? ErrorMessage { get; }

    /// <summary>
    ///     Creates a successful operation result.
    /// </summary>
    /// <returns>An <see cref="OperationResult" /> indicating success.</returns>
    public static OperationResult Ok()
    {
        return new OperationResult(true);
    }

    /// <summary>
    ///     Creates a failed operation result with the specified error message.
    /// </summary>
    /// <param name="errorMessage">The error message describing the failure.</param>
    /// <returns>An <see cref="OperationResult" /> indicating failure.</returns>
    public static OperationResult Fail(string errorMessage)
    {
        return new OperationResult(false, errorMessage);
    }
}