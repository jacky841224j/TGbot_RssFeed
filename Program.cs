using Microsoft.Extensions.DependencyInjection;
using System.Configuration;
using Telegram.Bot;
using TGbot_RssFeed.Controllers;
using TGbot_RssFeed.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
//var temp = builder.Configuration.GetSection("APIkey");
//builder.Services.AddSingleton<ITelegramBotClient>();

builder.Services.AddSingleton<UserSubList>();
builder.Services.AddSingleton<UserList>();
builder.Services.AddSingleton<Subscription>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
