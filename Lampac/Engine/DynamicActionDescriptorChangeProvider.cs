using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.Primitives;
using System.Threading;

namespace Lampac.Engine
{
    public class DynamicActionDescriptorChangeProvider : IActionDescriptorChangeProvider
    {
        public static DynamicActionDescriptorChangeProvider Instance { get; } = new DynamicActionDescriptorChangeProvider();

        private CancellationTokenSource tokenSource = new CancellationTokenSource();
        public CancellationTokenSource TokenSource => tokenSource;

        public IChangeToken GetChangeToken() => new CancellationChangeToken(tokenSource.Token);

        public void NotifyChanges()
        {
            var previous = Interlocked.Exchange(ref tokenSource, new CancellationTokenSource());
            previous.Cancel();
        }
    }
}
