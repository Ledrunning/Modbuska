using Microsoft.Extensions.DependencyInjection;
using Modbuska.ModbusManager.Extension;
using Modbuska.ModbusManager.Service;

namespace Modbuska.UnitTest;

public class ModbusServiceCollectionExtensionsTests
{
    [Test]
    public void AddModbus_ShouldRegisterRequiredServices()
    {
        var services = new ServiceCollection();

        services.AddModbus();

        using var provider = services.BuildServiceProvider();

        var modbusService = provider.GetService<ModbusRtuService>();
        var serialService = provider.GetService<SerialConnectionService>();

        Assert.That(modbusService, Is.Not.Null);
        Assert.That(serialService, Is.Not.Null);
    }

    [Test]
    public void AddModbus_ShouldReturnSameCollection()
    {
        var services = new ServiceCollection();

        var returned = services.AddModbus();

        Assert.That(returned, Is.SameAs(services));
    }
}
