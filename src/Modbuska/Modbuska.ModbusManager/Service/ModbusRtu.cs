using Modbuska.ModbusManager.Abstraction;
using Modbuska.ModbusManager.Extension;
using Modbuska.ModbusManager.Model;
using Modbuska.ModbusManager.Results;

namespace Modbuska.ModbusManager.Service;

public class ModbusRtu(SerialConnection serialConnection)
{
    private readonly SerialConnection _serialConnection = serialConnection;

    public (IOperationResult result, bool[] coils) SendFc1(ModbusMessage message)
    {
        if (!_serialConnection.IsSerialPortOpen)
        {
            return (OperationResult.Fail("Serial port is closed"), []);
        }

        _serialConnection.DiscardSerialBuffers();

        // Each 8 coils are packed into 1 byte → calculate byte length needed
        var coilByteCount = (message.Register + 7) / 8;
        var response = new byte[5 + coilByteCount]; // 5 = addr + func + byteCount + CRC(2)
        var request = message.BuildMessage();

        try
        {
            _serialConnection.WriteData(request);
            response = _serialConnection.ReadExact(response.Length);
        }
        catch (Exception ex)
        {
            return (OperationResult.Fail($"Error sending FC1: {ex.Message}"), []);
        }

        if (!response.CheckResponse())
        {
            return (OperationResult.Fail("Invalid CRC in response"), []);
        }

        // Byte 2 = byte count
        if (response[2] != coilByteCount)
        {
            return (OperationResult.Fail("Mismatch in expected coil data size"), []);
        }

        // Parse coil bits
        var coils = new bool[message.Register];
        for (var i = 0; i < message.Register; i++)
        {
            var byteIndex = i / 8;
            var bitIndex = i % 8;

            var isSet = (response[3 + byteIndex] & (1 << bitIndex)) != 0;
            coils[i] = isSet;
        }

        return (OperationResult.Ok(), coils);
    }


    public (IOperationResult result, short[] obtainedData) SendFc3(ModbusMessage message)
    {
        if (!_serialConnection.IsSerialPortOpen)
        {
            return (OperationResult.Fail("Serial port is closed"), []);
        }

        _serialConnection.DiscardSerialBuffers();

        var response = new byte[5 + 2 * message.Register];
        var request = message.BuildMessage();

        try
        {
            _serialConnection.WriteData(request);
            response = _serialConnection.ReadExact(response.Length);
        }
        catch (Exception ex)
        {
            return (OperationResult.Fail($"Error sending FC3: {ex.Message}"), []);
        }

        if (!response.CheckResponse())
        {
            return (OperationResult.Fail("Invalid CRC in response"), []);
        }

        var obtainedData = new short[message.Register];
        for (var i = 0; i < message.Register; i++)
        {
            obtainedData[i] = (short)((response[2 * i + 3] << 8) | response[2 * i + 4]);
        }

        return (OperationResult.Ok(), obtainedData);
    }

    /// <summary>
    ///     Function 16 - Write Multiple Registers
    /// </summary>
    /// <param name="address"></param>
    /// <param name="start"></param>
    /// <param name="registers"></param>
    /// <param name="values"></param>
    /// <returns></returns>
    public IOperationResult SendFc16(ModbusMessage message, short[] values)
    {
        if (!_serialConnection.IsSerialPortOpen)
        {
            return OperationResult.Fail("Serial port is closed");
        }

        _serialConnection.DiscardSerialBuffers();

        var request = message.BuildMessage(values);
        var response = new byte[ModbusMessage.MessageSize]; // FC16 response always 8 byte

        try
        {
            _serialConnection.WriteData(request);
            response = _serialConnection.ReadExact(response.Length);
        }
        catch (Exception ex)
        {
            return OperationResult.Fail($"Error sending FC16: {ex.Message}");
        }

        return response.CheckResponse()
            ? OperationResult.Ok()
            : OperationResult.Fail("CRC error in response");
    }
    
    
}