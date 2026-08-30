namespace Modbuska.ModbusManager.Model;

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
}