using AixecAPI.Data;
using AixecAPI.Hubs;
using AixecAPI.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Después de var builder = WebApplication.CreateBuilder(args);
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.SetIsOriginAllowed(_ => true)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials(); // Necesario para SignalR
    });
});

// Base de datos MySQL desde variables de entorno
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? $"Server={Environment.GetEnvironmentVariable("DB_HOST")};" +
       $"Port=3306;" +
       $"Database={Environment.GetEnvironmentVariable("DB_NAME")};" +
       $"User ID={Environment.GetEnvironmentVariable("DB_USER")};" +
       $"Password={Environment.GetEnvironmentVariable("DB_PASSWORD")};" +
       $"SslMode=Required;";



//var connectionString = $"Server={dbHost};Database={dbName};User={dbUser};Password={dbPass};";
//builder.Services.AddDbContext<AppDbContext>(options =>
//  options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));

// JWT
var jwtKey = builder.Configuration["Jwt:Key"]!;
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };

        // Permitir JWT en WebSockets (SignalR)
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var token = context.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(token))
                    context.Token = token;
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddScoped<JwtService>();
builder.Services.AddControllers();
builder.Services.AddSignalR();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        ServerVersion.AutoDetect(builder.Configuration.GetConnectionString("DefaultConnection"))
    )
);

var app = builder.Build();



// Aplicar migraciones automáticamente al arrancar

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<GameHub>("/ws/game");

app.Run($"http://0.0.0.0:{Environment.GetEnvironmentVariable("PORT") ?? "5000"}");