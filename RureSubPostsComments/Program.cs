using Confluent.Kafka;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;
using RureSubPostsComments.Models;
using RureSubPostsComments.Services;
using RureSubPostsComments.Workers;
using StackExchange.Redis;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddHostedService<CommentLikedWorker>();
builder.Services.AddHostedService<PostDeletedWorker>();

#region Kafka

var kafkaBootstrapServers = builder.Configuration["Kafka:BootstrapServers"];
var kafkaGroupId = builder.Configuration["Kafka:GroupId"];

if (string.IsNullOrEmpty(kafkaBootstrapServers) || string.IsNullOrEmpty(kafkaGroupId))
{
    throw new Exception("Kafka was not configured!");
}

var consumerConfig = new ConsumerConfig
{
    BootstrapServers = kafkaBootstrapServers,
    GroupId = kafkaGroupId,
    EnableAutoCommit = false,
    EnableAutoOffsetStore = false,
    AutoOffsetReset = AutoOffsetReset.Earliest
};
var producerConfig = new ProducerConfig
{
    BootstrapServers = kafkaBootstrapServers,
};

builder.Services.AddSingleton(consumerConfig);
builder.Services.AddSingleton(producerConfig);

#endregion

#region MongoDb

builder.Services.Configure<MongoDbSettings>(builder.Configuration.GetSection("MongoDb"));

builder.Services.AddSingleton<IMongoClient>(sp =>
{
    var settings = sp.GetRequiredService<IOptions<MongoDbSettings>>().Value;

    return new MongoClient(settings.ConnectionString);
});

builder.Services.AddSingleton<IMongoDbService, MongoDbService>();

builder.Services.AddHostedService<MongoDbInitializer>();

#endregion

#region Http

var httpProfileApi = builder.Configuration["Http:ProfileApi"];

if (string.IsNullOrEmpty(httpProfileApi))
{
    throw new Exception("Bad configuration! Http:Api is null or empty!");
}

builder.Services.AddHttpClient<IProfileService, HttpProfileService>(client =>
{
    client.BaseAddress = new Uri(httpProfileApi);
});

#endregion

#region Jwt

var jwtKey = builder.Configuration["Jwt:Key"];

if (string.IsNullOrEmpty(jwtKey))
{
    throw new Exception("Jwt was not configured!");
}

var jwtKeyBytes = Encoding.UTF8.GetBytes(jwtKey);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = false,
        ValidateAudience = false,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ClockSkew = TimeSpan.Zero,

        IssuerSigningKey = new SymmetricSecurityKey(jwtKeyBytes)
    };
});

#endregion

#region Redis

var redisConnectionString = builder.Configuration["Redis:ConnectionString"];

if (string.IsNullOrEmpty(redisConnectionString))
{
    throw new Exception("Redis was not configured!");
}

builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    return ConnectionMultiplexer.Connect(redisConnectionString);
});

#endregion

#region Cors

builder.Services.AddCors(options =>
{
    options.AddPolicy("Development", policy =>
    {
        policy.WithOrigins("http://localhost:5173", "http://localhost")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

#endregion

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
    app.UseHttpsRedirection();
}
else
{
    app.UseCors("Development");
}

app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Comments}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();
