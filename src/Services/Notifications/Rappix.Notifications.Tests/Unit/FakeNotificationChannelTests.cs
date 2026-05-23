using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.Notifications.Application.Abstractions;
using Rappix.Notifications.Infrastructure.Channels;

namespace Rappix.Notifications.Tests.Unit;

/// <summary>
/// Pruebas unitarias del <see cref="FakeNotificationChannel"/>. Cobertura minima — el canal solo
/// loguea y devuelve Success. La integracion (consumer -> NotifyHandler -> canal -> log) se cubre
/// en los integration tests de commits 6-8 que verifican que el canal fake fue llamado con los
/// args correctos.
/// </summary>
public sealed class FakeNotificationChannelTests
{
    private static FakeNotificationChannel Build() => new(NullLogger<FakeNotificationChannel>.Instance);

    [Fact]
    public async Task SendAsync_ReturnsSuccessWithNullProviderId()
    {
        FakeNotificationChannel channel = Build();
        var message = new NotificationMessage(
            ToEmail: "customer@test.local",
            ToName: "Juan Perez",
            Subject: "Tu pedido fue aceptado",
            Body: "Hola Juan, el comercio acepto tu pedido.");

        Result<NotificationSendResult> result = await channel.SendAsync(message, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        // El Fake no expone ProviderMessageId — solo el canal real (SendGrid) lo hace.
        result.Value.ProviderMessageId.Should().BeNull();
    }

    [Fact]
    public async Task SendAsync_WithNullMessage_ThrowsArgumentNullException()
    {
        FakeNotificationChannel channel = Build();

        Func<Task> act = () => channel.SendAsync(null!, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
