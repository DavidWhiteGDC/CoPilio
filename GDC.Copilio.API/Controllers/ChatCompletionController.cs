using GDC.Copilio.Business.Abstractions;
using GDC.Copilio.Entities.Models;
using GDC.Copilio.Common;
using Microsoft.AspNetCore.Mvc;
using Twilio.AspNet.Common;
using Twilio.AspNet.Core;
using Twilio.TwiML;

namespace GDC.Copilio.API.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class ChatController : TwilioController
    {
        private readonly IConversationService _conversationService;

        public ChatController(IConversationService conversationService)
        {
            Util.Gaurd.ArgumentIsNotNull(conversationService, nameof(conversationService));
            _conversationService = conversationService;
        }

        [HttpPost]
        [Consumes("application/x-www-form-urlencoded")]
        public async Task<IActionResult> Ask([FromForm] SmsRequest incomingMessage)
        {
            ChatRequestPayload chatRequestPayload = new ChatRequestPayload();
            chatRequestPayload.UserMessage = incomingMessage.Body;
            chatRequestPayload.Id = 1;

            try
            {
                var response = await _conversationService.ConversationHandler(chatRequestPayload);
                var messageContent = string.Join("\n", response.Items.Select(item => item)); // Assuming 'Items' is a collection and 'Content' is a property you want to display
                var messagingResponse = new MessagingResponse();
                messagingResponse.Message(messageContent);

                return TwiML(messagingResponse);
            }
            catch (Exception ex)
            {
                // Handle all exceptions
                return StatusCode(500, new { Message = "An unexpected error occurred.", Error = ex.Message });
            }
        }
    }
}

