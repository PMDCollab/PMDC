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
    // Batle events used by utility items, which often have a pre-used dialogue and then an on-use effect that processes the choice

    /// <summary>
    /// Event that checks if the tile can be unlocked by checking if the item matches in the UnlockState tile state 
    /// </summary>
    [Serializable]
    public class KeyCheckEvent : BattleEvent
    {
        public KeyCheckEvent() { }
        public override GameEvent Clone() { return new KeyCheckEvent(); }
        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.User.MemberTeam == DungeonScene.Instance.ActiveTeam)
            {
                if (context.User != context.User.MemberTeam.Leader)
                {
                    DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_LEADER_ONLY_ITEM").ToLocal()));
                    context.CancelState.Cancel = true;
                }
                else
                {
                    //check if the tile in front can be unlocked
                    bool unlockable = false;
                    Loc hitLoc = context.User.CharLoc + context.User.CharDir.GetLoc();

                    Tile tile = ZoneManager.Instance.CurrentMap.GetTile(hitLoc);
                    if (tile != null && !String.IsNullOrEmpty(tile.Effect.ID))
                    {
                        TileData tileData = DataManager.Instance.GetTile(tile.Effect.ID);
                        if (tileData.StepType == TileData.TriggerType.Unlockable)
                        {
                            UnlockState unlock = tile.Effect.TileStates.GetWithDefault<UnlockState>();
                            if (unlock != null && unlock.UnlockItem == context.Item.ID)
                                unlockable = true;
                        }
                    }

                    if (!unlockable)
                    {
                        DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_KEY_MISS").ToLocal()));
                        context.CancelState.Cancel = true;
                    }
                }
            }
            else
                context.CancelState.Cancel = true;
            yield break;
        }
    }

    /// <summary>
    /// Event that applies the effects of the unlockable tile if the item matches in the UnlockState tile state 
    /// </summary>
    [Serializable]
    public class KeyUnlockEvent : BattleEvent
    {
        public KeyUnlockEvent() { }
        public override GameEvent Clone() { return new KeyUnlockEvent(); }
        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            Tile tile = ZoneManager.Instance.CurrentMap.GetTile(context.TargetTile);
            if (tile == null)
                yield break;

            if (!String.IsNullOrEmpty(tile.Effect.ID))
            {
                TileData entry = DataManager.Instance.GetTile(tile.Effect.GetID());
                if (entry.StepType == TileData.TriggerType.Unlockable)
                {
                    UnlockState unlock = tile.Effect.TileStates.GetWithDefault<UnlockState>();
                    if (unlock != null && unlock.UnlockItem == context.Item.ID)
                    {
                        SingleCharContext singleContext = new SingleCharContext(context.User);
                        yield return CoroutineManager.Instance.StartCoroutine(tile.Effect.InteractWithTile(singleContext));
                    }
                }
            }
        }
    }

    /// <summary>
    /// Event that teaches the user the move in the item's ItemIDState
    /// </summary>
    [Serializable]
    public class TMEvent : BattleEvent
    {
        public override GameEvent Clone() { return new TMEvent(); }
        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            BaseMonsterForm entry = DataManager.Instance.GetMonster(context.User.BaseForm.Species).Forms[context.User.BaseForm.Form];
            ItemData item = DataManager.Instance.GetItem(owner.GetID());
            string moveIndex = "";
            ItemIDState state = item.ItemStates.GetWithDefault<ItemIDState>();
            if (state != null)
                moveIndex = state.ID;

            if (!entry.CanLearnSkill(moveIndex))
            {
                context.CancelState.Cancel = true;
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_CANT_LEARN_SKILL").ToLocal(), context.User.GetDisplayName(false)));
                yield break;
            }


            if (DataManager.Instance.CurrentReplay != null) // this block of code will never evaluate to true AND have UI read back -1 (cancel) at the same time
            {
                MoveLearnContext learn = new MoveLearnContext();
                learn.MoveLearn = moveIndex;
                learn.ReplaceSlot = DataManager.Instance.CurrentReplay.ReadUI();
                context.ContextStates.Set(learn);
            }
            else
            {
                yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.TryLearnSkill(context.User, moveIndex,
                (int slot) =>
                {
                    MoveLearnContext learn = new MoveLearnContext();
                    learn.MoveLearn = moveIndex;
                    learn.ReplaceSlot = slot;
                    context.ContextStates.Set(learn);
                },
                () => { context.CancelState.Cancel = true; }));

                if (!context.CancelState.Cancel)
                {
                    int slot = -1;
                    MoveLearnContext learn = context.ContextStates.GetWithDefault<MoveLearnContext>();
                    if (learn != null)
                        slot = learn.ReplaceSlot;
                    DataManager.Instance.LogUIPlay(slot);
                }
            }
        }

    }

    /// <summary>
    /// Event that prompts the user which form to change to and sets the value in SwitchFormContext
    /// </summary>
    [Serializable]
    public class FormChoiceEvent : BattleEvent
    {
        /// <summary>
        /// The required species for this event to have effect 
        /// </summary>
        [JsonConverter(typeof(MonsterConverter))]
        [DataType(0, DataManager.DataType.Monster, false)]
        public string Species;

        /// <summary>
        /// Whether to include temporary forms as an option 
        /// </summary>
        public bool IncludeTemp;

        public FormChoiceEvent() { Species = ""; }
        public FormChoiceEvent(string species) { Species = species; }
        public FormChoiceEvent(FormChoiceEvent other) { Species = other.Species; IncludeTemp = other.IncludeTemp; }
        public override GameEvent Clone() { return new FormChoiceEvent(this); }
        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.User.MemberTeam == DungeonScene.Instance.ActiveTeam)
            {
                List<int> eligibleForms = new List<int>();
                MonsterData entry = DataManager.Instance.GetMonster(context.User.BaseForm.Species);
                if (context.User.BaseForm.Species == Species)
                {

                    for (int ii = 0; ii < entry.Forms.Count; ii++)
                    {
                        if (context.User.BaseForm.Form == ii)
                            continue;
                        BaseMonsterForm form = entry.Forms[ii];
                        if (!form.Released)
                            continue;
                        if (!IncludeTemp && form.Temporary)
                            continue;
                        eligibleForms.Add(ii);
                    }
                }

                if (eligibleForms.Count > 1)
                {
                    if (DataManager.Instance.CurrentReplay != null) // this block of code will never evaluate to true AND have UI read back -1 (cancel) at the same time
                    {
                        SwitchFormContext change = new SwitchFormContext();
                        change.Form = DataManager.Instance.CurrentReplay.ReadUI();
                        context.ContextStates.Set(change);
                    }
                    else
                    {
                        List<DialogueChoice> choices = new List<DialogueChoice>();
                        foreach (int form in eligibleForms)
                        {
                            choices.Add(new DialogueChoice(entry.Forms[form].FormName.ToLocal(), () =>
                            {
                                SwitchFormContext change = new SwitchFormContext();
                                change.Form = form;
                                context.ContextStates.Set(change);
                            }));
                        }

                        choices.Add(new DialogueChoice(Text.FormatKey("MENU_CANCEL"), () => { context.CancelState.Cancel = true; }));
                        DialogueBox question = MenuManager.Instance.CreateMultiQuestion(Text.FormatGrammar(new StringKey("DLG_WHICH_FORM").ToLocal(), context.User.GetDisplayName(true)), true, choices, 0, choices.Count - 1);

                        yield return CoroutineManager.Instance.StartCoroutine(MenuManager.Instance.ProcessMenuCoroutine(question));

                        if (!context.CancelState.Cancel)
                        {
                            int formNum = -1;
                            SwitchFormContext change = context.ContextStates.GetWithDefault<SwitchFormContext>();
                            if (change != null)
                                formNum = change.Form;
                            DataManager.Instance.LogUIPlay(formNum);
                        }
                    }
                }
                else if (eligibleForms.Count == 1)
                {
                    SwitchFormContext change = new SwitchFormContext();
                    change.Form = eligibleForms[0];
                    context.ContextStates.Set(change);
                }
                else
                {
                    yield return CoroutineManager.Instance.StartCoroutine(MenuManager.Instance.SetDialogue(Text.FormatGrammar(new StringKey("MSG_ITEM_NO_EFFECT").ToLocal(), context.User.GetDisplayName(true))));
                    context.CancelState.Cancel = true;
                }
            }
            else
                context.CancelState.Cancel = true;
        }

    }

    /// <summary>
    /// Event that deactivates the use of the item by setting its hidden value 
    /// </summary>
    [Serializable]
    public class DeactivateItemEvent : BattleEvent
    {
        public DeactivateItemEvent() { }
        public override GameEvent Clone() { return new DeactivateItemEvent(); }
        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.UsageSlot > BattleContext.EQUIP_ITEM_SLOT)//item in inventory
            {
                InvItem item = ((ExplorerTeam)context.User.MemberTeam).GetInv(context.UsageSlot);
                item.HiddenValue = item.ID;
            }
            else if (context.UsageSlot == BattleContext.EQUIP_ITEM_SLOT)
            {
                InvItem item = context.User.EquippedItem;
                item.HiddenValue = item.ID;
            }
            else if (context.UsageSlot == BattleContext.FLOOR_ITEM_SLOT)
            {
                int mapSlot = ZoneManager.Instance.CurrentMap.GetItem(context.User.CharLoc);
                MapItem mapItem = ZoneManager.Instance.CurrentMap.Items[mapSlot];
                mapItem.HiddenValue = mapItem.Value;
            }
            yield break;
        }
    }

    /// <summary>
    /// Event that changes the form of the user using the value in SwitchFormContext
    /// </summary>
    [Serializable]
    public class SwitchFormEvent : BattleEvent
    {
        public SwitchFormEvent() { }
        public override GameEvent Clone() { return new SwitchFormEvent(); }
        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            int form = -1;
            SwitchFormContext change = context.ContextStates.GetWithDefault<SwitchFormContext>();
            if (change != null)
                form = change.Form;
            if (form > -1)
            {
                context.User.Promote(new MonsterID(context.User.CurrentForm.Species, form, context.User.CurrentForm.Skin, context.User.CurrentForm.Gender));
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_FORM_CHANGE").ToLocal(), context.User.GetDisplayName(false)));
            }
            yield break;
        }
    }

    /// <summary>
    /// Event that prompts the user to recall or delete moves and sets up MoveLearnContext and MoveDeleteContext
    /// </summary>
    [Serializable]
    public class LinkBoxEvent : BattleEvent
    {
        /// <summary>
        /// Whether pre-evolution moves can be relearned
        /// </summary>
        public bool IncludePreEvolutions;
        public LinkBoxEvent() { }
        public override GameEvent Clone() { return new LinkBoxEvent(); }
        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            List<string> forgottenMoves = context.User.GetRelearnableSkills(IncludePreEvolutions);

            if (DataManager.Instance.CurrentReplay != null)// this block of code will never evaluate to true AND have UI read back -1 (cancel)
            {
                int action = DataManager.Instance.CurrentReplay.ReadUI();
                if (action == 0)
                {
                    MoveLearnContext learn = new MoveLearnContext();
                    learn.MoveLearn = forgottenMoves[DataManager.Instance.CurrentReplay.ReadUI()];
                    learn.ReplaceSlot = DataManager.Instance.CurrentReplay.ReadUI();
                    context.ContextStates.Set(learn);
                }
                else if (action == 1)
                {
                    int deleteSlot = DataManager.Instance.CurrentReplay.ReadUI();
                    context.ContextStates.Set(new MoveDeleteContext(deleteSlot));
                }
                else
                    throw new Exception("Operation must learn or delete a move.");
            }
            else
            {
                yield return CoroutineManager.Instance.StartCoroutine(MenuManager.Instance.ProcessMenuCoroutine(createLinkBoxDialog(context, forgottenMoves)));

                if (!context.CancelState.Cancel)
                {
                    int deleteSlot = -1;
                    MoveDeleteContext delete = context.ContextStates.GetWithDefault<MoveDeleteContext>();
                    if (delete != null)
                        deleteSlot = delete.MoveDelete;

                    string moveLearn = "";
                    int learnSlot = -1;
                    MoveLearnContext learn = context.ContextStates.GetWithDefault<MoveLearnContext>();
                    if (learn != null)
                    {
                        moveLearn = learn.MoveLearn;
                        learnSlot = learn.ReplaceSlot;
                    }

                    if (!String.IsNullOrEmpty(moveLearn))
                    {
                        DataManager.Instance.LogUIPlay(0, forgottenMoves.IndexOf(moveLearn), learnSlot);
                    }
                    else if (deleteSlot > -1)
                    {
                        DataManager.Instance.LogUIPlay(1, deleteSlot);
                    }
                    else
                        throw new Exception("Link box must learn or delete a move.");
                }
            }
        }


        private DialogueBox createLinkBoxDialog(BattleContext context, List<string> forgottenMoves)
        {
            List<DialogueChoice> choices = new List<DialogueChoice>();
            choices.Add(new DialogueChoice(Text.FormatGrammar(new StringKey("MENU_RECALL_SKILL").ToLocal()), () => { MenuManager.Instance.AddMenu(createRememberDialog(context, forgottenMoves), false); }));
            choices.Add(new DialogueChoice(Text.FormatGrammar(new StringKey("MENU_FORGET_SKILL").ToLocal()), () =>
            {
                int totalMoves = 0;
                foreach (SlotSkill move in context.User.BaseSkills)
                {
                    if (!String.IsNullOrEmpty(move.SkillNum))
                        totalMoves++;
                }
                if (totalMoves > 1)
                {
                    MenuManager.Instance.AddMenu(new SkillForgetMenu(context.User,
                        (int slot) => { context.ContextStates.Set(new MoveDeleteContext(slot)); },
                        () => { MenuManager.Instance.AddMenu(createLinkBoxDialog(context, forgottenMoves), false); }), false);
                }
                else
                    MenuManager.Instance.AddMenu(MenuManager.Instance.CreateDialogue(() => { MenuManager.Instance.AddMenu(createLinkBoxDialog(context, forgottenMoves), false); },
                    Text.FormatGrammar(new StringKey("DLG_CANT_FORGET_SKILL").ToLocal(), context.User.GetDisplayName(true))), false);

            }));
            choices.Add(new DialogueChoice(Text.FormatKey("MENU_CANCEL"), () => { context.CancelState.Cancel = true; }));
            return MenuManager.Instance.CreateMultiQuestion(Text.FormatKey("DLG_WHAT_DO"), true, choices, 0, 2);
        }

        private IInteractable createRememberDialog(BattleContext context, List<string> forgottenMoves)
        {
            if (forgottenMoves.Count > 0)
            {
                return new SkillRecallMenu(context.User, forgottenMoves.ToArray(), (int moveSlot) =>
                {
                    string moveNum = forgottenMoves[moveSlot];
                    MenuManager.Instance.NextAction = DungeonScene.TryLearnSkill(context.User, moveNum,
                        (int slot) =>
                        {
                            MoveLearnContext learn = new MoveLearnContext();
                            learn.MoveLearn = moveNum;
                            learn.ReplaceSlot = slot;
                            context.ContextStates.Set(learn);
                        },
                        () => { MenuManager.Instance.AddMenu(createRememberDialog(context, forgottenMoves), false); });
                }, () => { MenuManager.Instance.AddMenu(createLinkBoxDialog(context, forgottenMoves), false); });
            }
            else
                return MenuManager.Instance.CreateDialogue(() => { MenuManager.Instance.AddMenu(createLinkBoxDialog(context, forgottenMoves), false); },
                    Text.FormatGrammar(new StringKey("DLG_CANT_RECALL_SKILL").ToLocal(), context.User.GetDisplayName(true)));

        }

    }

    /// <summary>
    /// Event that causes the user to relearn a move using the value in MoveLearnContext 
    /// </summary>
    [Serializable]
    public class MoveLearnEvent : BattleEvent
    {
        public MoveLearnEvent() { }
        public override GameEvent Clone() { return new MoveLearnEvent(); }
        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            string moveNum = "";
            int moveSlot = -1;
            MoveLearnContext learn = context.ContextStates.GetWithDefault<MoveLearnContext>();
            if (learn != null)
            {
                moveNum = learn.MoveLearn;
                moveSlot = learn.ReplaceSlot;
            }
            if (!String.IsNullOrEmpty(moveNum) && moveSlot > -1)
                yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.LearnSkillWithFanfare(context.User, moveNum, moveSlot));
        }
    }

    /// <summary>
    /// Event that causes the user to delete a move using the value in MoveDeleteContext 
    /// </summary>
    [Serializable]
    public class MoveDeleteEvent : BattleEvent
    {
        public MoveDeleteEvent() { }
        public override GameEvent Clone() { return new MoveDeleteEvent(); }
        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            int slot = -1;
            MoveDeleteContext delete = context.ContextStates.GetWithDefault<MoveDeleteContext>();
            if (delete != null)
                slot = delete.MoveDelete;
            if (slot > -1)
            {
                string moveNum = context.User.BaseSkills[slot].SkillNum;
                context.User.DeleteSkill(slot);
                yield return CoroutineManager.Instance.StartCoroutine(GameManager.Instance.LogSkippableMsg(Text.FormatGrammar(new StringKey("DLG_FORGET_SKILL").ToLocal(), context.User.GetDisplayName(false), DataManager.Instance.GetSkill(moveNum).GetIconName()), context.User.MemberTeam));
            }
        }
    }

    /// <summary>
    /// Event that prompts the user to learn a new ability and sets up AbilityLearnContext 
    /// </summary>
    [Serializable]
    public class AbilityCapsuleEvent : BattleEvent
    {
        public AbilityCapsuleEvent() { }
        public override GameEvent Clone() { return new AbilityCapsuleEvent(); }
        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.User.MemberTeam == DungeonScene.Instance.ActiveTeam)
            {
                BaseMonsterForm entry = DataManager.Instance.GetMonster(context.User.BaseForm.Species).Forms[context.User.BaseForm.Form];
                List<string> eligibleAbilities = new List<string>();

                if (entry.Intrinsic1 != DataManager.Instance.DefaultIntrinsic && context.User.BaseIntrinsics[0] != entry.Intrinsic1)
                    eligibleAbilities.Add(entry.Intrinsic1);
                if (entry.Intrinsic2 != DataManager.Instance.DefaultIntrinsic && context.User.BaseIntrinsics[0] != entry.Intrinsic2)
                    eligibleAbilities.Add(entry.Intrinsic2);
                if (entry.Intrinsic3 != DataManager.Instance.DefaultIntrinsic && context.User.BaseIntrinsics[0] != entry.Intrinsic3)
                    eligibleAbilities.Add(entry.Intrinsic3);

                if (eligibleAbilities.Count > 0)
                {
                    int chosenSlot = -1;
                    if (DataManager.Instance.CurrentReplay != null) // this block of code will never evaluate to true AND have UI read back -1 (cancel) at the same time
                        chosenSlot = DataManager.Instance.CurrentReplay.ReadUI();
                    else
                    {
                        yield return CoroutineManager.Instance.StartCoroutine(MenuManager.Instance.ProcessMenuCoroutine(new IntrinsicRecallMenu(context.User, eligibleAbilities.ToArray(),
                            (int abilitySlot) => { chosenSlot = abilitySlot; }, () => { context.CancelState.Cancel = true; })));

                        if (chosenSlot > -1)
                            DataManager.Instance.LogUIPlay(chosenSlot);
                    }

                    if (!context.CancelState.Cancel)
                    {
                        AbilityLearnContext learn = new AbilityLearnContext();
                        learn.AbilityLearn = eligibleAbilities[chosenSlot];
                        learn.ReplaceSlot = 0;
                        context.ContextStates.Set(learn);
                    }
                }
                else
                {
                    yield return CoroutineManager.Instance.StartCoroutine(MenuManager.Instance.SetDialogue(Text.FormatGrammar(new StringKey("DLG_CANT_RECALL_INTRINSIC").ToLocal(), context.User.GetDisplayName(true))));
                    context.CancelState.Cancel = true;
                }
            }
            else
                context.CancelState.Cancel = true;
        }

    }

    /// <summary>
    /// Event that causes the user to learn a new ability using the value in AbilityLearnContext 
    /// </summary>
    [Serializable]
    public class AbilityLearnEvent : BattleEvent
    {
        public AbilityLearnEvent() { }
        public override GameEvent Clone() { return new AbilityLearnEvent(); }
        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            string abilityNum = "";
            int abilitySlot = -1;
            AbilityLearnContext learn = context.ContextStates.GetWithDefault<AbilityLearnContext>();
            if (learn != null)
            {
                abilityNum = learn.AbilityLearn;
                abilitySlot = learn.ReplaceSlot;
            }
            if (!String.IsNullOrEmpty(abilityNum))
            {
                GameManager.Instance.SE("Fanfare/LearnSkill");
                context.User.LearnIntrinsic(abilityNum, abilitySlot);

                yield return CoroutineManager.Instance.StartCoroutine(GameManager.Instance.LogSkippableMsg(Text.FormatGrammar(new StringKey("DLG_LEARN_INTRINSIC").ToLocal(), context.User.GetDisplayName(false), DataManager.Instance.GetIntrinsic(abilityNum).GetColoredName()), context.User.MemberTeam));
            }
        }
    }

    /// <summary>
    /// Event that deletes the user's ability based on the value in the AbilityDeleteContext 
    /// </summary>
    [Serializable]
    public class AbilityDeleteEvent : BattleEvent
    {
        public AbilityDeleteEvent() { }
        public override GameEvent Clone() { return new AbilityDeleteEvent(); }
        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            int slot = -1;
            AbilityDeleteContext delete = context.ContextStates.GetWithDefault<AbilityDeleteContext>();
            if (delete != null)
                slot = delete.AbilityDelete;
            if (slot > -1)
            {
                string abilityNum = context.User.BaseIntrinsics[slot];
                context.User.DeleteIntrinsic(slot);

                yield return CoroutineManager.Instance.StartCoroutine(GameManager.Instance.LogSkippableMsg(Text.FormatGrammar(new StringKey("DLG_FORGET_INTRINSIC").ToLocal(), context.User.GetDisplayName(false), DataManager.Instance.GetIntrinsic(abilityNum).GetColoredName()), context.User.MemberTeam));
            }
        }
    }


    /// <summary>
    /// Event that prompts the user which item to withdraw from the storage and sets up WithdrawStorageContext 
    /// </summary>
    [Serializable]
    public class StorageBoxEvent : BattleEvent
    {
        public StorageBoxEvent() { }
        public override GameEvent Clone() { return new StorageBoxEvent(); }
        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.User.MemberTeam == DungeonScene.Instance.ActiveTeam)
            {
                bool hasItems = (DungeonScene.Instance.ActiveTeam.BoxStorage.Count > 0);
                foreach (string key in DungeonScene.Instance.ActiveTeam.Storage.Keys)
                {
                    if (DungeonScene.Instance.ActiveTeam.Storage[key] > 0)
                    {
                        hasItems = true;
                        break;
                    }
                }
                if (context.User != context.User.MemberTeam.Leader)
                {
                    DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_LEADER_ONLY_ITEM").ToLocal()));
                    context.CancelState.Cancel = true;
                }
                else if (!hasItems)
                {
                    yield return CoroutineManager.Instance.StartCoroutine(MenuManager.Instance.SetDialogue(Text.FormatGrammar(new StringKey("DLG_STORAGE_EMPTY").ToLocal())));
                    context.CancelState.Cancel = true;
                }
                else
                {
                    if (DataManager.Instance.CurrentReplay != null) // this block of code will never evaluate to true AND have UI read back -1 (cancel) at the same time
                    {
                        bool isBox = DataManager.Instance.CurrentReplay.ReadUI() != 0;
                        string id = DataManager.Instance.CurrentReplay.ReadUIString();
                        int slot = DataManager.Instance.CurrentReplay.ReadUI();
                        context.ContextStates.Set(new WithdrawStorageContext(new WithdrawSlot(isBox, id, slot)));
                    }
                    else
                    {
                        yield return CoroutineManager.Instance.StartCoroutine(MenuManager.Instance.SetDialogue(Text.FormatGrammar(new StringKey("DLG_ASK_STORAGE").ToLocal())));

                        bool chose = false;
                        yield return CoroutineManager.Instance.StartCoroutine(MenuManager.Instance.ProcessMenuCoroutine(new WithdrawMenu(0, false,
                            (List<WithdrawSlot> slots) => { context.ContextStates.Set(new WithdrawStorageContext(slots[0])); chose = true; })));

                        if (chose)
                        {
                            WithdrawStorageContext withdraw = context.ContextStates.GetWithDefault<WithdrawStorageContext>();
                            if (withdraw != null)
                            {
                                DataManager.Instance.LogUIPlay(withdraw.WithdrawSlot.IsBox ? 1 : 0);
                                DataManager.Instance.LogUIStringPlay(withdraw.WithdrawSlot.ItemID);
                                DataManager.Instance.LogUIPlay(withdraw.WithdrawSlot.BoxSlot);
                            }
                        }
                        else
                            context.CancelState.Cancel = true;
                    }
                }
            }
            else
                context.CancelState.Cancel = true;
        }

    }

    /// <summary>
    /// Event that withdraws an item from storage using the value in WithdrawStorageContext
    /// </summary>
    [Serializable]
    public class WithdrawItemEvent : BattleEvent
    {
        public WithdrawItemEvent() { }
        public override GameEvent Clone() { return new WithdrawItemEvent(); }
        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            WithdrawStorageContext withdraw = context.ContextStates.GetWithDefault<WithdrawStorageContext>();
            if (withdraw != null)
            {
                WithdrawSlot slot = withdraw.WithdrawSlot;

                if (context.User.MemberTeam == DungeonScene.Instance.ActiveTeam)
                {
                    ExplorerTeam team = (ExplorerTeam)context.User.MemberTeam;
                    InvItem item = team.TakeItems(new List<WithdrawSlot> { slot })[0];

                    DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_STORAGE_TAKE").ToLocal(), context.User.GetDisplayName(false), item.GetDisplayName()));
                    if (team.GetInvCount() < team.GetMaxInvSlots(ZoneManager.Instance.CurrentZone))
                        team.AddToInv(item);
                    else
                        yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.Instance.DropItem(item, context.User.CharLoc));
                }
            }
        }
    }



    /// <summary>
    /// Event that selects the item currently held by the user to send to the storage 
    /// </summary>
    [Serializable]
    public class DepositBoxEvent : BattleEvent
    {
        public DepositBoxEvent() { }
        public override GameEvent Clone() { return new DepositBoxEvent(); }
        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.User.MemberTeam == DungeonScene.Instance.ActiveTeam)
            {
                if (!String.IsNullOrEmpty(context.User.EquippedItem.ID) && context.UsageSlot != BattleContext.EQUIP_ITEM_SLOT)
                {
                    InvSlot chosenSlot = new InvSlot(true, context.User.MemberTeam.GetCharIndex(context.User).Char);

                    //TODO: make this into an inventory UI for the player to choose what to send to deposit.  make an exception for the usage slot itself

                    if (!context.CancelState.Cancel)
                    {
                        DepositStorageContext deposit = new DepositStorageContext(chosenSlot);
                        context.ContextStates.Set(deposit);
                    }
                }
                else
                {
                    yield return CoroutineManager.Instance.StartCoroutine(MenuManager.Instance.SetDialogue(Text.FormatGrammar(new StringKey("DLG_NO_HELD_ITEM").ToLocal(), context.User.GetDisplayName(true))));
                    context.CancelState.Cancel = true;
                }
            }
            else
                context.CancelState.Cancel = true;
        }

    }

    /// <summary>
    /// Event that stores an item using the value in DepositStorageContext
    /// </summary>
    [Serializable]
    public class StoreItemEvent : BattleEvent
    {
        public StoreItemEvent() { }
        public override GameEvent Clone() { return new StoreItemEvent(); }
        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            DepositStorageContext deposit = context.ContextStates.GetWithDefault<DepositStorageContext>();
            if (deposit != null)
            {
                InvSlot slot = deposit.DepositSlot;

                if (context.User.MemberTeam == DungeonScene.Instance.ActiveTeam)
                {
                    ExplorerTeam team = (ExplorerTeam)context.User.MemberTeam;
                    InvItem item;
                    if (slot.IsEquipped)
                    {
                        item = team.Players[slot.Slot].EquippedItem;
                        yield return CoroutineManager.Instance.StartCoroutine(team.Players[slot.Slot].DequipItem());
                    }
                    else
                    {
                        item = team.GetInv(slot.Slot);
                        team.RemoveFromInv(slot.Slot);
                    }

                    DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_STORAGE_STORE").ToLocal(), context.User.GetDisplayName(false), item.GetDisplayName()));
                    team.StoreItems(new List<InvItem> { item });
                }
            }
        }
    }


    /// <summary>
    /// Event that prompts the user which assembly member to add to the team the sets up WithdrawStorageContext 
    /// </summary>
    [Serializable]
    public class AssemblyBoxEvent : BattleEvent
    {
        public AssemblyBoxEvent() { }
        public override GameEvent Clone() { return new AssemblyBoxEvent(); }
        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.User.MemberTeam == DungeonScene.Instance.ActiveTeam)
            {
                if (context.User != context.User.MemberTeam.Leader)
                {
                    DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_LEADER_ONLY_ITEM").ToLocal()));
                    context.CancelState.Cancel = true;
                    yield break;
                }
                bool hasBench = false;
                ExplorerTeam team = ((ExplorerTeam)context.User.MemberTeam);
                foreach (Character chara in team.Assembly)
                {
                    //if (!chara.Absentee)
                    //{
                    hasBench = true;
                    break;
                    //}
                }
                if (!hasBench)
                {
                    yield return CoroutineManager.Instance.StartCoroutine(MenuManager.Instance.SetDialogue(Text.FormatGrammar(new StringKey("MSG_ASSEMBLY_EMPTY").ToLocal())));
                    context.CancelState.Cancel = true;
                }
                else
                {
                    if (DataManager.Instance.CurrentReplay != null) // this block of code will never evaluate to true AND have UI read back -1 (cancel) at the same time
                    {
                        int slot = DataManager.Instance.CurrentReplay.ReadUI();
                        context.ContextStates.Set(new WithdrawAssemblyContext(slot));
                    }
                    else
                    {
                        yield return CoroutineManager.Instance.StartCoroutine(MenuManager.Instance.SetDialogue(Text.FormatGrammar(new StringKey("MSG_ASK_ASSEMBLY").ToLocal())));

                        yield return CoroutineManager.Instance.StartCoroutine(MenuManager.Instance.ProcessMenuCoroutine(new AddToTeamMenu(
                            (List<int> slots) => { context.ContextStates.Set(new WithdrawAssemblyContext(slots[0])); },
                            () => { context.CancelState.Cancel = true; })));

                        if (!context.CancelState.Cancel)
                        {
                            int slot = -1;
                            WithdrawAssemblyContext withdraw = context.ContextStates.GetWithDefault<WithdrawAssemblyContext>();
                            if (withdraw != null)
                                slot = withdraw.WithdrawSlot;
                            DataManager.Instance.LogUIPlay(slot);
                        }
                    }
                }
            }
            else
                context.CancelState.Cancel = true;
        }
    }

    /// <summary>
    /// Event that adds a team member from assembly using the value in WithdrawStorageContext
    /// </summary>
    [Serializable]
    public class WithdrawRecruitEvent : BattleEvent
    {
        public WithdrawRecruitEvent() { }
        public override GameEvent Clone() { return new WithdrawRecruitEvent(); }
        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            int slot = -1;
            WithdrawAssemblyContext withdraw = context.ContextStates.GetWithDefault<WithdrawAssemblyContext>();
            if (withdraw != null)
                slot = withdraw.WithdrawSlot;
            if (slot > -1)
            {
                Character member = ((ExplorerTeam)context.User.MemberTeam).Assembly[slot];
                ((ExplorerTeam)context.User.MemberTeam).Assembly.RemoveAt(slot);
                Loc? endLoc = ZoneManager.Instance.CurrentMap.GetClosestTileForChar(member, context.User.CharLoc);
                if (endLoc == null)
                    endLoc = context.User.CharLoc;
                member.CharLoc = endLoc.Value;

                GameManager.Instance.BattleSE("DUN_Send_Home");
                SingleEmitter emitter = new SingleEmitter(new BeamAnimData("Column_Yellow", 3));
                emitter.Layer = DrawLayer.Front;
                emitter.SetupEmit(member.CharLoc * GraphicsManager.TileSize + new Loc(GraphicsManager.TileSize / 2), member.CharLoc * GraphicsManager.TileSize + new Loc(GraphicsManager.TileSize / 2), member.CharDir);
                DungeonScene.Instance.CreateAnim(emitter, DrawLayer.NoDraw);
                DungeonScene.Instance.AddCharToTeam(Faction.Player, 0, false, member);
                member.Absentee = false;
                member.Tactic = new AITactic(member.Tactic);
                member.RefreshTraits();
                member.Tactic.Initialize(member);
                ZoneManager.Instance.CurrentMap.UpdateExploration(member);

                yield return CoroutineManager.Instance.StartCoroutine(GameManager.Instance.LogSkippableMsg(Text.FormatGrammar(new StringKey("MSG_ASSEMBLY_TAKE_ANY").ToLocal(), member.GetDisplayName(true))));

                yield return CoroutineManager.Instance.StartCoroutine(member.OnMapStart());

                if (DungeonScene.Instance.ActiveTeam.Players.Count > DungeonScene.Instance.ActiveTeam.GetMaxTeam(ZoneManager.Instance.CurrentZone))
                    yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.Instance.AskToSendHome());
            }
        }
    }

}

