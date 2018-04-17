using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace Jaeger.Core.Util
{
    public interface IHttpClient
    {
        void SetAuthorization(AuthenticationHeaderValue authentication);

        Task<string> MakeGetRequestAsync(string urlToRead);

        Task<HttpResponseMessage> SendAsync(HttpRequestMessage request);
    }
}