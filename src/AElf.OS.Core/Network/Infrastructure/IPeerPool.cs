using System.Collections.Generic;
using System.Threading.Tasks;
using AElf.Types;
using Volo.Abp.DependencyInjection;

namespace AElf.OS.Network.Infrastructure
{
    public interface IPeerPool
    {
        int PeerCount { get; }
        
        Task<bool> AddPeerAsync(string address);
        Task<bool> RemovePeerByAddressAsync(string address);
        List<IPeer> GetPeers(bool includeFailing = false);
        IPeer GetBestPeer();
        
        IReadOnlyDictionary<long, Hash> RecentBlockHeightAndHashMappings { get; }
        void AddRecentBlockHeightAndHash(long blockHeight, Hash blockHash, bool hasFork);

        IPeer FindPeerByAddress(string peerIpAddress);
        IPeer FindPeerByPublicKey(string remotePubKey);
        
        Task<IPeer> RemovePeerAsync(string remotePubKey, bool sendDisconnect);

        Task<Handshake> GetHandshakeAsync();
    }
}