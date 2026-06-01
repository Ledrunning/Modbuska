using Modbuska.ModbusManager.Extension;

namespace Modbuska.UnitTest;

public class ModbusExtensionTests
{
    [Test]
    public void ComputeCrc_ShouldReturnExpectedForKnownFrame()
    {
        var message = new byte[] { 0x01, 0x03, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00 };

        var crc = message.ComputeCrc();

        Assert.That(crc[0], Is.EqualTo(0x84));
        Assert.That(crc[1], Is.EqualTo(0x0A));
    }

    [Test]
    public void BuildMessage_ForReadHoldingRegisters_ShouldBuildValidFrame()
    {
        var message = new ModbusMessageStub
        {
            Address = 0x01,
            Type = 0x03,
            Start = 0x0000,
            Register = 0x0001
        };

        var frame = message.BuildMessage();

        Assert.That(frame.Length, Is.EqualTo(Modbuska.ModbusManager.Model.ModbusMessage.MessageSize));
        Assert.That(frame[0], Is.EqualTo(0x01));
        Assert.That(frame[1], Is.EqualTo(0x03));
        Assert.That(frame[2], Is.EqualTo(0x00));
        Assert.That(frame[3], Is.EqualTo(0x00));
        Assert.That(frame[4], Is.EqualTo(0x00));
        Assert.That(frame[5], Is.EqualTo(0x01));
        Assert.That(frame[6], Is.EqualTo(0x84));
        Assert.That(frame[7], Is.EqualTo(0x0A));
    }

    [Test]
    public void BuildMessage_ForWriteSingleCoil_ShouldWriteValueAndCrc()
    {
        var message = new ModbusMessageStub
        {
            Address = 0x11,
            Type = 0x05,
            Start = 0x0013,
            Register = 0x0000
        };

        var frame = message.BuildMessage(0xFF00);

        Assert.That(frame.Length, Is.EqualTo(Modbuska.ModbusManager.Model.ModbusMessage.MessageSize));
        Assert.That(frame[0], Is.EqualTo(0x11));
        Assert.That(frame[1], Is.EqualTo(0x05));
        Assert.That(frame[2], Is.EqualTo(0x00));
        Assert.That(frame[3], Is.EqualTo(0x13));
        Assert.That(frame[4], Is.EqualTo(0xFF));
        Assert.That(frame[5], Is.EqualTo(0x00));

        Assert.That(frame.CheckResponse(), Is.True);
    }

    [Test]
    public void BuildMessage_ForWriteMultipleRegisters_ShouldBuildExpectedSize()
    {
        var message = new ModbusMessageStub
        {
            Address = 0x01,
            Type = 0x10,
            Start = 0x0001,
            Register = 0x0002
        };

        var frame = message.BuildMessage(values: [0x000A, 0x000B]);

        Assert.That(frame.Length, Is.EqualTo(13));
        Assert.That(frame[0], Is.EqualTo(0x01));
        Assert.That(frame[1], Is.EqualTo(0x10));
        Assert.That(frame[6], Is.EqualTo(0x04));
        Assert.That(frame.CheckResponse(), Is.True);
    }

    [Test]
    public void CheckResponse_ShouldReturnFalse_WhenCrcCorrupted()
    {
        var message = new ModbusMessageStub
        {
            Address = 0x01,
            Type = 0x03,
            Start = 0x0000,
            Register = 0x0001
        };

        var frame = message.BuildMessage();
        frame[^1] ^= 0xFF;

        var valid = frame.CheckResponse();

        Assert.That(valid, Is.False);
    }
}
