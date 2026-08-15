using Keystone.Core.Errors;
using Keystone.Runtime.Context;

namespace Keystone.Runtime.Tests;

public class ServiceStoreTests
{
    [Fact]
    public void Set_then_Get_returns_value()
    {
        var store = new ServiceStore();

        store.Set("fs", new FakeFs(), ownerId: "plugin-a");
        var fs = store.Get<FakeFs>("fs");

        Assert.NotNull(fs);
    }

    [Fact]
    public void Rebind_same_scope_throws()
    {
        var store = new ServiceStore();
        store.Set("fs", new FakeFs(), ownerId: "plugin-a");

        // 同 scope 重复注册（另一提供者，属主校验 G8）= 报错（03 §2.1 rebind，G14）
        var exception = Assert.Throws<KeystoneException>(() => store.Set("fs", new FakeFs(), ownerId: "plugin-b"));

        Assert.Equal(ErrorCode.ServiceAlreadyRegistered, exception.Code);
        Assert.Contains("fs", exception.Message);
    }

    [Fact]
    public void Owner_can_update_own_service_value()
    {
        var store = new ServiceStore();
        store.Set("fs", new FakeFs(), ownerId: "plugin-a");

        store.Set("fs", new FakeFs(), ownerId: "plugin-a"); // 同属主更新不报错

        Assert.NotNull(store.Get<FakeFs>("fs"));
    }

    [Fact]
    public void TryGet_returns_null_for_missing_service()
    {
        var store = new ServiceStore();

        Assert.Null(store.TryGet<FakeFs>("missing"));
    }

    [Fact]
    public void Get_missing_service_throws()
    {
        var store = new ServiceStore();

        Assert.Throws<KeystoneException>(() => store.Get<FakeFs>("missing"));
    }

    private sealed class FakeFs;
}
