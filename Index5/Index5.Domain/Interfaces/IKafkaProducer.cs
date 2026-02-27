namespace Index5.Domain.Interfaces;

public interface IKafkaProducer
{
    Task PublishAsync(string topic, string key, object message);
}
