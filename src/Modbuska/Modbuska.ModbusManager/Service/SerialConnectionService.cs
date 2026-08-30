using System.IO.Ports;
using Microsoft.Extensions.Logging;
using Modbuska.ModbusManager.Abstraction;
using Modbuska.ModbusManager.Model;
using Modbuska.ModbusManager.Results;

namespace Modbuska.ModbusManager.Service;

public class SerialConnectionService(SerialPort serialPort, ILogger<SerialConnectionService> logger) : ISerialConnectionService
{
    private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly SerialPort _serialPort = serialPort ?? throw new ArgumentNullException(nameof(serialPort));

    public bool IsSerialPortOpen => _serialPort.IsOpen;

    public void DiscardSerialBuffers()
    {
        _serialPort.DiscardOutBuffer();
        _serialPort.DiscardInBuffer();
        _logger.LogInformation("Discard In / Out serial buffers.");
    }

    public byte[] ReadExact(int length)
    {
        var buffer = new byte[length];
        var offset = 0;

        while (offset < length)
        {
            var read = _serialPort.Read(buffer, offset, length - offset);
            if (read == 0)
            {
                throw new IOException("Serial port returned 0 bytes. Connection may be closed.");
            }

            offset += read;
        }

        return buffer;
    }

    public void WriteData(byte[] data)
    {
        _serialPort.Write(data, 0, data.Length);
    }

    /// <summary>
    ///     Opens the serial port using specified settings.
    /// </summary>
    public IOperationResult Open(SerialPortSettings portSettings)
    {
        if (_serialPort.IsOpen)
        {
            return OperationResult.Fail("Serial port is already open");
        }

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
            _logger.LogInformation("Serial port opened.");
            return OperationResult.Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open serial port.");
            return OperationResult.Fail($"Failed to open serial port: {ex.Message}");
        }
    }

    /// <summary>
    ///     Closes the serial port if open.
    /// </summary>
    public IOperationResult Close()
    {
        if (!_serialPort.IsOpen)
        {
            return OperationResult.Fail("Serial port is not open");
        }

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
    }
}
