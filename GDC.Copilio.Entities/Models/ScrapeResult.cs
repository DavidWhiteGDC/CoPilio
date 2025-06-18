using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GDC.Copilio.Entities.Models
{
    public class ScrapeResult
    {
        public List<InputField> Inputs { get; set; } = new();
        public List<AnchorLink> Links { get; set; } = new();
    }
}
