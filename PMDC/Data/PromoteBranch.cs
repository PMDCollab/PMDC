using System;
using System.Collections.Generic;
using RogueEssence.Dungeon;
using System.Xml.Serialization;
using RogueElements;
using RogueEssence;
using RogueEssence.Data;
using RogueEssence.Dev;

namespace PMDC.Data
{
    [Serializable]
    public class EvoLevel : PromoteDetail
    {
        public int Level;

        public override string GetReqString() { return String.Format(new StringKey("EVO_REQ_LEVEL").ToLocal(), Level); }
        public override bool GetReq(Character character)
        {
            return character.Level >= Level;
        }
    }
    [Serializable]
    public class EvoItem : PromoteDetail
    {
        public int ItemNum;

        public override int GiveItem { get { return ItemNum; } }
        public override string GetReqString() { return String.Format(new StringKey("EVO_REQ_ITEM").ToLocal(), DataManager.Instance.GetItem(ItemNum).Name.ToLocal()); }
        public override bool GetGroundReq(Character character)
        {
            if (character.EquippedItem.ID == ItemNum)
                return true;
            //if (character.MemberTeam is ExplorerTeam)
            //{
            //    foreach (InvItem item in ((ExplorerTeam)character.MemberTeam).Inventory)
            //    {
            //        if (item.ID == ItemNum)
            //            return true;
            //    }
            //}
            return false;
        }
        public override bool GetReq(Character character)
        {
            if (character.EquippedItem.ID == ItemNum && !character.EquippedItem.Cursed)
                return true;

            return false;
        }
        public override void OnGroundPromote(Character character)
        {
            if (character.EquippedItem.ID == ItemNum)
                character.DequipItem();

            //if (character.MemberTeam is ExplorerTeam)
            //{
            //    List<InvItem> inv = ((ExplorerTeam)character.MemberTeam).Inventory;
            //    for (int ii = 0; ii < inv.Count; ii++)
            //    {
            //        if (inv[ii].ID == ItemNum)
            //        {
            //            inv.RemoveAt(ii);
            //            break;
            //        }
            //    }
            //}
        }

        public override void OnPromote(Character character)
        {
            if (character.EquippedItem.ID == ItemNum && !character.EquippedItem.Cursed)
                character.DequipItem();
            
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

        public override string GetReqString() { return String.Format(new StringKey("EVO_REQ_ALLIES").ToLocal(), Allies); }
        public override bool GetReq(Character character)
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
                    if (data.PromoteFrom > -1)
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

        public override string GetReqString() { return String.Format(new StringKey("EVO_REQ_TIME").ToLocal(), Time.ToLocal()); }
        public override bool GetReq(Character character)
        {
            return DataManager.Instance.Save.Time != TimeOfDay.Unknown && (DataManager.Instance.Save.Time == Time || (TimeOfDay)(((int)DataManager.Instance.Save.Time + 1) % 4) == Time);
        }
    }
    [Serializable]
    public class EvoWeather : PromoteDetail
    {
        [DataType(0, DataManager.DataType.MapStatus, false)]
        public int Weather;

        public override bool GetGroundReq(Character character) { return false; }
        public override string GetReqString() { return String.Format(new StringKey("EVO_REQ_MAP").ToLocal(), DataManager.Instance.GetMapStatus(Weather).Name); }
        public override bool GetReq(Character character)
        {
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
                return String.Format(new StringKey("EVO_REQ_ATK_DEF_GREATER").ToLocal());
            else if (AtkDefComparison < 0)
                return String.Format(new StringKey("EVO_REQ_ATK_DEF_LESS").ToLocal());
            else
                return String.Format(new StringKey("EVO_REQ_ATK_DEF_EQUAL").ToLocal());
        }
        public override bool GetReq(Character character)
        {
            return character.BaseAtk.CompareTo(character.BaseDef) == AtkDefComparison;
        }
    }
    [Serializable]
    public class EvoCrits : PromoteDetail
    {
        [DataType(0, DataManager.DataType.Status, false)]
        public int CritStatus;
        public int Stack;

        public override string GetReqString()
        {
            return String.Format(new StringKey("EVO_REQ_CRITS").ToLocal(), Stack);
        }
        public override bool GetReq(Character character)
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
        [DataType(0, DataManager.DataType.Status, false)]
        public int StatBoostStatus;

        public override string GetReqString()
        {
            return String.Format(new StringKey("EVO_REQ_STAT_BOOST").ToLocal(), DataManager.Instance.GetStatus(StatBoostStatus).Name);
        }
        public override bool GetReq(Character character)
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
        public int MoveNum;

        public override string GetReqString() { return String.Format(new StringKey("EVO_REQ_SKILL").ToLocal(), DataManager.Instance.GetSkill(MoveNum).Name.ToLocal()); }
        public override bool GetReq(Character character)
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
        [DataType(0, DataManager.DataType.Element, false)]
        public int MoveElement;

        public override string GetReqString()
        {
            ElementData elementEntry = DataManager.Instance.GetElement(MoveElement);
            return String.Format(new StringKey("EVO_REQ_SKILL_ELEMENT").ToLocal(), elementEntry.Name.ToLocal());
        }
        public override bool GetReq(Character character)
        {
            foreach (SlotSkill move in character.BaseSkills)
            {
                if (move.SkillNum > -1)
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
    public class EvoForm : PromoteDetail
    {
        public int ReqForm;

        public EvoForm() { }
        public EvoForm(int form)
        {
            ReqForm = form;
        }

        public override bool IsHardReq() { return true; }
        public override bool GetReq(Character character)
        {
            return character.BaseForm.Form == ReqForm;
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
        public override bool GetReq(Character character)
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
                return String.Format(new StringKey("EVO_REQ_HUNGER_LOW").ToLocal());
            else
                return String.Format(new StringKey("EVO_REQ_HUNGER_HIGH").ToLocal());
        }

        public override bool GetReq(Character character)
        {
            return Hungry ? (character.Fullness == 0) : (character.Fullness == 110);
        }
    }
    [Serializable]
    public class EvoLocation : PromoteDetail
    {
        [DataType(0, DataManager.DataType.Element, false)]
        public int TileElement;

        public EvoLocation() { }
        public EvoLocation(int element)
        {
            TileElement = element;
        }

        public override bool GetGroundReq(Character character) { return false; }
        public override string GetReqString()
        {
            ElementData elementEntry = DataManager.Instance.GetElement(TileElement);
            return String.Format(new StringKey("EVO_REQ_TILE_ELEMENT").ToLocal(), elementEntry.Name.ToLocal());
        }
        public override bool GetReq(Character character)
        {
            return ZoneManager.Instance.CurrentMap.Element == TileElement;
        }
    }
    [Serializable]
    public class EvoPartner : PromoteDetail
    {
        public int Species;

        public override string GetReqString() { return String.Format(new StringKey("EVO_REQ_ALLY_SPECIES").ToLocal(), DataManager.Instance.GetMonster(Species).Name.ToLocal()); }
        public override bool GetReq(Character character)
        {
            foreach (Character partner in character.MemberTeam.Players)
            {
                if (partner.BaseForm.Species == Species)
                    return true;
            }
            return false;
        }
    }
    [Serializable]
    public class EvoPartnerElement : PromoteDetail
    {
        [DataType(0, DataManager.DataType.Element, false)]
        public int PartnerElement;

        public override string GetReqString()
        {
            ElementData elementEntry = DataManager.Instance.GetElement(PartnerElement);
            return String.Format(new StringKey("EVO_REQ_ALLY_ELEMENT").ToLocal(), elementEntry.Name.ToLocal());
        }
        public override bool GetReq(Character character)
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
        public int ShedSpecies;

        public override void OnGroundPromote(Character character)
        {

        }

        public override void OnPromote(Character character)
        {
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

            newChar.BaseIntrinsics[0] = forme.RollIntrinsic(DataManager.Instance.Save.Rand, 2);

            newChar.Discriminator = character.Discriminator;

            newChar.MetAt = character.MetAt;

            Character player = new Character(newChar, character.MemberTeam);
            foreach (BackReference<Skill> move in player.Skills)
            {
                if (move.Element.SkillNum > -1)
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

            DataManager.Instance.Save.RegisterMonster(character.BaseForm.Species);
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

        public override void OnPromote(Character character)
        {
            MonsterData data = DataManager.Instance.GetMonster(character.BaseForm.Species);
            if (!data.Forms[Form].Released)
                return;

            //set forme depending on location
            foreach (PromoteDetail detail in Conditions)
            {
                if (!detail.GetReq(character))
                    return;
            }
            character.BaseForm.Form = Form;
            character.RestoreForm();
        }
    }
    [Serializable]
    public class EvoFormLocOrigin : PromoteDetail
    {
        public override void OnPromote(Character character)
        {
            //set forme depending on capture location
        }
    }
    [Serializable]
    public class EvoFormCream : PromoteDetail
    {
        public override bool GetReq(Character character)
        {
            return false;
        }

        public override void OnPromote(Character character)
        {
            //functions as an item check, AND sets the forme
            //set forme depending on ???
        }
    }
    [Serializable]
    public class EvoFormDusk : PromoteDetail
    {
        public override string GetReqString() { return String.Format(new StringKey("EVO_REQ_ITEM").ToLocal(), "???"); }

        public override bool GetReq(Character character)
        {
            return false;
        }

        public override void OnPromote(Character character)
        {
            //functions as an item check, AND sets the forme
            //set forme depending on the item donated
            //sun ribbon? midday
            //moon ribbon? midnight
            //harmony scarf? dusk
        }
    }
    [Serializable]
    public class EvoFormScroll : PromoteDetail
    {
        public override string GetReqString()
        {
            return String.Format(new StringKey("EVO_REQ_TILE_ELEMENT").ToLocal(), "???");
        }

        public override bool GetReq(Character character)
        {
            return false;
        }

        public override void OnPromote(Character character)
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
        public override bool GetReq(Character character)
        {
            return character.Discriminator % Divisor == Mod;
        }
    }
    [Serializable]
    public class EvoTrade : PromoteDetail
    {
        public override string GetReqString() { return String.Format(new StringKey("EVO_REQ_TRADE").ToLocal()); }
        public override bool GetReq(Character character)
        {
            return true; //character.TradeHistory.Count > 0;
        }
    }
}
