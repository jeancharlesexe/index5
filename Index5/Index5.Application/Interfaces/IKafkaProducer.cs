namespace Index5.Application.Interfaces;

public interface IKafkaProducer
{
    Task PublishAsync(string topic, string key, object message);
}
