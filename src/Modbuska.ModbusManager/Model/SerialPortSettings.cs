using System.IO.Ports;

namespace Modbuska.ModbusManager.Model;

/// <summary>
///     Settings for configuring a serial port connection.
/// </summary>
public class SerialPortSettings
{
    /// <summary>
    ///     Gets or sets the serial port name (e.g., "COM3" on Windows, "/dev/ttyUSB0" on Linux).
    /// </summary>
    public string? PortName { get; init; }

    /// <summary>
    ///     Gets or sets the baud rate.
    /// </summary>
    public int BaudRate { get; init; }

    /// <summary>
    ///     Gets or sets the number of data bits.
    /// </summary>
    public int DataBits { get; init; }

    /// <summary>
    ///     Gets or sets the parity.
    /// </summary>
    public Parity Parity { get; init; }

    /// <summary>
    ///     Gets or sets the stop bits.
    /// </summary>
    public StopBits StopBits { get; init; }

    /// <summary>
    ///     Gets or sets the read timeout in milliseconds. Defaults to 500.
    /// </summary>
    public int ReadTimeout { get; set; } = 500;

    /// <summary>
    ///     Gets or sets the write timeout in milliseconds. Defaults to 100.
    /// </summary>
    public int WriteTimeout { get; set; } = 100;
}