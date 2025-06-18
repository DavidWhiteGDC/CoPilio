using Azure.Core;
using GDC.Copilio.Business.Abstractions;
using GDC.Copilio.Entities.Models;
using GDC.Copilio.Schema;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using GDC.Copilio.Common;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using System;
using Microsoft.SemanticKernel.Plugins.Web.Bing;
using Microsoft.SemanticKernel.Plugins.Web;


namespace GDC.Copilio.Business
{
    public class ConversationService : IConversationService 
    {
        
        private readonly IChatCompletionService _chatService;
        private readonly Kernel _kernel;
        private readonly ConversationDbContext _context;
   

        // Initiate Chat Service and DB Context connection
        public ConversationService(IChatCompletionService chatService, Kernel kernel, ConversationDbContext context)
        {
            Util.Gaurd.ArgumentIsNotNull(chatService, nameof(_chatService));
            Util.Gaurd.ArgumentIsNotNull(kernel, nameof(_kernel));
            Util.Gaurd.ArgumentIsNotNull(context, nameof(_context));
            
      
            _chatService = chatService;
            _kernel = kernel;
            _context = context;
        
        }



        public async Task<ChatMessageContent> ConversationHandler(ChatRequestPayload request)
        {

           
           
        //    Util.Gaurd.ArgumentIsNotNull(request, nameof(request));
           
            // Creation and Authentication of User Conversation History
            var conversation = await _context.ConversationMemory.FindAsync(request.Id);

            if (conversation == null)
            {
                conversation = new Conversation
                {
                    Id =request.Id,
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


            var bingConnector = new BingConnector("f960d51145354a3b8f38edcfbec89da5");
            var plugin = new WebSearchEnginePlugin(bingConnector);
            _kernel.ImportPluginFromObject(plugin, "BingPlugin");

            // Initilaizes Semantic Kernel and returns Response


            var chat = new ChatHistory();
            chat.AddSystemMessage("You are a strict web-scraping assistant for https://www.westmorelandcountypa.gov/ and ONLY its pages and linked documents.\r\n\r\n1. On each user request, load the site’s homepage or relevant section (e.g. “Voting”).\r\n2. Find the <a> whose link text most closely matches the user’s question.\r\n3. Fetch that page, extract *all* visible text (including URLs, email addresses, and phone numbers).\r\n4. Return *only* that scraped text. Do not summarize—return verbatim.\r\n5. If no matching link exists, reply: “That is out of the scope of my abilities.”\r\n");

            chat.AddUserMessage(request.UserMessage);

            var settings = new OpenAIPromptExecutionSettings
            {
                ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions
            };

            var response = await _chatService.GetChatMessageContentAsync(chat, settings, _kernel);
            chat.Add(response);
            return response;
        }

        
    }
}
