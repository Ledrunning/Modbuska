using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Modbuska.ModbusManager.Abstraction;
using Modbuska.ModbusManager.Constant;
using Modbuska.ModbusManager.Extension;
using Modbuska.ModbusManager.Service;
using Xunit;

namespace Modbuska.UnitTest;

public sealed class ModbusRtuServiceLoggingTests
{
    private static ModbusMessageStub Msg(byte address, ushort start, ushort count) =>
        new() { Address = address, Start = start, Register = count };

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
    public void SendFc3_ShouldLogTxAndRxFramesAtTrace()
    {
        // Arrange
        var logger = new ListLogger<ModbusRtuService>();
        var fake = new FakeSerialConnectionService(BuildResponse(0x01, 0x03, 0x04, 0x00, 0x0A, 0x00, 0x0B));
        var service = new ModbusRtuService(fake, logger);

        // Act
        var (result, registers) = service.SendFc3(Msg(0x01, 0, 2));

        // Assert
        Assert.True(result.Success);
        Assert.Equal(2, registers.Length);

        var traceLogs = logger.Logs.Where(l => l.Level == LogLevel.Trace).ToList();
        Assert.Equal(2, traceLogs.Count);

        var tx = Assert.Single(traceLogs, l => l.Message.StartsWith("TX SlaveId=1 Function=ReadHoldingRegisters Frame="));
        Assert.Contains("01 03 00 00 00 02", tx.Message);

        var rx = Assert.Single(traceLogs, l => l.Message.StartsWith("RX SlaveId=1 Function=ReadHoldingRegisters Frame="));
        Assert.Contains("01 03 04 00 0A 00 0B", rx.Message);

        // Request is logged before the response.
        Assert.True(logger.Logs.IndexOf(tx) < logger.Logs.IndexOf(rx));

        var completed = Assert.Single(logger.Logs, l => l.Level == LogLevel.Debug);
        Assert.Contains("SlaveId=1 Function=ReadHoldingRegisters Count=2 item(s)", completed.Message);
    }

    [Fact]
    public void SendFc3_ShouldLogWarningOnTimeout()
    {
        // Arrange
        var logger = new ListLogger<ModbusRtuService>();
        var fake = new FakeSerialConnectionService(BuildResponse(0x01, 0x03, 0x04, 0x00, 0x0A, 0x00, 0x0B))
        {
            WriteTimeout = true
        };
        var service = new ModbusRtuService(fake, logger);

        // Act
        var (result, _) = service.SendFc3(Msg(0x01, 0, 2));

        // Assert
        Assert.False(result.Success);

        var warnings = logger.Logs.Where(l => l.Level == LogLevel.Warning).ToList();
        Assert.Single(warnings);
        Assert.Contains("timeout", warnings[0].Message, StringComparison.OrdinalIgnoreCase);
        Assert.IsType<TimeoutException>(warnings[0].Exception);
    }

    [Fact]
    public void SendFc3_ShouldLogWarningOnInvalidCrc()
    {
        // Arrange
        var logger = new ListLogger<ModbusRtuService>();
        var response = BuildResponse(0x01, 0x03, 0x04, 0x00, 0x0A, 0x00, 0x0B);
        response[^1] ^= 0xFF; // corrupt CRC
        var fake = new FakeSerialConnectionService(response);
        var service = new ModbusRtuService(fake, logger);

        // Act
        var (result, _) = service.SendFc3(Msg(0x01, 0, 2));

        // Assert
        Assert.False(result.Success);

        var warnings = logger.Logs.Where(l => l.Level == LogLevel.Warning).ToList();
        Assert.Single(warnings);
        Assert.Contains("Invalid CRC in response", warnings[0].Message);
    }

    [Fact]
    public void SendFc3_ShouldLogWarningOnModbusExceptionResponse()
    {
        // Arrange
        var logger = new ListLogger<ModbusRtuService>();
        // FC3 exception from slave: 01 83 02 + CRC (exception code 0x02 = illegal data address)
        var fake = new FakeSerialConnectionService(BuildResponse(0x01, 0x83, 0x02));
        var service = new ModbusRtuService(fake, logger);

        // Act
        var (result, _) = service.SendFc3(Msg(0x01, 0, 2));

        // Assert
        Assert.False(result.Success);

        var warnings = logger.Logs.Where(l => l.Level == LogLevel.Warning).ToList();
        Assert.Single(warnings);
        Assert.Contains("Modbus exception response", warnings[0].Message);
        Assert.Contains("0x02", warnings[0].Message);
    }

    [Fact]
    public void SendFc5_ShouldLogWarningOnWriteEchoMismatch()
    {
        // Arrange
        var logger = new ListLogger<ModbusRtuService>();
        var message = Msg(0x01, 0, 1);
        // Response with a valid CRC but a wrong echo byte at offset 4 (0x00 instead of 0xFF).
        // ComputeCrc covers Length - 2 bytes, so the frame needs space for two CRC bytes.
        var response = new byte[] { 0x01, 0x05, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
        var crc = response.ComputeCrc();
        response[^2] = crc[ModbusFrame.CrcLowIndex];
        response[^1] = crc[ModbusFrame.CrcHighIndex];
        var fake = new FakeSerialConnectionService(response);
        var service = new ModbusRtuService(fake, logger);

        // Act
        var (result, _) = service.SendFc5(message, true);

        // Assert
        Assert.False(result.Success);

        var warnings = logger.Logs.Where(l => l.Level == LogLevel.Warning).ToList();
        Assert.Single(warnings);
        Assert.Contains("echo mismatch", warnings[0].Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SendFc6_ShouldLogWarningWhenSerialPortIsClosed()
    {
        // Arrange
        var logger = new ListLogger<ModbusRtuService>();
        var fake = new FakeSerialConnectionService(Array.Empty<byte>()) { IsSerialPortOpen = false };
        var service = new ModbusRtuService(fake, logger);

        // Act
        var (result, _) = service.SendFc6(Msg(0x01, 0, 1), 0x0010);

        // Assert
        Assert.False(result.Success);
        var warnings = logger.Logs.Where(l => l.Level == LogLevel.Warning).ToList();
        Assert.Single(warnings);
        Assert.Contains("Serial port is closed", warnings[0].Message);
    }

    [Fact]
    public void SendFc3_ShouldWorkWithoutLogger()
    {
        // Arrange
        var fake = new FakeSerialConnectionService(BuildResponse(0x01, 0x03, 0x04, 0x00, 0x0A, 0x00, 0x0B));
        var service = new ModbusRtuService(fake, null);

        // Act
        var (result, registers) = service.SendFc3(Msg(0x01, 0, 2));

        // Assert
        Assert.True(result.Success);
        Assert.Equal(2, registers.Length);
    }

    [Fact]
    public void SendFc3_WhenLoggingIsDisabled_ShouldNotLogFrames()
    {
        // Arrange
        var logger = new ListLogger<ModbusRtuService> { IsLoggingEnabled = false };
        var fake = new FakeSerialConnectionService(BuildResponse(0x01, 0x03, 0x04, 0x00, 0x0A, 0x00, 0x0B));
        var service = new ModbusRtuService(fake, logger);

        // Act
        var (result, _) = service.SendFc3(Msg(0x01, 0, 2));

        // Assert
        Assert.True(result.Success);
        Assert.DoesNotContain(logger.Logs, l => l.Level == LogLevel.Trace);
    }

    [Fact]
    public void AddModbus_WithLogging_ShouldInjectLoggerReceivingRecords()
    {
        // Arrange
        var provider = new TestLoggerProvider();
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddProvider(provider));
        services.AddModbus();
        // Override serial connection with a fake that has the port closed.
        services.AddSingleton<ISerialConnectionService>(
            sp => new FakeSerialConnectionService(Array.Empty<byte>()) { IsSerialPortOpen = false });

        using var sp = services.BuildServiceProvider();
        var modbusService = sp.GetRequiredService<ModbusRtuService>();

        // Act
        var (result, _) = modbusService.SendFc6(Msg(0x01, 0, 1), 0x0010);

        // Assert
        Assert.False(result.Success);
        var record = Assert.Single(provider.Records);
        Assert.Contains("ModbusRtuService", record.Category);
        Assert.Contains("Serial port is closed", record.Message);
    }
}
