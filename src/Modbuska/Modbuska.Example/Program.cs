using Microsoft.Extensions.DependencyInjection;
using Modbuska.Example;
using Modbuska.ModbusManager.Extension;

var services = new ServiceCollection();

services.AddModbus();
services.AddSingleton<ModbusConsoleApp>();

using var serviceProvider = services.BuildServiceProvider();

var app = serviceProvider.GetRequiredService<ModbusConsoleApp>();
await app.RunAsync(args);
