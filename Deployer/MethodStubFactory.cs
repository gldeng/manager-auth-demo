using AElf.Client;
using AElf.Client.Dto;
using AElf.Cryptography;
using AElf.Cryptography.ECDSA;
using AElf.CSharp.Core;
using AElf.Types;
using Google.Protobuf;
using static System.Enum;

namespace Deployer;

public class MethodStubFactory : IMethodStubFactory
{
    private readonly AElfClient _client;
    private readonly Address _address;
    private readonly ECKeyPair _keyPair;
    private Address Sender => Address.FromPublicKey(_keyPair.PublicKey);

    public MethodStubFactory(AElfClient client, Address contractAddress, ECKeyPair keyPair)
    {
        _client = client;
        _address = contractAddress;
        _keyPair = keyPair;
    }

    public IMethodStub<TInput, TOutput> Create<TInput, TOutput>(Method<TInput, TOutput> method)
        where TInput : IMessage<TInput>, new() where TOutput : IMessage<TOutput>, new()
    {
        async Task<IExecutionResult<TOutput>> SendAsync(TInput input)
        {
            var refBlockInfo = await GetRefBlockInfoAsync();
            var transaction = GetTransaction(method, input, refBlockInfo);
            var txIdResult = await _client.SendTransactionAsync(new SendTransactionInput()
            {
                RawTransaction = BitConverter.ToString(transaction.ToByteArray()).Replace("-", string.Empty)
            });
            var result = await _client.GetTransactionResultAsync(txIdResult!.TransactionId);
            TryParse<TransactionResultStatus>(result!.Status, out var status);
            return new ExecutionResult<TOutput>
            {
                Transaction = transaction, TransactionResult = new TransactionResult
                {
                    BlockHash = Hash.LoadFromHex(result!.BlockHash),
                    BlockNumber = result.BlockNumber,
                    Bloom = ByteString.CopyFrom(result.Bloom.DecodeHex()),
                    Error = result.Error,
                    ReturnValue = ByteString.CopyFrom(result.ReturnValue.DecodeHex()),
                    Status = status,
                    TransactionId = Hash.LoadFromHex(result.TransactionId),
                },
                Output = method.ResponseMarshaller.Deserializer(result.ReturnValue.DecodeHex())
            };
        }

        async Task<TOutput> CallAsync(TInput input)
        {
            var kp = CryptoHelper.GenerateKeyPair();
            var transaction = GetTransactionWithoutSignature(input, method);
            transaction.From = Address.FromPublicKey(kp.PublicKey);

            var signature = CryptoHelper.SignWithPrivateKey(kp.PrivateKey, transaction.GetHash().Value.ToByteArray());
            transaction.Signature = ByteString.CopyFrom(signature);
            var returnValue = await _client.ExecuteTransactionAsync(new ExecuteTransactionDto
            {
                RawTransaction = BitConverter.ToString(transaction.ToByteArray()).Replace("-", string.Empty)
            });
            return method.ResponseMarshaller.Deserializer(returnValue.DecodeHex());
        }

        return new MethodStub<TInput, TOutput>(method, SendAsync, CallAsync);
    }

    private Transaction GetTransactionWithoutSignature<TInput, TOutput>(TInput input,
        Method<TInput, TOutput> method)
        where TInput : IMessage<TInput>, new() where TOutput : IMessage<TOutput>, new()
    {
        var transaction = new Transaction
        {
            From = Sender,
            To = _address,
            MethodName = method.Name,
            Params = ByteString.CopyFrom(method.RequestMarshaller.Serializer(input))
        };

        return transaction;
    }

    private async Task<(long, ByteString)> GetRefBlockInfoAsync()
    {
        ByteString GetRefBlockPrefix(Hash blockHash)
        {
            var refBlockPrefix = ByteString.CopyFrom(blockHash.Value.Take(4).ToArray());
            return refBlockPrefix;
        }

        var height = await _client.GetBlockHeightAsync();
        var block = await _client.GetBlockByHeightAsync(height);
        var hash = Hash.LoadFromHex(block!.BlockHash);
        return (height, GetRefBlockPrefix(hash));
    }

    private Transaction GetTransaction<TInput, TOutput>(Method<TInput, TOutput> method, TInput input,
        (long, ByteString) refBlockInfo)
        where TInput : IMessage<TInput>, new() where TOutput : IMessage<TOutput>, new()
    {
        var transaction = GetTransactionWithoutSignature(input, method);
        transaction.RefBlockNumber = refBlockInfo.Item1;
        transaction.RefBlockPrefix = refBlockInfo.Item2;

        var signature = CryptoHelper.SignWithPrivateKey(_keyPair.PrivateKey, transaction.GetHash().Value.ToByteArray());
        transaction.Signature = ByteString.CopyFrom(signature);
        return transaction;
    }
}