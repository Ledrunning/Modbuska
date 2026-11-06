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

        Assert.Equal(0x84, crc[ModbusFrame.CrcLowIndex]);
        Assert.Equal(0x0A, crc[ModbusFrame.CrcHighIndex]);
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

        Assert.Equal(0x01, frame[ModbusFrame.AddressIndex]);
        Assert.Equal(0x03, frame[ModbusFrame.TypeIndex]);
        Assert.Equal(0x00, frame[ModbusFrame.StartIndexHigh]);
        Assert.Equal(0x00, frame[ModbusFrame.StartIndexLow]);
        Assert.Equal(0x00, frame[ModbusFrame.RegisterIndexHigh]);
        Assert.Equal(0x01, frame[ModbusFrame.RegisterIndexLow]);

        var frameToCheck = (byte[])frame.Clone();
        frameToCheck[^2] = 0;
        frameToCheck[^1] = 0;

        var expectedCrc = frameToCheck.ComputeCrc();

        Assert.Equal(expectedCrc[ModbusFrame.CrcLowIndex], frame[^2]);
        Assert.Equal(expectedCrc[ModbusFrame.CrcHighIndex], frame[^1]);
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
        Assert.Equal(0x11, frame[ModbusFrame.AddressIndex]); 
        Assert.Equal(0x05, frame[ModbusFrame.TypeIndex]);    
        Assert.Equal(0x00, frame[ModbusFrame.StartIndexHigh]); 
        Assert.Equal(0x13, frame[ModbusFrame.StartIndexLow]);  
        Assert.Equal(0xFF, frame[ModbusFrame.RegisterIndexHigh]); 
        Assert.Equal(0x00, frame[ModbusFrame.RegisterIndexLow]);  

        var frameToCheck = (byte[])frame.Clone();
        frameToCheck[^2] = 0;
        frameToCheck[^1] = 0;

        var expectedCrc = frameToCheck.ComputeCrc();

        Assert.Equal(expectedCrc[ModbusFrame.CrcLowIndex], frame[^2]);
        Assert.Equal(expectedCrc[ModbusFrame.CrcHighIndex], frame[^1]);
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

        // Check the total length (7-byte header + 4 bytes of data (2 registers) + 2 CRC = 13)
        Assert.Equal(13, frame.Length);

        Assert.Equal(0x01, frame[ModbusFrame.AddressIndex]);
        Assert.Equal(0x10, frame[ModbusFrame.TypeIndex]);
        Assert.Equal(0x04, frame[ModbusFrame.WriteRequestByteCountIndex]);

        Assert.Equal(0x00, frame[7]); 
        Assert.Equal(0x0A, frame[8]);
        Assert.Equal(0x00, frame[9]); 
        Assert.Equal(0x0B, frame[10]); 

        var frameToCheck = (byte[])frame.Clone();
        frameToCheck[^2] = 0;
        frameToCheck[^1] = 0;

        var expectedCrc = frameToCheck.ComputeCrc();

        Assert.Equal(expectedCrc[ModbusFrame.CrcLowIndex], frame[^2]);
        Assert.Equal(expectedCrc[ModbusFrame.CrcHighIndex], frame[^1]);
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