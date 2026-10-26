using System.Threading.Channels;

namespace AuraNova.Infrastructure.Notifications.Email
{
    /// <summary>Cola en memoria: el correo se envía en segundo plano y el cliente no espera al SMTP.</summary>
    public interface IEmailQueue
    {
        void Enqueue(Guid notificationId);
        IAsyncEnumerable<Guid> ReadAllAsync(CancellationToken cancellationToken);
    }

    public class EmailQueue : IEmailQueue
    {
        private readonly Channel<Guid> _channel = Channel.CreateUnbounded<Guid>(new UnboundedChannelOptions { SingleReader = true });

        public void Enqueue(Guid notificationId) => _channel.Writer.TryWrite(notificationId);

        public IAsyncEnumerable<Guid> ReadAllAsync(CancellationToken cancellationToken) => _channel.Reader.ReadAllAsync(cancellationToken);
    }
}
