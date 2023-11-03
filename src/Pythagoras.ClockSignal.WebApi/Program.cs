using FluentValidation;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.WindowsServices;
using NLog.Web;
using Pythagoras.ClockSignal.WebApi.Hubs;
using Pythagoras.ClockSignal.WebApi.Services;
using Pythagoras.Infrastructure.Configuration;
using Pythagoras.Infrastructure.CubeClients.ClockSignal;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Pythagoras.ClockSignal.WebApi
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var options = new WebApplicationOptions
            {
                Args = args,
                ContentRootPath = WindowsServiceHelpers.IsWindowsService() ? AppContext.BaseDirectory : default
            };

            var builder = WebApplication.CreateBuilder(options);

            builder.Services.AddHttpLogging(httpLoggingOptions =>
            {
                httpLoggingOptions.LoggingFields =
                    HttpLoggingFields.All;
                //HttpLoggingFields.RequestPath |
                //HttpLoggingFields.RequestMethod |
                //HttpLoggingFields.ResponseStatusCode;
            });

            var customConfiguration = new CustomConfigurationStore<ClockSignalSettings>()
                .SetJsonFilePath(builder.Configuration["CustomSettingsPath"]!);

            // Add services to the container.

            builder.Services.AddControllers().AddJsonOptions(options =>
            {
                // by default
                options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
                options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;

                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            });

            builder.Services.AddSignalR()
                //added by defult
                .AddJsonProtocol(options =>
                {
                    //by default
                    options.PayloadSerializerOptions.PropertyNameCaseInsensitive = true;
                    options.PayloadSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
                })
                //not added by defult
                .AddMessagePackProtocol();

            builder.Services.AddSingleton(customConfiguration);
            builder.Services.AddScoped<IValidator<ClockSignalSettings>, ClockSignalSettingsValidator>();
            builder.Services.AddSingleton<ClockSignalService>();

            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen(options =>
            {
                options.AddSignalRSwaggerGen();
            });

            builder.Logging.ClearProviders();
            builder.Host.UseNLog();

            builder.Services.AddCors(options => options.AddPolicy("AllowAny", builder => builder
                .AllowAnyOrigin()
                .AllowAnyHeader()
                .AllowAnyMethod()));

            var app = builder.Build();

            app.UseHttpLogging();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseHttpsRedirection();

            app.UseAuthorization();

            app.MapControllers();

            app.MapHub<ClockSignalHub>(ClockSignalClient.HUB_URL_PATH, options =>
            {
                // only WebSockets
                options.Transports = HttpTransportType.WebSockets;
            });

            app.Run();
        }
    }
}