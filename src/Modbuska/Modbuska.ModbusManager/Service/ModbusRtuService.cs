using Modbuska.ModbusManager.Abstraction;
using Modbuska.ModbusManager.Extension;
using Modbuska.ModbusManager.Model;
using Modbuska.ModbusManager.Results;

namespace Modbuska.ModbusManager.Service;

public class ModbusRtuService(SerialConnectionService serialConnectionService)
{
    private readonly SerialConnectionService _serialConnectionService = serialConnectionService;

    private static IOperationResult ValidateResponse(byte[] response, ModbusMessage message, byte functionCode)
    {
        if (!response.CheckResponse())
        {
            return OperationResult.Fail("Invalid CRC in response");
        }

        if (response[ModbusMessage.AddressIndex] != message.Address)
        {
            return OperationResult.Fail("Unexpected slave address in response");
        }

        var responseFunction = response[ModbusMessage.TypeIndex];
        if (responseFunction == (functionCode | 0x80))
        {
            return OperationResult.Fail($"Modbus exception response. Code: 0x{response[2]:X2}");
        }

        if (responseFunction != functionCode)
        {
            return OperationResult.Fail("Unexpected function code in response");
        }

        return OperationResult.Ok();
    }

    private byte[] ReadReadFunctionResponse()
    {
        var header = _serialConnectionService.ReadExact(3);

        if ((header[ModbusMessage.TypeIndex] & 0x80) != 0)
        {
            var exceptionTail = _serialConnectionService.ReadExact(2);
            return [header[0], header[1], header[2], exceptionTail[0], exceptionTail[1]];
        }

        var dataAndCrc = _serialConnectionService.ReadExact(header[2] + 2);
        var response = new byte[header.Length + dataAndCrc.Length];

        Buffer.BlockCopy(header, 0, response, 0, header.Length);
        Buffer.BlockCopy(dataAndCrc, 0, response, header.Length, dataAndCrc.Length);

        return response;
    }

    private byte[] ReadWriteFunctionResponse()
    {
        var header = _serialConnectionService.ReadExact(2);

        if ((header[ModbusMessage.TypeIndex] & 0x80) != 0)
        {
            var exceptionTail = _serialConnectionService.ReadExact(3);
            return [header[0], header[1], exceptionTail[0], exceptionTail[1], exceptionTail[2]];
        }

        var payloadAndCrc = _serialConnectionService.ReadExact(6);
        var response = new byte[header.Length + payloadAndCrc.Length];

        Buffer.BlockCopy(header, 0, response, 0, header.Length);
        Buffer.BlockCopy(payloadAndCrc, 0, response, header.Length, payloadAndCrc.Length);

        return response;
    }

    public (IOperationResult result, bool[] coils) SendFc1(ModbusMessage message)
    {
        if (!_serialConnectionService.IsSerialPortOpen)
        {
            return (OperationResult.Fail("Serial port is closed"), []);
        }

        message.Type = 0x01;

        _serialConnectionService.DiscardSerialBuffers();

        var coilByteCount = (message.Register + 7) / 8;
        if (coilByteCount > byte.MaxValue)
        {
            return (OperationResult.Fail("Requested coil count is too large"), []);
        }

        byte[] response;
        var request = message.BuildMessage();

        try
        {
            _serialConnectionService.WriteData(request);
            response = ReadReadFunctionResponse();
        }
        catch (Exception ex)
        {
            return (OperationResult.Fail($"Error sending FC1: {ex.Message}"), []);
        }

        var validationResult = ValidateResponse(response, message, 0x01);
        if (!validationResult.Success)
        {
            return (validationResult, []);
        }

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

        message.Type = 0x03;

        _serialConnectionService.DiscardSerialBuffers();

        byte[] response;
        var request = message.BuildMessage();

        try
        {
            _serialConnectionService.WriteData(request);
            response = ReadReadFunctionResponse();
        }
        catch (Exception ex)
        {
            return (OperationResult.Fail($"Error sending FC3: {ex.Message}"), []);
        }

        var validationResult = ValidateResponse(response, message, 0x03);
        if (!validationResult.Success)
        {
            return (validationResult, []);
        }

        var expectedByteCount = message.Register * 2;
        if (response[2] != expectedByteCount)
        {
            return (OperationResult.Fail("Mismatch in expected register data size"), []);
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

        message.Type = 0x02;

        _serialConnectionService.DiscardSerialBuffers();

        var request = message.BuildMessage();

        var byteCount = (message.Register + 7) / 8;
        if (byteCount > byte.MaxValue)
        {
            return (OperationResult.Fail("Requested input count is too large"), []);
        }

        byte[] response;

        try
        {
            _serialConnectionService.WriteData(request);
            response = ReadReadFunctionResponse();
        }
        catch (Exception ex)
        {
            return (OperationResult.Fail($"Error sending FC2: {ex.Message}"), []);
        }

        var validationResult = ValidateResponse(response, message, 0x02);
        if (!validationResult.Success)
        {
            return (validationResult, []);
        }

        if (response[2] != byteCount)
        {
            return (OperationResult.Fail("Mismatch in expected input data size"), []);
        }

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

        message.Type = 0x04;

        byte[] response;
        var request = message.BuildMessage();

        try
        {
            _serialConnectionService.WriteData(request);
            response = ReadReadFunctionResponse();
        }
        catch (Exception ex)
        {
            return (OperationResult.Fail($"Error sending FC4: {ex.Message}"), []);
        }

        var validationResult = ValidateResponse(response, message, 0x04);
        if (!validationResult.Success)
        {
            return (validationResult, []);
        }

        var expectedByteCount = message.Register * 2;
        if (response[2] != expectedByteCount)
        {
            return (OperationResult.Fail("Mismatch in expected input register data size"), []);
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
            var response = ReadWriteFunctionResponse();

            var validationResult = ValidateResponse(response, message, 0x05);
            if (!validationResult.Success)
            {
                return (validationResult, false);
            }

            if (response.Length != ModbusMessage.MessageSize)
            {
                return (OperationResult.Fail("Unexpected FC5 response length"), false);
            }

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
            var response = ReadWriteFunctionResponse();

            var validationResult = ValidateResponse(response, message, 0x06);
            if (!validationResult.Success)
            {
                return (validationResult, 0);
            }

            if (response.Length != ModbusMessage.MessageSize)
            {
                return (OperationResult.Fail("Unexpected FC6 response length"), 0);
            }

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

        if (message.Register != values.Length)
        {
            return OperationResult.Fail("Register count does not match number of coil values");
        }

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
            var response = ReadWriteFunctionResponse();

            var validationResult = ValidateResponse(response, message, 0x0F);
            if (!validationResult.Success)
            {
                return validationResult;
            }

            if (response.Length != ModbusMessage.MessageSize)
            {
                return OperationResult.Fail("Unexpected FC15 response length");
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

        message.Type = 0x10;

        if (message.Register != values.Length)
        {
            return OperationResult.Fail("Register count does not match number of values");
        }

        var request = message.BuildMessage(null, values);

        try
        {
            _serialConnectionService.WriteData(request);
            var response = ReadWriteFunctionResponse();

            var validationResult = ValidateResponse(response, message, 0x10);
            if (!validationResult.Success)
            {
                return validationResult;
            }

            return response.Length == ModbusMessage.MessageSize
                ? OperationResult.Ok()
                : OperationResult.Fail("Unexpected FC16 response length");
        }
        catch (Exception ex)
        {
            return OperationResult.Fail($"Error sending FC16: {ex.Message}");
        }
    }
}