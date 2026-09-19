using System;
using System.Collections.Generic;
using System.Text;

namespace Cosmo.Application.Chat.CreateConversation;

public sealed record CreateConversationResult(Guid ConversationId, string Message);

