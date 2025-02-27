﻿//
// Copyright 2023 Google LLC
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
// Profileific language governing permissions and limitations
// under the License.
//

using Google.Apis.Compute.v1.Data;
using Google.Solutions.Apis.Auth;
using Google.Solutions.Apis.Client;
using Google.Solutions.Apis.Locator;
using Google.Solutions.Iap;
using Google.Solutions.Iap.Net;
using Google.Solutions.Iap.Protocol;
using Google.Solutions.IapDesktop.Core.ClientModel.Protocol;
using Google.Solutions.IapDesktop.Core.ClientModel.Transport;
using Google.Solutions.IapDesktop.Core.ObjectModel;
using Google.Solutions.Testing.Apis;
using Google.Solutions.Testing.Apis.Threading;
using Moq;
using NUnit.Framework;
using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Google.Solutions.IapDesktop.Core.Test.ClientModel.Transport
{
    [TestFixture]
    public class TestIapTransportFactory
    {
        private static readonly InstanceLocator SampleInstance
            = new InstanceLocator("project-1", "zone-1", "instance-1");

        private static readonly TimeSpan SampleTimeout = TimeSpan.FromSeconds(10);

        private static readonly IPEndPoint SampleLoopbackEndpoint
            = new IPEndPoint(IPAddress.Loopback, 123);

        private static IapTunnel.Profile CreateTunnelProfile(
            InstanceLocator instance,
            ushort port)
        {
            var protocol = new Mock<IProtocol>();
            var policy = new Mock<ITransportPolicy>();

            return new IapTunnel.Profile(
                protocol.Object,
                policy.Object,
                instance,
                port);
        }

        private static IapTunnel CreateTunnel(IapTunnel.Profile profile)
        {
            var listener = new Mock<IIapListener>();
            listener.SetupGet(l => l.LocalEndpoint).Returns(SampleLoopbackEndpoint);
            listener.SetupGet(l => l.Statistics).Returns(new Iap.Net.NetworkStatistics());

            return new IapTunnel(
                listener.Object,
                profile,
                IapTunnelFlags.None);
        }

        private static Mock<IapTunnel.Factory> CreateTunnelFactory()
        {
            return new Mock<IapTunnel.Factory>(new Mock<IIapClient>().Object);
        }

        //---------------------------------------------------------------------
        // Pool.
        //---------------------------------------------------------------------

        [Test]
        public void WhenNoTransportsCreated_ThenPoolIsEmpty()
        {
            var factory = new IapTransportFactory(
                new Mock<IEventQueue>().Object,
                CreateTunnelFactory().Object);

            CollectionAssert.IsEmpty(factory.Pool);
        }

        [Test]
        public void PoolIgnoresFaultedTunnels()
        {
            var validProfile = CreateTunnelProfile(SampleInstance, 22);
            var faultingProfile = CreateTunnelProfile(SampleInstance, 23);
            var tunnelFactory = CreateTunnelFactory();
            tunnelFactory
                .Setup(f => f.CreateTunnelAsync(
                    validProfile,
                    It.IsAny<TimeSpan>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(CreateTunnel(validProfile));
            tunnelFactory
                .Setup(f => f.CreateTunnelAsync(
                    faultingProfile,
                    It.IsAny<TimeSpan>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ApplicationException("mock"));

            var factory = new IapTransportFactory(
                new Mock<IEventQueue>().Object,
                tunnelFactory.Object);

            //
            // Create two tunnels, one of them faulting.
            //
            var validTransport = factory.CreateTransportAsync(
                validProfile.Protocol,
                validProfile.Policy,
                validProfile.TargetInstance,
                validProfile.TargetPort,
                validProfile.LocalEndpoint,
                SampleTimeout,
                CancellationToken.None);
            var faultingTransport = factory.CreateTransportAsync(
                 faultingProfile.Protocol,
                 faultingProfile.Policy,
                 faultingProfile.TargetInstance,
                 faultingProfile.TargetPort,
                 faultingProfile.LocalEndpoint,
                 SampleTimeout,
                 CancellationToken.None);

            var pool = factory.Pool;
            Assert.AreEqual(1, pool.Count());

            validTransport.Result.Dispose();
        }

        [Test]
        public void PoolIgnoresIncompleteTunnels()
        {
            var validProfile = CreateTunnelProfile(SampleInstance, 22);
            var tunnelTask = new TaskCompletionSource<IapTunnel>();
            var tunnelFactory = CreateTunnelFactory();
            tunnelFactory
                .Setup(f => f.CreateTunnelAsync(
                    validProfile,
                    It.IsAny<TimeSpan>(),
                    It.IsAny<CancellationToken>()))
                .Returns(tunnelTask.Task); // Task not complete!

            var factory = new IapTransportFactory(
                new Mock<IEventQueue>().Object,
                tunnelFactory.Object);

            var validButIncompleteTransport = factory.CreateTransportAsync(
                validProfile.Protocol,
                validProfile.Policy,
                validProfile.TargetInstance,
                validProfile.TargetPort,
                validProfile.LocalEndpoint,
                SampleTimeout,
                CancellationToken.None);

            CollectionAssert.IsEmpty(factory.Pool);
        }

        //---------------------------------------------------------------------
        // CreateTransport - pooling.
        //---------------------------------------------------------------------

        [Test]
        public void WhenMatchFoundInPoolButTunnelFaulted_ThenCreateTransportCreatesNewTunnel()
        {
            var faultingProfile = CreateTunnelProfile(SampleInstance, 23);
            var tunnelFactory = CreateTunnelFactory();
            tunnelFactory
                .Setup(f => f.CreateTunnelAsync(
                    It.IsAny<IapTunnel.Profile>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ApplicationException("mock"));

            var factory = new IapTransportFactory(
                new Mock<IEventQueue>().Object,
                tunnelFactory.Object);

            var faultingTransport1 = factory.CreateTransportAsync(
                 faultingProfile.Protocol,
                 faultingProfile.Policy,
                 faultingProfile.TargetInstance,
                 faultingProfile.TargetPort,
                 faultingProfile.LocalEndpoint,
                 SampleTimeout,
                 CancellationToken.None);

            //
            // Await task to make sure it's really faulted before we make
            // the next request.
            //
            ExceptionAssert.ThrowsAggregateException<ApplicationException>(
                () => faultingTransport1.Wait());

            var faultingTransport2 = factory.CreateTransportAsync(
                 faultingProfile.Protocol,
                 faultingProfile.Policy,
                 faultingProfile.TargetInstance,
                 faultingProfile.TargetPort,
                 faultingProfile.LocalEndpoint,
                 SampleTimeout,
                 CancellationToken.None);

            Assert.AreNotEqual(faultingTransport1, faultingTransport2);

            tunnelFactory
                .Verify(f => f.CreateTunnelAsync(
                    It.IsAny<IapTunnel.Profile>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<CancellationToken>()), Times.Exactly(2));
        }

        [Test]
        public void WhenMatchFoundInPoolButTunnelNotCompletedYet_ThenCreateTransportReturnsPooledTunnel()
        {
            var validProfile = CreateTunnelProfile(SampleInstance, 22);
            var tunnelTask = new TaskCompletionSource<IapTunnel>();
            var tunnelFactory = CreateTunnelFactory();
            tunnelFactory
                .Setup(f => f.CreateTunnelAsync(
                    validProfile,
                    It.IsAny<TimeSpan>(),
                    It.IsAny<CancellationToken>()))
                .Returns(tunnelTask.Task); // Task not complete!

            var factory = new IapTransportFactory(
                new Mock<IEventQueue>().Object,
                tunnelFactory.Object);

            var validButIncompleteTransport1 = factory.CreateTransportAsync(
                validProfile.Protocol,
                validProfile.Policy,
                validProfile.TargetInstance,
                validProfile.TargetPort,
                validProfile.LocalEndpoint,
                SampleTimeout,
                CancellationToken.None);
            var validButIncompleteTransport2 = factory.CreateTransportAsync(
                validProfile.Protocol,
                validProfile.Policy,
                validProfile.TargetInstance,
                validProfile.TargetPort,
                validProfile.LocalEndpoint,
                SampleTimeout,
                CancellationToken.None);

            Assert.AreNotSame(validButIncompleteTransport1, validButIncompleteTransport2);

            tunnelFactory
                .Verify(f => f.CreateTunnelAsync(
                    It.IsAny<IapTunnel.Profile>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<CancellationToken>()), Times.Once);
        }

        [Test]
        public async Task WhenMatchFoundInPool_ThenCreateTransportReturnsPooledTunnel()
        {
            var validProfile = CreateTunnelProfile(SampleInstance, 22);
            var tunnelTask = new TaskCompletionSource<IapTunnel>();
            var tunnelFactory = CreateTunnelFactory();
            tunnelFactory
                .Setup(f => f.CreateTunnelAsync(
                    validProfile,
                    It.IsAny<TimeSpan>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(CreateTunnel(validProfile));

            var factory = new IapTransportFactory(
                new Mock<IEventQueue>().Object,
                tunnelFactory.Object);

            var transport1 = await factory
                .CreateTransportAsync(
                    validProfile.Protocol,
                    validProfile.Policy,
                    validProfile.TargetInstance,
                    validProfile.TargetPort,
                    validProfile.LocalEndpoint,
                    SampleTimeout,
                    CancellationToken.None)
                .ConfigureAwait(false);
            var transport2 = await factory
                .CreateTransportAsync(
                    validProfile.Protocol,
                    validProfile.Policy,
                    validProfile.TargetInstance,
                    validProfile.TargetPort,
                    validProfile.LocalEndpoint,
                    SampleTimeout,
                    CancellationToken.None)
                .ConfigureAwait(false);

            //
            // Two different transports that use the same tunnel.
            //
            Assert.AreNotSame(transport1, transport2);
            Assert.AreSame(
                ((IapTransportFactory.Transport)transport1).Tunnel,
                ((IapTransportFactory.Transport)transport2).Tunnel);

            transport1.Dispose();
            transport2.Dispose();
        }

        //---------------------------------------------------------------------
        // CreateTransport - events.
        //---------------------------------------------------------------------

        [Test]
        public async Task WhenTunnelCreatedOrClosed_ThenCreateTransportPublishesEvent()
        {
            var eventQueue = new Mock<IEventQueue>();

            var validProfile = CreateTunnelProfile(SampleInstance, 22);
            var tunnelFactory = CreateTunnelFactory();
            tunnelFactory
                .Setup(f => f.CreateTunnelAsync(
                    validProfile,
                    It.IsAny<TimeSpan>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(CreateTunnel(validProfile));

            var factory = new IapTransportFactory(
                eventQueue.Object,
                tunnelFactory.Object);

            using (var transport = await factory
                .CreateTransportAsync(
                    validProfile.Protocol,
                    validProfile.Policy,
                    validProfile.TargetInstance,
                    validProfile.TargetPort,
                    validProfile.LocalEndpoint,
                    SampleTimeout,
                    CancellationToken.None)
                .ConfigureAwait(false))
            {
                eventQueue.Verify(
                    q => q.Publish(It.IsAny<TunnelEvents.TunnelCreated>()),
                    Times.Once);

                eventQueue.Verify(
                    q => q.Publish(It.IsAny<TunnelEvents.TunnelClosed>()),
                    Times.Never);
            }

            eventQueue.Verify(
                q => q.Publish(It.IsAny<TunnelEvents.TunnelClosed>()),
                Times.Once);
        }

        [Test]
        public async Task WhenFactoryPublishesEvents_ThenPoolIsUpToDate()
        {
            var invoker = new ThreadpoolInvoker();
            var eventQueue = new EventQueue(invoker);

            var validProfile = CreateTunnelProfile(SampleInstance, 22);
            var tunnelFactory = CreateTunnelFactory();
            tunnelFactory
                .Setup(f => f.CreateTunnelAsync(
                    validProfile,
                    It.IsAny<TimeSpan>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(CreateTunnel(validProfile));

            var factory = new IapTransportFactory(
                eventQueue,
                tunnelFactory.Object);

            var poolSizeWhenCreated = 0;
            var poolSizeWhenClosed = 0;
            eventQueue.Subscribe<TunnelEvents.TunnelCreated>(
                _ =>
                {
                    poolSizeWhenCreated = factory.Pool.Count();
                    return Task.CompletedTask;
                });
            eventQueue.Subscribe<TunnelEvents.TunnelClosed>(
                _ =>
                {
                    poolSizeWhenClosed = factory.Pool.Count();
                    return Task.CompletedTask;
                });

            using (var transport = await factory
                .CreateTransportAsync(
                    validProfile.Protocol,
                    validProfile.Policy,
                    validProfile.TargetInstance,
                    validProfile.TargetPort,
                    validProfile.LocalEndpoint,
                    SampleTimeout,
                    CancellationToken.None)
                .ConfigureAwait(false))
            { }

            await invoker.AwaitPendingInvocationsAsync();

            Assert.AreEqual(1, poolSizeWhenCreated);
            Assert.AreEqual(0, poolSizeWhenClosed);
        }

        //---------------------------------------------------------------------
        // CreateTransport - exceptions.
        //---------------------------------------------------------------------

        [Test]
        public void WhenSshRelayDenied_ThenCreateTransportThrowsException()
        {
            var validProfile = CreateTunnelProfile(SampleInstance, 22);
            var tunnelFactory = CreateTunnelFactory();
            tunnelFactory
                .Setup(f => f.CreateTunnelAsync(
                    validProfile,
                    It.IsAny<TimeSpan>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new SshRelayDeniedException("mock"));

            var factory = new IapTransportFactory(
                new Mock<IEventQueue>().Object,
                tunnelFactory.Object);

            ExceptionAssert.ThrowsAggregateException<TransportFailedException>(
                HelpTopics.IapAccess,
                () => factory.CreateTransportAsync(
                    validProfile.Protocol,
                    validProfile.Policy,
                    validProfile.TargetInstance,
                    validProfile.TargetPort,
                    validProfile.LocalEndpoint,
                    SampleTimeout,
                    CancellationToken.None).Wait());
        }

        [Test]
        public void WhenNetworkStreamClosed_ThenCreateTransportThrowsException()
        {
            var validProfile = CreateTunnelProfile(SampleInstance, 22);
            var tunnelFactory = CreateTunnelFactory();
            tunnelFactory
                .Setup(f => f.CreateTunnelAsync(
                    validProfile,
                    It.IsAny<TimeSpan>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new NetworkStreamClosedException("mock"));

            var factory = new IapTransportFactory(
                new Mock<IEventQueue>().Object,
                tunnelFactory.Object);

            ExceptionAssert.ThrowsAggregateException<TransportFailedException>(
                HelpTopics.CreateIapFirewallRule,
                () => factory.CreateTransportAsync(
                    validProfile.Protocol,
                    validProfile.Policy,
                    validProfile.TargetInstance,
                    validProfile.TargetPort,
                    validProfile.LocalEndpoint,
                    SampleTimeout,
                    CancellationToken.None).Wait());
        }

        [Test]
        public void WhenWebSocketConnectionDenied_ThenCreateTransportThrowsException()
        {
            var validProfile = CreateTunnelProfile(SampleInstance, 22);
            var tunnelFactory = CreateTunnelFactory();
            tunnelFactory
                .Setup(f => f.CreateTunnelAsync(
                    validProfile,
                    It.IsAny<TimeSpan>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new WebSocketConnectionDeniedException());

            var factory = new IapTransportFactory(
                new Mock<IEventQueue>().Object,
                tunnelFactory.Object);

            ExceptionAssert.ThrowsAggregateException<TransportFailedException>(
                HelpTopics.ProxyConfiguration,
                () => factory.CreateTransportAsync(
                    validProfile.Protocol,
                    validProfile.Policy,
                    validProfile.TargetInstance,
                    validProfile.TargetPort,
                    validProfile.LocalEndpoint,
                    SampleTimeout,
                    CancellationToken.None).Wait());
        }

        //---------------------------------------------------------------------
        // Transport.
        //---------------------------------------------------------------------

        [Test]
        public void WhenDisposedTwice_ThenTunnelIsOnlyDisposedOnce()
        {
            var protocol = new Mock<IProtocol>();
            var tunnel = new IapTunnel(
                new Mock<IIapListener>().Object,
                CreateTunnelProfile(SampleInstance, 22),
                IapTunnelFlags.None);

            var tunnelClosedEvents = 0;
            tunnel.Closed += (_, __) => tunnelClosedEvents++;

            var transport = new IapTransportFactory.Transport(
                tunnel,
                protocol.Object,
                SampleInstance);

            // Dispose once, closing the tunnel.
            transport.Dispose();
            Assert.AreEqual(1, tunnelClosedEvents);

            // Dispose again.
            transport.Dispose();
            Assert.AreEqual(1, tunnelClosedEvents);
        }

        [Test]
        public async Task WhenTunnelCreated_ThenCreateTransportReturnsTransport()
        {
            var validProfile = CreateTunnelProfile(SampleInstance, 22);
            var tunnelFactory = CreateTunnelFactory();
            tunnelFactory
                .Setup(f => f.CreateTunnelAsync(
                    validProfile,
                    It.IsAny<TimeSpan>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(CreateTunnel(validProfile));

            var factory = new IapTransportFactory(
                new Mock<IEventQueue>().Object,
                tunnelFactory.Object);

            using (var transport = await factory
                .CreateTransportAsync(
                    validProfile.Protocol,
                    validProfile.Policy,
                    validProfile.TargetInstance,
                    validProfile.TargetPort,
                    validProfile.LocalEndpoint,
                    SampleTimeout,
                    CancellationToken.None)
                .ConfigureAwait(false))
            {
                Assert.AreSame(validProfile.Protocol, transport.Protocol);
                Assert.AreSame(IPAddress.Loopback, transport.Endpoint.Address);
                Assert.AreSame(SampleInstance, transport.Target);
            }
        }
    }
}
