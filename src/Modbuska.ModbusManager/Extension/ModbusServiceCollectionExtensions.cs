using System.IO.Ports;
using Microsoft.Extensions.DependencyInjection;
using Modbuska.ModbusManager.Abstraction;
using Modbuska.ModbusManager.Service;

namespace Modbuska.ModbusManager.Extension;

/// <summary>
///     Provides extension methods for registering Modbus services with the dependency injection container.
/// </summary>
public static class ModbusServiceCollectionExtensions
{
    /// <summary>
    ///     Registers all required Modbus services (serial connection, RTU service, and logging) with the dependency injection
    ///     container.
    /// </summary>
    /// <param name="collection">The <see cref="IServiceCollection" /> to add services to.</param>
    /// <returns>The same <see cref="IServiceCollection" /> for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="collection" /> is <c>null</c>.</exception>
    public static IServiceCollection AddModbus(this IServiceCollection collection)
    {
        ArgumentNullException.ThrowIfNull(collection);

        collection.AddLogging();
        collection.AddSingleton<SerialPort>();
        collection.AddSingleton<SerialConnectionService>();
        collection.AddSingleton<ISerialConnectionService>(sp => sp.GetRequiredService<SerialConnectionService>());
        collection.AddSingleton<ModbusRtuService>();

        return collection;
    }
}