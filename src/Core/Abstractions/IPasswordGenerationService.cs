﻿using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Bit.Core.Models.Domain;

namespace Bit.Core.Abstractions
{
    public interface IPasswordGenerationService
    {
        Task AddHistoryAsync(string password, CancellationToken token = default(CancellationToken));
        Task ClearAsync(string userId = null);
        void ClearCache();
        Task<string> GeneratePassphraseAsync(PasswordGenerationOptions options);
        Task<string> GeneratePasswordAsync(PasswordGenerationOptions options);
        Task<List<GeneratedPasswordHistory>> GetHistoryAsync();
        Task<(PasswordGenerationOptions, PasswordGeneratorPolicyOptions)> GetOptionsAsync();
        Task<(PasswordGenerationOptions, PasswordGeneratorPolicyOptions)>
            EnforcePasswordGeneratorPoliciesOnOptionsAsync(PasswordGenerationOptions options);
        Zxcvbn.Result PasswordStrength(string password, List<string> userInputs = null);
        Task SaveOptionsAsync(PasswordGenerationOptions options);
        void NormalizeOptions(PasswordGenerationOptions options, PasswordGeneratorPolicyOptions enforcedPolicyOptions);
    }
}
