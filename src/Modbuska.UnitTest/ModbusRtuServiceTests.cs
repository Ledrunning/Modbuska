using Modbuska.ModbusManager.Constant;
using Modbuska.ModbusManager.Extension;
using Modbuska.ModbusManager.Service;
using Xunit;

namespace Modbuska.UnitTest;

public sealed class ModbusRtuServiceTests
{
    private static ModbusRtuService CreateService(byte[] response)
    {
        return new ModbusRtuService(new FakeSerialConnectionService(response));
    }

    private static (ModbusRtuService Service, FakeSerialConnectionService Fake) CreateWithFake(byte[] response)
    {
        var fake = new FakeSerialConnectionService(response);
        return (new ModbusRtuService(fake), fake);
    }

    private static ModbusMessageStub Msg(byte address, ushort start, ushort count)
    {
        return new ModbusMessageStub { Address = address, Start = start, Register = count };
    }

    /// <summary>
    ///     Appends a CRC to the given payload. Used both for responses and for expected request frames.
    ///     The CRC algorithm itself is verified independently against known vectors
    ///     (see <see cref="ComputeCrc_ShouldMatchKnownVector" /> and the *_ShouldSendKnownFrame tests).
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
    public void ComputeCrc_ShouldMatchKnownVector()
    {
        // Well-known Modbus RTU frame: 01 03 00 00 00 0A -> CRC C5 CD (low byte first)
        byte[] frame = [0x01, 0x03, 0x00, 0x00, 0x00, 0x0A, 0x00, 0x00];

        var crc = frame.ComputeCrc();

        Assert.Equal((byte)0xC5, crc[ModbusFrame.CrcLowIndex]);
        Assert.Equal((byte)0xCD, crc[ModbusFrame.CrcHighIndex]);
    }

    [Fact]
    public void SendFc1_ShouldParseCoilsCorrectly()
    {
        // data = 0x02 => coil 1 set
        var service = CreateService(BuildResponse(0x01, 0x01, 0x01, 0x02));

        var (result, coils) = service.SendFc1(Msg(0x01, 0, 8));

        Assert.True(result.Success);
        Assert.Equal(8, coils.Length);
        Assert.False(coils[0]);
        Assert.True(coils[1]);
    }

    [Fact]
    public void SendFc2_ShouldParseInputsCorrectly()
    {
        // data = 0x01 => input 0 set
        var service = CreateService(BuildResponse(0x01, 0x02, 0x01, 0x01));

        var (result, inputs) = service.SendFc2(Msg(0x01, 0, 8));

        Assert.True(result.Success);
        Assert.Equal(8, inputs.Length);
        Assert.True(inputs[0]);
        Assert.False(inputs[1]);
    }

    [Fact]
    public void SendFc3_ShouldParseRegistersCorrectly()
    {
        var service = CreateService(BuildResponse(0x01, 0x03, 0x04, 0x00, 0x0A, 0x00, 0x0B));

        var (result, registers) = service.SendFc3(Msg(0x01, 0, 2));

        Assert.True(result.Success);
        Assert.Equal(2, registers.Length);
        Assert.Equal(0x000A, registers[0]);
        Assert.Equal(0x000B, registers[1]);
    }

    [Fact]
    public void SendFc3_ShouldReturnUnsignedValues()
    {
        // 0xFFFF must not become -1 (registers are unsigned 16-bit)
        var service = CreateService(BuildResponse(0x01, 0x03, 0x02, 0xFF, 0xFF));

        var (result, registers) = service.SendFc3(Msg(0x01, 0, 1));

        Assert.True(result.Success);
        Assert.Equal(ushort.MaxValue, registers[0]);
    }

    [Fact]
    public void SendFc4_ShouldParseInputRegistersCorrectly()
    {
        var service = CreateService(BuildResponse(0x01, 0x04, 0x02, 0x00, 0x0C));

        var (result, registers) = service.SendFc4(Msg(0x01, 0, 1));

        Assert.True(result.Success);
        Assert.Single(registers);
        Assert.Equal(0x000C, registers[0]);
    }

    [Fact]
    public void SendFc5_ShouldReturnCoilState_WhenOn()
    {
        var service = CreateService(BuildResponse(0x01, 0x05, 0x00, 0x00, 0xFF, 0x00));

        var (result, coilState) = service.SendFc5(Msg(0x01, 0, 1), true);

        Assert.True(result.Success);
        Assert.True(coilState);
    }

    [Fact]
    public void SendFc5_ShouldReturnCoilState_WhenOff()
    {
        var service = CreateService(BuildResponse(0x01, 0x05, 0x00, 0x00, 0x00, 0x00));

        var (result, coilState) = service.SendFc5(Msg(0x01, 0, 1), false);

        Assert.True(result.Success);
        Assert.False(coilState);
    }

    [Fact]
    public void SendFc6_ShouldReturnRegisterValue()
    {
        var service = CreateService(BuildResponse(0x01, 0x06, 0x00, 0x00, 0x00, 0x10));

        var (result, registerValue) = service.SendFc6(Msg(0x01, 0, 1), 0x0010);

        Assert.True(result.Success);
        Assert.Equal(0x0010, registerValue);
    }

    [Fact]
    public void SendFc15_ShouldSucceed()
    {
        var service = CreateService(BuildResponse(0x01, 0x0F, 0x00, 0x00, 0x00, 0x08));

        var result = service.SendFc15(Msg(0x01, 0, 8), [true, false, false, false, false, false, false, false]);

        Assert.True(result.Success);
    }

    [Fact]
    public void SendFc16_ShouldSucceed()
    {
        var service = CreateService(BuildResponse(0x01, 0x10, 0x00, 0x00, 0x00, 0x02));

        var result = service.SendFc16(Msg(0x01, 0, 2), [0x000A, 0x000B]);

        Assert.True(result.Success);
    }

    [Fact]
    public void SendFc3_ShouldSendKnownFrame()
    {
        // Read 1 holding register, slave 1, address 0: 01 03 00 00 00 01 84 0A (known vector)
        var (service, fake) = CreateWithFake(BuildResponse(0x01, 0x03, 0x02, 0x00, 0x2A));

        var (result, _) = service.SendFc3(Msg(0x01, 0, 1));

        Assert.True(result.Success);
        Assert.Single(fake.SentBytes);
        Assert.Equal(new byte[] { 0x01, 0x03, 0x00, 0x00, 0x00, 0x01, 0x84, 0x0A }, fake.LastSentBytes);
    }

    [Fact]
    public void SendFc5_ShouldSendKnownFrame()
    {
        // Coil 0 ON, slave 1: 01 05 00 00 FF 00 8C 3A (known vector)
        var (service, fake) = CreateWithFake(BuildResponse(0x01, 0x05, 0x00, 0x00, 0xFF, 0x00));

        var (result, _) = service.SendFc5(Msg(0x01, 0, 1), true);

        Assert.True(result.Success);
        Assert.Single(fake.SentBytes);
        Assert.Equal(new byte[] { 0x01, 0x05, 0x00, 0x00, 0xFF, 0x00, 0x8C, 0x3A }, fake.LastSentBytes);
    }

    [Fact]
    public void SendFc6_ShouldSendKnownFrame()
    {
        // Register 1 = 3, slave 1: 01 06 00 01 00 03 98 0B (known vector)
        var (service, fake) = CreateWithFake(BuildResponse(0x01, 0x06, 0x00, 0x01, 0x00, 0x03));

        var (result, _) = service.SendFc6(Msg(0x01, 1, 1), 3);

        Assert.True(result.Success);
        Assert.Single(fake.SentBytes);
        Assert.Equal(new byte[] { 0x01, 0x06, 0x00, 0x01, 0x00, 0x03, 0x98, 0x0B }, fake.LastSentBytes);
    }

    [Fact]
    public void SendFc15_ShouldSendCorrectRequestBytes()
    {
        // Coils 0,2,4,6 set => data byte 0x55
        var (service, fake) = CreateWithFake(BuildResponse(0x01, 0x0F, 0x00, 0x00, 0x00, 0x08));
        var expected = BuildResponse(0x01, 0x0F, 0x00, 0x00, 0x00, 0x08, 0x01, 0x55);

        var result = service.SendFc15(Msg(0x01, 0, 8), [true, false, true, false, true, false, true, false]);

        Assert.True(result.Success);
        Assert.Single(fake.SentBytes);
        Assert.Equal(expected, fake.LastSentBytes);
    }

    [Fact]
    public void SendFc15_ShouldSendCorrectRequestBytes_WhenCountIsNotMultipleOfEight()
    {
        // 10 coils => 2 data bytes: coils 0 and 9 set => 0x01, 0x02
        var (service, fake) = CreateWithFake(BuildResponse(0x01, 0x0F, 0x00, 0x00, 0x00, 0x0A));
        var expected = BuildResponse(0x01, 0x0F, 0x00, 0x00, 0x00, 0x0A, 0x02, 0x01, 0x02);

        var values = new bool[10];
        values[0] = true;
        values[9] = true;
        var result = service.SendFc15(Msg(0x01, 0, 10), values);

        Assert.True(result.Success);
        Assert.Equal(expected, fake.LastSentBytes);
    }

    [Fact]
    public void SendFc16_ShouldSendCorrectRequestBytes()
    {
        var (service, fake) = CreateWithFake(BuildResponse(0x01, 0x10, 0x00, 0x00, 0x00, 0x02));
        var expected = BuildResponse(0x01, 0x10, 0x00, 0x00, 0x00, 0x02, 0x04, 0x00, 0x0A, 0x00, 0x0B);

        var result = service.SendFc16(Msg(0x01, 0, 2), [0x000A, 0x000B]);

        Assert.True(result.Success);
        Assert.Single(fake.SentBytes);
        Assert.Equal(expected, fake.LastSentBytes);
    }

    [Fact]
    public void SendFc5_ShouldFail_WhenWriteEchoMismatch()
    {
        // wrong echoed value (0x0000 instead of 0xFF00)
        var service = CreateService(BuildResponse(0x01, 0x05, 0x00, 0x00, 0x00, 0x00));

        var (result, _) = service.SendFc5(Msg(0x01, 0, 1), true);

        Assert.False(result.Success);
        Assert.Contains("echo mismatch", result.ErrorMessage);
    }

    [Fact]
    public void SendFc6_ShouldFail_WhenWriteEchoValueMismatch()
    {
        var service = CreateService(BuildResponse(0x01, 0x06, 0x00, 0x00, 0x00, 0x00));

        var (result, _) = service.SendFc6(Msg(0x01, 0, 1), 0x0010);

        Assert.False(result.Success);
        Assert.Contains("echo mismatch", result.ErrorMessage);
    }

    [Fact]
    public void SendFc6_ShouldFail_WhenWriteEchoAddressMismatch()
    {
        // correct value, but echoed register address 1 instead of 0
        var service = CreateService(BuildResponse(0x01, 0x06, 0x00, 0x01, 0x00, 0x10));

        var (result, _) = service.SendFc6(Msg(0x01, 0, 1), 0x0010);

        Assert.False(result.Success);
        Assert.Contains("echo mismatch", result.ErrorMessage);
    }

    [Fact]
    public void SendFc15_ShouldFail_WhenWriteEchoMismatch()
    {
        // wrong echoed quantity (9 instead of 8)
        var service = CreateService(BuildResponse(0x01, 0x0F, 0x00, 0x00, 0x00, 0x09));

        var result = service.SendFc15(Msg(0x01, 0, 8), [true, false, false, false, false, false, false, false]);

        Assert.False(result.Success);
        Assert.Contains("echo mismatch", result.ErrorMessage);
    }

    [Fact]
    public void SendFc16_ShouldFail_WhenWriteEchoMismatch()
    {
        // wrong echoed quantity (3 instead of 2)
        var service = CreateService(BuildResponse(0x01, 0x10, 0x00, 0x00, 0x00, 0x03));

        var result = service.SendFc16(Msg(0x01, 0, 2), [0x000A, 0x000B]);

        Assert.False(result.Success);
        Assert.Contains("echo mismatch", result.ErrorMessage);
    }

    [Fact]
    public void SendFc3_ShouldFail_WhenCrcInvalid()
    {
        var response = BuildResponse(0x01, 0x03, 0x04, 0x00, 0x0A, 0x00, 0x0B);
        response[^1] ^= 0xFF; // corrupt CRC
        var service = CreateService(response);

        var (result, _) = service.SendFc3(Msg(0x01, 0, 2));

        Assert.False(result.Success);
        Assert.Contains("Invalid CRC", result.ErrorMessage);
    }

    [Fact]
    public void SendFc3_ShouldFail_WhenAddressMismatch()
    {
        var service = CreateService(BuildResponse(0x02, 0x03, 0x04, 0x00, 0x0A, 0x00, 0x0B));

        var (result, _) = service.SendFc3(Msg(0x01, 0, 2));

        Assert.False(result.Success);
        Assert.Contains("Unexpected slave address", result.ErrorMessage);
    }

    [Fact]
    public void SendFc3_ShouldFail_WhenFunctionCodeMismatch()
    {
        // FC4 answer to an FC3 request
        var service = CreateService(BuildResponse(0x01, 0x04, 0x04, 0x00, 0x0A, 0x00, 0x0B));

        var (result, _) = service.SendFc3(Msg(0x01, 0, 2));

        Assert.False(result.Success);
    }

    [Fact]
    public void SendFc3_ShouldFail_WhenByteCountMismatch()
    {
        // 2 registers requested (4 bytes expected), slave claims 2 bytes; CRC is valid
        var service = CreateService(BuildResponse(0x01, 0x03, 0x02, 0x00, 0x0A));

        var (result, _) = service.SendFc3(Msg(0x01, 0, 2));

        Assert.False(result.Success);
    }

    [Fact]
    public void SendFc1_ShouldFail_WhenModbusException()
    {
        // function = 0x81 (0x01 | 0x80), code = 0x02
        var service = CreateService(BuildResponse(0x01, 0x81, 0x02));

        var (result, _) = service.SendFc1(Msg(0x01, 0, 8));

        Assert.False(result.Success);
        Assert.Contains("Modbus exception response", result.ErrorMessage);
    }

    [Fact]
    public void SendFc3_ShouldFail_WhenModbusException()
    {
        var service = CreateService(BuildResponse(0x01, 0x83, 0x02));

        var (result, _) = service.SendFc3(Msg(0x01, 0, 2));

        Assert.False(result.Success);
        Assert.Contains("Modbus exception response", result.ErrorMessage);
    }

    [Fact]
    public void SendFc5_ShouldFail_WhenExceptionResponse()
    {
        var service = CreateService(BuildResponse(0x01, 0x85, 0x02));

        var (result, _) = service.SendFc5(Msg(0x01, 0, 1), true);

        Assert.False(result.Success);
        Assert.Contains("Modbus exception response", result.ErrorMessage);
    }

    [Fact]
    public void SendFc15_ShouldFail_WhenExceptionResponse()
    {
        var service = CreateService(BuildResponse(0x01, 0x8F, 0x02));

        var result = service.SendFc15(Msg(0x01, 0, 8), [true, false, false, false, false, false, false, false]);

        Assert.False(result.Success);
        Assert.Contains("Modbus exception response", result.ErrorMessage);
    }

    [Fact]
    public void SendFc16_ShouldFail_WhenExceptionResponse()
    {
        var service = CreateService(BuildResponse(0x01, 0x90, 0x02));

        var result = service.SendFc16(Msg(0x01, 0, 2), [0x000A, 0x000B]);

        Assert.False(result.Success);
        Assert.Contains("Modbus exception response", result.ErrorMessage);
    }

    [Fact]
    public void SendFc1_ShouldFail_WhenPortClosed()
    {
        var fake = new FakeSerialConnectionService([0x00]) { IsSerialPortOpen = false };
        var service = new ModbusRtuService(fake);

        var (result, _) = service.SendFc1(Msg(0x01, 0, 8));

        Assert.False(result.Success);
        Assert.Contains("Serial port is closed", result.ErrorMessage);
    }

    [Fact]
    public void SendFc6_ShouldFail_WhenPortClosed()
    {
        var fake = new FakeSerialConnectionService([0x00]) { IsSerialPortOpen = false };
        var service = new ModbusRtuService(fake);

        var (result, _) = service.SendFc6(Msg(0x01, 0, 1), 1);

        Assert.False(result.Success);
        Assert.Contains("Serial port is closed", result.ErrorMessage);
    }

    [Fact]
    public void SendFc1_ShouldFail_WhenReadTimeout()
    {
        // truncated response: the fake times out when asked for more bytes than it has
        var service = CreateService([0x01, 0x01]);

        var (result, _) = service.SendFc1(Msg(0x01, 0, 8));

        Assert.False(result.Success);
        Assert.Contains("timeout", result.ErrorMessage);
    }

    [Fact]
    public void SendFc3_ShouldFail_WhenReadTimeout()
    {
        var service = CreateService([0x01, 0x03]);

        var (result, _) = service.SendFc3(Msg(0x01, 0, 2));

        Assert.False(result.Success);
        Assert.Contains("timeout", result.ErrorMessage);
    }

    [Fact]
    public void SendFc15_ShouldFail_WhenReadTimeout()
    {
        var service = CreateService([0x01, 0x0F]);

        var result = service.SendFc15(Msg(0x01, 0, 8), [true, false, false, false, false, false, false, false]);

        Assert.False(result.Success);
        Assert.Contains("timeout", result.ErrorMessage);
    }

    [Fact]
    public void SendFc16_ShouldFail_WhenReadTimeout()
    {
        var service = CreateService([0x01, 0x10]);

        var result = service.SendFc16(Msg(0x01, 0, 2), [0x000A, 0x000B]);

        Assert.False(result.Success);
        Assert.Contains("timeout", result.ErrorMessage);
    }

    [Fact]
    public void SendFc6_ShouldFail_WhenWriteTimeout()
    {
        var fake = new FakeSerialConnectionService([]) { WriteTimeout = true };
        var service = new ModbusRtuService(fake);

        var (result, _) = service.SendFc6(Msg(0x01, 0, 1), 1);

        Assert.False(result.Success);
        Assert.Contains("Write timeout", result.ErrorMessage);
    }

    [Fact]
    public void SendFc15_ShouldFail_WhenWriteTimeout()
    {
        var fake = new FakeSerialConnectionService([]) { WriteTimeout = true };
        var service = new ModbusRtuService(fake);

        var result = service.SendFc15(Msg(0x01, 0, 8), [true, false, true, false, true, false, true, false]);

        Assert.False(result.Success);
        Assert.Contains("Write timeout", result.ErrorMessage);
    }

    [Fact]
    public void SendFc16_ShouldFail_WhenWriteTimeout()
    {
        var fake = new FakeSerialConnectionService([]) { WriteTimeout = true };
        var service = new ModbusRtuService(fake);

        var result = service.SendFc16(Msg(0x01, 0, 2), [0x000A, 0x000B]);

        Assert.False(result.Success);
        Assert.Contains("Write timeout", result.ErrorMessage);
    }

    [Fact]
    public void SendFc1_ShouldFail_WhenRegisterExceedsMax()
    {
        var service = CreateService([]);

        var (result, _) = service.SendFc1(Msg(0x01, 0, 3000));

        Assert.False(result.Success);
        Assert.Contains($"exceeds maximum of {ModbusFrame.MaxReadCoils}", result.ErrorMessage);
    }

    [Fact]
    public void SendFc3_ShouldFail_WhenRegisterExceedsMax()
    {
        var service = CreateService([]);

        var (result, _) = service.SendFc3(Msg(0x01, 0, 200));

        Assert.False(result.Success);
        Assert.Contains($"exceeds maximum of {ModbusFrame.MaxReadHoldingRegisters}", result.ErrorMessage);
    }

    [Fact]
    public void SendFc15_ShouldFail_WhenCoilCountExceedsMax()
    {
        var service = CreateService([]);

        var result = service.SendFc15(Msg(0x01, 0, 3000), new bool[3000]);

        Assert.False(result.Success);
        Assert.Contains($"exceeds maximum of {ModbusFrame.MaxWriteMultipleCoils}", result.ErrorMessage);
    }

    [Fact]
    public void SendFc16_ShouldFail_WhenRegisterExceedsMax()
    {
        var service = CreateService([]);

        var result = service.SendFc16(Msg(0x01, 0, 200), new ushort[200]);

        Assert.False(result.Success);
        Assert.Contains($"exceeds maximum of {ModbusFrame.MaxWriteMultipleRegisters}", result.ErrorMessage);
    }

    [Fact]
    public void SendFc1_ShouldFail_WhenCountIsZero()
    {
        var (service, fake) = CreateWithFake([]);

        var (result, _) = service.SendFc1(Msg(0x01, 0, 0));

        Assert.False(result.Success);
        Assert.Empty(fake.SentBytes); // nothing must reach the wire
    }

    [Fact]
    public void SendFc3_ShouldFail_WhenCountIsZero()
    {
        var (service, fake) = CreateWithFake([]);

        var (result, _) = service.SendFc3(Msg(0x01, 0, 0));

        Assert.False(result.Success);
        Assert.Empty(fake.SentBytes);
    }

    [Fact]
    public void SendFc15_ShouldFail_WhenCoilCountIsZero()
    {
        var service = CreateService([]);

        var result = service.SendFc15(Msg(0x01, 0, 0), []);

        Assert.False(result.Success);
        Assert.Contains("Coil count must be greater than 0", result.ErrorMessage);
    }

    [Fact]
    public void SendFc16_ShouldFail_WhenRegisterCountIsZero()
    {
        var service = CreateService([]);

        var result = service.SendFc16(Msg(0x01, 0, 0), []);

        Assert.False(result.Success);
        Assert.Contains("Register count must be greater than 0", result.ErrorMessage);
    }

    [Fact]
    public void SendFc3_ShouldFail_WhenAddressIsInvalid()
    {
        var service = CreateService([]);

        var (result, _) = service.SendFc3(Msg(248, 0, 1));

        Assert.False(result.Success);
        Assert.Contains("Slave address must be between", result.ErrorMessage);
    }

    [Fact]
    public void SendFc5_ShouldFail_WhenAddressIsInvalid()
    {
        var service = CreateService([]);

        var (result, _) = service.SendFc5(Msg(248, 0, 1), true);

        Assert.False(result.Success);
        Assert.Contains("Slave address must be between", result.ErrorMessage);
    }

    [Fact]
    public void SendFc6_ShouldFail_WhenAddressIsInvalid()
    {
        var service = CreateService([]);

        var (result, _) = service.SendFc6(Msg(248, 0, 1), 0x0010);

        Assert.False(result.Success);
        Assert.Contains("Slave address must be between", result.ErrorMessage);
    }

    [Fact]
    public void SendFc15_ShouldFail_WhenAddressIsInvalid()
    {
        var service = CreateService([]);

        var result = service.SendFc15(Msg(248, 0, 8), new bool[8]);

        Assert.False(result.Success);
        Assert.Contains("Slave address must be between", result.ErrorMessage);
    }

    [Fact]
    public void SendFc16_ShouldFail_WhenAddressIsInvalid()
    {
        var service = CreateService([]);

        var result = service.SendFc16(Msg(248, 0, 2), [0x000A, 0x000B]);

        Assert.False(result.Success);
        Assert.Contains("Slave address must be between", result.ErrorMessage);
    }

    [Fact]
    public void SendFc1_ShouldFail_WhenAddressIsBroadcast()
    {
        var service = CreateService([]);

        var (result, _) = service.SendFc1(Msg(0, 0, 8));

        Assert.False(result.Success);
        Assert.Contains("Read operations are not supported", result.ErrorMessage);
    }

    [Fact]
    public void SendFc3_ShouldFail_WhenAddressIsBroadcast()
    {
        var service = CreateService([]);

        var (result, _) = service.SendFc3(Msg(0, 0, 2));

        Assert.False(result.Success);
        Assert.Contains("Read operations are not supported", result.ErrorMessage);
    }

    [Fact]
    public void SendFc5_ShouldSucceed_WhenAddressIsBroadcast()
    {
        var (service, fake) = CreateWithFake([]);

        var (result, coilState) = service.SendFc5(Msg(0, 0, 1), true);

        // Broadcast write: frame is sent, no response is read, value is not confirmed
        Assert.True(result.Success);
        Assert.False(coilState);
        Assert.Single(fake.SentBytes);
        Assert.NotNull(fake.LastSentBytes);
        Assert.Equal(0x00, fake.LastSentBytes[0]);
    }

    [Fact]
    public void SendFc6_ShouldSucceed_WhenAddressIsBroadcast()
    {
        var (service, fake) = CreateWithFake([]);

        var (result, registerValue) = service.SendFc6(Msg(0, 0, 1), 0x0010);

        Assert.True(result.Success);
        Assert.Equal(0, registerValue);
        Assert.Single(fake.SentBytes);
        Assert.NotNull(fake.LastSentBytes);
        Assert.Equal(0x00, fake.LastSentBytes[0]);
    }

    [Fact]
    public void SendFc15_ShouldSucceed_WhenAddressIsBroadcast()
    {
        var (service, fake) = CreateWithFake([]);

        var result = service.SendFc15(Msg(0, 0, 8), new bool[8]);

        Assert.True(result.Success);
        Assert.Single(fake.SentBytes);
        Assert.NotNull(fake.LastSentBytes);
        Assert.Equal(0x00, fake.LastSentBytes[0]);
    }

    [Fact]
    public void SendFc16_ShouldSucceed_WhenAddressIsBroadcast()
    {
        var (service, fake) = CreateWithFake([]);

        var result = service.SendFc16(Msg(0, 0, 2), [0x000A, 0x000B]);

        Assert.True(result.Success);
        Assert.Single(fake.SentBytes);
        Assert.NotNull(fake.LastSentBytes);
        Assert.Equal(0x00, fake.LastSentBytes[0]);
    }
}