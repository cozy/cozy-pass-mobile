using System;
using System.Collections.Generic;
using Bit.Core.Utilities;
using Xunit;

namespace Bit.Core.Test.Utilities
{
    public class UrlHelperTests
    {
        [Fact]
        public void GenerateWebLink_GeneratesTheRightLinkToAFlatCozy()
        {
            var sharecode = "sharingIsCaring";
            var username = "alice";

            var webLink = UrlHelper.GenerateWebLink(
                cozyUrl: "http://alice.cozy.tools",
                searchParams: new Dictionary<string, string>()
                {
                    { "sharecode", sharecode },
                    { "username", username },
                },
                pathname: "public",
                hash: "/n/4",
                slug: "notes",
                subDomainType: "flat"
            );

            Assert.Equal($"http://alice-notes.cozy.tools/public?sharecode={sharecode}&username={username}#/n/4", webLink);
        }

        [Fact]
        public void GenerateWebLink_GeneratesTheRightLinkToANestedCozy()
        {
            var webLink = UrlHelper.GenerateWebLink(
                cozyUrl: "https://alice.cozy.tools",
                searchParams: null,
                pathname: "/public/",
                hash: "files/432432",
                slug: "drive",
                subDomainType: "nested"
            );

            Assert.Equal("https://drive.alice.cozy.tools/public/#/files/432432", webLink);
        }

        [Fact]
        public void GenerateWebLink_CorrectlySetsSlashBeforeFragmentIfNoPath()
        {
            // Even if "/" is not mandatory in URL before the Hash, we want to be iso with CozyClient's behavior
            var webLink = UrlHelper.GenerateWebLink(
                cozyUrl: "https://alice.cozy.tools",
                searchParams: null,
                pathname: "",
                hash: "/vault?action=import",
                slug: "passwords",
                subDomainType: "flat"
            );

            Assert.Equal("https://alice-passwords.cozy.tools/#/vault?action=import", webLink);
        }

        [Fact]
        public void NormalizeUrl_ShouldReturnUndefinedIfTheInputIsEmpty()
        {
            var inputUrl = "";
            Assert.Throws<CozyUrlRequiredException>(() => {
                UrlHelper.SanitizeUrl(inputUrl);
            });
        }

        [Fact]
        public void NormalizeUrl_ShouldReturnUndefinedIfTheInputIsAnEmail()
        {
            var inputUrl = "claude@cozycloud.cc";
            Assert.Throws<NoEmailAsCozyUrlException>(() =>
            {
                UrlHelper.SanitizeUrl(inputUrl);
            });
        }

        [Fact]
        public void NormalizeUrl_ShouldReturnTheUrlWithoutTheAppSlugIfPresent()
        {
            var inputUrl = "claude-drive.mycozy.cloud";
            var url = UrlHelper.SanitizeUrl(inputUrl);
            Assert.Equal("https://claude.mycozy.cloud", url);
        }

        [Fact]
        public void NormalizeUrl_ShouldReturnTheUrlWithTheDefaultDomainIfMissing()
        {
            var inputUrl = "claude-drive";
            var url = UrlHelper.SanitizeUrl(inputUrl);
            Assert.Equal("https://claude.mycozy.cloud", url);
        }

        [Fact]
        public void NormalizeUrl_ShouldReturnTheUrlWithTheDefaultSchemeIfMissing()
        {
            var inputUrl = "claude.mycozy.cloud";
            var url = UrlHelper.SanitizeUrl(inputUrl);
            Assert.Equal("https://claude.mycozy.cloud", url);
        }

        [Fact]
        public void NormalizeUrl_ShouldReturnTheUrlIfTheInputIsCorrect()
        {
            var inputUrl = "https://claude.mycozy.cloud";
            var url = UrlHelper.SanitizeUrl(inputUrl);
            Assert.Equal("https://claude.mycozy.cloud", url);
        }

        [Fact]
        public void NormalizeUrl_ShouldAcceptLocalUrl()
        {
            var inputUrl = "http://claude.cozy.tools:8080";
            var url = UrlHelper.SanitizeUrl(inputUrl);
            Assert.Equal("http://claude.cozy.tools:8080", url);
        }

        [Fact]
        public void NormalizeUrl_ShouldNotTryToRemoveSlugIfPresentAndUrlHasACustomDomain()
        {
            var inputUrl = "claude-drive.on-premise.cloud";
            var url = UrlHelper.SanitizeUrl(inputUrl);
            Assert.Equal("https://claude-drive.on-premise.cloud", url);
        }

        [Fact]
        public void NormalizeUrl_ShouldReturnTheCorrectUrlIfDomainsContainsADash()
        {
            var inputUrl = "claude.on-premise.cloud";
            var url = UrlHelper.SanitizeUrl(inputUrl);
            Assert.Equal("https://claude.on-premise.cloud", url);
        }

        [Fact]
        public void NormalizeUrl_ShouldReturnTheCorrectUrlIfDomainsContainsADashAndCozyIsInstalledOnDomainRoot()
        {
            var inputUrl = "https://on-premise.cloud";
            var url = UrlHelper.SanitizeUrl(inputUrl);
            Assert.Equal("https://on-premise.cloud", url);
        }

        [Fact]
        public void NormalizeUrl_ShouldThrowIfUserWriteMycosyInsteadOfMycozy()
        {
            var inputUrl = "https://claude.mycosy.cloud";
            Assert.Throws<HasMispelledCozyException>(() => {
                UrlHelper.SanitizeUrl(inputUrl);
            });
        }

        [Fact]
        public void NormalizeUrl_ShouldAcceptRealCosyUrl()
        {
            var inputUrl = "https://claude.realdomaincosy.cloud";
            var url = UrlHelper.SanitizeUrl(inputUrl);
            Assert.Equal("https://claude.realdomaincosy.cloud", url);
        }

        [Fact]
        public void NormalizeUrl_ShouldRemoveTrailingInUrl()
        {
            var inputUrl = "https://claude.realdomaincosy.cloud/";
            var url = UrlHelper.SanitizeUrl(inputUrl);
            Assert.Equal("https://claude.realdomaincosy.cloud", url);
        }
    }
}
