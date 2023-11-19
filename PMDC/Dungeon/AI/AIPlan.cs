using System;
using System.Collections.Generic;
using RogueElements;
using RogueEssence.Data;
using RogueEssence;
using RogueEssence.Dungeon;
using System.Runtime.Serialization;

namespace PMDC.Dungeon
{
    [Flags]
    public enum AIFlags
    {
        None = 0,
        /// <summary>
        /// will not attack enemyoffriend
        /// </summary>
        TeamPartner = 1,
        /// <summary>
        /// will attack allies if given the opportunity
        /// </summary>
        Cannibal = 2,
        /// <summary>
        /// will pick up items
        /// </summary>
        ItemGrabber = 4,
        /// <summary>
        /// Knows how to use items
        /// </summary>
        ItemMaster = 8,
        /// <summary>
        /// Is aware of move-neutralizing abilities
        /// </summary>
        KnowsMatchups = 16,
        /// <summary>
        /// Uses moves to escape
        /// </summary>
        AttackToEscape = 32,
        /// <summary>
        /// will not attack sleepers/the frozen
        /// but will still wait on them to thaw out instead of walking away
        /// </summary>
        WontDisturb = 64,
        /// <summary>
        /// Avoids traps
        /// </summary>
        TrapAvoider = 128,
        /// <summary>
        /// Has the sensibilities of a player team's ally
        /// will not walk into silcoon/cascoon
        /// will not hit allies even if it's worth it to hit more foes
        /// will not path to the last seen location of an enemy if it finds no enemies
        /// will not attack or target certain AI
        /// will not attack or target sleepers and frozen, full stop
        /// </summary>
        PlayerSense = 256,
    }

    [Serializable]
    public abstract class AIPlan : BasePlan
    {
        /// <summary>
        /// The strategy that the monster takes when it goes aggro
        /// </summary>
        public AIFlags IQ;

        public int AttackRange;
        public int StatusRange;
        public int SelfStatusRange;


        [OnDeserialized]
        internal void OnDeserializedMethod(StreamingContext context)
        {
            if (Serializer.OldVersion < new Version(0, 7, 14))
            {
                SelfStatusRange = 4;
            }
        }

        public enum AttackChoice
        {
            /// <summary>
            /// Only chooses standard attack.
            /// </summary>
            StandardAttack,
            /// <summary>
            /// randomly chooses moves based on weight, sometimes walks within range due to missing moves having weight
            /// </summary>
            DumbAttack,
            /// <summary>
            /// randomly chooses moves based on weight, always attacks with damaging moves when within range, but sometimes moves forward if the only choice is a status move
            /// </summary>
            RandomAttack,
            /// <summary>
            /// randomly chooses a status move first and foremost
            /// </summary>
            StatusAttack,
            /// <summary>
            /// always chooses the best move, and always attacks when within range
            /// </summary>
            SmartAttack,
        }

        public enum PositionChoice
        {
            /// <summary>
            /// move in even if it's out of range of moves
            /// </summary>
            Approach,
            /// <summary>
            /// move in as close as possible within range of moves
            /// </summary>
            Close,
            /// <summary>
            /// move as far as possible within range
            /// </summary>
            Avoid,
        }

        public AIPlan() { }

        public AIPlan(AIFlags iq)
        {
            this.IQ = iq;
        }

        public AIPlan(AIFlags iq, int attackRange, int statusRange, int selfStatusRange)
        {
            this.IQ = iq;
            this.AttackRange = attackRange;
            this.StatusRange = statusRange;
            this.SelfStatusRange = selfStatusRange;
        }

        protected AIPlan(AIPlan other)
        {
            IQ = other.IQ;
            SelfStatusRange = other.SelfStatusRange;
            StatusRange = other.StatusRange;
            AttackRange = other.AttackRange;
        }

        /// <summary>
        /// Checks if the controlled character can push over the target character, based on ordering and matchup
        /// </summary>
        /// <param name="controlledChar"></param>
        /// <param name="destChar"></param>
        /// <param name="yieldToTeam">Considers other team members non-passable, even if they are lower rank.</param>
        /// <returns></returns>
        protected bool canPassChar(Character controlledChar, Character destChar, bool yieldToTeam)
        {
            if (destChar == null)
                return true;

            if (!controlledChar.CanSeeCharacter(destChar))
                return true;

            if (DungeonScene.Instance.GetMatchup(controlledChar, destChar) == Alignment.Foe)
                return false;

            if (destChar.MemberTeam.MapFaction < controlledChar.MemberTeam.MapFaction)
                return false;
            else if (destChar.MemberTeam.MapFaction == controlledChar.MemberTeam.MapFaction)
            {
                //always yield to the higher rank of team or member within team
                CharIndex destIndex = ZoneManager.Instance.CurrentMap.GetCharIndex(destChar);
                CharIndex controlledIndex = ZoneManager.Instance.CurrentMap.GetCharIndex(controlledChar);
                if (destIndex.Team < controlledIndex.Team)
                    return false;
                else if (destIndex.Team == controlledIndex.Team)
                {
                    if (yieldToTeam)
                        return false;

                    Team team = destChar.MemberTeam;
                    if (destChar == team.Leader)
                        return false;
                    else if (destIndex < controlledIndex)
                        return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Is it sensible for a player character to attack this way?
        /// </summary>
        /// <param name="seenChar"></param>
        /// <returns></returns>
        protected bool playerSensibleToAttack(Character seenChar)
        {
            StatusEffect freezeStatus = seenChar.GetStatusEffect("freeze");
            if (seenChar.GetStatusEffect("freeze") != null)//if they're frozen, and have one turn or less; NOTE: specialized AI code!
            {
                CountDownState countdown = freezeStatus.StatusStates.GetWithDefault<CountDownState>();
                if (countdown.Counter < 0 || countdown.Counter > 1)
                    return false;
            }

            StatusEffect sleepStatus = seenChar.GetStatusEffect("sleep");
            if (sleepStatus != null)//if they're asleep and have one turn or less; NOTE: specialized AI code!
            {
                CountDownState countdown = sleepStatus.StatusStates.GetWithDefault<CountDownState>();
                if (countdown.Counter < 0 || countdown.Counter > 1)
                    return false;
            }

            if (seenChar.GetStatusEffect("last_targeted_by") == null)//last targeted by someone; NOTE: specialized AI code!
            {
                //don't attack certain kinds of foes that won't attack first
                if (seenChar.Tactic.ID == "weird_tree")//weird tree; NOTE: specialized AI code!
                    return false;
                if (seenChar.Tactic.ID == "moon_dance")//moon dance; NOTE: specialized AI code!
                    return false;
                else if (seenChar.Tactic.ID == "wait_attack")//wait attack; NOTE: specialized AI code!
                    return false;
            }
            
            if (seenChar.GetStatusEffect("was_hurt_last_turn") == null)
            {
                if (seenChar.Tactic.ID == "tit_for_tat")//tit for tat; NOTE: specialized AI code!
                    return false;
            }
            return true;
        }

        protected bool BlockedByChar(Character controlledChar, Loc testLoc, Alignment alignment)
        {
            Character character = ZoneManager.Instance.CurrentMap.GetCharAtLoc(testLoc);
            if (character != null)
            {
                if (!character.Dead && (DungeonScene.Instance.GetMatchup(controlledChar, character) & alignment) != Alignment.None)
                    return true;
            }
            return false;
        }

        protected bool BlockedByTrap(Character controlledChar, Loc testLoc)
        {
            if ((IQ & AIFlags.TrapAvoider) == AIFlags.None)
                return false;

            Tile tile = ZoneManager.Instance.CurrentMap.GetTile(testLoc);
            if (!String.IsNullOrEmpty(tile.Effect.ID))
            {
                if (!tile.Effect.Revealed)
                    return true;
                TileData entry = DataManager.Instance.GetTile(tile.Effect.ID);
                if (entry.StepType == TileData.TriggerType.Trap || entry.StepType == TileData.TriggerType.Site || entry.StepType == TileData.TriggerType.Switch)
                {
                    if (tile.Effect.Owner != ZoneManager.Instance.CurrentMap.GetTileOwner(controlledChar))
                        return true;
                }
            }

            if (tile.Data.ID == "lava" && !controlledChar.HasElement("fire"))//check for lava; NOTE: specialized AI code!
                return true;
            if (tile.Data.ID == "water_poison" && !controlledChar.HasElement("poison") && !controlledChar.HasElement("steel"))//check for poison; NOTE: specialized AI code!
                return true;
            if (tile.Data.ID == DataManager.Instance.GenWall && controlledChar.MemberTeam is ExplorerTeam)//check for block; NOTE: specialized AI code!
            {
                //we can still walk on it if we're already in a block
                Tile curTile = ZoneManager.Instance.CurrentMap.GetTile(controlledChar.CharLoc);
                if (curTile.Data.ID != DataManager.Instance.GenWall)
                    return true;
            }

            return false;
        }

        protected bool BlockedByHazard(Character controlledChar, Loc testLoc)
        {
            if ((IQ & AIFlags.PlayerSense) == AIFlags.None)
                return false;
            //TODO: pass in the list of seen characters instead of computing them on the spot
            //this is very slow and expensive to do, and can lead to performance bottlenecks
            //and the only reason the game isn't lagging is because this check is only called for ally characters!
            List<Character> seenChars = controlledChar.GetSeenCharacters(Alignment.Foe);
            foreach (Character seenChar in seenChars)
            {
                if (seenChar.Tactic.ID == "wait_attack" && ZoneManager.Instance.CurrentMap.InRange(seenChar.CharLoc, testLoc, 1) && seenChar.GetStatusEffect("last_targeted_by") == null)//do not approach silcoon/cascoon; NOTE: specialized AI code!
                    return true;
            }
            return false;
        }

        protected Alignment GetAcceptableTargetAlignments()
        {
            Alignment target = Alignment.Foe;
            if ((IQ & AIFlags.Cannibal) != AIFlags.None)
                target |= Alignment.Friend;
            return target;
        }

        private bool isIllusionStealth(Character controlledChar, Character seenChar)
        {
            //illusion check; NOTE: specialized AI code!
            StatusEffect illusionEffect = seenChar.GetStatusEffect("illusion");
            if (illusionEffect != null)
            {
                StatusEffect previouslyAttackedEffect = controlledChar.GetStatusEffect("last_targeted_by");//only be passive if not previously attacked
                if (previouslyAttackedEffect == null)
                {
                    MonsterIDState illusionMon = illusionEffect.StatusStates.GetWithDefault<MonsterIDState>();
                    if (illusionMon.MonID.Species == controlledChar.CurrentForm.Species)
                        return true;
                }
            }
            return false;
        }

        protected IEnumerable<Character> GetAcceptableTargets(Character controlledChar)
        {
            List<Character> seenCharacters = controlledChar.GetSeenCharacters(GetAcceptableTargetAlignments());

            bool playerSense = (IQ & AIFlags.PlayerSense) != AIFlags.None;
            bool teamPartner = (IQ & AIFlags.TeamPartner) != AIFlags.None;
            foreach (Character seenChar in seenCharacters)
            {
                if (playerSense && !playerSensibleToAttack(seenChar))
                    continue;
                if (!teamPartner)
                {
                    if (isIllusionStealth(controlledChar, seenChar))
                        continue;
                }
                yield return seenChar;
            }
        }

        private Loc[] getWrappedEnds(Loc mapStart, Loc mapSize, Loc start, Loc[] ends)
        {
            Rect mapRect = new Rect(mapStart, mapSize);

            Loc[] wrappedEnds = new Loc[ends.Length];
            for (int ii = 0; ii < ends.Length; ii++)
            {
                bool first = true;
                foreach (Loc wrapLoc in ZoneManager.Instance.CurrentMap.IterateLocInBounds(mapRect, ends[ii]))
                {
                    if (first)
                    {
                        wrappedEnds[ii] = wrapLoc;
                        first = false;
                    }
                    else if ((start - wrapLoc).Dist8() < (start - wrappedEnds[ii]).Dist8())
                        wrappedEnds[ii] = wrapLoc;
                }
            }
            return wrappedEnds;
        }

        /// <summary>
        /// Gets the path directly to a target
        /// </summary>
        /// <param name="controlledChar"></param>
        /// <param name="ends"></param>
        /// <param name="freeGoal">Determines whether the goal should be reachable even if blocked.</param>
        /// <param name="respectPeers">Considers entities as blockers</param>
        /// <returns></returns>
        protected List<Loc>[] GetPaths(Character controlledChar, Loc[] ends, bool freeGoal, bool respectPeers, int limit = 1)
        {
            Loc mapStart = controlledChar.CharLoc - Character.GetSightDims();
            Loc mapSize = Character.GetSightDims() * 2 + new Loc(1);
            Loc[] wrappedEnds = getWrappedEnds(mapStart, mapSize, controlledChar.CharLoc, ends);

            //requires a valid target tile
            Grid.LocTest checkDiagBlock = (Loc loc) => {
                return (ZoneManager.Instance.CurrentMap.TileBlocked(loc, controlledChar.Mobility, true));
                //enemy/ally blockings don't matter for diagonals
            };

            Grid.LocTest checkBlock = (Loc testLoc) => {

                if (freeGoal)
                {
                    foreach (Loc end in wrappedEnds)
                    {
                        if (testLoc == end)
                            return false;
                    }
                }

                if (ZoneManager.Instance.CurrentMap.TileBlocked(testLoc, controlledChar.Mobility))
                    return true;

                if (BlockedByTrap(controlledChar, testLoc))
                    return true;
                if (BlockedByHazard(controlledChar, testLoc))
                    return true;

                if (respectPeers && BlockedByChar(controlledChar, testLoc, Alignment.Friend | Alignment.Foe))
                    return true;

                return false;
            };


            return Grid.FindNPaths(mapStart, mapSize, controlledChar.CharLoc, wrappedEnds, checkBlock, checkDiagBlock, limit, true);
        }

        protected List<Loc> GetRandomPathPermissive(IRandom rand, Character controlledChar, List<Loc> seenExits)
        {
            List<Loc>[] paths = GetPathsPermissive(controlledChar, seenExits);
            List<int> idx_list = new List<int>();
            for (int ii = 0; ii < paths.Length; ii++)
                idx_list.Add(ii);
            while (idx_list.Count > 0)
            {
                int list_idx = rand.Next(idx_list.Count);
                int idx = idx_list[list_idx];
                //check to make sure the path reaches the end
                if (paths[idx][0] == seenExits[idx])
                    return paths[idx];
                idx_list.RemoveAt(list_idx);
            }
            return new List<Loc>();
        }

        protected List<Loc>[] GetPathsPermissive(Character controlledChar, List<Loc> ends)
        {
            Loc mapStart = controlledChar.CharLoc - Character.GetSightDims();
            Loc mapSize = Character.GetSightDims() * 2 + new Loc(1);
            Loc[] wrappedEnds = getWrappedEnds(mapStart, mapSize, controlledChar.CharLoc, ends.ToArray());

            //requires a valid target tile
            Grid.LocTest checkDiagBlock = (Loc testLoc) => {
                return (ZoneManager.Instance.CurrentMap.TileBlocked(testLoc, controlledChar.Mobility, true));
                //enemy/ally blockings don't matter for diagonals
            };

            Grid.LocTest checkBlock = (Loc testLoc) => {

                foreach (Loc end in wrappedEnds)
                {
                    if (testLoc == end)
                        return false;
                }

                if (ZoneManager.Instance.CurrentMap.TileBlocked(testLoc, controlledChar.Mobility))
                    return true;

                if (BlockedByTrap(controlledChar, testLoc))
                    return true;
                if (BlockedByHazard(controlledChar, testLoc))
                    return true;

                if (BlockedByChar(controlledChar, testLoc, Alignment.Foe))
                    return true;

                return false;
            };

            return Grid.FindAllPaths(mapStart, mapSize, controlledChar.CharLoc, wrappedEnds, checkBlock, checkDiagBlock);
        }


        /// <summary>
        /// Gets all paths to all targets, only considering impassable blocks as blockers.
        /// </summary>
        /// <param name="controlledChar"></param>
        /// <param name="ends"></param>
        /// <returns></returns>
        protected List<Loc>[] GetPathsImpassable(Character controlledChar, List<Loc> ends)
        {
            Loc mapStart = controlledChar.CharLoc - Character.GetSightDims();
            Loc mapSize = Character.GetSightDims() * 2 + new Loc(1);
            Loc[] wrappedEnds = getWrappedEnds(mapStart, mapSize, controlledChar.CharLoc, ends.ToArray());

            //requires a valid target tile
            Grid.LocTest checkDiagBlock = (Loc testLoc) => {
                return (ZoneManager.Instance.CurrentMap.TileBlocked(testLoc, TerrainData.Mobility.All, true));
                //enemy/ally blockings don't matter for diagonals
            };

            Grid.LocTest checkBlock = (Loc testLoc) => {

                foreach (Loc end in wrappedEnds)
                {
                    if (testLoc == end)
                        return false;
                }

                if (ZoneManager.Instance.CurrentMap.TileBlocked(testLoc, TerrainData.Mobility.All))
                    return true;

                return false;
            };

            return Grid.FindNPaths(mapStart, Character.GetSightDims() * 2 + new Loc(1), controlledChar.CharLoc, wrappedEnds, checkBlock, checkDiagBlock, 1, false);
        }

        protected GameAction SelectChoiceFromPath(Character controlledChar, List<Loc> path)
        {
            if (path.Count <= 1)
                return new GameAction(GameAction.ActionType.Wait, Dir8.None);
            else
                return TrySelectWalk(controlledChar, ZoneManager.Instance.CurrentMap.GetClosestDir8(path[path.Count - 1], path[path.Count - 2]));
        }

        protected GameAction TrySelectWalk(Character controlledChar, Dir8 dir)
        {
            //assumes that this direction was checked for blocking against VISIBLE enemies, and no-walking
            Character invisibleChar = ZoneManager.Instance.CurrentMap.GetCharAtLoc(controlledChar.CharLoc + dir.GetLoc());
            if (invisibleChar != null && DungeonScene.Instance.GetMatchup(controlledChar, invisibleChar, false) == Alignment.Foe)
            {
                if (isIllusionStealth(controlledChar, invisibleChar))
                    return new GameAction(GameAction.ActionType.Move, dir, ((IQ & AIFlags.ItemGrabber) != AIFlags.None) ? 1 : 0);

                return new GameAction(GameAction.ActionType.Attack, dir);
            }
            else
            {
                if (controlledChar.Fullness <= 0)
                {
                    if ((IQ & AIFlags.PlayerSense) == AIFlags.None)
                        return new GameAction(GameAction.ActionType.Wait, Dir8.None);
                }
                return new GameAction(GameAction.ActionType.Move, dir, ((IQ & AIFlags.ItemGrabber) != AIFlags.None) ? 1 : 0);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="rand"></param>
        /// <param name="controlledChar"></param>
        /// <param name="attackPattern"></param>
        /// <param name="includeImagine">Excludes imaginary hits from causing attack fallthrough.  This will also skip threat checking.</param>
        /// <returns></returns>
        protected GameAction TryAttackChoice(IRandom rand, Character controlledChar, AttackChoice attackPattern, bool excludeImagine = false)
        {
            List<Character> seenChars = controlledChar.GetSeenCharacters(Alignment.Self | Alignment.Friend | Alignment.Foe);

            bool playerSense = (IQ & AIFlags.PlayerSense) != AIFlags.None;
            Character closestThreat = null;
            bool seesDanger = false;
            if (!excludeImagine)
            {
                int closestDiff = Int32.MaxValue;
                List<Character> threats = new List<Character>();
                foreach (Character seenChar in seenChars)
                {
                    bool canBetray = (IQ & AIFlags.TeamPartner) == AIFlags.None;
                    if ((DungeonScene.Instance.GetMatchup(controlledChar, seenChar, canBetray) & GetAcceptableTargetAlignments()) != Alignment.None)
                    {
                        //just for attacking, we check to see if we can see the controlledchar's current location from the target's location
                        //this is a hack to prevent unfair-feeling surprise attacks brought about by the non-symmetrical FOV
                        //all while still maintaining the better aesthetic of of that FOV
                        //If the FOV were ever to be made symmetric, this check will not be needed.
                        //additionally, we only do this for NPC AI, not ally AI
                        if (playerSense || controlledChar.CanSeeLocFromLoc(seenChar.CharLoc, controlledChar.CharLoc, controlledChar.GetCharSight()))
                        {
                            threats.Add(seenChar);
                        }
                    }
                }

                List<Loc> threatEnds = new List<Loc>();
                foreach (Character chara in threats)
                    threatEnds.Add(chara.CharLoc);
                List<Loc>[] threatPaths = GetPathsImpassable(controlledChar, threatEnds);
                for (int ii = 0; ii < threatPaths.Length; ii++)
                {
                    if (threatPaths[ii] != null && ZoneManager.Instance.CurrentMap.WrapLoc(threatPaths[ii][0]) == threats[ii].CharLoc)
                    {
                        seesDanger = true;

                        if (threatPaths[ii].Count < closestDiff)
                        {
                            closestDiff = threatPaths[ii].Count;
                            closestThreat = threats[ii];
                        }
                    }
                }

                if (!seesDanger)
                    return new GameAction(GameAction.ActionType.Wait, Dir8.None);
            }


            {
                StatusEffect sleepStatus = controlledChar.GetStatusEffect("sleep"); //NOTE: specialized AI code!
                if (sleepStatus != null)
                    return TrySleepAttackChoice(rand, controlledChar, seenChars, closestThreat);
            }

            {
                StatusEffect freezeStatus = controlledChar.GetStatusEffect("freeze"); //NOTE: specialized AI code!
                if (freezeStatus != null)
                    return TryFreezeAttackChoice(rand, controlledChar, seenChars, closestThreat);
            }

            if (controlledChar.CantInteract)//TODO: CantInteract doesn't always indicate forced attack, but this'll do for now.
                return TryForcedAttackChoice(rand, controlledChar, seenChars, closestThreat);

            if (!playerSense)
            {
                //for dumb NPCs, if they have a status where they can't attack, treat it as a regular attack pattern so that they walk up to the player
                //only cringe does this right now...
                StatusEffect flinchStatus = controlledChar.GetStatusEffect("flinch"); //NOTE: specialized AI code!
                if (flinchStatus != null)
                    attackPattern = AttackChoice.StandardAttack;

                if (controlledChar.Fullness <= 0)
                    attackPattern = AttackChoice.StandardAttack;
            }

            if (attackPattern == AttackChoice.StandardAttack)
                return TryDefaultAttackChoice(rand, controlledChar, seenChars, closestThreat);
            else if (attackPattern == AttackChoice.DumbAttack)
                return TryDumbAttackChoice(rand, controlledChar, seenChars, closestThreat);
            else if (attackPattern == AttackChoice.RandomAttack)
                return TryRandomMoveChoice(rand, controlledChar, seenChars, closestThreat);
            else if (attackPattern == AttackChoice.StatusAttack)
                return TryStatusMoveChoice(rand, controlledChar, seenChars, closestThreat);
            else
                return TryBestAttackChoice(rand, controlledChar, seenChars, closestThreat);
        }

        private IEnumerable<int> iterateUsableSkillIndices(Character controlledChar)
        {
            for (int ii = 0; ii < controlledChar.Skills.Count; ii++)
            {
                if (IsSkillUsable(controlledChar, ii))
                    yield return ii;
            }
        }

        public bool IsSkillUsable(Character controlledChar, int ii)
        {
            if (!String.IsNullOrEmpty(controlledChar.Skills[ii].Element.SkillNum) && controlledChar.Skills[ii].Element.Charges > 0 && !controlledChar.Skills[ii].Element.Sealed && controlledChar.Skills[ii].Element.Enabled)
                return true;
            return false;
        }

        private void updateDistanceTargetHash(Character controlledChar, Dictionary<Loc, RangeTarget> endHash, Character chara, Loc diff)
        {
            Loc loc = chara.CharLoc + diff;
            if (!controlledChar.CanSeeLocFromLoc(loc, chara.CharLoc, controlledChar.GetCharSight()))
                return;

            //due to all end locations being relative when passed in, this should be consistent with wrapped maps as well.
            int weight = diff.Dist8();
            if (endHash.ContainsKey(loc))
            {
                if (weight < endHash[loc].Weight)
                    endHash[loc] = new RangeTarget(chara, weight);
            }
            else
                endHash[loc] = new RangeTarget(chara, weight);
        }

        private ActionDirValue weightedActionChoice(List<ActionDirValue> moveIndices, IRandom rand)
        {
            //get a random move based on weighted chance
            int totalPoints = 0;
            for (int ii = 0; ii < moveIndices.Count; ii++)
                totalPoints += moveIndices[ii].Hit.Value;

            int pointChoice = rand.Next(totalPoints);
            int choice = 0;
            while (choice < moveIndices.Count)
            {
                if (pointChoice < moveIndices[choice].Hit.Value)
                    break;
                pointChoice -= moveIndices[choice].Hit.Value;
                choice++;
            }

            return moveIndices[choice];
        }

        protected GameAction TryDefaultAttackChoice(IRandom rand, Character controlledChar, List<Character> seenChars, Character closestThreat)
        {
            List<ActionDirValue> backupIndices = new List<ActionDirValue>();
            //default on attacking if no moves are to be found
            {
                HitValue[] attackDirs = new HitValue[8];
                GetActionValues(controlledChar, seenChars, closestThreat, DataManager.Instance.DefaultSkill, attackDirs, false);
                UpdateTotalIndices(rand, backupIndices, new GameAction(GameAction.ActionType.Attack, Dir8.None), attackDirs);
            }

            //if that attempt failed, because we hit a move that would hit no one, then we default to attempting attack
            if (backupIndices.Count > 0)
            {
                ActionDirValue actionVal = backupIndices[rand.Next(backupIndices.Count)];
                return actionVal.Action;
            }
            //if we can't attack, then we pass along to movement
            return new GameAction(GameAction.ActionType.Wait, Dir8.None);
        }


        protected GameAction TryDumbAttackChoice(IRandom rand, Character controlledChar, List<Character> seenChars, Character closestThreat)
        {
            List<ActionDirValue> moveIndices = new List<ActionDirValue>();
            foreach (int ii in iterateUsableSkillIndices(controlledChar))
            {
                HitValue[] moveDirs = new HitValue[8];
                GetActionValues(controlledChar, seenChars, closestThreat, controlledChar.Skills[ii].Element.SkillNum, moveDirs, true);
                UpdateTotalIndices(rand, moveIndices, new GameAction(GameAction.ActionType.UseSkill, Dir8.None, ii), moveDirs);
            }

            if (!String.IsNullOrEmpty(controlledChar.EquippedItem.ID) && (IQ & AIFlags.ItemMaster) != AIFlags.None)
            {
                //check item use
                {
                    HitValue[] moveDirs = new HitValue[8];
                    GetItemUseValues(controlledChar, seenChars, closestThreat, controlledChar.EquippedItem.ID, moveDirs);
                    UpdateTotalIndices(rand, moveIndices, new GameAction(GameAction.ActionType.UseItem, Dir8.None, BattleContext.EQUIP_ITEM_SLOT, -1), moveDirs);
                }

                //check item throw
                {
                    HitValue[] moveDirs = new HitValue[8];
                    GetItemThrowValues(controlledChar, seenChars, closestThreat, controlledChar.EquippedItem.ID, moveDirs);
                    UpdateTotalIndices(rand, moveIndices, new GameAction(GameAction.ActionType.Throw, Dir8.None, BattleContext.EQUIP_ITEM_SLOT), moveDirs);
                }
            }

            //default on attacking if no moves are to be found
            {
                HitValue[] attackDirs = new HitValue[8];
                GetActionValues(controlledChar, seenChars, closestThreat, DataManager.Instance.DefaultSkill, attackDirs, true);
                for (int ii = 0; ii < attackDirs.Length; ii++)
                    attackDirs[ii].Value = attackDirs[ii].Value * 2;
                UpdateTotalIndices(rand, moveIndices, new GameAction(GameAction.ActionType.Attack, Dir8.None), attackDirs);
            }

            //just try to choose once
            if (moveIndices.Count > 0)
            {
                ActionDirValue actionVal = weightedActionChoice(moveIndices, rand);
                if (!actionVal.Hit.ImaginedHit)
                    return actionVal.Action;

                //if we chose an imagined hit, we fall through and skip choosing a move altogether
                //this is equivalent of choosing a move that is out of range and thus doing nothing
            }

            //if we can't attack, then we pass along to movement
            return new GameAction(GameAction.ActionType.Wait, Dir8.None);
        }


        protected GameAction TryStatusMoveChoice(IRandom rand, Character controlledChar, List<Character> seenChars, Character closestThreat)
        {
            //first pass list of move candidates containing all status moves
            List<ActionDirValue> moveIndices = new List<ActionDirValue>();
            //the result is that we choose moves like dumb attack, but if we choose a hypothetical we roll again from attack moves assured to work
            foreach (int ii in iterateUsableSkillIndices(controlledChar))
            {
                SkillData entry = DataManager.Instance.GetSkill(controlledChar.Skills[ii].Element.SkillNum);
                if (entry.Data.Category == BattleData.SkillCategory.Status)
                {
                    HitValue[] moveDirs = new HitValue[8];
                    GetActionValues(controlledChar, seenChars, closestThreat, controlledChar.Skills[ii].Element.SkillNum, moveDirs, true);
                    UpdateTotalIndices(rand, moveIndices, new GameAction(GameAction.ActionType.UseSkill, Dir8.None, ii), moveDirs);
                }
            }

            if (moveIndices.Count > 0)
            {
                ActionDirValue actionVal = weightedActionChoice(moveIndices, rand);
                if (!actionVal.Hit.ImaginedHit)
                    return actionVal.Action;
            }

            //if we can't attack, then we pass along to movement
            return new GameAction(GameAction.ActionType.Wait, Dir8.None);
        }

        protected GameAction TryRandomMoveChoice(IRandom rand, Character controlledChar, List<Character> seenChars, Character closestThreat)
        {
            //first pass list of move candidates containing all moves, including hypothetical
            List<ActionDirValue> moveIndices = new List<ActionDirValue>();
            //backup list containing only attacking moves, none hypothetical
            List<ActionDirValue> attackIndices = new List<ActionDirValue>();
            //the result is that we choose moves like dumb attack, but if we choose a hypothetical we roll again from attack moves assured to work
            foreach (int ii in iterateUsableSkillIndices(controlledChar))
            {
                HitValue[] moveDirs = new HitValue[8];
                GetActionValues(controlledChar, seenChars, closestThreat, controlledChar.Skills[ii].Element.SkillNum, moveDirs, true);
                UpdateTotalIndices(rand, moveIndices, new GameAction(GameAction.ActionType.UseSkill, Dir8.None, ii), moveDirs);

                SkillData entry = DataManager.Instance.GetSkill(controlledChar.Skills[ii].Element.SkillNum);
                if (entry.Data.Category != BattleData.SkillCategory.Status)
                {
                    for (int jj = 0; jj < moveDirs.Length; jj++)
                    {
                        if (moveDirs[jj].ImaginedHit)
                            moveDirs[jj] = new HitValue();
                    }
                    UpdateTotalIndices(rand, attackIndices, new GameAction(GameAction.ActionType.UseSkill, Dir8.None, ii), moveDirs);
                }
            }

            if (!String.IsNullOrEmpty(controlledChar.EquippedItem.ID) && (IQ & AIFlags.ItemMaster) != AIFlags.None)
            {
                //check item use
                {
                    HitValue[] moveDirs = new HitValue[8];
                    GetItemUseValues(controlledChar, seenChars, closestThreat, controlledChar.EquippedItem.ID, moveDirs);
                    UpdateTotalIndices(rand, moveIndices, new GameAction(GameAction.ActionType.UseItem, Dir8.None, BattleContext.EQUIP_ITEM_SLOT, -1), moveDirs);
                }

                //check item throw
                {
                    HitValue[] moveDirs = new HitValue[8];
                    GetItemThrowValues(controlledChar, seenChars, closestThreat, controlledChar.EquippedItem.ID, moveDirs);
                    UpdateTotalIndices(rand, moveIndices, new GameAction(GameAction.ActionType.Throw, Dir8.None, BattleContext.EQUIP_ITEM_SLOT), moveDirs);
                }
            }

            if (moveIndices.Count > 0)
            {
                ActionDirValue actionVal = weightedActionChoice(moveIndices, rand);
                if (!actionVal.Hit.ImaginedHit)
                    return actionVal.Action;

                //if we chose an imagined hit, we fall through and skip choosing a move altogether
                //this is equivalent of choosing a move that is out of range and thus doing nothing
            }

            // if we fell to here, we likely chose an imagined hit.  Fall back to an attacking move.
            if (attackIndices.Count > 0)
            {
                //get a random move based on weighted chance
                ActionDirValue actionVal = weightedActionChoice(attackIndices, rand);
                return actionVal.Action;
            }

            //if we can't attack, then we pass along to movement
            return new GameAction(GameAction.ActionType.Wait, Dir8.None);
        }

        /// <summary>
        /// Always chooses the best attack
        /// </summary>
        /// <param name="rand"></param>
        /// <param name="controlledChar"></param>
        /// <param name="defaultDir"></param>
        /// <param name="seenChars"></param>
        /// <returns></returns>
        protected GameAction TryBestAttackChoice(IRandom rand, Character controlledChar, List<Character> seenChars, Character closestThreat)
        {
            List<ActionDirValue> highestIndices = new List<ActionDirValue>();
            List<ActionDirValue> highestStatusIndices = new List<ActionDirValue>();
            foreach (int ii in iterateUsableSkillIndices(controlledChar))
            {
                HitValue[] moveDirs = new HitValue[8];
                GetActionValues(controlledChar, seenChars, closestThreat, controlledChar.Skills[ii].Element.SkillNum, moveDirs, false);

                SkillData entry = DataManager.Instance.GetSkill(controlledChar.Skills[ii].Element.SkillNum);
                if (entry.Data.Category == BattleData.SkillCategory.Status)
                    UpdateHighestIndices(highestStatusIndices, new GameAction(GameAction.ActionType.UseSkill, Dir8.None, ii), moveDirs);
                else
                    UpdateHighestIndices(highestIndices, new GameAction(GameAction.ActionType.UseSkill, Dir8.None, ii), moveDirs);
            }

            if (!String.IsNullOrEmpty(controlledChar.EquippedItem.ID) && (IQ & AIFlags.ItemMaster) != AIFlags.None)
            {
                //check item use
                {
                    HitValue[] moveDirs = new HitValue[8];
                    GetItemUseValues(controlledChar, seenChars, closestThreat, controlledChar.EquippedItem.ID, moveDirs);
                    UpdateHighestIndices(highestIndices, new GameAction(GameAction.ActionType.UseItem, Dir8.None, BattleContext.EQUIP_ITEM_SLOT, -1), moveDirs);
                }

                //check item throw
                {
                    HitValue[] moveDirs = new HitValue[8];
                    GetItemThrowValues(controlledChar, seenChars, closestThreat, controlledChar.EquippedItem.ID, moveDirs);
                    UpdateHighestIndices(highestIndices, new GameAction(GameAction.ActionType.Throw, Dir8.None, BattleContext.EQUIP_ITEM_SLOT), moveDirs);
                }
            }

            if (highestIndices.Count > 0 || highestStatusIndices.Count > 0)
            {
                int randIndex = rand.Next(highestIndices.Count + highestStatusIndices.Count);
                ActionDirValue actionVal = (randIndex < highestIndices.Count) ? highestIndices[randIndex] : highestStatusIndices[randIndex - highestIndices.Count];
                return actionVal.Action;
            }

            return new GameAction(GameAction.ActionType.Wait, Dir8.None);
        }

        protected GameAction TryForcedAttackChoice(IRandom rand, Character controlledChar, List<Character> seenChars, Character closestThreat)
        {
            //check to see if currently under a force-move status
            //if so, aim a regular attack according to that move
            string forcedMove = "";
            foreach (string status in controlledChar.StatusEffects.Keys)
            {
                StatusData entry = DataManager.Instance.GetStatus(status);
                foreach (BattleEvent effect in entry.BeforeTryActions.EnumerateInOrder())
                {
                    if (effect is ForceMoveEvent)
                    {
                        forcedMove = ((ForceMoveEvent)effect).MoveIndex;
                        break;
                    }
                }
                if (!String.IsNullOrEmpty(forcedMove))
                    break;
            }


            List<ActionDirValue> highestIndices = new List<ActionDirValue>();

            if (String.IsNullOrEmpty(forcedMove)) // default to regular attack if no moves are to be found
                forcedMove = DataManager.Instance.DefaultSkill;

            HitValue[] moveDirs = new HitValue[8];
            GetActionValues(controlledChar, seenChars, closestThreat, forcedMove, moveDirs, false);
            UpdateHighestIndices(highestIndices, new GameAction(GameAction.ActionType.Attack, Dir8.None), moveDirs);

            if (highestIndices.Count > 0)
            {
                ActionDirValue actionVal = highestIndices[rand.Next(highestIndices.Count)];
                return actionVal.Action;
            }

            if (controlledChar.CantWalk)
                return new GameAction(GameAction.ActionType.Attack, Dir8.None);
            else
                return new GameAction(GameAction.ActionType.Wait, Dir8.None);
        }

        //TODO: refactor this better...
        protected GameAction TrySleepAttackChoice(IRandom rand, Character controlledChar, List<Character> seenChars, Character closestThreat)
        {
            List<ActionDirValue> highestIndices = new List<ActionDirValue>();

            foreach (int ii in iterateUsableSkillIndices(controlledChar))
            {
                string skillId = controlledChar.Skills[ii].Element.SkillNum;
                if (skillId == "sleep_talk" || skillId == "snore") //just actively except the two moves that can be executed asleep; NOTE: specialized AI code!
                {
                    HitValue[] moveDirs = new HitValue[8];
                    GetActionValues(controlledChar, seenChars, closestThreat, controlledChar.Skills[ii].Element.SkillNum, moveDirs, false);
                    UpdateTotalIndices(rand, highestIndices, new GameAction(GameAction.ActionType.UseSkill, Dir8.None, ii), moveDirs);
                }
            }

            if (!String.IsNullOrEmpty(controlledChar.EquippedItem.ID) && (IQ & AIFlags.ItemMaster) != AIFlags.None)
            {
                //check item use
                if (controlledChar.EquippedItem.ID == "berry_lum")//just actively except the one item that can be executed asleep; NOTE: specialized AI code!
                {
                    HitValue[] moveDirs = new HitValue[8];
                    GetItemUseValues(controlledChar, seenChars, closestThreat, controlledChar.EquippedItem.ID, moveDirs);
                    UpdateTotalIndices(rand, highestIndices, new GameAction(GameAction.ActionType.UseItem, Dir8.None, BattleContext.EQUIP_ITEM_SLOT, -1), moveDirs);
                }
            }

            {
                HitValue[] moveDirs = new HitValue[8];
                GetActionValues(controlledChar, seenChars, closestThreat, DataManager.Instance.DefaultSkill, moveDirs, false);
                UpdateHighestIndices(highestIndices, new GameAction(GameAction.ActionType.Attack, Dir8.None), moveDirs);
            }

            if (highestIndices.Count > 0)
            {
                ActionDirValue actionVal = highestIndices[rand.Next(highestIndices.Count)];
                return actionVal.Action;
            }

            return new GameAction(GameAction.ActionType.Attack, Dir8.None);
        }

        protected GameAction TryFreezeAttackChoice(IRandom rand, Character controlledChar, List<Character> seenChars, Character closestThreat)
        {
            List<ActionDirValue> highestIndices = new List<ActionDirValue>();

            foreach (int ii in iterateUsableSkillIndices(controlledChar))
            {
                SkillData entry = DataManager.Instance.GetSkill(controlledChar.Skills[ii].Element.SkillNum);
                if (entry.Data.Element == "fire") //just actively except the two moves that can be executed frozen; NOTE: specialized AI code!
                {
                    HitValue[] moveDirs = new HitValue[8];
                    GetActionValues(controlledChar, seenChars, closestThreat, controlledChar.Skills[ii].Element.SkillNum, moveDirs, false);
                    UpdateTotalIndices(rand, highestIndices, new GameAction(GameAction.ActionType.UseSkill, Dir8.None, ii), moveDirs);
                }
            }

            if (!String.IsNullOrEmpty(controlledChar.EquippedItem.ID) && (IQ & AIFlags.ItemMaster) != AIFlags.None)
            {
                //check item use
                if (controlledChar.EquippedItem.ID == "berry_lum")//just actively except the one item that can be executed asleep; NOTE: specialized AI code!
                {
                    HitValue[] moveDirs = new HitValue[8];
                    GetItemUseValues(controlledChar, seenChars, closestThreat, controlledChar.EquippedItem.ID, moveDirs);
                    UpdateTotalIndices(rand, highestIndices, new GameAction(GameAction.ActionType.UseItem, Dir8.None, BattleContext.EQUIP_ITEM_SLOT, -1), moveDirs);
                }
            }

            {
                HitValue[] moveDirs = new HitValue[8];
                GetActionValues(controlledChar, seenChars, closestThreat, DataManager.Instance.DefaultSkill, moveDirs, false);
                UpdateHighestIndices(highestIndices, new GameAction(GameAction.ActionType.Attack, Dir8.None), moveDirs);
            }

            if (highestIndices.Count > 0)
            {
                ActionDirValue actionVal = highestIndices[rand.Next(highestIndices.Count)];
                return actionVal.Action;
            }

            return new GameAction(GameAction.ActionType.Attack, Dir8.None);
        }

        /// <summary>
        /// Updates the highest indices list with the GameAction with the highest score.
        /// </summary>
        /// <param name="highestIndices"></param>
        /// <param name="baseAction">Non-directional action to use.  Direction will be selected when adding to list.</param>
        /// <param name="attackDirs"></param>
        protected void UpdateHighestIndices(List<ActionDirValue> highestIndices, GameAction baseAction, HitValue[] attackDirs)
        {
            int highestScore = 1;
            if (highestIndices.Count > 0)
                highestScore = highestIndices[0].Hit.Value;

            for (int ii = 0; ii < attackDirs.Length; ii++)
            {
                if (attackDirs[ii].Value > highestScore)
                {
                    highestIndices.Clear();
                    GameAction newAction = new GameAction(baseAction);
                    newAction.Dir = (Dir8)ii;
                    highestIndices.Add(new ActionDirValue(newAction, attackDirs[ii]));
                    highestScore = attackDirs[ii].Value;
                }
                else if (attackDirs[ii].Value == highestScore)
                {
                    GameAction newAction = new GameAction(baseAction);
                    newAction.Dir = (Dir8)ii;
                    highestIndices.Add(new ActionDirValue(newAction, attackDirs[ii]));
                }
            }
        }

        /// <summary>
        /// Updates the highest indices list with the GameAction with all scores.
        /// </summary>
        /// <param name="rand"></param>
        /// <param name="totalIndices"></param>
        /// <param name="baseAction">Non-directional action to use.  Direction will be selected when adding to list.</param>
        /// <param name="attackDirs"></param>
        protected void UpdateTotalIndices(IRandom rand, List<ActionDirValue> totalIndices, GameAction baseAction, HitValue[] attackDirs)
        {
            HitValue highestScore = new HitValue(0, false);
            List<int> highestDirs = new List<int>();
            for (int ii = 0; ii < attackDirs.Length; ii++)
            {
                if (attackDirs[ii].Value > highestScore.Value)
                {
                    highestDirs.Clear();
                    highestDirs.Add(ii);
                    highestScore = attackDirs[ii];
                }
                else if (attackDirs[ii].Value == highestScore.Value)
                    highestDirs.Add(ii);
            }
            if (highestScore.Value > 0)
            {
                int highestDir = highestDirs[rand.Next(highestDirs.Count)];
                GameAction newAction = new GameAction(baseAction);
                newAction.Dir = (Dir8)highestDir;
                totalIndices.Add(new ActionDirValue(newAction, highestScore));
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="controlledChar"></param>
        /// <param name="closestThreat">A character that will be used to judge an imagined hit. Leave blank for no imagined hits.</param>
        /// <param name="seenChars"></param>
        /// <param name="moveIndex"></param>
        /// <param name="dirs"></param>
        /// <param name="includeImagined">Whether or not we want to consider hypothetical hit weights.</param>
        protected void GetActionValues(Character controlledChar, List<Character> seenChars, Character closestThreat, string moveIndex, HitValue[] dirs, bool includeImagined)
        {
            SkillData entry = DataManager.Instance.GetSkill(moveIndex);

            bool canHitSomething;
            if (entry.HitboxAction is AreaAction && ((AreaAction)entry.HitboxAction).HitArea == Hitbox.AreaLimit.Full
                || entry.HitboxAction is SelfAction || entry.HitboxAction is ProjectileAction && ((ProjectileAction)entry.HitboxAction).Rays == ProjectileAction.RayCount.Eight)
            {
                Dir8 defaultDir = Dir8.None;
                if (closestThreat != null)
                    defaultDir = ZoneManager.Instance.CurrentMap.ApproximateClosestDir8(controlledChar.CharLoc, closestThreat.CharLoc);
                if (defaultDir == Dir8.None)
                    defaultDir = controlledChar.CharDir;
                HitValue highestVal = GetAttackDirValue(moveIndex, entry, controlledChar, seenChars, defaultDir);
                canHitSomething = (highestVal.Value > 0);
                dirs[(int)defaultDir] = highestVal;
            }
            else
            {
                HitValue[] vals = new HitValue[8];
                HitValue highestVal = new HitValue(0, false);

                //get the values of firing off an attack in the given direction, keeping track of the highest value
                for (int ii = 0; ii < DirExt.DIR8_COUNT; ii++)
                {
                    Dir8 dir = (Dir8)ii;
                    vals[ii] = GetAttackDirValue(moveIndex, entry, controlledChar, seenChars, dir);
                    if (vals[ii].CompareTo(highestVal) > 0)
                        highestVal = vals[ii];
                }

                //get the directions that result in the highest value and place them in the dirs to be used as the return variable
                for (int ii = 0; ii < DirExt.DIR8_COUNT; ii++)
                {
                    if (vals[ii].CompareTo(highestVal) == 0)
                        dirs[ii] = vals[ii];
                }
                canHitSomething = (highestVal.Value > 0);
            }

            if (!canHitSomething && includeImagined && closestThreat != null)
            {
                //in the event that no targets are found, and there is a non-null targetChar, calculate the potential attack value in a hypothetical hit scenario

                int newVal = GetAttackValue(controlledChar, moveIndex, entry, seenChars, closestThreat, 0, 100);
                //then, add it to the dirs value on the stipulation that it is a "desired" value- if chosen, it will cause the character to return "wait" instead.
                //this action value represents how valuable the action *would* be if in range
                if (newVal > 0)
                    dirs[(int)controlledChar.CharDir] = new HitValue(newVal, true, true);
            }
        }

        protected void GetItemUseValues(Character controlledChar, List<Character> seenChars, Character closestThreat, string itemIndex, HitValue[] dirs)
        {
            ItemData entry = DataManager.Instance.GetItem(itemIndex);
            if (!entry.ItemStates.Contains<BerryState>() && !entry.ItemStates.Contains<SeedState>() && !entry.ItemStates.Contains<WandState>())
                return;

            Dir8 defaultDir = Dir8.None;
            if (closestThreat != null)
                defaultDir = ZoneManager.Instance.CurrentMap.ApproximateClosestDir8(controlledChar.CharLoc, closestThreat.CharLoc);
            if (defaultDir == Dir8.None)
                defaultDir = controlledChar.CharDir;
            HitValue highestVal = GetActionDirValue("", null, entry.UseAction, entry.Explosion, entry.UseEvent, 0, controlledChar, seenChars, defaultDir, 0);
            dirs[(int)defaultDir] = highestVal;
        }

        protected void GetItemThrowValues(Character controlledChar, List<Character> seenChars, Character closestThreat, string itemIndex, HitValue[] dirs)
        {
            ItemData entry = DataManager.Instance.GetItem(itemIndex);

            if (!entry.ItemStates.Contains<BerryState>() && !entry.ItemStates.Contains<SeedState>() && !entry.ItemStates.Contains<AmmoState>())
                return;

            CombatAction hitboxAction;
            ExplosionData testExplosion = new ExplosionData(entry.Explosion);
            if (entry.ArcThrow)
            {
                ThrowAction action = new ThrowAction();
                action.Coverage = ThrowAction.ArcCoverage.WideAngle;
                action.TargetAlignments = Alignment.Foe;
                action.Speed = 10;
                action.Range = 6;
                hitboxAction = action;
            }
            else
            {
                ProjectileAction action = new ProjectileAction();
                action.TargetAlignments = Alignment.Friend | Alignment.Foe;
                action.Range = 8;
                action.StopAtHit = true;
                action.StopAtWall = true;
                action.HitTiles = true;
                hitboxAction = action;
                testExplosion.TargetAlignments = Alignment.Friend | Alignment.Foe | Alignment.Self;
            }

            HitValue[] vals = new HitValue[8];
            HitValue highestVal = new HitValue(0, false);

            //get the values of firing off an attack in the given direction, keeping track of the highest value
            for (int ii = 0; ii < DirExt.DIR8_COUNT; ii++)
            {
                Dir8 dir = (Dir8)ii;
                vals[ii] = GetActionDirValue("", null, hitboxAction, testExplosion, entry.UseEvent, 0, controlledChar, seenChars, dir, 0);
                if (vals[ii].CompareTo(highestVal) > 0)
                    highestVal = vals[ii];
            }

            //get the directions that result in the highest value and place them in the dirs to be used as the return variable
            for (int ii = 0; ii < DirExt.DIR8_COUNT; ii++)
            {
                if (vals[ii].CompareTo(highestVal) == 0)
                    dirs[ii] = vals[ii];
            }
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="controlledChar"></param>
        /// <param name="seenChars"></param>
        /// <param name="endHash"></param>
        /// <param name="blindspotOnly">Will only treat blindspot attacks as attacks that need to path to tiles.</param>
        protected void FillRangeTargets(Character controlledChar, List<Character> seenChars, Dictionary<Loc, RangeTarget> endHash, bool blindspotOnly)
        {
            foreach (Character seenChar in seenChars)
            {
                foreach (int skillSlot in iterateUsableSkillIndices(controlledChar))
                {
                    string skillIndex = controlledChar.Skills[skillSlot].Element.SkillNum;
                    SkillData entry = DataManager.Instance.GetSkill(skillIndex);

                    int rangeMod = 0;

                    CombatAction hitboxAction = entry.HitboxAction;
                    ExplosionData explosion = entry.Explosion;

                    //only check moves that hit foes
                    if ((explosion.TargetAlignments & Alignment.Foe) == Alignment.None)
                        continue;

                    Dir8 approxDir = ZoneManager.Instance.CurrentMap.ApproximateClosestDir8(controlledChar.CharLoc, seenChar.CharLoc);
                    modifyActionHitboxes(controlledChar, seenChars, approxDir, ref skillIndex, ref entry, ref rangeMod, ref hitboxAction, ref explosion);

                    if (hitboxAction == null)
                        continue;

                    //the attack has no effect on the target; don't count its tiles
                    int atkVal = GetAttackValue(controlledChar, skillIndex, entry, seenChars, seenChar, rangeMod, 100);
                    if (atkVal <= 0)
                        continue;

                    if (entry.HitboxAction is AreaAction)
                    {
                        if (blindspotOnly)
                            continue;
                        AreaAction areaAction = entry.HitboxAction as AreaAction;
                        int range = Math.Max(1, areaAction.Range + rangeMod);

                        //add everything in radius
                        for (int xx = -range; xx <= range; xx++)
                        {
                            for (int yy = -range; yy <= range; yy++)
                                updateDistanceTargetHash(controlledChar, endHash, seenChar, new Loc(xx, yy));
                        }
                    }
                    else if (entry.HitboxAction is ThrowAction)
                    {
                        if (blindspotOnly)
                            continue;
                        ThrowAction throwAction = entry.HitboxAction as ThrowAction;
                        int range = Math.Max(1, throwAction.Range + rangeMod);

                        //add everything in radius
                        for (int xx = -range; xx <= range; xx++)
                        {
                            for (int yy = -range; yy <= range; yy++)
                                updateDistanceTargetHash(controlledChar, endHash, seenChar, new Loc(xx, yy));
                        }
                    }
                    else if (entry.HitboxAction is LinearAction)
                    {
                        if (blindspotOnly)
                            continue;
                        LinearAction lineAction = entry.HitboxAction as LinearAction;
                        int range = Math.Max(1, lineAction.Range + rangeMod);
                        //add everything in line
                        for (int ii = 0; ii < DirExt.DIR8_COUNT; ii++)
                        {
                            Dir8 dir = (Dir8)ii;
                            for (int jj = 1; jj <= range; jj++)
                            {
                                bool blocked = DungeonScene.Instance.ShotBlocked(controlledChar, seenChar.CharLoc + dir.GetLoc() * (jj - 1), dir, entry.HitboxAction.TargetAlignments, false, lineAction.StopAtWall);
                                if (blocked)
                                    break;

                                updateDistanceTargetHash(controlledChar, endHash, seenChar, dir.GetLoc() * jj);
                                if (lineAction.IsWide())
                                {
                                    Dir8 left = DirExt.AddAngles(dir, Dir8.Left);
                                    Dir8 right = DirExt.AddAngles(dir, Dir8.Right);
                                    if (dir.IsDiagonal())
                                    {
                                        left = DirExt.AddAngles(dir, Dir8.UpLeft);
                                        right = DirExt.AddAngles(dir, Dir8.UpRight);
                                    }
                                    //add everything on the sides if it's a wide action
                                    updateDistanceTargetHash(controlledChar, endHash, seenChar, left.GetLoc() + dir.GetLoc() * jj);
                                    updateDistanceTargetHash(controlledChar, endHash, seenChar, right.GetLoc() + dir.GetLoc() * jj);
                                }
                            }
                        }
                    }
                    else if (entry.HitboxAction is OffsetAction)
                    {
                        OffsetAction offsetAction = entry.HitboxAction as OffsetAction;
                        int range = Math.Max(1, offsetAction.Range + rangeMod);
                        //add everything in the offset
                        for (int ii = 0; ii < DirExt.DIR8_COUNT; ii++)
                        {
                            Dir8 dir = (Dir8)ii;
                            switch (offsetAction.HitArea)
                            {
                                case OffsetAction.OffsetArea.Tile:
                                    updateDistanceTargetHash(controlledChar, endHash, seenChar, dir.GetLoc() * range);
                                    break;
                                case OffsetAction.OffsetArea.Sides:
                                    {
                                        updateDistanceTargetHash(controlledChar, endHash, seenChar, dir.GetLoc() * range);
                                        Dir8 left = DirExt.AddAngles(dir, Dir8.Left);
                                        Dir8 right = DirExt.AddAngles(dir, Dir8.Right);
                                        if (dir.IsDiagonal())
                                        {
                                            left = DirExt.AddAngles(dir, Dir8.UpLeft);
                                            right = DirExt.AddAngles(dir, Dir8.UpRight);
                                        }
                                        //add everything on the sides if it's a wide action
                                        updateDistanceTargetHash(controlledChar, endHash, seenChar, left.GetLoc() + dir.GetLoc() * range);
                                        updateDistanceTargetHash(controlledChar, endHash, seenChar, right.GetLoc() + dir.GetLoc() * range);
                                    }
                                    break;
                                case OffsetAction.OffsetArea.Cross:
                                    {
                                        updateDistanceTargetHash(controlledChar, endHash, seenChar, dir.GetLoc() * range);
                                        updateDistanceTargetHash(controlledChar, endHash, seenChar, Dir8.Down.GetLoc() + dir.GetLoc() * range);
                                        updateDistanceTargetHash(controlledChar, endHash, seenChar, Dir8.Left.GetLoc() + dir.GetLoc() * range);
                                        updateDistanceTargetHash(controlledChar, endHash, seenChar, Dir8.Up.GetLoc() + dir.GetLoc() * range);
                                        updateDistanceTargetHash(controlledChar, endHash, seenChar, Dir8.Right.GetLoc() + dir.GetLoc() * range);
                                    }
                                    break;
                                case OffsetAction.OffsetArea.Area:
                                    {
                                        for (int xx = -1; xx <= 1; xx++)
                                        {
                                            for (int yy = -1; yy <= 1; yy++)
                                                updateDistanceTargetHash(controlledChar, endHash, seenChar, dir.GetLoc() * range + new Loc(xx, yy));
                                        }
                                    }
                                    break;
                            }
                        }
                    }
                }
                updateDistanceTargetHash(controlledChar, endHash, seenChar, Loc.Zero);
            }
        }

        private T getConditionalEvent<T>(Character controlledChar, PassiveContext passiveContext, BattleEvent effect) where T : BattleEvent
        {
            if (effect is T)
                return (T)effect;

            //TODO: add other conditions
            FamilyBattleEvent familyEffect = effect as FamilyBattleEvent;
            if (familyEffect != null)
            {
                ItemData entry = (ItemData)passiveContext.Passive.GetData();
                FamilyState family;
                if (!entry.ItemStates.TryGet<FamilyState>(out family))
                    return null;
                if (family.Members.Contains(controlledChar.BaseForm.Species))
                    return getConditionalEvent<T>(controlledChar, passiveContext, familyEffect.BaseEvent);
            }
            return null;
        }

        private void modifyActionHitboxes(Character controlledChar, List<Character> seenChars, Dir8 dir, ref string skillIndex, ref SkillData entry, ref  int rangeMod, ref CombatAction hitboxAction, ref ExplosionData explosion)
        {
            //check for passives that modify range; NOTE: specialized AI code!
            foreach (PassiveContext passive in controlledChar.IteratePassives(GameEventPriority.USER_PORT_PRIORITY))
            {
                foreach (BattleEvent effect in passive.EventData.OnActions.EnumerateInOrder())
                {
                    AddRangeEvent addRangeEvent = getConditionalEvent<AddRangeEvent>(controlledChar, passive, effect);
                    if (addRangeEvent != null)
                    {
                        rangeMod += addRangeEvent.Range;
                        continue;
                    }

                    CategoryAddRangeEvent categoryRangeEvent = getConditionalEvent<CategoryAddRangeEvent>(controlledChar, passive, effect);
                    if (categoryRangeEvent != null)
                    {
                        if (entry.Data.Category == categoryRangeEvent.Category)
                            rangeMod += categoryRangeEvent.Range;
                        continue;
                    }

                    WeatherAddRangeEvent weatherRangeEvent = getConditionalEvent<WeatherAddRangeEvent>(controlledChar, passive, effect);
                    if (weatherRangeEvent != null)
                    {
                        if (ZoneManager.Instance.CurrentMap.Status.ContainsKey(weatherRangeEvent.WeatherID))
                            rangeMod += weatherRangeEvent.Range;
                        continue;
                    }
                    
                    ElementAddRangeEvent elementRangeEvent = getConditionalEvent<ElementAddRangeEvent>(controlledChar, passive, effect);
                    if (elementRangeEvent != null)
                    {
                        if (elementRangeEvent.Elements.Contains(controlledChar.Element1) || elementRangeEvent.Elements.Contains(controlledChar.Element2))
                        {
                            rangeMod += elementRangeEvent.Range;
                        }
                        continue;
                    }
                }
            }
            //check for moves that want to wait until within range
            if (skillIndex == "razor_wind")//wait until enemy is two+ tiles deep in the hitbox, to prevent immediate walk-away; NOTE: specialized AI code!
                rangeMod--;
            else if (skillIndex == "draco_meteor")//wait until enemy is two+ tiles deep in the hitbox, to catch potential partners
                rangeMod--;
            else if (skillIndex == "magnitude")//wait until enemy is three+ tiles deep in the hitbox, to increase damage output
                rangeMod -= 2;

            rangeMod = Math.Min(Math.Max(-3, rangeMod), 3);

            //check for moves that change range on conditions; NOTE: specialized AI code!
            switch (skillIndex)
            {
                case "fire_pledge":
                case "water_pledge":
                case "grass_pledge":
                    {
                        //intentionally add 4 to the range of 0, as 4 is the detection radius
                        rangeMod = 4;
                        break;
                    }
                case "mirror_move": // mirror move
                case "assist": // assist
                case "copycat": // copycat
                    {
                        string searchedStatus = "";
                        foreach (BattleEvent effect in entry.Data.OnHits.EnumerateInOrder())
                        {
                            MirrorMoveEvent mirror = effect as MirrorMoveEvent;
                            if (mirror != null)
                            {
                                searchedStatus = mirror.MoveStatusID;
                                break;
                            }
                        }

                        string calledMove = "";
                        HashSet<Loc> callTiles = new HashSet<Loc>();
                        foreach (Loc loc in hitboxAction.GetPreTargets(controlledChar, dir, rangeMod))
                        {
                            foreach (Loc expLoc in explosion.IterateTargetedTiles(loc))
                                callTiles.Add(ZoneManager.Instance.CurrentMap.WrapLoc(expLoc));
                        }

                        foreach (Character target in seenChars)
                        {
                            if (DungeonScene.Instance.IsTargeted(controlledChar, target, explosion.TargetAlignments) && callTiles.Contains(target.CharLoc))
                            {
                                StatusEffect moveEffect;
                                if (target.StatusEffects.TryGetValue(searchedStatus, out moveEffect))
                                {
                                    calledMove = moveEffect.StatusStates.Get<IDState>().ID;
                                    break;
                                }
                            }
                        }
                        skillIndex = calledMove;
                        if (String.IsNullOrEmpty(calledMove))
                        {
                            entry = null;
                            hitboxAction = null;
                            explosion = null;
                        }
                        else
                        {
                            SkillData calledEntry = DataManager.Instance.GetSkill(calledMove);
                            entry = calledEntry;

                            hitboxAction = entry.HitboxAction;
                            explosion = entry.Explosion;
                        }
                    }
                    break;
                case "curse": // curse
                    {
                        if (controlledChar.HasElement("ghost"))
                        {
                            foreach (BattleEvent effect in entry.Data.OnActions.EnumerateInOrder())
                            {
                                if (effect is ElementDifferentUseEvent)
                                {
                                    ElementDifferentUseEvent elementEffect = (ElementDifferentUseEvent)effect;
                                    hitboxAction = elementEffect.HitboxAction;
                                    explosion = elementEffect.Explosion;
                                    break;
                                }
                            }
                        }
                    }
                    break;
                case "nature_power": // nature power
                    {
                        string calledMove = "";
                        foreach (BattleEvent effect in entry.Data.OnHits.EnumerateInOrder())
                        {
                            NatureMoveEvent nature = effect as NatureMoveEvent;
                            if (nature != null)
                            {
                                foreach (string terrain in nature.TerrainPair.Keys)
                                {
                                    if (ZoneManager.Instance.CurrentMap.Status.ContainsKey(terrain))
                                    {
                                        calledMove = nature.TerrainPair[terrain];
                                        break;
                                    }
                                }

                                string moveNum;
                                if (nature.NaturePair.TryGetValue(ZoneManager.Instance.CurrentMap.Element, out moveNum))
                                    calledMove = moveNum;
                                else
                                    calledMove = "";
                                break;
                            }
                        }
                        skillIndex = calledMove;
                        if (String.IsNullOrEmpty(calledMove))
                        {
                            entry = null;
                            hitboxAction = null;
                            explosion = null;
                        }
                        else
                        {
                            SkillData calledEntry = DataManager.Instance.GetSkill(calledMove);
                            entry = calledEntry;

                            hitboxAction = entry.HitboxAction;
                            explosion = entry.Explosion;
                        }
                    }
                    break;
                case "sleep_talk": // sleep talk
                    {
                        string calledMove = "";

                        int recordSlot = -1;
                        int recordPower = -1;
                        for (int ii = 0; ii < controlledChar.Skills.Count; ii++)
                        {
                            if (!String.IsNullOrEmpty(controlledChar.Skills[ii].Element.SkillNum))
                            {
                                SkillData testEntry = DataManager.Instance.GetSkill(controlledChar.Skills[ii].Element.SkillNum);

                                int basePower = 0;
                                if (testEntry.Data.Category == BattleData.SkillCategory.Status)
                                    basePower = -1;
                                else
                                {
                                    BasePowerState state = testEntry.Data.SkillStates.GetWithDefault<BasePowerState>();
                                    if (state != null)
                                        basePower = state.Power;
                                }
                                if (basePower > recordPower)
                                {
                                    recordSlot = ii;
                                    recordPower = basePower;
                                }
                            }
                        }

                        if (recordSlot > -1)
                            calledMove = controlledChar.Skills[recordSlot].Element.SkillNum;

                        skillIndex = calledMove;
                        if (String.IsNullOrEmpty(calledMove))
                        {
                            entry = null;
                            hitboxAction = null;
                            explosion = null;
                        }
                        else
                        {
                            SkillData calledEntry = DataManager.Instance.GetSkill(calledMove);
                            entry = calledEntry;

                            hitboxAction = entry.HitboxAction;
                            explosion = entry.Explosion;
                        }
                    }
                    break;
                case "spit_up": // spit up
                    {
                        foreach (BattleEvent effect in entry.Data.OnActions.EnumerateInOrder())
                        {
                            if (effect is StatusStackDifferentEvent)
                            {
                                StatusStackDifferentEvent stackEffect = (StatusStackDifferentEvent)effect;
                                StatusEffect stockStatus = controlledChar.GetStatusEffect(stackEffect.StatusID);
                                if (stockStatus != null)
                                {
                                    StackState stack = stockStatus.StatusStates.Get<StackState>();
                                    hitboxAction = stackEffect.StackPair[stack.Stack].Item1;
                                    explosion = stackEffect.StackPair[stack.Stack].Item2;
                                }
                                else
                                {
                                    entry = null;
                                    hitboxAction = null;
                                    explosion = null;
                                }
                                break;
                            }
                        }
                    }
                    break;
                case "me_first": // me first
                    {
                        string calledMove = "";
                        HashSet<Loc> callTiles = new HashSet<Loc>();
                        foreach (Loc loc in hitboxAction.GetPreTargets(controlledChar, dir, rangeMod))
                        {
                            foreach (Loc expLoc in explosion.IterateTargetedTiles(loc))
                                callTiles.Add(ZoneManager.Instance.CurrentMap.WrapLoc(expLoc));
                        }

                        foreach (Character target in seenChars)
                        {
                            if (DungeonScene.Instance.IsTargeted(controlledChar, target, explosion.TargetAlignments) && callTiles.Contains(target.CharLoc))
                            {
                                int recordSlot = -1;
                                int recordPower = -1;
                                for (int ii = 0; ii < target.Skills.Count; ii++)
                                {
                                    if (!String.IsNullOrEmpty(target.Skills[ii].Element.SkillNum))
                                    {
                                        SkillData testEntry = DataManager.Instance.GetSkill(target.Skills[ii].Element.SkillNum);

                                        int basePower = 0;
                                        if (testEntry.Data.Category == BattleData.SkillCategory.Status)
                                            basePower = -1;
                                        else
                                        {
                                            BasePowerState state = testEntry.Data.SkillStates.GetWithDefault<BasePowerState>();
                                            if (state != null)
                                                basePower = state.Power;
                                        }
                                        if (basePower > recordPower)
                                        {
                                            recordSlot = ii;
                                            recordPower = basePower;
                                        }
                                    }
                                }

                                if (recordSlot > -1)
                                    calledMove = target.Skills[recordSlot].Element.SkillNum;
                                break;
                            }
                        }

                        skillIndex = calledMove;
                        if (String.IsNullOrEmpty(calledMove))
                        {
                            entry = null;
                            hitboxAction = null;
                            explosion = null;
                        }
                        else
                        {
                            SkillData calledEntry = DataManager.Instance.GetSkill(calledMove);
                            entry = calledEntry;

                            hitboxAction = entry.HitboxAction;
                            explosion = entry.Explosion;
                        }
                    }
                    break;
            }
        }

        protected HitValue GetAttackDirValue(string skillIndex, SkillData entry, Character controlledChar, List<Character> seenChars, Dir8 dir)
        {

            //Dig/Fly/Dive/Phantom Force; NOTE: specialized AI code!
            if (skillIndex == "dig" || skillIndex == "fly" || skillIndex == "dive" || skillIndex == "phantom_force")
            {
                if (!controlledChar.CantInteract)//TODO: CantInteract doesn't always indicate forced attack, but this'll do for now.
                {
                    foreach (Character target in seenChars)
                    {
                        if (DungeonScene.Instance.IsTargeted(controlledChar, target, Alignment.Foe) && ZoneManager.Instance.CurrentMap.InRange(target.CharLoc, controlledChar.CharLoc, 3))
                        {
                            int newVal = GetAttackValue(controlledChar, skillIndex, entry, seenChars, target, 0, 100);
                            if (newVal > 0)
                                return new HitValue(100, true);
                        }
                    }
                }
            }
            else if (skillIndex == "focus_punch")//Focus Punch;
            {
                //always activate if not already forced to attack
                if (!controlledChar.CantInteract)//TODO: CantInteract doesn't always indicate forced attack, but this'll do for now.
                    return new HitValue(100, true);
            }
            else if (skillIndex == "retaliate" || skillIndex == "fire_pledge" || skillIndex == "water_pledge" || skillIndex == "grass_pledge")
            {
                foreach (BattleEvent effect in entry.Data.BeforeTryActions.EnumerateInOrder())
                {
                    if (effect is WatchOrStrikeEvent)
                    {
                        string status = ((WatchOrStrikeEvent)effect).ChargeStatus;
                        if (controlledChar.GetStatusEffect(status) != null)
                            return new HitValue(0, false);
                    }
                }
            }

            int rangeMod = 0;

            CombatAction hitboxAction = entry.HitboxAction;
            ExplosionData explosion = entry.Explosion;

            modifyActionHitboxes(controlledChar, seenChars, dir, ref skillIndex, ref entry, ref rangeMod, ref hitboxAction, ref explosion);

            return GetActionDirValue(skillIndex, entry, hitboxAction, explosion, entry?.Data, rangeMod, controlledChar, seenChars, dir, 100);
        }

        protected HitValue GetActionDirValue(string skillIndex, SkillData entry, CombatAction hitboxAction, ExplosionData explosion, BattleData data, int rangeMod, Character controlledChar, List<Character> seenChars, Dir8 dir, int defaultVal)
        {
            if (hitboxAction == null)
                return new HitValue(0, false);

            HashSet<Loc> hitTiles = new HashSet<Loc>();
            foreach (Loc loc in hitboxAction.GetPreTargets(controlledChar, dir, rangeMod))
            {
                foreach (Loc expLoc in explosion.IterateTargetedTiles(loc))
                    hitTiles.Add(ZoneManager.Instance.CurrentMap.WrapLoc(expLoc));
            }

            bool directHit = false;
            int totalTargets = 0;
            int totalValue = 0;
            int maxValue = 0;

            foreach (Character target in seenChars)
            {
                if (DungeonScene.Instance.IsTargeted(controlledChar, target, explosion.TargetAlignments) && hitTiles.Contains(target.CharLoc))
                {
                    totalTargets++;
                    if (Collision.InFront(controlledChar.CharLoc, target.CharLoc, dir, -1))
                        directHit = true;

                    int newVal;
                    
                    if (entry != null)//for moves
                        newVal = GetAttackValue(controlledChar, skillIndex, entry, seenChars, target, rangeMod, defaultVal);
                    else//for items
                        newVal = GetBattleValue(controlledChar, data, seenChars, target, rangeMod, defaultVal);
                    totalValue += newVal;
                    if (newVal >= 0)
                        maxValue = Math.Max(newVal, maxValue);
                    else
                    {
                        //the AI will refuse to use the attack in this direction if it will harm any allies,
                        //even if it may be considered "worth it" to damage more foes in the process
                        if ((IQ & AIFlags.PlayerSense) != AIFlags.None)
                        {
                            maxValue = -1;
                            break;
                        }
                    }
                }
            }

            if (data.Category == BattleData.SkillCategory.Status && maxValue > 0)
                return new HitValue(totalValue / totalTargets, directHit);
            else
                return new HitValue(maxValue, directHit);
        }

        protected int GetAttackValue(Character controlledChar, string moveIndex, SkillData entry, List<Character> seenChars, Character target, int rangeMod, int defaultVal)
        {
            int delta = GetTargetEffect(controlledChar, moveIndex, entry, seenChars, target, rangeMod, defaultVal);

            return AlignTargetEffect(controlledChar, entry.Data, target, delta);
        }

        protected int GetBattleValue(Character controlledChar, BattleData data, List<Character> seenChars, Character target, int rangeMod, int defaultVal)
        {
            int delta = GetTargetBattleDataEffectWithRangeLimit(controlledChar, data, seenChars, target, 0, 1, defaultVal);

            return AlignTargetEffect(controlledChar, data, target, delta);
        }

        protected int AlignTargetEffect(Character controlledChar, BattleData data, Character target, int delta)
        {
            bool teamPartner = (IQ & AIFlags.TeamPartner) != AIFlags.None;
            Alignment matchup = DungeonScene.Instance.GetMatchup(controlledChar, target, !teamPartner);

            if (matchup == Alignment.Foe)
            {
                bool wontDisturb = (IQ & AIFlags.WontDisturb) != AIFlags.None;
                if (teamPartner || wontDisturb)
                {
                    //logic to not attack sleeping foes or foes with a certain AI
                    StatusEffect sleepStatus = target.GetStatusEffect("sleep");
                    if (sleepStatus != null)
                    {
                        bool leaveBe = true;
                        int counter = sleepStatus.StatusStates.GetWithDefault<CountDownState>().Counter;
                        if (!teamPartner)
                        {
                            //team partners are extra cautious not to do anything to sleepers, but npcs will still attack with status if it applies
                            if (data.Category == BattleData.SkillCategory.Status && counter > 1)
                                leaveBe = false;
                            else if (data.Category != BattleData.SkillCategory.Status && counter <= 1)
                                leaveBe = false;
                        }
                        else
                        {
                            //attack to wake up if they were going to wake up anyway
                            if (counter == 0 || counter == 1)
                                leaveBe = false;
                        }
                        if (leaveBe)
                            return 0;
                    }
                    StatusEffect freezeStatus = target.GetStatusEffect("freeze");
                    if (freezeStatus != null)
                    {
                        bool leaveBe = true;
                        int counter = freezeStatus.StatusStates.GetWithDefault<CountDownState>().Counter;
                        if (data.Element == "fire")
                        {
                            //attack to thaw if they were going to thaw anyway
                            if (counter == 0 || counter == 1)
                                leaveBe = false;
                        }

                        if (leaveBe)
                            return 0;
                    }
                }
                if (teamPartner)
                {
                    if (target.GetStatusEffect("last_targeted_by") == null)//last targeted by someone; NOTE: specialized AI code!
                    {
                        if (target.Tactic.ID == "weird_tree")//weird tree; NOTE: specialized AI code!
                            return 0;
                        if (target.Tactic.ID == "moon_dance")//moon dance; NOTE: specialized AI code!
                            return 0;
                        else if (target.Tactic.ID == "wait_attack")//wait attack; NOTE: specialized AI code!
                            return 0;
                        else if (target.Tactic.ID == "tit_for_tat")//tit for tat; NOTE: specialized AI code!
                            return 0;
                    }
                }
                else
                {
                    if (isIllusionStealth(controlledChar, target))
                        return 0;
                }
            }

            if (matchup == Alignment.Foe) //for enemies, having a positive effect on the target is a negative outcome for the player, so flip sign
                return -delta;
            else // for allies/self, having a positive effect on the target is a positive outcome for the player, keep sign
                return delta;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="controlledChar"></param>
        /// <param name="moveIndex"></param>
        /// <param name="entry"></param>
        /// <param name="seenChars"></param>
        /// <param name="target"></param>
        /// <param name="rangeMod"></param>
        /// <returns>Positive number means a positive effect for the target, negative number means a negative effect for the target.</returns>
        protected int GetTargetEffect(Character controlledChar, string moveIndex, SkillData entry, List<Character> seenChars, Character target, int rangeMod, int defaultVal)
        {
            //when trying to attack an enemy, first check for getting in range of any attack, and use that attack if possible
            //check against all possible enemies before trying to move

            //priority of choices
            //0:an attack that can kill someone
            //1:choose one of the following: an attack that damages someone (within this, the most damaging attack), an attack that will give the foe a new status problem, an attack that will give self/allies a new status plus
            //2: choose one of the following: an attack that will stack a status for either party, or approach them
            //never move away from the enemy to get in range; it just won't be done.

            //special cases go here
            //TODO: move all NOTE codes into properly specialized code blocks

            foreach (BattleEvent effect in entry.Data.OnActions.EnumerateInOrder())
            {
                if (effect is StatusNeededEvent)
                {
                    if (controlledChar.GetStatusEffect(((StatusNeededEvent)effect).StatusID) == null)
                        return 0;
                }
            }

            foreach (BattleEvent effect in entry.Data.BeforeHits.EnumerateInOrder())
            {
                if (effect is TipOnlyEvent)
                {
                    //TODO: this breaks in small wrapped maps
                    if (ZoneManager.Instance.CurrentMap.InRange(controlledChar.CharLoc, target.CharLoc, Math.Max(entry.HitboxAction.Distance + rangeMod, 1) - 1))
                        return 0;
                }
                else if (effect is DistanceOnlyEvent)
                {
                    //TODO: this breaks in small wrapped maps
                    if (ZoneManager.Instance.CurrentMap.InRange(controlledChar.CharLoc, target.CharLoc, 1))
                        return 0;
                }
            }
            foreach (BattleEvent effect in entry.Data.OnHits.EnumerateInOrder())
            {
                if (effect is ThrowBackEvent)
                {
                    //this is only reserved for AI that doesnt mind friendly fire
                    if ((IQ & AIFlags.PlayerSense) == AIFlags.None && DungeonScene.Instance.GetMatchup(controlledChar, target) != Alignment.Foe)
                    {
                        ThrowBackEvent throwEvent = (ThrowBackEvent)effect;
                        //check if throwing the (ally) target will hit an enemy
                        Dir8 throwDir = DirExt.ApproximateDir8(target.CharLoc - controlledChar.CharLoc);
                        Loc? throwTarget = ThrowAction.GetTarget(controlledChar, controlledChar.CharLoc, throwDir, true, throwEvent.Distance, Alignment.Foe, target);
                        if (throwTarget != null)
                            return 100;
                        else
                            return 0;
                    }
                }
            }
            if (moveIndex == "present")//Present; if an ally, use healing calculations; NOTE: specialized AI code!
            {
                if (DungeonScene.Instance.GetMatchup(controlledChar, target) != Alignment.Foe)
                {
                    int healHP = target.MaxHP / 3;
                    int hpToHeal = target.MaxHP - target.HP;
                    int hpWorthHealing = Math.Max(hpToHeal - healHP / 2, 0);
                    //the healing only has worth if the target is missing at least half the HP the healing would give
                    return hpWorthHealing * 200 / healHP;
                }
            }
            else if (moveIndex == "ingrain")//Ingrain; use it only if damaged, AND if enemies are close; NOTE: specialized AI code!
            {
                bool nearEnemy = false;
                foreach (Character character in seenChars)
                {
                    if (DungeonScene.Instance.GetMatchup(controlledChar, character) == Alignment.Foe && ZoneManager.Instance.CurrentMap.InRange(character.CharLoc, controlledChar.CharLoc, 2))
                    {
                        nearEnemy = true;
                        break;
                    }
                }
                if (target.HP * 4 / 3 > target.MaxHP && !nearEnemy)
                    return 0;
            }
            else if (moveIndex == "splash")//Splash; use it only if enemies are close; NOTE: specialized AI code!
            {
                bool nearEnemy = false;
                foreach (Character character in seenChars)
                {
                    if (DungeonScene.Instance.GetMatchup(controlledChar, character) == Alignment.Foe && ZoneManager.Instance.CurrentMap.InRange(character.CharLoc, controlledChar.CharLoc, 1))
                    {
                        nearEnemy = true;
                        break;
                    }
                }
                if (!nearEnemy)
                    return 0;
                //always use on self; considered good on self
                return 100;
            }
            else if (moveIndex == "perish_song")//perish song; don't care if it hits self or allies; NOTE: specialized AI code!
            {
                if (DungeonScene.Instance.GetMatchup(controlledChar, target) != Alignment.Foe)
                    return 0;
            }
            else if (moveIndex == "counter" && (IQ & AIFlags.KnowsMatchups) == AIFlags.None)//counter; do not use if mirror coat status exists and has more than 1 turn left; NOTE: specialized AI code!
            {
                StatusEffect mutexStatus = controlledChar.GetStatusEffect("mirror_coat");
                if (mutexStatus != null)
                {
                    CountDownState state = mutexStatus.StatusStates.Get<CountDownState>();
                    if (state.Counter > 1)
                        return 0;
                }
            }
            else if (moveIndex == "mirror_coat" && (IQ & AIFlags.KnowsMatchups) == AIFlags.None)//Mirror coat; do not use if counter status exists and has more than 1 turn left; NOTE: specialized AI code!
            {
                StatusEffect mutexStatus = controlledChar.GetStatusEffect("counter");
                if (mutexStatus != null)
                {
                    CountDownState state = mutexStatus.StatusStates.Get<CountDownState>();
                    if (state.Counter > 1)
                        return 0;
                }
            }
            else if (moveIndex == "swallow")//swallow; use only if stockpiled and damaged; NOTE: specialized AI code!
            {
                StatusEffect stockStatus = controlledChar.GetStatusEffect("stockpile");
                if (stockStatus != null)
                {
                    StackState stack = stockStatus.StatusStates.Get<StackState>();
                    int healHP = target.MaxHP * stack.Stack / 2;
                    int hpToHeal = target.MaxHP - target.HP;
                    int hpWorthHealing = Math.Max(hpToHeal - healHP / 2, 0);
                    //the healing only has worth if the target is missing at least half the HP the healing would give
                    return hpWorthHealing * 200 / healHP;
                }
                return 0;
            }
            else if (moveIndex == "bestow")//Bestow; use only if you have an item to give; NOTE: specialized AI code!
            {
                if (!String.IsNullOrEmpty(controlledChar.EquippedItem.ID))//let's assume the item is always bad
                    return -100;
                return 0;
            }
            else if (moveIndex == "yawn")//yawn; use only if the target is OK; NOTE: specialized AI code!
            {
                foreach (StatusEffect status in target.IterateStatusEffects())
                {
                    if (status.StatusStates.Contains<MajorStatusState>())
                        return 0;
                }
            }
            else if (moveIndex == "teleport")//teleport; never use here if we attack to escape; handle it elsewhere; NOTE: specialized AI code!
            {
                if ((IQ & AIFlags.AttackToEscape) != AIFlags.None)
                {
                    if (target.HP < target.MaxHP)// only have a chance if damaged, or enemies close
                        return 100;//always use on self; considered good on self

                    bool nearEnemy = false;
                    foreach (Character character in seenChars)
                    {
                        if (DungeonScene.Instance.GetMatchup(controlledChar, character) == Alignment.Foe && ZoneManager.Instance.CurrentMap.InRange(character.CharLoc, controlledChar.CharLoc, 3))
                        {
                            nearEnemy = true;
                            break;
                        }
                    }
                    if (nearEnemy)
                        return 100;
                }
                return 0;
            }

            return GetTargetBattleDataEffectWithRangeLimit(controlledChar, entry.Data, seenChars, target, rangeMod, entry.Strikes, defaultVal);
        }

        protected int GetTargetBattleDataEffectWithRangeLimit(Character controlledChar, BattleData data, List<Character> seenChars, Character target, int rangeMod, int strikes, int defaultVal)
        {
            int delta = GetTargetBattleDataEffect(controlledChar, data, seenChars, target, 0, 1, defaultVal);

            //special case for self-buffing
            if (controlledChar.GetStatusEffect("last_targeted_by") == null)
            {
                Alignment targetAlignment = DungeonScene.Instance.GetMatchup(controlledChar, target);
                if (data.Category == BattleData.SkillCategory.Status || data.Category == BattleData.SkillCategory.None)
                {
                    if (delta > 0 && targetAlignment != Alignment.Foe)
                    {
                        //do not self-buff if more than X tiles away and hasnt been attacked yet
                        if (SelfStatusRange > 0 && getClosestFoeDistance(controlledChar, seenChars) > SelfStatusRange)
                            delta = 0;
                    }
                    else if (delta < 0 && targetAlignment == Alignment.Foe)
                    {
                        //do not debuff if more than X tiles away and hasnt been attacked yet
                        if (StatusRange > 0 && getClosestFoeDistance(controlledChar, seenChars) > StatusRange)
                            delta = 0;
                    }
                }
                else if (targetAlignment == Alignment.Foe)
                {
                    //do not attack if more than X tiles away and hasnt been attacked yet
                    if (AttackRange > 0 && getClosestFoeDistance(controlledChar, seenChars) > AttackRange)
                        delta = 0;
                }
            }

            return delta;
        }

        protected int getClosestFoeDistance(Character controlledChar, List<Character> seenChars)
        {
            int nearestEnemy = Int32.MaxValue;
            foreach (Character character in seenChars)
            {
                if (DungeonScene.Instance.GetMatchup(controlledChar, character) == Alignment.Foe)
                    nearestEnemy = Math.Min(nearestEnemy, ZoneManager.Instance.CurrentMap.GetClosestDist8(character.CharLoc, controlledChar.CharLoc));
            }
            return nearestEnemy;
        }

        protected int GetTargetBattleDataEffect(Character controlledChar, BattleData data, List<Character> seenChars, Character target, int rangeMod, int strikes, int defaultVal)
        {
            if (data.Category == BattleData.SkillCategory.Status || data.Category == BattleData.SkillCategory.None)
            {
                foreach (BattleEvent effect in data.OnHitTiles.EnumerateInOrder())
                {
                    if (effect is SetTrapEvent)
                    {
                        Tile checkTile = ZoneManager.Instance.CurrentMap.Tiles[target.CharLoc.X][target.CharLoc.Y];
                        if (String.IsNullOrEmpty(checkTile.Effect.ID))
                            return -70;
                        return 0;
                    }
                }

                //heal checker/status removal checker/other effects
                foreach (BattleEvent effect in data.OnHits.EnumerateInOrder())
                {
                    if (effect is IHealEvent)
                    {
                        IHealEvent giveEffect = (IHealEvent)effect;
                        int healHP = target.MaxHP * giveEffect.HPNum / giveEffect.HPDen;
                        int hpToHeal = target.MaxHP - target.HP;
                        int hpWorthHealing = Math.Max(hpToHeal - healHP / 2, 0);
                        //the healing only has worth if the target is missing at least half the HP the healing would give
                        return hpWorthHealing * 200 / healHP;
                    }
                    else if (effect is RestorePPEvent)
                    {
                        int total = 0;
                        for (int ii = 0; ii < target.Skills.Count; ii++)
                        {
                            if (!String.IsNullOrEmpty(target.Skills[ii].Element.SkillNum) && controlledChar.Skills[ii].Element.Charges == 0)
                            {
                                if (total > 0)
                                    total += 50;
                                else
                                    total += 100;
                            }
                        }
                        return total;
                    }
                    else if (effect is RemoveStateStatusBattleEvent)
                    {
                        int totalEffect = 0;
                        RemoveStateStatusBattleEvent giveEffect = (RemoveStateStatusBattleEvent)effect;
                        foreach (StatusEffect status in target.IterateStatusEffects())
                        {
                            int addedEffect = 0;
                            foreach (FlagType state in giveEffect.States)
                            {
                                if (status.StatusStates.Contains(state.FullType))
                                {
                                    addedEffect = -100;
                                    break;
                                }
                            }
                            if (status.StatusStates.Contains<BadStatusState>())
                                addedEffect *= -1;
                            if (status.StatusStates.Contains<StackState>())
                            {
                                StackState stack = status.StatusStates.Get<StackState>();
                                addedEffect *= stack.Stack;
                                addedEffect /= 2;
                            }
                            totalEffect += addedEffect;
                        }
                        return totalEffect;
                    }
                    else if (effect is MimicBattleEvent)
                    {
                        //mimic only if there is something to mimic
                        MimicBattleEvent giveEffect = (MimicBattleEvent)effect;

                        StatusEffect moveEffect;
                        if (target.StatusEffects.TryGetValue(giveEffect.LastMoveStatusID, out moveEffect))
                        {
                            //beneficial to self/ally, detrimental to foe
                            if (DungeonScene.Instance.GetMatchup(controlledChar, target) != Alignment.Foe)
                                return 100;
                            else
                                return -100;
                        }
                        return 0;
                    }
                    else if (effect is KnockBackEvent)
                    {
                        //assume always pointed at foe, and neutral to allies
                        if (DungeonScene.Instance.GetMatchup(controlledChar, target) == Alignment.Foe)
                            return -100;
                        return 0;
                    }
                    else if (effect is TransformEvent)
                    {
                        if (controlledChar.CurrentForm.Species == target.CurrentForm.Species)
                            return 0;
                    }
                    else if (effect is TransferStatusEvent)
                    {
                        TransferStatusEvent transferEffect = (TransferStatusEvent)effect;
                        int startVal = 0;
                        if (transferEffect.GoodStatus)
                        {
                            //only look for non-bad status
                            foreach (StatusEffect status in controlledChar.StatusEffects.Values)
                            {
                                if (!status.StatusStates.Contains<BadStatusState>())
                                {
                                    StackState stack;
                                    if (status.StatusStates.TryGet(out stack))
                                    {
                                        int existingStack = 0;
                                        StatusEffect existingStatus;
                                        if (target.StatusEffects.TryGetValue(status.ID, out existingStatus))
                                            existingStack = existingStatus.StatusStates.GetWithDefault<StackState>().Stack;
                                        startVal += calculateStatusStackWorth(status.ID, stack.Stack, existingStack);
                                    }
                                    else
                                    {
                                        if (!target.StatusEffects.ContainsKey(status.ID))
                                            startVal += 100;
                                    }
                                }
                            }
                        }
                        else
                        {
                            //only look for bad status
                            foreach (StatusEffect status in controlledChar.StatusEffects.Values)
                            {
                                if (status.StatusStates.Contains<BadStatusState>())
                                    startVal += 100;
                            }
                        }
                        return startVal;
                    }
                    else if (effect is ChangeToAbilityEvent)
                    {
                        //assume always pointed at foe, always detrimental
                        if (target.Intrinsics[0].Element.ID != ((ChangeToAbilityEvent)effect).TargetAbility)
                            return -100;
                        return 0;
                    }
                    else if (effect is ReflectAbilityEvent)
                    {
                        //assume always pointed at foe, always detrimental
                        if (target.Intrinsics[0].Element.ID != controlledChar.Intrinsics[0].Element.ID)
                            return -100;
                        return 0;
                    }
                    else if (effect is SketchBattleEvent)
                    {
                        return -100;
                    }
                    else if (effect is PowerTrickEvent)
                    {
                        if (target.ProxyAtk == -1 || target.ProxyDef == -1)
                        {
                            //beneficial to self/ally, detrimental to foe
                            if (DungeonScene.Instance.GetMatchup(controlledChar, target) != Alignment.Foe)
                                return 100;
                            else
                                return -100;
                        }
                        return 0;
                    }
                    else if (effect is SetItemStickyEvent)
                    {
                        if (!String.IsNullOrEmpty(target.EquippedItem.ID))
                        {
                            if (target.EquippedItem.Cursed)
                                return 0;
                            return -100;
                        }
                        int startVal = 100;
                        bool foundNonStick = false;
                        for (int ii = 0; ii < target.MemberTeam.GetInvCount(); ii++)
                        {
                            if (target.MemberTeam.GetInv(ii).Cursed)
                            {
                                if (startVal < 50)
                                    startVal -= 5;
                                else
                                    startVal -= 10;
                            }
                            else
                                foundNonStick = true;
                        }
                        if (!foundNonStick)
                            return 0;
                        return -Math.Max(startVal, 10);
                    }
                    else if (effect is BegItemEvent)
                    {
                        if (!String.IsNullOrEmpty(target.EquippedItem.ID))
                        {
                            if (target.EquippedItem.Cursed)
                                return 0;
                            return -100;
                        }

                        int startVal = 0;
                        for (int ii = 0; ii < target.MemberTeam.GetInvCount(); ii++)
                        {
                            if (startVal < 50)
                                startVal += 10;
                            else
                                startVal += 5;
                        }
                        return -startVal;
                    }
                    else if (effect is StatSplitEvent)
                    {
                        bool attackStats = ((StatSplitEvent)effect).AttackStats;
                        //higher self stats mean a positive effect on target, lower self stats mean a negative effect on the target
                        int statDiff;
                        if (attackStats)
                            statDiff = (controlledChar.Atk + controlledChar.MAtk) - (target.Atk + target.MAtk);
                        else
                            statDiff = (controlledChar.Def + controlledChar.MDef) - (target.Def + target.MDef);
                        //TODO: if the stat diff is below a certain threshold, do not bother
                        return statDiff;
                    }
                    else if (effect is SwapAbilityEvent)
                    {
                        //assume always pointed at foe, always detrimental
                        if (target.Intrinsics[0].Element.ID != controlledChar.Intrinsics[0].Element.ID &&
                            controlledChar.Intrinsics[0].Element.ID == controlledChar.BaseIntrinsics[0])
                            return -100;
                        return 0;
                    }
                    else if (effect is SwitcherEvent)
                    {
                        //assume always pointed at foe, always detrimental
                        return -100;
                    }
                    else if (effect is SwitchHeldItemEvent)
                    {
                        //assume always pointed at foe, always detrimental
                        //if both parties aren't holding an item, cancel
                        if (String.IsNullOrEmpty(target.EquippedItem.ID) && String.IsNullOrEmpty(controlledChar.EquippedItem.ID))
                            return 0;
                        //don't trade an uncursed item for a cursed one
                        if (target.EquippedItem.Cursed && !controlledChar.EquippedItem.Cursed)
                            return 0;
                        return -100;
                    }
                    else if (effect is AddElementEvent)
                    {
                        //assume always pointed at foe, always detrimental
                        if (!target.HasElement(((AddElementEvent)effect).TargetElement))
                            return -100;
                        return 0;
                    }
                    else if (effect is ChangeToElementEvent)
                    {
                        //assume always always detrimental to foe, always beneficial to ally
                        if (!target.HasElement(((ChangeToElementEvent)effect).TargetElement))
                        {
                            if (DungeonScene.Instance.GetMatchup(controlledChar, target) != Alignment.Foe)
                                return 50;
                            else//this is just a hack to prevent enemies from throwing type-changers but still use type-changing move on opponents
                                return -defaultVal;
                        }
                        return 0;
                    }
                    else if (effect is RestEvent)
                    {
                        int healHP = target.MaxHP;
                        int hpMissing = target.MaxHP - target.HP - healHP / 4;
                        return hpMissing * 200 / healHP;
                    }
                    else if (effect is WarpAlliesInEvent)
                    {
                        //don't warp if there are already 2 or more allies within sight
                        int foundAllies = 0;

                        foreach (Character character in seenChars)
                        {
                            if (DungeonScene.Instance.GetMatchup(controlledChar, character) == Alignment.Friend && ZoneManager.Instance.CurrentMap.InRange(character.CharLoc, controlledChar.CharLoc, 5))
                            {
                                foundAllies++;
                                if (foundAllies >= 2)
                                    return 0;
                            }
                        }
                        //beneficial for self, detrimental to foe
                        if (DungeonScene.Instance.GetMatchup(controlledChar, target) != Alignment.Foe)
                            return 100;
                        else
                            return -100;
                    }
                    else if (effect is GiveMapStatusEvent)
                    {
                        GiveMapStatusEvent giveEffect = (GiveMapStatusEvent)effect;
                        if (ZoneManager.Instance.CurrentMap.Status.ContainsKey(giveEffect.StatusID))
                            return 0;
                        else
                        {
                            //less chance to change the weather if it's currently a weather that you can change to
                            foreach (int ii in iterateUsableSkillIndices(controlledChar))
                            {
                                SkillData otherEntry = DataManager.Instance.GetSkill(controlledChar.Skills[ii].Element.SkillNum);
                                foreach (BattleEvent otherEffect in otherEntry.Data.OnHits.EnumerateInOrder())
                                {
                                    if (otherEffect is GiveMapStatusEvent)
                                    {
                                        GiveMapStatusEvent otherGiveEffect = (GiveMapStatusEvent)otherEffect;
                                        if (ZoneManager.Instance.CurrentMap.Status.ContainsKey(otherGiveEffect.StatusID))
                                            return 30;
                                    }
                                }
                            }

                            //beneficial for self, detrimental to foe
                            if (DungeonScene.Instance.GetMatchup(controlledChar, target) != Alignment.Foe)
                                return 100;
                            else
                                return -100;
                        }
                    }
                }

                //status checker
                bool givesStatus = false;
                int statusWorth = 0;
                foreach (BattleEvent effect in data.OnHits.EnumerateInOrder())
                {
                    if (effect is StatusBattleEvent)
                    {
                        givesStatus = true;
                        int addedWorth = 0;
                        StatusBattleEvent giveEffect = (StatusBattleEvent)effect;
                        StatusData statusData = DataManager.Instance.GetStatus(giveEffect.StatusID);
                        Character statusTarget = giveEffect.AffectTarget ? target : controlledChar;
                        StatusEffect existingStatus = statusTarget.GetStatusEffect(giveEffect.StatusID);
                        if (effect is StatusStackBattleEvent)
                        {
                            StatusStackBattleEvent stackEffect = (StatusStackBattleEvent)effect;
                            int existingStack = 0;
                            if (existingStatus != null)
                                existingStack = existingStatus.StatusStates.GetWithDefault<StackState>().Stack;
                            addedWorth = calculateStatusStackWorth(giveEffect.StatusID, stackEffect.Stack, existingStack);
                        }
                        else if (existingStatus == null)
                        {
                            //Not a stackable status, and it doesn't exist on the target yet
                            if (giveEffect.StatusID == "in_love")//attract NOTE: Specialized code!
                            {
                                if ((statusTarget.CurrentForm.Gender == Gender.Genderless) != (statusTarget.CurrentForm.Gender == controlledChar.CurrentForm.Gender))
                                {
                                    //failure
                                }
                                else
                                    addedWorth = 100;
                            }
                            else if (giveEffect.StatusID == "burn" && (IQ & AIFlags.KnowsMatchups) != AIFlags.None)//burn NOTE: specialized code!
                            {
                                if (!statusTarget.HasElement("fire"))
                                    addedWorth = 100;
                            }
                            else if (giveEffect.StatusID == "freeze" && (IQ & AIFlags.KnowsMatchups) != AIFlags.None)//freeze NOTE: specialized code!
                            {
                                if (!statusTarget.HasElement("ice"))
                                    addedWorth = 100;
                            }
                            else if (giveEffect.StatusID == "paralyze" && (IQ & AIFlags.KnowsMatchups) != AIFlags.None)//paralyze NOTE: specialized code!
                            {
                                if (!statusTarget.HasElement("electric"))
                                    addedWorth = 100;
                            }
                            else if ((giveEffect.StatusID == "poison" || giveEffect.StatusID == "poison_toxic") && (IQ & AIFlags.KnowsMatchups) != AIFlags.None)//poison NOTE: specialized code!
                            {
                                if (!statusTarget.HasElement("poison") && !statusTarget.HasElement("steel"))
                                    addedWorth = 100;
                            }
                            else if (giveEffect.StatusID == "immobilized" && (IQ & AIFlags.KnowsMatchups) != AIFlags.None)//immobilize NOTE: specialized code!
                            {
                                if (!statusTarget.HasElement("ghost"))
                                    addedWorth = 100;
                            }
                            else if (giveEffect.StatusID == "leech_seed" && (IQ & AIFlags.KnowsMatchups) != AIFlags.None)//leech seed NOTE: specialized code!
                            {
                                if (!statusTarget.HasElement("grass"))
                                    addedWorth = 100;
                            }
                            else if (giveEffect.StatusID == "aqua_ring")
                            {
                                if (target.HP <= target.MaxHP * 3 / 4)
                                    addedWorth = (target.MaxHP - target.HP) * 100 / target.MaxHP;
                            }
                            else if (giveEffect.StatusID == "regeneration")
                            {
                                if (target.HP <= target.MaxHP * 3 / 4)
                                    addedWorth = (target.MaxHP - target.HP) * 100 / target.MaxHP;
                            }
                            else if (giveEffect.StatusID == "disable")//disable NOTE: specialized code!
                            {
                                if (statusTarget.StatusEffects.ContainsKey("last_used_move_slot"))
                                    addedWorth = 100;
                            }
                            else if (giveEffect.StatusID == "decoy")//substitute; not necessarily a bad status, but treated like one
                                addedWorth = -100;
                            else
                            {
                                //for any other effect, assume it has a negative effect on foes, and positive effect on allies 
                                if (DungeonScene.Instance.GetMatchup(controlledChar, target) != Alignment.Foe)
                                    return 100;
                                else
                                    return -100;
                            }
                        }

                        if (statusData.StatusStates.Contains<BadStatusState>())
                            addedWorth *= -1;

                        statusWorth += addedWorth;
                    }
                }

                if (givesStatus)
                    return statusWorth;

                //for any other effect, assume it has a negative effect on foes, and positive effect on allies
                if (DungeonScene.Instance.GetMatchup(controlledChar, target) != Alignment.Foe)
                    return defaultVal;
                else
                    return -defaultVal;
            }
            else
            {

                //here, we check to make sure the attack can affect the target
                //x100 if it does something helpful
                //x-100 if it does something bad
                //x0 if it does nothing
                int power = 0;

                BasePowerState state = data.SkillStates.GetWithDefault<BasePowerState>();
                if (state != null)
                    power = state.Power * 2;

                power *= strikes;

                if (power == 0)
                    power = 100;

                if (controlledChar.HasElement(data.Element))
                {
                    power *= 4;
                    power /= 3;
                }

                foreach (BattleEvent effect in data.BeforeHits.EnumerateInOrder())
                {
                    if (effect is DistanceDropEvent)
                    {
                        int diff = ZoneManager.Instance.CurrentMap.GetClosestDist8(target.CharLoc, controlledChar.CharLoc);
                        for (int nn = 0; nn < diff; nn++)
                            power /= 2;
                    }
                }

                foreach (BattleEvent effect in data.OnHits.EnumerateInOrder())
                {
                    if (effect is CutHPDamageEvent)
                    {
                        power = 200 * target.HP / target.MaxHP;
                    }
                    else if (effect is UserHPDamageEvent)
                    {
                        if (((UserHPDamageEvent)effect).Reverse)
                            power = 200 - 200 * controlledChar.HP / controlledChar.MaxHP;
                        else
                            power = 200 * controlledChar.HP / controlledChar.MaxHP;

                        if (power < 20)
                            power = 0;
                    }
                    else if (effect is EndeavorEvent)
                        power = 200 * Math.Max(0, target.HP - controlledChar.HP) / target.MaxHP;
                    else if (effect is FutureAttackEvent)
                    {
                        StatusEffect existingStatus = target.GetStatusEffect(((FutureAttackEvent)effect).StatusID);
                        if (existingStatus != null)
                            power = 0;
                    }
                    else if (effect is OnHitEvent)
                    {
                        foreach (BattleEvent baseEffect in ((OnHitEvent)effect).BaseEvents)
                        {
                            if (baseEffect is GiveContinuousDamageEvent)
                                power *= 3;
                            else if (baseEffect is StatusBattleEvent)
                            {
                                //note: specialized code mainly for trapping attacks!
                                //this part is usually only hit for trapping attacks!
                                StatusBattleEvent giveEffect = (StatusBattleEvent)baseEffect;
                                Character statusTarget = giveEffect.AffectTarget ? target : controlledChar;
                                StatusEffect existingStatus = statusTarget.GetStatusEffect(giveEffect.StatusID);
                                if (existingStatus == null)
                                    power += 100;
                            }
                        }
                    }
                }

                //check against move-neutralizing abilities; NOTE: specialized AI code!
                if ((IQ & AIFlags.KnowsMatchups) != AIFlags.None)
                {
                    foreach (PassiveContext passive in target.IteratePassives(GameEventPriority.TARGET_PORT_PRIORITY))
                    {
                        foreach (BattleEvent effect in passive.EventData.BeforeBeingHits.EnumerateInOrder())
                        {
                            if (effect is AbsorbElementEvent)
                            {
                                AbsorbElementEvent absorbEffect = (AbsorbElementEvent)effect;
                                if (absorbEffect.AbsorbElement == data.Element)
                                {
                                    bool redundantStatus = false;
                                    foreach (BattleEvent result in absorbEffect.BaseEvents)
                                    {
                                        if (result is StatusElementBattleEvent)
                                        {
                                            StatusElementBattleEvent absorbResult = (StatusElementBattleEvent)result;
                                            if (target.StatusEffects.ContainsKey(absorbResult.StatusID))
                                                redundantStatus = true;
                                        }
                                    }
                                    if (redundantStatus)
                                        power = 0;
                                    else
                                        power /= -2;
                                    break;
                                }
                            }
                        }
                    }
                }

                int matchup = PreTypeEvent.GetDualEffectiveness(controlledChar, target, data);
                power *= PreTypeEvent.GetEffectivenessMult(matchup);
                power /= PreTypeEvent.GetEffectivenessMult(PreTypeEvent.NRM_2);

                //positive power means positive damage, meaning negative effect
                return -power;
            }
        }

        private int calculateStatusStackWorth(string statusID, int stack, int existingStack)
        {
            int addedWorth = 64;
            StatusData statusEntry = DataManager.Instance.GetStatus(statusID);
            int minStack = 0;
            int maxStack = 0;
            foreach (StatusGivenEvent beforeEffect in statusEntry.BeforeStatusAdds.EnumerateInOrder())
            {
                if (beforeEffect is StatusStackCheck)
                {
                    minStack = ((StatusStackCheck)beforeEffect).Minimum;
                    maxStack = ((StatusStackCheck)beforeEffect).Maximum;
                }
            }
            if (stack > 0)
            {
                //positive stack implies a positive effect
                int addableStack = Math.Min(stack, maxStack - existingStack);
                addedWorth *= addableStack;
                addedWorth /= stack;

                for (int ii = 0; ii < existingStack; ii++)
                    addedWorth /= 2;
            }
            else if (stack < 0)
            {
                //Speed drop below -1 is considered worthless for enemies. NOTE: specialized code!
                if (statusID == "mod_speed" && (IQ & AIFlags.KnowsMatchups) == AIFlags.None)
                    minStack = -1;

                //negative stack implies a negative effect
                addedWorth *= -1;
                int addableStack = Math.Max(stack, minStack - existingStack);
                //addedWorth will always be multiplied and divided by a negative number, resulting in no sign change
                addedWorth *= addableStack;
                addedWorth /= stack;

                for (int ii = 0; ii < -existingStack; ii++)
                    addedWorth /= 2;
            }
            return addedWorth;
        }

        protected List<Loc> GetAreaExits(Character controlledChar)
        {
            //get all tiles that are within the border of sight range, or within the border of the screen
            Loc seen = Character.GetSightDims();

            List<Loc> loc_list = new List<Loc>();
            //currently, CPU sight cheats by knowing tiles up to the bounds, instead of individual tiles at the border of FOV.
            //fix later
            for (int x = -seen.X; x <= seen.X; x++)
            {
                TryAddDest(controlledChar, loc_list, new Loc(controlledChar.CharLoc.X + x, controlledChar.CharLoc.Y - seen.Y));
                TryAddDest(controlledChar, loc_list, new Loc(controlledChar.CharLoc.X + x, controlledChar.CharLoc.Y + seen.Y));

            }
            for (int y = -seen.Y; y <= seen.Y; y++)
            {
                TryAddDest(controlledChar, loc_list, new Loc(controlledChar.CharLoc.X - seen.X, controlledChar.CharLoc.Y + y));
                TryAddDest(controlledChar, loc_list, new Loc(controlledChar.CharLoc.X + seen.X, controlledChar.CharLoc.Y + y));
            }
            return loc_list;
        }

        protected void TryAddDest(Character controlledChar, List<Loc> loc_list, Loc border_loc)
        {
            if (ZoneManager.Instance.CurrentMap.TileBlocked(border_loc, controlledChar.Mobility, false))
                return;

            if (BlockedByTrap(controlledChar, border_loc))
                return;
            if (BlockedByHazard(controlledChar, border_loc))
                return;

            loc_list.Add(border_loc);
        }
    }

    public struct ActionDirValue
    {
        public GameAction Action;
        public HitValue Hit;

        public ActionDirValue(GameAction action, HitValue hit)
        {
            Action = action;
            Hit = hit;
        }
    }

    public struct HitValue
    {
        public int Value;
        public bool DirectHit;
        public bool ImaginedHit;

        public HitValue(int value, bool direct)
        {
            Value = value;
            DirectHit = direct;
            ImaginedHit = false;
        }

        public HitValue(int value, bool direct, bool imagined)
        {
            Value = value;
            DirectHit = direct;
            ImaginedHit = imagined;
        }

        public int CompareTo(HitValue other)
        {
            int val = Value.CompareTo(other.Value);
            if (val != 0)
                return val;

            int direct = DirectHit.CompareTo(other.DirectHit);
            if (direct != 0)
                return direct;

            return 0;
        }
    }

    public class RangeTarget
    {
        public int Weight;
        public Character Origin;

        public RangeTarget(Character origin, int weight)
        {
            Origin = origin;
            Weight = weight;
        }
    }
}
