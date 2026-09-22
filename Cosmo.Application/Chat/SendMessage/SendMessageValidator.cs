using FluentValidation;

namespace Cosmo.Application.Chat.SendMessage;

public sealed class SendMessageValidator : AbstractValidator<SendMessageCommand>
{
    public SendMessageValidator()
    {
        RuleFor(command => command.Message)
            .NotNull()
            .MaximumLength(64_000);
    }
}
