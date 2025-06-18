using GDC.Copilio.Entities.Models;
using GDC.Copilio.Schema;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;


namespace GDC.Copilio.Business.Abstractions
{
    public interface IConversationService
    {
        Task<ChatMessageContent> ConversationHandler(ChatRequestPayload request);

      
    }
}
