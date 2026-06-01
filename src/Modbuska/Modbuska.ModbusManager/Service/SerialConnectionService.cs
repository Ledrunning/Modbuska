using System.IO.Ports;
using Microsoft.Extensions.Logging;
using Modbuska.ModbusManager.Model;

namespace Modbuska.ModbusManager.Service;

public class SerialConnectionService(SerialPort serialPort, ILogger<SerialConnectionService> logger)
{
    private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly SerialPort _serialPort = serialPort ?? throw new ArgumentNullException(nameof(serialPort));
    private CancellationTokenSource? _cts;
    public bool IsSerialPortOpen => _serialPort.IsOpen;

    public event Action<byte[]>? DataReceived;

    public void DiscardSerialBuffers()
    {
        _serialPort.DiscardOutBuffer();
        _serialPort.DiscardInBuffer();
        _logger.LogInformation("Discard In / Out serial buffers.");
    }

    /// <summary>
    ///     Starts asynchronous reading from the serial port.
    /// </summary>
    public void StartReading(int bufferSize = 64)
    {
        if (_cts != null)
        {
            _logger.LogWarning("Reading already started.");
            return;
        }

        if (!_serialPort.IsOpen)
        {
            _logger.LogError("Cannot start reading. Serial port is not open.");
            return;
        }

        _cts = new CancellationTokenSource();
        Task.Run(() => ReadLoopAsync(bufferSize, _cts.Token));
    }

    /// <summary>
    ///     Stops asynchronous reading from the serial port.
    /// </summary>
    public void StopReading()
    {
        if (_cts == null)
        {
            return;
        }

        _cts.Cancel();
        _cts.Dispose();
        _cts = null;
        _logger.LogInformation("Stopped reading from serial port.");
    }

    /// <summary>
    ///     The background loop that reads from the serial port.
    /// </summary>
    private async Task ReadLoopAsync(int bufferSize, CancellationToken token)
    {
        var buffer = new byte[bufferSize];

        try
        {
            while (!token.IsCancellationRequested)
            {
                if (!_serialPort.IsOpen)
                {
                    _logger.LogWarning("Serial port was closed during reading. Exiting read loop.");
                    break;
                }

                var bytesRead = await _serialPort.BaseStream.ReadAsync(buffer, token);
                if (bytesRead > 0)
                {
                    var actualData = buffer[..bytesRead];
                    DataReceived?.Invoke(actualData);
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Serial read operation was cancelled.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while reading from the serial port.");
        }
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
    public bool Open(SerialPortSettings portSettings)
    {
        if (_serialPort.IsOpen)
        {
            return false;
        }

        _serialPort.PortName = portSettings.PortName;
        _serialPort.BaudRate = portSettings.BaudRate;
        _serialPort.DataBits = portSettings.DataBits;
        _serialPort.Parity = portSettings.Parity;
        _serialPort.StopBits = portSettings.StopBits;
        _serialPort.ReadTimeout = 500; // todo: move to settings
        _serialPort.WriteTimeout = 100;

        try
        {
            _serialPort.Open();
            _logger.LogInformation("Serial port opened.");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open serial port.");
            return false;
        }
    }

    /// <summary>
    ///     Closes the serial port if open.
    /// </summary>
    public bool Close()
    {
        if (!_serialPort.IsOpen)
        {
            return false;
        }

        try
        {
            _serialPort.Close();
            _logger.LogInformation("Serial port closed.");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to close serial port.");
            return false;
        }
    }
}