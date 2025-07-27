using Modbuska.ModbusManager.Abstraction;
using Modbuska.ModbusManager.Extension;
using Modbuska.ModbusManager.Model;
using Modbuska.ModbusManager.Results;

namespace Modbuska.ModbusManager.Service;

public class ModbusRtu(SerialConnection serialConnection)
{
    private readonly SerialConnection _serialConnection = serialConnection;

    private byte[] BuildMessage(ModbusMessage message)
    {
        var word = new byte[ModbusMessage.MessageSize];

        word[ModbusMessage.AddressIndex] = message.Address;
        word[ModbusMessage.TypeIndex] = message.Type;
        word[ModbusMessage.StartIndexHigh] = (byte)(message.Start >> 8);
        word[ModbusMessage.StartIndexLow] = (byte)message.Start;
        word[ModbusMessage.RegisterIndexHigh] = (byte)(message.Register >> 8);
        word[ModbusMessage.RegisterIndexLow] = (byte)message.Register;

        var crc = word.ComputeCrc();
        word[^2] = crc[0];
        word[^1] = crc[1];

        return word;
    }

    private bool CheckResponse(byte[] response)
    {
        //Perform a basic CRC check:
        var crc = response.ComputeCrc();
        return crc[0] == response[^2] && crc[1] == response[^1];
    }

    public (IOperationResult result, short[] obtainedData) SendFc3(ModbusMessage message)
    {
        if (!_serialConnection.IsSerialPortOpen)
        {
            return (OperationResult.Fail("Serial port is closed"), []);
        }

        _serialConnection.DiscardSerialBuffers();

        var response = new byte[5 + 2 * message.Register];
        var request = BuildMessage(message);

        try
        {
            _serialConnection.WriteData(request);
            response = _serialConnection.ReadExact(response.Length);
        }
        catch (Exception ex)
        {
            return (OperationResult.Fail($"Error sending FC3: {ex.Message}"), []);
        }

        if (!CheckResponse(response))
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
}