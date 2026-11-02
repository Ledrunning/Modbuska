namespace Modbuska.ModbusManager.Constant;

/// <summary>
///     Protocol-level constants for Modbus RTU frames.
/// </summary>
public static class ModbusFrame
{
    /// <summary>
    ///     Index of the slave address byte in a Modbus RTU frame.
    /// </summary>
    public const int AddressIndex = 0;

    /// <summary>
    ///     Index of the function code byte in a Modbus RTU frame.
    /// </summary>
    public const int TypeIndex = 1;

    /// <summary>
    ///     Index of the high byte of the start address in a request frame.
    /// </summary>
    public const int StartIndexHigh = 2;

    /// <summary>
    ///     Index of the low byte of the start address in a request frame.
    /// </summary>
    public const int StartIndexLow = 3;

    /// <summary>
    ///     Index of the high byte of the register quantity in a request frame.
    /// </summary>
    public const int RegisterIndexHigh = 4;

    /// <summary>
    ///     Index of the low byte of the register quantity in a request frame.
    /// </summary>
    public const int RegisterIndexLow = 5;

    /// <summary>
    ///     Index of the byte count field in a response frame.
    /// </summary>
    public const int ByteCountIndex = 2;

    /// <summary>
    ///     Index of the first data byte in a response frame.
    /// </summary>
    public const int DataStartIndex = 3;

    /// <summary>
    ///     Index of the low byte of the CRC in a Modbus RTU frame.
    /// </summary>
    public const int CrcLowIndex = 0;

    /// <summary>
    ///     Index of the high byte of the CRC in a Modbus RTU frame.
    /// </summary>
    public const int CrcHighIndex = 1;

    /// <summary>
    ///     Total size of a standard Modbus RTU request/response frame (8 bytes).
    /// </summary>
    public const byte MessageSize = 8;

    /// <summary>
    ///     Bit flag indicating an exception response (function code OR 0x80).
    /// </summary>
    public const byte ExceptionFlag = 0x80;

    /// <summary>
    ///     Size of the CRC checksum in bytes.
    /// </summary>
    public const int CrcSize = 2;

    /// <summary>
    ///     Size of a read response header in bytes (address + function + byte count).
    /// </summary>
    public const int ReadResponseHeaderSize = 3;

    /// <summary>
    ///     Size of a write response header in bytes (address + function).
    /// </summary>
    public const int WriteResponseHeaderSize = 2;

    /// <summary>
    ///     Size of a write response payload in bytes (start address + register count + CRC).
    /// </summary>
    public const int WriteResponsePayloadSize = 6;

    /// <summary>
    ///     Size of the exception tail for read function responses in bytes (CRC only, exception code is in byte count
    ///     position).
    /// </summary>
    public const int ExceptionReadTailSize = 2;

    /// <summary>
    ///     Size of the exception tail for write function responses in bytes (exception code + CRC).
    /// </summary>
    public const int ExceptionWriteTailSize = 3;

    /// <summary>
    ///     Size of a write request header in bytes before data (address + function + start address + register count + byte
    ///     count).
    /// </summary>
    public const int WriteRequestHeaderSize = 7;

    /// <summary>
    ///     Coil ON value for Function Code 05 (Write Single Coil).
    /// </summary>
    public const ushort CoilOnValue = 0xFF00;

    /// <summary>
    ///     Coil OFF value for Function Code 05 (Write Single Coil).
    /// </summary>
    public const ushort CoilOffValue = 0x0000;

    /// <summary>
    ///     High byte value indicating an ON coil state in the response.
    /// </summary>
    public const byte CoilOnHighByte = 0xFF;

    /// <summary>
    ///     Low byte value indicating an ON coil state in the response.
    /// </summary>
    public const byte CoilOnLowByte = 0x00;

    /// <summary>
    ///     Number of bytes in a single Modbus register (16-bit value).
    /// </summary>
    public const int BytesPerRegister = 2;

    /// <summary>
    ///     Number of bits to shift right to extract the high byte of a 16-bit value.
    /// </summary>
    public const int HighByteShift = 8;

    /// <summary>
    ///     CRC-16 Modbus polynomial used for checksum computation.
    /// </summary>
    public const ushort CrcPolynomial = 0xA001;

    /// <summary>
    ///     Index of the byte count field in a write request frame (FC15/FC16).
    /// </summary>
    public const int WriteRequestByteCountIndex = 6;

    /// <summary>
    ///     Broadcast slave address (no response expected from slaves).
    /// </summary>
    public const byte BroadcastAddress = 0;

    /// <summary>
    ///     Minimum valid slave address (inclusive).
    /// </summary>
    public const byte MinSlaveAddress = 1;

    /// <summary>
    ///     Maximum valid slave address (inclusive).
    /// </summary>
    public const byte MaxSlaveAddress = 247;

    /// <summary>
    ///     Maximum number of coils for FC1 (Read Coils).
    /// </summary>
    public const int MaxReadCoils = 2000;

    /// <summary>
    ///     Maximum number of discrete inputs for FC2 (Read Discrete Inputs).
    /// </summary>
    public const int MaxReadDiscreteInputs = 2000;

    /// <summary>
    ///     Maximum number of holding registers for FC3 (Read Holding Registers).
    /// </summary>
    public const int MaxReadHoldingRegisters = 125;

    /// <summary>
    ///     Maximum number of input registers for FC4 (Read Input Registers).
    /// </summary>
    public const int MaxReadInputRegisters = 125;

    /// <summary>
    ///     Maximum number of coils for FC15 (Write Multiple Coils).
    /// </summary>
    public const int MaxWriteMultipleCoils = 1968;

    /// <summary>
    ///     Maximum number of registers for FC16 (Write Multiple Registers).
    /// </summary>
    public const int MaxWriteMultipleRegisters = 123;
}