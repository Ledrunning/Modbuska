# Modbuska

**Modbuska** (pronounced *Mod-bus-ka*) is a lightweight .NET 10 library for Modbus RTU communication over serial ports.

## Features

- Modbus RTU over `System.IO.Ports`
- Dependency Injection support via `AddModbus()`
- Supported function codes:
  - FC1 – Read Coils
  - FC2 – Read Discrete Inputs
  - FC3 – Read Holding Registers
  - FC4 – Read Input Registers
  - FC5 – Write Single Coil
  - FC6 – Write Single Register
  - FC15 – Write Multiple Coils
  - FC16 – Write Multiple Registers
- Console example application included

## Installation

```bash
dotnet add package Modbuska.ModbusManager
```

## Quick Start

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

var openResult = serial.Open(new SerialPortSettings
{
    PortName = "COM3",
    BaudRate = 9600,
    DataBits = 8,
    Parity = System.IO.Ports.Parity.None,
    StopBits = System.IO.Ports.StopBits.One
});

if (!openResult.Success)
{
    Console.WriteLine(openResult.ErrorMessage);
    return;
}

var message = new MyModbusMessage
{
    Address = 1,
    Start = 0,
    Register = 4
};

var (result, values) = modbus.SendFc3(message);
```

`Open()` and `Close()` return `IOperationResult`, allowing you to check the operation status through `Success` and `ErrorMessage`.

Optional serial timeout settings:

```csharp
var openResult = serial.Open(new SerialPortSettings
{
    PortName = "COM3",
    BaudRate = 9600,
    ReadTimeout = 1000,   // Default: 500 ms
    WriteTimeout = 200    // Default: 100 ms
});
```

> `ModbusMessage` is abstract. Create your own concrete message type in the consuming project.

## Example Project

The repository includes `Modbuska.Example`, a console application demonstrating all currently implemented Modbus functions through Dependency Injection.

## Hardware Testing (ESP8266)

You can test Modbuska using an ESP8266 development board flashed with the Arduino sketch provided in the repository.

### Setup

1. Flash the ESP8266 with the provided Arduino sketch.
2. Connect the board to your computer via USB.
3. Run `Modbuska.Example` and select **Open serial**.
4. Configure the serial connection:

| Parameter | Value |
|---|---|
| Port name | `COM3` or `/dev/ttyUSB0` |
| Baud rate | `115200` |
| Data bits | `8` |
| Parity | `None` |
| Stop bits | `One` |

### Test Scenarios

The following scenarios demonstrate all supported Modbus function codes using the example application and ESP8266.

| Function | Start Address | Count / Value | Expected Result |
|---|---:|---:|---|
| FC1 – Read Coils | 0 | 8 | All coils initially `False` |
| FC2 – Read Discrete Inputs | 0 | 8 | `True, False, True, False, ...` |
| FC3 – Read Holding Registers | 0 | 4 | `0, 10, 20, 30` |
| FC4 – Read Input Registers | 0 | 4 | `0, 100, 200, 300` |
| FC5 – Write Single Coil | 0 | `1` | Coil 0 becomes `True`; built-in LED turns on |
| FC6 – Write Single Register | 0 | `555` | Register 0 becomes `555`; built-in LED turns on |
| FC15 – Write Multiple Coils | 0 | `1, 0, 1, 1` | Four coils are updated |
| FC16 – Write Multiple Registers | 0 | `100, 200, 300` | Three holding registers are updated |

**Additional verification:**

- **FC5:** Write `0` to coil 0 to turn the LED off.
- **FC6:** Write a value other than `555` (e.g. `10`) to turn the LED off.
- **FC15:** Run FC1 to verify the written coil values.
- **FC16:** Run FC3 to verify the written register values.

All test scenarios use slave address `1`.

## Build & Pack

Create a NuGet package locally:

```bash
dotnet pack Modbuska.ModbusManager/Modbuska.ModbusManager.csproj -c Release
```

`Modbuska.Example` is included in the repository as a sample project and is not packed into the NuGet package.

## License

This project is licensed under the [MIT License](LICENSE).