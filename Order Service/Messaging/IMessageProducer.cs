namespace Order_Service.Messaging
{
    public interface IMessageProducer
    {
        void SendMessageAsync<T>(T message);
    }
}
