using Modbuska.ModbusManager.Abstraction;

namespace Modbuska.ModbusManager.Model;

internal readonly record struct ParseResult<T>(IOperationResult Result, T Data);