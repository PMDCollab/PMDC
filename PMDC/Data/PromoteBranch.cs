using System;
using System.Collections.Generic;
using RogueEssence.Dungeon;
using System.Xml.Serialization;
using RogueElements;
using RogueEssence;
using RogueEssence.Data;
using RogueEssence.Dev;
using Newtonsoft.Json;
using PMDC.Dungeon;
using System.Runtime.Serialization;

namespace PMDC.Data
{
    [Serializable]
    public class EvoLevel : PromoteDetail
    {
        public int Level;

        public override string GetReqString() { return Text.FormatGrammar(new StringKey("EVO_REQ_LEVEL").ToLocal(), Level); }
        public override bool GetReq(Character character, bool inDungeon)
        {
            return character.Level >= Level;
        }
    }
    [Serializable]
    public class EvoItem : PromoteDetail
    {
        [JsonConverter(typeof(ItemConverter))]
        [DataType(0, DataManager.DataType.Item, false)]
        public string ItemNum;

        public override string GetReqItem(Character character) { return ItemNum; }
        public override string GetReqString() { return Text.FormatGrammar(new StringKey("EVO_REQ_ITEM").ToLocal(), ((ItemEntrySummary)DataManager.Instance.DataIndices[DataManager.DataType.Item].Get(ItemNum)).GetColoredName()); }
        
        public override bool GetReq(Character character, bool inDungeon)
        {
            if (inDungeon)
            {
                if (character.EquippedItem.ID == ItemNum && !character.EquippedItem.Cursed)
                    return true;
            }
            else
            {
                if (character.EquippedItem.ID == ItemNum)
                    return true;

                foreach (InvItem item in character.MemberTeam.EnumerateInv())
                {
                    if (item.ID == ItemNum)
                        return true;
                }
            }
            return false;
        }

        public override void OnPromote(Character character, bool inDungeon)
        {
            if (inDungeon)
            {
                if (character.EquippedItem.ID == ItemNum && !character.EquippedItem.Cursed)
                    character.SilentDequipItem();
            }
            else
            {
                if (character.EquippedItem.ID == ItemNum)
                    character.SilentDequipItem();
                else
                {
                    for (int ii = 0; ii < character.MemberTeam.GetInvCount(); ii++)
                    {
                        if (character.MemberTeam.GetInv(ii).ID == ItemNum)
                        {
                            character.MemberTeam.RemoveFromInv(ii);
                            break;
                        }
                    }
                }
            }
        }
    }
    [Serializable]
    public class EvoFriendship : PromoteDetail
    {
        public int Allies;

        public EvoFriendship() { }
        public EvoFriendship(int allies)
        {
            Allies = allies;
        }

        public override string GetReqString() { return Text.FormatGrammar(new StringKey("EVO_REQ_ALLIES").ToLocal(), Allies); }
        public override bool GetReq(Character character, bool inDungeon)
        {
            ExplorerTeam team = character.MemberTeam as ExplorerTeam;

            if (team == null)
                return false;

            int count = 0;
            foreach (Character ally in character.MemberTeam.Players)
            {
                if (ally != character)
                {
                    MonsterData data = DataManager.Instance.GetMonster(ally.BaseForm.Species);
                    if (!String.IsNullOrEmpty(data.PromoteFrom))
                        count++;
                }
            }

            return count >= Allies;
        }
    }
    [Serializable]
    public class EvoTime : PromoteDetail
    {
        public TimeOfDay Time;

        public override string GetReqString() { return Text.FormatGrammar(new StringKey("EVO_REQ_TIME").ToLocal(), Time.ToLocal()); }
        public override bool GetReq(Character character, bool inDungeon)
        {
            return DataManager.Instance.Save.Time != TimeOfDay.Unknown && (DataManager.Instance.Save.Time == Time || (TimeOfDay)(((int)DataManager.Instance.Save.Time + 1) % 4) == Time);
        }
    }
    [Serializable]
    public class EvoWeather : PromoteDetail
    {
        [JsonConverter(typeof(MapStatusConverter))]
        [DataType(0, DataManager.DataType.MapStatus, false)]
        public string Weather;

        public override string GetReqString() { return Text.FormatGrammar(new StringKey("EVO_REQ_MAP").ToLocal(), DataManager.Instance.GetMapStatus(Weather).GetColoredName()); }
        public override bool GetReq(Character character, bool inDungeon)
        {
            if (!inDungeon)
                return false;

            return ZoneManager.Instance.CurrentMap.Status.ContainsKey(Weather);
        }
    }
    [Serializable]
    public class EvoStats : PromoteDetail
    {
        public int AtkDefComparison;

        public override string GetReqString()
        {
            if (AtkDefComparison > 0)
                return Text.FormatGrammar(new StringKey("EVO_REQ_ATK_DEF_GREATER").ToLocal());
            else if (AtkDefComparison < 0)
                return Text.FormatGrammar(new StringKey("EVO_REQ_ATK_DEF_LESS").ToLocal());
            else
                return Text.FormatGrammar(new StringKey("EVO_REQ_ATK_DEF_EQUAL").ToLocal());
        }
        public override bool GetReq(Character character, bool inDungeon)
        {
            return character.BaseAtk.CompareTo(character.BaseDef) == AtkDefComparison;
        }
    }
    [Serializable]
    public class EvoCrits : PromoteDetail
    {
        [JsonConverter(typeof(StatusConverter))]
        [DataType(0, DataManager.DataType.Status, false)]
        public string CritStatus;
        public int Stack;

        public override string GetReqString()
        {
            return Text.FormatGrammar(new StringKey("EVO_REQ_CRITS").ToLocal(), Stack);
        }
        public override bool GetReq(Character character, bool inDungeon)
        {
            StatusEffect status;
            if (character.StatusEffects.TryGetValue(CritStatus, out status))
            {
                StackState state = status.StatusStates.Get<StackState>();
                if (state.Stack >= Stack)
                    return true;
            }
            return false;
        }
    }
    [Serializable]
    public class EvoStatBoost : PromoteDetail
    {
        [JsonConverter(typeof(StatusConverter))]
        [DataType(0, DataManager.DataType.Status, false)]
        public string StatBoostStatus;

        public override string GetReqString()
        {
            return Text.FormatGrammar(new StringKey("EVO_REQ_STAT_BOOST").ToLocal(), DataManager.Instance.GetStatus(StatBoostStatus).GetColoredName());
        }
        public override bool GetReq(Character character, bool inDungeon)
        {
            StatusEffect status;
            if (character.StatusEffects.TryGetValue(StatBoostStatus, out status))
            {
                StackState state = status.StatusStates.Get<StackState>();
                if (state.Stack > 0)
                    return true;
            }
            return false;
        }
    }
    [Serializable]
    public class EvoMove : PromoteDetail
    {
        [JsonConverter(typeof(SkillConverter))]
        [DataType(0, DataManager.DataType.Skill, false)]
        public string MoveNum;

        public override string GetReqString() { return Text.FormatGrammar(new StringKey("EVO_REQ_SKILL").ToLocal(), DataManager.Instance.GetSkill(MoveNum).GetColoredName()); }
        public override bool GetReq(Character character, bool inDungeon)
        {
            foreach (SlotSkill move in character.BaseSkills)
            {
                if (move.SkillNum == MoveNum)
                    return true;
            }
            return false;
        }
    }
    [Serializable]
    public class EvoMoveElement : PromoteDetail
    {
        [JsonConverter(typeof(ElementConverter))]
        [DataType(0, DataManager.DataType.Element, false)]
        public string MoveElement;

        public EvoMoveElement() { MoveElement = ""; }

        public override string GetReqString()
        {
            ElementData elementEntry = DataManager.Instance.GetElement(MoveElement);
            return Text.FormatGrammar(new StringKey("EVO_REQ_SKILL_ELEMENT").ToLocal(), elementEntry.GetColoredName());
        }
        public override bool GetReq(Character character, bool inDungeon)
        {
            foreach (SlotSkill move in character.BaseSkills)
            {
                if (!String.IsNullOrEmpty(move.SkillNum))
                {
                    SkillData data = DataManager.Instance.GetSkill(move.SkillNum);
                    if (data.Data.Element == MoveElement)
                        return true;
                }
            }
            return false;
        }
    }

    [Serializable]
    public class EvoMoveUse : PromoteDetail
    {
        [JsonConverter(typeof(StatusConverter))]
        [DataType(0, DataManager.DataType.Status, false)]
        public string LastMoveStatusID;
        [JsonConverter(typeof(StatusConverter))]
        [DataType(0, DataManager.DataType.Status, false)]
        public string MoveRepeatStatusID;

        [JsonConverter(typeof(SkillConverter))]
        [DataType(0, DataManager.DataType.Skill, false)]
        public string MoveNum;

        public int Amount;

        public override string GetReqString() { return Text.FormatGrammar(new StringKey("EVO_REQ_SKILL_USE").ToLocal(), DataManager.Instance.GetSkill(MoveNum).GetColoredName(), Amount); }
        public override bool GetReq(Character character, bool inDungeon)
        {
            StatusEffect moveStatus = character.GetStatusEffect(LastMoveStatusID);
            StatusEffect repeatStatus = character.GetStatusEffect(MoveRepeatStatusID);
            if (moveStatus == null || repeatStatus == null)
                return false;
            if (moveStatus.StatusStates.GetWithDefault<IDState>().ID != MoveNum)
                return false;
            return repeatStatus.StatusStates.GetWithDefault<CountState>().Count >= Amount;
        }
    }

    [Serializable]
    public class EvoKillCount : PromoteDetail
    {
        public int Amount;

        public override string GetReqString() { return ""; }
        public override bool GetReq(Character character, bool inDungeon)
        {
            //foreach (SlotSkill move in character.BaseSkills)
            //{
            //    if (move.SkillNum == MoveNum)
            //        return true;
            //}
            return false;
        }
    }

    [Serializable]
    public class EvoMoney : PromoteDetail
    {
        public int Amount;

        public override string GetReqString() { return ""; }
        public override bool GetReq(Character character, bool inDungeon)
        {
            //foreach (SlotSkill move in character.BaseSkills)
            //{
            //    if (move.SkillNum == MoveNum)
            //        return true;
            //}
            return false;
        }
    }

    [Serializable]
    public class EvoWalk : PromoteDetail
    {
        public override string GetReqString() { return ""; }
        public override bool GetReq(Character character, bool inDungeon)
        {
            //foreach (SlotSkill move in character.BaseSkills)
            //{
            //    if (move.SkillNum == MoveNum)
            //        return true;
            //}
            return false;
        }
    }


    [Serializable]
    public class EvoRescue : PromoteDetail
    {
        public override string GetReqString() { return ""; }
        public override bool GetReq(Character character, bool inDungeon)
        {
            //foreach (SlotSkill move in character.BaseSkills)
            //{
            //    if (move.SkillNum == MoveNum)
            //        return true;
            //}
            return false;
        }
    }

    [Serializable]
    public class EvoTookDamage : PromoteDetail
    {
        public int Amount;

        public override string GetReqString() { return ""; }
        public override bool GetReq(Character character, bool inDungeon)
        {
            //foreach (SlotSkill move in character.BaseSkills)
            //{
            //    if (move.SkillNum == MoveNum)
            //        return true;
            //}
            return false;
        }
    }

    [Serializable]
    public class EvoForm : PromoteDetail
    {
        public int ReqForm;

        public HashSet<int> ReqForms;

        public EvoForm()
        {
            ReqForms = new HashSet<int>();
        }
        public EvoForm(params int[] forms)
        {
            ReqForms = new HashSet<int>();
            foreach(int form in forms)
                ReqForms.Add(form);
        }

        public override bool IsHardReq() { return true; }
        public override bool GetReq(Character character, bool inDungeon)
        {
            return ReqForms.Contains(character.BaseForm.Form);
        }

        [OnDeserialized]
        internal void OnDeserializedMethod(StreamingContext context)
        {
            //TODO: remove on v1.1
            if (Serializer.OldVersion < new Version(0, 7, 22))
            {
                ReqForms = new HashSet<int>();
                ReqForms.Add(ReqForm);
            }
        }
    }
    [Serializable]
    public class EvoGender : PromoteDetail
    {
        public Gender ReqGender;

        public EvoGender() { }
        public EvoGender(Gender gender)
        {
            ReqGender = gender;
        }

        public override bool IsHardReq() { return true; }
        public override bool GetReq(Character character, bool inDungeon)
        {
            return character.BaseForm.Gender == ReqGender;
        }
    }
    [Serializable]
    public class EvoHunger : PromoteDetail
    {
        public bool Hungry;

        public EvoHunger() { }

        public override string GetReqString()
        {
            if (Hungry)
                return Text.FormatGrammar(new StringKey("EVO_REQ_HUNGER_LOW").ToLocal());
            else
                return Text.FormatGrammar(new StringKey("EVO_REQ_HUNGER_HIGH").ToLocal());
        }

        public override bool GetReq(Character character, bool inDungeon)
        {
            return Hungry ? (character.Fullness == 0) : (character.Fullness >= 110);
        }
    }
    [Serializable]
    public class EvoLocation : PromoteDetail
    {
        [JsonConverter(typeof(ElementConverter))]
        [DataType(0, DataManager.DataType.Element, false)]
        public string TileElement;

        public EvoLocation() { TileElement = ""; }
        public EvoLocation(string element)
        {
            TileElement = element;
        }

        public override string GetReqString()
        {
            ElementData elementEntry = DataManager.Instance.GetElement(TileElement);
            return Text.FormatGrammar(new StringKey("EVO_REQ_TILE_ELEMENT").ToLocal(), elementEntry.GetColoredName());
        }
        public override bool GetReq(Character character, bool inDungeon)
        {
            if (!inDungeon)
                return false;

            if (GameManager.Instance.CurrentScene == DungeonScene.Instance)
                return ZoneManager.Instance.CurrentMap.Element == TileElement;
            return false;
        }
    }

    /// <summary>
    /// Condition: if in a dungeon map and a turn has not passed.
    /// </summary>
    [Serializable]
    public class EvoMapStart : PromoteDetail
    {
        public EvoMapStart() { }

        public override string GetReqString() { return ""; }
        public override bool GetReq(Character character, bool inDungeon)
        {
            if (!inDungeon)
                return false;

            if (GameManager.Instance.CurrentScene == DungeonScene.Instance)
                return ZoneManager.Instance.CurrentMap.MapTurns == 0;
            return false;
        }
    }

    [Serializable]
    public class EvoPartner : PromoteDetail
    {
        [JsonConverter(typeof(MonsterConverter))]
        [DataType(0, DataManager.DataType.Monster, false)]
        public string Species;

        public int Amount;

        public override string GetReqString()
        {
            if (Amount > 1)
                return Text.FormatGrammar(new StringKey("EVO_REQ_ALLY_SPECIES_MULTI").ToLocal(), DataManager.Instance.GetMonster(Species).GetColoredName(), Amount);
            return Text.FormatGrammar(new StringKey("EVO_REQ_ALLY_SPECIES").ToLocal(), DataManager.Instance.GetMonster(Species).GetColoredName());
        }
        public override bool GetReq(Character character, bool inDungeon)
        {
            int amount = 0;
            foreach (Character partner in character.MemberTeam.Players)
            {
                if (partner.BaseForm.Species == Species)
                    amount++;
            }
            return amount >= Amount;
        }
    }
    [Serializable]
    public class EvoPartnerElement : PromoteDetail
    {
        [JsonConverter(typeof(ElementConverter))]
        [DataType(0, DataManager.DataType.Element, false)]
        public string PartnerElement;

        public EvoPartnerElement() { PartnerElement = ""; }

        public override string GetReqString()
        {
            ElementData elementEntry = DataManager.Instance.GetElement(PartnerElement);
            return Text.FormatGrammar(new StringKey("EVO_REQ_ALLY_ELEMENT").ToLocal(), elementEntry.GetColoredName());
        }
        public override bool GetReq(Character character, bool inDungeon)
        {
            foreach (Character partner in character.MemberTeam.Players)
            {
                if (partner.HasElement(PartnerElement))
                    return true;
            }
            return false;
        }
    }

    [Serializable]
    public class EvoShed : PromoteDetail
    {
        [JsonConverter(typeof(MonsterConverter))]
        [DataType(0, DataManager.DataType.Monster, false)]
        public string ShedSpecies;

        public override void OnPromote(Character character, bool inDungeon)
        {
            if (!inDungeon)
                return;

            ExplorerTeam team = character.MemberTeam as ExplorerTeam;
            if (team == null)
                return;
            if (character.MemberTeam.Players.Count == team.GetMaxTeam(ZoneManager.Instance.CurrentZone))
                return;

            //if character has an open team slot, spawn the new character based on the stats of the current one

            MonsterID formData = new MonsterID(ShedSpecies, 0, character.BaseForm.Skin, Gender.Genderless);
            MonsterData dex = DataManager.Instance.GetMonster(formData.Species);

            CharData newChar = new CharData();
            newChar.BaseForm = formData;
            newChar.Level = character.Level;

            newChar.MaxHPBonus = character.MaxHPBonus;
            newChar.AtkBonus = character.AtkBonus;
            newChar.DefBonus = character.DefBonus;
            newChar.MAtkBonus = character.MAtkBonus;
            newChar.MDefBonus = character.MDefBonus;
            newChar.SpeedBonus = character.SpeedBonus;

            BaseMonsterForm forme = dex.Forms[formData.Form];

            for (int ii = 0; ii < character.BaseSkills.Count; ii++)
                newChar.BaseSkills[ii] = new SlotSkill(character.BaseSkills[ii]);

            string intrinsic = forme.RollIntrinsic(DataManager.Instance.Save.Rand, 2);
            newChar.SetBaseIntrinsic(intrinsic);

            newChar.Discriminator = character.Discriminator;
            newChar.MetAt = character.MetAt;
            newChar.MetLoc = character.MetLoc;
            foreach (BattleEvent effect in character.ActionEvents)
                newChar.ActionEvents.Add((BattleEvent)effect.Clone());

            Character player = new Character(newChar);
            foreach (BackReference<Skill> move in player.Skills)
            {
                if (!String.IsNullOrEmpty(move.Element.SkillNum))
                {
                    SkillData entry = DataManager.Instance.GetSkill(move.Element.SkillNum);
                    move.Element.Enabled = (entry.Data.Category == BattleData.SkillCategory.Physical || entry.Data.Category == BattleData.SkillCategory.Magical);
                }
            }
            player.Tactic = new AITactic(character.Tactic);
            character.MemberTeam.Players.Add(player);

            Loc? endLoc = ZoneManager.Instance.CurrentMap.GetClosestTileForChar(player, character.CharLoc);
            if (endLoc == null)
                endLoc = character.CharLoc;

            player.CharLoc = endLoc.Value;

            ZoneManager.Instance.CurrentMap.UpdateExploration(player);

            player.RefreshTraits();

            DataManager.Instance.Save.RegisterMonster(newChar.BaseForm.Species);
            DataManager.Instance.Save.RogueUnlockMonster(newChar.BaseForm.Species);
        }
    }

    [Serializable]
    public class EvoSetForm : PromoteDetail
    {
        public List<PromoteDetail> Conditions;
        public int Form;

        public EvoSetForm()
        {
            Conditions = new List<PromoteDetail>();
        }
        public EvoSetForm(int form)
        {
            Conditions = new List<PromoteDetail>();
            Form = form;
        }

        public override void BeforePromote(Character character, bool inDungeon, ref MonsterID result)
        {
            MonsterData data = DataManager.Instance.GetMonster(result.Species);
            if (!data.Forms[Form].Released)
                return;

            //set forme depending on condition
            foreach (PromoteDetail detail in Conditions)
            {
                if (!detail.GetReq(character, inDungeon))
                    return;
            }
            result.Form = Form;
        }
    }
    [Serializable]
    public class EvoFormLocOrigin : PromoteDetail
    {
        public override void OnPromote(Character character, bool inDungeon)
        {
            //set forme depending on capture location
        }
    }
    [Serializable]
    public class EvoFormCream : PromoteDetail
    {
        public override bool GetReq(Character character, bool inDungeon)
        {
            return false;
        }

        public override void OnPromote(Character character, bool inDungeon)
        {
            //functions as an item check, AND sets the forme
            //set forme depending on ???
        }
    }
    [Serializable]
    public class EvoFormDusk : PromoteDetail
    {
        [DataType(1, DataManager.DataType.Item, false)]
        public Dictionary<string, int> ItemMap;
        public int DefaultForm;

        public override string GetReqItem(Character character) { return character.EquippedItem.ID; }

        public EvoFormDusk() { ItemMap = new Dictionary<string, int>(); }

        public override string GetReqString() { return Text.FormatGrammar(new StringKey("EVO_REQ_ITEM").ToLocal(), "???"); }

        public override bool GetReq(Character character, bool inDungeon)
        {
            foreach (string itemNum in ItemMap.Keys)
            {
                if (inDungeon)
                {
                    if (character.EquippedItem.ID == itemNum && !character.EquippedItem.Cursed)
                        return true;
                }
                else
                {
                    if (character.EquippedItem.ID == itemNum)
                        return true;
                }
            }
            return false;
        }

        public override void BeforePromote(Character character, bool inDungeon, ref MonsterID result)
        {
            int resultForm = DefaultForm;

            //set forme depending on condition
            foreach (string itemNum in ItemMap.Keys)
            {
                if (character.EquippedItem.ID == itemNum)
                {
                    resultForm = ItemMap[itemNum];
                    break;
                }
            }

            MonsterData data = DataManager.Instance.GetMonster(result.Species);
            if (data.Forms[resultForm].Released)
                result.Form = resultForm;
        }

        public override void OnPromote(Character character, bool inDungeon)
        {
            character.SilentDequipItem();
        }
    }
    [Serializable]
    public class EvoFormScroll : PromoteDetail
    {
        public override string GetReqString()
        {
            return Text.FormatGrammar(new StringKey("EVO_REQ_TILE_ELEMENT").ToLocal(), "???");
        }

        public override bool GetReq(Character character, bool inDungeon)
        {
            return false;
        }

        public override void OnPromote(Character character, bool inDungeon)
        {
            //functions as a terrain check, AND sets the forme
        }
    }



    [Serializable]
    public class EvoPersonality : PromoteDetail
    {
        public int Mod;
        public int Divisor;

        public override bool IsHardReq() { return true; }
        public override bool GetReq(Character character, bool inDungeon)
        {
            return character.Discriminator % Divisor == Mod;
        }
    }
    [Serializable]
    public class EvoTrade : PromoteDetail
    {
        public override string GetReqString() { return Text.FormatGrammar(new StringKey("EVO_REQ_TRADE").ToLocal()); }
        public override bool GetReq(Character character, bool inDungeon)
        {
            return true; //character.TradeHistory.Count > 0;
        }
    }
}
