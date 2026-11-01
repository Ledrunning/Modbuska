namespace Modbuska.ModbusManager.Constant;

/// <summary>
///     Modbus function codes supported by the library.
/// </summary>
public enum ModbusFunctionType : byte
{
    /// <summary>
    ///     Read Coils (Function Code 0x01).
    /// </summary>
    ReadCoils = 0x01,

    /// <summary>
    ///     Read Discrete Inputs (Function Code 0x02).
    /// </summary>
    ReadDiscreteInputs = 0x02,

    /// <summary>
    ///     Read Holding Registers (Function Code 0x03).
    /// </summary>
    ReadHoldingRegisters = 0x03,

    /// <summary>
    ///     Read Input Registers (Function Code 0x04).
    /// </summary>
    ReadInputRegisters = 0x04,

    /// <summary>
    ///     Write Single Coil (Function Code 0x05).
    /// </summary>
    WriteSingleCoil = 0x05,

    /// <summary>
    ///     Write Single Register (Function Code 0x06).
    /// </summary>
    WriteSingleRegister = 0x06,

    /// <summary>
    ///     Write Multiple Coils (Function Code 0x0F).
    /// </summary>
    WriteMultipleCoils = 0x0F,

    /// <summary>
    ///     Write Multiple Registers (Function Code 0x10).
    /// </summary>
    WriteMultipleRegisters = 0x10
}