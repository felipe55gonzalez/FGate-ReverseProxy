using Microsoft.Extensions.Primitives;

namespace FGate.Services
{
    public class ProxyConfigManager
    {
        private CancellationTokenSource _cancellationTokenSource = new();

        public IChangeToken ChangeToken => new CancellationChangeToken(_cancellationTokenSource.Token);
        public void TriggerReload()
        {
            var oldTokenSource = Interlocked.Exchange(ref _cancellationTokenSource, new CancellationTokenSource());
            oldTokenSource.Cancel();
            oldTokenSource.Dispose();
        }
    }
}