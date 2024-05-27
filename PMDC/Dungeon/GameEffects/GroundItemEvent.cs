
using System;
using System.Collections.Generic;
using RogueEssence;
using RogueEssence.Data;
using RogueEssence.Dev;
using RogueEssence.Dungeon;
using RogueEssence.Ground;
using RogueEssence.Menu;

namespace PMDC.Dungeon
{
    [Serializable]
    public class AbilityCapsuleItemEvent : GroundItemEvent
    {
        public override GameEvent Clone() { return new AbilityCapsuleItemEvent(); }
        public override IEnumerator<YieldInstruction> Apply(GroundContext context)
        {
            Character target = context.User;
            BaseMonsterForm entry = DataManager.Instance.GetMonster(target.BaseForm.Species).Forms[target.BaseForm.Form];
            List<string> eligibleAbilities = new List<string>();

            if (entry.Intrinsic1 != DataManager.Instance.DefaultIntrinsic && target.BaseIntrinsics[0] != entry.Intrinsic1)
                eligibleAbilities.Add(entry.Intrinsic1);
            if (entry.Intrinsic2 != DataManager.Instance.DefaultIntrinsic && target.BaseIntrinsics[0] != entry.Intrinsic2)
                eligibleAbilities.Add(entry.Intrinsic2);
            if (entry.Intrinsic3 != DataManager.Instance.DefaultIntrinsic && target.BaseIntrinsics[0] != entry.Intrinsic3)
                eligibleAbilities.Add(entry.Intrinsic3);

            if (eligibleAbilities.Count > 0)
            {
                int chosenSlot = -1;
                yield return CoroutineManager.Instance.StartCoroutine(MenuManager.Instance.ProcessMenuCoroutine(new IntrinsicRecallMenu(target, eligibleAbilities.ToArray(),
                    (int abilitySlot) => { chosenSlot = abilitySlot; }, () => { context.CancelState.Cancel = true; })));

                if (context.CancelState.Cancel) yield break;
                string ability = eligibleAbilities[chosenSlot];
                GameManager.Instance.SE("Fanfare/LearnSkill");
                target.LearnIntrinsic(ability, 0);
                yield return CoroutineManager.Instance.StartCoroutine(GameManager.Instance.LogSkippableMsg(Text.FormatGrammar(new StringKey("DLG_LEARN_INTRINSIC").ToLocal(), target.GetDisplayName(false), DataManager.Instance.GetIntrinsic(ability).GetColoredName()), target.MemberTeam));
            }
            else
            {
                yield return CoroutineManager.Instance.StartCoroutine(MenuManager.Instance.SetDialogue(Text.FormatGrammar(new StringKey("DLG_CANT_RECALL_INTRINSIC").ToLocal(), target.GetDisplayName(true))));
                context.CancelState.Cancel = true;
            }
        }
    }

    [Serializable]
    public class RecallBoxEvent : GroundItemEvent
    {
        public bool IncludePreEvolutions;
        protected RecallBoxEvent(RecallBoxEvent other) { IncludePreEvolutions = other.IncludePreEvolutions; }
        public RecallBoxEvent(bool includePreEvolution) { IncludePreEvolutions = includePreEvolution; }
        public override GameEvent Clone() { return new RecallBoxEvent(this); }
        
        public override IEnumerator<YieldInstruction> Apply(GroundContext context)
        {
            List<string> forgottenMoves = context.User.GetRelearnableSkills(IncludePreEvolutions);
            yield return CoroutineManager.Instance.StartCoroutine(MenuManager.Instance.ProcessMenuCoroutine(createLinkBoxDialog(context, forgottenMoves)));
            if (!context.CancelState.Cancel)
            {
                MoveDeleteContext delete = context.ContextStates.GetWithDefault<MoveDeleteContext>();
                if (delete != null)
                {
                    string moveNum = context.User.BaseSkills[delete.MoveDelete].SkillNum;
                    context.User.DeleteSkill(delete.MoveDelete);
                    yield return CoroutineManager.Instance.StartCoroutine(GameManager.Instance.LogSkippableMsg(Text.FormatGrammar(new StringKey("DLG_FORGET_SKILL").ToLocal(), context.User.GetDisplayName(false), DataManager.Instance.GetSkill(moveNum).GetIconName()), context.User.MemberTeam));
                }
                
                MoveLearnContext learn = context.ContextStates.GetWithDefault<MoveLearnContext>();
                if (learn != null)
                {
                    if (!String.IsNullOrEmpty(learn.MoveLearn))
                    {
                        yield return CoroutineManager.Instance.StartCoroutine(
                            DungeonScene.LearnSkillWithFanfare(context.User, learn.MoveLearn, learn.ReplaceSlot));
                    }
                }
            }
        }
        
        private DialogueBox createLinkBoxDialog(GroundContext context, List<string> forgottenMoves)
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

        private IInteractable createRememberDialog(GroundContext context, List<string> forgottenMoves)
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
    
}
