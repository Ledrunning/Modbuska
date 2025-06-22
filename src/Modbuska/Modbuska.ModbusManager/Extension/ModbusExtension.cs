namespace Modbuska.ModbusManager.Extension;

public static class ModbusExtension
{
    public static byte[] ComputeCrc(this byte[] message)
    {
        //Function expects a modbus message of any length as well as a 2 byte CRC array in which to 
        //return the CRC values:
        ushort crcFull = 0xFFFF;
        var crc = new byte[2];

        for (var i = 0; i < message.Length - 2; i++)
        {
            crcFull = (ushort)(crcFull ^ message[i]);

            for (var j = 0; j < 8; j++)
            {
                var crcLsb = (byte)(crcFull & 0x0001);
                crcFull = (ushort)((crcFull >> 1) & 0x7FFF);

                if (crcLsb == 1)
                {
                    crcFull = (ushort)(crcFull ^ 0xA001);
                }
            }
        }

        crc[1] = (byte)((crcFull >> 8) & 0xFF); // Crc High
        crc[0] = (byte)(crcFull & 0xFF); // Crc low

        return crc;
    }
}