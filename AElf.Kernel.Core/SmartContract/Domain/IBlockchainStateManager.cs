using System;
using System.Linq;
using System.Threading.Tasks;
using AElf.Common;
using AElf.Kernel.Infrastructure;
using AElf.Kernel.SmartContract.Infrastructure;
using Google.Protobuf;
using Volo.Abp.DependencyInjection;

namespace AElf.Kernel.SmartContract.Domain
{

    public interface IBlockchainStateManager
    {
        //Task<VersionedState> GetVersionedStateAsync(Hash blockHash,long blockHeight, string key);
        Task<ByteString> GetStateAsync(string key, ulong blockHeight, Hash blockHash);
        Task SetBlockStateSetAsync(BlockStateSet blockStateSet);
        Task MergeBlockStateAsync(int chainId, Hash blockStateHash);
        Task<ChainStateInfo> GetChainStateInfoAsync(int chainId);
    }

    public class BlockchainStateManager : IBlockchainStateManager, ITransientDependency
    {
        private readonly IStateStore<VersionedState> _versionedStates;
        private readonly IStateStore<BlockStateSet> _blockStateSets;

        private readonly IStateStore<ChainStateInfo> _chainStateInfoCollection;

        public BlockchainStateManager(IStateStore<VersionedState> versionedStates,
            IStateStore<BlockStateSet> blockStateSets, IStateStore<ChainStateInfo> chainStateInfoCollection)
        {
            _versionedStates = versionedStates;
            _blockStateSets = blockStateSets;
            _chainStateInfoCollection = chainStateInfoCollection;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="key"></param>
        /// <param name="blockHeight"></param>
        /// <param name="blockHash">should already in store</param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public async Task<ByteString> GetStateAsync(string key, ulong blockHeight, Hash blockHash)
        {
            ByteString value = null;

            //first DB read
            var bestChainState = await _versionedStates.GetAsync(key);

            if (bestChainState != null)
            {
                if (bestChainState.BlockHash == blockHash)
                {
                    value = bestChainState.Value;
                }
                else
                {
                    if (bestChainState.BlockHeight >= blockHeight)
                    {
                        //because we may clear history state
                        throw new InvalidOperationException("cannot read history state");
                    }
                    else
                    {
                        //find value in block state set
                        var blockStateKey = blockHash.ToHex();
                        var blockStateSet = await _blockStateSets.GetAsync(blockStateKey);
                        while (blockStateSet != null && blockStateSet.BlockHeight > bestChainState.BlockHeight)
                        {
                            if (blockStateSet.Changes.ContainsKey(key))
                            {
                                value = blockStateSet.Changes[key];
                                break;
                            }

                            blockStateKey = blockStateSet.PreviousHash?.ToHex();

                            if (blockStateKey != null)
                            {
                                blockStateSet = await _blockStateSets.GetAsync(blockStateKey);
                            }
                            else
                            {
                                blockStateSet = null;
                            }
                        }

                        if (value == null)
                        {
                            //not found value in block state sets. for example, best chain is 100, blockHeight is 105,
                            //it will find 105 ~ 101 block state set. so the value could only be the best chain state value.
                            value = bestChainState.Value;
                        }
                    }
                }
            }
            else
            {
                //best chain state is null, it will find value in block state set
                var blockStateKey = blockHash.ToHex();
                var blockStateSet = await _blockStateSets.GetAsync(blockStateKey);
                while (blockStateSet != null)
                {
                    if (blockStateSet.Changes.ContainsKey(key))
                    {
                        value = blockStateSet.Changes[key];
                        break;
                    }

                    blockStateKey = blockStateSet.PreviousHash?.ToHex();

                    if (blockStateKey != null)
                    {
                        blockStateSet = await _blockStateSets.GetAsync(blockStateKey);
                    }
                    else
                    {
                        blockStateSet = null;
                    }
                }
            }

            return value;
        }

        public async Task SetBlockStateSetAsync(BlockStateSet blockStateSet)
        {
            await _blockStateSets.SetAsync(GetKey(blockStateSet), blockStateSet);
        }

        public async Task MergeBlockStateAsync(int chainId, Hash blockStateHash)
        {
            var chainStateInfo = await GetChainStateInfoAsync(chainId);

            var blockState = await _blockStateSets.GetAsync(blockStateHash.ToStorageKey());
            if (blockState == null)
            {
                if (chainStateInfo.Status == ChainStateMergingStatus.Merged &&
                    chainStateInfo.MergingBlockHash == blockStateHash)
                {
                    chainStateInfo.Status = ChainStateMergingStatus.Common;
                    chainStateInfo.MergingBlockHash = null;

                    await _chainStateInfoCollection.SetAsync(chainId.ToStorageKey(), chainStateInfo);
                    return;
                }

                throw new InvalidOperationException($"cannot get block state of {blockStateHash}");
            }

            if (chainStateInfo.BlockHash == null || chainStateInfo.BlockHash == blockState.PreviousHash)
            {
                if (chainStateInfo.Status != ChainStateMergingStatus.Common)
                {
                    throw new InvalidOperationException("another merging");
                }

                chainStateInfo.Status = ChainStateMergingStatus.Merging;
                chainStateInfo.MergingBlockHash = blockStateHash;

                await _chainStateInfoCollection.SetAsync(chainId.ToStorageKey(), chainStateInfo);
                var dic = blockState.Changes.Select(change => new VersionedState()
                {
                    Key = change.Key,
                    Value = change.Value,
                    BlockHash = blockState.BlockHash,
                    BlockHeight = blockState.BlockHeight,
                    //OriginBlockHash = origin.BlockHash
                }).ToDictionary(p => p.Key, p => p);

                await _versionedStates.PipelineSetAsync(dic);

                chainStateInfo.Status = ChainStateMergingStatus.Merged;
                chainStateInfo.BlockHash = blockState.BlockHash;
                chainStateInfo.BlockHeight = blockState.BlockHeight;
                await _chainStateInfoCollection.SetAsync(chainId.ToStorageKey(), chainStateInfo);

                await _blockStateSets.RemoveAsync(blockStateHash.ToStorageKey());

                chainStateInfo.Status = ChainStateMergingStatus.Common;
                chainStateInfo.MergingBlockHash = null;

                await _chainStateInfoCollection.SetAsync(chainId.ToStorageKey(), chainStateInfo);
            }
            else
            {
                throw new InvalidOperationException(
                    "cannot merge block not linked, check new block's previous block hash ");
            }
        }

        public async Task<ChainStateInfo> GetChainStateInfoAsync(int chainId)
        {
            var o = await _chainStateInfoCollection.GetAsync(chainId.ToStorageKey());
            return o ?? new ChainStateInfo() {ChainId = chainId};
        }


        private string GetKey(BlockStateSet blockStateSet)
        {
            return blockStateSet.BlockHash.ToStorageKey();
        }
    }
}