using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System.ServiceModel.Syndication;
using System.Xml;

namespace TGbot_RssFeed.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class RSSController : Controller
    {
        [HttpGet]
        public string Index()
        {
            string url = "https://www.moea.gov.tw/MNS/doit/news/NewsRSSdetail.aspx?sno=31&Kind=1";
            XmlReader reader = XmlReader.Create(url);
            SyndicationFeed feed = SyndicationFeed.Load(reader);
            reader.Close();
            var RssList = new Dictionary<string, Uri>();
            foreach (SyndicationItem item in feed.Items)
            {
                RssList.Add(item.Title.Text, item.Links[0].Uri);
            }
            return JsonConvert.SerializeObject(RssList, Newtonsoft.Json.Formatting.Indented);
        }
    }
}
