﻿using System;
using System.Collections.Concurrent;

namespace Bit.App.Controls
{
    public interface IAvatarImageSourcePool
    {
        AvatarImageSource GetOrCreateAvatar(string name, string email);
    }

    public class AvatarImageSourcePool : IAvatarImageSourcePool
    {
        private readonly ConcurrentDictionary<string, AvatarImageSource> _cache = new ConcurrentDictionary<string, AvatarImageSource>();

        public AvatarImageSource GetOrCreateAvatar(string name, string email)
        {
            var key = $"{name}{email}";
            if (!_cache.TryGetValue(key, out var avatar))
            {
                avatar = new AvatarImageSource(name, email);
                if (!_cache.TryAdd(key, avatar)
                    &&
                    !_cache.TryGetValue(key, out avatar)) // If add fails another thread created the avatar in between the first try get and the try add.
                {
                    // if add and get after fails, then something wrong is going on with this method.
                    throw new InvalidOperationException("Something is wrong creating the avatar image");
                }
            }
            return avatar;
        }
    }
}

