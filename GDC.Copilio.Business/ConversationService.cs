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
using HtmlAgilityPack;
using System.Web;
using System.Text;

namespace GDC.Copilio.Business
{
    public class ConversationService : IConversationService
    {

        private readonly IChatCompletionService _chatService;
        private readonly Kernel _kernel;
        private readonly ConversationDbContext _context;
        private readonly HttpClient _httpClient;


        // Initiate Chat Service and DB Context connection
        public ConversationService(IChatCompletionService chatService, Kernel kernel, ConversationDbContext context, HttpClient httpClient)
        {
            Util.Gaurd.ArgumentIsNotNull(chatService, nameof(_chatService));
            Util.Gaurd.ArgumentIsNotNull(kernel, nameof(_kernel));
            Util.Gaurd.ArgumentIsNotNull(context, nameof(_context));
            Util.Gaurd.ArgumentIsNotNull(httpClient, nameof(httpClient));

            _chatService = chatService;
            _kernel = kernel;
            _context = context;
            _httpClient = httpClient;
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

            // Step 1: Generate optimized search query using LLM
            var searchQuery = await GenerateSearchQuery(request.UserMessage);

            // Step 2: Scrape search results to get URLs
            var searchUrls = await ScrapeSearchResults(searchQuery);

            // Step 3: Scrape content from URLs and let LLM search for relevant information
            var relevantContent = await ProcessUrlsWithLLM(searchUrls, request.UserMessage);

            return relevantContent;
        }

        private async Task<string> GenerateSearchQuery(string userMessage)
        {
            var queryChat = new ChatHistory();
            queryChat.AddSystemMessage(
                @"You are a search query optimizer for the Westmoreland County PA government website. 
                Convert the user's question into the most effective search terms that would find relevant information 
                on a county government website. Keep it concise and focused on key terms.
                
                Examples:
                - User: 'How do I get a marriage license?' -> 'marriage license'
                - User: 'What are the tax rates for property?' -> 'property tax rates'
                - User: 'Where can I vote in the upcoming election?' -> 'voting locations polling places'
                
                Return only the search terms, no additional text."
            );

            queryChat.AddUserMessage($"Convert this question to search terms: {userMessage}");

            var settings = new OpenAIPromptExecutionSettings
            {
                MaxTokens = 50,
                Temperature = 0.1
            };

            var response = await _chatService.GetChatMessageContentAsync(queryChat, settings, _kernel);
            return response.Content?.Trim() ?? userMessage;
        }

        private async Task<List<string>> ScrapeSearchResults(string searchQuery)
        {
            var urls = new List<string>();

            try
            {
                // URL encode the search query
                var encodedQuery = HttpUtility.UrlEncode(searchQuery);
                var searchUrl = $"https://www.westmorelandcountypa.gov/Search/Index?searchPhrase={encodedQuery}";

                // Perform the search
                var searchResponse = await _httpClient.GetStringAsync(searchUrl);

                // Parse HTML to extract URLs
                var doc = new HtmlDocument();
                doc.LoadHtml(searchResponse);

                // Look for search result links - adjust selectors based on actual HTML structure
                var linkNodes = doc.DocumentNode.SelectNodes("//a[@href]");

                if (linkNodes != null)
                {
                    foreach (var link in linkNodes)
                    {
                        var href = link.GetAttributeValue("href", "");

                        // Filter for actual content URLs (not navigation, etc.)
                        if (!string.IsNullOrEmpty(href) &&
                            (href.StartsWith("https://www.westmorelandcountypa.gov/") ||
                             href.StartsWith("/")) &&
                            !href.Contains("/Search/") &&
                            !href.Contains("javascript:") &&
                            !href.Contains("mailto:") &&
                            !href.Contains("#"))
                        {
                            // Convert relative URLs to absolute
                            if (href.StartsWith("/"))
                            {
                                href = "https://www.westmorelandcountypa.gov" + href;
                            }

                            if (!urls.Contains(href))
                            {
                                urls.Add(href);
                            }
                        }
                    }
                }

                // Limit to top 5-10 results to avoid overwhelming the LLM
                return urls.Take(10).ToList();
            }
            catch (Exception ex)
            {
                // Log error and return empty list
                Console.WriteLine($"Error scraping search results: {ex.Message}");
                return new List<string>();
            }
        }

        private async Task<ChatMessageContent> ProcessUrlsWithLLM(List<string> urls, string originalQuestion)
        {
            // Set up Bing connector for searching specific URLs
            var bingConnector = new BingConnector("f960d51145354a3b8f38edcfbec89da5");
            var plugin = new WebSearchEnginePlugin(bingConnector);
            _kernel.ImportPluginFromObject(plugin, "BingPlugin");

            var chat = new ChatHistory();

            // System message for the main conversation
            chat.AddSystemMessage(
                @"You are an AI assistant for Westmoreland County PA government website. 
                You have access to Bing search capabilities to find information from specific URLs.
                
                Here are the relevant URLs from the county website search results:
                " + string.Join("\n", urls) + @"
                
                Your task:
                1. Use Bing search to find the most relevant information from these specific URLs
                2. Search each URL individually using site-specific searches like 'site:URL_HERE [search terms]'
                3. Provide a comprehensive answer based on the information found
                4. Always mention the specific URLs where you found the information
                5. If information is not available, say so clearly."
            );

            // Add the original user question
            chat.AddUserMessage($"Search the provided Westmoreland County URLs to answer this question: {originalQuestion}");

            var settings = new OpenAIPromptExecutionSettings
            {
                ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions,
                MaxTokens = 1000,
                Temperature = 0.3
            };

            // First search to get information from the URLs
            var response = await _chatService.GetChatMessageContentAsync(chat, settings, _kernel);
            chat.Add(response);

            // If needed, do a follow-up search for more specific information
            if (response.Content?.Contains("more information") == true ||
                response.Content?.Contains("not found") == true)
            {
                chat.AddUserMessage("Please search more specifically within those URLs for detailed information that answers the user's question.");
                response = await _chatService.GetChatMessageContentAsync(chat, settings, _kernel);
            }

            return response;
        }

        private async Task<string> ScrapePageContent(string url)
        {
            try
            {
                var pageHtml = await _httpClient.GetStringAsync(url);
                var doc = new HtmlDocument();
                doc.LoadHtml(pageHtml);

                // Extract main content - adjust selectors based on actual site structure
                var contentSelectors = new[]
                {
                    "main",
                    ".content",
                    "#content",
                    ".main-content",
                    "article",
                    ".page-content"
                };

                string content = "";
                foreach (var selector in contentSelectors)
                {
                    var contentNode = doc.DocumentNode.SelectSingleNode($"//{selector}");
                    if (contentNode != null)
                    {
                        content = contentNode.InnerText;
                        break;
                    }
                }

                // Fallback to body if no main content found
                if (string.IsNullOrEmpty(content))
                {
                    var bodyNode = doc.DocumentNode.SelectSingleNode("//body");
                    content = bodyNode?.InnerText ?? "";
                }

                // Clean up the content
                content = System.Text.RegularExpressions.Regex.Replace(content, @"\s+", " ");
                content = content.Trim();

                // Limit content length to avoid token limits
                if (content.Length > 2000)
                {
                    content = content.Substring(0, 2000) + "...";
                }

                return content;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error scraping content from {url}: {ex.Message}");
                return "";
            }
        }
    }
}