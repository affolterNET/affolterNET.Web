using affolterNET.Web.Core.Configuration;
using Microsoft.Extensions.Options;

namespace affolterNET.Web.Core.Services;

/// <summary>
/// Tests for the master gate added in <see cref="PermissionService.GetUserPermissionsAsync"/>:
/// when <see cref="PermissionCacheOptions.Enabled"/> is false, the method must short-circuit
/// and never touch <see cref="RptTokenService"/>. This is the regression guard for the
/// "Client does not support permissions" warning that fired on every login when the Keycloak
/// client did not have the UMA Authorization feature enabled.
/// </summary>
public class PermissionServiceGateTests
{
    [Fact]
    public async Task GetUserPermissionsAsync_ReturnsEmpty_WhenDisabled()
    {
        // Arrange — construct PermissionService with the gate flipped off.
        // Other dependencies pass null! because the early-return must trigger
        // before any other field is touched. If the gate is broken, the test
        // will throw NRE on rptTokenService / cache / etc.
        var monitor = new TestOptionsMonitor<PermissionCacheOptions>(
            new PermissionCacheOptions { Enabled = false });
        var sut = new PermissionService(
            rptTokenService: null!,
            rptCacheService: null!,
            cache: null!,
            httpContextAccessor: null!,
            logger: Microsoft.Extensions.Logging.Abstractions.NullLogger<PermissionService>.Instance,
            permissionCacheOptions: monitor);

        // Act
        var result = await sut.GetUserPermissionsAsync("user-1", "any-access-token");

        // Assert
        Assert.Empty(result);
    }

    private sealed class TestOptionsMonitor<T>(T currentValue) : IOptionsMonitor<T>
    {
        public T CurrentValue { get; } = currentValue;

        public T Get(string? name) => CurrentValue;

        public IDisposable OnChange(Action<T, string?> listener) => NullDisposable.Instance;

        private sealed class NullDisposable : IDisposable
        {
            public static readonly NullDisposable Instance = new();
            public void Dispose() { }
        }
    }
}
