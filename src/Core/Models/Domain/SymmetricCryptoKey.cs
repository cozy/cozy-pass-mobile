﻿using System;
using System.Linq;
using Bit.Core.Enums;

namespace Bit.Core.Models.Domain
{
    public class SymmetricCryptoKey
    {
        public SymmetricCryptoKey(byte[] key, EncryptionType? encType = null)
        {
            if (key == null)
            {
                throw new Exception("Must provide key.");
            }

            if (encType == null)
            {
                if (key.Length == 32)
                {
                    encType = EncryptionType.AesCbc256_B64;
                }
                else if (key.Length == 64)
                {
                    encType = EncryptionType.AesCbc256_HmacSha256_B64;
                }
                else
                {
                    throw new Exception("Unable to determine encType.");
                }
            }

            Key = key;
            EncType = encType.Value;

            if (EncType == EncryptionType.AesCbc256_B64 && Key.Length == 32)
            {
                EncKey = Key;
                MacKey = null;
            }
            else if (EncType == EncryptionType.AesCbc128_HmacSha256_B64 && Key.Length == 32)
            {
                EncKey = new ArraySegment<byte>(Key, 0, 16).ToArray();
                MacKey = new ArraySegment<byte>(Key, 16, 16).ToArray();
            }
            else if (EncType == EncryptionType.AesCbc256_HmacSha256_B64 && Key.Length == 64)
            {
                EncKey = new ArraySegment<byte>(Key, 0, 32).ToArray();
                MacKey = new ArraySegment<byte>(Key, 32, 32).ToArray();
            }
            else
            {
                throw new Exception("Unsupported encType/key length.");
            }

            if (Key != null)
            {
                KeyB64 = Convert.ToBase64String(Key);
            }
            if (EncKey != null)
            {
                EncKeyB64 = Convert.ToBase64String(EncKey);
            }
            if (MacKey != null)
            {
                MacKeyB64 = Convert.ToBase64String(MacKey);
            }
        }

        public byte[] Key { get; set; }
        public byte[] EncKey { get; set; }
        public byte[] MacKey { get; set; }
        public EncryptionType EncType { get; set; }
        public string KeyB64 { get; set; }
        public string EncKeyB64 { get; set; }
        public string MacKeyB64 { get; set; }
    }
}
