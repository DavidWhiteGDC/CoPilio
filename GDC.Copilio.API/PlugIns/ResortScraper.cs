using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Linq;


namespace GDC.Copilio.API.PlugIns
{
    public class ResortScraper
    {
        public List<ResortInfo> ScrapeResortInfo(string url)
        {
            var web = new HtmlWeb();
            var doc = web.Load(url);

            var resortInfoList = new List<ResortInfo>();

            // Example: Extract room details
            var roomNodes = doc.DocumentNode.SelectNodes("//div[@class='room-info']"); // Update XPath as needed
            foreach (var node in roomNodes)
            {
                var roomName = node.SelectSingleNode(".//h2")?.InnerText;
                var price = node.SelectSingleNode(".//span[@class='price']")?.InnerText;
                var amenities = node.SelectNodes(".//ul[@class='amenities']/li").Select(li => li.InnerText).ToList();

                resortInfoList.Add(new ResortInfo
                {
                    RoomName = roomName,
                    Price = price,
                    Amenities = amenities
                });
            }

            return resortInfoList;
        }
    }

    public class ResortInfo
    {
        public string RoomName { get; set; }
        public string Price { get; set; }
        public List<string> Amenities { get; set; }
    }
}
