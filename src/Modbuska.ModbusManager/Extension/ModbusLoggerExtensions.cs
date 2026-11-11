using Microsoft.Extensions.Logging;
using Modbuska.ModbusManager.Constant;

namespace Modbuska.ModbusManager.Extension
{
    /// <summary>
    /// Helper for deep logging
    /// </summary>
    public static class ModbusLoggerExtensions
    {
        private static readonly Action<ILogger, byte, ModbusFunctionType, string, Exception?> _logTxFrame =
            LoggerMessage.Define<byte, ModbusFunctionType, string>(
                LogLevel.Trace,
                new EventId(1, "ModbusRtuService.TxFrame"),
                "TX SlaveId={SlaveId} Function={Function} Frame={Frame}");

        private static readonly Action<ILogger, byte, ModbusFunctionType, string, Exception?> _logRxFrame =
            LoggerMessage.Define<byte, ModbusFunctionType, string>(
                LogLevel.Trace,
                new EventId(2, "ModbusRtuService.RxFrame"),
                "RX SlaveId={SlaveId} Function={Function} Frame={Frame}");

        private static readonly Action<ILogger, byte, ModbusFunctionType, string, Exception?> _logTransactionFailure =
                LoggerMessage.Define<byte, ModbusFunctionType, string>(
                    LogLevel.Warning,
                    new EventId(3, "ModbusRtuService.TransactionFailure"),
                    "Modbus transaction failed. SlaveId={SlaveId} Function={Function} Reason={Reason}");

        private static readonly Action<ILogger, byte, ModbusFunctionType, int, Exception?> _logTransactionCompleted =
            LoggerMessage.Define<byte, ModbusFunctionType, int>(
                LogLevel.Debug,
                new EventId(4, "ModbusRtuService.TransactionCompleted"),
                "Modbus transaction completed. SlaveId={SlaveId} Function={Function} Count={Count} item(s)");

        private static readonly Action<ILogger, byte, ModbusFunctionType, string, Exception?> _logValidationFailure =
            LoggerMessage.Define<byte, ModbusFunctionType, string>(
                LogLevel.Debug,
                new EventId(5, "ModbusRtuService.ValidationFailure"),
                "Modbus transaction validation failed. SlaveId={SlaveId} Function={Function} Reason={Reason}");

        extension(ILogger logger)
        {
            /// <summary>
            /// Logging transmission message
            /// </summary>
            /// <param name="slaveId"></param>
            /// <param name="function"></param>
            /// <param name="frame"></param>
            public void LogTxFrame(byte slaveId, ModbusFunctionType function, string frame) =>
            _logTxFrame(logger, slaveId, function, frame, null);

            /// <summary>
            /// Logging recieve messages
            /// </summary>
            /// <param name="slaveId"></param>
            /// <param name="function"></param>
            /// <param name="frame"></param>
            public void LogRxFrame(byte slaveId, ModbusFunctionType function, string frame) =>
                _logRxFrame(logger, slaveId, function, frame, null);

            /// <summary>
            /// Logging failure
            /// </summary>
            /// <param name="slaveId"></param>
            /// <param name="function"></param>
            /// <param name="reason"></param>
            /// <param name="exception"></param>
            /// <summary>
            /// Logging failure
            /// </summary>
            public void LogTransactionFailure(byte slaveId, ModbusFunctionType function, string reason, Exception? exception = null) =>
                _logTransactionFailure(logger, slaveId, function, reason, exception);

            /// <summary>
            /// Logging success states
            /// </summary>
            /// <param name="slaveId"></param>
            /// <param name="function"></param>
            /// <param name="count"></param>
            public void LogTransactionCompleted(byte slaveId, ModbusFunctionType function, int count) =>
                _logTransactionCompleted(logger, slaveId, function, count, null);

            /// <summary>
            /// Logging validation errors
            /// </summary>
            /// <param name="slaveId"></param>
            /// <param name="function"></param>
            /// <param name="reason"></param>
            public void LogValidationFailure(byte slaveId, ModbusFunctionType function, string reason) =>
                _logValidationFailure(logger, slaveId, function, reason, null);
        }
    }
}
