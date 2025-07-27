namespace Modbuska.ModbusManager.Model;

public class ModbusMessage
{
    public const int AddressIndex = 0;
    public const int TypeIndex = 1;
    public const int StartIndexHigh = 2;
    public const int StartIndexLow = 3;
    public const int RegisterIndexHigh = 4;
    public const int RegisterIndexLow = 5;
    public const int CrcLowIndex = 6;
    public const int CrcHighIndex = 7;
    public const byte MessageSize = 8;

    public byte Address { get; set; }
    public byte Type { get; set; }
    public ushort Start { get; set; }
    public ushort Register { get; set; }
    public byte[] Message { get; set; } = new byte[MessageSize];
}