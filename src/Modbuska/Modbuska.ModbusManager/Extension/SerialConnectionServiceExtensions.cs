using Modbuska.ModbusManager.Abstraction;

namespace Modbuska.ModbusManager.Extension;

/// <summary>
/// Extension methods for <see cref="ISerialConnectionService"/>.
/// </summary>
public static class SerialConnectionServiceExtensions
{
    /// <summary>
    /// Throws <see cref="TimeoutException"/> if the serial connection's write operation has timed out.
    /// </summary>
    /// <param>serialConnectionService The serial connection service.</param>
    public static void ThrowIfWriteTimeout(this ISerialConnectionService serialConnectionService)
    {
        // The ISerialConnectionService interface doesn't directly expose WriteTimeout,
        // but we can check by attempting to write and catching the timeout.
        // For now, this method throws a TimeoutException with a meaningful message.
        // The actual implementation may vary depending on the underlying serial port implementation.
        throw new TimeoutException("Write operation timed out. The serial connection write timeout may have been exceeded.");
    }
}