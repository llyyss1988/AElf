using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using AElf.OS.Network.Events;
using AElf.OS.Network.Infrastructure;
using AElf.OS.Node.Application;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus.Local;

namespace AElf.OS.Network.Grpc
{
    public class GrpcNetworkServer : IAElfNetworkServer, ISingletonDependency
    {
        private readonly IGrpcPeerPool _grpcPeerPool;
        
        private NetworkOptions NetworkOptions => NetworkOptionsSnapshot.Value;
        public IOptionsSnapshot<NetworkOptions> NetworkOptionsSnapshot { get; set; }

        private readonly PeerService.PeerServiceBase _serverService;
        private readonly AuthInterceptor _authInterceptor;

        private Server _server;

        public ILocalEventBus EventBus { get; set; }
        public ILogger<GrpcNetworkServer> Logger { get; set; }

        public GrpcNetworkServer(PeerService.PeerServiceBase serverService, IGrpcPeerPool grpcPeerPool, 
            AuthInterceptor authInterceptor)
        {
            _serverService = serverService;
            _authInterceptor = authInterceptor;
            _grpcPeerPool = grpcPeerPool;

            Logger = NullLogger<GrpcNetworkServer>.Instance;
            EventBus = NullLocalEventBus.Instance;
        }

        public async Task StartAsync()
        {
            ServerServiceDefinition serviceDefinition = PeerService.BindService(_serverService);

            if (_authInterceptor != null)
                serviceDefinition = serviceDefinition.Intercept(_authInterceptor);

            _server = new Server(new List<ChannelOption>
            {
                new ChannelOption(ChannelOptions.MaxSendMessageLength, GrpcConstants.DefaultMaxSendMessageLength),
                new ChannelOption(ChannelOptions.MaxReceiveMessageLength, GrpcConstants.DefaultMaxReceiveMessageLength)
            })
            {
                Services = {serviceDefinition},
                Ports =
                {
                    new ServerPort(IPAddress.Any.ToString(), NetworkOptions.ListeningPort, ServerCredentials.Insecure)
                }
            };

            await Task.Run(() => _server.Start());

            // Add the provided boot nodes
            if (NetworkOptions.BootNodes != null && NetworkOptions.BootNodes.Any())
            {
                List<Task<bool>> taskList = NetworkOptions.BootNodes.Select(_grpcPeerPool.AddPeerAsync).ToList();
                await Task.WhenAll(taskList.ToArray<Task>());
            }
            else
            {
                Logger.LogWarning("Boot nodes list is empty.");
            }

            await EventBus.PublishAsync(new NetworkInitializationFinishedEvent());
        }

        public async Task StopAsync(bool gracefulDisconnect = true)
        {
            try
            {
                await _server.KillAsync();
            }
            catch (InvalidOperationException)
            {
                // if server already shutdown, we continue and clear the channels.
            }

            await _grpcPeerPool.ClearAllPeersAsync(gracefulDisconnect);
        }

        public void Dispose()
        {
        }
    }
}