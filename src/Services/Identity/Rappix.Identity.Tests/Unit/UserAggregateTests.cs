using FluentAssertions;
using Rappix.Identity.Domain;
using Rappix.Identity.Domain.Users;
using Rappix.Identity.Domain.Users.Events;

namespace Rappix.Identity.Tests.Unit;

/// <summary>Pruebas unitarias del agregado User (rotacion de refresh y deteccion de robo).</summary>
public sealed class UserAggregateTests
{
    private static readonly DateTime Now = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan RefreshLifetime = TimeSpan.FromDays(30);

    [Fact]
    public void Register_LeavesEmailUnconfirmed_AndRaisesEvent()
    {
        User user = CreateUser();

        user.EmailConfirmed.Should().BeFalse();
        user.IsActive.Should().BeTrue();
        user.DomainEvents.Should().ContainSingle(domainEvent => domainEvent is UserRegisteredDomainEvent);
    }

    [Fact]
    public void RotateRefreshToken_WithValidToken_RevokesOldAndIssuesNew()
    {
        User user = CreateUser();
        user.IssueRefreshToken("hash-1", Now, RefreshLifetime, null);

        var result = user.RotateRefreshToken("hash-1", "hash-2", Now.AddMinutes(1), RefreshLifetime, null);

        result.IsSuccess.Should().BeTrue();
        user.RefreshTokens.Should().HaveCount(2);
        user.RefreshTokens.Single(token => token.TokenHash == "hash-1").IsRevoked.Should().BeTrue();
        user.RefreshTokens.Single(token => token.TokenHash == "hash-2").IsRevoked.Should().BeFalse();
    }

    [Fact]
    public void RotateRefreshToken_ReusingRevokedToken_DetectsTheftAndRevokesEntireChain()
    {
        User user = CreateUser();
        user.IssueRefreshToken("hash-1", Now, RefreshLifetime, null);
        user.RotateRefreshToken("hash-1", "hash-2", Now.AddMinutes(1), RefreshLifetime, null);

        var reuse = user.RotateRefreshToken("hash-1", "hash-3", Now.AddMinutes(2), RefreshLifetime, null);

        reuse.IsFailure.Should().BeTrue();
        reuse.Error.Should().Be(TokenErrors.ReuseDetected);
        user.RefreshTokens.Where(token => !token.IsRevoked).Should().BeEmpty();
    }

    [Fact]
    public void RotateRefreshToken_WithUnknownToken_ReturnsInvalid()
    {
        User user = CreateUser();

        var result = user.RotateRefreshToken("unknown", "new", Now, RefreshLifetime, null);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(TokenErrors.InvalidRefresh);
    }

    [Fact]
    public void ConfirmEmail_WithValidToken_ConfirmsAndRaisesEvent()
    {
        User user = CreateUser();
        user.AddEmailConfirmationToken("token-hash", Now, TimeSpan.FromHours(24));
        user.ClearDomainEvents();

        var result = user.ConfirmEmail("token-hash", Now.AddHours(1));

        result.IsSuccess.Should().BeTrue();
        user.EmailConfirmed.Should().BeTrue();
        user.DomainEvents.Should().ContainSingle(domainEvent => domainEvent is EmailConfirmedDomainEvent);
    }

    [Fact]
    public void ConfirmEmail_WithExpiredToken_Fails()
    {
        User user = CreateUser();
        user.AddEmailConfirmationToken("token-hash", Now, TimeSpan.FromHours(24));

        var result = user.ConfirmEmail("token-hash", Now.AddHours(25));

        result.IsFailure.Should().BeTrue();
        user.EmailConfirmed.Should().BeFalse();
    }

    [Fact]
    public void ChangePassword_RevokesAllActiveRefreshTokens()
    {
        User user = CreateUser();
        user.IssueRefreshToken("hash-1", Now, RefreshLifetime, null);
        user.IssueRefreshToken("hash-2", Now, RefreshLifetime, null);

        user.ChangePassword("new-hash", Now.AddMinutes(1));

        user.PasswordHash.Should().Be("new-hash");
        user.RefreshTokens.Where(token => !token.IsRevoked).Should().BeEmpty();
    }

    private static User CreateUser()
    {
        Email email = Email.Create("user@rappix.test").Value;
        return User.Register(email, phoneNumber: null, "password-hash", "First", "Last", UserType.Customer, Now);
    }
}
