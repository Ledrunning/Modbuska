using Modbuska.ModbusManager.Model;

namespace Modbuska.ModbusManager.Abstraction;

/// <summary>
///     Abstraction over a serial connection used by the Modbus RTU service.
/// </summary>
public interface ISerialConnectionService
{
    /// <summary>
    ///     Gets a value indicating whether the serial port is currently open.
    /// </summary>
    bool IsSerialPortOpen { get; }

    /// <summary>
    ///     Discards all data in the serial port's input and output buffers.
    /// </summary>
    void DiscardSerialBuffers();

    /// <summary>
    ///     Reads exactly the specified number of bytes from the serial port.
    /// </summary>
    /// <param name="length">The number of bytes to read.</param>
    /// <returns>A byte array containing the read data.</returns>
    byte[] ReadExact(int length);

    /// <summary>
    ///     Writes the specified data to the serial port.
    /// </summary>
    /// <param name="data">The data to write.</param>
    void WriteData(byte[] data);

    /// <summary>
    ///     Opens the serial port using the specified settings.
    /// </summary>
    /// <param name="portSettings">The serial port settings to apply.</param>
    /// <returns>An <see cref="IOperationResult" /> indicating success or failure.</returns>
    IOperationResult Open(SerialPortSettings portSettings);

    /// <summary>
    ///     Closes the serial port if it is open.
    /// </summary>
    /// <returns>An <see cref="IOperationResult" /> indicating success or failure.</returns>
    IOperationResult Close();
}