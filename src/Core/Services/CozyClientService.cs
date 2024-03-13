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
using System.Collections.Generic;
using Bit.Core.Utilities;
using Xamarin.Essentials;

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

        private string _registrationState;
        private string _registrationSecret;

        private string ApiBaseUrl { get; set; }
        public Uri OnboardedURL { get; set; }

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
            var builder = new UriBuilder
            {
                Host = apiBaseUrl.Host,
                Scheme = apiBaseUrl.Scheme
            };
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

        public async Task UpdateSynchronizedAtAsync() {
            try
            {
                await FetchJSONAsync<object, object>(HttpMethod.Post, "settings/synchronized", null, false);
            } catch
            {
                return;
            }
        }

        public async Task<LogoutResponse> LogoutAsync() {
            var clientId = await _tokenService.GetClientId();
            var registrationAccessToken = await _tokenService.GetRegistrationAccessToken();
            try
            {
                var resp = await FetchJSONAsync<object, LogoutResponse>(
                     HttpMethod.Delete,
                     $"auth/register/{clientId}",
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

        private string NormalizeUserCozyURL(string userCozyURL)
        {
            var scheme = (userCozyURL.StartsWith("http://") || userCozyURL.StartsWith("https://")) ? "" : "https://";
            return $"{scheme}{userCozyURL}";
        }

        public string GetEmailFromCozyURL(string userCozyURL) {
            var normalizedURL = NormalizeUserCozyURL(userCozyURL);
            var url = new Uri(normalizedURL);
            var host = url.Host;
            return $"me@{host}";
        }

        public async Task ConfigureEnvironmentFromCozyURLAsync(string userCozyURL)
        {
            var environmentData = new EnvironmentUrlData();
            var cozyURL = NormalizeUserCozyURL(userCozyURL); 
            var baseURL = $"{cozyURL}/bitwarden";
            environmentData.Base = baseURL;
            await _environmentService.SetRegionAsync(environmentData.Region, environmentData);
        }

        public async Task<string> GetURLForApp(string appName, string fragment = null)
        {
            var capabilities = await FetchJSONAsync<object, Capabilities>(
                method: HttpMethod.Get,
                path: "settings/capabilities",
                body: null,
                hasResponse: true
            );

            var cozyURL = GetCozyURL();
            var subdomainKey = "flat_subdomains";
            var subdomain = capabilities.Data.Attributes.ContainsKey(subdomainKey) && capabilities.Data.Attributes[subdomainKey] ? "flat" : "nested";

            var link = UrlHelper.GenerateWebLink(
                cozyUrl: cozyURL,
                searchParams: null,
                pathname: "",
                hash: fragment,
                slug: appName,
                subDomainType: subdomain
            );

            return link;
        }

        private string GenerateRandomValue() {
            var rand = new Random();
            var d = rand.NextDouble();
            return BitConverter.DoubleToInt64Bits(d).ToString("X");
        }

        private void GenerateOnboardingSecretAndState() {
            _registrationSecret = GenerateRandomValue();
            _registrationState = GenerateRandomValue();
        }

        public bool CheckStateAndSecretInOnboardingCallbackURL()
        {
            if (OnboardedURL == null)
            {
                return false;
            }
            var query = OnboardedURL.Query;
            var queryDict = System.Web.HttpUtility.ParseQueryString(query);
            var urlState = queryDict.Get("state");
            return urlState == _registrationState;
        }

        public string GetRegistrationURL(string lang)
        {

            var doctypes = new [] {
                "com.bitwarden.profiles",
                "com.bitwarden.ciphers",
                "com.bitwarden.folders",
                "com.bitwarden.organizations",
                "io.cozy.konnectors",
                "io.cozy.apps.suggestions",
                "io.cozy.support"
            };

            GenerateOnboardingSecretAndState();

            var logoURI = "https://files.cozycloud.cc/pass/logo-pass.png";
            var policyURI = "https://files.cozycloud.cc/cgu.pdf";
            var softwareID = "io.cozy.pass.mobile";

            var data = new
            {
                software_id = softwareID,
                client_name = $"Cozy Pass ({DeviceInfo.Name})",
                client_kind = "mobile",
                logo_uri = logoURI,
                policy_uri = policyURI,
                scopes = doctypes,
                redirect_uri = "cozypass://onboarded", //"https://links.mycozy.cloud/pass",
                onboarding = new
                {
                    app = softwareID,
                    permissions = doctypes,
                    secret = _registrationSecret,
                    state = _registrationState
                }
            };
            var json = JsonConvert.SerializeObject(data);
            var escaped = Uri.EscapeDataString(json);
            var domain = "mycozy.cloud";
            #if DEBUG
	          domain = "cozy.rocks";
	        #endif
            return $"https://manager.cozycloud.cc/cozy/create?domain={domain}&lang={lang}&onboarding={escaped}";
        }

        public string GetRemindCozyAddressUrl(string lang)
        {
            return $"https://manager.cozycloud.cc/v2/cozy/remind?lang={lang}";
        }
    }
}

public class Capabilities
{

    [JsonProperty("data")]
    public CapabilitiesData Data { get; set; }
}

public class CapabilitiesData
{
    [JsonProperty("type")]
    public string Type { get; set; }

    [JsonProperty("id")]
    public string Id { get; set; }

    [JsonProperty("attributes")]
    public Dictionary<string, bool> Attributes { get; set; }

    [JsonProperty("links")]
    public Dictionary<string, string> Links { get; set; }
}
