using System.Threading.Tasks;
using AElf.Contracts.Election;
using AElf.Contracts.MultiToken;
using AElf.Contracts.MultiToken.Messages;
using AElf.Contracts.Resource.FeeReceiver;
using AElf.Contracts.TestBase;
using AElf.Kernel;
using AElf.Kernel.Consensus.AEDPoS;
using AElf.Kernel.Token;
using AElf.OS.Node.Application;

namespace AElf.Contracts.Resource.Tests
{
    public static class ResourceContractTesterExtensions
    {
        public static async Task InitialChainAndTokenAsync(this ContractTester<ResourceContractTestAElfModule> starter)
        {
            var tokenContractCallList = new SystemContractDeploymentInput.Types.SystemTransactionMethodCallList();
            tokenContractCallList.Add(nameof(TokenContract.CreateNativeToken), new CreateNativeTokenInput
            {
                Symbol = "ELF",
                Decimals = 2,
                IsBurnable = true,
                TokenName = "elf token",
                TotalSupply = 1000_0000L,
                Issuer = starter.GetCallOwnerAddress()
            });
            
            // For testing.
            tokenContractCallList.Add(nameof(TokenContract.Issue), new IssueInput
            {
                Symbol = "ELF",
                Amount = 1000_000L,
                To = starter.GetCallOwnerAddress(),
                Memo = "Set dividends.",
            });
            
            await starter.InitialChainAsync(
                list =>
                {
                    // Dividends contract must be deployed before token contract.
                    list.AddGenesisSmartContract<TokenContract>(TokenSmartContractAddressNameProvider.Name, tokenContractCallList);
                    list.AddGenesisSmartContract<ResourceContract>(ResourceSmartContractAddressNameProvider.Name);
                    list.AddGenesisSmartContract<FeeReceiverContract>(ResourceFeeReceiverSmartContractAddressNameProvider.Name);
                });
        }
    }
}