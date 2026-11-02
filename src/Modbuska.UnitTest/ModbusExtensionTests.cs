using Modbuska.ModbusManager.Constant;
using Modbuska.ModbusManager.Extension;
using Xunit;

namespace Modbuska.UnitTest;

public class ModbusExtensionTests
{
    [Fact]
    public void ComputeCrc_ShouldReturnExpectedForKnownFrame()
    {
        var message = new byte[] { 0x01, 0x03, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00 };

        var crc = message.ComputeCrc();

        Assert.Equal(0x84, crc[0]);
        Assert.Equal(0x0A, crc[1]);
    }

    [Fact]
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

        Assert.Equal(ModbusFrame.MessageSize, frame.Length);
        Assert.Equal(0x01, frame[0]);
        Assert.Equal(0x03, frame[1]);
        Assert.Equal(0x00, frame[2]);
        Assert.Equal(0x00, frame[3]);
        Assert.Equal(0x00, frame[4]);
        Assert.Equal(0x01, frame[5]);
        Assert.Equal(0x84, frame[6]);
        Assert.Equal(0x0A, frame[7]);
    }

    [Fact]
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

        Assert.Equal(ModbusFrame.MessageSize, frame.Length);
        Assert.Equal(0x11, frame[0]);
        Assert.Equal(0x05, frame[1]);
        Assert.Equal(0x00, frame[2]);
        Assert.Equal(0x13, frame[3]);
        Assert.Equal(0xFF, frame[4]);
        Assert.Equal(0x00, frame[5]);

        Assert.True(frame.CheckResponse());
    }

    [Fact]
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

        Assert.Equal(13, frame.Length);
        Assert.Equal(0x01, frame[0]);
        Assert.Equal(0x10, frame[1]);
        Assert.Equal(0x04, frame[6]);
        Assert.True(frame.CheckResponse());
    }

    [Fact]
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

        Assert.False(valid);
    }
}