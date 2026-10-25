using Modbuska.ModbusManager.Model;

namespace Modbuska.ModbusManager.Extension;

/// <summary>
///     Provides extension methods for Modbus RTU message building, CRC computation, and response validation.
/// </summary>
public static class ModbusExtension
{
    private const ushort TwoBytesFull = 0xFFFF;

    /// <summary>
    ///     Computes the CRC-16 (Modbus) checksum for the specified message.
    /// </summary>
    /// <param name="message">The message bytes excluding the last 2 CRC bytes.</param>
    /// <returns>A 2-byte array containing the CRC low byte at index 0 and CRC high byte at index 1.</returns>
    public static byte[] ComputeCrc(this byte[] message)
    {
        //Function expects a modbus message of any length as well as a 2 byte CRC array in which to
        //return the CRC values:
        var crcFull = TwoBytesFull;
        var crc = new byte[ModbusFrame.CrcSize];

        for (var i = 0; i < message.Length - ModbusFrame.CrcSize; i++)
        {
            crcFull = (ushort)(crcFull ^ message[i]); // XOR byte into least sig. byte of crc

            for (var j = 0; j < ModbusFrame.HighByteShift; j++)
            {
                var crcLsb = (byte)(crcFull & 0x0001); // check LSB (low bit)
                crcFull = (ushort)((crcFull >> 1) & 0x7FFF); // shift right 1 bit

                if (crcLsb == 1)
                    crcFull = (ushort)(crcFull ^ ModbusFrame.CrcPolynomial); // if LSB was 1, apply polynomial
            }
        }

        crc[1] = (byte)((crcFull >> ModbusFrame.HighByteShift) & 0xFF); // Crc High
        crc[0] = (byte)(crcFull & 0xFF); // Crc low

        return crc;
    }

    /// <summary>
    ///     Builds a complete Modbus RTU request frame from a <see cref="ModbusMessage" />.
    /// </summary>
    /// <param name="message">The Modbus message containing address, type, start, and register fields.</param>
    /// <param name="singleValue">An optional single value for functions FC5 and FC6.</param>
    /// <param name="values">An optional array of values for functions FC15 and FC16.</param>
    /// <returns>A byte array representing the complete Modbus RTU frame with CRC.</returns>
    public static byte[] BuildMessage(this ModbusMessage message, ushort? singleValue = null, ushort[]? values = null)
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
            buffer[ModbusFrame.StartIndexHigh] = (byte)(message.Start >> ModbusFrame.HighByteShift);
            buffer[ModbusFrame.StartIndexLow] = (byte)message.Start;
            buffer[ModbusFrame.RegisterIndexHigh] = (byte)(singleValue.Value >> ModbusFrame.HighByteShift);
            buffer[ModbusFrame.RegisterIndexLow] = (byte)singleValue.Value;
        }
        else
        {
            // Functions without data (FC1, FC3, etc.)
            buffer = new byte[ModbusFrame.MessageSize];

            buffer[ModbusFrame.AddressIndex] = message.Address;
            buffer[ModbusFrame.TypeIndex] = message.Type;
            buffer[ModbusFrame.StartIndexHigh] = (byte)(message.Start >> ModbusFrame.HighByteShift);
            buffer[ModbusFrame.StartIndexLow] = (byte)message.Start;
            buffer[ModbusFrame.RegisterIndexHigh] = (byte)(message.Register >> ModbusFrame.HighByteShift);
            buffer[ModbusFrame.RegisterIndexLow] = (byte)message.Register;
        }

        var crc = buffer.ComputeCrc();
        buffer[^2] = crc[ModbusFrame.CrcLowIndex];
        buffer[^1] = crc[ModbusFrame.CrcHighIndex];

        return buffer;
    }

    /// <summary>
    ///     Validates a Modbus RTU response by checking its CRC checksum.
    /// </summary>
    /// <param name="response">The complete response frame including the 2-byte CRC at the end.</param>
    /// <returns><c>true</c> if the CRC is valid; otherwise, <c>false</c>.</returns>
    public static bool CheckResponse(this byte[] response)
    {
        //Perform a basic CRC check:
        var crc = response.ComputeCrc();
        return crc[ModbusFrame.CrcLowIndex] == response[^2] && crc[ModbusFrame.CrcHighIndex] == response[^1];
    }
}