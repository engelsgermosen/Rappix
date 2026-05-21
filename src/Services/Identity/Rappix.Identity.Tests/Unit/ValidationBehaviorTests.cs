using FluentAssertions;
using FluentValidation;
using MediatR;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.Identity.Application.Behaviors;

namespace Rappix.Identity.Tests.Unit;

/// <summary>Pruebas del comportamiento de validacion de MediatR (devuelve Result de fallo, no lanza).</summary>
public sealed class ValidationBehaviorTests
{
    [Fact]
    public async Task Handle_InvalidRequest_ReturnsValidationFailure_WithoutCallingNext()
    {
        var behavior = new ValidationBehavior<DummyRequest, Result<string>>([new DummyValidator()]);
        bool nextCalled = false;
        Task<Result<string>> Next()
        {
            nextCalled = true;
            return Task.FromResult(Result.Success("ok"));
        }

        Result<string> result = await behavior.Handle(new DummyRequest(string.Empty), Next, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        nextCalled.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ValidRequest_CallsNext()
    {
        var behavior = new ValidationBehavior<DummyRequest, Result<string>>([new DummyValidator()]);

        Result<string> result = await behavior.Handle(
            new DummyRequest("valid"),
            () => Task.FromResult(Result.Success("ok")),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("ok");
    }

    private sealed record DummyRequest(string Name) : IRequest<Result<string>>;

    private sealed class DummyValidator : AbstractValidator<DummyRequest>
    {
        public DummyValidator() => RuleFor(request => request.Name).NotEmpty();
    }
}
