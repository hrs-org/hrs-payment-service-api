using System.Text;
using FluentValidation;
using HRS.API.Filters;
using HRS.API.Middleware;
using HRS.API.Services;
using HRS.API.Services.Interfaces;
using HRS.API.Validators.Payment;
using HRS.Domain.Interfaces;
using HRS.Infrastructure;
using HRS.Infrastructure.Repositories;
using HRS.Shared.Core.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using MongoDB.Driver;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.AddScoped<IAppConfiguration, AppConfiguration>();
builder.Services.AddScoped<IUserContextService, UserContextService>();
builder.Services.AddHttpContextAccessor();

// ----------------------------
// IConfiguration & MongoClient
// ----------------------------

builder.Services.AddSingleton<IMongoClient>(sp =>
{
    var connectionString = sp.GetRequiredService<IConfiguration>()
                             .GetConnectionString("DefaultConnection");
    return new MongoClient(connectionString);
});

// MongoContext
builder.Services.AddSingleton<MongoContext>();
builder.Services.AddScoped<IPaymentRepository, PaymentRepository>();
builder.Services.AddScoped(typeof(ICrudRepository<>), typeof(CrudRepository<>));

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddControllers(options => { options.Filters.Add<ValidationFilter>(); });
builder.Services.AddValidatorsFromAssemblyContaining<PaymentRequestDtoValidator>();
builder.Services.AddValidatorsFromAssemblyContaining<VerifyPaymentRequestDtoValidator>();
builder.Services.AddHttpContextAccessor();

builder.Services.AddHttpClient("RentalOrderService", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["RentalOrderService"]!);
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "HRS API-Payment", Version = "v1" });

    // 🔑 Enable JWT Bearer in Swagger
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter 'Bearer' followed by your JWT token.\n\nExample: **Bearer eyJhbGciOi...**"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            []
        }
    });
});


builder.Services.AddAutoMapper(cfg => { }, typeof(Program));

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowWebClient", policy =>
        policy.WithOrigins(
                builder.Configuration["AllowedOrigins"]?.Split(',') ?? Array.Empty<string>()
            )
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials()
    );
});

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseSwagger();
if (app.Environment.IsDevelopment()) app.UseSwaggerUI();

app.UseHttpsRedirection();

app.UseMiddleware<ExceptionMiddleware>();
app.UseCors("AllowWebClient");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
