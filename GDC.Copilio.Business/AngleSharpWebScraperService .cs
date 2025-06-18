using AngleSharp;
using GDC.Copilio.Business.Abstractions;
using GDC.Copilio.Entities.Models;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GDC.Copilio.Business
{
    public class AngleSharpWebScraperService : IWebScraperService
    {
        private readonly HttpClient _http;

        public AngleSharpWebScraperService(HttpClient http) => _http = http;

        public async Task<ScrapeResult> ScrapePageAsync(string url)
        {
            var html = await _http.GetStringAsync(url);

            // parse
            var config = Configuration.Default;
            var context = BrowsingContext.New(config);
            var document = await context.OpenAsync(req => req.Content(html));

            var inputs = document
              .QuerySelectorAll("input")
              .Select(e => new InputField
              {
                  Id = e.Id,
                  Name = e.GetAttribute("name") ?? "",
                  Type = e.GetAttribute("type") ?? "text",
                  Value = e.GetAttribute("value") ?? ""
              })
              .ToList();

            var links = document
              .QuerySelectorAll("a")
              .Select(a => new AnchorLink
              {
                  Href = a.GetAttribute("href") ?? "",
                  Text = a.TextContent.Trim()
              })
              .ToList();

            return new ScrapeResult { Inputs = inputs, Links = links };
        }
    }
}
