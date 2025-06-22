using System.IO.Ports;
using Microsoft.Extensions.Logging;

namespace Modbuska.ModbusManager;

public class SerialConnection(SerialPort? serialPort, ILogger logger, CancellationTokenSource? cts)
{
    /// <summary>
    ///     Using
    ///     await ReadAsync(data => { Console.WriteLine($"Data: {BitConverter.ToString(data)}");
    /// </summary>
    /// <param name="onData">Lamda or method puts inside</param>
    /// <param name="bufferSize">receiving buffer size</param>
    public async Task ReadAsync(Action<byte[]> onData, int bufferSize = 64)
    {
        var buffer = new byte[bufferSize];

        try
        {
            while (cts is { Token.IsCancellationRequested: false })
            {
                var bytesRead = await (serialPort?.BaseStream
                                       ?? throw new InvalidOperationException("Serial port is not available"))
                    .ReadAsync(buffer, cts.Token);
                if (bytesRead > 0)
                {
                    var actual = new byte[bytesRead];
                    Array.Copy(buffer, actual, bytesRead);
                    onData?.Invoke(actual);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // To do logger
            logger?.LogError("Read operation was cancelled.");
        }
        finally
        {
            serialPort?.Close();
        }
    }

    /// <summary>
    ///     Stop reading bytes from serial
    /// </summary>
    public void StopReading()
    {
        cts?.Cancel();
    }

    public bool Open(string portName, int baudRate, int databits, Parity parity, StopBits stopBits)
    {
        //Ensure port isn't already opened:
        if (serialPort is not { IsOpen: false })
        {
            return false;
        }

        //Assign desired settings to the serial port:
        serialPort.PortName = portName;
        serialPort.BaudRate = baudRate;
        serialPort.DataBits = databits;
        serialPort.Parity = parity;
        serialPort.StopBits = stopBits;
        //These timeouts are default and cannot be editted through the class at this point:
        serialPort.ReadTimeout = 100;
        serialPort.WriteTimeout = 100;

        try
        {
            serialPort.Open();
        }
        catch (Exception err)
        {
            //todo log
            return false;
        }

        //todo log
        return true;
    }

    public bool Close()
    {
        //Ensure port is opened before attempting to close:
        if (serialPort is not { IsOpen: true })
        {
            return false;
        }

        try
        {
            serialPort.Close();
        }
        catch (Exception err)
        {
            // todo log
            return false;
        }

        return true;
    }
}