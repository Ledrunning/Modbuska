using System.IO.Ports;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Modbuska.ModbusManager.Abstraction;
using Modbuska.ModbusManager.Model;
using Modbuska.ModbusManager.Results;

namespace Modbuska.ModbusManager.Service;

/// <summary>
///     Provides a concrete implementation of <see cref="ISerialConnectionService" /> for communicating over a serial port.
/// </summary>
public sealed class SerialConnectionService(SerialPort serialPort, ILogger<SerialConnectionService>? logger = null)
    : ISerialConnectionService, IDisposable
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly ILogger _logger = logger ?? NullLogger<SerialConnectionService>.Instance;
    private readonly SerialPort _serialPort = serialPort ?? throw new ArgumentNullException(nameof(serialPort));
    private bool _disposed;

    /// <summary>
    /// Dispose monitor
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _lock.Dispose();
        _disposed = true;

        GC.SuppressFinalize(this);
    }

    /// <summary>
    ///     Gets a value indicating whether the serial port is currently open.
    /// </summary>
    public bool IsSerialPortOpen => _serialPort.IsOpen;

    /// <summary>
    ///     Discards all data in the serial port's input and output buffers.
    /// </summary>
    public void DiscardSerialBuffers()
    {
        ExecuteSynchronized(() =>
        {
            _serialPort.DiscardOutBuffer();
            _serialPort.DiscardInBuffer();
            _logger.LogTrace("Discard In / Out serial buffers.");
        });
    }

    /// <summary>
    ///     Reads exactly the specified number of bytes from the serial port.
    /// </summary>
    /// <param name="length">The number of bytes to read.</param>
    /// <returns>A byte array containing the read data.</returns>
    /// <exception cref="IOException">Thrown when the serial port returns 0 bytes, indicating the connection may be closed.</exception>
    public byte[] ReadExact(int length)
    {
        return ExecuteSynchronized(() =>
        {
            var buffer = new byte[length];
            var offset = 0;

            while (offset < length)
            {
                var read = _serialPort.Read(buffer, offset, length - offset);
                if (read == 0) throw new IOException("Serial port returned 0 bytes. Connection may be closed.");

                offset += read;
            }

            return buffer;
        });
    }

    /// <summary>
    ///     Writes the specified data to the serial port.
    /// </summary>
    /// <param name="data">The data to write.</param>
    public void WriteData(byte[] data)
    {
        ExecuteSynchronized(() => { _serialPort.Write(data, 0, data.Length); });
    }

    /// <summary>
    ///     Opens the serial port using specified settings.
    /// </summary>
    public IOperationResult Open(SerialPortSettings portSettings)
    {
        return ExecuteSynchronized(() =>
        {
            if (_serialPort.IsOpen) return OperationResult.Fail("Serial port is already open");

            _serialPort.PortName = portSettings.PortName;
            _serialPort.BaudRate = portSettings.BaudRate;
            _serialPort.DataBits = portSettings.DataBits;
            _serialPort.Parity = portSettings.Parity;
            _serialPort.StopBits = portSettings.StopBits;
            _serialPort.ReadTimeout = portSettings.ReadTimeout;
            _serialPort.WriteTimeout = portSettings.WriteTimeout;

            try
            {
                _serialPort.Open();
                _logger.LogInformation(
                    "Serial port opened. PortName={PortName}, BaudRate={BaudRate}, DataBits={DataBits}, Parity={Parity}, StopBits={StopBits}",
                    portSettings.PortName,
                    portSettings.BaudRate,
                    portSettings.DataBits,
                    portSettings.Parity,
                    portSettings.StopBits);
                return OperationResult.Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to open serial port.");
                return OperationResult.Fail($"Failed to open serial port: {ex.Message}");
            }
        });
    }

    /// <summary>
    ///     Closes the serial port if open.
    /// </summary>
    public IOperationResult Close()
    {
        return ExecuteSynchronized(() =>
        {
            if (!_serialPort.IsOpen) return OperationResult.Fail("Serial port is not open");

            try
            {
                _serialPort.Close();
                _logger.LogInformation("Serial port closed.");
                return OperationResult.Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to close serial port.");
                return OperationResult.Fail($"Failed to close serial port: {ex.Message}");
            }
        });
    }

    private void ExecuteSynchronized(Action action)
    {
        _lock.Wait();
        try
        {
            action();
        }
        finally
        {
            _lock.Release();
        }
    }

    private T ExecuteSynchronized<T>(Func<T> func)
    {
        _lock.Wait();
        try
        {
            return func();
        }
        finally
        {
            _lock.Release();
        }
    }
}