using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Distributed;
using ScalableNotification.Api.Data;
using ScalableNotification.Api.Models;
using ScalableNotification.Api.Services;
using System.Text.Json;

namespace ScalableNotification.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class NotificationController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IDistributedCache _cache;
        private readonly IRabbitMQProducer _rabbitMQProducer;

        public NotificationController(AppDbContext context, IDistributedCache cache, IRabbitMQProducer rabbitMQProducer)
        {
            _context = context;
            _cache = cache;
            _rabbitMQProducer = rabbitMQProducer; // Inisialisasi RabbitMQProducer
        }

        // 1. Endpoint untuk kirim notifikasi (Simpan ke Postgres)
       [HttpPost]
        public IActionResult CreateNotification([FromBody] Notificationlogs notification)
        {
            notification.CreatedAt = DateTime.UtcNow;
            notification.Status = "Queued in RabbitMQ";

            // Lepas beban database, kirim langsung ke antrean RabbitMQ!
            _rabbitMQProducer.SendNotificationMessage(notification);

            return Ok(new { message = "Notification successfully queued!", status = notification.Status });
        }

        // 2. Endpoint performa tinggi (Cek Redis dulu, kalau zonk baru ke Postgres)
        [HttpGet("{id}")]
        public async Task<IActionResult> GetNotification(int id)
        {
            string cacheKey = $"notification-{id}";
            
            // Cek di Redis Cache
            var cachedData = await _cache.GetStringAsync(cacheKey);
            if (!string.IsNullOrEmpty(cachedData))
            {
                var notificationCache = JsonSerializer.Deserialize<Notificationlogs>(cachedData);
                return Ok(new { source = "Redis Cache (Super Fast!)", data = notificationCache });
            }

            // Jika tidak ada di Redis, cari ke PostgreSQL
            var notification = await _context.Notificationlogs.FindAsync(id);
            if (notification == null) return NotFound();

            // Simpan ke Redis selama 5 menit biar request berikutnya cepat
            var cacheOptions = new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5) };
            await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(notification), cacheOptions);

            return Ok(new { source = "PostgreSQL Database", data = notification });
        }
    }
}