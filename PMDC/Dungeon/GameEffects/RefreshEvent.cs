using System;
using RogueEssence.Data;
using RogueEssence;
using RogueEssence.Dungeon;
using RogueEssence.Dev;
using System.Collections.Generic;

namespace PMDC.Dungeon
{
    [Serializable]
    public class RefreshPreEvent : RefreshEvent
    {
        public override GameEvent Clone() { return new RefreshPreEvent(); }
        public override void Apply(GameEventOwner owner, Character ownerChar, Character character)
        {
            if (character.HasElement(18) || character.HasElement(08) || character.HasElement(03))
                character.Mobility |= (1U << (int)TerrainData.Mobility.Water);

            if (character.HasElement(07) || character.HasElement(08) || character.HasElement(03))
                character.Mobility |= (1U << (int)TerrainData.Mobility.Lava);

            if (character.HasElement(08))
                character.Mobility |= (1U << (int)TerrainData.Mobility.Abyss);

            if (character.HasElement(09))
                character.Mobility |= (1U << (int)TerrainData.Mobility.Block);

        }
    }


    [Serializable]
    public class FamilyRefreshEvent : RefreshEvent
    {
        public RefreshEvent BaseEvent;

        public FamilyRefreshEvent() { }
        public FamilyRefreshEvent(RefreshEvent baseEvent) { BaseEvent = baseEvent; }
        protected FamilyRefreshEvent(FamilyRefreshEvent other)
        {
            BaseEvent = (RefreshEvent)other.BaseEvent.Clone();
        }
        public override GameEvent Clone() { return new FamilyRefreshEvent(this); }

        public override void Apply(GameEventOwner owner, Character ownerChar, Character character)
        {
            ItemData entry = DataManager.Instance.GetItem(owner.GetID());
            FamilyState family;
            if (!entry.ItemStates.TryGet<FamilyState>(out family))
                return;

            if (family.Members.Contains(ownerChar.BaseForm.Species))
                BaseEvent.Apply(owner, ownerChar, character);
        }
    }

    [Serializable]
    public class AddMobilityEvent : RefreshEvent
    {
        public TerrainData.Mobility Mobility;

        public AddMobilityEvent() { }
        public AddMobilityEvent(TerrainData.Mobility mobility)
        {
            Mobility = mobility;
        }
        protected AddMobilityEvent(AddMobilityEvent other)
        {
            Mobility = other.Mobility;
        }
        public override GameEvent Clone() { return new AddMobilityEvent(this); }

        public override void Apply(GameEventOwner owner, Character ownerChar, Character character)
        {
            character.Mobility |= (1U << (int)Mobility);
        }
    }

    [Serializable]
    public class FactionRefreshEvent : RefreshEvent
    {
        public Faction Faction;

        public RefreshEvent BaseEvent;

        public FactionRefreshEvent() { }
        public FactionRefreshEvent(Faction faction, RefreshEvent baseEvent)
        {
            Faction = faction;
            BaseEvent = baseEvent;
        }
        protected FactionRefreshEvent(FactionRefreshEvent other)
        {
            Faction = other.Faction;
            BaseEvent = (RefreshEvent)other.BaseEvent.Clone();
        }
        public override GameEvent Clone() { return new FactionRefreshEvent(this); }
        public override void Apply(GameEventOwner owner, Character ownerChar, Character character)
        {
            CharIndex charIndex = ZoneManager.Instance.CurrentMap.GetCharIndex(character);
            if (charIndex.Faction == Faction)
                BaseEvent.Apply(owner, ownerChar, character);
        }
    }

    [Serializable]
    public class SetSightEvent : RefreshEvent
    {
        public bool CharSight;
        public Map.SightRange Sight;

        public SetSightEvent() { }
        public SetSightEvent(bool charSight, Map.SightRange sight)
        {
            CharSight = charSight;
            Sight = sight;
        }
        protected SetSightEvent(SetSightEvent other)
        {
            CharSight = other.CharSight;
            Sight = other.Sight;
        }
        public override GameEvent Clone() { return new SetSightEvent(this); }
        public override void Apply(GameEventOwner owner, Character ownerChar, Character character)
        {
            if (CharSight)
                character.CharSight = Sight;
            else
                character.TileSight = Sight;
        }
    }
    [Serializable]
    public class SeeCharsEvent : RefreshEvent
    {
        public override GameEvent Clone() { return new SeeCharsEvent(); }
        public override void Apply(GameEventOwner owner, Character ownerChar, Character character)
        {
            character.SeeAllChars = true;
        }
    }
    [Serializable]
    public class SeeItemsEvent : RefreshEvent
    {
        public bool WallItems;

        public SeeItemsEvent()
        { }
        public SeeItemsEvent(bool wallItems)
        {
            WallItems = wallItems;
        }
        protected SeeItemsEvent(SeeItemsEvent other)
        {
            WallItems = other.WallItems;
        }
        public override GameEvent Clone() { return new SeeItemsEvent(this); }
        public override void Apply(GameEventOwner owner, Character ownerChar, Character character)
        {
            if (WallItems)
                character.SeeWallItems = true;
            else
                character.SeeItems = true;
        }
    }
    [Serializable]
    public class BlindEvent : RefreshEvent
    {
        public override GameEvent Clone() { return new BlindEvent(); }
        public override void Apply(GameEventOwner owner, Character ownerChar, Character character)
        {
            character.CharSight = Map.SightRange.Blind;
            character.TileSight = Map.SightRange.Blind;
        }
    }
    [Serializable]
    public class VanishEvent : RefreshEvent
    {
        public override GameEvent Clone() { return new VanishEvent(); }
        public override void Apply(GameEventOwner owner, Character ownerChar, Character character)
        {
            character.Unidentifiable = true;
            character.Unlocatable = true;
        }
    }
    [Serializable]
    public class IllusionEvent : RefreshEvent
    {
        public override GameEvent Clone() { return new IllusionEvent(); }
        public override void Apply(GameEventOwner owner, Character ownerChar, Character character)
        {
            MonsterID proxy = ((StatusEffect)owner).StatusStates.GetWithDefault<MonsterIDState>().MonID;
            character.ProxySprite = proxy;
            character.ProxyName = Character.GetFullFormName(character.Appearance);
        }
    }
    [Serializable]
    public class AppearanceEvent : RefreshEvent
    {
        MonsterID Appearance;
        
        public AppearanceEvent() { }
        public AppearanceEvent(MonsterID appearance) { Appearance = appearance; }
        protected AppearanceEvent(AppearanceEvent other)
        {
            Appearance = other.Appearance;
        }
        public override GameEvent Clone() { return new AppearanceEvent(this); }

        public override void Apply(GameEventOwner owner, Character ownerChar, Character character)
        {
            character.ProxySprite = Appearance;
        }
    }
    [Serializable]
    public class AddTypeSpeedEvent : RefreshEvent
    {
        public CharState NoDupeEffect;
        public int Element;
        public int Speed;
        public AddTypeSpeedEvent() { }
        public AddTypeSpeedEvent(int element, int speed, CharState effect)
        {
            Element = element;
            Speed = speed;
            NoDupeEffect = effect;
        }
        protected AddTypeSpeedEvent(AddTypeSpeedEvent other)
        {
            Element = other.Element;
            Speed = other.Speed;
            NoDupeEffect = other.NoDupeEffect;
        }
        public override GameEvent Clone() { return new AddTypeSpeedEvent(this); }

        public override void Apply(GameEventOwner owner, Character ownerChar, Character character)
        {
            //good and bad status conditions?
            if (character.HasElement(Element) && character.CharStates.Contains(NoDupeEffect.GetType()))
            {
                bool hasStatus = false;
                foreach (int statusID in character.StatusEffects.Keys)
                {
                    if (character.StatusEffects[statusID].StatusStates.Contains<BadStatusState>())
                    {
                        hasStatus = true;
                        break;
                    }
                }
                if (!hasStatus)
                {
                    character.MovementSpeed += Speed;
                    character.CharStates.Set(NoDupeEffect.Clone<CharState>());
                }
            }
        }
    }
    [Serializable]
    public class AddSpeedEvent : RefreshEvent
    {
        public int Speed;
        public AddSpeedEvent() { }
        public AddSpeedEvent(int speed) { Speed = speed; }
        protected AddSpeedEvent(AddSpeedEvent other)
        {
            Speed = other.Speed;
        }
        public override GameEvent Clone() { return new AddSpeedEvent(this); }

        public override void Apply(GameEventOwner owner, Character ownerChar, Character character)
        {
            character.MovementSpeed += Speed;
        }
    }
    [Serializable]
    public class SpeedLimitEvent : RefreshEvent
    {
        public override GameEvent Clone() { return new SpeedLimitEvent(); }
        public override void Apply(GameEventOwner owner, Character ownerChar, Character character)
        {
            character.MovementSpeed = Math.Min(0, character.MovementSpeed);
        }
    }
    [Serializable]
    public class SpeedReverseEvent : RefreshEvent
    {
        public override GameEvent Clone() { return new SpeedReverseEvent(); }
        public override void Apply(GameEventOwner owner, Character ownerChar, Character character)
        {
            character.MovementSpeed = character.MovementSpeed * -1;
        }
    }
    [Serializable]
    public class UnburdenEvent : RefreshEvent
    {
        public UnburdenEvent() { }
        public override GameEvent Clone() { return new UnburdenEvent(); }

        public override void Apply(GameEventOwner owner, Character ownerChar, Character character)
        {
            if (character.EquippedItem.ID == -1)
                character.MovementSpeed += 1;
        }
    }
    [Serializable]
    public class WeatherSpeedEvent : RefreshEvent
    {
        public int WeatherID;
        public WeatherSpeedEvent() { }
        public WeatherSpeedEvent(int id) { WeatherID = id; }
        protected WeatherSpeedEvent(WeatherSpeedEvent other)
        {
            WeatherID = other.WeatherID;
        }
        public override GameEvent Clone() { return new WeatherSpeedEvent(this); }

        public override void Apply(GameEventOwner owner, Character ownerChar, Character character)
        {
            if (ZoneManager.Instance.CurrentMap != null && ZoneManager.Instance.CurrentMap.Status.ContainsKey(WeatherID))
                character.MovementSpeed += 1;
        }
    }
    [Serializable]
    public class StatusSpeedEvent : RefreshEvent
    {
        public StatusSpeedEvent() { }
        public override GameEvent Clone() { return new StatusSpeedEvent(); }

        public override void Apply(GameEventOwner owner, Character ownerChar, Character character)
        {
            foreach (StatusEffect status in character.IterateStatusEffects())
            {
                if (status.StatusStates.Contains<MajorStatusState>())
                {
                    character.MovementSpeed += 1;
                    break;
                }
            }
        }
    }
    [Serializable]
    public class ImmobilizationEvent : RefreshEvent
    {
        [StringTypeConstraint(1, typeof(CharState))]
        public List<FlagType> States;

        public ImmobilizationEvent() { States = new List<FlagType>(); }
        public ImmobilizationEvent(Type state) : this() { States.Add(new FlagType(state)); }
        protected ImmobilizationEvent(ImmobilizationEvent other) : this()
        {
            States.AddRange(other.States);
        }
        public override GameEvent Clone() { return new ImmobilizationEvent(this); }

        public override void Apply(GameEventOwner owner, Character ownerChar, Character character)
        {
            bool hasState = false;
            foreach (FlagType state in States)
            {
                if (character.CharStates.Contains(state.FullType))
                    hasState = true;
            }
            if (!hasState)
                character.CantWalk = true;
        }
    }
    [Serializable]
    public class AttackOnlyEvent : RefreshEvent
    {
        public override GameEvent Clone() { return new AttackOnlyEvent(); }
        public override void Apply(GameEventOwner owner, Character ownerChar, Character character)
        {
            character.AttackOnly = true;
        }
    }

    [Serializable]
    public class ParaPauseEvent : RefreshEvent
    {
        public override GameEvent Clone() { return new ParaPauseEvent(); }
        public override void Apply(GameEventOwner owner, Character ownerChar, Character character)
        {
            ParalyzeState para = ((StatusEffect)owner).StatusStates.GetWithDefault<ParalyzeState>();
            if (para.Recent)
            {
                character.CantWalk = true;
                character.AttackOnly = true;
            }
        }
    }
    [Serializable]
    public class NoStickItemEvent : RefreshEvent
    {
        public override GameEvent Clone() { return new NoStickItemEvent(); }
        public override void Apply(GameEventOwner owner, Character ownerChar, Character character)
        {
            character.CanRemoveStuck = true;
        }
    }
    [Serializable]
    public class NoHeldItemEvent : RefreshEvent
    {
        public override GameEvent Clone() { return new NoHeldItemEvent(); }
        public override void Apply(GameEventOwner owner, Character ownerChar, Character character)
        {
            character.ItemDisabled = true;
        }
    }
    [Serializable]
    public class NoAbilityEvent : RefreshEvent
    {
        public override GameEvent Clone() { return new NoAbilityEvent(); }
        public override void Apply(GameEventOwner owner, Character ownerChar, Character character)
        {
            character.IntrinsicDisabled = true;
        }
    }

    [Serializable]
    public class SpeedStackEvent : RefreshEvent
    {
        public SpeedStackEvent() { }
        public override GameEvent Clone() { return new SpeedStackEvent(); }

        public override void Apply(GameEventOwner owner, Character ownerChar, Character character)
        {
            int boost = ((StatusEffect)owner).StatusStates.GetWithDefault<StackState>().Stack;
            character.MovementSpeed += boost;
        }
    }
    [Serializable]
    public class DisableEvent : RefreshEvent
    {
        public DisableEvent() { }
        public override GameEvent Clone() { return new DisableEvent(); }

        public override void Apply(GameEventOwner owner, Character ownerChar, Character character)
        {
            character.Skills[((StatusEffect)owner).StatusStates.GetWithDefault<SlotState>().Slot].Element.Sealed = true;
        }
    }
    [Serializable]
    public class MoveLockEvent : RefreshEvent
    {
        public bool LockOthers;
        [DataType(0, DataManager.DataType.Status, false)]
        public int LastSlotStatusID;

        public MoveLockEvent() { }
        public MoveLockEvent(int statusID, bool lockOthers)
        {
            LastSlotStatusID = statusID;
            LockOthers = lockOthers;
        }
        protected MoveLockEvent(MoveLockEvent other)
        {
            LockOthers = other.LockOthers;
            LastSlotStatusID = other.LastSlotStatusID;
        }
        public override GameEvent Clone() { return new MoveLockEvent(this); }

        public override void Apply(GameEventOwner owner, Character ownerChar, Character character)
        {
            StatusEffect status = character.GetStatusEffect(LastSlotStatusID);
            if (status != null)
            {
                int slot = status.StatusStates.GetWithDefault<SlotState>().Slot;
                for (int ii = 0; ii < character.Skills.Count; ii++)
                {
                    if (character.Skills[ii].Element.SkillNum > -1 && ((ii == slot) != LockOthers))
                        character.Skills[ii].Element.Sealed = true;
                }
            }
        }
    }
    [Serializable]
    public class TauntEvent : RefreshEvent
    {
        public override GameEvent Clone() { return new TauntEvent(); }
        public override void Apply(GameEventOwner owner, Character ownerChar, Character character)
        {
            for (int ii = 0; ii < character.Skills.Count; ii++)
            {
                if (character.Skills[ii].Element.SkillNum > -1 && DataManager.Instance.GetSkill(character.Skills[ii].Element.SkillNum).Data.Category == BattleData.SkillCategory.Status)
                    character.Skills[ii].Element.Sealed = true;
            }
        }
    }

    [Serializable]
    public class AddChargeEvent : RefreshEvent
    {
        public int AddCharge;

        public AddChargeEvent() { }
        public AddChargeEvent(int addCharge)
        {
            AddCharge = addCharge;
        }
        protected AddChargeEvent(AddChargeEvent other)
        {
            AddCharge = other.AddCharge;
        }
        public override GameEvent Clone() { return new AddChargeEvent(this); }

        public override void Apply(GameEventOwner owner, Character ownerChar, Character character)
        {
            character.ChargeBoost += AddCharge;
        }
    }

    [Serializable]
    public class FreeMoveEvent : RefreshEvent
    {
        public override GameEvent Clone() { return new FreeMoveEvent(); }
        public override void Apply(GameEventOwner owner, Character ownerChar, Character character)
        {
            for (int ii = 0; ii < character.Skills.Count; ii++)
            {
                if (character.Skills[ii].Element.SkillNum > -1)
                    character.Skills[ii].Element.Sealed = false;
            }
        }
    }
    [Serializable]
    public class MoveBanEvent : RefreshEvent
    {
        public override GameEvent Clone() { return new MoveBanEvent(); }
        public override void Apply(GameEventOwner owner, Character ownerChar, Character character)
        {
            for (int ii = 0; ii < character.Skills.Count; ii++)
            {
                if (character.Skills[ii].Element.SkillNum == ((MapStatus)owner).StatusStates.GetWithDefault<MapIndexState>().Index)
                    character.Skills[ii].Element.Sealed = true;
            }
        }
    }
    [Serializable]
    public class MovementScrambleEvent : RefreshEvent
    {
        public override GameEvent Clone() { return new MovementScrambleEvent(); }
        public override void Apply(GameEventOwner owner, Character ownerChar, Character character)
        {
            character.MovesScrambled = true;
        }
    }
    [Serializable]
    public class PPSaverEvent : RefreshEvent
    {
        public override GameEvent Clone() { return new PPSaverEvent(); }
        public override void Apply(GameEventOwner owner, Character ownerChar, Character character)
        {
            character.ChargeSaver = true;
        }
    }
    [Serializable]
    public class ThrownItemBarrierEvent : RefreshEvent
    {
        public override GameEvent Clone() { return new ThrownItemBarrierEvent(); }
        public override void Apply(GameEventOwner owner, Character ownerChar, Character character)
        {
            character.StopItemAtHit = true;
        }
    }
    [Serializable]
    public class FriendlyFiredEvent : RefreshEvent
    {
        public override GameEvent Clone() { return new FriendlyFiredEvent(); }
        public override void Apply(GameEventOwner owner, Character ownerChar, Character character)
        {
            character.EnemyOfFriend = true;
        }
    }
    [Serializable]
    public class MiscEvent : RefreshEvent
    {
        public CharState Effect;
        public MiscEvent() { }
        public MiscEvent(CharState effect)
        {
            Effect = effect;
        }
        protected MiscEvent(MiscEvent other)
        {
            Effect = other.Effect.Clone<CharState>();
        }
        public override GameEvent Clone() { return new MiscEvent(this); }
        public override void Apply(GameEventOwner owner, Character ownerChar, Character character)
        {
            character.CharStates.Set(Effect.Clone<CharState>());
        }
    }


    [Serializable]
    public class MapNoSwitchEvent : RefreshEvent
    {
        public override GameEvent Clone() { return new MapNoSwitchEvent(); }
        public override void Apply(GameEventOwner owner, Character ownerChar, Character character)
        {
            ZoneManager.Instance.CurrentMap.NoSwitching = true;
        }
    }

    [Serializable]
    public class MapNoRescueEvent : RefreshEvent
    {
        public override GameEvent Clone() { return new MapNoRescueEvent(); }
        public override void Apply(GameEventOwner owner, Character ownerChar, Character character)
        {
            ZoneManager.Instance.CurrentMap.NoRescue = true;
        }
    }
}
