using API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace API.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class ChatController : ControllerBase
    {
        private readonly IChatCompletionService _chatService;
        private readonly Kernel _kernel;
        private readonly ConversationDbContext _context;

        // Constructor to initialize dependencies
        public ChatController(IChatCompletionService chatService, Kernel kernel, ConversationDbContext context)
        {
            _chatService = chatService;
            _kernel = kernel;
            _context = context;
        }

        // POST endpoint to handle chat requests
        [HttpPost("ask/{id}")]
        public async Task<IActionResult> Ask([FromBody] ChatRequest request, int id)
        {
            // Validate the request payload
            if (request == null || string.IsNullOrEmpty(request.UserMessage))
            {
                return BadRequest(new { Message = "The UserMessage field is required." });
            }

            // Try to find existing conversation context by ID
            var context = await _context.ConversationContexts.FindAsync(id);

            if (context == null)
            {
                // If no context exists, create a new one
                context = new ConversationContext
                {
                    Id = id,
                    UserMessage = request.UserMessage,
                    BotResponse = string.Empty,
                    Timestamp = DateTime.UtcNow
                };
                _context.ConversationContexts.Add(context);
            }
            else
            {
                // If context exists, update it with the new user message
                context.UserMessage += " " + request.UserMessage;
                context.Timestamp = DateTime.UtcNow;
                _context.ConversationContexts.Update(context);
            }

            // Save changes to the database
            await _context.SaveChangesAsync();

            // Initialize chat history with system and user messages
            var chat = new ChatHistory();
            chat.AddSystemMessage($"You are a text service desk assistant. People text you—assist them in what ever way you can. Including searching up the answer.Here is the historical context of the chat -> {context.UserMessage}");
            chat.AddUserMessage(request.UserMessage);

            // Configure settings for OpenAI prompt execution
            var settings = new OpenAIPromptExecutionSettings
            {
                ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions
            };

            // Get the chat response from the service
            var response = await _chatService.GetChatMessageContentAsync(chat, settings, _kernel);
            chat.Add(response);

            // Return the response to the client
            return Ok(response.Items);
        }
    }

    // Model representing the chat request payload
    public class ChatRequest
    {
        public string UserMessage { get; set; }
    }
}

