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
    }

    public IOperationResult Open(SerialPortSettings portSettings) => OperationResult.Ok();

    public IOperationResult Close() => OperationResult.Ok();
}