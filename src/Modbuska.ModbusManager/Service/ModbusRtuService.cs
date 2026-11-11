using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Modbuska.ModbusManager.Abstraction;
using Modbuska.ModbusManager.Constant;
using Modbuska.ModbusManager.Extension;
using Modbuska.ModbusManager.Model;
using Modbuska.ModbusManager.Results;

namespace Modbuska.ModbusManager.Service;

/// <summary>
///     Provides methods for sending Modbus RTU function requests and parsing responses over a serial connection.
/// </summary>
/// <param name="serialConnectionService">The serial connection used to exchange frames.</param>
/// <param name="logger">Optional logger. When <c>null</c>, <see cref="NullLogger{T}.Instance" /> is used.</param>
public class ModbusRtuService(
    ISerialConnectionService serialConnectionService,
    ILogger<ModbusRtuService>? logger = null)
{
    private static readonly Action<ILogger, byte, ModbusFunctionType, string, Exception?> _logTxFrame =
        LoggerMessage.Define<byte, ModbusFunctionType, string>(
            LogLevel.Trace,
            new EventId(1, "ModbusRtuService.TxFrame"),
            "TX SlaveId={SlaveId} Function={Function} Frame={Frame}");

    private static readonly Action<ILogger, byte, ModbusFunctionType, string, Exception?> _logRxFrame =
        LoggerMessage.Define<byte, ModbusFunctionType, string>(
            LogLevel.Trace,
            new EventId(2, "ModbusRtuService.RxFrame"),
            "RX SlaveId={SlaveId} Function={Function} Frame={Frame}");

    private static readonly Action<ILogger, byte, ModbusFunctionType, string, Exception?> _logTransactionFailure =
        LoggerMessage.Define<byte, ModbusFunctionType, string>(
            LogLevel.Warning,
            new EventId(3, "ModbusRtuService.TransactionFailure"),
            "Modbus transaction failed. SlaveId={SlaveId} Function={Function} Reason={Reason}");

    private static readonly Action<ILogger, byte, ModbusFunctionType, int, Exception?> _logTransactionCompleted =
        LoggerMessage.Define<byte, ModbusFunctionType, int>(
            LogLevel.Debug,
            new EventId(4, "ModbusRtuService.TransactionCompleted"),
            "Modbus transaction completed. SlaveId={SlaveId} Function={Function} Count={Count} item(s)");

    private static readonly Action<ILogger, byte, ModbusFunctionType, string, Exception?> _logValidationFailure =
        LoggerMessage.Define<byte, ModbusFunctionType, string>(
            LogLevel.Debug,
            new EventId(5, "ModbusRtuService.ValidationFailure"),
            "Modbus transaction validation failed. SlaveId={SlaveId} Function={Function} Reason={Reason}");

    private readonly ILogger _logger = logger ?? NullLogger<ModbusRtuService>.Instance;
    private readonly ISerialConnectionService _serialConnectionService = serialConnectionService;
    private readonly Lock _transactionLock = new();

    /// <summary>
    ///     Formats a frame as uppercase hex bytes separated by spaces, e.g. <c>01 03 00 00 00 01 84 0A</c>.
    /// </summary>
    private static string FormatFrame(byte[] frame)
    {
        if (frame.Length == 0) return string.Empty;

        var chars = new char[frame.Length * 3 - 1];
        for (var i = 0; i < frame.Length; i++)
        {
            var value = frame[i];
            chars[i * 3] = ToHex(value >> 4);
            chars[i * 3 + 1] = ToHex(value & 0x0F);
            if (i < frame.Length - 1) chars[i * 3 + 2] = ' ';
        }

        return new string(chars);

        static char ToHex(int nibble)
        {
            return nibble < 10 ? (char)('0' + nibble) : (char)('A' + nibble - 10);
        }
    }

    private IOperationResult ValidateAddress(byte address, ModbusFunctionType function)
    {
        if (address == ModbusFrame.BroadcastAddress) return OperationResult.Ok();

        if (address is < ModbusFrame.MinSlaveAddress or > ModbusFrame.MaxSlaveAddress)
        {
            var reason =
                $"Slave address must be between {ModbusFrame.MinSlaveAddress} and {ModbusFrame.MaxSlaveAddress} (0 for broadcast)";
            _logValidationFailure(_logger, address, function, reason, null);
            return OperationResult.Fail(reason);
        }

        return OperationResult.Ok();
    }

    private static IOperationResult ValidateWriteEcho(byte[] request, byte[] response)
    {
        // Write responses echo the request header: address + function + start address + quantity/value
        const int echoLength = ModbusFrame.WriteResponseHeaderSize + ModbusFrame.WriteResponsePayloadSize -
                               ModbusFrame.CrcSize;
        if (response.Length < echoLength)
            return OperationResult.Fail("Write response is too short to contain echo data");

        for (var i = 0; i < echoLength; i++)
            if (response[i] != request[i])
                return OperationResult.Fail(
                    $"Write response echo mismatch at byte {i}: expected 0x{request[i]:X2}, got 0x{response[i]:X2}");

        return OperationResult.Ok();
    }

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
        lock (_transactionLock)
        {
            if (!_serialConnectionService.IsSerialPortOpen)
            {
                _logTransactionFailure(_logger, message.Address, function, "Serial port is closed", null);
                return (OperationResult.Fail("Serial port is closed"), default!);
            }

            message.Type = (byte)function;

            if (message.Address == ModbusFrame.BroadcastAddress)
            {
                _logTransactionFailure(_logger, message.Address, function,
                    "Read operations are not supported for broadcast address (0)", null);
                return (OperationResult.Fail("Read operations are not supported for broadcast address (0)"), default!);
            }

            _serialConnectionService.DiscardSerialBuffers();

            byte[] response;
            var request = message.BuildMessage();

            if (_logger.IsEnabled(LogLevel.Trace))
                _logTxFrame(_logger, message.Address, function, FormatFrame(request), null);

            try
            {
                _serialConnectionService.WriteData(request);
                response = ReadReadFunctionResponse();
            }
            catch (Exception ex)
            {
                _logTransactionFailure(_logger, message.Address, function, ex.Message, ex);
                return (OperationResult.Fail($"Error sending {operationName}: {ex.Message}"), default!);
            }

            if (_logger.IsEnabled(LogLevel.Trace))
                _logRxFrame(_logger, message.Address, function, FormatFrame(response), null);

            var validationResult = ValidateResponse(response, message, message.Type);

            if (!validationResult.Success)
            {
                _logTransactionFailure(_logger, message.Address, function,
                    validationResult.ErrorMessage ?? "Invalid response", null);
                return (validationResult, default!);
            }

            var parsed = parser(response);

            if (!parsed.Result.Success)
            {
                _logTransactionFailure(_logger, message.Address, function,
                    parsed.Result.ErrorMessage ?? "Invalid response", null);
                return (parsed.Result, default!);
            }

            _logTransactionCompleted(_logger, message.Address, function, message.Register, null);
            return (parsed.Result, parsed.Data);
        }
    }

    private (IOperationResult result, T data) ExecuteWrite<T>(
        ModbusMessage message,
        ModbusFunctionType function,
        string operationName,
        byte[] request,
        int itemCount,
        Func<byte[], ParseResult<T>> parser)
    {
        lock (_transactionLock)
        {
            if (!_serialConnectionService.IsSerialPortOpen)
            {
                _logTransactionFailure(_logger, message.Address, function, "Serial port is closed", null);
                return (OperationResult.Fail("Serial port is closed"), default!);
            }

            _serialConnectionService.DiscardSerialBuffers();

            if (_logger.IsEnabled(LogLevel.Trace))
                _logTxFrame(_logger, message.Address, function, FormatFrame(request), null);

            try
            {
                _serialConnectionService.WriteData(request);

                if (message.Address == ModbusFrame.BroadcastAddress)
                {
                    _logTransactionCompleted(_logger, message.Address, function, itemCount, null);
                    return (OperationResult.Ok(), default!);
                }

                var response = ReadWriteFunctionResponse();

                if (_logger.IsEnabled(LogLevel.Trace))
                    _logRxFrame(_logger, message.Address, function, FormatFrame(response), null);

                var validationResult = ValidateResponse(response, message, message.Type);

                if (!validationResult.Success)
                {
                    _logTransactionFailure(_logger, message.Address, function,
                        validationResult.ErrorMessage ?? "Invalid response", null);
                    return (validationResult, default!);
                }

                var echoResult = ValidateWriteEcho(request, response);

                if (!echoResult.Success)
                {
                    _logTransactionFailure(_logger, message.Address, function,
                        echoResult.ErrorMessage ?? "Invalid response", null);
                    return (echoResult, default!);
                }

                var parsed = parser(response);

                if (!parsed.Result.Success)
                {
                    _logTransactionFailure(_logger, message.Address, function,
                        parsed.Result.ErrorMessage ?? "Invalid response", null);
                    return (parsed.Result, default!);
                }

                _logTransactionCompleted(_logger, message.Address, function, itemCount, null);
                return (parsed.Result, parsed.Data);
            }
            catch (Exception ex)
            {
                _logTransactionFailure(_logger, message.Address, function, ex.Message, ex);
                return (OperationResult.Fail($"Error sending {operationName}: {ex.Message}"), default!);
            }
        }
    }

    /// <summary>
    ///     Function 01 - Read Coils. Reads the status of multiple coils from a Modbus slave device.
    /// </summary>
    /// <param name="message">The Modbus message containing the slave address, start address, and number of coils to read.</param>
    /// <returns>A tuple containing the operation result and an array of coil states.</returns>
    public (IOperationResult result, bool[] coils) SendFc1(ModbusMessage message)
    {
        var addressResult = ValidateAddress(message.Address, ModbusFunctionType.ReadCoils);
        if (!addressResult.Success) return (addressResult, []);

        if (message.Register == 0)
        {
            var reason = "Coil count must be greater than 0";
            _logValidationFailure(_logger, message.Address, ModbusFunctionType.ReadCoils, reason, null);
            return (OperationResult.Fail(reason), []);
        }

        if (message.Register > ModbusFrame.MaxReadCoils)
        {
            var reason = $"Requested coil count exceeds maximum of {ModbusFrame.MaxReadCoils}";
            _logValidationFailure(_logger, message.Address, ModbusFunctionType.ReadCoils, reason, null);
            return (OperationResult.Fail(reason), []);
        }

        var coilByteCount = (message.Register + 7) / 8;

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
    public (IOperationResult result, ushort[] obtainedData) SendFc3(ModbusMessage message)
    {
        var addressResult = ValidateAddress(message.Address, ModbusFunctionType.ReadHoldingRegisters);
        if (!addressResult.Success) return (addressResult, []);

        if (message.Register == 0)
        {
            var reason = "Register count must be greater than 0";
            _logValidationFailure(_logger, message.Address, ModbusFunctionType.ReadHoldingRegisters, reason, null);
            return (OperationResult.Fail(reason), []);
        }

        if (message.Register > ModbusFrame.MaxReadHoldingRegisters)
        {
            var reason = $"Requested register count exceeds maximum of {ModbusFrame.MaxReadHoldingRegisters}";
            _logValidationFailure(_logger, message.Address, ModbusFunctionType.ReadHoldingRegisters, reason, null);
            return (OperationResult.Fail(reason), []);
        }

        return ExecuteRead(message, ModbusFunctionType.ReadHoldingRegisters, "FC3", response =>
        {
            var expectedByteCount = message.Register * 2;
            if (response[ModbusFrame.ByteCountIndex] != expectedByteCount)
                return new ParseResult<ushort[]>(OperationResult.Fail("Mismatch in expected register data size"), []);

            var obtainedData = new ushort[message.Register];
            for (var i = 0; i < message.Register; i++)
                obtainedData[i] = (ushort)((response[ModbusFrame.DataStartIndex + 2 * i] << 8) |
                                           response[ModbusFrame.DataStartIndex + 2 * i + 1]);

            return new ParseResult<ushort[]>(OperationResult.Ok(), obtainedData);
        });
    }

    /// <summary>
    ///     Function 02 - Read Discrete Inputs. Reads the status of multiple discrete inputs from a Modbus slave device.
    /// </summary>
    /// <param name="message">The Modbus message containing the slave address, start address, and number of inputs to read.</param>
    /// <returns>A tuple containing the operation result and an array of input states.</returns>
    public (IOperationResult result, bool[] inputs) SendFc2(ModbusMessage message)
    {
        var addressResult = ValidateAddress(message.Address, ModbusFunctionType.ReadDiscreteInputs);
        if (!addressResult.Success) return (addressResult, []);

        if (message.Register == 0)
        {
            var reason = "Input count must be greater than 0";
            _logValidationFailure(_logger, message.Address, ModbusFunctionType.ReadDiscreteInputs, reason, null);
            return (OperationResult.Fail(reason), []);
        }

        if (message.Register > ModbusFrame.MaxReadDiscreteInputs)
        {
            var reason = $"Requested input count exceeds maximum of {ModbusFrame.MaxReadDiscreteInputs}";
            _logValidationFailure(_logger, message.Address, ModbusFunctionType.ReadDiscreteInputs, reason, null);
            return (OperationResult.Fail(reason), []);
        }

        var byteCount = (message.Register + 7) / 8;

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
    public (IOperationResult result, ushort[] obtainedData) SendFc4(ModbusMessage message)
    {
        var addressResult = ValidateAddress(message.Address, ModbusFunctionType.ReadInputRegisters);
        if (!addressResult.Success) return (addressResult, []);

        if (message.Register == 0)
        {
            var reason = "Register count must be greater than 0";
            _logValidationFailure(_logger, message.Address, ModbusFunctionType.ReadInputRegisters, reason, null);
            return (OperationResult.Fail(reason), []);
        }

        if (message.Register > ModbusFrame.MaxReadInputRegisters)
        {
            var reason = $"Requested register count exceeds maximum of {ModbusFrame.MaxReadInputRegisters}";
            _logValidationFailure(_logger, message.Address, ModbusFunctionType.ReadInputRegisters, reason, null);
            return (OperationResult.Fail(reason), []);
        }

        return ExecuteRead(message, ModbusFunctionType.ReadInputRegisters, "FC4", response =>
        {
            var expectedByteCount = message.Register * 2;
            if (response[ModbusFrame.ByteCountIndex] != expectedByteCount)
                return new ParseResult<ushort[]>(OperationResult.Fail("Mismatch in expected input register data size"),
                    []);

            var obtainedData = new ushort[message.Register];
            for (var i = 0; i < message.Register; i++)
                obtainedData[i] = (ushort)((response[ModbusFrame.DataStartIndex + 2 * i] << 8) |
                                           response[ModbusFrame.DataStartIndex + 2 * i + 1]);

            return new ParseResult<ushort[]>(OperationResult.Ok(), obtainedData);
        });
    }

    /// <summary>
    ///     Function 05 - Write Single Coil. Writes a single coil to a Modbus slave device.
    /// </summary>
    /// <param name="message">The Modbus message containing the slave address and coil address.</param>
    /// <param name="value">The coil state to write (<c>true</c> for ON, <c>false</c> for OFF).</param>
    /// <returns>A tuple containing the operation result and the confirmed coil state.</returns>
    /// <remarks>
    ///     When <paramref name="message" />.<see cref="ModbusMessage.Address" /> is
    ///     <see cref="ModbusFrame.BroadcastAddress" /> (0),
    ///     no slave response is expected. The method returns <see cref="IOperationResult.Ok" /> with a default coil state of
    ///     <c>false</c>.
    ///     Do not interpret <c>coilState == false</c> as the slave reporting that the coil is OFF — it simply means no
    ///     confirmation was
    ///     received (and none was expected).
    /// </remarks>
    public (IOperationResult result, bool coilState) SendFc5(ModbusMessage message, bool value)
    {
        var addressResult = ValidateAddress(message.Address, ModbusFunctionType.WriteSingleCoil);
        if (!addressResult.Success) return (addressResult, false);

        message.Type = (byte)ModbusFunctionType.WriteSingleCoil;
        var coilValue = value ? ModbusFrame.CoilOnValue : ModbusFrame.CoilOffValue;
        var request = message.BuildMessage(coilValue);

        return ExecuteWrite(message, ModbusFunctionType.WriteSingleCoil, "FC5", request, 1, response =>
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
    /// <remarks>
    ///     When <paramref name="message" />.<see cref="ModbusMessage.Address" /> is
    ///     <see cref="ModbusFrame.BroadcastAddress" /> (0),
    ///     no slave response is expected. The method returns <see cref="IOperationResult.Ok" /> with a default register value
    ///     of <c>0</c>.
    ///     Do not interpret <c>registerValue == 0</c> as the slave reporting that the register contains zero — it simply means
    ///     no confirmation
    ///     was received (and none was expected).
    /// </remarks>
    public (IOperationResult result, ushort registerValue) SendFc6(ModbusMessage message, ushort value)
    {
        var addressResult = ValidateAddress(message.Address, ModbusFunctionType.WriteSingleRegister);
        if (!addressResult.Success) return (addressResult, 0);

        message.Type = (byte)ModbusFunctionType.WriteSingleRegister;
        var request = message.BuildMessage(value);

        return ExecuteWrite(message, ModbusFunctionType.WriteSingleRegister, "FC6", request, 1, response =>
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
        var addressResult = ValidateAddress(message.Address, ModbusFunctionType.WriteMultipleCoils);
        if (!addressResult.Success) return addressResult;

        if (message.Register != values.Length)
        {
            var reason = "Register count does not match number of coil values";
            _logValidationFailure(_logger, message.Address, ModbusFunctionType.WriteMultipleCoils, reason, null);
            return OperationResult.Fail(reason);
        }

        if (message.Register == 0)
        {
            var reason = "Coil count must be greater than 0";
            _logValidationFailure(_logger, message.Address, ModbusFunctionType.WriteMultipleCoils, reason, null);
            return OperationResult.Fail(reason);
        }

        if (message.Register > ModbusFrame.MaxWriteMultipleCoils)
        {
            var reason = $"Requested coil count exceeds maximum of {ModbusFrame.MaxWriteMultipleCoils}";
            _logValidationFailure(_logger, message.Address, ModbusFunctionType.WriteMultipleCoils, reason, null);
            return OperationResult.Fail(reason);
        }

        var byteCount = (values.Length + 7) / 8; // upward rounding
        var buffer = new byte[ModbusFrame.WriteRequestHeaderSize + byteCount +
                              ModbusFrame.CrcSize]; // address+function+start+number+of+bytes+software+data+CRC

        message.Type = (byte)ModbusFunctionType.WriteMultipleCoils;
        buffer[ModbusFrame.AddressIndex] = message.Address;
        buffer[ModbusFrame.TypeIndex] = message.Type;
        buffer[ModbusFrame.StartIndexHigh] = (byte)(message.Start >> ModbusFrame.HighByteShift);
        buffer[ModbusFrame.StartIndexLow] = (byte)message.Start;
        buffer[ModbusFrame.RegisterIndexHigh] = (byte)(message.Register >> ModbusFrame.HighByteShift);
        buffer[ModbusFrame.RegisterIndexLow] = (byte)message.Register;
        buffer[ModbusFrame.WriteRequestByteCountIndex] = (byte)byteCount;

        // Pack LSB bits first (data starts at offset WriteRequestHeaderSize in FC15 request frame)
        for (var i = 0; i < values.Length; i++)
            if (values[i])
                buffer[ModbusFrame.WriteRequestHeaderSize + i / 8] |= (byte)(1 << (i % 8));

        // CRC
        var crc = buffer.ComputeCrc();
        buffer[^2] = crc[ModbusFrame.CrcLowIndex];
        buffer[^1] = crc[ModbusFrame.CrcHighIndex];

        return ExecuteWrite(message, ModbusFunctionType.WriteMultipleCoils, "FC15", buffer, values.Length,
            response => response.Length != ModbusFrame.MessageSize
                ? new ParseResult<bool>(OperationResult.Fail("Unexpected FC15 response length"), false)
                : new ParseResult<bool>(OperationResult.Ok(), true)).result;
    }

    /// <summary>
    ///     Function 16 - Write Multiple Registers. Writes multiple holding registers to a Modbus slave device.
    /// </summary>
    /// <param name="message">The Modbus message containing the slave address, start address, and number of registers.</param>
    /// <param name="values">An array of register values to write.</param>
    /// <returns>An <see cref="IOperationResult" /> indicating success or failure.</returns>
    public IOperationResult SendFc16(ModbusMessage message, ushort[] values)
    {
        var addressResult = ValidateAddress(message.Address, ModbusFunctionType.WriteMultipleRegisters);
        if (!addressResult.Success) return addressResult;

        if (message.Register != values.Length)
        {
            var reason = "Register count does not match number of values";
            _logValidationFailure(_logger, message.Address, ModbusFunctionType.WriteMultipleRegisters, reason, null);
            return OperationResult.Fail(reason);
        }

        if (message.Register == 0)
        {
            var reason = "Register count must be greater than 0";
            _logValidationFailure(_logger, message.Address, ModbusFunctionType.WriteMultipleRegisters, reason, null);
            return OperationResult.Fail(reason);
        }

        if (message.Register > ModbusFrame.MaxWriteMultipleRegisters)
        {
            var reason = $"Requested register count exceeds maximum of {ModbusFrame.MaxWriteMultipleRegisters}";
            _logValidationFailure(_logger, message.Address, ModbusFunctionType.WriteMultipleRegisters, reason, null);
            return OperationResult.Fail(reason);
        }

        message.Type = (byte)ModbusFunctionType.WriteMultipleRegisters;
        var request = message.BuildMessage(null, values);

        return ExecuteWrite(message, ModbusFunctionType.WriteMultipleRegisters, "FC16", request, values.Length,
            response => response.Length != ModbusFrame.MessageSize
                ? new ParseResult<bool>(OperationResult.Fail("Unexpected FC16 response length"), false)
                : new ParseResult<bool>(OperationResult.Ok(), true)).result;
    }
}