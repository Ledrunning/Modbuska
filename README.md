# Modbuska

Modbuska is a lightweight .NET 9 library for Modbus RTU communication over serial ports.

## Features

- Modbus RTU over `System.IO.Ports`
- DI registration via `AddModbus()`
- Supported function codes:
    - FC1 - Read Coils
    - FC2 - Read Discrete Inputs
    - FC3 - Read Holding Registers
    - FC4 - Read Input Registers
    - FC5 - Write Single Coil
    - FC6 - Write Single Register
    - FC15 - Write Multiple Coils
    - FC16 - Write Multiple Registers
- Example console application included in repository

## Installation

```bash
dotnet add package Modbuska.ModbusManager
```

## Quick start

```csharp
using Microsoft.Extensions.DependencyInjection;
using Modbuska.ModbusManager.Extension;
using Modbuska.ModbusManager.Model;
using Modbuska.ModbusManager.Service;

var services = new ServiceCollection();
services.AddModbus();

using var provider = services.BuildServiceProvider();
var serial = provider.GetRequiredService<SerialConnectionService>();
var modbus = provider.GetRequiredService<ModbusRtuService>();

serial.Open(new SerialPortSettings
{
    PortName = "COM3",
    BaudRate = 9600,
    DataBits = 8,
    Parity = System.IO.Ports.Parity.None,
    StopBits = System.IO.Ports.StopBits.One
});

var message = new MyModbusMessage
{
    Address = 1,
    Start = 0,
    Register = 4
};

var (result, values) = modbus.SendFc3(message);
```

`Open` and `Close` return `IOperationResult`, so you can check `Success`/`ErrorMessage`:

```csharp
var openResult = serial.Open(new SerialPortSettings
{
    PortName = "COM3",
    BaudRate = 9600,
    ReadTimeout = 1000,   // default 500
    WriteTimeout = 200    // default 100
});

if (!openResult.Success)
{
    Console.WriteLine(openResult.ErrorMessage);
}
```

> `ModbusMessage` is abstract, so create your own concrete type in the consuming project.

## Example project

The repository contains `Modbuska.Example`, a console sample with menu-based execution of all currently implemented
Modbus functions through DI.

## Pack

Create a NuGet package locally:

```bash
dotnet pack Modbuska.ModbusManager/Modbuska.ModbusManager.csproj -c Release
```

## Notes before publishing

- Replace `https://github.com/your-github-user/Modbuska` in the project metadata with your actual GitHub repository URL.
- Review package author/company/version values before pushing to NuGet.
- Keep `Modbuska.Example` in the repository as a sample project; it is not packed.

## License

MIT
