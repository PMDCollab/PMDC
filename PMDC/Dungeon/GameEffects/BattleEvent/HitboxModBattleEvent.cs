using System;
using System.Collections.Generic;
using RogueEssence.Data;
using RogueEssence.Menu;
using RogueElements;
using RogueEssence.Content;
using RogueEssence.LevelGen;
using RogueEssence;
using RogueEssence.Dungeon;
using RogueEssence.Dev;
using PMDC.Dev;
using PMDC.Data;
using System.Runtime.Serialization;
using Newtonsoft.Json;
using NLua;
using RogueEssence.Script;
using System.Linq;

namespace PMDC.Dungeon
{
    // Battle events that modify the hitbox, such as its hitbox type, wideness, range, or targeting

    /// <summary>
    /// Event that sets the total strikes to be 1 if no strikes have been made
    /// Used by the move Sky Drop
    /// </summary>
    [Serializable]
    public class SingleStrikeEvent : BattleEvent
    {
        public override GameEvent Clone() { return new SingleStrikeEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.StrikesMade == 0)
                context.Strikes = 1;

            yield break;
        }
    }

    /// <summary>
    /// Event that modifies the total amount the character strikes
    /// </summary>
    [Serializable]
    public class MultiStrikeEvent : BattleEvent
    {

        /// <summary>
        /// The total amount of strikes
        /// </summary>
        public int StrikeMult;

        /// <summary>
        /// Whether to make the strikes progressively weaker
        /// </summary>
        public bool Div;

        public MultiStrikeEvent() { }
        public MultiStrikeEvent(int mult, bool div)
        {
            StrikeMult = mult;
            Div = div;
        }
        protected MultiStrikeEvent(MultiStrikeEvent other)
        {
            StrikeMult = other.StrikeMult;
            Div = other.Div;
        }
        public override GameEvent Clone() { return new MultiStrikeEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.StrikesMade == 0)
            {
                context.Strikes *= StrikeMult;
                if (Div && (context.Data.Category == BattleData.SkillCategory.Physical || context.Data.Category == BattleData.SkillCategory.Magical))
                    context.AddContextStateMult<DmgMult>(false, 1, StrikeMult);
            }
            yield break;
        }
    }


    /// <summary>
    /// UNUSED
    /// Event that causes the character to use the effects of berries twice.
    /// </summary>
    [Serializable]
    public class HarvestEvent : BattleEvent
    {
        public override GameEvent Clone() { return new HarvestEvent(); }
        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.ActionType == BattleActionType.Item && context.StrikesMade == 0)
            {
                ItemData itemData = DataManager.Instance.GetItem(context.Item.ID);
                if (itemData.ItemStates.Contains<BerryState>())
                    context.Strikes *= 2;
            }
            yield break;
        }
    }


    /// <summary>
    /// Event that modifies the damage multiplier based on the strikes made divided by a denominator
    /// </summary>
    [Serializable]
    public class RepeatStrikeEvent : BattleEvent
    {
        /// <summary>
        /// The denominator of the modifier
        /// </summary>
        public int Denominator;

        public RepeatStrikeEvent() { }
        public RepeatStrikeEvent(int denominator)
        {
            Denominator = denominator;
        }
        protected RepeatStrikeEvent(RepeatStrikeEvent other)
        {
            Denominator = other.Denominator;
        }
        public override GameEvent Clone() { return new RepeatStrikeEvent(this); }


        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            context.AddContextStateMult<DmgMult>(false, context.StrikesMade + 1, Denominator);
            yield break;
        }
    }


    /// <summary>
    /// Event that modifies the range of the skill category affected
    /// </summary>
    [Serializable]
    public class CategoryAddRangeEvent : BattleEvent
    {

        /// <summary>
        /// The affected skill category
        /// </summary> 
        public BattleData.SkillCategory Category;

        /// <summary>
        /// The range modifer
        /// </summary>
        public int Range;

        public CategoryAddRangeEvent() { }
        public CategoryAddRangeEvent(BattleData.SkillCategory category, int range)
        {
            Category = category;
            Range = range;
        }
        protected CategoryAddRangeEvent(CategoryAddRangeEvent other)
        {
            Category = other.Category;
            Range = other.Range;
        }
        public override GameEvent Clone() { return new CategoryAddRangeEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (Category == BattleData.SkillCategory.None || context.Data.Category == Category)
                context.RangeMod += Range;
            yield break;
        }
    }

    /// <summary>
    /// Event that modifies the range of moves under a map status
    /// </summary>
    [Serializable]
    public class WeatherAddRangeEvent : BattleEvent
    {

        /// <summary>
        /// The map status to check for
        /// </summary>
        [JsonConverter(typeof(MapStatusConverter))]
        [DataType(0, DataManager.DataType.MapStatus, false)]
        public string WeatherID;

        /// <summary>
        /// The range modifer
        /// </summary>
        public int Range;

        public WeatherAddRangeEvent() { WeatherID = ""; }
        public WeatherAddRangeEvent(string weatherId, int range)
        {
            WeatherID = weatherId;
            Range = range;
        }
        protected WeatherAddRangeEvent(WeatherAddRangeEvent other)
        {
            WeatherID = other.WeatherID;
            Range = other.Range;
        }
        public override GameEvent Clone() { return new WeatherAddRangeEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (ZoneManager.Instance.CurrentMap.Status.ContainsKey(WeatherID))
                context.RangeMod += Range;
            yield break;
        }
    }


    /// <summary>
    /// Event that modifies hitbox action of moves to hit tiles
    /// </summary>
    [Serializable]
    public class MeleeHitTilesEvent : BattleEvent
    {

        /// <summary>
        /// USUSED
        /// </summary>
        public TileAlignment Tile;

        public MeleeHitTilesEvent() { }
        public MeleeHitTilesEvent(TileAlignment tile)
        {
            Tile = tile;
        }
        protected MeleeHitTilesEvent(MeleeHitTilesEvent other)
        {
            Tile = other.Tile;
        }
        public override GameEvent Clone() { return new MeleeHitTilesEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.ActionType != BattleActionType.Skill)
                yield break;

            if (context.HitboxAction is AttackAction)
            {
                ((AttackAction)context.HitboxAction).HitTiles = true;
                ((AttackAction)context.HitboxAction).WideAngle = AttackCoverage.FrontAndCorners;
            }
            else if (context.HitboxAction is DashAction)
            {
                context.Explosion.HitTiles = true;
                ((DashAction)context.HitboxAction).WideAngle = LineCoverage.FrontAndCorners;
            }
            yield break;
        }
    }

    /// <summary>
    /// Event that modifies the hitbox action to pierce through enemies and walls
    /// </summary>
    [Serializable]
    public class PierceEvent : BattleEvent
    {
        /// <summary>
        /// Whether to allow moves to pierce
        /// </summary>
        public bool SkillsPierce;

        /// <summary>
        /// Whether to allow items to pierce
        /// </summary>
        public bool ItemsPierce;

        /// <summary>
        /// Whether the action can pierce through enemies
        /// </summary>
        public bool PierceEnemies;

        /// <summary>
        /// Whether the action can pierce through walls
        /// </summary>
        public bool PierceWalls;

        public PierceEvent() { }
        public PierceEvent(bool skills, bool items, bool enemies, bool walls)
        {
            SkillsPierce = skills;
            ItemsPierce = items;
            PierceEnemies = enemies;
            PierceWalls = walls;
        }
        protected PierceEvent(PierceEvent other)
        {
            SkillsPierce = other.SkillsPierce;
            ItemsPierce = other.ItemsPierce;
            PierceEnemies = other.PierceEnemies;
            PierceWalls = other.PierceWalls;
        }
        public override GameEvent Clone() { return new PierceEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.ActionType == BattleActionType.Throw)
            {
                if (!ItemsPierce)
                    yield break;
                //can't pierce-throw edibles
                ItemData entry = DataManager.Instance.GetItem(context.Item.ID);
                if (entry.ItemStates.Contains<EdibleState>())
                    yield break;
            }
            else if (context.ActionType == BattleActionType.Skill)
            {
                if (!SkillsPierce)
                    yield break;
            }
            else
            {
                yield break;
            }

            if (context.HitboxAction is LinearAction)
            {
                if (PierceEnemies)
                    ((LinearAction)context.HitboxAction).StopAtHit = false;
                if (PierceWalls)
                    ((LinearAction)context.HitboxAction).StopAtWall = false;
            }
            yield break;
        }
    }

    /// <summary>
    /// Event that modifies the hitbox action to stop piercing through enemies and walls
    /// </summary>
    [Serializable]
    public class NoPierceEvent : BattleEvent
    {
        /// <summary>
        /// Whether the action should stop piercing enemies
        /// </summary>
        public bool PierceEnemies;

        /// <summary>
        /// Whether the action should stop piercing walls
        /// </summary>
        public bool PierceWalls;

        public NoPierceEvent() { }
        public NoPierceEvent(bool enemies, bool walls)
        {
            PierceEnemies = enemies;
            PierceWalls = walls;
        }
        protected NoPierceEvent(NoPierceEvent other)
        {
            PierceEnemies = other.PierceEnemies;
            PierceWalls = other.PierceWalls;
        }
        public override GameEvent Clone() { return new NoPierceEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.HitboxAction is LinearAction)
            {
                if (PierceEnemies)
                    ((LinearAction)context.HitboxAction).StopAtHit = true;
                if (PierceWalls)
                    ((LinearAction)context.HitboxAction).StopAtWall = true;
            }
            yield break;
        }
    }

    /// <summary>
    /// Event that modifies the amount of ray projectiles of an action
    /// </summary>
    [Serializable]
    public class SpreadProjectileEvent : BattleEvent
    {

        /// <summary>
        /// The ray projectile amount
        /// </summary>
        public ProjectileAction.RayCount Rays;

        public SpreadProjectileEvent() { }
        public SpreadProjectileEvent(ProjectileAction.RayCount rays)
        {
            Rays = rays;
        }
        protected SpreadProjectileEvent(SpreadProjectileEvent other)
        {
            Rays = other.Rays;
        }
        public override GameEvent Clone() { return new SpreadProjectileEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {

            if (context.HitboxAction is ProjectileAction)
            {
                ((ProjectileAction)context.HitboxAction).Rays = Rays;
            }
            yield break;
        }
    }

    /// <summary>
    /// UNUSED
    /// Event that makes dash or attack actions wide.
    /// </summary>
    [Serializable]
    public class MakeWideEvent : BattleEvent
    {
        public override GameEvent Clone() { return new MakeWideEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {

            if (context.HitboxAction is AttackAction)
            {
                ((AttackAction)context.HitboxAction).WideAngle = AttackCoverage.Wide;
                ((AttackAction)context.HitboxAction).CharAnimData = new CharAnimFrameType(40);//Swing
            }
            else if (context.HitboxAction is DashAction)
            {
                ((DashAction)context.HitboxAction).WideAngle = LineCoverage.Wide;
                ((DashAction)context.HitboxAction).CharAnim = 40;//Swing
            }
            yield break;
        }
    }





    /// <summary>
    /// Event that modifies the range
    /// </summary>
    [Serializable]
    public class AddRangeEvent : BattleEvent
    {
        /// <summary>
        /// The range modifier
        /// </summary>
        public int Range;

        /// <summary>
        /// The list of battle events that will be applied
        /// </summary>
        public List<BattleEvent> Anims;

        public AddRangeEvent() { Anims = new List<BattleEvent>(); }
        public AddRangeEvent(int range, params BattleEvent[] anims)
        {
            Range = range;

            Anims = new List<BattleEvent>();
            Anims.AddRange(anims);
        }
        protected AddRangeEvent(AddRangeEvent other)
        {
            Range = other.Range;

            Anims = new List<BattleEvent>();
            foreach (BattleEvent anim in other.Anims)
                Anims.Add((BattleEvent)anim.Clone());
        }
        public override GameEvent Clone() { return new AddRangeEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.ActionType == BattleActionType.Skill && context.Data.ID != DataManager.Instance.DefaultSkill)
            {
                foreach (BattleEvent anim in Anims)
                    yield return CoroutineManager.Instance.StartCoroutine(anim.Apply(owner, ownerChar, context));

                context.RangeMod += Range;
            }
        }
    }

    /// <summary>
    /// Event that modifies the range if the user is the specified type
    /// </summary>
    [Serializable]
    public class ElementAddRangeEvent : BattleEvent
    {
        /// <summary>
        /// The list of valid types
        /// </summary>
        [DataType(1, DataManager.DataType.Element, false)]
        public HashSet<string> Elements;

        public int Range;

        public ElementAddRangeEvent()
        {
            Elements = new HashSet<string>();
        }

        public ElementAddRangeEvent(int range, HashSet<string> elements) : this()
        {
            Range = range;
            Elements = elements;
        }
        protected ElementAddRangeEvent(ElementAddRangeEvent other) : this()
        {
            Range = other.Range;
            foreach (string element in other.Elements)
                Elements.Add(element);
        }

        public override GameEvent Clone() { return new ElementAddRangeEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (Elements.Contains(context.User.Element1) || Elements.Contains(context.User.Element2))
            {
                context.RangeMod += Range;
            }
            yield break;
        }
    }



    /// <summary>
    /// Event that makes the character return to its original position after a dash action
    /// </summary>
    [Serializable]
    public class SnapDashBackEvent : BattleEvent
    {
        public override GameEvent Clone() { return new SnapDashBackEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            DashAction dash = context.HitboxAction as DashAction;
            if (dash != null)
                dash.SnapBack = true;
            yield break;
        }
    }




    /// <summary>
    /// Event that causes the user moves to not affect friendly targets
    /// </summary>
    [Serializable]
    public class NontraitorEvent : BattleEvent
    {
        public override GameEvent Clone() { return new NontraitorEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            BasePowerState basePower = context.Data.SkillStates.GetWithDefault<BasePowerState>();
            if (basePower != null && context.ActionType == BattleActionType.Skill)
            {
                context.HitboxAction.TargetAlignments &= ~Alignment.Friend;
                context.Explosion.TargetAlignments &= ~Alignment.Friend;
            }
            yield break;
        }
    }

    /// <summary>
    /// Event that causes damaging battle actions that hit in a straight line to not affect friendly targets
    /// </summary>
    [Serializable]
    public class GapProberEvent : BattleEvent
    {
        public override GameEvent Clone() { return new GapProberEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            BasePowerState basePower = context.Data.SkillStates.GetWithDefault<BasePowerState>();
            if (basePower != null && context.HitboxAction is LinearAction)
            {
                context.HitboxAction.TargetAlignments &= ~Alignment.Friend;
                context.Explosion.TargetAlignments &= ~Alignment.Friend;
            }
            yield break;
        }
    }

    /// <summary>
    /// Event that causes battle actions that target foes to also hit friendly targets and vice versa.
    /// </summary>
    [Serializable]
    public class TraitorEvent : BattleEvent
    {
        public override GameEvent Clone() { return new TraitorEvent(); }
        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if ((context.HitboxAction.TargetAlignments & Alignment.Foe) != Alignment.None)
                context.HitboxAction.TargetAlignments |= Alignment.Friend;
            if ((context.HitboxAction.TargetAlignments & Alignment.Friend) != Alignment.None)
                context.HitboxAction.TargetAlignments |= Alignment.Foe;
            if ((context.Explosion.TargetAlignments & Alignment.Foe) != Alignment.None)
                context.Explosion.TargetAlignments |= Alignment.Friend;
            if ((context.Explosion.TargetAlignments & Alignment.Friend) != Alignment.None)
                context.Explosion.TargetAlignments |= Alignment.Foe;
            yield break;
        }
    }
}

