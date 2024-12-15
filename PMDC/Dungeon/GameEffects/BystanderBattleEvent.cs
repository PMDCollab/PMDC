using System;
using System.Collections.Generic;
using RogueEssence.Data;
using RogueElements;
using RogueEssence.Content;
using RogueEssence;
using RogueEssence.Dungeon;
using RogueEssence.Dev;
using Newtonsoft.Json;

namespace PMDC.Dungeon
{
    [Serializable]
    public class SupportAbilityEvent : BattleEvent
    {
        [JsonConverter(typeof(IntrinsicConverter))]
        [DataType(0, DataManager.DataType.Intrinsic, false)]
        public string SupportAbility;

        public SupportAbilityEvent() { SupportAbility = ""; }
        public SupportAbilityEvent(string supportAbility)
        {
            SupportAbility = supportAbility;
        }
        protected SupportAbilityEvent(SupportAbilityEvent other)
        {
            SupportAbility = other.SupportAbility;
        }
        public override GameEvent Clone() { return new SupportAbilityEvent(this); }
        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.Data.Category == BattleData.SkillCategory.Magical
                && context.User.HasIntrinsic(SupportAbility))
                context.AddContextStateMult<DmgMult>(false, 4, 3);
            yield break;
        }
    }

    [Serializable]
    public class SnatchEvent : BattleEvent
    {
        public FiniteEmitter Emitter;
        [Sound(0)]
        public string Sound;

        public SnatchEvent() { Emitter = new EmptyFiniteEmitter(); }
        public SnatchEvent(FiniteEmitter emitter, string sound)
            : this()
        {
            Emitter = emitter;
            Sound = sound;
        }
        protected SnatchEvent(SnatchEvent other)
            : this()
        {
            Emitter = (FiniteEmitter)other.Emitter.Clone();
            Sound = other.Sound;
        }
        public override GameEvent Clone() { return new SnatchEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.ContextStates.Contains<Redirected>())
                yield break;

            if (context.ActionType == BattleActionType.Trap || context.ActionType == BattleActionType.Item)
                yield break;

            //must be a status move
            if (context.Data.Category != BattleData.SkillCategory.Status)
                yield break;

            //attacker must be target
            if (context.User != context.Target)
                yield break;


            GameManager.Instance.BattleSE(Sound);
            if (!ownerChar.Unidentifiable)
            {
                FiniteEmitter endEmitter = (FiniteEmitter)Emitter.Clone();
                endEmitter.SetupEmit(ownerChar.MapLoc, ownerChar.MapLoc, ownerChar.CharDir);
                DungeonScene.Instance.CreateAnim(endEmitter, DrawLayer.NoDraw);
            }

            CharAnimAction SpinAnim = new CharAnimAction(ownerChar.CharLoc, ZoneManager.Instance.CurrentMap.ApproximateClosestDir8(ownerChar.CharLoc, context.Target.CharLoc), 05);//Attack
            SpinAnim.MajorAnim = true;

            yield return CoroutineManager.Instance.StartCoroutine(ownerChar.StartAnim(SpinAnim));
            yield return new WaitWhile(ownerChar.OccupiedwithAction);

            DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_SNATCH").ToLocal(), ownerChar.GetDisplayName(false)));
            context.Target = ownerChar;
            context.ContextStates.Set(new Redirected());
        }
    }


    //below, the effects deal exclusively with explosions

    [Serializable]
    public class AllyDifferentExplosionEvent : BattleEvent
    {
        //also need to somehow specify alternative animations/sounds
        public List<BattleEvent> BaseEvents;

        public AllyDifferentExplosionEvent() { BaseEvents = new List<BattleEvent>(); }
        protected AllyDifferentExplosionEvent(AllyDifferentExplosionEvent other)
            : this()
        {
            foreach (BattleEvent battleEffect in other.BaseEvents)
                BaseEvents.Add((BattleEvent)battleEffect.Clone());
        }
        public override GameEvent Clone() { return new AllyDifferentExplosionEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            Character targetChar = ZoneManager.Instance.CurrentMap.GetCharAtLoc(context.ExplosionTile);
            if (targetChar == null)
                yield break;

            if (DungeonScene.Instance.GetMatchup(context.User, targetChar) == Alignment.Friend)
            {
                //remove all MoveHit effects (except for the post-effect)
                context.Data.OnHits.Clear();
                context.Data.OnHitTiles.Clear();
                //remove BasePower component
                if (context.Data.SkillStates.Contains<BasePowerState>())
                    context.Data.SkillStates.Remove<BasePowerState>();

                //add the alternative effects
                foreach (BattleEvent battleEffect in BaseEvents)
                    context.Data.OnHits.Add(0, (BattleEvent)battleEffect.Clone());
            }
        }
    }

    [Serializable]
    public class DampEvent : BattleEvent
    {
        public int Div;
        StringKey Msg;

        public DampEvent() { }
        public DampEvent(int div, StringKey msg)
        {
            Div = div;
            Msg = msg;
        }
        protected DampEvent(DampEvent other)
        {
            Div = other.Div;
            Msg = other.Msg;
        }
        public override GameEvent Clone() { return new DampEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            //only block explosions
            if (context.Explosion.Range == 0)
                yield break;

            //make sure to exempt Round.

            DungeonScene.Instance.LogMsg(Text.FormatGrammar(Msg.ToLocal(), ownerChar.GetDisplayName(false)));
            context.Explosion.Range = 0;
            context.Explosion.ExplodeFX = new BattleFX();
            context.Explosion.Emitter = new EmptyCircleSquareEmitter();
            context.Explosion.TileEmitter = new EmptyFiniteEmitter();
            if (Div > 0)
                context.AddContextStateMult<DmgMult>(false,1, Div);
            else
                context.AddContextStateMult<DmgMult>(false,Div, 1);
        }
    }

    [Serializable]
    public class DampItemEvent : BattleEvent
    {
        public override GameEvent Clone() { return new DampItemEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.ActionType == BattleActionType.Throw)
            {
                ItemData entry = DataManager.Instance.GetItem(context.Item.ID);
                if (!entry.ItemStates.Contains<RecruitState>())
                {
                    context.Explosion.Range = 0;
                    context.Explosion.ExplodeFX = new BattleFX();
                    context.Explosion.Emitter = new EmptyCircleSquareEmitter();
                    context.Explosion.TileEmitter = new EmptyFiniteEmitter();
                }
            }
            yield break;
        }
    }


    [Serializable]
    public class CatchItemSplashEvent : BattleEvent
    {
        public override GameEvent Clone() { return new CatchItemSplashEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.ActionType == BattleActionType.Throw)
            {
                //can't catch pierce
                if (context.HitboxAction is LinearAction && !((LinearAction)context.HitboxAction).StopAtHit)
                    yield break;

                Character targetChar = ZoneManager.Instance.CurrentMap.GetCharAtLoc(context.ExplosionTile);
                if (targetChar != null)
                {

                    //can't catch when holding
                    if (!String.IsNullOrEmpty(targetChar.EquippedItem.ID))
                        yield break;

                    ItemData entry = DataManager.Instance.GetItem(context.Item.ID);

                    //can't catch recruit item under any circumstances
                    if (entry.ItemStates.Contains<RecruitState>())
                        yield break;


                    if (targetChar.MemberTeam is MonsterTeam)
                    {
                        //can't catch if it's a wild team, and it's an edible or ammo
                        if (entry.ItemStates.Contains<EdibleState>() || entry.ItemStates.Contains<AmmoState>())
                            yield break;
                    }

                    // throwing edibles at an ally always results in no-catch (eaten)
                    if (DungeonScene.Instance.GetMatchup(context.User, targetChar) == Alignment.Friend && entry.ItemStates.Contains<EdibleState>())
                        yield break;

                    context.Explosion.Range = 0;
                    context.Explosion.ExplodeFX = new BattleFX();
                    context.Explosion.Emitter = new EmptyCircleSquareEmitter();
                    context.Explosion.TileEmitter = new EmptyFiniteEmitter();



                    BattleData catchData = new BattleData();
                    catchData.Element = DataManager.Instance.DefaultElement;
                    catchData.OnHits.Add(0, new CatchItemEvent());
                    catchData.HitFX.Sound = "DUN_Equip";
                    context.Data.BeforeHits.Add(-5, new CatchableEvent(catchData));
                }
            }
        }
    }

    [Serializable]
    public class IsolateElementEvent : BattleEvent
    {
        [JsonConverter(typeof(ElementConverter))]
        [DataType(0, DataManager.DataType.Element, false)]
        public string Element;

        public IsolateElementEvent() { Element = ""; }
        public IsolateElementEvent(string element)
        {
            Element = element;
        }
        protected IsolateElementEvent(IsolateElementEvent other)
        {
            Element = other.Element;
        }
        public override GameEvent Clone() { return new IsolateElementEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (Element != DataManager.Instance.DefaultElement && context.Data.Element != Element)
                yield break;

            if (ZoneManager.Instance.CurrentMap.GetCharAtLoc(context.ExplosionTile) != ownerChar)
                yield break;

            context.Explosion.Range = 0;
        }
    }

    [Serializable]
    public class DrawAttackEvent : BattleEvent
    {
        [JsonConverter(typeof(ElementConverter))]
        [DataType(0, DataManager.DataType.Element, false)]
        public string Element;
        public Alignment DrawFrom;
        public StringKey Msg;

        public DrawAttackEvent() { Element = ""; }
        public DrawAttackEvent(Alignment drawFrom, string element, StringKey msg)
        {
            DrawFrom = drawFrom;
            Element = element;
            Msg = msg;
        }
        protected DrawAttackEvent(DrawAttackEvent other)
        {
            DrawFrom = other.DrawFrom;
            Element = other.Element;
            Msg = other.Msg;
        }
        public override GameEvent Clone() { return new DrawAttackEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.ContextStates.Contains<Redirected>())
                yield break;

            if (context.ActionType == BattleActionType.Trap || context.ActionType == BattleActionType.Item)
                yield break;

            if (Element != DataManager.Instance.DefaultElement && context.Data.Element != Element)
                yield break;

            Character targetChar = ZoneManager.Instance.CurrentMap.GetCharAtLoc(context.ExplosionTile);
            if (targetChar == null)
                yield break;

            //the attack needs to be able to hit foes
            if ((context.HitboxAction.TargetAlignments & Alignment.Foe) == Alignment.None)
                yield break;

            //original target char needs to be a friend of the target char
            if ((DungeonScene.Instance.GetMatchup(ownerChar, targetChar) & DrawFrom) == Alignment.None)
                yield break;

            CharAnimSpin spinAnim = new CharAnimSpin();
            spinAnim.CharLoc = ownerChar.CharLoc;
            spinAnim.CharDir = ownerChar.CharDir;
            spinAnim.MajorAnim = true;

            yield return CoroutineManager.Instance.StartCoroutine(ownerChar.StartAnim(spinAnim));
            yield return new WaitWhile(ownerChar.OccupiedwithAction);

            DungeonScene.Instance.LogMsg(Text.FormatGrammar(Msg.ToLocal(), ownerChar.GetDisplayName(false), owner.GetDisplayName()));
            context.ExplosionTile = ownerChar.CharLoc;
            context.Explosion.Range = 0;
            context.ContextStates.Set(new Redirected());
        }
    }

    [Serializable]
    public class PassAttackEvent : BattleEvent
    {
        public int BellyCost;
        public PassAttackEvent() { }
        public PassAttackEvent(int bellyCost)
        {
            BellyCost = bellyCost;
        }
        protected PassAttackEvent(PassAttackEvent other)
        {
            BellyCost = other.BellyCost;
        }
        public override GameEvent Clone() { return new PassAttackEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.ContextStates.Contains<Redirected>())
                yield break;

            if (context.ActionType == BattleActionType.Trap || context.ActionType == BattleActionType.Item)
                yield break;

            //needs to be an attacking move
            if (context.Data.Category != BattleData.SkillCategory.Physical && context.Data.Category != BattleData.SkillCategory.Magical)
                yield break;

            if (ZoneManager.Instance.CurrentMap.GetCharAtLoc(context.ExplosionTile) != ownerChar)
                yield break;

            if (ownerChar.Fullness < BellyCost)
                yield break;
            
            foreach (Character newTarget in ZoneManager.Instance.CurrentMap.GetCharsInFillRect(ownerChar.CharLoc, Rect.FromPointRadius(ownerChar.CharLoc, 1)))
            {
                if (!newTarget.Dead && newTarget != ownerChar && newTarget != context.User)
                {
                    ownerChar.Fullness -= BellyCost;
                    if (ownerChar.Fullness < 0)
                        ownerChar.Fullness = 0;

                    CharAnimSpin spinAnim = new CharAnimSpin();
                    spinAnim.CharLoc = ownerChar.CharLoc;
                    spinAnim.CharDir = ownerChar.CharDir;
                    spinAnim.MajorAnim = true;

                    yield return CoroutineManager.Instance.StartCoroutine(ownerChar.StartAnim(spinAnim));
                    yield return new WaitWhile(ownerChar.OccupiedwithAction);

                    DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_PASS_ATTACK").ToLocal(), ownerChar.GetDisplayName(false), newTarget.GetDisplayName(false)));
                    context.ExplosionTile = newTarget.CharLoc;
                    context.Explosion.TargetAlignments |= Alignment.Foe;
                    context.Explosion.TargetAlignments |= Alignment.Friend;
                    context.ContextStates.Set(new Redirected());
                    yield break;
                }
            }
            
        }
    }

    [Serializable]
    public class CoverAttackEvent : BattleEvent
    {
        public CoverAttackEvent() { }
        public override GameEvent Clone() { return new CoverAttackEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.ContextStates.Contains<Redirected>())
                yield break;

            if (context.ActionType == BattleActionType.Trap || context.ActionType == BattleActionType.Item)
                yield break;

            Character targetChar = ZoneManager.Instance.CurrentMap.GetCharAtLoc(context.ExplosionTile);
            if (targetChar == null)
                yield break;

            if (ownerChar.HP < ownerChar.MaxHP / 2)
                yield break;

            //char needs to be a friend of the target char
            if (DungeonScene.Instance.GetMatchup(ownerChar, targetChar) != Alignment.Friend)
                yield break;

            CharAnimSpin spinAnim = new CharAnimSpin();
            spinAnim.CharLoc = ownerChar.CharLoc;
            spinAnim.CharDir = ownerChar.CharDir;
            spinAnim.MajorAnim = true;

            yield return CoroutineManager.Instance.StartCoroutine(ownerChar.StartAnim(spinAnim));
            yield return new WaitWhile(ownerChar.OccupiedwithAction);

            DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_COVER_ATTACK").ToLocal(), ownerChar.GetDisplayName(false)));
            context.ExplosionTile = ownerChar.CharLoc;
            context.ContextStates.Set(new Redirected());        
        }
    }


    [Serializable]
    public class FetchEvent : BattleEvent
    {
        public StringKey Msg;

        public FetchEvent() { }
        public FetchEvent(StringKey msg)
        {
            Msg = msg;
        }
        protected FetchEvent(FetchEvent other)
        {
            Msg = other.Msg;
        }
        public override GameEvent Clone() { return new FetchEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.ContextStates.Contains<BallFetch>())
                yield break;

            RecruitFail state = context.ContextStates.GetWithDefault<RecruitFail>();
            if (state == null || state.ResultLoc == null)
                yield break;

            //the item needs to be there
            int itemSlot = ZoneManager.Instance.CurrentMap.GetItem(state.ResultLoc.Value);

            //the item needs to match
            if (itemSlot == -1)
                yield break;

            MapItem mapItem = ZoneManager.Instance.CurrentMap.Items[itemSlot];

            //make sure it's the right one
            if (mapItem.Value != context.Item.ID)
                yield break;


            //fetch the ball!
            InvItem item = context.Item;
            Character origin = ownerChar;


            yield return new WaitForFrames(30);

            ZoneManager.Instance.CurrentMap.Items.RemoveAt(itemSlot);

            //item steal animation
            Loc itemStartLoc = mapItem.TileLoc * GraphicsManager.TileSize + new Loc(GraphicsManager.TileSize / 2);
            int MaxDistance = (int)Math.Sqrt((itemStartLoc - origin.MapLoc).DistSquared());
            ItemAnim itemAnim = new ItemAnim(itemStartLoc, origin.MapLoc, DataManager.Instance.GetItem(item.ID).Sprite, MaxDistance / 2, 0);
            DungeonScene.Instance.CreateAnim(itemAnim, DrawLayer.Normal);
            yield return new WaitForFrames(ItemAnim.ITEM_ACTION_TIME);

            GameManager.Instance.SE(GraphicsManager.EquipSE);
            DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_BALL_FETCH").ToLocal(), origin.GetDisplayName(false), item.GetDisplayName()));

            if (origin.MemberTeam is ExplorerTeam)
            {
                if (((ExplorerTeam)origin.MemberTeam).GetInvCount() < ((ExplorerTeam)origin.MemberTeam).GetMaxInvSlots(ZoneManager.Instance.CurrentZone))
                {
                    //attackers already holding an item will have the item returned to the bag
                    if (!String.IsNullOrEmpty(origin.EquippedItem.ID))
                    {
                        InvItem attackerItem = origin.EquippedItem;
                        yield return CoroutineManager.Instance.StartCoroutine(origin.DequipItem());
                        origin.MemberTeam.AddToInv(attackerItem);
                    }
                    yield return CoroutineManager.Instance.StartCoroutine(origin.EquipItem(item));
                }
                else
                {
                    yield return new WaitForFrames(GameManager.Instance.ModifyBattleSpeed(30));
                    DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_INV_FULL").ToLocal(), origin.GetDisplayName(false), item.GetDisplayName()));
                    //if the bag is full, or there is no bag, the stolen item will slide off in the opposite direction they're facing
                    Loc endLoc = origin.CharLoc + origin.CharDir.Reverse().GetLoc();
                    yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.Instance.DropItem(item, endLoc, origin.CharLoc));
                }
            }
            else
            {
                if (!String.IsNullOrEmpty(origin.EquippedItem.ID))
                {
                    InvItem attackerItem = origin.EquippedItem;
                    yield return CoroutineManager.Instance.StartCoroutine(origin.DequipItem());
                    //if the user is holding an item already, the item will slide off in the opposite direction they're facing
                    Loc endLoc = origin.CharLoc + origin.CharDir.Reverse().GetLoc();
                    yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.Instance.DropItem(attackerItem, endLoc, origin.CharLoc));
                }
                yield return CoroutineManager.Instance.StartCoroutine(origin.EquipItem(item));
            }
        }
    }


    [Serializable]
    public class FollowUpEvent : InvokeBattleEvent
    {
        [JsonConverter(typeof(SkillConverter))]
        [DataType(0, DataManager.DataType.Skill, false)]
        public string InvokedMove;
        public bool AffectTarget;
        public int FrontOffset;
        public StringKey Msg;

        public FollowUpEvent() { InvokedMove = ""; }
        public FollowUpEvent(string invokedMove, bool affectTarget, int frontOffset, StringKey msg)
        {
            InvokedMove = invokedMove;
            AffectTarget = affectTarget;
            FrontOffset = frontOffset;
            Msg = msg;
        }
        protected FollowUpEvent(FollowUpEvent other)
        {
            InvokedMove = other.InvokedMove;
            AffectTarget = other.AffectTarget;
            FrontOffset = other.FrontOffset;
            Msg = other.Msg;
        }
        public override GameEvent Clone() { return new FollowUpEvent(this); }
        
        protected override BattleContext CreateContext(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            Character target = (AffectTarget ? context.Target : context.User);
            int damage = context.GetContextStateInt<DamageDealt>(0);
            if (damage > 0 && ownerChar != context.User &&
                DungeonScene.Instance.GetMatchup(context.User, context.Target) == Alignment.Foe &&
                !context.ContextStates.Contains<FollowUp>())
            {
                //the attack needs to face the foe, and *auto-target*
                Dir8 attackDir = ZoneManager.Instance.CurrentMap.GetClosestDir8(ownerChar.CharLoc, target.CharLoc);
                if (attackDir == Dir8.None)
                    attackDir = Dir8.Down;
                ownerChar.CharDir = attackDir;
                Loc frontLoc = ownerChar.CharLoc + attackDir.GetLoc() * FrontOffset;

                SkillData entry = DataManager.Instance.GetSkill(InvokedMove);

                DungeonScene.Instance.LogMsg(Text.FormatGrammar(Msg.ToLocal(), ownerChar.GetDisplayName(false), context.User.GetDisplayName(false)));

                BattleContext newContext = new BattleContext(BattleActionType.Skill);
                newContext.User = ownerChar;
                newContext.UsageSlot = BattleContext.FORCED_SLOT;

                newContext.StartDir = newContext.User.CharDir;

                //fill effects
                newContext.Data = new BattleData(entry.Data);
                newContext.Data.ID = InvokedMove;
                newContext.Data.DataType = DataManager.DataType.Skill;
                newContext.Explosion = new ExplosionData(entry.Explosion);
                newContext.HitboxAction = entry.HitboxAction.Clone();
                //make the attack *autotarget*; set the offset to the space between the front loc and the target
                newContext.HitboxAction.HitOffset = target.CharLoc - frontLoc;
                newContext.Strikes = entry.Strikes;
                newContext.Item = new InvItem();
                //don't set move message, just directly give the message of what the move turned into

                //add a tag that will allow the moves themselves to switch to their offensive versions
                newContext.ContextStates.Set(new FollowUp());


                return newContext;
            }

            return null;
        }
    }

}

