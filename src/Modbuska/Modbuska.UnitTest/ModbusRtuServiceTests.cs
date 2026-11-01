using Modbuska.ModbusManager.Constant;
using Modbuska.ModbusManager.Extension;
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
    public void SendFc5_ShouldFail_WhenWriteEchoMismatch()
    {
        // Valid CRC, correct address/function, but wrong echo value (0x0000 instead of 0xFF00)
        var response = BuildResponse(0x01, 0x05, 0x00, 0x00, 0x00, 0x00);
        var service = CreateService(response);
        var message = new ModbusMessageStub { Address = 0x01, Start = 0, Register = 1 };

        var (result, coilState) = service.SendFc5(message, true);

        Assert.False(result.Success);
        Assert.Contains("echo mismatch", result.ErrorMessage);
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
    public void SendFc6_ShouldFail_WhenWriteEchoMismatch()
    {
        // Valid CRC, correct address/function, but wrong echo value (0x0000 instead of 0x0010)
        var response = BuildResponse(0x01, 0x06, 0x00, 0x00, 0x00, 0x00);
        var service = CreateService(response);
        var message = new ModbusMessageStub { Address = 0x01, Start = 0, Register = 1 };

        var (result, registerValue) = service.SendFc6(message, 0x0010);

        Assert.False(result.Success);
        Assert.Contains("echo mismatch", result.ErrorMessage);
    }

    [Fact]
    public void SendFc5_ShouldFail_WhenAddressIsInvalid()
    {
        var service = CreateService(Array.Empty<byte>());
        var message = new ModbusMessageStub { Address = 248, Start = 0, Register = 1 };

        var (result, coilState) = service.SendFc5(message, true);

        Assert.False(result.Success);
        Assert.Contains("Slave address must be between", result.ErrorMessage);
    }

    [Fact]
    public void SendFc5_ShouldSucceed_WhenAddressIsBroadcast()
    {
        var service = CreateService(Array.Empty<byte>());
        var message = new ModbusMessageStub { Address = 0, Start = 0, Register = 1 };

        var (result, coilState) = service.SendFc5(message, true);

        // Broadcast write: no response expected, value is not confirmed
        Assert.True(result.Success);
        Assert.False(coilState); // default, not confirmed
    }

    [Fact]
    public void SendFc6_ShouldFail_WhenAddressIsInvalid()
    {
        var service = CreateService(Array.Empty<byte>());
        var message = new ModbusMessageStub { Address = 248, Start = 0, Register = 1 };

        var (result, registerValue) = service.SendFc6(message, 0x0010);

        Assert.False(result.Success);
        Assert.Contains("Slave address must be between", result.ErrorMessage);
    }

    [Fact]
    public void SendFc6_ShouldSucceed_WhenAddressIsBroadcast()
    {
        var service = CreateService(Array.Empty<byte>());
        var message = new ModbusMessageStub { Address = 0, Start = 0, Register = 1 };

        var (result, registerValue) = service.SendFc6(message, 0x0010);

        // Broadcast write: no response expected, value is not confirmed
        Assert.True(result.Success);
        Assert.Equal(0, registerValue); // default, not confirmed
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
    public void SendFc1_ShouldFail_WhenReadTimeout()
    {
        var fake = new FakeSerialConnectionService(new byte[] { 0x01, 0x01 }) { IsSerialPortOpen = true };
        var service = new ModbusRtuService(fake);
        var message = new ModbusMessageStub { Address = 0x01, Start = 0, Register = 8 };

        var (result, _) = service.SendFc1(message);

        Assert.False(result.Success);
        Assert.Contains("timeout", result.ErrorMessage);
    }

    [Fact]
    public void SendFc3_ShouldFail_WhenReadTimeout()
    {
        var fake = new FakeSerialConnectionService(new byte[] { 0x01, 0x03 }) { IsSerialPortOpen = true };
        var service = new ModbusRtuService(fake);
        var message = new ModbusMessageStub { Address = 0x01, Start = 0, Register = 2 };

        var (result, _) = service.SendFc3(message);

        Assert.False(result.Success);
        Assert.Contains("timeout", result.ErrorMessage);
    }

    [Fact]
    public void SendFc15_ShouldVerifyEcho()
    {
        var response = BuildResponse(0x01, 0x0F, 0x00, 0x00, 0x00, 0x08);
        var service = CreateService(response);
        var message = new ModbusMessageStub { Address = 0x01, Start = 0, Register = 8 };

        var values = new[] { true, false, false, false, false, false, false, false };
        var result = service.SendFc15(message, values);

        Assert.True(result.Success);
    }

    [Fact]
    public void SendFc15_ShouldFail_WhenReadTimeout()
    {
        var fake = new FakeSerialConnectionService(new byte[] { 0x01, 0x0F }) { IsSerialPortOpen = true };
        var service = new ModbusRtuService(fake);
        var message = new ModbusMessageStub { Address = 0x01, Start = 0, Register = 8 };

        var values = new[] { true, false, false, false, false, false, false, false };
        var result = service.SendFc15(message, values);

        Assert.False(result.Success);
        Assert.Contains("timeout", result.ErrorMessage);
    }

    [Fact]
    public void SendFc16_ShouldVerifyEcho()
    {
        var response = BuildResponse(0x01, 0x10, 0x00, 0x00, 0x00, 0x02);
        var service = CreateService(response);
        var message = new ModbusMessageStub { Address = 0x01, Start = 0, Register = 2 };

        var values = new ushort[] { 0x000A, 0x000B };
        var result = service.SendFc16(message, values);

        Assert.True(result.Success);
    }

    [Fact]
    public void SendFc16_ShouldFail_WhenReadTimeout()
    {
        var fake = new FakeSerialConnectionService(new byte[] { 0x01, 0x10 }) { IsSerialPortOpen = true };
        var service = new ModbusRtuService(fake);
        var message = new ModbusMessageStub { Address = 0x01, Start = 0, Register = 2 };

        var values = new ushort[] { 0x000A, 0x000B };
        var result = service.SendFc16(message, values);

        Assert.False(result.Success);
        Assert.Contains("timeout", result.ErrorMessage);
    }

    [Fact]
    public void SendFc5_ShouldFail_WhenExceptionResponse()
    {
        var response = BuildResponse(0x01, 0x85, 0x02); // FC5 exception: code 0x02
        var service = CreateService(response);
        var message = new ModbusMessageStub { Address = 0x01, Start = 0, Register = 1 };

        var (result, coilState) = service.SendFc5(message, true);

        Assert.False(result.Success);
        Assert.Contains("Modbus exception response", result.ErrorMessage);
    }

    [Fact]
    public void SendFc15_ShouldFail_WhenExceptionResponse()
    {
        var response = BuildResponse(0x01, 0x8F, 0x02); // FC15 exception: code 0x02
        var service = CreateService(response);
        var message = new ModbusMessageStub { Address = 0x01, Start = 0, Register = 8 };

        var values = new[] { true, false, false, false, false, false, false, false };
        var result = service.SendFc15(message, values);

        Assert.False(result.Success);
        Assert.Contains("Modbus exception response", result.ErrorMessage);
    }

    [Fact]
    public void SendFc1_ShouldFail_WhenRegisterExceedsMax()
    {
        var message = new ModbusMessageStub { Address = 0x01, Start = 0, Register = 3000 };
        var service = CreateService(new byte[0]);
        var (result, _) = service.SendFc1(message);

        Assert.False(result.Success);
        Assert.Contains($"exceeds maximum of {ModbusFrame.MaxReadCoils}", result.ErrorMessage);
    }

    [Fact]
    public void SendFc3_ShouldFail_WhenRegisterExceedsMax()
    {
        var message = new ModbusMessageStub { Address = 0x01, Start = 0, Register = 200 };
        var service = CreateService(new byte[0]);
        var (result, _) = service.SendFc3(message);

        Assert.False(result.Success);
        Assert.Contains($"exceeds maximum of {ModbusFrame.MaxReadHoldingRegisters}", result.ErrorMessage);
    }

    [Fact]
    public void SendFc15_ShouldFail_WhenCoilCountExceedsMax()
    {
        var message = new ModbusMessageStub { Address = 0x01, Start = 0, Register = 3000 };
        var service = CreateService(new byte[0]);
        var result = service.SendFc15(message, new bool[3000]);

        Assert.False(result.Success);
        Assert.Contains($"exceeds maximum of {ModbusFrame.MaxWriteMultipleCoils}", result.ErrorMessage);
    }

    [Fact]
    public void SendFc16_ShouldFail_WhenRegisterExceedsMax()
    {
        var message = new ModbusMessageStub { Address = 0x01, Start = 0, Register = 200 };
        var service = CreateService(new byte[0]);
        var result = service.SendFc16(message, new ushort[200]);

        Assert.False(result.Success);
        Assert.Contains($"exceeds maximum of {ModbusFrame.MaxWriteMultipleRegisters}", result.ErrorMessage);
    }

    [Fact]
    public void SendFc16_ShouldFail_WhenExceptionResponse()
    {
        var response = BuildResponse(0x01, 0x90, 0x02); // FC16 exception: code 0x02
        var service = CreateService(response);
        var message = new ModbusMessageStub { Address = 0x01, Start = 0, Register = 2 };
        var values = new ushort[] { 0x000A, 0x000B };
        var result = service.SendFc16(message, values);
        Assert.False(result.Success);
        Assert.Contains("Modbus exception response", result.ErrorMessage);
    }

    [Fact]
    public void SendFc16_ShouldWrite_CorrectRequestBytes()
    {
        var fake = new FakeSerialConnectionService(new byte[0]) { IsSerialPortOpen = true };
        var service = new ModbusRtuService(fake);
        var message = new ModbusMessageStub { Address = 0x01, Start = 0, Register = 2 };
        var values = new ushort[] { 0x000A, 0x000B };

        // Build expected request frame: address + FC16 + start(2 bytes) + quantity(2 bytes) + byte count + data + 2 CRC placeholder bytes
        // ComputeCrc iterates buffer.Length - 2 bytes, so include 2 zeroed CRC placeholders to match BuildMessage behavior
        var expectedRequest = new byte[13]
        {
            0x01, // Address
            (byte)ModbusFunctionType.WriteMultipleRegisters, // Function 0x10
            0x00, 0x00, // Start address = 0
            0x00, 0x02, // Quantity = 2 registers
            0x04, // Byte count = 4 bytes
            0x00, 0x0A, // Register value 0
            0x00, 0x0B, // Register value 1
            0x00, 0x00 // CRC placeholders (zeroed)
        };
        var requestCrc = expectedRequest.ComputeCrc(); // CRC over first 11 data bytes
        var expectedFrame = new byte[expectedRequest.Length];
        Array.Copy(expectedRequest, expectedFrame, expectedRequest.Length);
        expectedFrame[^2] = requestCrc[ModbusFrame.CrcLowIndex];
        expectedFrame[^1] = requestCrc[ModbusFrame.CrcHighIndex];

        // Build a valid echo response: first 6 bytes match request header, then CRC
        var echoResponse = new byte[8];
        Array.Copy(expectedRequest, echoResponse,
            ModbusFrame.WriteResponseHeaderSize + ModbusFrame.WriteResponsePayloadSize - ModbusFrame.CrcSize);
        var echoCrc = echoResponse.ComputeCrc();
        echoResponse[6] = echoCrc[ModbusFrame.CrcLowIndex];
        echoResponse[7] = echoCrc[ModbusFrame.CrcHighIndex];

        fake = new FakeSerialConnectionService(echoResponse) { IsSerialPortOpen = true };
        service = new ModbusRtuService(fake);

        var result = service.SendFc16(message, values);

        Assert.True(result.Success);
        Assert.Single(fake.SentBytes);
        Assert.Equal(expectedFrame, fake.LastSentBytes);
    }

    [Fact]
    public void SendFc15_ShouldWrite_CorrectRequestBytes()
    {
        var fake = new FakeSerialConnectionService(new byte[0]) { IsSerialPortOpen = true };
        var service = new ModbusRtuService(fake);
        var message = new ModbusMessageStub { Address = 0x01, Start = 0, Register = 8 };
        var values = new[] { true, false, true, false, true, false, true, false };

        // Build expected request frame for FC15:
        // address + FC15 + start(2 bytes) + quantity(2 bytes) + byte count + coil data bytes + 2 CRC placeholder bytes
        // ComputeCrc iterates buffer.Length - 2 bytes, so include 2 zeroed CRC placeholders to match service behavior
        var expectedRequest = new byte[10]
        {
            0x01, // Address
            (byte)ModbusFunctionType.WriteMultipleCoils, // Function 0x0F
            0x00, 0x00, // Start address = 0
            0x00, 0x08, // Quantity = 8 coils
            0x01, // Byte count = 1 byte
            0x55, // Coil data: bits 0,2,4,6 set => 0x55
            0x00, 0x00 // CRC placeholders (zeroed)
        };
        var requestCrc = expectedRequest.ComputeCrc(); // CRC over first 8 data bytes
        var expectedFrame = new byte[expectedRequest.Length];
        Array.Copy(expectedRequest, expectedFrame, expectedRequest.Length);
        expectedFrame[^2] = requestCrc[ModbusFrame.CrcLowIndex];
        expectedFrame[^1] = requestCrc[ModbusFrame.CrcHighIndex];
        var echoResponse = new byte[8];
        Array.Copy(expectedRequest, echoResponse,
            ModbusFrame.WriteResponseHeaderSize + ModbusFrame.WriteResponsePayloadSize - ModbusFrame.CrcSize);
        var echoCrc = echoResponse.ComputeCrc();
        echoResponse[6] = echoCrc[ModbusFrame.CrcLowIndex];
        echoResponse[7] = echoCrc[ModbusFrame.CrcHighIndex];

        fake = new FakeSerialConnectionService(echoResponse) { IsSerialPortOpen = true };
        service = new ModbusRtuService(fake);

        var result = service.SendFc15(message, values);

        Assert.True(result.Success);
        Assert.Single(fake.SentBytes);
        Assert.Equal(expectedFrame, fake.LastSentBytes);
    }

    [Fact]
    public void SendFc15_ShouldFail_WhenWriteEchoMismatch()
    {
        // Valid CRC and function, but wrong echoed quantity (0x09 instead of 0x08)
        var response = BuildResponse(0x01, 0x0F, 0x00, 0x00, 0x00, 0x09);
        var service = CreateService(response);
        var message = new ModbusMessageStub { Address = 0x01, Start = 0, Register = 8 };

        var values = new[] { true, false, false, false, false, false, false, false };
        var result = service.SendFc15(message, values);

        Assert.False(result.Success);
        Assert.Contains("echo mismatch", result.ErrorMessage);
    }

    [Fact]
    public void SendFc16_ShouldFail_WhenWriteEchoMismatch()
    {
        // Valid CRC and function, but wrong echoed quantity (0x03 instead of 0x02)
        var response = BuildResponse(0x01, 0x10, 0x00, 0x00, 0x00, 0x03);
        var service = CreateService(response);
        var message = new ModbusMessageStub { Address = 0x01, Start = 0, Register = 2 };

        var values = new ushort[] { 0x000A, 0x000B };
        var result = service.SendFc16(message, values);

        Assert.False(result.Success);
        Assert.Contains("echo mismatch", result.ErrorMessage);
    }

    [Fact]
    public void SendFc15_ShouldFail_WhenCoilCountIsZero()
    {
        var service = CreateService(Array.Empty<byte>());
        var message = new ModbusMessageStub { Address = 0x01, Start = 0, Register = 0 };

        var result = service.SendFc15(message, Array.Empty<bool>());

        Assert.False(result.Success);
        Assert.Contains("Coil count must be greater than 0", result.ErrorMessage);
    }

    [Fact]
    public void SendFc16_ShouldFail_WhenRegisterCountIsZero()
    {
        var service = CreateService(Array.Empty<byte>());
        var message = new ModbusMessageStub { Address = 0x01, Start = 0, Register = 0 };

        var result = service.SendFc16(message, Array.Empty<ushort>());

        Assert.False(result.Success);
        Assert.Contains("Register count must be greater than 0", result.ErrorMessage);
    }

    [Fact]
    public void SendFc15_ShouldFail_WhenAddressIsInvalid()
    {
        var service = CreateService(Array.Empty<byte>());
        var message = new ModbusMessageStub { Address = 248, Start = 0, Register = 8 };

        var result = service.SendFc15(message, new bool[8]);

        Assert.False(result.Success);
        Assert.Contains("Slave address must be between", result.ErrorMessage);
    }

    [Fact]
    public void SendFc16_ShouldFail_WhenAddressIsInvalid()
    {
        var service = CreateService(Array.Empty<byte>());
        var message = new ModbusMessageStub { Address = 248, Start = 0, Register = 2 };

        var result = service.SendFc16(message, new ushort[] { 0x000A, 0x000B });

        Assert.False(result.Success);
        Assert.Contains("Slave address must be between", result.ErrorMessage);
    }

    [Fact]
    public void SendFc15_ShouldSucceed_WhenAddressIsBroadcast()
    {
        // Broadcast write: no response expected, nothing is read
        var service = CreateService(Array.Empty<byte>());
        var message = new ModbusMessageStub { Address = 0, Start = 0, Register = 8 };

        var result = service.SendFc15(message, new bool[8]);

        Assert.True(result.Success);
    }

    [Fact]
    public void SendFc16_ShouldSucceed_WhenAddressIsBroadcast()
    {
        // Broadcast write: no response expected, nothing is read
        var service = CreateService(Array.Empty<byte>());
        var message = new ModbusMessageStub { Address = 0, Start = 0, Register = 2 };

        var result = service.SendFc16(message, new ushort[] { 0x000A, 0x000B });

        Assert.True(result.Success);
    }

    [Fact]
    public void SendFc1_ShouldFail_WhenAddressIsBroadcast()
    {
        var service = CreateService(Array.Empty<byte>());
        var message = new ModbusMessageStub { Address = 0, Start = 0, Register = 8 };

        var (result, _) = service.SendFc1(message);

        Assert.False(result.Success);
        Assert.Contains("Read operations are not supported", result.ErrorMessage);
    }

    [Fact]
    public void SendFc3_ShouldFail_WhenAddressIsBroadcast()
    {
        var service = CreateService(Array.Empty<byte>());
        var message = new ModbusMessageStub { Address = 0, Start = 0, Register = 2 };

        var (result, _) = service.SendFc3(message);

        Assert.False(result.Success);
        Assert.Contains("Read operations are not supported", result.ErrorMessage);
    }

    [Fact]
    public void SendFc16_ShouldFail_WhenWriteTimeout()
    {
        var fake = new FakeSerialConnectionService(new byte[0]) { IsSerialPortOpen = true, WriteTimeout = true };
        var service = new ModbusRtuService(fake);
        var message = new ModbusMessageStub { Address = 0x01, Start = 0, Register = 2 };
        var values = new ushort[] { 0x000A, 0x000B };

        var result = service.SendFc16(message, values);

        Assert.False(result.Success);
        Assert.Contains("Write timeout", result.ErrorMessage);
    }

    [Fact]
    public void SendFc15_ShouldFail_WhenWriteTimeout()
    {
        var fake = new FakeSerialConnectionService(new byte[0]) { IsSerialPortOpen = true, WriteTimeout = true };
        var service = new ModbusRtuService(fake);
        var message = new ModbusMessageStub { Address = 0x01, Start = 0, Register = 8 };
        var values = new[] { true, false, true, false, true, false, true, false };

        var result = service.SendFc15(message, values);

        Assert.False(result.Success);
        Assert.Contains("Write timeout", result.ErrorMessage);
    }
}