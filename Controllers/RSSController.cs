using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System.ServiceModel.Syndication;
using System.Text;
using System.Xml;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using Telegram.Bot.Types;
using TGbot_RssFeed.Models;
using Formatting = Newtonsoft.Json.Formatting;
using System.Threading;
using System.Security.Cryptography.X509Certificates;

namespace TGbot_RssFeed.Controllers
{
    [ApiController]
    [Route("[controller]/[Action]")]
    public class RSSController : Controller
    {
        private Subscription sub;
        private UserSubList subList;
        private UserList user;
        IConfiguration conf;
        ITelegramBotClient botClient;
        CancellationTokenSource cts;
        private readonly string SavePath = @"./Sub";

        #region 基本參數

        //Time
        int year;
        int month;
        int day;
        int hour;
        int minute;
        int second;

        //Messages and user info
        long chatId = 0;
        string messageText;
        int messageId;
        string firstName;
        string lastName;
        long id;
        Message sentMessage;
        #endregion

        public RSSController(Subscription sub, UserSubList subList, UserList user, IConfiguration conf )
        {
            this.sub = sub;
            this.subList = subList;
            this.user = user;
            this.conf = conf;

            #region 設定TG BOT
            botClient = new TelegramBotClient(conf["APIkey"]);
            // Bot StartReceiving, does not block the caller thread. Receiving is done on the ThreadPool.
            cts = new CancellationTokenSource();
            var receiverOptions = new ReceiverOptions
            {
                AllowedUpdates = { } // receive all update types
            };
            botClient.StartReceiving(
                HandleUpdateAsync,
                HandleErrorAsync,
                receiverOptions,
                cancellationToken: cts.Token);
            #endregion
        }

        [HttpPost]
        public async Task<ActionResult> Insert(Subscription sub)
        {
            #region 讀取id資料
            var UserPath = $"{SavePath}\\UserList.txt";
            if (!System.IO.File.Exists(UserPath))
                await System.IO.File.Create(UserPath).DisposeAsync();
            List<UserList> UserList = new List<UserList>();
            //讀取現有RSS清單
            var list = await ReadUserList(UserPath, sub.id);
            //判斷是否為舊有id
            if (list != null) UserList = list;
            UserList.Add(new UserList { id = sub.id });
            // 寫入資料到檔案
            using (StreamWriter writer = new StreamWriter(UserPath))
            {
                await writer.WriteLineAsync(JsonConvert.SerializeObject(UserList, Formatting.Indented));
            }
            #endregion

            #region 讀取id訂閱資料
            var subpath = $"{SavePath}\\{sub.id}.txt";
            if (!System.IO.File.Exists(subpath))
                await System.IO.File.Create(subpath).DisposeAsync();

            List<UserSubList> subRssList = new List<UserSubList>();
            //讀取現有RSS清單
            var record = await ReadSubRssList(subpath, sub.id);
            //判斷是否為舊有id
            if (record != null) subRssList = record;
            //判斷是否已訂閱
            if (subRssList.Any(x => x.url == sub.url)) return NotFound("當前網站已訂閱");

            #endregion

            //寫入當前資料
            subRssList.Add(new UserSubList { url = sub.url, updateTime = DateTime.Now });
            //轉換成json格式
            var JsubList = JsonConvert.SerializeObject(subRssList, Formatting.Indented);

            // 寫入資料到檔案
            using (StreamWriter writer = new StreamWriter(subpath))
            {
                await writer.WriteLineAsync(JsubList);
            }

            return Ok(JsubList);
        }
        public static async Task<List<UserSubList>> ReadSubRssList(string SavePath, string id)
        {
            List<UserSubList> subRssList = new List<UserSubList>();
            if (System.IO.File.Exists(SavePath))
            {
                string file = await System.IO.File.ReadAllTextAsync(SavePath);
                subRssList = JsonConvert.DeserializeObject<List<UserSubList>>(file);
            }
            return subRssList;
        }
        public static async Task<List<UserList>> ReadUserList(string UserPath, string id)
        {
            List<UserList> userlist = new List<UserList>();
            if (System.IO.File.Exists(UserPath))
            {
                string file = await System.IO.File.ReadAllTextAsync(UserPath);
                userlist = JsonConvert.DeserializeObject<List<UserList>>(file);
            }
            return userlist;
        }

        [HttpGet]
        public async Task<ActionResult> Notify(UserList user)
        {
            var UserPath = $"{SavePath}\\UserList.txt";
            List<UserSubList> UserList = new List<UserSubList>();
            if (System.IO.File.Exists(SavePath))
            {
                string file = await System.IO.File.ReadAllTextAsync(SavePath);
                UserList = JsonConvert.DeserializeObject<List<UserSubList>>(file);
            }
           
            foreach(var u in UserList)
            {
                var Subpath = $"{SavePath}\\{u.id}.txt";
                List<UserSubList> userSubLists = new List<UserSubList>();
                if (System.IO.File.Exists(UserPath))
                {
                    string file = await System.IO.File.ReadAllTextAsync(Subpath);
                    userSubLists = JsonConvert.DeserializeObject<List<UserSubList>>(file);
                }

                foreach(var ul in userSubLists)
                {
                    string url = ul.url;
                    XmlReader reader = XmlReader.Create(url);
                    SyndicationFeed feed = SyndicationFeed.Load(reader);
                    reader.Close();
                    foreach (var item in feed.Items.Where(o => o.PublishDate >= DateTime.Now).ToList())
                    {
                        //更新通知時間
                        ul.updateTime = DateTime.Now;

                        //轉換成台灣+8 時區
                        TimeZoneInfo TimeZone = TimeZoneInfo.FindSystemTimeZoneById("Taipei Standard Time");
                        DateTimeOffset convertedTime = TimeZoneInfo.ConvertTime(item.PublishDate, TimeZone);

                        botClient.SendTextMessageAsync(
                        chatId: chatId,
                        text: @$"⚡️{item.Summary}",
                        parseMode: ParseMode.Html
                        );
                    }
                }
                //回寫至文字檔
                var JsubList = JsonConvert.SerializeObject(userSubLists, Formatting.Indented);
                using (StreamWriter writer = new StreamWriter(Subpath))
                {
                    await writer.WriteLineAsync(JsubList);
                }
            }

            return Ok();
        }

        async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            try
            {
                if (update.Type != UpdateType.Message || update.Message!.Type != MessageType.Text) return;

                #region 初始化參數
                chatId = update.Message.Chat.Id;
                messageText = update.Message.Text;
                messageId = update.Message.MessageId;
                firstName = update.Message.From.FirstName;
                lastName = update.Message.From.LastName;
                id = update.Message.From.Id;
                year = update.Message.Date.Year;
                month = update.Message.Date.Month;
                day = update.Message.Date.Day;
                hour = update.Message.Date.Hour;
                minute = update.Message.Date.Minute;
                second = update.Message.Date.Second;

                Console.WriteLine(" message --> " + year + "/" + month + "/" + day + " - " + hour + ":" + minute + ":" + second);
                Console.WriteLine($"Received a '{messageText}' message in chat {chatId} from user:\n" + firstName + " - " + lastName + " - " + " 5873853");

                messageText = messageText.ToLower();
                #endregion

                if (messageText == "/start" || messageText == "hello")
                {
                    // Echo received message text
                    sentMessage = await botClient.SendTextMessageAsync(
                    chatId: chatId,
                    text: "Hello " + firstName + " " + lastName + "",
                    cancellationToken: cancellationToken);
                }
            }
            catch (ApiRequestException ex)
            {
                Console.WriteLine(ex.Message);
                sentMessage = await botClient.SendTextMessageAsync(
                        chatId: chatId,
                        text: $"錯誤：{ex.Message}",
                        cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                sentMessage = await botClient.SendTextMessageAsync(
                        chatId: chatId,
                        text: $"錯誤：{ex.Message}",
                        cancellationToken: cancellationToken);
            }
        }

        Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
        {
            var ErrorMessage = exception switch
            {
                ApiRequestException apiRequestException
                    => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
                _ => exception.ToString()
            };
            Console.WriteLine(ErrorMessage);
            return Task.CompletedTask;
        }


    }
}
