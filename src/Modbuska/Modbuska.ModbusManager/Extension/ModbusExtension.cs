namespace Modbuska.ModbusManager.Extension;

public static class ModbusExtension
{
    private const ushort TwoBytesFull = 0xFFFF;
    private const byte CrcSize = 2;

    public static byte[] ComputeCrc(this byte[] message)
    {
        //Function expects a modbus message of any length as well as a 2 byte CRC array in which to 
        //return the CRC values:
        var crcFull = TwoBytesFull;
        var crc = new byte[CrcSize];

        for (var i = 0; i < message.Length - CrcSize; i++)
        {
            crcFull = (ushort)(crcFull ^ message[i]); // XOR byte into least sig. byte of crc

            for (var j = 0; j < 8; j++)
            {
                var crcLsb = (byte)(crcFull & 0x0001); // check LSB (low bit)
                crcFull = (ushort)((crcFull >> 1) & 0x7FFF); // shift right 1 bit

                if (crcLsb == 1)
                {
                    crcFull = (ushort)(crcFull ^ 0xA001); // if LSB was 1, apply polynomial
                }
            }
        }

        crc[1] = (byte)((crcFull >> 8) & 0xFF); // Crc High
        crc[0] = (byte)(crcFull & 0xFF); // Crc low

        return crc;
    }
}