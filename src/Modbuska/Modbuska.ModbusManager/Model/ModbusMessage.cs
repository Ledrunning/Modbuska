namespace Modbuska.ModbusManager.Model;

public class ModbusMessage
{
    private const byte WordSize = 8;

    public byte Address { get; set; }
    public byte Type { get; set; }
    public ushort Start { get; set; }
    public ushort Register { get; set; }
    public byte[] Message { get; set; } = new byte[WordSize];
}