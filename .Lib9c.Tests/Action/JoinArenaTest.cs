namespace Lib9c.Tests.Action
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Bencodex.Types;
    using Libplanet;
    using Libplanet.Action;
    using Libplanet.Assets;
    using Libplanet.Crypto;
    using Nekoyume;
    using Nekoyume.Action;
    using Nekoyume.Model.Arena;
    using Nekoyume.Model.EnumType;
    using Nekoyume.Model.Item;
    using Nekoyume.Model.State;
    using Nekoyume.TableData;
    using Serilog;
    using Xunit;
    using Xunit.Abstractions;
    using static SerializeKeys;

    public class JoinArenaTest
    {
        private readonly Dictionary<string, string> _sheets;
        private readonly TableSheets _tableSheets;
        private readonly Address _signer;
        private readonly Address _avatarAddress;
        private readonly IRandom _random;
        private readonly Currency _currency;
        private IAccountStateDelta _state;

        public JoinArenaTest(ITestOutputHelper outputHelper)
        {
            _random = new TestRandom();
            _sheets = TableSheetsImporter.ImportSheets();
            _tableSheets = new TableSheets(_sheets);

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Verbose()
                .WriteTo.TestOutput(outputHelper)
                .CreateLogger();

            _signer = default;
            _avatarAddress = _signer.Derive("avatar");
            _state = new State();
            var sheets = TableSheetsImporter.ImportSheets();
            var tableSheets = new TableSheets(sheets);
            var rankingMapAddress = new PrivateKey().ToAddress();
            var agentState = new AgentState(_signer);
            var avatarState = new AvatarState(
                _avatarAddress,
                _signer,
                0,
                tableSheets.GetAvatarSheets(),
                new GameConfigState(),
                rankingMapAddress);
            agentState.avatarAddresses[0] = _avatarAddress;

            var currency = new Currency("NCG", 2, minters: null);
            var goldCurrencyState = new GoldCurrencyState(currency);
            _currency = new Currency("CRYSTAL", 18, minters: null);

            _state = _state
                .SetState(_signer, agentState.Serialize())
                .SetState(_avatarAddress.Derive(LegacyInventoryKey), avatarState.inventory.Serialize())
                .SetState(_avatarAddress.Derive(LegacyWorldInformationKey), avatarState.worldInformation.Serialize())
                .SetState(_avatarAddress.Derive(LegacyQuestListKey), avatarState.questList.Serialize())
                .SetState(_avatarAddress, avatarState.SerializeV2())
                .SetState(Addresses.GoldCurrency, goldCurrencyState.Serialize());

            foreach ((string key, string value) in sheets)
            {
                _state = _state
                    .SetState(Addresses.TableSheet.Derive(key), value.Serialize());
            }
        }

        public (List<Guid> Equipments, List<Guid> Costumes) GetDummyItems(AvatarState avatarState)
        {
            var items = Doomfist.GetAllParts(_tableSheets, avatarState.level);
            foreach (var equipment in items)
            {
                avatarState.inventory.AddItem(equipment);
            }

            var equipments = items.Select(e => e.NonFungibleId).ToList();

            var random = new TestRandom();
            var costumes = new List<Guid>();
            if (avatarState.level >= GameConfig.RequireCharacterLevel.CharacterFullCostumeSlot)
            {
                var costumeId = _tableSheets
                    .CostumeItemSheet
                    .Values
                    .First(r => r.ItemSubType == ItemSubType.FullCostume)
                    .Id;

                var costume = (Costume)ItemFactory.CreateItem(
                    _tableSheets.ItemSheet[costumeId], random);
                avatarState.inventory.AddItem(costume);
                costumes.Add(costume.ItemId);
            }

            return (equipments, costumes);
        }

        public AvatarState GetAvatarState(AvatarState avatarState, out List<Guid> equipments, out List<Guid> costumes)
        {
            avatarState.level = 999;
            (equipments, costumes) = GetDummyItems(avatarState);

            _state = _state
                .SetState(_avatarAddress, avatarState.SerializeV2())
                .SetState(_avatarAddress.Derive(LegacyInventoryKey), avatarState.inventory.Serialize())
                .SetState(_avatarAddress.Derive(LegacyWorldInformationKey), avatarState.worldInformation.Serialize())
                .SetState(_avatarAddress.Derive(LegacyQuestListKey), avatarState.questList.Serialize());

            return avatarState;
        }

        public AvatarState AddMedal(AvatarState avatarState, ArenaSheet.Row row)
        {
            var materialSheet = _state.GetSheet<MaterialItemSheet>();
            foreach (var data in row.Round)
            {
                if (!data.ArenaType.Equals(ArenaType.Season))
                {
                    continue;
                }

                var itemId = JoinArena.GetMedalItemId(data.Id, data.Round);
                var material = ItemFactory.CreateMaterial(materialSheet, itemId);
                avatarState.inventory.AddItem(material);
            }

            _state = _state
                .SetState(_avatarAddress, avatarState.SerializeV2())
                .SetState(_avatarAddress.Derive(LegacyInventoryKey), avatarState.inventory.Serialize())
                .SetState(_avatarAddress.Derive(LegacyWorldInformationKey), avatarState.worldInformation.Serialize())
                .SetState(_avatarAddress.Derive(LegacyQuestListKey), avatarState.questList.Serialize());

            return avatarState;
        }

        [Theory]
        [InlineData(0, 1, 1)]
        [InlineData(0, 1, 2)]
        [InlineData(7, 1, 2)]
        [InlineData(100, 1, 8)]
        public void Execute(long blockIndex, int championshipId, int round)
        {
            var arenaSheet = _state.GetSheet<ArenaSheet>();
            if (!arenaSheet.TryGetValue(championshipId, out var row))
            {
                throw new SheetRowNotFoundException(
                    nameof(ArenaSheet), $"championship Id : {championshipId}");
            }

            var avatarState = _state.GetAvatarStateV2(_avatarAddress);
            avatarState = GetAvatarState(avatarState, out var equipments, out var costumes);
            avatarState = AddMedal(avatarState, row);
            var preCurrency = 1000 * _currency;
            var state = _state.MintAsset(_signer, preCurrency);

            var action = new JoinArena()
            {
                championshipId = championshipId,
                round = round,
                costumes = costumes,
                equipments = equipments,
                avatarAddress = _avatarAddress,
            };

            state = action.Execute(new ActionContext
            {
                PreviousStates = state,
                Signer = _signer,
                Random = _random,
                Rehearsal = false,
                BlockIndex = blockIndex,
            });

            // ArenaParticipants
            var arenaParticipantsAdr = ArenaParticipants.DeriveAddress(championshipId, round);
            var serializedArenaParticipants = (List)state.GetState(arenaParticipantsAdr);
            var arenaParticipants = new ArenaParticipants(serializedArenaParticipants);

            Assert.Equal(arenaParticipantsAdr, arenaParticipants.Address);
            Assert.Equal(_avatarAddress, arenaParticipants.AvatarAddresses.First());

            // ArenaAvatarState
            var arenaAvatarStateAdr = ArenaAvatarState.DeriveAddress(_avatarAddress);
            var serializedArenaAvatarState = (List)state.GetState(arenaAvatarStateAdr);
            var arenaAvatarState = new ArenaAvatarState(serializedArenaAvatarState);

            foreach (var guid in arenaAvatarState.Equipments)
            {
                Assert.Contains(avatarState.inventory.Equipments, x => x.ItemId.Equals(guid));
            }

            foreach (var guid in arenaAvatarState.Costumes)
            {
                Assert.Contains(avatarState.inventory.Costumes, x => x.ItemId.Equals(guid));
            }

            Assert.Equal(arenaAvatarStateAdr, arenaAvatarState.Address);
            Assert.Equal(avatarState.level, arenaAvatarState.Level);

            // ArenaScore
            var arenaScoreAdr = ArenaScore.DeriveAddress(_avatarAddress, championshipId, round);
            var serializedArenaScore = (List)state.GetState(arenaScoreAdr);
            var arenaScore = new ArenaScore(serializedArenaScore);

            Assert.Equal(arenaScoreAdr, arenaScore.Address);
            Assert.Equal(GameConfig.ArenaScoreDefault, arenaScore.Score);

            // ArenaInformation
            var arenaInformationAdr = ArenaInformation.DeriveAddress(_avatarAddress, championshipId, round);
            var serializedArenaInformation = (List)state.GetState(arenaInformationAdr);
            var arenaInformation = new ArenaInformation(serializedArenaInformation);

            Assert.Equal(arenaInformationAdr, arenaInformation.Address);
            Assert.Equal(0, arenaInformation.Win);
            Assert.Equal(0, arenaInformation.Lose);
            Assert.Equal(GameConfig.ArenaChallengeCountMax, arenaInformation.Ticket);

            if (!row.TryGetRound(championshipId, round, out var roundData))
            {
                throw new RoundNotFoundByIdsException($"{nameof(JoinArena)} : {championshipId} / {round}");
            }

            if (row.IsTheRoundOpened(blockIndex, championshipId, round))
            {
                var curCurrency = preCurrency - (roundData.EntranceFee * _currency);
                Assert.Equal(curCurrency, state.GetBalance(_signer, _currency));
            }
            else
            {
                var curCurrency = preCurrency - (roundData.DiscountedEntranceFee * _currency);
                Assert.Equal(curCurrency, state.GetBalance(_signer, _currency));
            }
        }

        [Theory]
        [InlineData(9999)]
        public void Execute_SheetRowNotFoundException(int championshipId)
        {
            var avatarState = _state.GetAvatarStateV2(_avatarAddress);
            avatarState = GetAvatarState(avatarState, out var equipments, out var costumes);
            var state = _state.SetState(_avatarAddress, avatarState.SerializeV2());

            var action = new JoinArena()
            {
                championshipId = championshipId,
                round = 1,
                costumes = costumes,
                equipments = equipments,
                avatarAddress = _avatarAddress,
            };

            Assert.Throws<SheetRowNotFoundException>(() => action.Execute(new ActionContext()
            {
                PreviousStates = state,
                Signer = _signer,
                Random = new TestRandom(),
            }));
        }

        [Theory]
        [InlineData(9999999999)]
        public void Execute_RoundNotFoundByBlockIndexException(long blockIndex)
        {
            var avatarState = _state.GetAvatarStateV2(_avatarAddress);
            avatarState = GetAvatarState(avatarState, out var equipments, out var costumes);
            var state = _state.SetState(_avatarAddress, avatarState.SerializeV2());

            var action = new JoinArena()
            {
                championshipId = 1,
                round = 1,
                costumes = costumes,
                equipments = equipments,
                avatarAddress = _avatarAddress,
            };

            Assert.Throws<RoundNotFoundByBlockIndexException>(() => action.Execute(new ActionContext()
            {
                PreviousStates = state,
                Signer = _signer,
                Random = new TestRandom(),
                BlockIndex = blockIndex,
            }));
        }

        [Theory]
        [InlineData(123)]
        public void Execute_RoundNotFoundByIdsException(int round)
        {
            var avatarState = _state.GetAvatarStateV2(_avatarAddress);
            avatarState = GetAvatarState(avatarState, out var equipments, out var costumes);
            var state = _state.SetState(_avatarAddress, avatarState.SerializeV2());

            var action = new JoinArena()
            {
                championshipId = 1,
                round = round,
                costumes = costumes,
                equipments = equipments,
                avatarAddress = _avatarAddress,
            };

            Assert.Throws<RoundNotFoundByIdsException>(() => action.Execute(new ActionContext()
            {
                PreviousStates = state,
                Signer = _signer,
                Random = new TestRandom(),
                BlockIndex = 1,
            }));
        }

        [Theory]
        [InlineData(8)]
        public void Execute_NotEnoughMedalException(int round)
        {
            var avatarState = _state.GetAvatarStateV2(_avatarAddress);
            GetAvatarState(avatarState, out var equipments, out var costumes);
            var preCurrency = 1000 * _currency;
            var state = _state.MintAsset(_signer, preCurrency);

            var action = new JoinArena()
            {
                championshipId = 1,
                round = round,
                costumes = costumes,
                equipments = equipments,
                avatarAddress = _avatarAddress,
            };

            Assert.Throws<NotEnoughMedalException>(() => action.Execute(new ActionContext()
            {
                PreviousStates = state,
                Signer = _signer,
                Random = new TestRandom(),
                BlockIndex = 100,
            }));
        }

        [Theory]
        [InlineData(6, 0)] // discounted_entrance_fee
        [InlineData(8, 100)] // entrance_fee
        public void Execute_NotEnoughFungibleAssetValueException(int round, long blockIndex)
        {
            var avatarState = _state.GetAvatarStateV2(_avatarAddress);
            GetAvatarState(avatarState, out var equipments, out var costumes);
            var state = _state.SetState(_avatarAddress, avatarState.SerializeV2());

            var action = new JoinArena()
            {
                championshipId = 1,
                round = round,
                costumes = costumes,
                equipments = equipments,
                avatarAddress = _avatarAddress,
            };

            Assert.Throws<NotEnoughFungibleAssetValueException>(() => action.Execute(new ActionContext()
            {
                PreviousStates = state,
                Signer = _signer,
                Random = new TestRandom(),
                BlockIndex = blockIndex,
            }));
        }

        [Fact]
        public void Execute_ArenaScoreAlreadyContainsException()
        {
            var avatarState = _state.GetAvatarStateV2(_avatarAddress);
            avatarState = GetAvatarState(avatarState, out var equipments, out var costumes);
            var state = _state.SetState(_avatarAddress, avatarState.SerializeV2());

            var action = new JoinArena()
            {
                championshipId = 1,
                round = 1,
                costumes = costumes,
                equipments = equipments,
                avatarAddress = _avatarAddress,
            };

            state = action.Execute(new ActionContext
            {
                PreviousStates = state,
                Signer = _signer,
                Random = _random,
                Rehearsal = false,
                BlockIndex = 1,
            });

            Assert.Throws<ArenaScoreAlreadyContainsException>(() => action.Execute(new ActionContext()
            {
                PreviousStates = state,
                Signer = _signer,
                Random = new TestRandom(),
                BlockIndex = 2,
            }));
        }

        [Fact]
        public void Execute_ArenaScoreAlreadyContainsException2()
        {
            const int championshipId = 1;
            const int round = 1;

            var avatarState = _state.GetAvatarStateV2(_avatarAddress);
            avatarState = GetAvatarState(avatarState, out var equipments, out var costumes);
            var state = _state.SetState(_avatarAddress, avatarState.SerializeV2());

            var arenaScoreAdr = ArenaScore.DeriveAddress(_avatarAddress, championshipId, round);
            var arenaScore = new ArenaScore(_avatarAddress, championshipId, round);
            state = state.SetState(arenaScoreAdr, arenaScore.Serialize());

            var action = new JoinArena()
            {
                championshipId = championshipId,
                round = round,
                costumes = costumes,
                equipments = equipments,
                avatarAddress = _avatarAddress,
            };

            Assert.Throws<ArenaScoreAlreadyContainsException>(() => action.Execute(new ActionContext()
            {
                PreviousStates = state,
                Signer = _signer,
                Random = new TestRandom(),
                BlockIndex = 1,
            }));
        }

        [Fact]
        public void Execute_ArenaInformationAlreadyContainsException()
        {
            const int championshipId = 1;
            const int round = 1;

            var avatarState = _state.GetAvatarStateV2(_avatarAddress);
            avatarState = GetAvatarState(avatarState, out var equipments, out var costumes);
            var state = _state.SetState(_avatarAddress, avatarState.SerializeV2());

            var arenaInformationAdr = ArenaInformation.DeriveAddress(_avatarAddress, championshipId, round);
            var arenaInformation = new ArenaInformation(_avatarAddress, championshipId, round);
            state = state.SetState(arenaInformationAdr, arenaInformation.Serialize());

            var action = new JoinArena()
            {
                championshipId = championshipId,
                round = round,
                costumes = costumes,
                equipments = equipments,
                avatarAddress = _avatarAddress,
            };

            Assert.Throws<ArenaInformationAlreadyContainsException>(() => action.Execute(new ActionContext()
            {
                PreviousStates = state,
                Signer = _signer,
                Random = new TestRandom(),
                BlockIndex = 1,
            }));
        }
    }
}
