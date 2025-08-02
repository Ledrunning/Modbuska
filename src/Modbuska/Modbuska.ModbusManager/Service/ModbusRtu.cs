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
        var request = BuildMessage(message);

        try
        {
            _serialConnection.WriteData(request);
            response = _serialConnection.ReadExact(response.Length);
        }
        catch (Exception ex)
        {
            return (OperationResult.Fail($"Error sending FC1: {ex.Message}"), []);
        }

        if (!CheckResponse(response))
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

    /// <summary>
    ///     Function 16 - Write Multiple Registers
    /// </summary>
    /// <param name="address"></param>
    /// <param name="start"></param>
    /// <param name="registers"></param>
    /// <param name="values"></param>
    /// <returns></returns>
    public bool SendFc16(ModbusMessage message)
    {
        //Ensure port is open:
        if (_serialConnection.IsSerialPortOpen)
        {
            //Clear in/out buffers:
            _serialConnection.DiscardSerialBuffers();

            //Message is 1 addr + 1 fcn + 2 start + 2 reg + 1 count + 2 * reg vals + 2 CRC
            var buffer = new byte[9 + 2 * message.Register];

            //Function 16 response is fixed at 8 bytes
            var response = new byte[ModbusMessage.MessageSize];

            //Add bytecount to message:
            message[6] = (byte)(registers * 2);
            //Put write values into message prior to sending:
            for (var i = 0; i < registers; i++)
            {
                message[7 + 2 * i] = (byte)(values[i] >> 8);
                message[8 + 2 * i] = (byte)values[i];
            }

            //Build outgoing message:
            BuildMessage(address, (byte)16, start, registers, ref message);

            //Send Modbus message to Serial Port:
            try
            {
                _serialConnection.WriteData(message);
                CheckResponse(ref response);
            }
            catch (Exception err)
            {
                return false;
            }

            //Evaluate message:
            if (CheckResponse(response))
            {
                return true;
            }

            return false;
        }

        return false;
    }
}