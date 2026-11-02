using Microsoft.Extensions.DependencyInjection;
using Modbuska.ModbusManager.Extension;
using Modbuska.ModbusManager.Service;
using Xunit;

namespace Modbuska.UnitTest;

public class ModbusServiceCollectionExtensionsTests
{
    [Fact]
    public void AddModbus_ShouldRegisterRequiredServices()
    {
        var services = new ServiceCollection();

        services.AddModbus();

        using var provider = services.BuildServiceProvider();

        var modbusService = provider.GetService<ModbusRtuService>();
        var serialService = provider.GetService<SerialConnectionService>();

        Assert.NotNull(modbusService);
        Assert.NotNull(serialService);
    }

    [Fact]
    public void AddModbus_ShouldReturnSameCollection()
    {
        var services = new ServiceCollection();

        var returned = services.AddModbus();

        Assert.Same(services, returned);
    }
}