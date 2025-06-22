using System.IO.Ports;

namespace Modbuska.ModbusManager.Model;

public class SerialPortSettings
{
    public string? Port { get; set; }
    public int BaudRate { get; set; }
    public int DataBits { get; set; }
    public Parity Parity { get; set; }
    public StopBits StopBits { get; set; }
    public string? ThreadControl { get; set; }
}