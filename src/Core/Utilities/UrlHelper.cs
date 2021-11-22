using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Bit.Core.Exceptions;
using Flurl;

namespace Bit.Core.Utilities
{
    public static class UrlHelper
    {
        public static string COZY_DOMAIN = ".mycozy.cloud";

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

        /// <summary>
        /// Sanitize the given url in order to fit CozyUrl format
        /// A CozyUrl :
        /// - should not be null
        /// - should not be an email
        /// - should not misspell Cozy with Cosy
        /// - has a valid scheme (http or https)
        /// - has a hostname
        /// - do not has an app slug in it (except for custom domains)
        /// - do no ends with a trailing space
        /// 
        /// To have a complete list of CozyUrl rules, please refer to UrlHelperTests.cs tests
        /// </summary>
        /// <param name="inputUrl">Url to sanitize</param>
        /// <returns>Sanitized url</returns>
        public static string SanitizeUrl(string inputUrl)
        {
            // Prevent empty url
            if (string.IsNullOrEmpty(inputUrl))
            {
                throw new CozyUrlRequiredException();
            }

            // Prevent email input
            if (inputUrl.Contains('@'))
            {
                throw new NoEmailAsCozyUrlException();
            }

            if (HasMispelledCozy(inputUrl))
            {
                throw new HasMispelledCozyException();
            }

            return NormalizeUrl(inputUrl, COZY_DOMAIN);
        }

        private static string NormalizeUrl(string value, string defaultDomain)
        {
            var valueWithProtocol = PrependProtocol(value);
            var valueWithoutTrailingSlash = RemoveTrailingSlash(valueWithProtocol);
            var valueWithProtocolAndDomain = AppendDomain(
                valueWithoutTrailingSlash,
                defaultDomain
            );

            var isDefaultDomain = valueWithProtocolAndDomain.Contains(defaultDomain);

            return isDefaultDomain
            ? RemoveAppSlug(valueWithProtocolAndDomain)
            : valueWithProtocolAndDomain;
        }

        private static bool HasMispelledCozy(string value)
        {
            return value.Contains("mycosy");
        }

        private static string AppendDomain(string value, string domain)
        {
            var regex = new Regex(@"\.", RegexOptions.IgnoreCase);
            if (regex.IsMatch(value))
            {
                return value;
            }

            return $"{value}{domain}";
        }

        private static string PrependProtocol(string value)
        {
            var regex = new Regex(@"^http(s)?:\/\/", RegexOptions.IgnoreCase);
            if (regex.IsMatch(value))
            {
                return value;
            }

            return $"https://{value}";
        }

        private static string RemoveTrailingSlash(string value)
        {
            if (value.EndsWith("/"))
            {
                return value.Substring(0, value.Length - 1);
            }

            return value;
        }

        private static string RemoveAppSlug(string value)
        {
            var regex = new Regex(@"^https?:\/\/\w+(-\w+)\.", RegexOptions.IgnoreCase);

            var matches = regex.Match(value);

            if (matches.Groups.Count > 1)
            {
                return value.Replace(matches.Groups[1].Value, "");
            }

            return value;
        }
    }

    public class CozyUrlRequiredException : CozyException
    {
    }

    public class NoEmailAsCozyUrlException : CozyException
    {
    }

    public class HasMispelledCozyException : CozyException
    {
    }
}
