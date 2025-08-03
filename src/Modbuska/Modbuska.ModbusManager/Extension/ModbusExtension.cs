using Modbuska.ModbusManager.Model;

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

    public static byte[] BuildMessage(this ModbusMessage message)
    {
        var buffer = new byte[ModbusMessage.MessageSize];

        buffer[ModbusMessage.AddressIndex] = message.Address;
        buffer[ModbusMessage.TypeIndex] = message.Type;
        buffer[ModbusMessage.StartIndexHigh] = (byte)(message.Start >> 8);
        buffer[ModbusMessage.StartIndexLow] = (byte)message.Start;
        buffer[ModbusMessage.RegisterIndexHigh] = (byte)(message.Register >> 8);
        buffer[ModbusMessage.RegisterIndexLow] = (byte)message.Register;

        var crc = buffer.ComputeCrc();
        buffer[^2] = crc[0];
        buffer[^1] = crc[1];

        return buffer;
    }

    public static byte[] BuildMessage(this ModbusMessage message, short[] values)
    {
        var byteCount = values.Length * 2;
        var buffer = new byte[9 + byteCount];

        buffer[ModbusMessage.AddressIndex] = message.Address;
        buffer[ModbusMessage.TypeIndex] = message.Type;
        buffer[ModbusMessage.StartIndexHigh] = (byte)(message.Start >> 8);
        buffer[ModbusMessage.StartIndexLow] = (byte)message.Start;
        buffer[ModbusMessage.RegisterIndexHigh] = (byte)(message.Register >> 8);
        buffer[ModbusMessage.RegisterIndexLow] = (byte)message.Register;
        buffer[6] = (byte)byteCount;

        for (var i = 0; i < values.Length; i++)
        {
            buffer[7 + i * 2] = (byte)(values[i] >> 8);
            buffer[8 + i * 2] = (byte)values[i];
        }

        var crc = buffer.ComputeCrc();
        buffer[^2] = crc[0];
        buffer[^1] = crc[1];

        return buffer;
    }

    public static bool CheckResponse(this byte[] response)
    {
        //Perform a basic CRC check:
        var crc = response.ComputeCrc();
        return crc[0] == response[^2] && crc[1] == response[^1];
    }
}