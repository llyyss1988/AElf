using System.Threading.Tasks;
using AElf.OS.Network.Infrastructure;
using Volo.Abp.DependencyInjection;

namespace AElf.OS.Network.Grpc
{
    public interface IGrpcPeerPool : IPeerPool
    {
        bool AddPeer(GrpcPeer peer);
        GrpcPeer FindGrpcPeerByPublicKey(string publicKey);
        Task ClearAllPeersAsync(bool sendDisconnect);
    }
}