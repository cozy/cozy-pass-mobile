﻿using System.Collections.Generic;
using System.Threading.Tasks;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Models.Domain;

namespace Bit.Core.Abstractions
{
    public interface IUserService
    {
        Task<bool> CanAccessPremiumAsync();
        Task ClearAsync();
        Task ClearOrganizationsAsync(string userId);
        Task<List<Organization>> GetAllOrganizationAsync();
        Task<string> GetEmailAsync();
        Task<KdfType?> GetKdfAsync();
        Task<int?> GetKdfIterationsAsync();
        Task<Organization> GetOrganizationAsync(string id);
        Task<Organization> GetOrganizationByIdentifierAsync(string identifier);
        Task<string> GetSecurityStampAsync();
        Task<bool> GetEmailVerifiedAsync();
        Task<bool> GetForcePasswordReset();
        Task<string> GetUserIdAsync();
        Task<bool> IsAuthenticatedAsync();
        Task ReplaceOrganizationsAsync(Dictionary<string, OrganizationData> organizations);
        Task SetInformationAsync(string userId, string email, KdfType kdf, int? kdfIterations);
        Task SetSecurityStampAsync(string stamp);
        Task SetEmailVerifiedAsync(bool emailVerified);
        Task SetForcePasswordReset(bool forcePasswordReset);

        #region cozy
        string CozyOrganizationId { get; }
        Task CacheCozyOrganizationId();
        #endregion
    }
}
