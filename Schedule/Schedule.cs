using Newtonsoft.Json;
using Quartz;
using System.Configuration;
using System.ServiceModel.Syndication;
using System.Threading;
using System.Xml;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using TGbot_RssFeed.Models;
using Formatting = Newtonsoft.Json.Formatting;


namespace TGbot_RssFeed
{
    [DisallowConcurrentExecution]
    public class Schedule : IJob
    {
        private readonly string SavePath = Path.Combine(Environment.CurrentDirectory, "Sub");
        private ITelegramBotClient botClient;
        TimeZoneInfo targetTimeZone;
        public Schedule(ITelegramBotClient botClient)
        {
            this.botClient = botClient;
            targetTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Taipei Standard Time");
        }
        public async Task Execute(IJobExecutionContext context)
        {
            try
            {
                Console.WriteLine($"啟動排程-{DateTime.Now}");
                var UserPath = Path.Combine(SavePath, "UserList.txt"); ;
                HashSet<UserList> UserList = new HashSet<UserList>();
                if (!System.IO.File.Exists(UserPath)) return;

                string Readfile = await System.IO.File.ReadAllTextAsync(UserPath);
                UserList = JsonConvert.DeserializeObject<HashSet<UserList>>(Readfile);
                var InlineList = new List<IEnumerable<InlineKeyboardButton>>();

                foreach (var u in UserList)
                {
                    var Subpath = Path.Combine(SavePath, $"{u.id}.txt");
                    List<UserSubList> userSubLists = new List<UserSubList>();
                    if (!System.IO.File.Exists(UserPath)) return;

                    string file = await System.IO.File.ReadAllTextAsync(Subpath);
                    userSubLists = JsonConvert.DeserializeObject<List<UserSubList>>(file);

                    foreach (var ul in userSubLists)
                    {
                        string url = ul.url;
                        XmlReader reader = XmlReader.Create(url);
                        SyndicationFeed feed = SyndicationFeed.Load(reader);
                        reader.Close();

                        //判斷通知時間為
                        foreach (var item in feed.Items.Where(o => o.PublishDate.DateTime >= ul.updateTime).ToList())
                        {
                            InlineList.Add(new[] { InlineKeyboardButton.WithUrl(item.Title.Text, item.Links[0].Uri.ToString()) });
                        }

                        if(InlineList.Count > 0)
                        {
                            InlineKeyboardMarkup inlineKeyboard = new(InlineList);
                            await botClient.SendTextMessageAsync(
                                chatId: u.id,
                                text: @$"{feed.Title.Text}⚡️",
                                replyMarkup: inlineKeyboard
                                );
                            InlineList.Clear();
                        }
                        //更新通知時間
                        ul.updateTime = TimeZoneInfo.ConvertTime(DateTime.Now, targetTimeZone);
                    }
                    //回寫至文字檔
                    var JsubList = JsonConvert.SerializeObject(userSubLists, Formatting.Indented);
                    using (StreamWriter writer = new StreamWriter(Subpath))
                    {
                        await writer.WriteLineAsync(JsubList);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
            return ;
        }
    }
}