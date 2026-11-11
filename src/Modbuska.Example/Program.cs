using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Modbuska.Example;
using Modbuska.ModbusManager.Extension;

var services = new ServiceCollection();

services.AddLogging(builder =>
{
    builder.AddConsole();
    builder.SetMinimumLevel(LogLevel.Trace);
});

services.AddModbus();
services.AddSingleton<ModbusConsoleApp>();

await using var serviceProvider = services.BuildServiceProvider();

var app = serviceProvider.GetRequiredService<ModbusConsoleApp>();
await app.RunAsync(args);