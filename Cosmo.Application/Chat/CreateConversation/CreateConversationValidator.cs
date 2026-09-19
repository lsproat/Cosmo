using FluentValidation;

namespace Cosmo.Application.Chat.CreateConversation;

public sealed class CreateConversationValidator : AbstractValidator<CreateConversationCommand>
{
    public CreateConversationValidator()
    {
        RuleFor(command => command.Message)
            .NotNull()
            .MaximumLength(64_000);
    }
}