using Modbuska.ModbusManager.Abstraction;
using Modbuska.ModbusManager.Model;
using Modbuska.ModbusManager.Results;

namespace Modbuska.UnitTest;

/// <summary>
///     A test double that simulates a serial connection by returning a pre-built response frame.
/// </summary>
internal sealed class FakeSerialConnectionService(byte[] response) : ISerialConnectionService
{
    private readonly byte[] _response = response;
    private readonly List<byte[]> _sentBytes = new();
    private int _readOffset;

    public bool IsSerialPortOpen { get; set; } = true;

    public void DiscardSerialBuffers()
    {
    }

    public byte[] ReadExact(int length)
    {
        var result = new byte[length];
        Array.Copy(_response, _readOffset, result, 0, length);
        _readOffset += length;
        return result;
    }

    public void WriteData(byte[] data)
    {
        _sentBytes.Add((byte[])data.Clone());
    }

    public IOperationResult Open(SerialPortSettings portSettings)
    {
        return OperationResult.Ok();
    }

    public IOperationResult Close()
    {
        return OperationResult.Ok();
    }

    /// <summary>
    ///     Throws if the last WriteData call resulted in fewer bytes than expected,
    ///     simulating a timeout on the write path.
    /// </summary>
    public void ThrowIfWriteTimeout()
    {
        if (_sentBytes.Count > 0 && _sentBytes[^1].Length < 3)
            throw new TimeoutException("Write timeout: response too short after request");
    }
}