﻿using System.Threading.Tasks;
using System.Net.Http;
using Bit.Core.Models.Response;
using System;

namespace Bit.Core.Abstractions
{
    public interface ICozyClientService
    {
        Task<TResponse> FetchJSONAsync<TRequest, TResponse>(HttpMethod method, string path, TRequest body,
            bool hasResponse, string customAuthHeader = null);
        Task<LogoutResponse> LogoutAsync();
        string GetEmailFromCozyURL(string cozyURL);
        Task ConfigureEnvironmentFromCozyURLAsync(string cozyURL);
        string GetURLForApp(string appname);
        string GetRegistrationURL(string lang);
        bool CheckStateAndSecretInOnboardingCallbackURL(Uri url);
    }
}