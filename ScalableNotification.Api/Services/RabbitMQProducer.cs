using System.Text;
using System.Text.Json;
using RabbitMQ.Client;

namespace ScalableNotification.Api.Services
{
    public class RabbitMQProducer : IRabbitMQProducer
    {
        private readonly IConfiguration _configuration;

        public RabbitMQProducer(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public void SendNotificationMessage<T>(T message)
        {
            // Ambil konfigurasi dari appsettings.json
            var factory = new ConnectionFactory
            {
                HostName = _configuration["RabbitMQ:Host"],
                UserName = _configuration["RabbitMQ:Username"],
                Password = _configuration["RabbitMQ:Password"]
            };

            // Buka koneksi ke RabbitMQ Docker
            using var connection = factory.CreateConnection();
            using var channel = connection.CreateModel();

            // Buat antrean bernama "notification_queue" jika belum ada
            channel.QueueDeclare(queue: "notification_queue", 
                                 durable: true, 
                                 exclusive: false, 
                                 autoDelete: false, 
                                 arguments: null);

            // Ubah objek menjadi JSON string, lalu konversi ke Byte Array
            var json = JsonSerializer.Serialize(message);
            var body = Encoding.UTF8.GetBytes(json);

            // Kirim pesan ke antrean
            channel.BasicPublish(exchange: "", 
                                 routingKey: "notification_queue", 
                                 basicProperties: null, 
                                 body: body);
        }
    }
}