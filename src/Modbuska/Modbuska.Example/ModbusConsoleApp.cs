using System.Globalization;
using System.IO.Ports;
using ConsoleTools;
using Modbuska.ModbusManager.Abstraction;
using Modbuska.ModbusManager.Model;
using Modbuska.ModbusManager.Service;

namespace Modbuska.Example;

internal sealed class ModbusConsoleApp(
    ModbusRtuService modbusService,
    SerialConnectionService serialConnectionService)
{
    private readonly ModbusRtuService _modbusService = modbusService;
    private readonly SerialConnectionService _serialConnectionService = serialConnectionService;

    public async Task RunAsync(string[] args)
    {
        var menu = new ConsoleMenu(args, 0)
            .Add("Open serial", OpenSerialPort)
            .Add("Close serial", CloseSerialPort)
            .Add("FC1 - Read Coils", ExecuteFc1)
            .Add("FC2 - Read Discrete Inputs", ExecuteFc2)
            .Add("FC3 - Read Holding Registers", ExecuteFc3)
            .Add("FC4 - Read Input Registers", ExecuteFc4)
            .Add("FC5 - Write Single Coil", ExecuteFc5)
            .Add("FC6 - Write Single Register", ExecuteFc6)
            .Add("FC15 - Write Multiple Coils", ExecuteFc15)
            .Add("FC16 - Write Multiple Registers", ExecuteFc16)
            .Add("Exit", () => Environment.Exit(0))
            .Configure(config =>
            {
                config.Selector = "--> ";
                config.EnableFilter = true;
                config.EnableWriteTitle = true;
                config.EnableBreadcrumb = true;
                config.ItemForegroundColor = ConsoleColor.Green;
            });

        await menu.ShowAsync();
    }

    private void OpenSerialPort()
    {
        var portName = ReadRequiredString("Port name", "COM3");
        var baudRate = ReadInt("Baud rate", 9600, 1);
        var dataBits = ReadInt("Data bits", 8, 5, 8);
        var parity = ReadEnum("Parity", Parity.None);
        var stopBits = ReadEnum("Stop bits", StopBits.One);

        var settings = new SerialPortSettings
        {
            PortName = portName,
            BaudRate = baudRate,
            DataBits = dataBits,
            Parity = parity,
            StopBits = stopBits
        };

        var openResult = _serialConnectionService.Open(settings);
        WriteStatus(openResult.Success
            ? $"Serial port {portName} opened"
            : $"Failed to open serial port {portName}: {openResult.ErrorMessage}");
    }

    private void CloseSerialPort()
    {
        var closeResult = _serialConnectionService.Close();
        WriteStatus(closeResult.Success
            ? "Serial port closed"
            : $"Failed to close serial port: {closeResult.ErrorMessage}");
    }

    private void ExecuteFc1()
    {
        var message = ReadBaseMessage();
        var (result, coils) = _modbusService.SendFc1(message);

        WriteResult(result, result.Success ? $"Coils: {FormatValues(coils)}" : null);
    }

    private void ExecuteFc2()
    {
        var message = ReadBaseMessage();
        var (result, inputs) = _modbusService.SendFc2(message);

        WriteResult(result, result.Success ? $"Inputs: {FormatValues(inputs)}" : null);
    }

    private void ExecuteFc3()
    {
        var message = ReadBaseMessage();
        var (result, registers) = _modbusService.SendFc3(message);

        WriteResult(result, result.Success ? $"Holding registers: {FormatValues(registers)}" : null);
    }

    private void ExecuteFc4()
    {
        var message = ReadBaseMessage();
        var (result, registers) = _modbusService.SendFc4(message);

        WriteResult(result, result.Success ? $"Input registers: {FormatValues(registers)}" : null);
    }

    private void ExecuteFc5()
    {
        var message = ReadSingleAddressMessage();
        var value = ReadBool("Coil value", true);
        var (result, coilState) = _modbusService.SendFc5(message, value);

        WriteResult(result, result.Success ? $"Returned coil state: {coilState}" : null);
    }

    private void ExecuteFc6()
    {
        var message = ReadSingleAddressMessage();
        var value = (ushort)ReadInt("Register value", 1, 0, ushort.MaxValue);
        var (result, registerValue) = _modbusService.SendFc6(message, value);

        WriteResult(result, result.Success ? $"Returned register value: {registerValue}" : null);
    }

    private void ExecuteFc15()
    {
        var message = ReadBaseMessage();
        var values = ReadBoolArray(message.Register, "coil values");
        var result = _modbusService.SendFc15(message, values);

        WriteResult(result, result.Success ? $"Written coils: {FormatValues(values)}" : null);
    }

    private void ExecuteFc16()
    {
        var message = ReadBaseMessage();
        var values = ReadShortArray(message.Register, "register values");
        var result = _modbusService.SendFc16(message, values);

        WriteResult(result, result.Success ? $"Written registers: {FormatValues(values)}" : null);
    }

    private static ModbusRequestMessage ReadBaseMessage()
    {
        return new ModbusRequestMessage
        {
            Address = (byte)ReadInt("Slave address", 1, 0, byte.MaxValue),
            Start = (ushort)ReadInt("Start address", 0, 0, ushort.MaxValue),
            Register = (ushort)ReadInt("Register/count", 1, 1, ushort.MaxValue)
        };
    }

    private static ModbusRequestMessage ReadSingleAddressMessage()
    {
        return new ModbusRequestMessage
        {
            Address = (byte)ReadInt("Slave address", 1, 0, byte.MaxValue),
            Start = (ushort)ReadInt("Output/register address", 0, 0, ushort.MaxValue),
            Register = 1
        };
    }

    private static bool[] ReadBoolArray(ushort expectedCount, string label)
    {
        while (true)
        {
            var raw = ReadRequiredString($"Enter {label} ({expectedCount} values, e.g. 1,0,1)");
            var parts = raw.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length != expectedCount)
            {
                WriteStatus($"Expected {expectedCount} values, but got {parts.Length}.");
                continue;
            }

            var values = new bool[parts.Length];
            var valid = true;

            for (var i = 0; i < parts.Length; i++)
            {
                if (TryParseBool(parts[i], out var parsed))
                {
                    values[i] = parsed;
                    continue;
                }

                WriteStatus($"Invalid boolean value: {parts[i]}");
                valid = false;
                break;
            }

            if (valid) return values;
        }
    }

    private static ushort[] ReadShortArray(ushort expectedCount, string label)
    {
        while (true)
        {
            var raw = ReadRequiredString($"Enter {label} ({expectedCount} values, e.g. 10,20,30)");
            var parts = raw.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length != expectedCount)
            {
                WriteStatus($"Expected {expectedCount} values, but got {parts.Length}.");
                continue;
            }

            var values = new ushort[parts.Length];
            var valid = true;

            for (var i = 0; i < parts.Length; i++)
            {
                if (ushort.TryParse(parts[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
                {
                    values[i] = parsed;
                    continue;
                }

                WriteStatus($"Invalid UInt16 value: {parts[i]}");
                valid = false;
                break;
            }

            if (valid) return values;
        }
    }

    private static int ReadInt(string prompt, int defaultValue, int min, int max = int.MaxValue)
    {
        while (true)
        {
            Console.Write($"{prompt} [{defaultValue}]: ");
            var input = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(input)) return defaultValue;

            if (int.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) &&
                value >= min && value <= max) return value;

            WriteStatus($"Enter a value between {min} and {max}.");
        }
    }

    private static TEnum ReadEnum<TEnum>(string prompt, TEnum defaultValue) where TEnum : struct, Enum
    {
        while (true)
        {
            Console.Write($"{prompt} [{defaultValue}] ({string.Join(", ", Enum.GetNames<TEnum>())}): ");
            var input = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(input)) return defaultValue;

            if (Enum.TryParse<TEnum>(input, true, out var value)) return value;

            WriteStatus($"Unknown {prompt} value.");
        }
    }

    private static bool ReadBool(string prompt, bool defaultValue)
    {
        while (true)
        {
            Console.Write($"{prompt} [{(defaultValue ? "1" : "0")}] (1/0,true/false): ");
            var input = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(input)) return defaultValue;

            if (TryParseBool(input, out var value)) return value;

            WriteStatus("Enter 1/0 or true/false.");
        }
    }

    private static string ReadRequiredString(string prompt, string? defaultValue = null)
    {
        while (true)
        {
            if (defaultValue is null)
                Console.Write($"{prompt}: ");
            else
                Console.Write($"{prompt} [{defaultValue}]: ");

            var input = Console.ReadLine();
            if (!string.IsNullOrWhiteSpace(input)) return input.Trim();

            if (!string.IsNullOrWhiteSpace(defaultValue)) return defaultValue;

            WriteStatus("Value is required.");
        }
    }

    private static bool TryParseBool(string input, out bool value)
    {
        if (bool.TryParse(input, out value)) return true;

        switch (input.Trim().ToLowerInvariant())
        {
            case "1":
            case "y":
            case "yes":
                value = true;
                return true;
            case "0":
            case "n":
            case "no":
                value = false;
                return true;
            default:
                value = false;
                return false;
        }
    }

    private static string FormatValues<T>(IEnumerable<T> values)
    {
        return string.Join(", ", values);
    }

    private static void WriteResult(IOperationResult result, string? successMessage)
    {
        if (result.Success)
        {
            WriteStatus(successMessage ?? "Operation completed successfully.");
            return;
        }

        WriteStatus($"Operation failed: {result.ErrorMessage}");
    }

    private static void WriteStatus(string message)
    {
        Console.WriteLine(message);
        Console.WriteLine("Press any key to continue...");
        Console.ReadKey(true);
        Console.WriteLine();
    }
}