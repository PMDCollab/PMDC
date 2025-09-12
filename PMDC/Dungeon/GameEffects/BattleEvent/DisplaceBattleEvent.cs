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
    // Battle events that change the target's position on the map

    /// <summary>
    /// Event that makes the user hop by the specified distance
    /// </summary>
    [Serializable]
    public class HopEvent : BattleEvent
    {
        /// <summary>
        /// The total distance to hop
        /// </summary>
        public int Distance;

        /// <summary>
        /// Whether to hop forwards or backwards
        /// </summary>
        public bool Reverse;

        /// <summary>
        /// Whether to affect the user or target
        /// </summary>
        public bool AffectTarget;

        public HopEvent() { }
        public HopEvent(int distance, bool reverse)
        {
            Distance = distance;
            Reverse = reverse;
        }
        protected HopEvent(HopEvent other)
        {
            Distance = other.Distance;
            Reverse = other.Reverse;
            AffectTarget = other.AffectTarget;
        }
        public override GameEvent Clone() { return new HopEvent(this); }


        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            Character target = (AffectTarget ? context.Target : context.User);

            if (target.Dead)
                yield break;
            //jump back a number of spaces
            if (target.CharStates.Contains<AnchorState>())
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_ANCHORED").ToLocal(), target.GetDisplayName(false)));
            else
            {
                Dir8 hopDir = (Reverse ? target.CharDir.Reverse() : target.CharDir);
                yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.Instance.JumpTo(target, hopDir, Distance));
            }
        }
    }

    /// <summary>
    /// Event that transport the user and nearby allies to the tile directly in front of another character or wall
    /// </summary>
    [Serializable]
    public class PounceEvent : BattleEvent
    {
        /// <summary>
        /// The radius that allies must be within in order to pounce
        /// </summary>
        public int AllyRadius;
        public PounceEvent()
        { }
        public PounceEvent(int allyRadius)
        {
            AllyRadius = allyRadius;
        }
        public PounceEvent(PounceEvent other)
        {
            AllyRadius = other.AllyRadius;
        }
        public override GameEvent Clone() { return new PounceEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            Character target = context.User;
            if (target == null || target.Dead)
                yield break;

            List<Character> allies = new List<Character>();
            //take count of allies
            if (AllyRadius > 0)
            {
                foreach (Character character in ZoneManager.Instance.CurrentMap.GetCharsInFillRect(target.CharLoc, Rect.FromPointRadius(target.CharLoc, AllyRadius)))
                {
                    if (!character.Dead && DungeonScene.Instance.GetMatchup(character, target) == Alignment.Friend)
                        allies.Add(character);
                }
            }


            yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.Instance.Pounce(target, context.User.CharDir, context.StrikeStartTile, (context.StrikeStartTile - context.TargetTile).Dist8()));

            //place the allies
            foreach (Character ally in allies)
            {
                if (ally.CharStates.Contains<AnchorState>())
                    DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_ANCHORED").ToLocal(), ally.GetDisplayName(false)));
                else
                    yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.Instance.WarpNear(ally, context.User.CharLoc));
            }
        }
    }

    /// <summary>
    /// Event that makes the target warp in front of the user
    /// </summary>
    [Serializable]
    public class LureEvent : BattleEvent
    {
        public override GameEvent Clone() { return new LureEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            Character target = context.Target;
            if (target == null || target.Dead)
                yield break;

            //knock back a number of spaces
            if (target.CharStates.Contains<AnchorState>())
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_ANCHORED").ToLocal(), target.GetDisplayName(false)));
            else
                yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.Instance.WarpNear(target, context.User.CharLoc + context.User.CharDir.GetLoc()));
        }
    }

    /// <summary>
    /// Event that knocks the target back by the specified distance
    /// </summary>
    [Serializable]
    public class KnockBackEvent : BattleEvent
    {
        /// <summary>
        /// The distance to knock back
        /// </summary>
        public int Distance;

        public KnockBackEvent() { }
        public KnockBackEvent(int distance) { Distance = distance; }
        protected KnockBackEvent(KnockBackEvent other)
        {
            Distance = other.Distance;
        }
        public override GameEvent Clone() { return new KnockBackEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.Target.Dead)
                yield break;

            //knock back a number of spaces
            if (context.Target.CharStates.Contains<AnchorState>())
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_ANCHORED").ToLocal(), context.Target.GetDisplayName(false)));
            else
            {
                Dir8 dir = ZoneManager.Instance.CurrentMap.GetClosestDir8(context.User.CharLoc, context.Target.CharLoc);
                if (dir == Dir8.None)
                    dir = context.User.CharDir.Reverse();
                yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.Instance.KnockBack(context.Target, dir, Distance));
            }
        }
    }

    /// <summary>
    /// Event that throws the target backwards by the specified distance 
    /// </summary>
    [Serializable]
    public class ThrowBackEvent : BattleEvent
    {
        /// <summary>
        /// The distance to throw the target back
        /// </summary>
        public int Distance;

        /// <summary>
        /// The event calculating how much damage the target will take
        /// </summary>
        public CalculatedDamageEvent HitEvent;

        public ThrowBackEvent() { }
        public ThrowBackEvent(int distance, CalculatedDamageEvent hitEvent) { Distance = distance; HitEvent = hitEvent; }
        protected ThrowBackEvent(ThrowBackEvent other)
        {
            Distance = other.Distance;
            HitEvent = (CalculatedDamageEvent)other.HitEvent.Clone();
        }
        public override GameEvent Clone() { return new ThrowBackEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.Target.Dead)
                yield break;

            //knock back a number of spaces
            if (context.Target.CharStates.Contains<AnchorState>())
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_ANCHORED").ToLocal(), context.Target.GetDisplayName(false)));
            else
            {
                int damage = HitEvent.CalculateDamage(owner, context);
                ThrowTargetContext throwContext = new ThrowTargetContext(damage);
                Dir8 dir = ZoneManager.Instance.CurrentMap.GetClosestDir8(context.User.CharLoc, context.Target.CharLoc);
                if (dir == Dir8.None)
                    dir = context.User.CharDir.Reverse();
                yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.Instance.ThrowTo(context.Target, context.User,
                    dir, Distance, Alignment.Foe, throwContext.Hit));
            }
        }

        private class ThrowTargetContext
        {
            /// <summary>
            /// The total damage the target will take
            /// </summary>
            public int Damage;
            public ThrowTargetContext(int damage)
            {
                Damage = damage;
            }

            public IEnumerator<YieldInstruction> Hit(Character targetChar, Character attacker)
            {
                GameManager.Instance.BattleSE("DUN_Hit_Neutral");
                if (!targetChar.Unidentifiable)
                {
                    SingleEmitter endEmitter = new SingleEmitter(new AnimData("Hit_Neutral", 3));
                    endEmitter.SetupEmit(targetChar.MapLoc, attacker.MapLoc, targetChar.CharDir);
                    DungeonScene.Instance.CreateAnim(endEmitter, DrawLayer.NoDraw);
                }

                yield return CoroutineManager.Instance.StartCoroutine(targetChar.InflictDamage(Damage, true));
            }

        }

    }

    /// <summary>
    /// Event that knocks back all characters within 1-tile away by the specified distance
    /// </summary>
    [Serializable]
    public class LaunchAllEvent : BattleEvent
    {

        /// <summary>
        /// The distance to knock back
        /// </summary>
        public int Distance;

        public LaunchAllEvent() { }
        public LaunchAllEvent(int distance) { Distance = distance; }
        protected LaunchAllEvent(LaunchAllEvent other)
        {
            Distance = other.Distance;
        }
        public override GameEvent Clone() { return new LaunchAllEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            Dir8 moveDir = context.User.CharDir;
            yield return CoroutineManager.Instance.StartCoroutine(launchTile(context.TargetTile, moveDir, Dir8.Down));
            yield return CoroutineManager.Instance.StartCoroutine(launchTile(context.TargetTile, moveDir, Dir8.DownLeft));
            yield return CoroutineManager.Instance.StartCoroutine(launchTile(context.TargetTile, moveDir, Dir8.DownRight));
            yield return CoroutineManager.Instance.StartCoroutine(launchTile(context.TargetTile, moveDir, Dir8.Left));
            yield return CoroutineManager.Instance.StartCoroutine(launchTile(context.TargetTile, moveDir, Dir8.None));
            yield return CoroutineManager.Instance.StartCoroutine(launchTile(context.TargetTile, moveDir, Dir8.Right));
            yield return CoroutineManager.Instance.StartCoroutine(launchTile(context.TargetTile, moveDir, Dir8.UpLeft));
            yield return CoroutineManager.Instance.StartCoroutine(launchTile(context.TargetTile, moveDir, Dir8.UpRight));
            yield return CoroutineManager.Instance.StartCoroutine(launchTile(context.TargetTile, moveDir, Dir8.Up));
        }

        private IEnumerator<YieldInstruction> launchTile(Loc loc, Dir8 dir, Dir8 offsetDir)
        {
            if (offsetDir != Dir8.None)
                loc = loc + DirExt.AddAngles(dir, offsetDir).GetLoc();
            Character target = ZoneManager.Instance.CurrentMap.GetCharAtLoc(loc);
            if (target != null)
            {
                //knock back a number of spaces
                if (target.CharStates.Contains<AnchorState>())
                    DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_ANCHORED").ToLocal(), target.GetDisplayName(false)));
                else
                    yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.Instance.KnockBack(target, dir, Distance));
            }
        }
    }

    /// <summary>
    /// Event that warps a character and nearby allies to a random location within the specified distance
    /// </summary>
    [Serializable]
    public class RandomGroupWarpEvent : BattleEvent
    {
        /// <summary>
        /// The max warp distance 
        /// </summary>
        public int Distance;

        /// <summary>
        /// Whether to affect the target or user
        /// </summary>
        public bool AffectTarget;

        public RandomGroupWarpEvent() { }
        public RandomGroupWarpEvent(int distance, bool affectTarget)
        {
            Distance = distance;
            AffectTarget = affectTarget;
        }
        protected RandomGroupWarpEvent(RandomGroupWarpEvent other)
        {
            Distance = other.Distance;
            AffectTarget = other.AffectTarget;
        }
        public override GameEvent Clone() { return new RandomGroupWarpEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            Character target = (AffectTarget ? context.Target : context.User);
            if (target.Dead)
                yield break;

            if (target.CharStates.Contains<AnchorState>())
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_ANCHORED").ToLocal(), target.GetDisplayName(false)));
            else
            {
                //warp within the space
                Loc startLoc = target.CharLoc;
                yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.Instance.RandomWarp(target, Distance));
                foreach (Character character in ZoneManager.Instance.CurrentMap.GetCharsInFillRect(startLoc, Rect.FromPointRadius(startLoc, 1)))
                {
                    if (!character.Dead && DungeonScene.Instance.GetMatchup(character, target) == Alignment.Friend)
                    {
                        if (character.CharStates.Contains<AnchorState>())
                            DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_ANCHORED").ToLocal(), character.GetDisplayName(false)));
                        else
                            yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.Instance.WarpNear(character, target.CharLoc));
                    }
                }
            }
        }
    }

    /// <summary>
    /// Event that warps a character to a random location within the specified distance
    /// </summary>
    [Serializable]
    public class RandomWarpEvent : BattleEvent
    {

        /// <summary>
        /// The max warp distance 
        /// </summary>
        public int Distance;

        /// <summary>
        /// Whether to affect the target or user
        /// </summary>
        public bool AffectTarget;

        /// <summary>
        /// The message displayed in the dungeon log 
        /// </summary>
        [StringKey(0, true)]
        public StringKey TriggerMsg;

        public RandomWarpEvent() { }
        public RandomWarpEvent(int distance, bool affectTarget)
        {
            Distance = distance;
            AffectTarget = affectTarget;
        }
        public RandomWarpEvent(int distance, bool affectTarget, StringKey triggerMsg)
        {
            Distance = distance;
            AffectTarget = affectTarget;
            TriggerMsg = triggerMsg;
        }
        protected RandomWarpEvent(RandomWarpEvent other)
        {
            Distance = other.Distance;
            AffectTarget = other.AffectTarget;
        }
        public override GameEvent Clone() { return new RandomWarpEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            Character target = (AffectTarget ? context.Target : context.User);
            if (target.Dead)
                yield break;
            //warp within the space
            if (target.CharStates.Contains<AnchorState>())
            {
                if (!TriggerMsg.IsValid())
                    DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_ANCHORED").ToLocal(), target.GetDisplayName(false)));
            }
            else
            {
                if (TriggerMsg.IsValid())
                    DungeonScene.Instance.LogMsg(Text.FormatGrammar(TriggerMsg.ToLocal(), ownerChar.GetDisplayName(false), owner.GetDisplayName()));

                yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.Instance.RandomWarp(target, Distance));
            }
        }
    }

    /// <summary>
    /// Event that warps the character nearby the stairs
    /// </summary>
    [Serializable]
    public class WarpToEndEvent : BattleEvent
    {
        /// <summary>
        /// The max warp distance to check for the end point
        /// </summary>
        public int Distance;

        /// <summary>
        /// The max distance away the character will be from the end point
        /// </summary>
        public int DiffRange;


        public bool AffectTarget;


        public WarpToEndEvent() { }
        public WarpToEndEvent(int distance, int diff, bool affectTarget)
        {
            Distance = distance;
            DiffRange = diff;
            AffectTarget = affectTarget;
        }
        protected WarpToEndEvent(WarpToEndEvent other)
        {
            Distance = other.Distance;
            DiffRange = other.DiffRange;
            AffectTarget = other.AffectTarget;
        }
        public override GameEvent Clone() { return new WarpToEndEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            Character target = (AffectTarget ? context.Target : context.User);
            if (target.Dead)
                yield break;
            //warp within the space
            if (target.CharStates.Contains<AnchorState>())
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_ANCHORED").ToLocal(), target.GetDisplayName(false)));
            else
                yield return CoroutineManager.Instance.StartCoroutine(WarpToEnd(target, Distance, DiffRange));
        }


        public static List<Loc> FindExits()
        {
            List<Loc> exits = new List<Loc>();
            for (int xx = 0; xx < ZoneManager.Instance.CurrentMap.Width; xx++)
            {
                for (int yy = 0; yy < ZoneManager.Instance.CurrentMap.Height; yy++)
                {
                    Tile tile = ZoneManager.Instance.CurrentMap.Tiles[xx][yy];

                    if (tile.Effect.ID == "stairs_go_up" || tile.Effect.ID == "stairs_go_down")//TODO: remove this magic number
                        exits.Add(new Loc(xx, yy));
                }
            }
            return exits;
        }

        public static IEnumerator<YieldInstruction> WarpToEnd(Character character, int radius, int diffRange, bool msg = true)
        {
            List<Character> characters = new List<Character>();

            Loc? loc = Grid.FindClosestConnectedTile(character.CharLoc - new Loc(radius), new Loc(radius * 2 + 1),
                (Loc testLoc) =>
                {

                    Tile tile = ZoneManager.Instance.CurrentMap.GetTile(testLoc);
                    if (tile == null)
                        return false;

                    if (tile.Effect.ID == "stairs_go_up" || tile.Effect.ID == "stairs_go_down")//TODO: remove this magic number
                        return true;
                    return false;
                },
                (Loc testLoc) =>
                {
                    return ZoneManager.Instance.CurrentMap.TileBlocked(testLoc, true);
                },
                (Loc testLoc) =>
                {
                    return ZoneManager.Instance.CurrentMap.TileBlocked(testLoc, true, true);
                },
                character.CharLoc);

            if (!loc.HasValue)
            {
                if (msg)
                    DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_NO_EXIT").ToLocal()));
            }
            else
                yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.Instance.WarpNear(character, loc.Value, diffRange, msg));
        }
    }

    /// <summary>
    /// Event that warps the user nearby the target
    /// </summary>
    [Serializable]
    public class WarpHereEvent : BattleEvent
    {
        /// <summary>
        /// The message displayed in the dungeon log 
        /// </summary>
        [StringKey(0, true)]
        public StringKey Msg;

        /// <summary>
        /// Whether to warp the target nearby the user
        /// </summary>
        public bool AffectTarget;

        public WarpHereEvent() { }
        public WarpHereEvent(StringKey msg, bool affectTarget)
        {
            Msg = msg;
            AffectTarget = affectTarget;
        }
        protected WarpHereEvent(WarpHereEvent other)
        {
            Msg = other.Msg;
            AffectTarget = other.AffectTarget;
        }
        public override GameEvent Clone() { return new WarpHereEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            Character target = (AffectTarget ? context.Target : context.User);
            Character origin = (AffectTarget ? context.User : context.Target);
            if (target.Dead || origin.Dead)
                yield break;

            if (target.CharStates.Contains<AnchorState>())
                yield break;


            if (Msg.IsValid())
            {
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(Msg.ToLocal(), origin.GetDisplayName(false), target.GetDisplayName(false)));
                yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.Instance.WarpNear(target, origin.CharLoc, false));
            }
            else
                yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.Instance.WarpNear(target, origin.CharLoc, true));
        }
    }

    /// <summary>
    /// Event that warps the character to one of its nearby allies
    /// </summary>
    [Serializable]
    public class WarpToAllyEvent : BattleEvent
    {
        public WarpToAllyEvent() { }
        public override GameEvent Clone() { return new WarpToAllyEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.Target.CharStates.Contains<AnchorState>())
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_ANCHORED").ToLocal(), context.Target.GetDisplayName(false)));
            else
            {
                foreach (Character character in context.Target.MemberTeam.Players)
                {
                    if (character != context.Target)
                    {
                        //found a target
                        //are we already next to them?
                        if (ZoneManager.Instance.CurrentMap.InRange(character.CharLoc, context.Target.CharLoc, 1))
                            break;
                        for (int ii = 0; ii < DirRemap.FOCUSED_DIR8.Length; ii++)
                        {
                            //always warp behind the target
                            Dir8 dir = DirExt.AddAngles(DirRemap.FOCUSED_DIR8[ii], DirExt.AddAngles(character.CharDir, Dir8.Up));
                            if (!ZoneManager.Instance.CurrentMap.DirBlocked(dir, character.CharLoc, context.Target.Mobility))
                            {
                                Loc targetLoc = character.CharLoc + dir.GetLoc();
                                yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.Instance.PointWarp(context.Target, targetLoc, false));
                                yield break;
                            }
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// Event that warps allies to the user that are within the specified distance 
    /// </summary>
    [Serializable]
    public class WarpAlliesInEvent : BattleEvent
    {

        /// <summary>
        /// The max distance that allies can be summoned from
        /// </summary>
        public int Distance;

        /// <summary>
        /// The max amount of allies to summon
        /// </summary>
        public int Amount;

        /// <summary>
        /// Whether to warp the furthest allies
        /// </summary>
        public bool FarthestFirst;

        /// <summary>
        /// Whether to print a fail message if no allies are warped
        /// </summary>
        public bool SilentFail;

        /// <summary>
        /// The message displayed in the dungeon log if an ally was warped
        /// </summary>
        public StringKey Msg;

        public WarpAlliesInEvent() { }
        public WarpAlliesInEvent(int distance, int allies, bool farthestFirst, StringKey msg, bool silentFail)
        {
            Distance = distance;
            Amount = allies;
            FarthestFirst = farthestFirst;
            Msg = msg;
            SilentFail = silentFail;
        }
        protected WarpAlliesInEvent(WarpAlliesInEvent other)
        {
            Distance = other.Distance;
            Amount = other.Amount;
            FarthestFirst = other.FarthestFirst;
            Msg = other.Msg;
            SilentFail = other.SilentFail;
        }
        public override GameEvent Clone() { return new WarpAlliesInEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            StablePriorityQueue<int, Character> targets = new StablePriorityQueue<int, Character>();
            foreach (Character character in ZoneManager.Instance.CurrentMap.GetCharsInFillRect(context.Target.CharLoc, Rect.FromPointRadius(context.Target.CharLoc, Distance)))
            {
                if (!character.Dead && DungeonScene.Instance.GetMatchup(character, context.Target) == Alignment.Friend)
                    targets.Enqueue((FarthestFirst ? -1 : 1) * (character.CharLoc - context.Target.CharLoc).DistSquared(), character);
            }
            int totalWarp = 0;
            for (int ii = 0; ii < Amount && targets.Count > 0; ii++)
            {
                Character target = targets.Dequeue();
                if (target.CharStates.Contains<AnchorState>())
                    yield break;

                yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.Instance.WarpNear(target, context.Target.CharLoc, false));
                if (target.MemberTeam.MapFaction != Faction.Player)
                    target.TurnUsed = true;
                totalWarp++;
            }
            if (totalWarp > 0)
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(Msg.ToLocal(), context.Target.GetDisplayName(false)));
            else if (!SilentFail)
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_NOTHING_HAPPENED").ToLocal()));
        }
    }

    /// <summary>
    /// Event that warps enemies to the user that are within the specified distance 
    /// </summary>
    [Serializable]
    public class WarpFoesToTileEvent : BattleEvent
    {

        /// <summary>
        /// The max amount of allies to summon
        /// </summary>
        public int Amount;

        /// <summary>
        /// The max distance that enemies can be summoned from
        /// </summary>
        public int Distance;

        public WarpFoesToTileEvent() { }
        public WarpFoesToTileEvent(int distance, int foes) { Distance = distance; Amount = foes; }
        protected WarpFoesToTileEvent(WarpFoesToTileEvent other) { Distance = other.Distance; Amount = other.Amount; }
        public override GameEvent Clone() { return new WarpFoesToTileEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            StablePriorityQueue<int, Character> targets = new StablePriorityQueue<int, Character>();
            foreach (Character character in ZoneManager.Instance.CurrentMap.GetCharsInFillRect(context.User.CharLoc, Rect.FromPointRadius(context.User.CharLoc, Distance)))
            {
                if (!character.Dead && DungeonScene.Instance.GetMatchup(character, context.User) == Alignment.Foe)
                    targets.Enqueue(-(character.CharLoc - context.TargetTile).DistSquared(), character);
            }
            int totalWarp = 0;
            for (int ii = 0; ii < Amount && targets.Count > 0; ii++)
            {
                Character target = targets.Dequeue();
                if (target.CharStates.Contains<AnchorState>())
                    yield break;

                yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.Instance.WarpNear(target, context.TargetTile, false));
                if (target.MemberTeam.MapFaction != Faction.Player)
                    target.TurnUsed = true;
                totalWarp++;
            }
            if (totalWarp == 0)
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_NOTHING_HAPPENED").ToLocal()));
            else
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_SUMMON_FOES").ToLocal(), context.User.GetDisplayName(false)));
        }
    }

    /// <summary>
    /// Event that causes the user to swap places with the target
    /// </summary>
    [Serializable]
    public class SwitcherEvent : BattleEvent
    {
        public SwitcherEvent() { }
        public override GameEvent Clone() { return new SwitcherEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.Target.CharStates.Contains<AnchorState>())
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_ANCHORED").ToLocal(), context.Target.GetDisplayName(false)));
            else
            {
                //switch the positions of the attacker and target

                CharAnimSwitch switch1Anim = new CharAnimSwitch();
                switch1Anim.FromLoc = context.User.CharLoc;
                switch1Anim.CharDir = context.User.CharDir;
                switch1Anim.ToLoc = context.Target.CharLoc;
                switch1Anim.MajorAnim = true;

                CharAnimSwitch switch2Anim = new CharAnimSwitch();
                switch2Anim.FromLoc = context.Target.CharLoc;
                switch2Anim.CharDir = context.Target.CharDir;
                switch2Anim.ToLoc = context.User.CharLoc;
                switch2Anim.MajorAnim = true;

                yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.Instance.SyncActions(context.User, switch1Anim, context.Target, switch2Anim));
            }
        }
    }

}

