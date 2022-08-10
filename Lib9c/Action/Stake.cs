using System;
using System.Globalization;
using System.Linq;
using System.Numerics;
using Bencodex.Types;
using Libplanet;
using Libplanet.Action;
using Nekoyume.Extensions;
using Nekoyume.Model.State;
using Nekoyume.TableData;
using static Lib9c.SerializeKeys;

namespace Nekoyume.Action
{
    [ActionType("stake")]
    public class Stake : ActionBase
    {
        internal BigInteger Amount { get; set; }

        public Stake(BigInteger amount)
        {
            Amount = amount >= 0
                ? amount
                : throw new ArgumentOutOfRangeException(nameof(amount));
        }

        public Stake()
        {
        }

        public override IValue PlainValue =>
            Dictionary.Empty.Add(AmountKey, (IValue) (Integer) Amount);

        public override void LoadPlainValue(IValue plainValue)
        {
            var dictionary = (Dictionary) plainValue;
            Amount = dictionary[AmountKey].ToBigInteger();
        }

        public override IAccountStateDelta Execute(IActionContext context)
        {
            IAccountStateDelta states = context.PreviousStates;

            // Restrict staking if there is a monster collection until now.
            if (states.GetAgentState(context.Signer) is { } agentState &&
                states.TryGetState(MonsterCollectionState.DeriveAddress(
                    context.Signer,
                    agentState.MonsterCollectionRound), out Dictionary _))
            {
                throw new MonsterCollectionExistingException();
            }

            if (context.Rehearsal)
            {
                return states.SetState(StakeState.DeriveAddress(context.Signer), MarkChanged)
                    .MarkBalanceChanged(
                        GoldCurrencyMock,
                        context.Signer,
                        StakeState.DeriveAddress(context.Signer));
            }

            if (context.BlockIndex > 4286621 && context.BlockIndex < 4287696)
            {
                if (
                    context.TxId.ToString() ==
                    "a9ee9611c0a319c1c726f75ff9dab0551e07f7b95c611db80d370308010b28d3" ||
                    context.TxId.ToString() ==
                    "18cea2547a29da7f58c0979a8f253a59bfbb8c5f73b10f76b1938cc45f23cdb6" ||
                    context.TxId.ToString() ==
                    "8e2e0c97ab68aec8db94562f8f75ec4cc35f026e035481be6122fdca2020085a" ||
                    context.TxId.ToString() ==
                    "0862c8103e3f4d63d51c492f7417bd98860843f2f9953d9032a65b2abc1bd437" ||
                    context.TxId.ToString() ==
                    "8017498b465233e32f23b1963975771ded84b9bcc11510aeee9c337451f46d26" ||
                    context.TxId.ToString() ==
                    "c407c37c8cc6b3cbb4329180c0c46d949003ad47199f995c7da245cab20722b4" ||
                    context.TxId.ToString() ==
                    "1e8bbce4e60051cfb609ef080470fd231891d17bcaf4b4e9a2a1bee9ad6ea3ab" ||
                    context.TxId.ToString() ==
                    "d45b86a2adddf4f7e82786a25597c64a19f1fa9c1683628e56ab25267e7ae85f" ||
                    context.TxId.ToString() ==
                    "4d63437d49c2608ffa41c46d7cfba68fdf15afd61573cc3376ee6a87a65f63e5" ||
                    context.TxId.ToString() ==
                    "e4a21d154ceb8dabe1c099bdb7865da19e53d23d99da31b8cfe980445e0f1ff9" ||
                    context.TxId.ToString() ==
                    "e679161cb75c25b84811f306a797763177b0e05477f4bd5699e8c92639342e9f" ||
                    context.TxId.ToString() ==
                    "d503ee08718eb807a858f03a61fd3de2d2aefc0e9cccf34e6890f889ae3f42c3" ||
                    context.TxId.ToString() ==
                    "ebb5401547c9f5289849a7c952ec54f0871faa232a529c3254a5a96d14a9a908" ||
                    context.TxId.ToString() ==
                    "455725378438d14e641ae3ee67fb5ef2944492bc0902dc3aecab5905cd62eca7" ||
                    context.TxId.ToString() ==
                    "c1273877a55441d2e76b18f7513fa06ddeef980b4265b773151f2bc416953d32" ||
                    context.TxId.ToString() ==
                    "dad5ba7f76e0b4f6c0a0604d0e88fc679bc092f09abe485fb72cfa6c2c5fed7d" ||
                    context.TxId.ToString() ==
                    "d1c583d2fb8cbe46ff72c235d503f55a193489978386be54218f7bf9a2cbeef8" ||
                    context.TxId.ToString() ==
                    "31899cad515b536837ee3bc6a073be5d05a05a78c76d5d23b0a0c4b8d85d4d25" ||
                    context.TxId.ToString() ==
                    "fecdc25eb45fc0181592bd1da4b9e579e7631769442a8b3e6e9cdaa2aef35736" ||
                    context.TxId.ToString() ==
                    "5c853a8deb366dee217eb842c0a4f7ff1b84cb73ddd4121590d416c47372f508" ||
                    context.TxId.ToString() ==
                    "f0752d55946b3e44bfe28a6d6610246ba772055044e34ecbbb37b03a4bc55a1e")
                {
                    // do nothing and move onto execution
                }
                else
                {
                    return states;
                }
            }

            if (Amount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(Amount));
            }

            var stakeRegularRewardSheet = states.GetSheet<StakeRegularRewardSheet>();
            var minimumRequiredGold = stakeRegularRewardSheet.OrderedRows.Min(x => x.RequiredGold);
            if (Amount != 0 && Amount < minimumRequiredGold)
            {
                throw new ArgumentOutOfRangeException(nameof(Amount));
            }

            var stakeStateAddress = StakeState.DeriveAddress(context.Signer);
            var currency = states.GetGoldCurrency();
            var currentBalance = states.GetBalance(context.Signer, currency);
            var stakedBalance = states.GetBalance(stakeStateAddress, currency);
            var targetStakeBalance = currency * Amount;
            if (currentBalance + stakedBalance < targetStakeBalance)
            {
                throw new NotEnoughFungibleAssetValueException(
                    context.Signer.ToHex(),
                    Amount,
                    currentBalance);
            }

            // Stake if it doesn't exist yet.
            if (!states.TryGetStakeState(context.Signer, out StakeState stakeState))
            {
                stakeState = new StakeState(stakeStateAddress, context.BlockIndex);
                return states
                    .SetState(
                        stakeStateAddress,
                        stakeState.SerializeV2())
                    .TransferAsset(context.Signer, stakeStateAddress, targetStakeBalance);
            }

            if (stakeState.IsClaimable(context.BlockIndex))
            {
                throw new StakeExistingClaimableException();
            }

            if (!stakeState.IsCancellable(context.BlockIndex) &&
                (context.BlockIndex >= 4611070
                    ? targetStakeBalance <= stakedBalance
                    : targetStakeBalance < stakedBalance))
            {
                throw new RequiredBlockIndexException();
            }

            // Cancel
            if (Amount == 0)
            {
                if (stakeState.IsCancellable(context.BlockIndex))
                {
                    return states.SetState(stakeState.address, Null.Value)
                        .TransferAsset(stakeState.address, context.Signer, stakedBalance);
                }
            }

            // Stake with more or less amount.
            return states.TransferAsset(stakeState.address, context.Signer, stakedBalance)
                .TransferAsset(context.Signer, stakeState.address, targetStakeBalance)
                .SetState(
                    stakeState.address,
                    new StakeState(stakeState.address, context.BlockIndex).SerializeV2());
        }
    }
}
