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
    }
}
