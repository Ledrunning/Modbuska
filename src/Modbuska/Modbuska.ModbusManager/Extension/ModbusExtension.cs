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

    public static byte[] BuildMessage(this ModbusMessage message, ushort? singleValue = null, short[]? values = null)
    {
        byte[] buffer;

        if (values != null)
        {
            // Functions with an array of values (FC15, FC16)
            var byteCount = values.Length * 2;
            buffer = new byte[9 + byteCount];

            buffer[ModbusFrame.AddressIndex] = message.Address;
            buffer[ModbusFrame.TypeIndex] = message.Type;
            buffer[ModbusFrame.StartIndexHigh] = (byte)(message.Start >> ModbusFrame.MessageSize);
            buffer[ModbusFrame.StartIndexLow] = (byte)message.Start;
            buffer[ModbusFrame.RegisterIndexHigh] = (byte)(message.Register >> ModbusFrame.MessageSize);
            buffer[ModbusFrame.RegisterIndexLow] = (byte)message.Register;
            buffer[6] = (byte)byteCount;

            for (var i = 0; i < values.Length; i++)
            {
                buffer[7 + i * 2] = (byte)(values[i] >> ModbusFrame.MessageSize);
                buffer[8 + i * 2] = (byte)values[i];
            }
        }
        else if (singleValue.HasValue)
        {
            // Functions with one value (FC5, FC6)
            buffer = new byte[ModbusFrame.MessageSize];

            buffer[ModbusFrame.AddressIndex] = message.Address;
            buffer[ModbusFrame.TypeIndex] = message.Type;
            buffer[ModbusFrame.StartIndexHigh] = (byte)(message.Start >> ModbusFrame.MessageSize);
            buffer[ModbusFrame.StartIndexLow] = (byte)message.Start;
            buffer[ModbusFrame.RegisterIndexHigh] = (byte)(singleValue.Value >> ModbusFrame.MessageSize);
            buffer[ModbusFrame.RegisterIndexLow] = (byte)singleValue.Value;
        }
        else
        {
            // Functions without data (FC1, FC3, etc.)
            buffer = new byte[ModbusFrame.MessageSize];

            buffer[ModbusFrame.AddressIndex] = message.Address;
            buffer[ModbusFrame.TypeIndex] = message.Type;
            buffer[ModbusFrame.StartIndexHigh] = (byte)(message.Start >> ModbusFrame.MessageSize);
            buffer[ModbusFrame.StartIndexLow] = (byte)message.Start;
            buffer[ModbusFrame.RegisterIndexHigh] = (byte)(message.Register >> ModbusFrame.MessageSize);
            buffer[ModbusFrame.RegisterIndexLow] = (byte)message.Register;
        }

        var crc = buffer.ComputeCrc();
        buffer[^2] = crc[ModbusFrame.CrcLowIndex];
        buffer[^1] = crc[ModbusFrame.CrcHighIndex];

        return buffer;
    }


    public static bool CheckResponse(this byte[] response)
    {
        //Perform a basic CRC check:
        var crc = response.ComputeCrc();
        return crc[ModbusFrame.CrcLowIndex] == response[^2] && crc[ModbusFrame.CrcHighIndex] == response[^1];
    }
}