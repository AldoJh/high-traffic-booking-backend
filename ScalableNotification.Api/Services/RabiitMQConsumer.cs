using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using ScalableNotification.Api.Data;
using ScalableNotification.Api.Models;
using Microsoft.Extensions.Caching.Distributed;

namespace ScalableNotification.Api.Services
{
    public class RabbitMQConsumer : BackgroundService
    {
        private readonly IConnection _connection;
        private readonly IModel _channel;
        private readonly IServiceProvider _serviceProvider;

        public RabbitMQConsumer(IConfiguration configuration, IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;

            var factory = new ConnectionFactory
            {
                HostName = configuration["RabbitMQ:Host"],
                UserName = configuration["RabbitMQ:Username"],
                Password = configuration["RabbitMQ:Password"]
            };

            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();
            
            _channel.QueueDeclare(queue: "notification_queue", 
                                 durable: true, 
                                 exclusive: false, 
                                 autoDelete: false, 
                                 arguments: null);
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            stoppingToken.ThrowIfCancellationRequested();

            var consumer = new EventingBasicConsumer(_channel);
            consumer.Received += async (model, ea) =>
            {
                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);
                
                // Parsing data JSON dari antrean menjadi Objek C#
                var notification = JsonSerializer.Deserialize<Notificationlogs>(message);

                if (notification != null)
                {
                    // Karena BackgroundService bersifat Singleton, kita panggil DB & Redis lewat Scope
                    using var scope = _serviceProvider.CreateScope();
                    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    var cache = scope.ServiceProvider.GetRequiredService<IDistributedCache>();

                    // 1. Ubah status dan simpan ke PostgreSQL
                    notification.Status = "Sent via Worker";
                    context.Notificationlogs.Add(notification);
                    await context.SaveChangesAsync();

                    // 2. Set/Update ke Redis Cache agar sinkron saat di-GET
                    string cacheKey = $"notification-{notification.Id}";
                    var cacheOptions = new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5) };
                    await cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(notification), cacheOptions);

                    Console.WriteLine($"[Worker] Sukses memproses notifikasi ke ID: {notification.Id}");
                }

                // Beri tahu RabbitMQ bahwa pesan sukses diproses dan boleh dihapus dari antrean
                _channel.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false);
            };

            _channel.BasicConsume(queue: "notification_queue", autoAck: false, consumer: consumer);
            return Task.CompletedTask;
        }

        public override void Dispose()
        {
            _channel.Close();
            _connection.Close();
            base.Dispose();
        }
    }
}