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
    // Battle events that invoke attacks or attack effects


    [Serializable]
    public abstract class InvokeBattleEvent : BattleEvent
    {
        protected abstract BattleContext CreateContext(GameEventOwner owner, Character ownerChar, BattleContext context);
        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            BattleContext newContext = CreateContext(owner, ownerChar, context);
            if (newContext == null)
                yield break;

            //beforetryaction and beforeAction need to distinguish forced effects vs willing effects for all times it's triggered
            //as a forced attack, preprocessaction also should not factor in confusion dizziness
            //examples where the distinction matters:
            //-counting down
            //-confusion dizziness
            //-certain kinds of status-based move prevention
            //-forced actions (charging moves, rampage moves, etc)

            yield return CoroutineManager.Instance.StartCoroutine(newContext.User.BeforeTryAction(newContext));
            if (newContext.CancelState.Cancel) { yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.Instance.CancelWait(newContext.User.CharLoc)); yield break; }
            yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.Instance.PreProcessAction(newContext));

            //Handle Use
            yield return CoroutineManager.Instance.StartCoroutine(newContext.User.BeforeAction(newContext));
            if (newContext.CancelState.Cancel) { yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.Instance.CancelWait(newContext.User.CharLoc)); yield break; }

            newContext.PrintActionMsg();

            yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.Instance.ExecuteAction(newContext));
            if (newContext.CancelState.Cancel) { yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.Instance.CancelWait(newContext.User.CharLoc)); yield break; }
            yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.Instance.RepeatActions(newContext));
        }
    }

    [Serializable]
    public abstract class InvokedMoveEvent : InvokeBattleEvent
    {
        protected abstract string GetInvokedMove(GameEventOwner owner, BattleContext context);
        protected override BattleContext CreateContext(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            string moveID = "";

            if (context.UsageSlot != BattleContext.FORCED_SLOT)
                moveID = GetInvokedMove(owner, context);

            if (!String.IsNullOrEmpty(moveID))
            {
                SkillData entry = DataManager.Instance.GetSkill(moveID);

                DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_SKILL_CALL").ToLocal(), entry.GetIconName()));

                if (!entry.Released)
                {
                    DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_SKILL_UNFINISHED").ToLocal()));
                    return null;
                }

                BattleContext newContext = new BattleContext(BattleActionType.Skill);
                newContext.User = context.User;
                newContext.UsageSlot = BattleContext.FORCED_SLOT;

                newContext.StartDir = newContext.User.CharDir;

                //fill effects
                newContext.Data = new BattleData(entry.Data);

                newContext.Data.ID = moveID;
                newContext.Data.DataType = DataManager.DataType.Skill;
                newContext.Explosion = new ExplosionData(entry.Explosion);
                newContext.HitboxAction = entry.HitboxAction.Clone();
                newContext.Strikes = entry.Strikes;
                newContext.Item = new InvItem();
                //don't set move message, just directly give the message of what the move turned into

                return newContext;
            }
            else
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_SKILL_FAILED").ToLocal()));

            return null;
        }
    }

    /// <summary>
    /// Event that makes the user use the target's strongest base power move
    /// </summary>
    [Serializable]
    public class StrongestMoveEvent : InvokedMoveEvent
    {
        public override GameEvent Clone() { return new StrongestMoveEvent(); }

        protected override string GetInvokedMove(GameEventOwner owner, BattleContext context)
        {
            int recordSlot = -1;
            int recordPower = -1;
            for (int ii = 0; ii < context.Target.Skills.Count; ii++)
            {
                if (!String.IsNullOrEmpty(context.Target.Skills[ii].Element.SkillNum))
                {
                    SkillData entry = DataManager.Instance.GetSkill(context.Target.Skills[ii].Element.SkillNum);

                    int basePower = 0;
                    if (entry.Data.Category == BattleData.SkillCategory.Status)
                        basePower = -1;
                    else
                    {
                        BasePowerState state = entry.Data.SkillStates.GetWithDefault<BasePowerState>();
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
                return context.Target.Skills[recordSlot].Element.SkillNum;
            else
                return "";
        }
    }


    /// <summary>
    /// Event that makes the user randomly use any move.
    /// </summary>
    [Serializable]
    public class RandomMoveEvent : InvokedMoveEvent
    {
        public override GameEvent Clone() { return new RandomMoveEvent(); }

        protected override string GetInvokedMove(GameEventOwner owner, BattleContext context)
        {
            List<string> releasedMoves = new List<string>();
            foreach (string key in DataManager.Instance.DataIndices[DataManager.DataType.Skill].GetOrderedKeys(true))
            {
                if (key == DataManager.Instance.DefaultSkill)
                    continue;
                if (DataManager.Instance.DataIndices[DataManager.DataType.Skill].Get(key).Released)
                    releasedMoves.Add(key);
            }

            int randIndex = DataManager.Instance.Save.Rand.Next(releasedMoves.Count);
            return releasedMoves[randIndex];
        }
    }

    /// <summary>
    /// User will more likely use a random move that benefits the team
    /// </summary>
    [Serializable]
    public class NeededMoveEvent : InvokedMoveEvent
    {
        public override GameEvent Clone() { return new NeededMoveEvent(); }

        private void tryAddMove(List<string> moves, string move)
        {
            if (DataManager.Instance.DataIndices[DataManager.DataType.Skill].Get(move).Released)
                moves.Add(move);
        }


        private void tryAddTargetMove(Character user, List<Character> seenChars, List<string> moves, string move)
        {
            int effectiveness = 0;
            SkillData skill = DataManager.Instance.GetSkill(move);
            HashSet<Loc> targetLocs = new HashSet<Loc>();
            foreach (Loc loc in skill.HitboxAction.GetPreTargets(user, user.CharDir, 0))
                targetLocs.Add(ZoneManager.Instance.CurrentMap.WrapLoc(loc));
            foreach (Character seenChar in seenChars)
            {
                if (targetLocs.Contains(seenChar.CharLoc))
                    effectiveness += PreTypeEvent.GetDualEffectiveness(user, seenChar, skill.Data.Element) - PreTypeEvent.NRM_2;
            }

            if (effectiveness > 0)
                tryAddMove(moves, move);
        }

        protected override string GetInvokedMove(GameEventOwner owner, BattleContext context)
        {
            // scroll of need style move choice

            List<Character> seenAllies = context.User.GetSeenCharacters(Alignment.Friend);

            List<List<string>> tryingCategories = new List<List<string>>();
            //conditions:
            //are you wounded?
            bool needHeal = false;
            if (context.User.HP < context.User.MaxHP * 2 / 3)
            {
                List<string> tryingMoves = new List<string>();
                tryAddMove(tryingMoves, "recover");//recover
                tryAddMove(tryingMoves, "synthesis");//synthesis
                tryAddMove(tryingMoves, "roost");//roost
                tryAddMove(tryingMoves, "slack_off");//slack off
                tryingCategories.Add(tryingMoves);
                if (context.User.HP < context.User.MaxHP / 3)
                    needHeal = true;
            }

            //are your allies wounded? 2+ separate mons needed
            int woundedAllies = 0;
            foreach (Character ally in seenAllies)
            {
                if (ally.HP < ally.MaxHP * 2 / 3)
                {
                    woundedAllies++;
                    if (ally.HP < ally.MaxHP / 3)
                        needHeal = true;
                }
            }
            if (woundedAllies >= 2)
            {
                List<string> tryingMoves = new List<string>();
                tryAddMove(tryingMoves, "moonlight");//moonlight
                tryAddMove(tryingMoves, "morning_sun");//morning sun
                tryAddMove(tryingMoves, "milk_drink");//milk drink
                tryingCategories.Add(tryingMoves);
            }

            //how about for the target?
            //are any of yours or your targetable ally stats lowered? raise stat


            //status effects?  3+ needed in party
            int badStates = 0;
            foreach (StatusEffect status in context.User.IterateStatusEffects())
            {
                if (status.StatusStates.Contains<BadStatusState>())
                    badStates++;
            }
            foreach (Character ally in seenAllies)
            {
                foreach (StatusEffect status in ally.IterateStatusEffects())
                {
                    if (status.StatusStates.Contains<BadStatusState>())
                        badStates++;
                }
            }
            if (badStates > 2)
            {
                List<string> tryingMoves = new List<string>();
                tryAddMove(tryingMoves, "heal_bell");//heal bell
                tryAddMove(tryingMoves, "refresh");//refresh
                tryingCategories.Add(tryingMoves);
            }

            if (!needHeal)
            {
                List<string> tryingMoves = new List<string>();
                //enemy is weak to a type and can die from it?  use that type move, base it on your higher stat
                //multiple enemies weak to the same type?  use that type move, base it on your higher stat
                HashSet<string> availableWeaknesses = new HashSet<string>();
                List<Character> seenFoes = context.User.GetSeenCharacters(Alignment.Foe);
                foreach (Character chara in seenFoes)
                {
                    foreach (string key in DataManager.Instance.DataIndices[DataManager.DataType.Element].GetOrderedKeys(true))
                    {
                        if (PreTypeEvent.GetDualEffectiveness(context.User, chara, key) > PreTypeEvent.NRM_2)
                            availableWeaknesses.Add(key);
                    }
                }

                foreach (string ii in availableWeaknesses)
                {
                    switch (ii)
                    {
                        case "bug":
                            {
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "attack_order");//attack order
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "x_scissor");//x-scissor
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "megahorn");//megahorn
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "bug_buzz");//bug buzz
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "signal_beam");//signal beam
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "silver_wind");//silver wind
                            }
                            break;
                        case "dark":
                            {
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "hyperspace_fury");//hyperspace fury
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "night_daze");//night daze
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "assurance");//assurance
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "night_slash");//night slash
                            }
                            break;
                        case "dragon":
                            {
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "roar_of_time");//roar of time
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "draco_meteor");//draco meteor
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "spacial_rend");//spacial rend
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "outrage");//outrage
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "dragon_claw");//dragon claw
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "dragon_tail");//dragon tail
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "dragon_rush");//dragon rush
                            }
                            break;
                        case "electric":
                            {
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "discharge");//discharge
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "volt_tackle");//volt tackle
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "zap_cannon");//zap cannon
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "thunder");//thunder
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "bolt_strike");//bolt strike
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "fusion_bolt");//fusion bolt
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "parabolic_charge");//parabolic charge
                            }
                            break;
                        case "fairy":
                            {
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "light_of_ruin");//light of ruin
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "moonblast");//moonblast
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "play_rough");//play rough
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "dazzling_gleam");//dazzling gleam
                            }
                            break;
                        case "fighting":
                            {
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "high_jump_kick");//high jump kick
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "close_combat");//close combat
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "focus_blast");//focus blast
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "cross_chop");//cross chop
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "sacred_sword");//sacred sword
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "aura_sphere");//aura sphere
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "secret_sword");//secret sword
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "drain_punch");//drain punch
                            }
                            break;
                        case "fire":
                            {
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "v_create");//v-create
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "blast_burn");//blast burn
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "eruption");//eruption
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "sacred_fire");//sacred fire
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "flare_blitz");//flare blitz
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "blue_flare");//blue flare
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "fire_blast");//fire blast
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "magma_storm");//magma storm
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "inferno");//inferno
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "heat_wave");//heat wave
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "searing_shot");//searing shot
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "fiery_dance");//fiery dance
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "fusion_flare");//fusion flare
                            }
                            break;
                        case "flying":
                            {
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "brave_bird");//brave bird
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "dragon_ascent");//dragon ascent
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "hurricane");//hurricane
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "aeroblast");//aeroblast
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "oblivion_wing");//oblivion wing
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "sky_attack");//sky attack
                            }
                            break;
                        case "ghost":
                            {
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "shadow_force");//shadow force
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "shadow_ball");//shadow ball
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "ominous_wind");//ominous wind
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "hex");//hex
                            }
                            break;
                        case "grass":
                            {
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "leaf_storm");//leaf storm
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "frenzy_plant");//frenzy plant
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "power_whip");//power whip
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "wood_hammer");//wood hammer
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "energy_ball");//energy ball
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "petal_blizzard");//petal blizzard
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "seed_bomb");//seed bomb
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "solar_beam");//solar beam
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "giga_drain");//giga drain
                            }
                            break;
                        case "ground":
                            {
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "precipice_blades");//precipice blades
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "lands_wrath");//land's wrath
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "earth_power");//earth power
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "drill_run");//drill run
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "thousand_arrows");//thousand arrows
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "thousand_waves");//thousand waves
                            }
                            break;
                        case "ice":
                            {
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "blizzard");//blizzard
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "ice_beam");//ice beam
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "icicle_crash");//icicle crash
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "icicle_spear");//icicle spear
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "ice_burn");//ice burn
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "freeze_shock");//freeze shock
                            }
                            break;
                        case "normal":
                            {
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "hyper_voice");//hyper voice
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "giga_impact");//giga impact
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "double_edge");//double-edge
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "tri_attack");//tri-attack
                            }
                            break;
                        case "poison":
                            {
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "gunk_shot");//gunk shot
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "sludge_wave");//sludge wave
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "sludge_bomb");//sludge bomb
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "cross_poison");//cross poison
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "venoshock");//venoshock
                            }
                            break;
                        case "psychic":
                            {
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "psychic");//psychic
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "hyperspace_hole");//hyperspace hole
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "psycho_boost");//psycho boost
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "psystrike");//psystrike
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "zen_headbutt");//zen headbutt
                            }
                            break;
                        case "rock":
                            {
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "head_smash");//head smash
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "rock_wrecker");//rock wrecker
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "rock_blast");//rock blast
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "power_gem");//power gem
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "ancient_power");//ancient power
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "rollout");//rollout
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "diamond_storm");//diamond storm
                            }
                            break;
                        case "steel":
                            {
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "meteor_mash");//meteor mash
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "iron_tail");//iron tail
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "flash_cannon");//flash cannon
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "iron_head");//iron head
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "magnet_bomb");//magnet bomb
                            }
                            break;
                        case "water":
                            {
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "hydro_cannon");//hydro cannon
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "hydro_pump");//hydro pump
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "origin_pulse");//origin pulse
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "water_spout");//water spout
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "steam_eruption");//steam eruption
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "crabhammer");//crabhammer
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "aqua_tail");//aqua tail
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "waterfall");//waterfall
                            }
                            break;
                    }
                }
                if (tryingMoves.Count > 0)
                    tryingCategories.Add(tryingMoves);
            }

            //are you surrounded by enemies and cannot hit them all? crowd control: dark void, spore, stun spore

            //otherwise?  give a random buff attack
            if (tryingCategories.Count == 0)
            {
                List<string> tryingMoves = new List<string>();
                if (!context.User.StatusEffects.ContainsKey("aqua_ring"))
                    tryAddMove(tryingMoves, "aqua_ring");//aqua ring
                if (!context.User.StatusEffects.ContainsKey("reflect"))
                    tryAddMove(tryingMoves, "reflect");//reflect
                if (!context.User.StatusEffects.ContainsKey("light_screen"))
                    tryAddMove(tryingMoves, "light_screen");//light screen
                if (!context.User.StatusEffects.ContainsKey("wish"))
                    tryAddMove(tryingMoves, "wish");//wish
                if (!context.User.StatusEffects.ContainsKey("mist"))
                    tryAddMove(tryingMoves, "mist");//mist
                if (!context.User.StatusEffects.ContainsKey("safeguard"))
                    tryAddMove(tryingMoves, "safeguard");//safeguard
                if (!context.User.StatusEffects.ContainsKey("magic_coat"))
                    tryAddMove(tryingMoves, "magic_coat");//magic coat
                if (!context.User.StatusEffects.ContainsKey("mirror_coat"))
                    tryAddMove(tryingMoves, "mirror_coat");//mirror coat
                if (!context.User.StatusEffects.ContainsKey("counter"))
                    tryAddMove(tryingMoves, "counter");//counter
                if (!context.User.StatusEffects.ContainsKey("metal_burst"))
                    tryAddMove(tryingMoves, "metal_burst");//metal burst
                if (!context.User.StatusEffects.ContainsKey("lucky_chant"))
                    tryAddMove(tryingMoves, "lucky_chant");//lucky chant
                if (!context.User.StatusEffects.ContainsKey("focus_energy"))
                    tryAddMove(tryingMoves, "focus_energy");//focus energy
                if (!context.User.StatusEffects.ContainsKey("sure_shot"))
                    tryAddMove(tryingMoves, "lock_on");//lock-on
                tryingCategories.Add(tryingMoves);
            }


            //threat of status effects from enemies? safeguard
            //does the enemy have an ability that covers their weakness?  gastro acid

            //does your target have unusually high stat boosts?  clear stat boosts


            //do nearby targets have a high attack/special attack?  boost defense in that side
            //are you alone with summonable friends? beat up

            if (tryingCategories.Count > 0)
            {
                //75% chance of picking a good move
                if (DataManager.Instance.Save.Rand.Next(100) < 75)
                {
                    List<string> tryingMoves = tryingCategories[DataManager.Instance.Save.Rand.Next(tryingCategories.Count)];
                    return tryingMoves[DataManager.Instance.Save.Rand.Next(tryingMoves.Count)];
                }
            }

            List<string> releasedMoves = new List<string>();
            foreach (string key in DataManager.Instance.DataIndices[DataManager.DataType.Skill].GetOrderedKeys(true))
            {
                if (key == DataManager.Instance.DefaultSkill)
                    continue;
                if (DataManager.Instance.DataIndices[DataManager.DataType.Skill].Get(key).Released)
                    releasedMoves.Add(key);
            }
            int randIndex = DataManager.Instance.Save.Rand.Next(releasedMoves.Count);
            return releasedMoves[randIndex];
        }
    }

    /// <summary>
    /// Event that makes character will use a move that depends on the map status and dungeon type
    /// </summary>
    [Serializable]
    public class NatureMoveEvent : InvokedMoveEvent
    {
        /// <summary>
        /// The move used mapped to the current map status
        /// </summary>
        [JsonConverter(typeof(MapStatusSkillDictConverter))]
        [DataType(1, DataManager.DataType.MapStatus, false)]
        [DataType(2, DataManager.DataType.Skill, false)]
        public Dictionary<string, string> TerrainPair;

        /// <summary>
        /// The move used mapped to the current floor's nature environment
        /// </summary>
        [JsonConverter(typeof(ElementSkillDictConverter))]
        [DataType(1, DataManager.DataType.Element, false)]
        [DataType(2, DataManager.DataType.Skill, false)]
        public Dictionary<string, string> NaturePair;

        public NatureMoveEvent()
        {
            TerrainPair = new Dictionary<string, string>();
            NaturePair = new Dictionary<string, string>();
        }
        public NatureMoveEvent(Dictionary<string, string> terrain, Dictionary<string, string> moves)
        {
            TerrainPair = terrain;
            NaturePair = moves;
        }
        protected NatureMoveEvent(NatureMoveEvent other)
            : this()
        {
            foreach (string terrain in other.TerrainPair.Keys)
                TerrainPair.Add(terrain, other.TerrainPair[terrain]);
            foreach (string element in other.NaturePair.Keys)
                NaturePair.Add(element, other.NaturePair[element]);
        }
        public override GameEvent Clone() { return new NatureMoveEvent(this); }

        protected override string GetInvokedMove(GameEventOwner owner, BattleContext context)
        {
            foreach (string terrain in TerrainPair.Keys)
            {
                if (ZoneManager.Instance.CurrentMap.Status.ContainsKey(terrain))
                    return TerrainPair[terrain];
            }

            string moveNum;
            if (NaturePair.TryGetValue(ZoneManager.Instance.CurrentMap.Element, out moveNum))
                return moveNum;
            else
                return "";
        }
    }

    /// <summary>
    /// Event that makes the user use the last used move
    /// </summary>  
    [Serializable]
    public class MirrorMoveEvent : InvokedMoveEvent
    {
        /// <summary>
        /// A status containing the move in IDState that this event will use
        /// This status should either be Last Used Effect, Last Ally Effect, Last Effect Hit By Someone Else
        /// </summary>   
        [JsonConverter(typeof(StatusConverter))]
        [DataType(0, DataManager.DataType.Status, false)]
        public string MoveStatusID;

        public MirrorMoveEvent() { MoveStatusID = ""; }
        public MirrorMoveEvent(string prevMoveStatusID)
        {
            MoveStatusID = prevMoveStatusID;
        }
        protected MirrorMoveEvent(MirrorMoveEvent other)
        {
            MoveStatusID = other.MoveStatusID;
        }
        public override GameEvent Clone() { return new MirrorMoveEvent(this); }

        protected override string GetInvokedMove(GameEventOwner owner, BattleContext context)
        {
            StatusEffect status = context.Target.GetStatusEffect(MoveStatusID);
            if (status != null)
                return status.StatusStates.GetWithDefault<IDState>().ID;
            else
                return "";
        }
    }

    /// <summary>
    /// Event that is called as a turn-taking battle action
    /// </summary> 
    [Serializable]
    public class InvokeCustomBattleEvent : InvokeBattleEvent
    {
        /// <summary>
        /// Data on the hitbox of the attack. Controls range and targeting
        /// </summary>
        public CombatAction HitboxAction;

        /// <summary>
        /// Optional data to specify a splash effect on the tiles hit
        /// </summary>
        public ExplosionData Explosion;

        /// <summary>
        /// Events that occur with this skill.
        /// Before it's used, when it hits, after it's used, etc
        /// </summary>
        public BattleData NewData;

        /// <summary>
        /// The message displayed in the dungeon log 
        /// </summary>
        [StringKey(0, true)]
        public StringKey Msg;

        /// <summary>
        /// Whether to affect the target or user
        /// </summary>
        public bool AffectTarget;

        public InvokeCustomBattleEvent()
        {

        }

        public InvokeCustomBattleEvent(CombatAction action, ExplosionData explosion, BattleData moveData, StringKey msg, bool affectTarget = true)
        {
            HitboxAction = action;
            Explosion = explosion;
            NewData = moveData;
            Msg = msg;
            AffectTarget = affectTarget;
        }
        protected InvokeCustomBattleEvent(InvokeCustomBattleEvent other)
        {
            HitboxAction = other.HitboxAction;
            Explosion = other.Explosion;
            NewData = new BattleData(other.NewData);
            Msg = other.Msg;
            AffectTarget = other.AffectTarget;
        }
        public override GameEvent Clone() { return new InvokeCustomBattleEvent(this); }

        protected override BattleContext CreateContext(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            BattleContext newContext = new BattleContext(BattleActionType.Skill);
            newContext.User = (AffectTarget ? context.Target : context.User);
            newContext.UsageSlot = BattleContext.FORCED_SLOT;

            newContext.StartDir = newContext.User.CharDir;

            //change move effects
            newContext.Data = new BattleData(NewData);
            newContext.Data.ID = context.Data.ID;
            newContext.Data.DataType = context.Data.DataType;

            newContext.Explosion = new ExplosionData(Explosion);
            newContext.HitboxAction = HitboxAction.Clone();
            newContext.Strikes = 1;
            newContext.Item = new InvItem();

            if (Msg.IsValid())
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(Msg.ToLocal(), ownerChar.GetDisplayName(false), owner.GetDisplayName()));

            return newContext;
        }


        [OnDeserialized]
        internal void OnDeserializedMethod(StreamingContext context)
        {
            //TODO: remove on v1.1
            if (Serializer.OldVersion < new Version(0, 7, 10))
            {
                AffectTarget = true;
            }
        }
    }

}

