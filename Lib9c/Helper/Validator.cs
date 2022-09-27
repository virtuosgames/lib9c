using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Libplanet;
using Nekoyume.Action;
using Nekoyume.Extensions;
using Nekoyume.Model.State;
using Nekoyume.TableData;
using Serilog;

namespace Nekoyume.Helper
{
    public static class Validator
    {
        public static void ValidateForHackAndSlash(
            AvatarState avatarState,
            Dictionary<Type, (Address address, ISheet sheet)> sheets,
            int worldId,
            int stageId,
            List<Guid> equipments,
            List<Guid> costumes,
            List<Guid> foods,
            Stopwatch sw,
            long blockIndex,
            string addressesHex,
            int playCount = 1,
            int stakingLevel = 0)
        {
            var worldSheet = sheets.GetSheet<WorldSheet>();
            if (!worldSheet.TryGetValue(worldId, out var worldRow, false))
            {
                throw new SheetRowNotFoundException(addressesHex, nameof(WorldSheet), worldId);
            }

            if (stageId < worldRow.StageBegin ||
                stageId > worldRow.StageEnd)
            {
                throw new SheetRowColumnException(
                    $"{addressesHex}{worldId} world is not contains {worldRow.Id} stage: " +
                    $"{worldRow.StageBegin}-{worldRow.StageEnd}");
            }

            sw.Restart();
            if (!sheets.GetSheet<StageSheet>().TryGetValue(stageId, out var stageRow))
            {
                throw new SheetRowNotFoundException(addressesHex, nameof(StageSheet), stageId);
            }

            sw.Stop();
            Log.Verbose("{AddressesHex}HAS Get StageSheet: {Elapsed}", addressesHex, sw.Elapsed);

            sw.Restart();
            var worldInformation = avatarState.worldInformation;
            if (!worldInformation.TryGetWorld(worldId, out var world))
            {
                // NOTE: Add new World from WorldSheet
                worldInformation.AddAndUnlockNewWorld(worldRow, blockIndex, worldSheet);
                worldInformation.TryGetWorld(worldId, out world);
            }

            if (!world.IsUnlocked)
            {
                throw new InvalidWorldException($"{addressesHex}{worldId} is locked.");
            }

            if (world.StageBegin != worldRow.StageBegin ||
                world.StageEnd != worldRow.StageEnd)
            {
                worldInformation.UpdateWorld(worldRow);
            }

            if (world.IsStageCleared && stageId > world.StageClearedId + 1 ||
                !world.IsStageCleared && stageId != world.StageBegin)
            {
                throw new InvalidStageException(
                    $"{addressesHex}Aborted as the stage ({worldId}/{stageId}) is not cleared; " +
                    $"cleared stage: {world.StageClearedId}"
                );
            }

            sw.Stop();
            Log.Verbose("{AddressesHex}HAS Validate World: {Elapsed}", addressesHex, sw.Elapsed);

            sw.Restart();
            var equipmentList = avatarState.ValidateEquipmentsV2(equipments, blockIndex);
            var foodIds = avatarState.ValidateConsumable(foods, blockIndex);
            var costumeIds = avatarState.ValidateCostume(costumes);
            sw.Stop();
            Log.Verbose("{AddressesHex}HAS Validate Items: {Elapsed}", addressesHex, sw.Elapsed);

            var costAp = stageRow.CostAP;
            // ?먮옒??ap ?뚮え?됱? 5?댁?留?            // ?ㅽ뀒?댄궧 ?덈꺼???덈뒗 ?곹깭?댁옄 StakeActionPointCoefficientSheet ?쒗듃媛 議댁옱?섎뒗 寃쎌슦 洹몄뿉 ?곕Ⅸ 蹂寃쎈맂 ap ?뚮え?됱쓣 媛?몄샂.
            if (stakingLevel > 0 && sheets.TryGetSheet<StakeActionPointCoefficientSheet>(out var apSheet))
            {
                costAp = apSheet.GetActionPointByStaking(costAp, 1, stakingLevel);
                Log.Error("!!In Validator, success to get staking level and sheet, costAp:{CostAp}", costAp);
            }

            Log.Error("!!In Validator, final cost ap:{CostAp}", costAp);
            if (avatarState.actionPoint < costAp * playCount)
            {
                Log.Error(
                    "!!In Validator, throw NotEnoughActionPointException. avatar ap:{AvatarActionPoint}, final costAp:{CostAp}",
                    avatarState.actionPoint, costAp * playCount);
                throw new NotEnoughActionPointException(
                    $"{addressesHex}Aborted due to insufficient action point: " +
                    $"{avatarState.actionPoint} < cost({costAp * playCount}))"
                );
            }

            Log.Error(
                "!!In Validator, avatar ap:{AvatarActionPoint}, final costAp:{CostAp}",
                avatarState.actionPoint, costAp * playCount);
            avatarState.ValidateItemRequirement(
                costumeIds.Concat(foodIds).ToList(),
                equipmentList,
                sheets.GetSheet<ItemRequirementSheet>(),
                sheets.GetSheet<EquipmentItemRecipeSheet>(),
                sheets.GetSheet<EquipmentItemSubRecipeSheetV2>(),
                sheets.GetSheet<EquipmentItemOptionSheet>(),
                addressesHex);
        }

        #region ObsoletedValidating

        [Obsolete("Use ValidateForHackAndSlash")]
        public static void ValidateForHackAndSlashV1(
            AvatarState avatarState,
            Dictionary<Type, (Address address, ISheet sheet)> sheets,
            int worldId,
            int stageId,
            List<Guid> equipments,
            List<Guid> costumes,
            List<Guid> foods,
            Stopwatch sw,
            long blockIndex,
            string addressesHex,
            int playCount = 1)
        {
            var worldSheet = sheets.GetSheet<WorldSheet>();
            if (!worldSheet.TryGetValue(worldId, out var worldRow, false))
            {
                throw new SheetRowNotFoundException(addressesHex, nameof(WorldSheet), worldId);
            }

            if (stageId < worldRow.StageBegin ||
                stageId > worldRow.StageEnd)
            {
                throw new SheetRowColumnException(
                    $"{addressesHex}{worldId} world is not contains {worldRow.Id} stage: " +
                    $"{worldRow.StageBegin}-{worldRow.StageEnd}");
            }

            sw.Restart();
            if (!sheets.GetSheet<StageSheet>().TryGetValue(stageId, out var stageRow))
            {
                throw new SheetRowNotFoundException(addressesHex, nameof(StageSheet), stageId);
            }

            sw.Stop();
            Log.Verbose("{AddressesHex}HAS Get StageSheet: {Elapsed}", addressesHex, sw.Elapsed);

            sw.Restart();
            var worldInformation = avatarState.worldInformation;
            if (!worldInformation.TryGetWorld(worldId, out var world))
            {
                // NOTE: Add new World from WorldSheet
                worldInformation.AddAndUnlockNewWorld(worldRow, blockIndex, worldSheet);
            }

            if (!world.IsUnlocked)
            {
                throw new InvalidWorldException($"{addressesHex}{worldId} is locked.");
            }

            if (world.StageBegin != worldRow.StageBegin ||
                world.StageEnd != worldRow.StageEnd)
            {
                worldInformation.UpdateWorld(worldRow);
            }

            if (world.IsStageCleared && stageId > world.StageClearedId + 1 ||
                !world.IsStageCleared && stageId != world.StageBegin)
            {
                throw new InvalidStageException(
                    $"{addressesHex}Aborted as the stage ({worldId}/{stageId}) is not cleared; " +
                    $"cleared stage: {world.StageClearedId}"
                );
            }

            sw.Stop();
            Log.Verbose("{AddressesHex}HAS Validate World: {Elapsed}", addressesHex, sw.Elapsed);

            sw.Restart();
            var equipmentList = avatarState.ValidateEquipmentsV2(equipments, blockIndex);
            var foodIds = avatarState.ValidateConsumable(foods, blockIndex);
            var costumeIds = avatarState.ValidateCostume(costumes);
            sw.Stop();
            Log.Verbose("{AddressesHex}HAS Validate Items: {Elapsed}", addressesHex, sw.Elapsed);

            if (avatarState.actionPoint < stageRow.CostAP * playCount)
            {
                throw new NotEnoughActionPointException(
                    $"{addressesHex}Aborted due to insufficient action point: " +
                    $"{avatarState.actionPoint} < cost({stageRow.CostAP * playCount}))"
                );
            }

            avatarState.ValidateItemRequirement(
                costumeIds.Concat(foodIds).ToList(),
                equipmentList,
                sheets.GetSheet<ItemRequirementSheet>(),
                sheets.GetSheet<EquipmentItemRecipeSheet>(),
                sheets.GetSheet<EquipmentItemSubRecipeSheetV2>(),
                sheets.GetSheet<EquipmentItemOptionSheet>(),
                addressesHex);
        }

        #endregion
    }
}
