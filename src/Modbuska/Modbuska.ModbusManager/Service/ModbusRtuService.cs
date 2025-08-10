using Modbuska.ModbusManager.Abstraction;
using Modbuska.ModbusManager.Extension;
using Modbuska.ModbusManager.Model;
using Modbuska.ModbusManager.Results;

namespace Modbuska.ModbusManager.Service;

public class ModbusRtuService(SerialConnectionService serialConnectionService)
{
    private readonly SerialConnectionService _serialConnectionService = serialConnectionService;

    public (IOperationResult result, bool[] coils) SendFc1(ModbusMessage message)
    {
        if (!_serialConnectionService.IsSerialPortOpen)
        {
            return (OperationResult.Fail("Serial port is closed"), []);
        }

        _serialConnectionService.DiscardSerialBuffers();

        // Each 8 coils are packed into 1 byte → calculate byte length needed
        var coilByteCount = (message.Register + 7) / 8;
        var response = new byte[5 + coilByteCount]; // 5 = addr + func + byteCount + CRC(2)
        var request = message.BuildMessage();

        try
        {
            _serialConnectionService.WriteData(request);
            response = _serialConnectionService.ReadExact(response.Length);
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
        if (!_serialConnectionService.IsSerialPortOpen)
        {
            return (OperationResult.Fail("Serial port is closed"), []);
        }

        _serialConnectionService.DiscardSerialBuffers();

        var response = new byte[5 + 2 * message.Register];
        var request = message.BuildMessage();

        try
        {
            _serialConnectionService.WriteData(request);
            response = _serialConnectionService.ReadExact(response.Length);
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

    public (IOperationResult result, bool[] inputs) SendFc2(ModbusMessage message)
    {
        if (!_serialConnectionService.IsSerialPortOpen)
        {
            return (OperationResult.Fail("Serial port is closed"), []);
        }

        _serialConnectionService.DiscardSerialBuffers();

        var request = message.BuildMessage();

        // Number of bytes in response = 1 (byte count) + ceil(message.Register / 8.0) + 2 (CRC)
        var byteCount = (message.Register + 7) / 8;
        var response = new byte[5 + byteCount]; // 1 addr + 1 fc + 1 count + N data + 2 CRC

        try
        {
            _serialConnectionService.WriteData(request);
            response = _serialConnectionService.ReadExact(response.Length);
        }
        catch (Exception ex)
        {
            return (OperationResult.Fail($"Error sending FC2: {ex.Message}"), []);
        }

        if (!response.CheckResponse())
        {
            return (OperationResult.Fail("Invalid CRC in response"), []);
        }

        // Bits parsing 
        var inputs = new bool[message.Register];
        for (var i = 0; i < message.Register; i++)
        {
            var byteIndex = i / 8;
            var bitIndex = i % 8;
            inputs[i] = (response[3 + byteIndex] & (1 << bitIndex)) != 0;
        }

        return (OperationResult.Ok(), inputs);
    }

    public (IOperationResult result, short[] obtainedData) SendFc4(ModbusMessage message)
    {
        if (!_serialConnectionService.IsSerialPortOpen)
        {
            return (OperationResult.Fail("Serial port is closed"), []);
        }

        _serialConnectionService.DiscardSerialBuffers();

        // Response size: 5 bytes header + 2 bytes per register
        var response = new byte[5 + 2 * message.Register];

        // For function 0x04 simply form a query as in FC3
        message.Type = 0x04;
        var request = message.BuildMessage();

        try
        {
            _serialConnectionService.WriteData(request);
            response = _serialConnectionService.ReadExact(response.Length);
        }
        catch (Exception ex)
        {
            return (OperationResult.Fail($"Error sending FC4: {ex.Message}"), []);
        }

        if (!response.CheckResponse())
        {
            return (OperationResult.Fail("Invalid CRC in FC4 response"), []);
        }

        var obtainedData = new short[message.Register];
        for (var i = 0; i < message.Register; i++)
        {
            obtainedData[i] = (short)((response[3 + 2 * i] << 8) | response[4 + 2 * i]);
        }

        return (OperationResult.Ok(), obtainedData);
    }

    public (IOperationResult result, bool coilState) SendFc5(ModbusMessage message, bool value)
    {
        if (!_serialConnectionService.IsSerialPortOpen)
        {
            return (OperationResult.Fail("Serial port is closed"), false);
        }

        _serialConnectionService.DiscardSerialBuffers();

        message.Type = 0x05;
        var coilValue = value ? (ushort)0xFF00 : (ushort)0x0000;

        var request = message.BuildMessage(coilValue);

        try
        {
            _serialConnectionService.WriteData(request);
            var response = _serialConnectionService.ReadExact(ModbusMessage.MessageSize);

            if (!response.CheckResponse())
            {
                return (OperationResult.Fail("Invalid CRC in FC5 response"), false);
            }

            // read coilState from answer
            var returnedState = response[4] == 0xFF && response[5] == 0x00;

            return (OperationResult.Ok(), returnedState);
        }
        catch (Exception ex)
        {
            return (OperationResult.Fail($"Error sending FC5: {ex.Message}"), false);
        }
    }

    public (IOperationResult result, ushort registerValue) SendFc6(ModbusMessage message, ushort value)
    {
        if (!_serialConnectionService.IsSerialPortOpen)
        {
            return (OperationResult.Fail("Serial port is closed"), 0);
        }

        _serialConnectionService.DiscardSerialBuffers();

        message.Type = 0x06;

        var request = message.BuildMessage(value);

        try
        {
            _serialConnectionService.WriteData(request);
            var response = _serialConnectionService.ReadExact(8);

            if (!response.CheckResponse())
            {
                return (OperationResult.Fail("Invalid CRC in FC6 response"), 0);
            }

            // read the value from the answer
            var returnedValue = (ushort)((response[4] << 8) | response[5]);

            return (OperationResult.Ok(), returnedValue);
        }
        catch (Exception ex)
        {
            return (OperationResult.Fail($"Error sending FC6: {ex.Message}"), 0);
        }
    }

    public IOperationResult SendFc15(ModbusMessage message, bool[] values)
    {
        if (!_serialConnectionService.IsSerialPortOpen)
        {
            return OperationResult.Fail("Serial port is closed");
        }

        _serialConnectionService.DiscardSerialBuffers();

        message.Type = 0x0F;

        var byteCount = (values.Length + 7) / 8; // upward rounding
        var buffer = new byte[7 + 1 + byteCount + 2]; // address+function+start+number+of+bytes+software+data+CRC

        buffer[0] = message.Address;
        buffer[1] = message.Type;
        buffer[2] = (byte)(message.Start >> 8);
        buffer[3] = (byte)message.Start;
        buffer[4] = (byte)(message.Register >> 8);
        buffer[5] = (byte)message.Register;
        buffer[6] = (byte)byteCount;

        // Pack LSB bits first
        for (var i = 0; i < values.Length; i++)
        {
            if (values[i])
            {
                buffer[7 + i / 8] |= (byte)(1 << (i % 8));
            }
        }

        // CRC
        var crc = buffer.ComputeCrc();
        buffer[^2] = crc[0];
        buffer[^1] = crc[1];

        try
        {
            _serialConnectionService.WriteData(buffer);
            var response = _serialConnectionService.ReadExact(8); // the answer is always 8 bytes

            if (!response.CheckResponse())
            {
                return OperationResult.Fail("Invalid CRC in FC15 response");
            }

            return OperationResult.Ok();
        }
        catch (Exception ex)
        {
            return OperationResult.Fail($"Error sending FC15: {ex.Message}");
        }
    }


    /// <summary>
    ///     Function 16 - Write Multiple Registers
    /// </summary>
    /// <param name="message"></param>
    /// <param name="values"></param>
    /// <returns></returns>
    public IOperationResult SendFc16(ModbusMessage message, short[] values)
    {
        if (!_serialConnectionService.IsSerialPortOpen)
        {
            return OperationResult.Fail("Serial port is closed");
        }

        _serialConnectionService.DiscardSerialBuffers();

        var request = message.BuildMessage(values);
        var response = new byte[ModbusMessage.MessageSize]; // FC16 response always 8 byte

        try
        {
            _serialConnectionService.WriteData(request);
            response = _serialConnectionService.ReadExact(response.Length);
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