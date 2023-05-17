namespace TGbot_RssFeed.Models
{
    public class UserSubList
    {
        private static int counter = 0;
        public int id { get; private set; }
        public string url { get; set; }
        public DateTime updateTime { get; set; }
        public UserSubList()
        {
            counter++;
            id = counter;
        }
    }
}
