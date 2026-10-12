using Modbuska.ModbusManager.Extension;
using Modbuska.ModbusManager.Model;
using Modbuska.ModbusManager.Service;
using Xunit;

namespace Modbuska.UnitTest;

public class ModbusRtuServiceTests
{
    private static ModbusRtuService CreateService(byte[] response)
    {
        var fake = new FakeSerialConnectionService(response);
        return new ModbusRtuService(fake);
    }

    /// <summary>
    ///     Builds a complete Modbus response frame by appending CRC to the given payload.
    /// </summary>
    private static byte[] BuildResponse(params byte[] payload)
    {
        var frame = new byte[payload.Length + 2];
        Array.Copy(payload, frame, payload.Length);

        var crc = frame.ComputeCrc();
        frame[^2] = crc[ModbusFrame.CrcLowIndex];
        frame[^1] = crc[ModbusFrame.CrcHighIndex];

        return frame;
    }

    [Fact]
    public void SendFc1_ShouldParseCoilsCorrectly()
    {
        // FC1: address=0x01, function=0x01, byteCount=0x01, data=0x02 (coil 1 set)
        var response = BuildResponse(0x01, 0x01, 0x01, 0x02);
        var service = CreateService(response);
        var message = new ModbusMessageStub { Address = 0x01, Start = 0, Register = 8 };

        var (result, coils) = service.SendFc1(message);

        Assert.True(result.Success);
        Assert.Equal(8, coils.Length);
        // data = 0x02 => bit 1 set
        Assert.False(coils[0]);
        Assert.True(coils[1]);
    }

    [Fact]
    public void SendFc2_ShouldParseInputsCorrectly()
    {
        // FC2: address=0x01, function=0x02, byteCount=0x01, data=0x01 (input 0 set)
        var response = BuildResponse(0x01, 0x02, 0x01, 0x01);
        var service = CreateService(response);
        var message = new ModbusMessageStub { Address = 0x01, Start = 0, Register = 8 };

        var (result, inputs) = service.SendFc2(message);

        Assert.True(result.Success);
        Assert.Equal(8, inputs.Length);
        // data = 0x01 => bit 0 set
        Assert.True(inputs[0]);
        Assert.False(inputs[1]);
    }

    [Fact]
    public void SendFc3_ShouldParseRegistersCorrectly()
    {
        // FC3: address=0x01, function=0x03, byteCount=0x04, data=0x000A 0x000B
        var response = BuildResponse(0x01, 0x03, 0x04, 0x00, 0x0A, 0x00, 0x0B);
        var service = CreateService(response);
        var message = new ModbusMessageStub { Address = 0x01, Start = 0, Register = 2 };

        var (result, registers) = service.SendFc3(message);

        Assert.True(result.Success);
        Assert.Equal(2, registers.Length);
        Assert.Equal(0x000A, registers[0]);
        Assert.Equal(0x000B, registers[1]);
    }

    [Fact]
    public void SendFc4_ShouldParseInputRegistersCorrectly()
    {
        // FC4: address=0x01, function=0x04, byteCount=0x02, data=0x000C
        var response = BuildResponse(0x01, 0x04, 0x02, 0x00, 0x0C);
        var service = CreateService(response);
        var message = new ModbusMessageStub { Address = 0x01, Start = 0, Register = 1 };

        var (result, registers) = service.SendFc4(message);

        Assert.True(result.Success);
        Assert.Single(registers);
        Assert.Equal(0x000C, registers[0]);
    }

    [Fact]
    public void SendFc5_ShouldReturnCoilState()
    {
        // FC5: address=0x01, function=0x05, start=0x0000, value=0xFF00
        var response = BuildResponse(0x01, 0x05, 0x00, 0x00, 0xFF, 0x00);
        var service = CreateService(response);
        var message = new ModbusMessageStub { Address = 0x01, Start = 0, Register = 1 };

        var (result, coilState) = service.SendFc5(message, true);

        Assert.True(result.Success);
        Assert.True(coilState);
    }

    [Fact]
    public void SendFc6_ShouldReturnRegisterValue()
    {
        // FC6: address=0x01, function=0x06, start=0x0000, value=0x0010
        var response = BuildResponse(0x01, 0x06, 0x00, 0x00, 0x00, 0x10);
        var service = CreateService(response);
        var message = new ModbusMessageStub { Address = 0x01, Start = 0, Register = 1 };

        var (result, registerValue) = service.SendFc6(message, 0x0010);

        Assert.True(result.Success);
        Assert.Equal(0x0010, registerValue);
    }

    [Fact]
    public void SendFc15_ShouldSucceed()
    {
        // FC15: address=0x01, function=0x0F, start=0x0000, count=0x0008
        var response = BuildResponse(0x01, 0x0F, 0x00, 0x00, 0x00, 0x08);
        var service = CreateService(response);
        var message = new ModbusMessageStub { Address = 0x01, Start = 0, Register = 8 };

        var result = service.SendFc15(message, [true, false, false, false, false, false, false, false]);

        Assert.True(result.Success);
    }

    [Fact]
    public void SendFc16_ShouldSucceed()
    {
        // FC16: address=0x01, function=0x10, start=0x0000, count=0x0002
        var response = BuildResponse(0x01, 0x10, 0x00, 0x00, 0x00, 0x02);
        var service = CreateService(response);
        var message = new ModbusMessageStub { Address = 0x01, Start = 0, Register = 2 };

        var result = service.SendFc16(message, [0x000A, 0x000B]);

        Assert.True(result.Success);
    }

    [Fact]
    public void SendFc1_ShouldFail_WhenPortClosed()
    {
        var fake = new FakeSerialConnectionService([0x00]) { IsSerialPortOpen = false };
        var service = new ModbusRtuService(fake);
        var message = new ModbusMessageStub { Address = 0x01, Start = 0, Register = 8 };

        var (result, _) = service.SendFc1(message);

        Assert.False(result.Success);
        Assert.Contains("Serial port is closed", result.ErrorMessage);
    }

    [Fact]
    public void SendFc3_ShouldFail_WhenCrcInvalid()
    {
        var response = BuildResponse(0x01, 0x03, 0x04, 0x00, 0x0A, 0x00, 0x0B);
        response[^1] ^= 0xFF; // corrupt CRC
        var service = CreateService(response);
        var message = new ModbusMessageStub { Address = 0x01, Start = 0, Register = 2 };

        var (result, _) = service.SendFc3(message);

        Assert.False(result.Success);
        Assert.Contains("Invalid CRC", result.ErrorMessage);
    }

    [Fact]
    public void SendFc3_ShouldFail_WhenAddressMismatch()
    {
        var response = BuildResponse(0x02, 0x03, 0x04, 0x00, 0x0A, 0x00, 0x0B); // wrong address
        var service = CreateService(response);
        var message = new ModbusMessageStub { Address = 0x01, Start = 0, Register = 2 };

        var (result, _) = service.SendFc3(message);

        Assert.False(result.Success);
        Assert.Contains("Unexpected slave address", result.ErrorMessage);
    }

    [Fact]
    public void SendFc1_ShouldFail_WhenModbusException()
    {
        // Exception response: address=0x01, function=0x81 (0x01 | 0x80), code=0x02
        var response = BuildResponse(0x01, 0x81, 0x02);
        var service = CreateService(response);
        var message = new ModbusMessageStub { Address = 0x01, Start = 0, Register = 8 };

        var (result, _) = service.SendFc1(message);

        Assert.False(result.Success);
        Assert.Contains("Modbus exception response", result.ErrorMessage);
    }

    [Fact]
    public void SendFc3_ShouldFail_WhenByteCountMismatch()
    {
        // FC3: address=0x01, function=0x03, byteCount=0x02 (wrong, should be 0x04 for 2 registers)
        // Build the response with the wrong byte count from the start so CRC is valid
        // Response: [addr=0x01, func=0x03, byteCount=0x02, data=0x000A, CRC]
        var response = BuildResponse(0x01, 0x03, 0x02, 0x00, 0x0A);

        var service = CreateService(response);
        var message = new ModbusMessageStub { Address = 0x01, Start = 0, Register = 2 };

        var (result, _) = service.SendFc3(message);

        Assert.False(result.Success);
        Assert.Contains("Mismatch in expected register data size", result.ErrorMessage);
    }
}