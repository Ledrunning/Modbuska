using Modbuska.ModbusManager.Extension;

namespace Modbuska.ModbusManager.Service;

public class ModbusRtu
{
    private void BuildMessage(byte address, byte type, ushort start, ushort registers, ref byte[] message)
    {
        message[0] = address;
        message[1] = type;
        message[2] = (byte)(start >> 8);
        message[3] = (byte)start;
        message[4] = (byte)(registers >> 8);
        message[5] = (byte)registers;

        var crc = message.ComputeCrc();
        message[^2] = crc[0];
        message[^1] = crc[1];
    }

    private bool CheckResponse(byte[] response)
    {
        //Perform a basic CRC check:
        var crc = response.ComputeCrc();
        return crc[0] == response[^2] && crc[1] == response[^1];
    }
}