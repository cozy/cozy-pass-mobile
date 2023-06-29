using System.Collections.Generic;
using System.Threading.Tasks;
using Bit.Core.Abstractions;
using Flurl;

namespace Bit.Core.Services
{
    public class CozyClouderyEnvService: ICozyClouderyEnvService
    {
        private const string Keys_ClouderyEnvConfig = "cozyClouderyEnvConfig";

        private const string DEFAULT_ENV = "PROD";

        private const string PROD_BASE_URI = "https://manager.cozycloud.cc";
        private const string INT_BASE_URI = "https://staging-manager.cozycloud.cc";
        private const string DEV_BASE_URI = "https://manager-dev.cozycloud.cc";

        private const string LOGIN_RELATIVE_URI = "/v2/neutral/start";

        private const string QUERY_STRING = "redirect_after_email=cozypass%3A%2F%2Fpass%2Fonboarding&redirect_after_login=cozypass%3A%2F%2Fpass%2Flogin";

        private readonly IStorageService _storageService;

        public CozyClouderyEnvService(IStorageService storageService)
        {
            _storageService = storageService;
        }

        /// <summary>
        /// Get Cloudery URL to be used on Login InAppBrowser
        ///
        /// The returned URL depends on the current app configuration (PROD, INT, DEV)
        /// </summary>
        /// <returns>The Cloudery URL</returns>
        public async Task<string> GetClouderyUrl()
        {
            var clouderyEnv = await GetClouderyEnvFromAsyncStorage();

            var baseUris = new Dictionary<string, string>() {
                { "PROD", PROD_BASE_URI },
                { "INT", INT_BASE_URI },
                { "DEV", DEV_BASE_URI },
            };
            var baseUri = baseUris[clouderyEnv];

            var clouderyUrl = $"{baseUri}{LOGIN_RELATIVE_URI}?{QUERY_STRING}";

            return clouderyUrl;
        }

        /// <summary>
        /// Parse the given deep link URL and extract Cloudery Environment from it
        /// </summary>
        /// <param name="uri">URL received from deep link feature</param>
        /// <returns>The extracted Cloudery Environment (PROD, INT, DEV), or null if none was found</returns>
        public string ParseClouderyEnvFromUrl(Url uri)
        {
            var queryString = uri.Query;
            var queryDictionary = System.Web.HttpUtility.ParseQueryString(queryString);

            var clouderyEnv = queryDictionary.Get("cloudery_environment");

            if (!IsClouderyEnv(clouderyEnv))
            {
                return null;
            }

            return clouderyEnv;
        }

        /// <summary>
        /// Save the given Cloudery Environment (PROD, INT, DEV) in AsyncStorage
        /// </summary>
        /// <param name="clouderyEnv">The Cloudery Environment to be saved</param>
        /// <returns></returns>
        public async Task SaveClouderyEnvOnAsyncStorage(string clouderyEnv)
        {
            await _storageService.SaveAsync(Keys_ClouderyEnvConfig, clouderyEnv);
        }

        /// <summary>
        /// Get the Cloudery Environment (PROD, INT, DEV) stored in AsyncStorage
        /// </summary>
        /// <returns>The Cloudery Environment (PROD, INT, DEV)</returns>
        public async Task<string> GetClouderyEnvFromAsyncStorage()
        {
            var clouderyEnv = await _storageService.GetAsync<string>(Keys_ClouderyEnvConfig);

            if (!IsClouderyEnv(clouderyEnv))
            {
                return DEFAULT_ENV;
            }

            return clouderyEnv;
        }

        private bool IsClouderyEnv(string value)
        {
            return value == "PROD" || value == "INT" || value == "DEV";
        }
    }
}

