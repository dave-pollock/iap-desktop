﻿//
// Copyright 2020 Google LLC
//
// Licensed to the Apache Software Foundation (ASF) under one
// or more contributor license agreements.  See the NOTICE file
// distributed with this work for additional information
// regarding copyright ownership.  The ASF licenses this file
// to you under the Apache License, Version 2.0 (the
// "License"); you may not use this file except in compliance
// with the License.  You may obtain a copy of the License at
// 
//   http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing,
// software distributed under the License is distributed on an
// "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY
// KIND, either express or implied.  See the License for the
// specific language governing permissions and limitations
// under the License.
//

using Google.Solutions.Common.Util;
using Google.Solutions.IapDesktop.Application.Profile;
using Google.Solutions.IapDesktop.Application.Profile.Settings;
using Google.Solutions.IapDesktop.Application.Profile.Settings.Registry;
using Google.Solutions.IapDesktop.Core.ObjectModel;
using Google.Solutions.IapDesktop.Extensions.Session.Protocol.Ssh;
using Google.Solutions.Ssh.Cryptography;
using Microsoft.Win32;
using System;
using System.Collections.Generic;

namespace Google.Solutions.IapDesktop.Extensions.Session.Settings
{
    /// <summary>
    /// SSH-related settings.
    /// </summary>
    public interface ISshSettings : ISettingsCollection
    {
        /// <summary>
        /// Enable propagation of current locate.
        /// </summary>
        IBoolSetting IsPropagateLocaleEnabled { get; }

        /// <summary>
        /// Gets or sets the validity of public keys uploaded
        /// to OS Login or metadata.
        /// </summary>
        IIntSetting PublicKeyValidity { get; }

        /// <summary>
        /// Type of public key to use. This determines the
        /// algorithm for public key use authentication.
        /// </summary>
        IEnumSetting<SshKeyType> PublicKeyType { get; }

        /// <summary>
        /// Controls whether the SSH signing key is stored in the
        /// local key store.
        /// </summary>
        IBoolSetting UsePersistentKey { get; }
    }

    /// <summary>
    /// Registry-backed repository for SSH settings.
    /// 
    /// Service is a singleton so that objects can subscribe to events.
    /// </summary>
    [Service(typeof(IRepository<ISshSettings>), ServiceLifetime.Singleton)]
    public class SshSettingsRepository : PolicyEnabledRegistryRepository<ISshSettings>
    {
        private readonly UserProfile.SchemaVersion schemaVersion;

        internal SshSettingsRepository(
            RegistryKey settingsKey,
            RegistryKey machinePolicyKey,
            RegistryKey userPolicyKey,
            UserProfile.SchemaVersion schemaVersion) 
            : base(settingsKey, machinePolicyKey, userPolicyKey)
        {
            Precondition.ExpectNotNull(settingsKey, nameof(settingsKey));
            this.schemaVersion = schemaVersion;
        }

        public SshSettingsRepository(UserProfile profile)
            : this(
                  profile.SettingsKey.CreateSubKey("Ssh"),
                  profile.MachinePolicyKey?.OpenSubKey("Ssh"),
                  profile.UserPolicyKey?.OpenSubKey("Ssh"),
                  profile.Version)
        {
            profile.ExpectNotNull(nameof(profile));
        }

        protected override ISshSettings LoadSettings(
            RegistryKey settingsKey,
            RegistryKey machinePolicyKey,
            RegistryKey userPolicyKey)
        {
            return SshSettings.FromKey(
                settingsKey,
                machinePolicyKey,
                userPolicyKey,
                this.schemaVersion);
        }


        //---------------------------------------------------------------------
        // Inner class.
        //---------------------------------------------------------------------

        private class SshSettings : ISshSettings
        {
            public IBoolSetting IsPropagateLocaleEnabled { get; private set; }
            public IIntSetting PublicKeyValidity { get; private set; }
            public IEnumSetting<SshKeyType> PublicKeyType { get; private set; }
            public IBoolSetting UsePersistentKey { get; private set; }

            public IEnumerable<ISetting> Settings => new ISetting[]
            {
                this.IsPropagateLocaleEnabled,
                this.PublicKeyValidity,
                this.PublicKeyType,
                this.UsePersistentKey
            };

            private SshSettings()
            {
            }

            public static SshSettings FromKey(
                RegistryKey settingsKey,
                RegistryKey machinePolicyKey,
                RegistryKey userPolicyKey,
                UserProfile.SchemaVersion schemaVersion)
            {
                return new SshSettings()
                {
                    //
                    // Settings that can be overridden by policy.
                    //
                    // NB. Default values must be kept consistent with the
                    //     ADMX policy templates!
                    // NB. Machine policies override user policies, and
                    //     user policies override settings.
                    //

                    // 
                    // NB. Initially, the default key type was Rsa3072,
                    // but rsa-ssh is deprecated and many users's machines
                    // aren't allowed to use RSA. Therefore, use ECDSA as
                    // default for newly created profiles.
                    //
                    PublicKeyType = RegistryEnumSetting<SshKeyType>.FromKey(
                            "PublicKeyType",
                            "PublicKeyType",
                            "Key type for public key authentication",
                            null,
                            schemaVersion >= UserProfile.SchemaVersion.Version229
                                ? SshKeyType.EcdsaNistp384
                                : SshKeyType.Rsa3072,
                            settingsKey)
                        .ApplyPolicy(userPolicyKey)
                        .ApplyPolicy(machinePolicyKey),
                    PublicKeyValidity = RegistryDwordSetting.FromKey(
                            "PublicKeyValidity",
                            "PublicKeyValidity",
                            "Validity of (OS Login/Metadata) keys in seconds",
                            null,
                            (int)SshParameters.DefaultPublicKeyValidity.TotalSeconds,
                            settingsKey,
                            (int)TimeSpan.FromMinutes(1).TotalSeconds,
                            int.MaxValue)
                        .ApplyPolicy(userPolicyKey)
                        .ApplyPolicy(machinePolicyKey),
                    UsePersistentKey = RegistryBoolSetting.FromKey(
                            "UsePersistentKey",
                            "UsePersistentKey",
                            "Persist SSH signing key",
                            null,
                            true,
                            settingsKey)
                        .ApplyPolicy(userPolicyKey)
                        .ApplyPolicy(machinePolicyKey),

                    //
                    // User preferences. These cannot be overridden by policy.
                    //
                    IsPropagateLocaleEnabled = RegistryBoolSetting.FromKey(
                        "IsPropagateLocaleEnabled",
                        "IsPropagateLocaleEnabled",
                        null,
                        null,
                        true,
                        settingsKey)
                };
            }
        }
    }
}