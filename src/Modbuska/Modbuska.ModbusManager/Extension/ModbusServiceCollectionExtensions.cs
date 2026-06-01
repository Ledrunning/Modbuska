using Microsoft.Extensions.DependencyInjection;
using Modbuska.ModbusManager.Service;
using System.IO.Ports;

namespace Modbuska.ModbusManager.Extension;

public static class ModbusServiceCollectionExtensions
{
    public static IServiceCollection AddModbus(this IServiceCollection collection)
    {
        ArgumentNullException.ThrowIfNull(collection);

        collection.AddLogging();
        collection.AddSingleton<SerialPort>();
        collection.AddSingleton<SerialConnectionService>();
        collection.AddSingleton<ModbusRtuService>();

        return collection;
    }
}