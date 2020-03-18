using System.Threading.Tasks;
using System.Net.Http;
using Bit.Core.Models.Response;

namespace Bit.Core.Abstractions
{
    public interface ICozyClientService
    {
        Task<TResponse> FetchJSONAsync<TRequest, TResponse>(HttpMethod method, string path, TRequest body,
            bool hasResponse, string customAuthHeader = null);
        Task<LogoutResponse> LogoutAsync();
    }
}