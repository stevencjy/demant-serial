using System.Text.RegularExpressions;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// DI add connection pool
var configOptions = new ConfigurationOptions
{
    EndPoints = {"redis:6379"},
    Ssl = false,
    AbortOnConnectFail = false
};
var multiplexer = ConnectionMultiplexer.Connect(configOptions);
builder.Services.AddSingleton<IConnectionMultiplexer>(multiplexer);

var app = builder.Build();

// Configure the HTTP request pipeline.
// date received in UTC format. 2023-05-28T12:34:56Z.
string dateFormatRegex = "^[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}Z$";

var SerialNumberGenerator = (DateTime date, int count) => date.ToString("yyyyMMdd") + count.ToString().PadLeft(6, '0');

app.MapGet("/serialnumber/{date}", async Task<IResult> (IConnectionMultiplexer _redis, string date) => {
    var match = Regex.Match(date, dateFormatRegex, RegexOptions.IgnoreCase);
    if(!match.Success){
        return TypedResults.BadRequest();
    }
    var time = DateTimeOffset.Parse(date).UtcDateTime;
    var startTime = new DateTime(time.Year, time.Month, time.Day, 0, 0, 0, DateTimeKind.Utc);
 
    /* disable access to previous day serial number count
    //var todayTime = DateTime.UtcNow;
    //var todayStart = new DateTime(todayTime.Year, todayTime.Month, todayTime.Day, 0, 0, 0, DateTimeKind.Utc);
    //if(todayStart.Subtract(startTime).TotalSeconds != 0){
    //    return TypedResults.BadRequest();
    } */

    //use start date unix timestamp as key 
    var redisKey = startTime.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
    //DI
    var db = _redis.GetDatabase();
    // atomic increment
    var result = await db.StringIncrementAsync(redisKey.ToString(), 1, CommandFlags.PreferMaster);
    return TypedResults.Ok(SerialNumberGenerator(startTime, int.Parse(result.ToString())));
});

app.Run();