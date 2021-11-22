using System;
using System.Collections.Generic;
using System.Linq;
using Flurl;

namespace Bit.Core.Utilities
{
    public static class UrlHelper
    {
        // Code taken from CozyClient.EnsureFirstSlash()
        private static string EnsureFirstSlash(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return "/";
            }
            else
            {
                return path.StartsWith("/") ? path : "/" + path;
            }
        }

        /// <summary>
        /// Construct a link to a web app
        /// 
        /// This function does not get its cozy url from a CozyClient instance so it can
        /// be used to build urls that point to other Cozies than the user's own Cozy.
        /// This is useful when pointing to the Cozy of the owner of a shared note for
        /// example.
        /// 
        /// Code taken from CozyClient.generateWebLink()
        /// </summary>
        /// <param name="cozyUrl">Base URL of the cozy, eg. cozy.tools or test.mycozy.cloud</param>
        /// <param name="searchParams">Dictionary of search parameters as {key, value}, eg. {'username', 'bob'}</param>
        /// <param name="pathname">Path to a specific part of the app, eg. /public</param>
        /// <param name="hash">Path inside the app, eg. /files/test.jpg</param>
        /// <param name="slug">Slug of the app</param>
        /// <param name="subDomainType">Whether the cozy is using flat or nested subdomains. Defaults to flat.</param>
        /// <returns>Generated URL</returns>
        public static string GenerateWebLink(
            string cozyUrl,
            Dictionary<string, string> searchParams,
            string pathname,
            string hash,
            string slug,
            string subDomainType
        )
        {
            if (searchParams == null)
            {
                searchParams = new Dictionary<string, string>();
            }

            var url = new Url(cozyUrl);

            if (subDomainType == "nested")
            {
                url.Host = $"{slug}.{url.Host}";
            }
            else
            {
                var hostParts = url.Host
                        .Split('.')
                        .Select((x, i) => i == 0 ? x + '-' + slug : x);

                url.Host = string.Join(".", hostParts);
            }

            url.Path = EnsureFirstSlash(pathname);
            url.Fragment = EnsureFirstSlash(hash);

            foreach (var entry in searchParams)
            {
                url.QueryParams.Add(entry.Key, entry.Value);
            }

            return url.ToString();
        }
    }
}
