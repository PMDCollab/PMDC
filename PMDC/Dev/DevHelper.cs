using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using RogueElements;
using RogueEssence.Data;
using RogueEssence.Content;
using RogueEssence.Dungeon;
using RogueEssence.LevelGen;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Reflection;
using System.Linq;
using RogueEssence;
using RogueEssence.Dev;
using PMDC.LevelGen;
using System.Diagnostics;

namespace PMDC.Dev
{

    public static class DevHelper
    {

        public delegate IEntryData GetNamedData(int ii);

        public static void IndexHardcodedNamedData(string dataPath, GetNamedData getData, int max)
        {
            try
            {
                EntryDataIndex fullGuide = new EntryDataIndex();

                for (int ii = 0; ii < max; ii++)
                {
                    IEntryData data = getData(ii);
                    fullGuide.Entries.Add(data.GenerateEntrySummary());
                }

                using (Stream stream = new FileStream(dataPath + "index.idx", FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    using (BinaryWriter writer = new BinaryWriter(stream))
                    {
                        IFormatter formatter = new BinaryFormatter();
                        formatter.Serialize(stream, fullGuide);
                    }
                }
            }
            catch (Exception ex)
            {
                DiagManager.Instance.LogError(new Exception("Error importing index at " + dataPath + "\n", ex));
            }
        }


        public static void AddEvoFamily(Dictionary<int, HashSet<ZoneLoc>> found, int index, ZoneLoc encounter)
        {
            addMonster(found, index, encounter);
            //also add all the evos
            MonsterData data = DataManager.Instance.GetMonster(index);
            findEvos(found, data);
        }

        private static void addMonster(Dictionary<int, HashSet<ZoneLoc>> found, int index, ZoneLoc encounter)
        {
            if (!found.ContainsKey(index))
                found[index] = new HashSet<ZoneLoc>();
            found[index].Add(encounter);
        }

        private static void findEvos(Dictionary<int, HashSet<ZoneLoc>> found, MonsterData data)
        {
            foreach (PromoteBranch evo in data.Promotions)
            {
                addMonster(found, evo.Result, ZoneLoc.Invalid);
                MonsterData evoData = DataManager.Instance.GetMonster(evo.Result);
                findEvos(found, evoData);
            }
        }

        private static void extractMobSpawnFromObject(Dictionary<int, HashSet<ZoneLoc>> foundSpecies, object member, bool recruitableOnly, ZoneLoc encounter)
        {
            Type type = member.GetType();

            //members are set when control values are changed?
            try
            {
                if (type.IsEnum)
                {

                }
                else if (type == typeof(MobSpawn))
                {
                    MobSpawn spawn = (MobSpawn)member;
                    bool skip = false;
                    foreach (MobSpawnExtra feature in spawn.SpawnFeatures)
                    {
                        if (feature is MobSpawnUnrecruitable)
                        {
                            skip = true;
                            break;
                        }
                    }
                    if (!skip || !recruitableOnly)
                        AddEvoFamily(foundSpecies, spawn.BaseForm.Species, encounter);
                }
                else if (type.IsArray)
                {
                    Array array = ((Array)member);
                    foreach (object val in array)
                        extractMobSpawnFromObject(foundSpecies, val, recruitableOnly, encounter);
                }
                else if (encounter.StructID.ID == -1 && type.GetInterfaces().Contains(typeof(ISpawnRangeList)))
                {
                    ISpawnRangeList enumerable = (ISpawnRangeList)member;
                    for (int nn = 0; nn < enumerable.Count; nn++)
                    {
                        object val = enumerable.GetSpawn(nn);
                        IntRange range = enumerable.GetSpawnRange(nn);
                        for (int kk = range.Min; kk < range.Max; kk++)
                        {
                            ZoneLoc newEnc = encounter;
                            newEnc.StructID.ID = kk;
                            extractMobSpawnFromObject(foundSpecies, val, recruitableOnly, newEnc);
                        }
                    }
                }
                else if (type.GetInterfaces().Contains(typeof(IEnumerable)))
                {
                    IEnumerable enumerable = (IEnumerable)member;
                    foreach (object val in enumerable)
                        extractMobSpawnFromObject(foundSpecies, val, recruitableOnly, encounter);
                }
                else
                {
                    //members of the type
                    extractMobSpawnsFromClass(foundSpecies, member, recruitableOnly, encounter);
                }
            }
            catch (Exception e)
            {
                DiagManager.Instance.LogError(e);
            }
        }

        private static void extractMobSpawnsFromClass(Dictionary<int, HashSet<ZoneLoc>> foundSpecies, object obj, bool recruitableOnly, ZoneLoc encounter)
        {
            //go through all members and add for them
            //control starts off clean; this is the control that will have all member controls on it
            try
            {
                Type type = obj.GetType();

                List<MemberInfo> myFields = type.GetEditableMembers();

                for (int ii = 0; ii < myFields.Count; ii++)
                {
                    if (myFields[ii].GetCustomAttributes(typeof(NonSerializedAttribute), false).Length > 0)
                        continue;

                    object member = myFields[ii].GetValue(obj);
                    if (member == null)
                        continue;

                    object memberObj = myFields[ii].GetValue(obj);
                    extractMobSpawnFromObject(foundSpecies, memberObj, recruitableOnly, encounter);
                }
            }
            catch (Exception e)
            {
                DiagManager.Instance.LogError(e);
            }
        }

        public static Dictionary<int, HashSet<ZoneLoc>> GetAllAppearingMonsters(bool recruitableOnly)
        {
            //go through all dungeons
            //get all potential spawns in the dungeons (make it a table of bools mapping dex num to boolean)
            Dictionary<int, HashSet<ZoneLoc>> foundSpecies = new Dictionary<int, HashSet<ZoneLoc>>();
            
            //check all structures
            for (int zz = 0; zz < DataManager.Instance.DataIndices[DataManager.DataType.Zone].Count; zz++)
            {
                if (zz == 0 || zz == 35)
                    continue;
                ZoneData mainZone = DataManager.Instance.GetZone(zz);

                for (int ii = 0; ii < mainZone.Structures.Count; ii++)
                {
                    LayeredSegment structure = mainZone.Structures[ii] as LayeredSegment;
                    //check the postprocs for spawn-related classes
                    if (structure != null)
                    {
                        extractMobSpawnFromObject(foundSpecies, structure.PostProcessingSteps, recruitableOnly, new ZoneLoc(zz, new SegLoc(ii, -1)));
                        for (int jj = 0; jj < structure.Floors.Count; jj++)
                            extractMobSpawnFromObject(foundSpecies, structure.Floors[jj], recruitableOnly, new ZoneLoc(zz, new SegLoc(ii, jj)));
                    }
                    else
                        extractMobSpawnFromObject(foundSpecies, mainZone.Structures[ii], recruitableOnly, new ZoneLoc(zz, new SegLoc(ii, -1)));
                }
            }
            return foundSpecies;
        }


        /// <summary>
        /// Prints all the moves that can appear in the game due to the monsters appearing in the game, and alerts which unfinished ones need to be finished
        /// </summary>
        public static void PrintAllUnfinishedMoves()
        {
            Dictionary<int, HashSet<ZoneLoc>> foundSpecies = GetAllAppearingMonsters(false);
            //check against the regional dex for inconsistencies
            for (int ii = 1; ii < DataManager.Instance.DataIndices[DataManager.DataType.Monster].Count; ii++)
            {
                //bool hasDex = false;
                //for (int jj = 0; jj < MonsterInfo.DEX_MAP.Length; jj++)
                //{
                //    if (MonsterInfo.DEX_MAP[jj] == ii)
                //        hasDex = true;
                //}
                //if (hasDex != foundSpecies.ContainsKey(ii))
                //    Debug.WriteLine(String.Format("{0:D3}: Dex:{1} != Search:{2}", ii, hasDex, foundSpecies[ii]));
            }

            //get all learnable moves, keeping a hit count (make it a table mapping move index to number of distinct mons that learn this move)
            List<string>[] moveHits = new List<string>[DataManager.Instance.DataIndices[DataManager.DataType.Skill].Count];
            for (int ii = 0; ii < DataManager.Instance.DataIndices[DataManager.DataType.Skill].Count; ii++)
                moveHits[ii] = new List<string>();

            for (int ii = 1; ii < DataManager.Instance.DataIndices[DataManager.DataType.Monster].Count; ii++)
            {
                if (foundSpecies.ContainsKey(ii))
                {
                    MonsterData data = DataManager.Instance.GetMonster(ii);
                    for (int jj = 0; jj < data.Forms.Count; jj++)
                    {
                        BaseMonsterForm form = data.Forms[jj];
                        for (int kk = 0; kk < form.LevelSkills.Count; kk++)
                            moveHits[form.LevelSkills[kk].Skill].Add(form.FormName.ToLocal());
                    }
                }
            }
            Debug.WriteLine("");
            //go through the table, print the moves that are "unfinished"
            for (int ii = 0; ii < moveHits.Length; ii++)
            {
                SkillData skill = DataManager.Instance.GetSkill(ii);
                if (moveHits[ii].Count > 0 /*&& (skill.Name.DefaultText.StartsWith("-") || skill.Name.DefaultText.StartsWith("="))*/)
                {
                    if (moveHits[ii].Count > 4)
                        Debug.WriteLine(String.Format("{1}({0}):\t{2}", ii, skill.Name.ToLocal(), "x" + moveHits[ii].Count));
                    else
                    {
                        string nameList = "";
                        for (int jj = 0; jj < moveHits[ii].Count; jj++)
                            nameList += (moveHits[ii][jj] + " ");
                        Debug.WriteLine(String.Format("{1}({0}):\t{2}", ii, skill.Name.ToLocal(), nameList));
                    }
                }
            }
        }

        public static List<MonsterID> FindMoveAbilityUsers(int ability, int[] moves)
        {
            List<MonsterID> results = new List<MonsterID>();
            //go through entire dex
            for (int ii = 1; ii < 493; ii++)
            {
                MonsterData dex = DataManager.Instance.GetMonster(ii);
                for (int jj = 0; jj < dex.Forms.Count; jj++)
                {
                    if (HasAbilityMoves(ability, moves, dex.Forms[jj]))
                        results.Add(new MonsterID(ii, jj, 0, Gender.Genderless));
                }
            }

            return results;
        }

        public static bool HasAbilityMoves(int ability, int[] moves, BaseMonsterForm entry)
        {
            //check if form has ability given
            if (ability > -1)
            {
                if (entry.Intrinsic1 != ability && entry.Intrinsic2 != ability && entry.Intrinsic3 != ability)
                    return false;
            }

            //check if form has all moves given
            foreach (int reqMove in moves)
            {
                bool foundMove = false;
                foreach (LevelUpSkill move in entry.LevelSkills)
                {
                    if (move.Skill == reqMove)
                    {
                        foundMove = true;
                        break;
                    }
                }
                if (!foundMove)
                    return false;
            }

            return true;
        }


        /// <summary>
        /// Gets the abilities from a text file in the path, and prints the monsters that are capable of each.
        /// </summary>
        /// <param name="path"></param>
        public static void PrintAbilityUsers(string path)
        {
            try
            {
                using (StreamReader s = new StreamReader(path))
                {
                    while (!s.EndOfStream)
                    {
                        string[] names = s.ReadLine().Split('/');
                        foreach (string name in names)
                        {
                            if (name == "")
                                continue;

                            int ability = -1;
                            IntrinsicData entry = null;
                            for (int ii = 0; ii < DataManager.Instance.DataIndices[DataManager.DataType.Intrinsic].Count; ii++)
                            {
                                entry = DataManager.Instance.GetIntrinsic(ii);
                                if (entry.Name.ToLocal().ToLower() == name.ToLower())
                                {
                                    ability = ii;
                                    break;
                                }
                            }
                            if (ability == -1)
                                throw new Exception("Unknown Ability");

                            Debug.WriteLine(String.Format("{0:D3}: {1}", ability, entry.Name.ToLocal()));
                            List<MonsterID> forms = FindMoveAbilityUsers(ability, new int[0]);
                            for (int ii = 0; ii < forms.Count; ii++)
                            {
                                MonsterData dex = DataManager.Instance.GetMonster(forms[ii].Species);
                                Debug.WriteLine(String.Format("    {0:D3}: {1} {2}", forms[ii].Species, dex.Name.ToLocal(),
                                    (dex.Forms[forms[ii].Form].FormName.DefaultText == dex.Name.DefaultText) ? "" : "(" + dex.Forms[forms[ii].Form].FormName.DefaultText + ")"));
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine("The file could not be read:");
                Debug.WriteLine(e.Message);
            }
        }

        /// <summary>
        /// Gets the moves from a text file in the path, and prints the monsters that are capable of using each.
        /// </summary>
        /// <param name="path"></param>
        public static void PrintMoveUsers(string path)
        {
            try
            {
                using (StreamReader s = new StreamReader(path))
                {
                    while (!s.EndOfStream)
                    {
                        string[] names = s.ReadLine().Split('/');
                        foreach (string name in names)
                        {
                            if (name == "")
                                continue;

                            int move = -1;
                            SkillData entry = null;
                            for (int ii = 0; ii < DataManager.Instance.DataIndices[DataManager.DataType.Skill].Count; ii++)
                            {
                                entry = DataManager.Instance.GetSkill(ii);
                                if (entry.Name.DefaultText.ToLower() == name.ToLower())
                                {
                                    move = ii;
                                    break;
                                }
                            }
                            if (move == -1)
                                throw new Exception("Unknown Move");

                            Debug.WriteLine(String.Format("{0:D3}: {1}", move, entry.Name.ToLocal()));
                            List<MonsterID> forms = FindMoveAbilityUsers(-1, new int[1] { move });
                            for (int ii = 0; ii < forms.Count; ii++)
                            {
                                MonsterData dex = DataManager.Instance.GetMonster(forms[ii].Species);
                                Debug.WriteLine(String.Format("    {0:D3}: {1}  {2}", forms[ii].Species, dex.Name.ToLocal(),
                                    (dex.Forms[forms[ii].Form].FormName.DefaultText == dex.Name.DefaultText) ? "" : "(" + dex.Forms[forms[ii].Form].FormName.ToLocal() + ")"));
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine("The file could not be read:");
                Debug.WriteLine(e.Message);
            }
        }

    }
}
