using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.Text;

namespace GDC.Copilio.Business.Plugins
{
    public class DocumentReaderPlugin
    {
        private readonly string _documentContent;
        private readonly string _documentPath;

        public DocumentReaderPlugin(string documentPath)
        {
            _documentPath = documentPath;
            _documentContent = LoadDocumentContent();
        }

        [KernelFunction]
        [Description("Search for information in the support document based on a user's question or issue")]
        public async Task<string> SearchSupportDocument(
            [Description("The user's question or issue they need help with")] string query)
        {
            try
            {
                if (string.IsNullOrEmpty(_documentContent))
                {
                    return "Support document is not available at this time.";
                }

                var queryLower = query.ToLower();
                var lines = _documentContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                var relevantSections = new List<string>();
                var currentSection = new StringBuilder();
                bool foundRelevantSection = false;

                foreach (var line in lines)
                {
                    // Check if this is a header line
                    if (line.StartsWith('#'))
                    {
                        // If we were building a relevant section, add it to results
                        if (foundRelevantSection && currentSection.Length > 0)
                        {
                            relevantSections.Add(currentSection.ToString().Trim());
                        }

                        // Start new section
                        currentSection.Clear();
                        currentSection.AppendLine(line);

                        // Check if this header is relevant to the query
                        foundRelevantSection = line.ToLower().Contains(queryLower) ||
                                             IsQueryRelevantToSection(queryLower, line.ToLower());
                    }
                    else
                    {
                        // Add content to current section
                        currentSection.AppendLine(line);

                        // Check if this content line is relevant
                        if (!foundRelevantSection && line.ToLower().Contains(queryLower))
                        {
                            foundRelevantSection = true;
                        }
                    }
                }

                // Add the last section if it was relevant
                if (foundRelevantSection && currentSection.Length > 0)
                {
                    relevantSections.Add(currentSection.ToString().Trim());
                }

                if (relevantSections.Count == 0)
                {
                    return "I couldn't find specific information about that in the support document. Please try rephrasing your question or contact support for further assistance.";
                }

                return string.Join("\n\n", relevantSections);
            }
            catch (Exception ex)
            {
                return $"Error searching support document: {ex.Message}";
            }
        }

        [KernelFunction]
        [Description("Get the complete support document content")]
        public async Task<string> GetFullDocument()
        {
            try
            {
                if (string.IsNullOrEmpty(_documentContent))
                {
                    return "Support document is not available at this time.";
                }

                return _documentContent;
            }
            catch (Exception ex)
            {
                return $"Error retrieving document: {ex.Message}";
            }
        }

        [KernelFunction]
        [Description("Handle escalation requests by providing the escalation message")]
        public async Task<string> HandleEscalation()
        {
            return "Your request has been escalated to a county rep with ticket 00001234.";
        }

        private string LoadDocumentContent()
        {
            try
            {
                if (File.Exists(_documentPath))
                {
                    return File.ReadAllText(_documentPath);
                }
                else
                {
                    throw new FileNotFoundException($"Document not found at path: {_documentPath}");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Error loading document: {ex.Message}");
            }
        }

        private bool IsQueryRelevantToSection(string query, string sectionHeader)
        {
            // Define keyword mappings for better matching
            var keywordMappings = new Dictionary<string, string[]>
            {
                { "network", new[] { "connectivity", "internet", "connection", "cable" } },
                { "connectivity", new[] { "network", "internet", "connection", "cable" } },
                { "internet", new[] { "network", "connectivity", "connection", "web" } },
                { "connection", new[] { "network", "connectivity", "internet", "cable" } },
                { "escalation", new[] { "escalate", "ticket", "support", "help" } },
                { "escalate", new[] { "escalation", "ticket", "support", "help" } }
            };

            // Check direct match first
            if (sectionHeader.Contains(query))
            {
                return true;
            }

            // Check keyword mappings
            foreach (var mapping in keywordMappings)
            {
                if (query.Contains(mapping.Key))
                {
                    foreach (var relatedKeyword in mapping.Value)
                    {
                        if (sectionHeader.Contains(relatedKeyword))
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }
    }
}