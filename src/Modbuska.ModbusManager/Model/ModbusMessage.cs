namespace Modbuska.ModbusManager.Model;

/// <summary>
///     Base class for Modbus RTU messages. Concrete implementations should be created in the consuming project.
/// </summary>
public abstract class ModbusMessage
{
    /// <summary>
    ///     Gets or sets the slave address (1–247).
    /// </summary>
    public byte Address { get; init; }

    /// <summary>
    ///     Gets or sets the Modbus function code.
    /// </summary>
    public byte Type { get; set; }

    /// <summary>
    ///     Gets or sets the starting register address.
    /// </summary>
    public ushort Start { get; init; }

    /// <summary>
    ///     Gets or sets the number of registers or quantity.
    /// </summary>
    public ushort Register { get; init; }
}