using System;
using System.Collections.Generic;
using Bencodex.Types;
using Libplanet;

namespace Nekoyume.Action
{
    public interface IEventDungeonBattleV2
    {
        Address AvatarAddress { get; }
        int EventScheduleId { get; }
        int EventDungeonId { get; }
        int EventDungeonStageId { get; }
        IEnumerable<Guid> Equipments { get; }
        IEnumerable<Guid> Costumes { get; }
        IEnumerable<Guid> Foods { get; }
        IEnumerable<IValue> RuneSlotInfos { get; }
        bool BuyTicketIfNeeded { get; }
    }
}
