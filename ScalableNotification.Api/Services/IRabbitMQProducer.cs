namespace ScalableNotification.Api.Services
{
    public interface IRabbitMQProducer
    {
        void SendNotificationMessage<T>(T message);
    }
}