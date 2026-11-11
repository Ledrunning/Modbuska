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

    /// <summary>
    ///     All byte arrays written via <see cref="WriteData" />, in order.
    /// </summary>
    public IReadOnlyList<byte[]> SentBytes => _sentBytes;

    /// <summary>
    ///     The last byte array written via <see cref="WriteData" />, or null if nothing has been written.
    /// </summary>
    public byte[]? LastSentBytes => _sentBytes.Count > 0 ? _sentBytes[^1] : null;

    /// <summary>
    ///     When true, <see cref="WriteData" /> throws a <see cref="TimeoutException" /> to simulate a write timeout.
    /// </summary>
    public bool WriteTimeout { get; set; }

    public bool IsSerialPortOpen { get; set; } = true;

    public void DiscardSerialBuffers()
    {
    }

    public byte[] ReadExact(int length)
    {
        if (_readOffset + length > _response.Length)
            throw new TimeoutException(
                $"Simulated read timeout: requested {length} bytes but only {_response.Length - _readOffset} bytes remain in the response");

        var result = new byte[length];
        Array.Copy(_response, _readOffset, result, 0, length);
        _readOffset += length;
        return result;
    }

    public void WriteData(byte[] data)
    {
        if (WriteTimeout) throw new TimeoutException("Write timeout: simulated write timeout");

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
}