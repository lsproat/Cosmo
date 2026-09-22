using Cosmo.Application.Behaviors;
using Cosmo.Application.Chat.CreateConversation;
using Cosmo.Application.Chat.SendMessage;
using FluentValidation;

namespace Cosmo.Tests.Application;

public class ValidationTests
{
    [Theory]
    [InlineData(null, false)]
    [InlineData("", true)]
    [InlineData("   \t\r\n", true)]
    [InlineData("Hello", true)]
    public void Validators_EnforceExistingNullRuleWithoutRejectingEmptyOrWhitespace(string? message, bool valid)
    {
        Assert.Equal(valid, new SendMessageValidator().Validate(new SendMessageCommand(message!)).IsValid);
        Assert.Equal(valid, new CreateConversationValidator().Validate(new CreateConversationCommand(message!)).IsValid);
    }

    [Theory]
    [InlineData(63_999, true)]
    [InlineData(64_000, true)]
    [InlineData(64_001, false)]
    public void Validators_EnforceMaximumMessageLength(int length, bool valid)
    {
        var message = new string('x', length);
        var results = new[]
        {
            new SendMessageValidator().Validate(new SendMessageCommand(message)),
            new CreateConversationValidator().Validate(new CreateConversationCommand(message))
        };

        foreach (var result in results)
        {
            Assert.Equal(valid, result.IsValid);
            if (!valid) Assert.Equal("Message", Assert.Single(result.Errors).PropertyName);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Pipeline_ValidRequestOrNoValidators_CallsNextWithCancellationToken(bool useValidator)
    {
        using var cancellation = new CancellationTokenSource();
        var pipeline = new ValidationBehavior<SendMessageCommand, string>(useValidator ? [new SendMessageValidator()] : []);
        var calls = 0;

        var result = await pipeline.Handle(new SendMessageCommand("Hello"), token =>
        {
            Assert.Equal(cancellation.Token, token);
            calls++;
            return Task.FromResult("Response");
        }, cancellation.Token);

        Assert.Equal("Response", result);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task Pipeline_AggregatesValidatorFailuresWithoutCallingNext()
    {
        var first = new InlineValidator<SendMessageCommand>();
        first.RuleFor(x => x.Message).NotEmpty().WithMessage("Required");
        var second = new InlineValidator<SendMessageCommand>();
        second.RuleFor(x => x.Message).MinimumLength(3).WithMessage("Too short");
        var pipeline = new ValidationBehavior<SendMessageCommand, string>([first, second]);
        var called = false;

        var exception = await Assert.ThrowsAsync<ValidationException>(() => pipeline.Handle(new SendMessageCommand(""), _ =>
        {
            called = true;
            return Task.FromResult("Unexpected");
        }, CancellationToken.None));

        Assert.False(called);
        Assert.Equal(new[] { "Required", "Too short" }, exception.Errors.Select(x => x.ErrorMessage));
    }

    [Fact]
    public async Task Pipeline_PassesCancellationToAsyncValidators()
    {
        using var cancellation = new CancellationTokenSource();
        var validator = new InlineValidator<SendMessageCommand>();
        validator.RuleFor(x => x.Message).MustAsync((_, token) =>
        {
            Assert.Equal(cancellation.Token, token);
            cancellation.Cancel();
            token.ThrowIfCancellationRequested();
            return Task.FromResult(true);
        });
        var pipeline = new ValidationBehavior<SendMessageCommand, string>([validator]);
        var called = false;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pipeline.Handle(new SendMessageCommand("Hello"), _ =>
        {
            called = true;
            return Task.FromResult("Unexpected");
        }, cancellation.Token));

        Assert.False(called);
    }
}
