using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.Primitives;
using System.Threading;

namespace Lampac.Engine
{
    public class DynamicActionDescriptorChangeProvider : IActionDescriptorChangeProvider
    {
        public static DynamicActionDescriptorChangeProvider Instance { get; } = new DynamicActionDescriptorChangeProvider();

        public CancellationTokenSource TokenSource { get; private set; } = new CancellationTokenSource();

        public IChangeToken GetChangeToken() => new CancellationChangeToken(TokenSource.Token);

        public void NotifyChanges()
        {
            var previous = Interlocked.Exchange(ref TokenSource, new CancellationTokenSource());
            previous.Cancel();
        }
    }
}
