using System.Collections.Generic;
using System.Collections.Immutable;
using System.Security.Cryptography;
using Libplanet;
using Libplanet.Action;
using Libplanet.Assets;
using Libplanet.Blockchain;
using Libplanet.Blockchain.Policies;
using Libplanet.Blocks;
using Libplanet.Crypto;
using Libplanet.PoS;
using Libplanet.Tx;
using Nekoyume.Action;

namespace Nekoyume.BlockChain.Policy
{
    public class DebugPolicy : IBlockPolicy<PolymorphicAction<ActionBase>>
    {
        public DebugPolicy(long blockDifficulty)
        {
            _blockDifficulty = blockDifficulty;
        }

        public IAction BlockAction { get; } = new RewardGold();

        private readonly long _blockDifficulty;

        public TxPolicyViolationException ValidateNextBlockTx(
            BlockChain<PolymorphicAction<ActionBase>> blockChain,
            Transaction<PolymorphicAction<ActionBase>> transaction)
        {
            return null;
        }

        public BlockPolicyViolationException ValidateNextBlock(
            BlockChain<PolymorphicAction<ActionBase>> blockChain,
            Block<PolymorphicAction<ActionBase>> nextBlock)
        {
            return null;
        }

        public long GetNextBlockDifficulty(BlockChain<PolymorphicAction<ActionBase>> blockChain)
        {
            return blockChain.Count > 0 ? _blockDifficulty : 0;
        }

        public long GetMaxBlockBytes(long index) => long.MaxValue;

        public int GetMinTransactionsPerBlock(long index) => 0;

        public int GetMaxTransactionsPerBlock(long index) => int.MaxValue;

        public int GetMaxTransactionsPerSignerPerBlock(long index) => int.MaxValue;

        public IEnumerable<PublicKey> GetValidators()
        {
            throw new System.NotImplementedException();
        }

        public IImmutableSet<Currency> NativeTokens => new Currency[] { Asset.GovernanceToken }.ToImmutableHashSet();

        public IAction UpdateValidatorSetAction => null;
    }
}
