using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bit.Core.Abstractions;
using Bit.Core.Models.Data;
using Bit.Core.Models.Domain;

namespace Bit.Core.Services
{
    public class OrganizationService : IOrganizationService
    {
        private readonly IStateService _stateService;

        public OrganizationService(IStateService stateService)
        {
            _stateService = stateService;
        }

        public async Task<Organization> GetAsync(string id)
        {
            var organizations = await _stateService.GetOrganizationsAsync();
            if (organizations == null || !organizations.ContainsKey(id))
            {
                return null;
            }
            return new Organization(organizations[id]);
        }

        public async Task<Organization> GetByIdentifierAsync(string identifier)
        {
            var organizations = await GetAllAsync();
            if (organizations == null || organizations.Count == 0)
            {
                return null;
            }
            return organizations.FirstOrDefault(o => o.Identifier == identifier);
        }

        public async Task<List<Organization>> GetAllAsync(string userId = null)
        {
            var organizations = await _stateService.GetOrganizationsAsync(userId);
            return organizations?.Select(o => new Organization(o.Value)).ToList() ?? new List<Organization>();
        }

        public async Task ReplaceAsync(Dictionary<string, OrganizationData> organizations)
        {
            #region cozy
            await CacheCozyOrganizationId();
            #endregion

            await _stateService.SetOrganizationsAsync(organizations);
        }

        #region cozy
        public async Task CacheCozyOrganizationId()
        {
            var organizations = await GetAllAsync();
            for (int count = 0; count < organizations.Count; count++)
            {
                var organization = organizations.ElementAt(count);
                var id = organization.Id;
                if (organization.Name == "Cozy")
                {
                    CozyOrganizationId = id;
                }
            }
        }

        public string CozyOrganizationId { get; private set; }
        #endregion cozy

        public async Task ClearAllAsync(string userId)
        {
            #region cozy
            CozyOrganizationId = null;
            #endregion

            await _stateService.SetOrganizationsAsync(null, userId);
        }
    }
}
