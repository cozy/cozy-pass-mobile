using Bit.Core.Abstractions;
using Bit.Core.Exceptions;
using Bit.Core.Models.Response;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using System;
using Bit.Core.Models.Data;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Bit.Core.Services
{
    public class CozyClientService : ICozyClientService
    {
        private readonly JsonSerializerSettings _jsonSettings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver()
        };
        private readonly HttpClient _httpClient = new HttpClient();
        private readonly ITokenService _tokenService;
        private readonly IApiService _apiService;
        private readonly IEnvironmentService _environmentService;

        private string ApiBaseUrl { get; set; }

        public CozyClientService(
            ITokenService tokenService,
            IApiService apiService,
            IEnvironmentService environmentService)
        {
            _tokenService = tokenService;
            _apiService = apiService;
            _environmentService = environmentService;
        }

        private async Task<string> GetTokenAsync() {
            var token = await _tokenService.GetTokenAsync();
            return token;
        }

        private string GetCozyURL() {
            var apiBaseUrl = new Uri(_apiService.ApiBaseUrl);
            var builder = new UriBuilder();
            builder.Host = apiBaseUrl.Host;
            builder.Scheme = apiBaseUrl.Scheme;
            return builder.ToString();
        }


        public async Task<TResponse> FetchJSONAsync<TRequest, TResponse>(HttpMethod method, string path, TRequest body,
            bool hasResponse, string customBearerValue = null)
        {

            using (var requestMessage = new HttpRequestMessage())
            {
                var cozyUrl = GetCozyURL();
                requestMessage.Version = new Version(1, 0);
                requestMessage.Method = method;
                requestMessage.RequestUri = new Uri(string.Concat(cozyUrl, path));
                if (body != null)
                {
                    var bodyType = body.GetType();
                    if (bodyType == typeof(string))
                    {
                        requestMessage.Content = new StringContent((object)bodyType as string, Encoding.UTF8,
                            "application/x-www-form-urlencoded; charset=utf-8");
                    }
                    else if (bodyType == typeof(MultipartFormDataContent))
                    {
                        requestMessage.Content = body as MultipartFormDataContent;
                    }
                    else
                    {
                        requestMessage.Content = new StringContent(JsonConvert.SerializeObject(body, _jsonSettings),
                            Encoding.UTF8, "application/json");
                    }
                }

                if (customBearerValue != null)
                {
                    requestMessage.Headers.Add("Authorization", string.Concat("Bearer ", customBearerValue));
                } else {
                    var authHeader = await GetTokenAsync();
                    requestMessage.Headers.Add("Authorization", string.Concat("Bearer ", authHeader));
                }
                    
                if (hasResponse)
                {
                    requestMessage.Headers.Add("Accept", "application/json");
                }

                HttpResponseMessage response;
                try
                {
                    response = await _httpClient.SendAsync(requestMessage);
                }
                catch (Exception e)
                {
                    throw new ApiException(HandleWebError(e));
                }
                if (hasResponse && response.IsSuccessStatusCode)
                {

                    var responseJsonString = await response.Content.ReadAsStringAsync();
                    return JsonConvert.DeserializeObject<TResponse>(responseJsonString);
                }
                else if (!response.IsSuccessStatusCode)
                {
                    var error = await HandleErrorAsync(response, false);
                    throw new ApiException(error);
                }
                return (TResponse)(object)null;
            }
        }

        public async Task<LogoutResponse> LogoutAsync() {
            var clientId = _tokenService.ClientId;
            var registrationAccessToken = _tokenService.RegistrationAccessToken;
            try
            {
                var resp = await FetchJSONAsync<object, LogoutResponse>(
                     HttpMethod.Delete,
                     string.Concat("auth/register/", clientId),
                     null,
                     false,
                     registrationAccessToken
                 );
                return resp;

            }
            catch
            {
                return null;
            }
        }


        private ErrorResponse HandleWebError(Exception e)
        {
            return new ErrorResponse
            {
                StatusCode = HttpStatusCode.BadGateway,
                Message = "Exception message: " + e.Message
            };
        }

        private async Task<ErrorResponse> HandleErrorAsync(HttpResponseMessage response, bool tokenError)
        {
            if ((tokenError && response.StatusCode == HttpStatusCode.BadRequest) ||
                response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden)
            {
                return null;
            }
            JObject responseJObject = null;
            if (IsJsonResponse(response))
            {
                var responseJsonString = await response.Content.ReadAsStringAsync();
                responseJObject = JObject.Parse(responseJsonString);
            }
            return new ErrorResponse(responseJObject, response.StatusCode, tokenError);
        }

        private bool IsJsonResponse(HttpResponseMessage response)
        {
            return (response.Content?.Headers?.ContentType?.MediaType ?? string.Empty) == "application/json";
        }

        public string GetEmailFromCozyURL(string cozyURL)
        {
            return string.Concat("me@", cozyURL);
        }

        public async Task ConfigureEnvironmentFromCozyURLAsync(string cozyURL)
        {
            var environmentData = new EnvironmentUrlData();
            var scheme = cozyURL.StartsWith("http://") ? "" : "https://";
            var baseURL = string.Concat(scheme, cozyURL, "/bitwarden");
            environmentData.Base = baseURL;
            await _environmentService.SetUrlsAsync(environmentData);
        }
    }
}
