using System.IO.Ports;
using Microsoft.Extensions.Logging;
using Modbuska.ModbusManager.Model;

namespace Modbuska.ModbusManager.Service;

public class SerialConnection(SerialPort? serialPort, ILogger logger, CancellationTokenSource? cts)
{
    /// <summary>
    ///     Using
    ///     await ReadAsync(data => { Console.WriteLine($"Data: {BitConverter.ToString(data)}");
    /// </summary>
    /// <param name="onData">Lambda or method puts inside</param>
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
                    var actualData = new byte[bytesRead];
                    Array.Copy(buffer, actualData, bytesRead);
                    onData?.Invoke(actualData);
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

    public bool Open(SerialPortSettings portSettings)
    {
        //Ensure port isn't already opened:
        if (serialPort is not { IsOpen: false })
        {
            return false;
        }

        //Assign desired settings to the serial port:
        serialPort.PortName = portSettings.PortName;
        serialPort.BaudRate = portSettings.BaudRate;
        serialPort.DataBits = portSettings.DataBits;
        serialPort.Parity = portSettings.Parity;
        serialPort.StopBits = portSettings.StopBits;
        //These timeouts are default and cannot be edited through the class at this point:
        serialPort.ReadTimeout = 100; // todo move to settings
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