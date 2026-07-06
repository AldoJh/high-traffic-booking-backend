using System;

namespace ScalableNotification.Api.Models
{
    public class Notificationlogs
    {
        public int Id { get; set; }
        public string? Recipient { get; set; } = string.Empty;
        public string? Message { get; set; } = string.Empty;
        public string? Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }
}