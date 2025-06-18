using GDC.Copilio.Business.Abstractions;
using GDC.Copilio.Common;
using GDC.Copilio.Entities.Models;
using GDC.Copilio.Schema;
using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.Plugins.Web;
using Microsoft.SemanticKernel.Plugins.Web.Bing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace GDC.Copilio.Business
{
    public class ConversationService : IConversationService
    {
        private readonly IChatCompletionService _chatService;
        private readonly Kernel _kernel;
        private readonly ConversationDbContext _context;
        private readonly IWebScraperService _scraper;

        public ConversationService(
            IChatCompletionService chatService,
            Kernel kernel,
            ConversationDbContext context,
            IWebScraperService scraper
            )
        {
            Util.Gaurd.ArgumentIsNotNull(chatService, nameof(chatService));
            Util.Gaurd.ArgumentIsNotNull(kernel, nameof(kernel));
            Util.Gaurd.ArgumentIsNotNull(context, nameof(context));

            _chatService = chatService;
            _kernel = kernel;
            _context = context;
            _scraper = scraper;

            // Import BingPlugin one time
            var bingConnector = new BingConnector("f960d51145354a3b8f38edcfbec89da5");
            var bingPlugin = new WebSearchEnginePlugin(bingConnector);
            _kernel.ImportPluginFromObject(bingPlugin, "BingPlugin");
        }

        public async Task<ChatMessageContent> ConversationHandler(ChatRequestPayload request)
        {
            // 1) Save to DB as before…
            var conv = await _context.ConversationMemory.FindAsync(request.Id);
            if (conv == null)
            {
                conv = new Conversation
                {
                    Id = request.Id,
                    UserMessage = request.UserMessage,
                    BotResponse = "",
                    Timestamp = DateTime.UtcNow
                };
                _context.ConversationMemory.Add(conv);
            }
            else
            {
                conv.UserMessage += " " + request.UserMessage;
                conv.Timestamp = DateTime.UtcNow;
                _context.ConversationMemory.Update(conv);
            }
            await _context.SaveChangesAsync();
            const string scrapeTrigger = "scrape ";
            if (!request.UserMessage
                       .StartsWith(scrapeTrigger, StringComparison.OrdinalIgnoreCase))
            {
                var url = request.UserMessage[scrapeTrigger.Length..].Trim();
                if (!Uri.IsWellFormedUriString(url, UriKind.Absolute))
                {
                    return new ChatMessageContent
                    {
                        Content = "That doesn’t look like a valid URL. Please send “scrape https://…”"
                    };
                }

                // call your AngleSharp scraper
                var result = await _scraper.ScrapePageAsync(url);

                // serialize to JSON (or format however you like)
                var json = JsonSerializer.Serialize(result, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                return new ChatMessageContent
                {
                    Content = $"Here are the inputs and links I found on **{url}**:\n\n```json\n{json}\n```"
                };
            }



            var message = (request.UserMessage ?? "")
                 .Trim();       // remove leading/trailing spaces
            Console.WriteLine($"[ConversationHandler] got message: '{message}'");
            // 2) Check for our “search links for {term}” intent
            const string trigger = "search links for ";
            if (request.UserMessage
                       .StartsWith(trigger, StringComparison.OrdinalIgnoreCase))
            {
                var term = request.UserMessage[trigger.Length..].Trim();
                if (string.IsNullOrEmpty(term))
                {
                    return new ChatMessageContent
                    { Content = "Please specify what you want to search for." };
                }

                // a) initial site: query to get top URLs
                var initialQuery = $"site:westmorelandcountypa.gov {term}";
                var rawResults = await _kernel
                    .InvokeAsync<string>("BingPlugin.SearchAsync", initialQuery);

                // b) parse out URLs from the BingPlugin output
                var urls = rawResults
                    .Split('\n')
                    .Where(line => line.Contains("http"))
                    .SelectMany(line =>
                        line.Split(' ')
                            .Where(p => p.StartsWith("http", StringComparison.OrdinalIgnoreCase)))
                    .Distinct()
                    .Take(5)    // limit to top 5 links to avoid spam
                    .ToList();

                if (!urls.Any())
                {
                    return new ChatMessageContent
                    { Content = $"No links found for “{term}” on westmorelandcountypa.gov." };
                }

                // c) loop and get scoped snippets
                var sb = new StringBuilder();
                foreach (var u in urls)
                {
                    var uri = new Uri(u);
                    var scopedQuery = $"site:{uri.Host + uri.AbsolutePath} {term}";
                    var snippet = await _kernel
                        .InvokeAsync<string>("BingPlugin.SearchAsync", scopedQuery);

                    sb
                      .AppendLine($"🔗 {u}")
                      .AppendLine(snippet)
                      .AppendLine();
                }

                return new ChatMessageContent
                {
                    Content = sb.ToString().Trim()
                };
            }

            var chat = new ChatHistory();
            chat.AddSystemMessage(
                "You are a strict web-scraping assistant for https://www.westmorelandcountypa.gov/ and ONLY its pages…"
                /* etc */);
            chat.AddUserMessage(request.UserMessage);

            var settings = new OpenAIPromptExecutionSettings
            {
                ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions
            };
            var response = await _chatService
                .GetChatMessageContentAsync(chat, settings, _kernel);

            chat.Add(response);
            return response;
        }
    }
}
