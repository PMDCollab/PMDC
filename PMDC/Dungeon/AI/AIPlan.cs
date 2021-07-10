using System;
using System.Collections.Generic;
using RogueElements;
using RogueEssence.Data;
using RogueEssence;
using RogueEssence.Dungeon;

namespace PMDC.Dungeon
{
    [Flags]
    public enum AIFlags
    {
        None = 0,
        FriendlyFire = 1,
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
        /// HAs the sensibilities of a player team's ally
        /// will not walk into silcoon/cascoon
        /// will not hit allies even if it's worth it to hit more foes
        /// will not attack enemyoffriend
        /// will not path to the last seen location of an enemy if it finds no enemies
        /// will not attack or target certain AI
        /// will not attack or target sleepers and frozen, full stop
        /// </summary>
        TeamPartner = 256,
        /// <summary>
        /// Will attack enemy units if in a neutral faction, and vice versa
        /// </summary>
        NeutralFoeConflict = 512,
    }

    public struct ActionValue
    {
        public int Value;
        public bool DirectHit;

        public ActionValue(int value, bool direct)
        {
            Value = value;
            DirectHit = direct;
        }

        public int CompareTo(ActionValue other)
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

    [Serializable]
    public abstract class AIPlan : BasePlan
    {
        /// <summary>
        /// The strategy that the monster takes when it goes aggro
        /// </summary>
        public AIFlags IQ;

        public enum AttackChoice
        {
            SmartAttack,//always chooses the best move, and always attacks when within range
            RandomAttack,//randomly chooses moves based on weight, always attacks when within range
            RandomApproach,//randomly chooses moves based on weight, sometimes walks when within range
            DumbApproach//
        }

        public AIPlan() { }
        //public AIPlan(AIFlags iq) : this(iq, true, AttackChoice.SmartAttack) { }

        public AIPlan(AIFlags iq)
        {
            this.IQ = iq;
        }

        protected AIPlan(AIPlan other)
        {
            IQ = other.IQ;
        }

        private bool allowedToPass(CharIndex myCharIndex, CharIndex charIndex, bool respectLeaders)
        {
            Character controlledChar = ZoneManager.Instance.CurrentMap.LookupCharIndex(myCharIndex);
            Character otherChar = ZoneManager.Instance.CurrentMap.LookupCharIndex(charIndex);

            Faction foeFaction = (IQ & AIFlags.NeutralFoeConflict) != AIFlags.None ? Faction.Foe : Faction.None;
            if (DungeonScene.Instance.GetMatchup(controlledChar, otherChar, foeFaction, false) == Alignment.Foe)
                return false;
            else if (!respectLeaders)
                return true;
            else
                return false;
        }

        protected bool BlockedByChar(Loc testLoc, Alignment alignment)
        {
            if ((alignment & Alignment.Self) != Alignment.None)
            {
                foreach (Character chara in ZoneManager.Instance.CurrentMap.ActiveTeam.EnumerateChars())
                {
                    if (!chara.Dead && chara.CharLoc == testLoc)
                        return true;
                }
            }

            if ((alignment & Alignment.Friend) != Alignment.None)
            {
                for (int ii = 0; ii < ZoneManager.Instance.CurrentMap.AllyTeams.Count; ii++)
                {
                    foreach (Character chara in ZoneManager.Instance.CurrentMap.AllyTeams[ii].EnumerateChars())
                    {
                        if (!chara.Dead && chara.CharLoc == testLoc)
                            return true;
                    }
                }
            }

            if ((alignment & Alignment.Foe) != Alignment.None)
            {
                for (int ii = 0; ii < ZoneManager.Instance.CurrentMap.MapTeams.Count; ii++)
                {
                    foreach (Character chara in ZoneManager.Instance.CurrentMap.MapTeams[ii].EnumerateChars())
                    {
                        if (!chara.Dead && chara.CharLoc == testLoc)
                            return true;
                    }
                }
            }

            return false;
        }

        protected bool BlockedByTrap(Character controlledChar, Loc testLoc)
        {
            if ((IQ & AIFlags.TrapAvoider) == AIFlags.None)
                return false;

            Tile tile = ZoneManager.Instance.CurrentMap.Tiles[testLoc.X][testLoc.Y];
            if (tile.Effect.ID > -1)
            {
                TileData entry = DataManager.Instance.GetTile(tile.Effect.ID);
                if (entry.StepType == TileData.TriggerType.Trap || entry.StepType == TileData.TriggerType.Site || entry.StepType == TileData.TriggerType.Switch)
                    return true;
            }
            TerrainData terrain = tile.Data.GetData();
            if (tile.Data.ID == 4 && !controlledChar.HasElement(07))//check for lava; NOTE: specialized AI code!
                return true;
            if (tile.Data.ID == 6 && !controlledChar.HasElement(14) && !controlledChar.HasElement(17))//check for poison; NOTE: specialized AI code!
                return true;
            if (tile.Data.ID == 2 && controlledChar.MemberTeam is ExplorerTeam)//check for block; NOTE: specialized AI code!
                return true;

            return false;
        }

        protected bool BlockedByHazard(Character controlledChar, Loc testLoc)
        {
            if ((IQ & AIFlags.TeamPartner) == AIFlags.None)
                return false;
            //TODO: pass in the list of seen characters instead of computing them on the spot
            //this is very expensive to do, and the only reason the game isn't lagging is because this check is only called for ally characters!
            Faction foeFaction = (IQ & AIFlags.NeutralFoeConflict) != AIFlags.None ? Faction.Foe : Faction.None;
            List<Character> seenChars = controlledChar.GetSeenCharacters(Alignment.Foe, foeFaction);
            foreach (Character seenChar in seenChars)
            {
                if (seenChar.Tactic.ID == 8 && (seenChar.CharLoc - testLoc).Dist8() <= 1 && seenChar.GetStatusEffect(25) == null)//do not approach silcoon/cascoon; NOTE: specialized AI code!
                    return true;
            }
            return false;
        }

        protected Alignment GetAcceptableTargets()
        {
            Alignment target = Alignment.Foe;
            if ((IQ & AIFlags.Cannibal) != AIFlags.None)
                target |= Alignment.Friend;
            return target;
        }

        /// <summary>
        /// Gets the path directly to a target
        /// </summary>
        /// <param name="controlledChar"></param>
        /// <param name="end"></param>
        /// <param name="freeGoal">Determines whether the goal should be reachable even if blocked.</param>
        /// <param name="respectPeers"></param>
        /// <returns></returns>
        protected List<Loc>[] GetPaths(Character controlledChar, Loc[] ends, bool freeGoal, bool respectPeers)
        {

            //requires a valid target tile
            Grid.LocTest checkDiagBlock = (Loc loc) => {
                return (ZoneManager.Instance.CurrentMap.TileBlocked(loc, controlledChar.Mobility, true));
                //enemy/ally blockings don't matter for diagonals
            };

            Grid.LocTest checkBlock = (Loc testLoc) => {

                if (freeGoal)
                {
                    foreach (Loc end in ends)
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

                if (respectPeers && BlockedByChar(testLoc, Alignment.Self | Alignment.Foe))
                    return true;

                return false;
            };


            Loc mapStart = controlledChar.CharLoc - Character.GetSightDims();
            return Grid.FindNPaths(mapStart, Character.GetSightDims() * 2 + new Loc(1), controlledChar.CharLoc, ends, checkBlock, checkDiagBlock, 1, true);
        }

        protected List<Loc> GetPathPermissive(Character controlledChar, List<Loc> ends)
        {

            //requires a valid target tile
            Grid.LocTest checkDiagBlock = (Loc testLoc) => {
                return (ZoneManager.Instance.CurrentMap.TileBlocked(testLoc, controlledChar.Mobility, true));
                //enemy/ally blockings don't matter for diagonals
            };

            Grid.LocTest checkBlock = (Loc testLoc) => {

                foreach (Loc end in ends)
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

                if (BlockedByChar(testLoc, Alignment.Self))
                    return true;

                return false;
            };


            Loc mapStart = controlledChar.CharLoc - Character.GetSightDims();
            return Grid.FindAPath(mapStart, Character.GetSightDims() * 2 + new Loc(1), controlledChar.CharLoc, ends.ToArray(), checkBlock, checkDiagBlock);
        }

        protected GameAction SelectChoiceFromPath(Character controlledChar, List<Loc> path)
        {
            if (path.Count <= 1)
                return new GameAction(GameAction.ActionType.Wait, Dir8.None);
            else
                return TrySelectWalk(controlledChar, DirExt.GetDir(path[path.Count - 1], path[path.Count - 2]));
        }

        protected GameAction TrySelectWalk(Character controlledChar, Dir8 dir)
        {
            //assumes that this direction was checked for blocking, and no-walking
            return new GameAction(GameAction.ActionType.Move, dir, ((IQ & AIFlags.ItemGrabber) != AIFlags.None) ? 1 : 0);
        }

        protected GameAction TryAttackChoice(ReRandom rand, Character controlledChar, Dir8 defaultDir, AttackChoice attackPattern)
        {
            List<Character> seenChars = controlledChar.GetSeenCharacters(Alignment.Self | Alignment.Friend | Alignment.Foe, Faction.None);

            bool seesDanger = false;
            foreach (Character seenChar in seenChars)
            {
                Faction foeFaction = (IQ & AIFlags.NeutralFoeConflict) != AIFlags.None ? Faction.Foe : Faction.None;
                bool canBetray = (IQ & AIFlags.TeamPartner) == AIFlags.None;
                if ((DungeonScene.Instance.GetMatchup(controlledChar, seenChar, foeFaction, canBetray) & GetAcceptableTargets()) != Alignment.None)
                    seesDanger = true;
            }
            if (!seesDanger)
                return new GameAction(GameAction.ActionType.Wait, Dir8.None);


            if (controlledChar.AttackOnly)
                return TryForcedAttackChoice(rand, controlledChar, seenChars);

            if (attackPattern == AttackChoice.DumbApproach)
                return TryDumbAttackChoice(rand, controlledChar, defaultDir, seenChars);
            else if (attackPattern == AttackChoice.RandomAttack)
                return TryRandomMoveChoice(rand, controlledChar, defaultDir, seenChars, false);
            else if (attackPattern == AttackChoice.RandomApproach)
                return TryRandomMoveChoice(rand, controlledChar, defaultDir, seenChars, true);
            else
                return TryBestAttackChoice(rand, controlledChar, defaultDir, seenChars);
        }

        private IEnumerable<int> iterateUsableSkillIndices(Character controlledChar)
        {
            for (int ii = 0; ii < controlledChar.Skills.Count; ii++)
            {
                if (controlledChar.Skills[ii].Element.SkillNum > -1 && controlledChar.Skills[ii].Element.Charges > 0 && !controlledChar.Skills[ii].Element.Sealed && controlledChar.Skills[ii].Element.Enabled)
                    yield return ii;
            }
        }

        private void updateDistanceTargetHash(Dictionary<Loc, RangeTarget> endHash, Character chara, Loc diff)
        {
            int weight = diff.Dist8();
            Loc loc = chara.CharLoc + diff;
            if (endHash.ContainsKey(loc))
            {
                if (weight < endHash[loc].Weight)
                    endHash[loc] = new RangeTarget(chara, weight);
            }
            else
                endHash[loc] = new RangeTarget(chara, weight);
        }

        protected GameAction TryDumbAttackChoice(ReRandom rand, Character controlledChar, Dir8 defaultDir, List<Character> seenChars)
        {
            List<Tuple<int, ActionValue>> moveIndices = new List<Tuple<int, ActionValue>>();
            foreach (int ii in iterateUsableSkillIndices(controlledChar))
            {
                ActionValue[] moveDirs = new ActionValue[8];
                GetActionValues(controlledChar, defaultDir, seenChars, controlledChar.Skills[ii].Element.SkillNum, moveDirs);

                SkillData entry = DataManager.Instance.GetSkill(controlledChar.Skills[ii].Element.SkillNum);
                UpdateTotalIndices(rand, moveIndices, ii, moveDirs);
            }

            List<Tuple<int, ActionValue>> backupIndices = new List<Tuple<int, ActionValue>>();
            //default on attacking if no moves are to be found
            {
                ActionValue[] attackDirs = new ActionValue[8];
                GetActionValues(controlledChar, defaultDir, seenChars, 0, attackDirs);
                for (int ii = 0; ii < attackDirs.Length; ii++)
                    attackDirs[ii].Value = attackDirs[ii].Value * 2;
                UpdateTotalIndices(rand, moveIndices, CharData.MAX_SKILL_SLOTS, attackDirs);
                for (int ii = 0; ii < attackDirs.Length; ii++)
                    attackDirs[ii].Value = attackDirs[ii].Value;
                UpdateTotalIndices(rand, backupIndices, CharData.MAX_SKILL_SLOTS, attackDirs);
            }

            //just try to choose once
            if (moveIndices.Count > 0)
            {
                //get a random move based on weighted chance
                int totalPoints = 0;
                for (int ii = 0; ii < moveIndices.Count; ii++)
                    totalPoints += moveIndices[ii].Item2.Value;

                int pointChoice = rand.Next(totalPoints);
                int choice = 0;
                while (choice < moveIndices.Count)
                {
                    if (pointChoice < moveIndices[choice].Item2.Value)
                        break;
                    pointChoice -= moveIndices[choice].Item2.Value;
                    choice++;
                }

                //if the move is a "false hit", that means it won't have any effect, but had a value that meant it could if it was in range; so move up if not in range

                int chosenMove = moveIndices[choice].Item1;
                int moveIndex = chosenMove / 8;
                int dirIndex = chosenMove % 8;
                if (moveIndex < CharData.MAX_SKILL_SLOTS)
                    return new GameAction(GameAction.ActionType.UseSkill, (Dir8)dirIndex, moveIndex);
                else
                    return new GameAction(GameAction.ActionType.Attack, (Dir8)dirIndex);
            }

            //if that attempt failed, because we hit a move that would hit no one, then we default to attempting attack
            if (backupIndices.Count > 0)
            {
                int chosenMove = backupIndices[rand.Next(backupIndices.Count)].Item1;
                int moveIndex = chosenMove / 8;
                int dirIndex = chosenMove % 8;
                if (moveIndex < CharData.MAX_SKILL_SLOTS)
                    return new GameAction(GameAction.ActionType.UseSkill, (Dir8)dirIndex, moveIndex);
                else
                    return new GameAction(GameAction.ActionType.Attack, (Dir8)dirIndex);
            }
            //if we can't attack, then we pass along to movement
            return new GameAction(GameAction.ActionType.Wait, Dir8.None);
        }

        protected GameAction TryRandomMoveChoice(ReRandom rand, Character controlledChar, Dir8 defaultDir, List<Character> seenChars, bool allowApproach)
        {
            List<Tuple<int, ActionValue>> moveIndices = new List<Tuple<int, ActionValue>>();
            List<Tuple<int, ActionValue>> attackMoveIndices = new List<Tuple<int, ActionValue>>();
            foreach (int ii in iterateUsableSkillIndices(controlledChar))
            {
                ActionValue[] moveDirs = new ActionValue[8];
                GetActionValues(controlledChar, defaultDir, seenChars, controlledChar.Skills[ii].Element.SkillNum, moveDirs);

                SkillData entry = DataManager.Instance.GetSkill(controlledChar.Skills[ii].Element.SkillNum);
                UpdateTotalIndices(rand, moveIndices, ii, moveDirs);
                if (entry.Data.Category != BattleData.SkillCategory.Status)
                    UpdateTotalIndices(rand, attackMoveIndices, ii, moveDirs);
            }

            List<Tuple<int, ActionValue>> backupIndices = new List<Tuple<int, ActionValue>>();
            //default on attacking if no moves are to be found
            {
                ActionValue[] attackDirs = new ActionValue[8];

                //setting approach to false will result in an AI that will NEVER favor walking forwards over attacking
                //"I can already hit you from where I am, so you're gonna have to come to me"
                if (allowApproach)
                {
                    GetActionValues(controlledChar, defaultDir, seenChars, 0, attackDirs);
                    for (int ii = 0; ii < attackDirs.Length; ii++)
                        attackDirs[ii].Value = attackDirs[ii].Value * 2;
                    UpdateTotalIndices(rand, moveIndices, CharData.MAX_SKILL_SLOTS, attackDirs);
                    for (int ii = 0; ii < attackDirs.Length; ii++)
                        attackDirs[ii].Value = attackDirs[ii].Value;
                    UpdateTotalIndices(rand, backupIndices, CharData.MAX_SKILL_SLOTS, attackDirs);
                }
                else
                {
                    GetActionValues(controlledChar, defaultDir, seenChars, 0, attackDirs);
                    UpdateTotalIndices(rand, backupIndices, CharData.MAX_SKILL_SLOTS, attackDirs);
                }
            }


            if (moveIndices.Count > 0)
            {
                //get a random move based on weighted chance
                int totalPoints = 0;
                for (int ii = 0; ii < moveIndices.Count; ii++)
                    totalPoints += moveIndices[ii].Item2.Value;

                int pointChoice = rand.Next(totalPoints);
                int choice = 0;
                while (choice < moveIndices.Count)
                {
                    if (pointChoice < moveIndices[choice].Item2.Value)
                        break;
                    pointChoice -= moveIndices[choice].Item2.Value;
                    choice++;
                }

                //if the move is a "false hit", that means it won't have any effect, but had a value that meant it could if it was in range; so move up if not in range
                //also, prevent attacking in any way in this step.
                if (moveIndices[choice].Item1 / 8 < CharData.MAX_SKILL_SLOTS)
                {
                    int chosenMove = moveIndices[choice].Item1;
                    int moveIndex = chosenMove / 8;
                    int dirIndex = chosenMove % 8;
                    if (moveIndex < CharData.MAX_SKILL_SLOTS)
                        return new GameAction(GameAction.ActionType.UseSkill, (Dir8)dirIndex, moveIndex);
                    else
                        return new GameAction(GameAction.ActionType.Attack, (Dir8)dirIndex);
                }
                else if (backupIndices.Count == 0)//check if attack isn't an option; in which case just move forward
                    return new GameAction(GameAction.ActionType.Wait, Dir8.None);
            }

            //try again since regular attack is a last resort, but only if we have attacking moves; we've abandoned using status moves at this point in code
            if (attackMoveIndices.Count > 0)
            {
                int totalPoints = 0;
                for (int ii = 0; ii < attackMoveIndices.Count; ii++)
                    totalPoints += attackMoveIndices[ii].Item2.Value;

                int pointChoice = rand.Next(totalPoints);
                int choice = 0;
                while (choice < attackMoveIndices.Count)
                {
                    if (pointChoice < attackMoveIndices[choice].Item2.Value)
                        break;
                    pointChoice -= attackMoveIndices[choice].Item2.Value;
                    choice++;
                }

                if (choice < attackMoveIndices.Count)
                {
                    int chosenMove = attackMoveIndices[choice].Item1;
                    int moveIndex = chosenMove / 8;
                    int dirIndex = chosenMove % 8;
                    if (moveIndex < CharData.MAX_SKILL_SLOTS)
                        return new GameAction(GameAction.ActionType.UseSkill, (Dir8)dirIndex, moveIndex);
                    else
                        return new GameAction(GameAction.ActionType.Attack, (Dir8)dirIndex);
                }
            }

            //no attacking moves?  how about regular attack?
            if (backupIndices.Count > 0)
            {
                int chosenMove = backupIndices[rand.Next(backupIndices.Count)].Item1;
                int moveIndex = chosenMove / 8;
                int dirIndex = chosenMove % 8;
                if (moveIndex < CharData.MAX_SKILL_SLOTS)
                    return new GameAction(GameAction.ActionType.UseSkill, (Dir8)dirIndex, moveIndex);
                else
                    return new GameAction(GameAction.ActionType.Attack, (Dir8)dirIndex);
            }
            //not even regular attack?  we leave it to the backup plans...
            return new GameAction(GameAction.ActionType.Wait, Dir8.None);
        }

        protected GameAction TryBestAttackChoice(ReRandom rand, Character controlledChar, Dir8 defaultDir, List<Character> seenChars)
        {
            int highestScore = 1;
            int highestStatusScore = 1;
            List<int> highestIndices = new List<int>();
            List<int> highestStatusIndices = new List<int>();
            foreach (int ii in iterateUsableSkillIndices(controlledChar))
            {
                ActionValue[] moveDirs = new ActionValue[8];
                GetActionValues(controlledChar, defaultDir, seenChars, controlledChar.Skills[ii].Element.SkillNum, moveDirs);

                SkillData entry = DataManager.Instance.GetSkill(controlledChar.Skills[ii].Element.SkillNum);
                if (entry.Data.Category == BattleData.SkillCategory.Status)
                    UpdateHighestIndices(ref highestStatusScore, highestStatusIndices, ii, moveDirs);
                else
                    UpdateHighestIndices(ref highestScore, highestIndices, ii, moveDirs);
            }

            {
                ActionValue[] attackDirs = new ActionValue[8];
                GetActionValues(controlledChar, defaultDir, seenChars, 0, attackDirs);
                UpdateHighestIndices(ref highestScore, highestIndices, CharData.MAX_SKILL_SLOTS, attackDirs);
            }

            if (highestIndices.Count > 0 || highestStatusIndices.Count > 0)
            {
                int randIndex = rand.Next(highestIndices.Count + highestStatusIndices.Count);
                int chosenMove = (randIndex < highestIndices.Count) ? highestIndices[randIndex] : highestStatusIndices[randIndex - highestIndices.Count];
                int moveIndex = chosenMove / 8;
                int dirIndex = chosenMove % 8;
                if (moveIndex < CharData.MAX_SKILL_SLOTS)
                    return new GameAction(GameAction.ActionType.UseSkill, (Dir8)dirIndex, moveIndex);
                else
                    return new GameAction(GameAction.ActionType.Attack, (Dir8)dirIndex);
            }

            return new GameAction(GameAction.ActionType.Wait, Dir8.None);
        }

        protected GameAction TryForcedAttackChoice(ReRandom rand, Character controlledChar, List<Character> seenChars)
        {
            //check to see if currently under a force-move status
            //if so, aim a regular attack according to that move
            int forcedMove = -1;
            foreach (int status in controlledChar.StatusEffects.Keys)
            {
                StatusData entry = DataManager.Instance.GetStatus(status);
                foreach (BattleEvent effect in entry.BeforeTryActions)
                {
                    if (effect is ForceMoveEvent)
                    {
                        forcedMove = ((ForceMoveEvent)effect).MoveIndex;
                        break;
                    }
                }
                if (forcedMove > -1)
                    break;
            }


            int highestScore = 1;
            int highestStatusScore = 1;
            List<int> highestIndices = new List<int>();
            List<int> highestStatusIndices = new List<int>();

            if (forcedMove > -1)
            {
                ActionValue[] moveDirs = new ActionValue[8];
                GetActionValues(controlledChar, Dir8.None, seenChars, forcedMove, moveDirs);

                SkillData entry = DataManager.Instance.GetSkill(forcedMove);
                if (entry.Data.Category == BattleData.SkillCategory.Status)
                    UpdateHighestIndices(ref highestStatusScore, highestStatusIndices, CharData.MAX_SKILL_SLOTS, moveDirs);
                else
                    UpdateHighestIndices(ref highestScore, highestIndices, CharData.MAX_SKILL_SLOTS, moveDirs);
            }
            else
            {
                //default on attacking if no moves are to be found
                ActionValue[] attackDirs = new ActionValue[8];
                GetActionValues(controlledChar, Dir8.None, seenChars, 0, attackDirs);
                UpdateHighestIndices(ref highestScore, highestIndices, CharData.MAX_SKILL_SLOTS, attackDirs);
            }

            if (highestIndices.Count > 0 || highestStatusIndices.Count > 0)
            {
                int randIndex = rand.Next(highestIndices.Count + highestStatusIndices.Count);
                int chosenMove = (randIndex < highestIndices.Count) ? highestIndices[randIndex] : highestStatusIndices[randIndex - highestIndices.Count];
                int dirIndex = chosenMove % 8;
                return new GameAction(GameAction.ActionType.Attack, (Dir8)dirIndex);
            }

            if (controlledChar.CantWalk)
                return new GameAction(GameAction.ActionType.Attack, Dir8.None);
            else
                return new GameAction(GameAction.ActionType.Wait, Dir8.None);
        }

        protected void UpdateHighestIndices(ref int highestScore, List<int> highestIndices, int moveIndex, ActionValue[] attackDirs)
        {
            for (int ii = 0; ii < attackDirs.Length; ii++)
            {
                if (attackDirs[ii].Value > highestScore)
                {
                    highestIndices.Clear();
                    highestIndices.Add(ii + moveIndex * attackDirs.Length);
                    highestScore = attackDirs[ii].Value;
                }
                else if (attackDirs[ii].Value == highestScore)
                    highestIndices.Add(ii + moveIndex * attackDirs.Length);
            }
        }

        protected void UpdateTotalIndices(ReRandom rand, List<Tuple<int, ActionValue>> totalIndices, int moveIndex, ActionValue[] attackDirs)
        {
            ActionValue highestScore = new ActionValue(0, false);
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
                totalIndices.Add(new Tuple<int, ActionValue>(highestDir + moveIndex * attackDirs.Length, highestScore));
            }
        }

        protected void GetActionValues(Character controlledChar, Dir8 defaultDir, List<Character> seenChars, int moveIndex, ActionValue[] dirs)
        {
            SkillData entry = DataManager.Instance.GetSkill(moveIndex);

            if (entry.HitboxAction is AreaAction && ((AreaAction)entry.HitboxAction).HitArea == Hitbox.AreaLimit.Full
                || entry.HitboxAction is SelfAction || entry.HitboxAction is ProjectileAction && ((ProjectileAction)entry.HitboxAction).Rays == ProjectileAction.RayCount.Eight)
            {
                if (defaultDir == Dir8.None)
                    defaultDir = controlledChar.CharDir;
                ActionValue highestVal = GetActionDirValue(moveIndex, entry, controlledChar, seenChars, defaultDir);
                dirs[(int)defaultDir] = highestVal;
            }
            else
            {
                ActionValue[] vals = new ActionValue[8];
                ActionValue highestVal = new ActionValue(0, false);

                //get the values of firing off an attack in the given direction, keeping track of the highest value
                for (int ii = 0; ii < DirExt.DIR8_COUNT; ii++)
                {
                    vals[ii] = GetActionDirValue(moveIndex, entry, controlledChar, seenChars, (Dir8)ii);
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
        }


        protected void FillRangeTargets(Character controlledChar, List<Character> seenCharacters, Dictionary<Loc, RangeTarget> endHash)
        {
            foreach (Character seenChar in seenCharacters)
            {
                foreach (int skillSlot in iterateUsableSkillIndices(controlledChar))
                {
                    int skillIndex = controlledChar.Skills[skillSlot].Element.SkillNum;
                    SkillData entry = DataManager.Instance.GetSkill(skillIndex);

                    int rangeMod = 0;

                    CombatAction hitboxAction = entry.HitboxAction;
                    ExplosionData explosion = entry.Explosion;

                    Dir8 approxDir = (seenChar.CharLoc - controlledChar.CharLoc).ApproximateDir8();
                    getActionHitboxes(controlledChar, seenCharacters, approxDir, ref skillIndex, ref entry, ref rangeMod, ref hitboxAction, ref explosion);

                    if (entry.HitboxAction is AreaAction || entry.HitboxAction is ThrowAction)
                    {
                        AreaAction areaAction = entry.HitboxAction as AreaAction;
                        int range = Math.Max(1, areaAction.Range + rangeMod);
                        //add everything in radius
                        for (int xx = -range; xx <= range; xx++)
                        {
                            for (int yy = -range; yy <= range; yy++)
                                updateDistanceTargetHash(endHash, seenChar, new Loc(xx, yy));
                        }
                    }
                    else if (entry.HitboxAction is LinearAction)
                    {
                        LinearAction lineAction = entry.HitboxAction as LinearAction;
                        int range = Math.Max(1, lineAction.Range + rangeMod);
                        //add everything in line
                        for (int ii = 0; ii < DirExt.DIR8_COUNT; ii++)
                        {
                            Dir8 dir = (Dir8)ii;
                            for (int jj = 1; jj <= range; jj++)
                            {
                                updateDistanceTargetHash(endHash, seenChar, dir.GetLoc() * jj);
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
                                    updateDistanceTargetHash(endHash, seenChar, left.GetLoc() + dir.GetLoc() * jj);
                                    updateDistanceTargetHash(endHash, seenChar, right.GetLoc() + dir.GetLoc() * jj);
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
                                    updateDistanceTargetHash(endHash, seenChar, dir.GetLoc() * range);
                                    break;
                                case OffsetAction.OffsetArea.Sides:
                                    {
                                        updateDistanceTargetHash(endHash, seenChar, dir.GetLoc() * range);
                                        Dir8 left = DirExt.AddAngles(dir, Dir8.Left);
                                        Dir8 right = DirExt.AddAngles(dir, Dir8.Right);
                                        if (dir.IsDiagonal())
                                        {
                                            left = DirExt.AddAngles(dir, Dir8.UpLeft);
                                            right = DirExt.AddAngles(dir, Dir8.UpRight);
                                        }
                                        //add everything on the sides if it's a wide action
                                        updateDistanceTargetHash(endHash, seenChar, left.GetLoc() + dir.GetLoc() * range);
                                        updateDistanceTargetHash(endHash, seenChar, right.GetLoc() + dir.GetLoc() * range);
                                    }
                                    break;
                                case OffsetAction.OffsetArea.Area:
                                    {
                                        for (int xx = -1; xx <= 1; xx++)
                                        {
                                            for (int yy = -1; yy <= 1; yy++)
                                                updateDistanceTargetHash(endHash, seenChar, dir.GetLoc() * range + new Loc(xx, yy));
                                        }
                                    }
                                    break;
                            }
                        }
                    }
                }
                updateDistanceTargetHash(endHash, seenChar, Loc.Zero);
            }
        }

        private void getActionHitboxes(Character controlledChar, List<Character> seenChars, Dir8 dir, ref int skillIndex, ref SkillData entry, ref  int rangeMod, ref CombatAction hitboxAction, ref ExplosionData explosion)
        {
            //check for passives that modify range; NOTE: specialized AI code!
            foreach (PassiveContext passive in controlledChar.IteratePassives(GameEventPriority.USER_PORT_PRIORITY))
            {
                foreach (BattleEvent effect in passive.EventData.OnActions)
                {
                    if (effect is AddRangeEvent)
                    {
                        AddRangeEvent rangeEffect = (AddRangeEvent)effect;
                        rangeMod += rangeEffect.Range;
                    }
                    else if (effect is CategoryAddRangeEvent)
                    {
                        CategoryAddRangeEvent rangeEffect = (CategoryAddRangeEvent)effect;
                        if (entry.Data.Category == rangeEffect.Category)
                            rangeMod += rangeEffect.Range;
                    }
                }
            }
            //check for moves that want to wait until within range
            if (skillIndex == 13)//wait until enemy is wihin two tiles of razor wind's hitbox, to prevent immediate walk-away; NOTE: specialized AI code!
                rangeMod--;

            rangeMod = Math.Min(Math.Max(-3, rangeMod), 3);

            //check for moves that change range on conditions; NOTE: specialized AI code!
            switch (skillIndex)
            {
                case 119: // mirror move
                case 274: // assist
                case 383: // copycat
                    {
                        int searchedStatus = -1;
                        foreach (BattleEvent effect in entry.Data.OnHits)
                        {
                            MirrorMoveEvent mirror = effect as MirrorMoveEvent;
                            if (mirror != null)
                            {
                                searchedStatus = mirror.MoveStatusID;
                                break;
                            }
                        }

                        int calledMove = -1;
                        HashSet<Loc> callTiles = new HashSet<Loc>();
                        foreach (Loc loc in hitboxAction.GetPreTargets(controlledChar, dir, rangeMod))
                            explosion.AddTargetedTiles(loc, callTiles);

                        foreach (Character target in seenChars)
                        {
                            if (DungeonScene.Instance.IsTargeted(controlledChar, target, explosion.TargetAlignments) && callTiles.Contains(target.CharLoc))
                            {
                                StatusEffect moveEffect;
                                if (target.StatusEffects.TryGetValue(searchedStatus, out moveEffect))
                                {
                                    calledMove = moveEffect.StatusStates.Get<IndexState>().Index;
                                    break;
                                }
                            }
                        }
                        skillIndex = calledMove;
                        if (calledMove == -1)
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
                case 174: // curse
                    {
                        if (controlledChar.HasElement(09))
                        {
                            foreach (BattleEvent effect in entry.Data.OnActions)
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
                case 267: // nature power
                    {
                        //TODO
                    }
                    break;
                case 214: // sleep talk
                    {
                        //TODO
                    }
                    break;
                case 382: // me first
                    {
                        int calledMove = -1;
                        HashSet<Loc> callTiles = new HashSet<Loc>();
                        foreach (Loc loc in hitboxAction.GetPreTargets(controlledChar, dir, rangeMod))
                            explosion.AddTargetedTiles(loc, callTiles);

                        foreach (Character target in seenChars)
                        {
                            if (DungeonScene.Instance.IsTargeted(controlledChar, target, explosion.TargetAlignments) && callTiles.Contains(target.CharLoc))
                            {
                                int recordSlot = -1;
                                int recordPower = -1;
                                for (int ii = 0; ii < target.Skills.Count; ii++)
                                {
                                    if (target.Skills[ii].Element.SkillNum > -1)
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
                        if (calledMove == -1)
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

        protected ActionValue GetActionDirValue(int skillIndex, SkillData entry, Character controlledChar, List<Character> seenChars, Dir8 dir)
        {

            //Dig/Fly/Dive/Phantom Force/Focus Punch; NOTE: specialized AI code!
            if (skillIndex == 91 || skillIndex == 19 || skillIndex == 291 || skillIndex == 566 || skillIndex == 264)//Focus Punch;
            {
                //always activate if not already forced to attack
                if (!controlledChar.AttackOnly)
                    return new ActionValue(100, true);
            }

            int rangeMod = 0;

            CombatAction hitboxAction = entry.HitboxAction;
            ExplosionData explosion = entry.Explosion;

            getActionHitboxes(controlledChar, seenChars, dir, ref skillIndex, ref entry, ref rangeMod, ref hitboxAction, ref explosion);

            if (hitboxAction == null)
                return new ActionValue(0, false);

            HashSet<Loc> hitTiles = new HashSet<Loc>();
            foreach (Loc loc in hitboxAction.GetPreTargets(controlledChar, dir, rangeMod))
                explosion.AddTargetedTiles(loc, hitTiles);

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

                    int newVal = GetAttackValue(controlledChar, skillIndex, entry, seenChars, target, rangeMod);
                    totalValue += newVal;
                    if (newVal >= 0)
                        maxValue = Math.Max(newVal, maxValue);
                    else
                    {
                        //the AI will refuse to use the attack in this direction if it will harm any allies,
                        //even if it may be considered "worth it" to damage more foes in the process
                        if ((IQ & AIFlags.TeamPartner) != AIFlags.None)
                        {
                            maxValue = -1;
                            break;
                        }
                    }
                }
            }

            if (entry.Data.Category == BattleData.SkillCategory.Status && maxValue > 0)
                return new ActionValue(totalValue / totalTargets, directHit);
            else
                return new ActionValue(maxValue, directHit);
        }

        protected int GetAttackValue(Character controlledChar, int moveIndex, SkillData entry, List<Character> seenChars, Character target, int rangeMod)
        {
            int delta = GetTargetEffect(controlledChar, moveIndex, entry, seenChars, target, rangeMod);

            Faction foeFaction = (IQ & AIFlags.NeutralFoeConflict) != AIFlags.None ? Faction.Foe : Faction.None;
            bool teamPartner = (IQ & AIFlags.TeamPartner) != AIFlags.None;
            Alignment matchup = DungeonScene.Instance.GetMatchup(controlledChar, target, foeFaction, !teamPartner);


            if (matchup == Alignment.Foe)
            {
                bool wontDisturb = (IQ & AIFlags.WontDisturb) != AIFlags.None;
                if (teamPartner || wontDisturb)
                {
                    //logic to not attack sleeping foes or foes with a certain AI
                    StatusEffect sleepStatus = target.GetStatusEffect(1);
                    if (sleepStatus != null)
                    {
                        bool leaveSleeping = true;
                        //don't wake up sleeping foes; NOTE: specialized AI code!
                        //the exceptions are dream eater and wake-up slap
                        if (moveIndex == 138 || moveIndex == 358)
                            leaveSleeping = false;

                        if (!teamPartner)
                        {
                            int counter = sleepStatus.StatusStates.GetWithDefault<CountDownState>().Counter;
                            //team partners are extra cautious not to do anything to sleepers, but npcs will still attack with status if it applies
                            if (entry.Data.Category == BattleData.SkillCategory.Status && counter > 1)
                                leaveSleeping = false;
                            else if (entry.Data.Category != BattleData.SkillCategory.Status && counter <= 1)
                                leaveSleeping = false;
                        }
                        if (leaveSleeping)
                            return 0;
                    }
                    if (target.GetStatusEffect(3) != null)
                    {
                        //don't attack the frozen; it won't help
                        return 0;
                    }
                }
                if (teamPartner)
                {
                    if (target.GetStatusEffect(25) == null)//last targeted by someone; NOTE: specialized AI code!
                    {
                        if (target.Tactic.ID == 10)//weird tree; NOTE: specialized AI code!
                            return 0;
                        else if (target.Tactic.ID == 8)//wait attack; NOTE: specialized AI code!
                            return 0;
                        else if (target.Tactic.ID == 18)//tit for tat; NOTE: specialized AI code!
                            return 0;
                    }
                }
            }

            if (matchup == Alignment.Friend || matchup == Alignment.Self)
            {
                if ((entry.Explosion.TargetAlignments & Alignment.Foe) == Alignment.Foe)
                    return -delta;
                else
                    return delta;
            }
            //matchup is foe
            return delta;
        }

        protected int GetTargetEffect(Character controlledChar, int moveIndex, SkillData entry, List<Character> seenChars, Character target, int rangeMod)
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

            if (target.GetStatusEffect(3) != null/* && entry.SkillEffect.MoveType != 07*/)
                return 0;

            foreach (BattleEvent effect in entry.Data.OnActions)
            {
                if (effect is StatusNeededEvent)
                {
                    if (controlledChar.GetStatusEffect(((StatusNeededEvent)effect).StatusID) == null)
                        return 0;
                }
            }

            foreach (BattleEvent effect in entry.Data.BeforeHits)
            {
                if (effect is TipOnlyEvent)
                {
                    if ((controlledChar.CharLoc - target.CharLoc).Dist8() != Math.Max(entry.HitboxAction.Distance + rangeMod, 1))
                        return 0;
                }
            }
            if (moveIndex == 217)//Present; if an ally, use healing calculations; NOTE: specialized AI code!
            {
                if (DungeonScene.Instance.GetMatchup(controlledChar, target) == Alignment.Friend)
                {
                    int healHP = target.MaxHP / 3;
                    int hpMissing = target.MaxHP - target.HP - healHP / 2;
                    return -hpMissing * 200 / healHP;
                }
            }
                


            if (entry.Data.Category == BattleData.SkillCategory.Status)
            {
                if (moveIndex == 275)//Ingrain; use it only if damaged, or if enemies are close; NOTE: specialized AI code!
                {
                    bool nearEnemy = false;
                    foreach (Character character in seenChars)
                    {
                        if (DungeonScene.Instance.GetMatchup(controlledChar, character) == Alignment.Foe && (character.CharLoc - controlledChar.CharLoc).Dist8() <= 2)
                        {
                            nearEnemy = true;
                            break;
                        }
                    }
                    if (target.HP * 4 / 3 > target.MaxHP && !nearEnemy)
                        return 0;
                }
                else if (moveIndex == 150)//Splash; use it only if enemies are close; NOTE: specialized AI code!
                {
                    bool nearEnemy = false;
                    foreach (Character character in seenChars)
                    {
                        if (DungeonScene.Instance.GetMatchup(controlledChar, character) == Alignment.Foe && (character.CharLoc - controlledChar.CharLoc).Dist8() <= 1)
                        {
                            nearEnemy = true;
                            break;
                        }
                    }
                    if (!nearEnemy)
                        return 0;
                }
                else if (moveIndex == 392)//aqua ring; use only if damaged; NOTE: specialized AI code!
                {
                    if (target.HP * 4 / 3 > target.MaxHP)
                        return 0;
                }
                else if (moveIndex == 516)//Bestow; use only if you have an item to give; NOTE: specialized AI code!
                {
                    if (controlledChar.EquippedItem.ID > -1)
                        return 100;
                    else
                        return 0;
                }
                else if (moveIndex == 281)//yawn; use only if the target is OK; NOTE: specialized AI code!
                {
                    foreach (StatusEffect status in target.IterateStatusEffects())
                    {
                        if (status.StatusStates.Contains<MajorStatusState>())
                            return 0;
                    }
                }
                else if (moveIndex == 100)//teleport; never use here; handle it elsewhere; NOTE: specialized AI code!
                {
                    if ((IQ & AIFlags.AttackToEscape) == AIFlags.None)
                        return 0;
                }


                //heal checker/status removal checker/other effects
                foreach (BattleEvent effect in entry.Data.OnHits)
                {
                    if (effect is IHealEvent)
                    {
                        IHealEvent giveEffect = (IHealEvent)effect;
                        int healHP = target.MaxHP * giveEffect.HPNum / giveEffect.HPDen;
                        int hpMissing = target.MaxHP - target.HP - healHP / 2;
                        return hpMissing * 200 / healHP;
                    }
                    else if (effect is RemoveStateStatusBattleEvent)
                    {
                        RemoveStateStatusBattleEvent giveEffect = (RemoveStateStatusBattleEvent)effect;
                        foreach (StatusEffect status in target.IterateStatusEffects())
                        {
                            foreach (FlagType state in giveEffect.States)
                            {
                                if (status.StatusStates.Contains(state.FullType))
                                    return 100;
                            }
                        }
                        return 0;
                    }
                    else if (effect is ChangeToAbilityEvent)
                    {
                        if (target.Intrinsics[0].Element.ID != ((ChangeToAbilityEvent)effect).TargetAbility)
                            return 100;
                        else
                            return 0;
                    }
                    else if (effect is ReflectAbilityEvent)
                    {
                        if (target.Intrinsics[0].Element.ID != controlledChar.Intrinsics[0].Element.ID)
                            return 100;
                        else
                            return 0;
                    }
                    else if (effect is SwapAbilityEvent)
                    {
                        if (target.Intrinsics[0].Element.ID != controlledChar.Intrinsics[0].Element.ID &&
                            controlledChar.Intrinsics[0].Element.ID == controlledChar.BaseIntrinsics[0])
                            return 100;
                        else
                            return 0;
                    }
                    else if (effect is AddElementEvent)
                    {
                        if (target.HasElement(((AddElementEvent)effect).TargetElement))
                            return 0;
                        else
                            return 100;
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
                            if (DungeonScene.Instance.GetMatchup(controlledChar, character) == Alignment.Friend && (character.CharLoc - controlledChar.CharLoc).Dist8() <= 5)
                            {
                                foundAllies++;
                                if (foundAllies >= 2)
                                    return 0;
                            }
                        }
                    }
                }

                //status checker
                bool givesStatus = false;
                int minStatusRedundancy = -1;
                foreach (BattleEvent effect in entry.Data.OnHits)
                {
                    if (effect is StatusBattleEvent)
                    {
                        givesStatus = true;
                        StatusBattleEvent giveEffect = (StatusBattleEvent)effect;
                        Character statusTarget = giveEffect.AffectTarget ? target : controlledChar;
                        StatusEffect status = statusTarget.GetStatusEffect(giveEffect.StatusID);
                        if (status == null)
                            minStatusRedundancy = 0;
                        else if (effect is StatusStackBattleEvent)
                        {
                            StatusData statusEntry = DataManager.Instance.GetStatus(giveEffect.StatusID);
                            int minStack = 0;
                            int maxStack = 0;
                            foreach (StatusGivenEvent beforeEffect in statusEntry.BeforeStatusAdds)
                            {
                                if (beforeEffect is StatusStackCheck)
                                {
                                    minStack = ((StatusStackCheck)beforeEffect).Minimum;
                                    maxStack = ((StatusStackCheck)beforeEffect).Maximum;
                                }
                            }
                            StatusStackBattleEvent stackEffect = (StatusStackBattleEvent)effect;
                            int existingStack = status.StatusStates.GetWithDefault<StackState>().Stack;
                            if (stackEffect.Stack > 0 && existingStack < maxStack)
                                minStatusRedundancy = (minStatusRedundancy == -1 ? Math.Abs(existingStack * 6 / maxStack) : Math.Min(minStatusRedundancy, Math.Abs(existingStack * 6 / maxStack)));
                            else if (stackEffect.Stack < 0 && existingStack > minStack)
                                minStatusRedundancy = (minStatusRedundancy == -1 ? Math.Abs(-existingStack * 6 / minStack) : Math.Min(minStatusRedundancy, Math.Abs(-existingStack * 6 / minStack)));
                        }
                    }
                }

                if (givesStatus)
                {
                    if (minStatusRedundancy == -1)
                        return 0;
                    else
                    {
                        int redundantVal = 64;
                        for (int ii = 0; ii < minStatusRedundancy; ii++)
                            redundantVal /= 2;
                        return redundantVal;
                    }
                }

                //weather checker
                foreach (BattleEvent effect in entry.Data.OnHits)
                {
                    if (effect is GiveMapStatusEvent)
                    {
                        GiveMapStatusEvent giveEffect = (GiveMapStatusEvent)effect;
                        if (ZoneManager.Instance.CurrentMap.Status.ContainsKey(giveEffect.StatusID))
                            return 0;
                        else
                            return 100;
                    }
                }

                return 100;
            }
            else
            {

                //here, we check to make sure the attack can affect the target
                //x100 if it does something
                //x-100 if it does something bad
                //x0 if it does nothing
                int power = 0;

                BasePowerState state = entry.Data.SkillStates.GetWithDefault<BasePowerState>();
                if (state != null)
                    power = state.Power * 2;

                power *= entry.Strikes;

                if (power == 0)
                    power = 100;

                if (controlledChar.HasElement(entry.Data.Element))
                {
                    power *= 4;
                    power /= 3;
                }

                if (moveIndex == 162)//super fang; NOTE: specialized AI code!
                    power = 200 * target.HP / target.MaxHP;
                else if (moveIndex == 515)//final gambit; NOTE: specialized AI code!
                {
                    power = 200 * controlledChar.HP / controlledChar.MaxHP;
                    if (power < 20)
                        power = 0;
                }
                else if (moveIndex == 283)//endeavor; NOTE: specialized AI code!
                    power = 200 * Math.Max(0, target.HP - controlledChar.HP) / target.MaxHP;

                //check against move-neutralizing abilities; NOTE: specialized AI code!
                if ((IQ & AIFlags.KnowsMatchups) != AIFlags.None)
                {
                    foreach (PassiveContext passive in target.IteratePassives(GameEventPriority.TARGET_PORT_PRIORITY))
                    {
                        foreach (BattleEvent effect in passive.EventData.BeforeBeingHits)
                        {
                            if (effect is AbsorbElementEvent)
                            {
                                AbsorbElementEvent absorbEffect = (AbsorbElementEvent)effect;
                                if (absorbEffect.AbsorbElement == entry.Data.Element)
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
                foreach (BattleEvent effect in entry.Data.OnHits)
                {
                    if (effect is OnHitEvent)
                    {
                        foreach (BattleEvent baseEffect in ((OnHitEvent)effect).BaseEvents)
                        {
                            if (baseEffect is GiveContinuousDamageEvent)
                                power *= 3;
                        }
                    }
                }

                int matchup = PreTypeEvent.GetDualEffectiveness(controlledChar, target, entry.Data);
                power *= PreTypeEvent.Effectiveness[matchup];
                power /= PreTypeEvent.Effectiveness[PreTypeEvent.NRM_2];

                return power;
            }
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
