using Modbuska.ModbusManager.Abstraction;
using Modbuska.ModbusManager.Extension;
using Modbuska.ModbusManager.Model;
using Modbuska.ModbusManager.Results;

namespace Modbuska.ModbusManager.Service;

/// <summary>
///     Provides methods for sending Modbus RTU function requests and parsing responses over a serial connection.
/// </summary>
public class ModbusRtuService(ISerialConnectionService serialConnectionService)
{
    private readonly ISerialConnectionService _serialConnectionService = serialConnectionService;

    private static IOperationResult ValidateResponse(byte[] response, ModbusMessage message, byte functionCode)
    {
        if (!response.CheckResponse()) return OperationResult.Fail("Invalid CRC in response");

        if (response[ModbusFrame.AddressIndex] != message.Address)
            return OperationResult.Fail("Unexpected slave address in response");

        var responseFunction = response[ModbusFrame.TypeIndex];
        if (responseFunction == (functionCode | ModbusFrame.ExceptionFlag))
            return OperationResult.Fail($"Modbus exception response. Code: 0x{response[2]:X2}");

        if (responseFunction != functionCode) return OperationResult.Fail("Unexpected function code in response");

        return OperationResult.Ok();
    }

    private byte[] ReadReadFunctionResponse()
    {
        var header = _serialConnectionService.ReadExact(ModbusFrame.ReadResponseHeaderSize);

        if ((header[ModbusFrame.TypeIndex] & ModbusFrame.ExceptionFlag) != 0)
        {
            var exceptionTail = _serialConnectionService.ReadExact(ModbusFrame.ExceptionReadTailSize);
            return [header[0], header[1], header[2], exceptionTail[0], exceptionTail[1]];
        }

        var dataAndCrc = _serialConnectionService.ReadExact(header[ModbusFrame.ByteCountIndex] + ModbusFrame.CrcSize);
        var response = new byte[header.Length + dataAndCrc.Length];

        Buffer.BlockCopy(header, 0, response, 0, header.Length);
        Buffer.BlockCopy(dataAndCrc, 0, response, header.Length, dataAndCrc.Length);

        return response;
    }

    private byte[] ReadWriteFunctionResponse()
    {
        var header = _serialConnectionService.ReadExact(ModbusFrame.WriteResponseHeaderSize);

        if ((header[ModbusFrame.TypeIndex] & ModbusFrame.ExceptionFlag) != 0)
        {
            var exceptionTail = _serialConnectionService.ReadExact(ModbusFrame.ExceptionWriteTailSize);
            return [header[0], header[1], exceptionTail[0], exceptionTail[1], exceptionTail[2]];
        }

        var payloadAndCrc = _serialConnectionService.ReadExact(ModbusFrame.WriteResponsePayloadSize);
        var response = new byte[header.Length + payloadAndCrc.Length];

        Buffer.BlockCopy(header, 0, response, 0, header.Length);
        Buffer.BlockCopy(payloadAndCrc, 0, response, header.Length, payloadAndCrc.Length);

        return response;
    }

    private (IOperationResult result, T data) ExecuteRead<T>(
        ModbusMessage message,
        ModbusFunctionType function,
        string operationName,
        Func<byte[], ParseResult<T>> parser)
    {
        if (!_serialConnectionService.IsSerialPortOpen)
            return (OperationResult.Fail("Serial port is closed"), default!);

        message.Type = (byte)function;

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
            return (OperationResult.Fail($"Error sending {operationName}: {ex.Message}"), default!);
        }

        var validationResult = ValidateResponse(response, message, (byte)function);
        if (!validationResult.Success) return (validationResult, default!);

        var parsed = parser(response);
        return (parsed.Result, parsed.Data);
    }

    private (IOperationResult result, T data) ExecuteWrite<T>(
        ModbusMessage message,
        ModbusFunctionType function,
        string operationName,
        byte[] request,
        Func<byte[], ParseResult<T>> parser)
    {
        if (!_serialConnectionService.IsSerialPortOpen)
            return (OperationResult.Fail("Serial port is closed"), default!);

        message.Type = (byte)function;

        _serialConnectionService.DiscardSerialBuffers();

        try
        {
            _serialConnectionService.WriteData(request);
            var response = ReadWriteFunctionResponse();

            var validationResult = ValidateResponse(response, message, (byte)function);
            if (!validationResult.Success) return (validationResult, default!);

            var parsed = parser(response);
            return (parsed.Result, parsed.Data);
        }
        catch (Exception ex)
        {
            return (OperationResult.Fail($"Error sending {operationName}: {ex.Message}"), default!);
        }
    }

    /// <summary>
    ///     Function 01 - Read Coils. Reads the status of multiple coils from a Modbus slave device.
    /// </summary>
    /// <param name="message">The Modbus message containing the slave address, start address, and number of coils to read.</param>
    /// <returns>A tuple containing the operation result and an array of coil states.</returns>
    public (IOperationResult result, bool[] coils) SendFc1(ModbusMessage message)
    {
        var coilByteCount = (message.Register + 7) / 8;
        if (coilByteCount > byte.MaxValue)
            return (OperationResult.Fail("Requested coil count is too large"), []);

        return ExecuteRead(message, ModbusFunctionType.ReadCoils, "FC1", response =>
        {
            if (response[ModbusFrame.ByteCountIndex] != coilByteCount)
                return new ParseResult<bool[]>(OperationResult.Fail("Mismatch in expected coil data size"), []);

            var coils = new bool[message.Register];
            for (var i = 0; i < message.Register; i++)
            {
                var byteIndex = i / 8;
                var bitIndex = i % 8;
                coils[i] = (response[ModbusFrame.DataStartIndex + byteIndex] & (1 << bitIndex)) != 0;
            }

            return new ParseResult<bool[]>(OperationResult.Ok(), coils);
        });
    }

    /// <summary>
    ///     Function 03 - Read Holding Registers. Reads the content of multiple holding registers from a Modbus slave device.
    /// </summary>
    /// <param name="message">The Modbus message containing the slave address, start address, and number of registers to read.</param>
    /// <returns>A tuple containing the operation result and an array of register values.</returns>
    public (IOperationResult result, short[] obtainedData) SendFc3(ModbusMessage message)
    {
        return ExecuteRead(message, ModbusFunctionType.ReadHoldingRegisters, "FC3", response =>
        {
            var expectedByteCount = message.Register * 2;
            if (response[ModbusFrame.ByteCountIndex] != expectedByteCount)
                return new ParseResult<short[]>(OperationResult.Fail("Mismatch in expected register data size"), []);

            var obtainedData = new short[message.Register];
            for (var i = 0; i < message.Register; i++)
                obtainedData[i] = (short)((response[ModbusFrame.DataStartIndex + 2 * i] << 8) |
                                          response[ModbusFrame.DataStartIndex + 2 * i + 1]);

            return new ParseResult<short[]>(OperationResult.Ok(), obtainedData);
        });
    }

    /// <summary>
    ///     Function 02 - Read Discrete Inputs. Reads the status of multiple discrete inputs from a Modbus slave device.
    /// </summary>
    /// <param name="message">The Modbus message containing the slave address, start address, and number of inputs to read.</param>
    /// <returns>A tuple containing the operation result and an array of input states.</returns>
    public (IOperationResult result, bool[] inputs) SendFc2(ModbusMessage message)
    {
        var byteCount = (message.Register + 7) / 8;
        if (byteCount > byte.MaxValue)
            return (OperationResult.Fail("Requested input count is too large"), []);

        return ExecuteRead(message, ModbusFunctionType.ReadDiscreteInputs, "FC2", response =>
        {
            if (response[ModbusFrame.ByteCountIndex] != byteCount)
                return new ParseResult<bool[]>(OperationResult.Fail("Mismatch in expected input data size"), []);

            var inputs = new bool[message.Register];
            for (var i = 0; i < message.Register; i++)
            {
                var byteIndex = i / 8;
                var bitIndex = i % 8;
                inputs[i] = (response[ModbusFrame.DataStartIndex + byteIndex] & (1 << bitIndex)) != 0;
            }

            return new ParseResult<bool[]>(OperationResult.Ok(), inputs);
        });
    }

    /// <summary>
    ///     Function 04 - Read Input Registers. Reads the content of multiple input registers from a Modbus slave device.
    /// </summary>
    /// <param name="message">The Modbus message containing the slave address, start address, and number of registers to read.</param>
    /// <returns>A tuple containing the operation result and an array of register values.</returns>
    public (IOperationResult result, short[] obtainedData) SendFc4(ModbusMessage message)
    {
        return ExecuteRead(message, ModbusFunctionType.ReadInputRegisters, "FC4", response =>
        {
            var expectedByteCount = message.Register * 2;
            if (response[ModbusFrame.ByteCountIndex] != expectedByteCount)
                return new ParseResult<short[]>(OperationResult.Fail("Mismatch in expected input register data size"),
                    []);

            var obtainedData = new short[message.Register];
            for (var i = 0; i < message.Register; i++)
                obtainedData[i] = (short)((response[ModbusFrame.DataStartIndex + 2 * i] << 8) |
                                          response[ModbusFrame.DataStartIndex + 2 * i + 1]);

            return new ParseResult<short[]>(OperationResult.Ok(), obtainedData);
        });
    }

    /// <summary>
    ///     Function 05 - Write Single Coil. Writes a single coil to a Modbus slave device.
    /// </summary>
    /// <param name="message">The Modbus message containing the slave address and coil address.</param>
    /// <param name="value">The coil state to write (<c>true</c> for ON, <c>false</c> for OFF).</param>
    /// <returns>A tuple containing the operation result and the confirmed coil state.</returns>
    public (IOperationResult result, bool coilState) SendFc5(ModbusMessage message, bool value)
    {
        var coilValue = value ? ModbusFrame.CoilOnValue : ModbusFrame.CoilOffValue;
        var request = message.BuildMessage(coilValue);

        return ExecuteWrite(message, ModbusFunctionType.WriteSingleCoil, "FC5", request, response =>
        {
            if (response.Length != ModbusFrame.MessageSize)
                return new ParseResult<bool>(OperationResult.Fail("Unexpected FC5 response length"), false);

            var returnedState = response[ModbusFrame.RegisterIndexHigh] == ModbusFrame.CoilOnHighByte &&
                                response[ModbusFrame.RegisterIndexLow] == ModbusFrame.CoilOnLowByte;

            return new ParseResult<bool>(OperationResult.Ok(), returnedState);
        });
    }

    /// <summary>
    ///     Function 06 - Write Single Register. Writes a value to a single holding register on a Modbus slave device.
    /// </summary>
    /// <param name="message">The Modbus message containing the slave address and register address.</param>
    /// <param name="value">The value to write to the register.</param>
    /// <returns>A tuple containing the operation result and the confirmed register value.</returns>
    public (IOperationResult result, ushort registerValue) SendFc6(ModbusMessage message, ushort value)
    {
        var request = message.BuildMessage(value);

        return ExecuteWrite(message, ModbusFunctionType.WriteSingleRegister, "FC6", request, response =>
        {
            if (response.Length != ModbusFrame.MessageSize)
                return new ParseResult<ushort>(OperationResult.Fail("Unexpected FC6 response length"), 0);

            var returnedValue = (ushort)((response[ModbusFrame.RegisterIndexHigh] << 8) |
                                         response[ModbusFrame.RegisterIndexLow]);

            return new ParseResult<ushort>(OperationResult.Ok(), returnedValue);
        });
    }

    /// <summary>
    ///     Function 15 - Write Multiple Coils. Writes multiple coils to a Modbus slave device.
    /// </summary>
    /// <param name="message">The Modbus message containing the slave address, start address, and number of coils.</param>
    /// <param name="values">An array of coil states to write.</param>
    /// <returns>An <see cref="IOperationResult" /> indicating success or failure.</returns>
    public IOperationResult SendFc15(ModbusMessage message, bool[] values)
    {
        if (message.Register != values.Length)
            return OperationResult.Fail("Register count does not match number of coil values");

        var byteCount = (values.Length + 7) / 8; // upward rounding
        var buffer =
            new byte[ModbusFrame.WriteRequestHeaderSize + 1 + byteCount +
                     ModbusFrame.CrcSize]; // address+function+start+number+of+bytes+software+data+CRC

        buffer[ModbusFrame.AddressIndex] = message.Address;
        buffer[ModbusFrame.TypeIndex] = message.Type;
        buffer[ModbusFrame.StartIndexHigh] = (byte)(message.Start >> 8);
        buffer[ModbusFrame.StartIndexLow] = (byte)message.Start;
        buffer[ModbusFrame.RegisterIndexHigh] = (byte)(message.Register >> 8);
        buffer[ModbusFrame.RegisterIndexLow] = (byte)message.Register;
        buffer[ModbusFrame.ByteCountIndex] = (byte)byteCount;

        // Pack LSB bits first
        for (var i = 0; i < values.Length; i++)
            if (values[i])
                buffer[ModbusFrame.DataStartIndex + i / 8] |= (byte)(1 << (i % 8));

        // CRC
        var crc = buffer.ComputeCrc();
        buffer[^2] = crc[ModbusFrame.CrcLowIndex];
        buffer[^1] = crc[ModbusFrame.CrcHighIndex];

        return ExecuteWrite(message, ModbusFunctionType.WriteMultipleCoils, "FC15", buffer, response =>
        {
            if (response.Length != ModbusFrame.MessageSize)
                return new ParseResult<bool>(OperationResult.Fail("Unexpected FC15 response length"), false);

            return new ParseResult<bool>(OperationResult.Ok(), true);
        }).result;
    }

    /// <summary>
    ///     Function 16 - Write Multiple Registers. Writes multiple holding registers to a Modbus slave device.
    /// </summary>
    /// <param name="message">The Modbus message containing the slave address, start address, and number of registers.</param>
    /// <param name="values">An array of register values to write.</param>
    /// <returns>An <see cref="IOperationResult" /> indicating success or failure.</returns>
    public IOperationResult SendFc16(ModbusMessage message, short[] values)
    {
        if (message.Register != values.Length)
            return OperationResult.Fail("Register count does not match number of values");

        var request = message.BuildMessage(null, values);

        return ExecuteWrite(message, ModbusFunctionType.WriteMultipleRegisters, "FC16", request, response =>
        {
            if (response.Length != ModbusFrame.MessageSize)
                return new ParseResult<bool>(OperationResult.Fail("Unexpected FC16 response length"), false);

            return new ParseResult<bool>(OperationResult.Ok(), true);
        }).result;
    }

    private readonly record struct ParseResult<T>(IOperationResult Result, T Data);
}