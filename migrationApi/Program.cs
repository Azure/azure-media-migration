using Azure.Identity;
using Azure.Messaging.ServiceBus;
using migrationApi.Services;

namespace migrationApi
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.

            builder.Services.AddControllers();
            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            builder.Services.AddCors(p => p.AddPolicy("corsapp", builder =>
            {
                builder.WithOrigins("*").AllowAnyMethod().AllowAnyHeader();
            }));

            builder.Services.AddSingleton(serviceProvider =>
            {
                var configuration = serviceProvider.GetRequiredService<IConfiguration>();
                return new ServiceBusClient(configuration.GetValue<string>("SERVICEBUS_NAMESPACE"), new DefaultAzureCredential(), new ServiceBusClientOptions
                {
                    TransportType = ServiceBusTransportType.AmqpWebSockets
                });
            });

            builder.Services.AddScoped<ServiceBusService>();

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseHttpsRedirection();

            app.UseAuthorization();

            app.UseCors("corsapp");

            app.MapControllers();

            app.Run();
        }
    }
}
