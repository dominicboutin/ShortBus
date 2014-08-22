using System.Threading.Tasks;
using ShortBus.Tests.Interceptors;

namespace ShortBus.Tests.Example
{
    public class Pong : IRequestHandler<Ping, string>
    {
        [ShortBusAT("test")]
        public string Handle(Ping request)
        {
            return "PONG!";
        }

        [ShortBusAT("test")]
        public async Task<string> HandleAsync(Ping request)
        {
            return "PONG!";
        }
    }
}