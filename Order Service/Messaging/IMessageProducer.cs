namespace Order_Service.Messaging
{
    public interface IMessageProducer
    {
        Task PublishAsync(string queueName, string payload);
    }
}
