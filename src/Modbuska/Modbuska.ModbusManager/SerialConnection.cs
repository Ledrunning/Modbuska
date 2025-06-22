namespace Modbuska.ModbusManager;

public class SerialConnection
{
    SerialPort? serialPort;

    public SerialConnection(SerialPort? serialPort)
    {
        this.serialPort = serialPort;
    }
}