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
    ///     Registers all required Modbus services (serial connection and RTU service) with the dependency injection
    ///     container.
    ///     The library does not configure any logging provider: the host application is responsible for calling
    ///     <c>AddLogging()</c> and adding the desired providers.
    /// </summary>
    /// <param name="collection">The <see cref="IServiceCollection" /> to add services to.</param>
    /// <returns>The same <see cref="IServiceCollection" /> for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="collection" /> is <c>null</c>.</exception>
    public static IServiceCollection AddModbus(this IServiceCollection collection)
    {
        ArgumentNullException.ThrowIfNull(collection);

        collection.AddSingleton<SerialPort>();
        collection.AddSingleton<SerialConnectionService>();
        collection.AddSingleton<ISerialConnectionService>(sp => sp.GetRequiredService<SerialConnectionService>());
        collection.AddSingleton<ModbusRtuService>();

        return collection;
    }
}