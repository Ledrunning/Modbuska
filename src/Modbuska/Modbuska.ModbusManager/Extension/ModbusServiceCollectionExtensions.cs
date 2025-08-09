using Microsoft.Extensions.DependencyInjection;
using Modbuska.ModbusManager.Service;

namespace Modbuska.ModbusManager.Extension;

public static class ModbusServiceCollectionExtensions
{
    public static void AddModbus(this IServiceCollection collection)
    {
        collection.AddTransient<ModbusRtuService>();
        collection.AddTransient<SerialConnectionService>();
    }
}