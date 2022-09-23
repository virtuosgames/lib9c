using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Security.Cryptography;
using Libplanet;
using Libplanet.Action;
using Libplanet.Action.Sys;
using Libplanet.Assets;
using Libplanet.Blockchain;
using Libplanet.Blocks;
using Libplanet.Crypto;
using Libplanet.PoS;
using Libplanet.Tx;
using Nekoyume.Action;
using Nekoyume.BlockChain.Policy;
using Nekoyume.Model.State;
using Nekoyume.TableData;
using Serilog;

namespace Nekoyume
{
    public static class BlockHelper
    {
        public static List<Transaction<PolymorphicAction<ActionBase>>> GenesisValidatorTransaction(
            List<PrivateKey> privateKeys)
        {
            var txs = new List<Transaction<PolymorphicAction<ActionBase>>>();
            Currency governance = Asset.GovernanceToken;
            foreach (var privateKey in privateKeys)
            {
                txs.Add(Transaction<PolymorphicAction<ActionBase>>.Create(
                    0,
                    privateKey,
                    null,
                    new Mint(
                        privateKey.ToAddress(),
                        new FungibleAssetValue(governance, 1000, 0))));
                txs.Add(Transaction<PolymorphicAction<ActionBase>>.Create(
                    1,
                    privateKey,
                    null,
                    new PromoteValidator(
                        privateKey.PublicKey,
                        new FungibleAssetValue(governance, 300, 0))));
            }

            return txs;
        }
        public static Block<PolymorphicAction<ActionBase>> ProposeGenesisBlock(
            IDictionary<string, string> tableSheets,
            GoldDistribution[] goldDistributions,
            PendingActivationState[] pendingActivationStates,
            AdminState adminState = null,
            AuthorizedMinersState authorizedMinersState = null,
            IImmutableSet<Address> activatedAccounts = null,
            bool isActivateAdminAddress = false,
            IEnumerable<string> credits = null,
            PrivateKey privateKey = null,
            DateTimeOffset? timestamp = null,
            IEnumerable<ActionBase> actionBases = null,
            IEnumerable<PrivateKey> initialValidator = null)
        {
            if (!tableSheets.TryGetValue(nameof(GameConfigSheet), out var csv))
            {
                throw new KeyNotFoundException(nameof(GameConfigSheet));
            }
            var gameConfigState = new GameConfigState(csv);
            var redeemCodeListSheet = new RedeemCodeListSheet();
            redeemCodeListSheet.Set(tableSheets[nameof(RedeemCodeListSheet)]);

            if (privateKey is null)
            {
                privateKey = new PrivateKey();
            }

#pragma warning disable CS0618
            // Use of obsolete method Currency.Legacy(): https://github.com/planetarium/lib9c/discussions/1319
            var ncg = Currency.Legacy("NCG", 2, privateKey.ToAddress());
#pragma warning restore CS0618
            activatedAccounts = activatedAccounts ?? ImmutableHashSet<Address>.Empty;
            var initialStatesAction = new InitializeStates
            (
                rankingState: new RankingState0(),
                shopState: new ShopState(),
                tableSheets: (Dictionary<string, string>) tableSheets,
                gameConfigState: gameConfigState,
                redeemCodeState: new RedeemCodeState(redeemCodeListSheet),
                adminAddressState: adminState,
                activatedAccountsState: new ActivatedAccountsState(
                    isActivateAdminAddress && !(adminState is null)
                    ? activatedAccounts.Add(adminState.AdminAddress)
                    : activatedAccounts),
                goldCurrencyState: new GoldCurrencyState(ncg),
                goldDistributions: goldDistributions,
                pendingActivationStates: pendingActivationStates,
                authorizedMinersState: authorizedMinersState,
                creditsState: credits is null ? null : new CreditsState(credits)
            );
            List<PolymorphicAction<ActionBase>> actions = new List<PolymorphicAction<ActionBase>>
            {
                initialStatesAction,
            };

            if (!(actionBases is null))
            {
                actions.AddRange(actionBases.Select(actionBase =>
                    new PolymorphicAction<ActionBase>(actionBase)));
            }

            var blockAction = new BlockPolicySource(Log.Logger).GetPolicy().BlockAction;
            var nativeTokens = new BlockPolicySource(Log.Logger).GetPolicy().NativeTokens;
            return
                BlockChain<PolymorphicAction<ActionBase>>.ProposeGenesisBlock(
                    actions,
                    privateKey: privateKey,
                    blockAction: blockAction,
                    timestamp: timestamp,
                    nativeTokenPredicate: nativeTokens.Contains,
                    nativeTokens: nativeTokens,
                    manualTransaction: GenesisValidatorTransaction(
                        initialValidator?.ToList()));
        }
    }
}
