using Azure.Core;
using GDC.Copilio.Business.Abstractions;
using GDC.Copilio.Business.Plugins;
using GDC.Copilio.Common;
using GDC.Copilio.Entities.Models;
using GDC.Copilio.Schema;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace GDC.Copilio.Business
{
    public class ConversationService : IConversationService
    {
        private readonly IChatCompletionService _chatService;
        private readonly Kernel _kernel;
        private readonly ConversationDbContext _context;
        private readonly string _documentPath;

        // Initiate Chat Service and DB Context connection
        public ConversationService(IChatCompletionService chatService, Kernel kernel, ConversationDbContext context, IConfiguration configuration)
        {
            Util.Gaurd.ArgumentIsNotNull(chatService, nameof(_chatService));
            Util.Gaurd.ArgumentIsNotNull(kernel, nameof(_kernel));
            Util.Gaurd.ArgumentIsNotNull(context, nameof(_context));

            _chatService = chatService;
            _kernel = kernel;
            _context = context;
            _documentPath = configuration["SupportDocumentPath"] ?? "Documents/wm_county_support 1.md";
        }

        public async Task<ChatMessageContent> ConversationHandler(ChatRequestPayload request)
        {
            // Creation and Authentication of User Conversation History
            var conversation = await _context.ConversationMemory.FindAsync(request.Id);

            if (conversation == null)
            {
                conversation = new Conversation
                {
                    Id = request.Id,
                    UserMessage = request.UserMessage,
                    BotResponse = string.Empty,
                    Timestamp = DateTime.UtcNow
                };
                _context.ConversationMemory.Add(conversation);
            }
            else
            {
                conversation.UserMessage += " " + request.UserMessage;
                conversation.Timestamp = DateTime.UtcNow;
                _context.ConversationMemory.Update(conversation);
            }

            await _context.SaveChangesAsync();

            // Import Document Reader plugin
            var documentPlugin = new DocumentReaderPlugin(_documentPath);
            _kernel.ImportPluginFromObject(documentPlugin, "DocumentPlugin");

            // Initialize Semantic Kernel and return Response
            var chat = new ChatHistory();
            chat.AddSystemMessage(
                @"You are a helpful AI assistant for Westmoreland County technical support.

Your primary role is to help users with technical issues by providing information from the support documentation.

When a user asks a question:
1. Use the DocumentPlugin to search for relevant information in the support documentation
2. If the user specifically asks for escalation or if you cannot solve their issue, use the HandleEscalation function
3. Always be helpful, clear, and follow the step-by-step instructions from the documentation
4. If you cannot find relevant information, politely let them know and offer escalation

Key behaviors:
- For network/connectivity issues, provide the troubleshooting steps from the documentation
- If users ask to escalate or need further help, use the escalation function
- Be conversational but professional
- Focus on solving their technical problems efficiently"
            );

            chat.AddUserMessage(request.UserMessage);

            var settings = new OpenAIPromptExecutionSettings
            {
                ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions
            };

            var response = await _chatService.GetChatMessageContentAsync(chat, settings, _kernel);
            chat.Add(response);

            // Update conversation with bot response
            conversation.BotResponse = response.Content ?? string.Empty;
            _context.ConversationMemory.Update(conversation);
            await _context.SaveChangesAsync();

            return response;
        }
    }
}