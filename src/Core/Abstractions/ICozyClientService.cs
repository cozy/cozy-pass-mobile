using System.Threading.Tasks;
using System.Net.Http;
using Bit.Core.Models.Response;
using System;

namespace Bit.Core.Abstractions
{
    public interface ICozyClientService
    {
        Uri OnboardedURL { get; set; }
        Task<TResponse> FetchJSONAsync<TRequest, TResponse>(HttpMethod method, string path, TRequest body,
            bool hasResponse, string customAuthHeader = null);
        Task<LogoutResponse> LogoutAsync();
        string GetEmailFromCozyURL(string cozyURL);
        Task ConfigureEnvironmentFromCozyURLAsync(string cozyURL);
        Task<string> GetURLForApp(string appname, string fragment);
        string GetRegistrationURL(string lang);
        string GetRemindCozyAddressUrl(string lang);
        bool CheckStateAndSecretInOnboardingCallbackURL();
        Task UpdateSynchronizedAtAsync();
    }
}
