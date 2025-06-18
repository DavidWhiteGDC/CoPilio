using GDC.Copilio.Entities.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GDC.Copilio.Business.Abstractions
{
    public interface IWebScraperService
    {
        Task<ScrapeResult> ScrapePageAsync(string url);
    }
}
