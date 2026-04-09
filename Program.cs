using AixecAPI.Data;
using AixecAPI.Hubs;
using AixecAPI.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Base de datos MySQL desde variables de entorno
var dbHost = Environment.GetEnvironmentVariable("DB_HOST");
var dbName = Environment.GetEnvironmentVariable("DB_NAME");
var dbUser = Environment.GetEnvironmentVariable("DB_USER");
var dbPass = Environment.GetEnvironmentVariable("DB_PASSWORD");
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")!;

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));



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

var app = builder.Build();

// Aplicar migraciones automáticamente al arrancar


app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<GameHub>("/ws/game");

app.Run($"http://0.0.0.0:{Environment.GetEnvironmentVariable("PORT") ?? "5001"}");