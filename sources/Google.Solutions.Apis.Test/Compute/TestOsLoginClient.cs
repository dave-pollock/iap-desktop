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

using Google.Apis.Auth.OAuth2;
using Google.Solutions.Apis.Auth;
using Google.Solutions.Apis.Auth.Gaia;
using Google.Solutions.Apis.Compute;
using Google.Solutions.Apis.Locator;
using Google.Solutions.Common.Util;
using Google.Solutions.Testing.Apis;
using Google.Solutions.Testing.Apis.Integration;
using Moq;
using NUnit.Framework;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Google.Solutions.Apis.Test.Compute
{
    [TestFixture]
    [UsesCloudResources]
    public class TestOsLoginClient
    {
        public const string SampleKeyNistp256 =
            "AAAAE2VjZHNhLXNoYTItbmlzdHAyNTYAAAAIbmlzdHAyNTYAAABBBOAATK5b5Y" +
            "ERo8r80PGSNgH+fabpTTr1tSt3CcAXd1gk3E+f1vvPL/1MxYeGolwehAyTL8mP" +
            "kxxmyn0tRb5TGvM=";

        private static IAuthorization CreateGaiaAuthorizationWithMismatchedUser(
            string username,
            ICredential credential)
        {
            var session = new Mock<IGaiaOidcSession>();
            session.SetupGet(s => s.Username).Returns(username);
            session.SetupGet(s => s.Email).Returns(username);
            session.SetupGet(s => s.ApiCredential).Returns(credential);

            var authorization = new Mock<IAuthorization>();
            authorization.SetupGet(a => a.Session).Returns(session.Object);
            authorization.SetupGet(a => a.DeviceEnrollment)
                .Returns(TestProject.DisabledEnrollment);

            return authorization.Object;
        }

        //---------------------------------------------------------------------
        // ImportSshPublicKeyAsync.
        //---------------------------------------------------------------------

        [Test]
        public async Task WhenUsingWorkforceSession_ThenImportSshPublicKeyThrowsException(
            [Credential(Type = PrincipalType.WorkforceIdentity)]
            ResourceTask<IAuthorization> authorization)
        {
            var client = new OsLoginClient(
                OsLoginClient.CreateEndpoint(),
                await authorization,
                TestProject.ApiKey,
                TestProject.UserAgent);

            ExceptionAssert.ThrowsAggregateException<OsLoginNotSupportedForWorkloadIdentityException>(
                () => client.ImportSshPublicKeyAsync(
                    new ProjectLocator(TestProject.ProjectId),
                    "ssh-rsa blob",
                    TimeSpan.FromMinutes(1),
                    CancellationToken.None).Wait());
        }

        [Test]
        public async Task WhenEmailAndCredentialMismatch_ThenImportSshPublicKeyThrowsException(
            [Credential] ResourceTask<ICredential> credentialTask)
        {
            var authorization = CreateGaiaAuthorizationWithMismatchedUser(
                "x@gmail.com",
                await credentialTask);
            var client = new OsLoginClient(
                OsLoginClient.CreateEndpoint(),
                authorization,
                TestProject.ApiKey,
                TestProject.UserAgent);

            ExceptionAssert.ThrowsAggregateException<ResourceAccessDeniedException>(
                () => client.ImportSshPublicKeyAsync(
                    new ProjectLocator(TestProject.ProjectId),
                    "ssh-rsa blob",
                    TimeSpan.FromMinutes(1),
                    CancellationToken.None).Wait());
        }

        [Test]
        public async Task WhenEmailValid_ThenImportSshPublicKeySucceeds(
            [Credential(Role = PredefinedRole.ComputeViewer)]
            ResourceTask<IAuthorization> authorizationTask)
        {
            var client = new OsLoginClient(
                OsLoginClient.CreateEndpoint(),
                await authorizationTask,
                TestProject.ApiKey,
                TestProject.UserAgent);

            var key = "ssh-rsa notarealkey-" + Guid.NewGuid().ToString();

            var profile = await client.ImportSshPublicKeyAsync(
                new ProjectLocator(TestProject.ProjectId),
                key,
                TimeSpan.FromMinutes(5),
                CancellationToken.None)
            .ConfigureAwait(false);

            var entry = profile.SshPublicKeys
                .Values
                .Where(k => k.Key.Contains(key))
                .FirstOrDefault();
            Assert.IsNotNull(entry);
        }

        //---------------------------------------------------------------------
        // GetLoginProfileAsync.
        //---------------------------------------------------------------------

        [Test]
        public async Task WhenUsingWorkforceSession_ThenGetLoginProfileThrowsException(
            [Credential(Type = PrincipalType.WorkforceIdentity)]
            ResourceTask<IAuthorization> authorization)
        {
            var client = new OsLoginClient(
                OsLoginClient.CreateEndpoint(),
                await authorization,
                TestProject.ApiKey,
                TestProject.UserAgent);

            ExceptionAssert.ThrowsAggregateException<OsLoginNotSupportedForWorkloadIdentityException>(
                () => client
                .GetLoginProfileAsync(
                    new ProjectLocator(TestProject.ProjectId),
                    CancellationToken.None)
                .Wait());
        }

        [Test]
        public async Task WhenEmailAndCredentialMismatch_ThenGetLoginProfileThrowsException(
            [Credential] ResourceTask<ICredential> credentialTask)
        {
            var authorization = CreateGaiaAuthorizationWithMismatchedUser(
                "x@gmail.com",
                await credentialTask);
            var client = new OsLoginClient(
                OsLoginClient.CreateEndpoint(),
                authorization,
                TestProject.ApiKey,
                TestProject.UserAgent);

            ExceptionAssert.ThrowsAggregateException<ResourceAccessDeniedException>(
                () => client.GetLoginProfileAsync(
                    new ProjectLocator(TestProject.ProjectId),
                    CancellationToken.None).Wait());
        }

        [Test]
        public async Task WhenEmailValid_ThenGetLoginProfileSucceeds(
            [Credential(Role = PredefinedRole.ComputeViewer)]
            ResourceTask<IAuthorization> authorizationTask)
        {
            var client = new OsLoginClient(
                OsLoginClient.CreateEndpoint(),
                await authorizationTask,
                TestProject.ApiKey,
                TestProject.UserAgent);

            var profile = await client
                .GetLoginProfileAsync(
                    new ProjectLocator(TestProject.ProjectId),
                    CancellationToken.None)
                .ConfigureAwait(false);

            Assert.IsNotNull(profile);
        }

        //---------------------------------------------------------------------
        // DeleteSshPublicKey.
        //---------------------------------------------------------------------

        [Test]
        public async Task WhenUsingWorkforceSession_ThenDeleteSshPublicKeyThrowsException(
            [Credential(Type = PrincipalType.WorkforceIdentity)]
            ResourceTask<IAuthorization> authorization)
        {
            var client = new OsLoginClient(
                OsLoginClient.CreateEndpoint(),
                await authorization,
                TestProject.ApiKey,
                TestProject.UserAgent);

            ExceptionAssert.ThrowsAggregateException<OsLoginNotSupportedForWorkloadIdentityException>(
                () => client.DeleteSshPublicKeyAsync(
                    "fingerprint",
                    CancellationToken.None).Wait());
        }

        [Test]
        public async Task WhenDeletingKeyTwice_ThenDeleteSshPublicKeySucceeds(
            [Credential(Role = PredefinedRole.ComputeViewer)]
            ResourceTask<IAuthorization> authorizationTask)
        {
            var client = new OsLoginClient(
                OsLoginClient.CreateEndpoint(),
                await authorizationTask,
                TestProject.ApiKey,
                TestProject.UserAgent);

            var key = "ssh-rsa notarealkey-" + Guid.NewGuid().ToString();

            //
            // Import a key.
            //
            var profile = await client
                .ImportSshPublicKeyAsync(
                    new ProjectLocator(TestProject.ProjectId),
                    key,
                    TimeSpan.FromMinutes(5),
                    CancellationToken.None)
                .ConfigureAwait(false);

            var entry = profile.SshPublicKeys
                .Values
                .Where(k => k.Key.Contains(key))
                .FirstOrDefault();
            Assert.IsNotNull(entry);

            //
            // Delete key twice.
            //
            await client
                .DeleteSshPublicKeyAsync(
                    entry.Fingerprint,
                    CancellationToken.None)
                .ConfigureAwait(false);
            await client.DeleteSshPublicKeyAsync(
                    entry.Fingerprint,
                    CancellationToken.None)
                .ConfigureAwait(false);

            //
            // Check that it's gone.
            //
            profile = await client
                .GetLoginProfileAsync(
                    new ProjectLocator(TestProject.ProjectId),
                    CancellationToken.None)
                .ConfigureAwait(false);

            Assert.IsFalse(profile.SshPublicKeys
                .EnsureNotNull()
                .Any(k => k.Key.Contains(key)));
        }

        [Test]
        public async Task WhenDeletingNonexistingKey_ThenDeleteSshPublicKeySucceeds(
            [Credential(Role = PredefinedRole.ComputeViewer)]
            ResourceTask<IAuthorization> authorizationTask)
        {
            var client = new OsLoginClient(
                OsLoginClient.CreateEndpoint(),
                await authorizationTask,
                TestProject.ApiKey,
                TestProject.UserAgent);

            await client.DeleteSshPublicKeyAsync(
                    "nonexisting",
                    CancellationToken.None)
                .ConfigureAwait(false);
        }

        //---------------------------------------------------------------------
        // SignPublicKeyAsync.
        //---------------------------------------------------------------------

        [Test]
        [Ignore("b/325544415")]
        public async Task WhenUsingGaiaSession_ThenSignPublicKeyThrowsException(
            [Credential(Role = PredefinedRole.ComputeViewer)]
            ResourceTask<IAuthorization> authorizationTask)
        {
            var client = new OsLoginClient(
                OsLoginClient.CreateEndpoint(),
                await authorizationTask,
                TestProject.ApiKey,
                TestProject.UserAgent);

            ExceptionAssert.ThrowsAggregateException<ExternalIdpNotConfiguredForOsLoginException>(
                () => client.SignPublicKeyAsync(
                    new ZoneLocator(TestProject.ProjectId, TestProject.Zone),
                    $"ecdsa-sha2-nistp256 {SampleKeyNistp256}",
                    CancellationToken.None).Wait());
        }

        [Test]
        public async Task WhenUsingWorkforceSessionAndUserInNotRole_ThenSignPublicKeyThrowsException(
            [Credential(Type = PrincipalType.WorkforceIdentity)]
            ResourceTask<IAuthorization> authorizationTask)
        {
            var client = new OsLoginClient(
                OsLoginClient.CreateEndpoint(),
                await authorizationTask,
                TestProject.ApiKey,
                TestProject.UserAgent);

            ExceptionAssert.ThrowsAggregateException<ResourceAccessDeniedException>(
                () => client
                    .SignPublicKeyAsync(
                        new ZoneLocator(TestProject.ProjectId, TestProject.Zone),
                        $"ecdsa-sha2-nistp256 {SampleKeyNistp256}",
                        CancellationToken.None)
                    .Wait());
        }

        [Test]
        [Ignore("b/325544415")]
        public async Task WhenUsingWorkforceSessionAndUserInRole_ThenSignPublicKeySucceeds(
            [Credential(Type = PrincipalType.WorkforceIdentity, Role = PredefinedRole.ServiceUsageConsumer)]
            ResourceTask<IAuthorization> authorizationTask)
        {
            var client = new OsLoginClient(
                OsLoginClient.CreateEndpoint(),
                await authorizationTask,
                TestProject.ApiKey,
                TestProject.UserAgent);

            var certifiedKey = await client
                .SignPublicKeyAsync(
                    new ZoneLocator(TestProject.ProjectId, TestProject.Zone),
                    $"ecdsa-sha2-nistp256 {SampleKeyNistp256}",
                    CancellationToken.None)
                .ConfigureAwait(false);

            StringAssert.StartsWith(
                "ecdsa-sha2-nistp256-cert-v01@openssh.com",
                certifiedKey);
        }

        //---------------------------------------------------------------------
        // ListSecurityKeysAsync.
        //---------------------------------------------------------------------

        [Test]
        public async Task WhenUsingWorkforceSession_ThenListSecurityKeysAsyncException(
            [Credential(Type = PrincipalType.WorkforceIdentity)]
            ResourceTask<IAuthorization> authorization)
        {
            var client = new OsLoginClient(
                OsLoginClient.CreateEndpoint(),
                await authorization,
                TestProject.ApiKey,
                TestProject.UserAgent);

            ExceptionAssert.ThrowsAggregateException<OsLoginNotSupportedForWorkloadIdentityException>(
                () => client.ListSecurityKeysAsync(
                    new ProjectLocator(TestProject.ProjectId),
                    CancellationToken.None).Wait());
        }

        [Test]
        public async Task WhenEmailAndCredentialMismatch_ThenListSecurityKeysAsyncThrowsException(
            [Credential] ResourceTask<ICredential> credentialTask)
        {
            var authorization = CreateGaiaAuthorizationWithMismatchedUser(
                "x@gmail.com",
                await credentialTask);
            var client = new OsLoginClient(
                OsLoginClient.CreateEndpoint(),
                authorization,
                TestProject.ApiKey,
                TestProject.UserAgent);

            ExceptionAssert.ThrowsAggregateException<ResourceAccessDeniedException>(
                () => client.ListSecurityKeysAsync(
                    new ProjectLocator(TestProject.ProjectId),
                    CancellationToken.None).Wait());
        }

        [Test]
        public async Task WhenEmailValid_ThenListSecurityKeysAsyncReturnsList(
            [Credential(Role = PredefinedRole.ComputeViewer)]
            ResourceTask<IAuthorization> authorizationTask)
        {
            var client = new OsLoginClient(
                OsLoginClient.CreateEndpoint(),
                await authorizationTask,
                TestProject.ApiKey,
                TestProject.UserAgent);

            var keys = await client
                .ListSecurityKeysAsync(
                    new ProjectLocator(TestProject.ProjectId),
                    CancellationToken.None)
                .ConfigureAwait(false);

            Assert.IsNotNull(keys);
        }
    }
}
