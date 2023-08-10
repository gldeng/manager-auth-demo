using System.Security.Cryptography;
using System.Text;
using AElf.Client;
using AElf.Contracts.Consensus.AEDPoS;
using AElf.Contracts.Genesis;
using AElf.Contracts.Parliament;
using AElf.Cryptography;
using AElf.Cryptography.ECDSA;
using AElf.CSharp.Core;
using AElf.Standards.ACS0;
using AElf.Standards.ACS3;
using AElf.Types;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Token;

namespace Deployer;

public class Deployer
{
    private readonly AElfClient _client;
    private readonly ECKeyPair _keyPair;
    private Address _genesisContractAddress;
    internal Address ParliamentContractAddress;

    public Deployer(string baseUrl, byte[] privateKey)
    {
        _client = new AElfClient(baseUrl);
        _keyPair = CryptoHelper.FromPrivateKey(privateKey);
        TokenContractStub = new TokenContractContainer.TokenContractStub();
    }

    internal async Task SetupAsync()
    {
        TokenContractStub =
            await GetContractInstanceAsync<TokenContractContainer.TokenContractStub>(ContractType.Token);
        ParliamentContractStub =
            await GetContractInstanceAsync<ParliamentContractContainer.ParliamentContractStub>(ContractType.Parliament);
        AuthorizationContractStub =
            await GetContractInstanceAsync<AuthorizationContractContainer.AuthorizationContractStub>(ContractType
                .Parliament);
        AEDPoSContractStub = await GetContractInstanceAsync<AEDPoSContractContainer.AEDPoSContractStub>(ContractType.Consensus);
        ParliamentContractAddress = await GetAddressAsync(ContractType.Parliament);
        var res = await _client.GetGenesisContractAddressAsync();
        _genesisContractAddress = Address.FromBase58(res);
        GenesisContractStub = new ACS0Container.ACS0Stub
        {
            __factory = GetMethodStubFactory(_genesisContractAddress)
        };
        BasicContractZeroStub = new BasicContractZeroContainer.BasicContractZeroStub
        {
            __factory = GetMethodStubFactory(_genesisContractAddress)
        };
        await MaybeAddGenesisAsProposerAsync();
    }

    internal async Task<Address?> DeployAsync(string filename)
    {
        var code = await File.ReadAllBytesAsync(filename);
        await SetupAsync();
        var result0 = await GenesisContractStub.DeployUserSmartContract.SendAsync(new ContractDeploymentInput
        {
            Category = 0,
            Code = ByteString.CopyFrom(code),
        });
        var codeHash = result0.Output.CodeHash;

        var proposalCreated = result0.TransactionResult.Logs.FirstOrDefault(l => l.Name == nameof(ProposalCreated));
        return await CheckDeployedAddressAsync(codeHash);
    }

    internal async Task<Address?> CheckDeployedAddressAsync(Hash codeHash)
    {
        var remainingRetries = 1000;
        while (remainingRetries > 0)
        {
            var result = await GenesisContractStub.GetSmartContractRegistrationByCodeHash.CallAsync(codeHash);
            if (result?.ContractAddress != null)
            {
                return result.ContractAddress;
            }

            remainingRetries--;
            Thread.Sleep(500);
        }

        return null;
    }
    
    internal async Task<Address> GetAddressAsync(ContractType contractType)
    {
        async Task<Address> GetAsync(string name)
        {
            var hasher = SHA256.Create();
            var hash = hasher.ComputeHash(Encoding.UTF8.GetBytes(name));
            return await _client.GetContractAddressByNameAsync(new Hash
            {
                Value = ByteString.CopyFrom(hash)
            });
        }

        return contractType switch
        {
            ContractType.Token => await GetAsync("AElf.ContractNames.Token"),
            ContractType.Parliament => await GetAsync("AElf.ContractNames.Parliament"),
            ContractType.Consensus => await GetAsync("AElf.ContractNames.Consensus"),
            _ => throw new ArgumentOutOfRangeException(nameof(contractType), contractType, null)
        };
    }

    internal async Task<TContractStub> GetContractInstanceAsync<TContractStub>(ContractType contractType)
        where TContractStub : ContractStubBase, new()
    {
        var address = await GetAddressAsync(contractType);

        return GetContractInstance<TContractStub>(address);
    }

    private TContractStub GetContractInstance<TContractStub>(Address address)
        where TContractStub : ContractStubBase, new()
    {
        return new TContractStub
        {
            __factory = GetMethodStubFactory(address)
        };
    }

    private IMethodStubFactory GetMethodStubFactory(Address address)
    {
        return new MethodStubFactory(_client, address, _keyPair);
    }

    private async Task MaybeAddGenesisAsProposerAsync()
    {
        var whiteList = await ParliamentContractStub.GetProposerWhiteList.CallAsync(new Empty());
        var alreadyInWhiteList = whiteList.Proposers.Contains(_genesisContractAddress);
        if (alreadyInWhiteList)
        {
            return;
        }

        whiteList.Proposers.Add(_genesisContractAddress);

        Timestamp GetExpiryTime()
        {
            var now = DateTime.UtcNow;
            var oneHourLater = now.AddHours(1);
            return Timestamp.FromDateTime(oneHourLater);
        }

        var organizationAddress = await ParliamentContractStub.GetDefaultOrganizationAddress.CallAsync(new Empty());

        var result = await AuthorizationContractStub.CreateProposal.SendAsync(new CreateProposalInput
        {
            ContractMethodName = nameof(AuthorizationContractStub.ChangeOrganizationProposerWhiteList),
            ToAddress = ParliamentContractAddress,
            Params = whiteList.ToByteString(),
            ExpiredTime = GetExpiryTime(),
            OrganizationAddress = organizationAddress,
        });

        await AuthorizationContractStub.Approve.SendAsync(result.Output);
        await AuthorizationContractStub.Release.SendAsync(result.Output);
    }

    internal TokenContractContainer.TokenContractStub TokenContractStub { get; private set; }

    internal ACS0Container.ACS0Stub GenesisContractStub { get; private set; }
    internal BasicContractZeroContainer.BasicContractZeroStub BasicContractZeroStub { get; private set; }
    internal ParliamentContractContainer.ParliamentContractStub ParliamentContractStub { get; private set; }
    internal AuthorizationContractContainer.AuthorizationContractStub AuthorizationContractStub { get; private set; }
    internal AEDPoSContractContainer.AEDPoSContractStub AEDPoSContractStub { get; private set; }
}