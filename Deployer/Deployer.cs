using System.Security.Cryptography;
using System.Text;
using AElf.Client;
using AElf.Cryptography;
using AElf.Cryptography.ECDSA;
using AElf.CSharp.Core;
using AElf.Types;
using Google.Protobuf;
using Grpc.Net.Client;
using Token;

namespace Deployer;

public class Deployer
{
    private readonly AElfClient _client;
    private readonly ECKeyPair _keyPair;

    public Deployer(string baseUrl, byte[] privateKey)
    {
        _client = new AElfClient(baseUrl);
        _keyPair = CryptoHelper.FromPrivateKey(privateKey);
        TokenContractStub = new TokenContractContainer.TokenContractStub();
    }

    internal async Task SetupAsync()
    {
        var hasher = SHA256.Create();
        var hash = hasher.ComputeHash(Encoding.UTF8.GetBytes("AElf.ContractNames.Token"));
        var tokenAddr = await _client.GetContractAddressByNameAsync(new Hash
        {
            Value = ByteString.CopyFrom(hash)
        });
        TokenContractStub = new TokenContractContainer.TokenContractStub
        {
            __factory = GetMethodStubFactory(tokenAddr)
        };
    }

    internal async Task<TContractStub> GetContractInstanceAsync<TContractStub>(ContractType contractType)
        where TContractStub : ContractStubBase, new()
    {
        async Task<Address> GetAddressAsync(string name)
        {
            var hasher = SHA256.Create();
            var hash = hasher.ComputeHash(Encoding.UTF8.GetBytes(name));
            return await _client.GetContractAddressByNameAsync(new Hash
            {
                Value = ByteString.CopyFrom(hash)
            });
        }

        var address = contractType switch
        {
            ContractType.Token => await GetAddressAsync("AElf.ContractNames.Token"),
            _ => throw new ArgumentOutOfRangeException(nameof(contractType), contractType, null)
        };

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

    internal TokenContractContainer.TokenContractStub TokenContractStub { get; private set; }
}