using System.Collections.Generic;
using System.Threading.Tasks;
using AElf.ChainController;
using AElf.Common;
using AElf.Execution.Execution;
using AElf.Kernel;
using AElf.Kernel.Managers;
using AElf.Kernel.Storages;
using AElf.Miner.TxMemPool;
using AElf.SmartContract;
using AElf.SmartContract.Metadata;
using AElf.Synchronization.BlockExecution;
using AElf.Synchronization.BlockSynchronization;
using Moq;
using NLog;

namespace AElf.Synchronization.Tests
{
    public class MockSetup
    {
        private ILogger _logger = LogManager.GetLogger("Synchronization.Tests");
        private List<IBlockHeader> _headers = new List<IBlockHeader>();
        private List<IBlockHeader> _sideChainHeaders = new List<IBlockHeader>();
        private List<IBlock> _blocks = new List<IBlock>();
        
        private readonly IDataStore _dataStore;
        private readonly IStateStore _stateStore;
        private readonly ISmartContractManager _smartContractManager;
        private ITransactionManager _transactionManager;
        private ITransactionResultManager _transactionResultManager;
        private ITransactionTraceManager _transactionTraceManager;
        private ISmartContractRunnerContainer _smartContractRunnerContainer;
        private IFunctionMetadataService _functionMetadataService;
        private IExecutingService _concurrencyExecutingService;
        private ITxHub _txHub;
        private IBlockManager _blockManager;
        private IChainManager _chainManager;
        private IChainService _chainService;

        // private IBlockSynchronizer _blockSynchronizer;

        public MockSetup(IDataStore dataStore, IStateStore stateStore, ITxHub txHub)
        {
            _dataStore = dataStore;
            _stateStore = stateStore;
            
            _smartContractManager = new SmartContractManager(_dataStore);
            _transactionManager = new TransactionManager(_dataStore);
            _transactionTraceManager = new TransactionTraceManager(_dataStore);
            _transactionResultManager = new TransactionResultManager(_dataStore);
            _smartContractRunnerContainer = new SmartContractRunnerContainer();
            _functionMetadataService = new FunctionMetadataService(_dataStore, _logger);
            _blockManager = new BlockManager(_dataStore);
            _chainManager = new ChainManager(_dataStore);
            _chainService = new ChainService(_chainManager, _blockManager, _transactionManager, _transactionTraceManager,
                _dataStore, _stateStore);
            _concurrencyExecutingService = new SimpleExecutingService(
                new SmartContractService(_smartContractManager, _smartContractRunnerContainer, _stateStore,
                    _functionMetadataService, _chainService), _transactionTraceManager, _stateStore,
                new ChainContextService(GetChainService()));
            _txHub = txHub;
            _chainManager = new ChainManager(dataStore);
        }

        public IBlockSynchronizer GetBlockSynchronizer()
        {
            var executor = GetBlockExecutor();
            return new BlockSynchronizer(GetChainService(), GetBlockValidationService(), executor,
                new BlockSet(), null);
        }

        public IChainService GetChainService()
        {
            Mock<IChainService> mock = new Mock<IChainService>();
            mock.Setup(cs => cs.GetLightChain(It.IsAny<Hash>())).Returns(MockLightChain().Object);
            mock.Setup(cs => cs.GetBlockChain(It.IsAny<Hash>())).Returns(MockBlockChain().Object);
            return mock.Object;
        }

        private Mock<ILightChain> MockLightChain()
        {
            Mock<ILightChain> mock = new Mock<ILightChain>();
            mock.Setup(lc => lc.GetCurrentBlockHeightAsync())
                .Returns(Task.FromResult((ulong) _headers.Count - 1 + GlobalConfig.GenesisBlockHeight));
            mock.Setup(lc => lc.GetHeaderByHeightAsync(It.IsAny<ulong>()))
                .Returns<ulong>(p => Task.FromResult(_sideChainHeaders[(int) p - 1]));

            return mock;
        }

        private Mock<IBlockChain> MockBlockChain()
        {
            Mock<IBlockChain> mock = new Mock<IBlockChain>();
            mock.Setup(bc => bc.GetBlockByHeightAsync(It.IsAny<ulong>(), false))
                .Returns<ulong>(p => Task.FromResult(_blocks[(int) p - 1]));
            return mock;
        }
        
        /// <summary>
        /// Which will always return true.
        /// </summary>
        /// <returns></returns>
        public IBlockValidationService GetBlockValidationService()
        {
            var mock = new Mock<IBlockValidationService>();
            mock.Setup(bvs => bvs.ValidateBlockAsync(It.IsAny<IBlock>(), It.IsAny<IChainContext>()))
                .Returns(() => Task.FromResult(BlockValidationResult.Success));
            return mock.Object;
        }

        public IBlockExecutor GetBlockExecutor()
        {
            return new BlockExecutor(GetChainService(), _concurrencyExecutingService, 
                _transactionResultManager, null, null, _txHub,_stateStore);
        }
    }
}