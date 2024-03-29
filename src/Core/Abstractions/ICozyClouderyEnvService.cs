﻿using System.Threading.Tasks;
using Flurl;

namespace Bit.Core.Abstractions
{
    public interface ICozyClouderyEnvService
    {
        string ParseClouderyEnvFromUrl(Url uri);

        Task<string> GetClouderyUrl();

        Task<string> GetClouderyEnvFromAsyncStorage();
        Task SaveClouderyEnvOnAsyncStorage(string clouderyEnv);
    }
}
