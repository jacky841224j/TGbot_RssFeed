using Microsoft.AspNetCore.Mvc;
using Quartz;
using System.Text;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using TGbot_RssFeed;
using TGbot_RssFeed.Controllers;
using TGbot_RssFeed.Models;

var builder = WebApplication.CreateBuilder(args);

#region 基本參數

//讀取appsettings.json
var APIkey = builder.Configuration["APIkey"];

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

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddQuartzHostedService(options => options.WaitForJobsToComplete = true);
builder.Services.AddQuartz(q =>
{
    //排程執行程式時使用DI注入
    q.UseMicrosoftDependencyInjectionScopedJobFactory();

    var jobKey = new JobKey("Schedule", "TG");

    q.AddJob<Schedule>(opts =>
    {
        opts.WithIdentity(jobKey);
        opts.StoreDurably();
    });

    q.AddTrigger(opts =>
    {
        opts.ForJob(jobKey);
        opts.WithIdentity("ScheduleTrigger", "TG");
        opts.WithSimpleSchedule(x => x.WithIntervalInMinutes(30).RepeatForever());
    });
});

builder.Services.AddSingleton<UserSubList>();
builder.Services.AddSingleton<UserList>();
builder.Services.AddSingleton<RSSController>();

builder.Services.AddSingleton<ITelegramBotClient>(new TelegramBotClient(APIkey));

var app = builder.Build();
var controller = app.Services.GetRequiredService<RSSController>();

#region 設定TG_BOT

var botClient = new TelegramBotClient(APIkey);
var cts = new CancellationTokenSource();
var receiverOptions = new ReceiverOptions
{
    AllowedUpdates = { } // receive all update types
};
botClient.StartReceiving(
    HandleUpdateAsync,
    HandleErrorAsync,
    receiverOptions,
    cancellationToken: cts.Token);

var me = await botClient.GetMeAsync();

Console.WriteLine($"\nHello! I'm {me.Username} and i'm your Bot!");

#endregion

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();
app.MapGet("/", () => $"Hello !");

app.Run();

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

        #endregion

        if (messageText == "/start" || messageText == "hello")
        {
            // Echo received message text
            sentMessage = await botClient.SendTextMessageAsync(
            chatId: chatId,
            text: "Hello " + firstName + " " + lastName + "",
            cancellationToken: cancellationToken);
        }
        else if (messageText.Contains("/list"))
        {
            var result = await controller.Search(chatId);
            if (result is OkObjectResult actionResult)
            {
                List<UserSubList> resultList = (List<UserSubList>)actionResult.Value;
                StringBuilder sb = new StringBuilder();
                resultList.ForEach(item => sb.AppendLine($"{item.id}_{item.name}"));

                sentMessage = await botClient.SendTextMessageAsync(
                    chatId: chatId,
                    text: sb.ToString(),
                    cancellationToken: cancellationToken);
            }
            else
            {
                sentMessage = await botClient.SendTextMessageAsync(
                    chatId: chatId,
                    text: $"訂閱清單為空",
                    cancellationToken: cancellationToken);
            }
        }
        else if (messageText.Split().ToList().Count >= 2)
        {
            var text = messageText.Split().ToList();

            //訂閱
            if (messageText.Contains("/sub"))
            {
                var result = await controller.Insert(chatId, text[1]);
                if (result is OkObjectResult ruselt)
                {
                    sentMessage = await botClient.SendTextMessageAsync(
                        chatId: chatId,
                        text: $"訂閱成功！",
                        cancellationToken: cancellationToken);
                }
                else
                {
                    sentMessage = await botClient.SendTextMessageAsync(
                        chatId: chatId,
                        text: $"訂閱失敗，請檢查網址是否為RSS格式",
                        cancellationToken: cancellationToken);
                }
            }
            else if (messageText.Contains("/del"))
            {
                int num;
                if (int.TryParse(text[1], out num))
                {
                    if (await controller.Delete(chatId, num) is OkResult)
                    {
                        sentMessage = await botClient.SendTextMessageAsync(
                            chatId: chatId,
                            text: $"移除成功！",
                            cancellationToken: cancellationToken);
                        Console.WriteLine($"{chatId}：移除成功");
                    }
                    else
                    {
                        sentMessage = await botClient.SendTextMessageAsync(
                            chatId: chatId,
                            text: $"錯誤：移除失敗",
                            cancellationToken: cancellationToken);
                        Console.WriteLine($"{chatId}：移除失敗");

                    }
                }
                else
                {
                    sentMessage = await botClient.SendTextMessageAsync(
                            chatId: chatId,
                            text: $"錯誤：請輸入要移除的{"}編號{"}",
                            cancellationToken: cancellationToken);
                    Console.WriteLine($"{chatId}：請輸入要移除的{"}編號{"}");

                }
            }
        }
    }
    catch (ApiRequestException ex)
    {
        sentMessage = await botClient.SendTextMessageAsync(
                chatId: chatId,
                text: $"錯誤：{ex.Message}",
                cancellationToken: cancellationToken);
        Console.WriteLine($"{chatId}-錯誤：{ex.Message}");

    }
    catch (Exception ex)
    {
        sentMessage = await botClient.SendTextMessageAsync(
                chatId: chatId,
                text: $"錯誤：{ex.Message}",
                cancellationToken: cancellationToken);
        Console.WriteLine($"{chatId}-錯誤：{ex.Message}");
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
