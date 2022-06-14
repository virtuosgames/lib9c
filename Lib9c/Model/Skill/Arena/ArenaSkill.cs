using System;
using System.Collections.Generic;
using Bencodex.Types;
using Nekoyume.Model.Elemental;
using Nekoyume.Model.State;
using Nekoyume.TableData;

namespace Nekoyume.Model.Skill.Arena
{
    [Serializable]
    public abstract class ArenaSkill : IState
    {
        public readonly SkillSheet.Row SkillRow;
        public int Power { get; private set; }
        public int Chance { get; private set; }

        protected ArenaSkill(SkillSheet.Row skillRow, int power, int chance)
        {
            SkillRow = skillRow;
            Power = power;
            Chance = chance;
        }

        public abstract BattleStatus.Arena.ArenaSkill Use(
            ArenaCharacter caster,
            ArenaCharacter target,
            int turn,
            IEnumerable<Buff.Buff> buffs
        );

        protected bool Equals(Skill other)
        {
            return SkillRow.Equals(other.SkillRow) && Power == other.Power && Chance.Equals(other.Chance);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((Skill) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = SkillRow.GetHashCode();
                hashCode = (hashCode * 397) ^ Power;
                hashCode = (hashCode * 397) ^ Chance.GetHashCode();
                return hashCode;
            }
        }

        protected IEnumerable<BattleStatus.Arena.ArenaSkill.ArenaSkillInfo> ProcessBuff(
            ArenaCharacter caster,
            ArenaCharacter target,
            int turn,
            IEnumerable<Buff.Buff> buffs
        )
        {
            var infos = new List<BattleStatus.Arena.ArenaSkill.ArenaSkillInfo>();
            foreach (var buff in buffs)
            {
                switch (buff.RowData.TargetType)
                {
                    case SkillTargetType.Enemy:
                    case SkillTargetType.Enemies:
                        target.AddBuff(buff);
                        infos.Add(GetSkillInfo(target, turn, buff));
                        break;

                    case SkillTargetType.Self:
                    case SkillTargetType.Ally:
                        caster.AddBuff(buff);
                        infos.Add(GetSkillInfo(caster, turn, buff));
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            return infos;
        }

        private BattleStatus.Arena.ArenaSkill.ArenaSkillInfo GetSkillInfo(ICloneable target, int turn, Buff.Buff buff)
        {
            return new BattleStatus.Arena.ArenaSkill.ArenaSkillInfo(
                (ArenaCharacter) target.Clone(),
                0,
                false,
                SkillRow.SkillCategory,
                turn,
                ElementalType.Normal,
                SkillRow.SkillTargetType,
                buff);
        }


        public void Update(int chance, int power)
        {
            Chance = chance;
            Power = power;
        }

        public IValue Serialize() =>
            new Bencodex.Types.Dictionary(new Dictionary<IKey, IValue>
            {
                [(Text) "skillRow"] = SkillRow.Serialize(),
                [(Text) "power"] = Power.Serialize(),
                [(Text) "chance"] = Chance.Serialize()
            });
    }
}
