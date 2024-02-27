using System;
using System.Collections.Generic;
using RogueElements;
using RogueEssence.Data;
using RogueEssence.Content;
using RogueEssence;
using RogueEssence.Dungeon;
using PMDC.Dev;
using RogueEssence.Dev;
using RogueEssence.LevelGen;
using Newtonsoft.Json;

namespace PMDC.Dungeon
{
    [Serializable]
    public class AutoCurseItemEvent : ItemGivenEvent
    {
        public override GameEvent Clone() { return new AutoCurseItemEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, ItemCheckContext context)
        {
            if (!context.User.EquippedItem.Cursed)
            {
                GameManager.Instance.SE(GraphicsManager.CursedSE);
                if (!context.User.CanRemoveStuck)
                    DungeonScene.Instance.LogMsg(Text.FormatKey("MSG_EQUIP_AUTOCURSE", context.User.EquippedItem.GetDisplayName(), context.User.GetDisplayName(false)));
                else
                    DungeonScene.Instance.LogMsg(Text.FormatKey("MSG_EQUIP_AUTOCURSE_AVOID", context.User.EquippedItem.GetDisplayName(), context.User.GetDisplayName(false)));
                context.User.EquippedItem.Cursed = true;
            }
            context.User.RefreshTraits();
            yield break;
        }
    }

    [Serializable]
    public class CurseWarningEvent : ItemGivenEvent
    {
        public override GameEvent Clone() { return new CurseWarningEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, ItemCheckContext context)
        {
            if (context.User.EquippedItem.Cursed && !context.User.CanRemoveStuck)
            {
                GameManager.Instance.SE(GraphicsManager.CursedSE);
                DungeonScene.Instance.LogMsg(Text.FormatKey("MSG_EQUIP_CURSED", context.User.EquippedItem.GetDisplayName(), context.User.GetDisplayName(false)));
            }
            yield break;
        }
    }

    [Serializable]
    public struct ItemFake
    {
        [DataType(0, DataManager.DataType.Item, false)]
        public string Item;
        [DataType(0, DataManager.DataType.Monster, false)]
        public string Species;

        public ItemFake(string item, string species)
        {
            Item = item;
            Species = species;
        }

        public override bool Equals(object obj)
        {
            return (obj is ItemFake) && Equals((ItemFake)obj);
        }

        public bool Equals(ItemFake other)
        {
            if (Species != other.Species)
                return false;
            if (Item != other.Item)
                return false;

            return true;
        }

        public override int GetHashCode()
        {
            return String.GetHashCode(Species) ^ String.GetHashCode(Item);
        }

        public override string ToString()
        {
            return String.Format("{0}:{1}", Item, Species);
        }
    }

    [Serializable]
    public class FakeItemEvent : ItemGivenEvent
    {
        [JsonConverter(typeof(ItemFakeTableConverter))]
        public Dictionary<ItemFake, MobSpawn> SpawnTable;

        public FakeItemEvent()
        {
            SpawnTable = new Dictionary<ItemFake, MobSpawn>();
        }

        public FakeItemEvent(Dictionary<ItemFake, MobSpawn> spawnTable)
        {
            this.SpawnTable = spawnTable;
        }

        public FakeItemEvent(FakeItemEvent other)
        {
            this.SpawnTable = new Dictionary<ItemFake, MobSpawn>();
            foreach (ItemFake fake in other.SpawnTable.Keys)
                this.SpawnTable.Add(fake, other.SpawnTable[fake].Copy());
        }

        public override GameEvent Clone() { return new FakeItemEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, ItemCheckContext context)
        {
            ItemFake fake = new ItemFake(context.Item.Value, context.Item.HiddenValue);
            MobSpawn spawn;
            if (SpawnTable.TryGetValue(fake, out spawn))
            {
                deleteFakeItem(context.User, fake);

                if (context.User.MemberTeam == DungeonScene.Instance.ActiveTeam)
                {
                    yield return CoroutineManager.Instance.StartCoroutine(SpawnFake(context.User, context.Item.MakeInvItem(), spawn));
                }
                else
                {
                    //enemies might pick up the item, just silently put it back down.

                    //spawn the item directly below
                    DungeonScene.Instance.DropMapItem(new MapItem(context.Item), context.User.CharLoc, context.User.CharLoc, true);
                }

                //cancel the pickup
                context.CancelState.Cancel = true;
            }
            yield break;
        }

        public static IEnumerator<YieldInstruction> SpawnFake(Character chara, InvItem item, MobSpawn spawn)
        {
            //pause
            yield return new WaitForFrames(GameManager.Instance.ModifyBattleSpeed(30));

            //gasp!
            EmoteData emoteData = DataManager.Instance.GetEmote("shock");
            chara.StartEmote(new Emote(emoteData.Anim, emoteData.LocHeight, 1));
            GameManager.Instance.BattleSE("EVT_Emote_Shock_2");

            //spawn the enemy
            MonsterTeam team = new MonsterTeam();
            Character mob = spawn.Spawn(team, ZoneManager.Instance.CurrentMap);
            Loc? dest = ZoneManager.Instance.CurrentMap.GetClosestTileForChar(mob, chara.CharLoc + chara.CharDir.GetLoc());
            Loc endLoc;
            if (dest.HasValue)
                endLoc = dest.Value;
            else
                endLoc = chara.CharLoc;

            DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_FAKE_ITEM").ToLocal(), item.GetDisplayName(), mob.GetDisplayName(false)));

            ZoneManager.Instance.CurrentMap.MapTeams.Add(team);
            mob.RefreshTraits();
            CharAnimJump jumpTo = new CharAnimJump();
            jumpTo.FromLoc = chara.CharLoc;
            jumpTo.CharDir = mob.CharDir;
            jumpTo.ToLoc = endLoc;
            jumpTo.MajorAnim = true;
            Dir8 dir = ZoneManager.Instance.CurrentMap.ApproximateClosestDir8(endLoc, chara.CharLoc);
            if (dir > Dir8.None)
                jumpTo.CharDir = dir;

            yield return CoroutineManager.Instance.StartCoroutine(mob.StartAnim(jumpTo));
            mob.Tactic.Initialize(mob);

            yield return CoroutineManager.Instance.StartCoroutine(mob.OnMapStart());
            ZoneManager.Instance.CurrentMap.UpdateExploration(mob);
        }

        private static void deleteFakeItem(Character chara, ItemFake fake)
        {
            //delete the item from held items and inventory (just check all slots for the an item that matches and delete it)
            //later maybe make a more watertight way to check??
            if (chara.EquippedItem.ID == fake.Item && chara.EquippedItem.HiddenValue == fake.Item)
            {
                chara.SilentDequipItem();
                return;
            }

            for (int ii = 0; ii < chara.MemberTeam.GetInvCount(); ii++)
            {
                InvItem item = chara.MemberTeam.GetInv(ii);
                if (item.ID == fake.Item && item.HiddenValue == fake.Species)
                {
                    chara.MemberTeam.RemoveFromInv(ii);
                    return;
                }
            }
        }
    }

    [Serializable]
    public class CheckEquipPassValidityEvent : ItemGivenEvent
    {
        public override GameEvent Clone() { return new CheckEquipPassValidityEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, ItemCheckContext context)
        {
            if (!String.IsNullOrEmpty(context.User.EquippedItem.ID))
            {
                ItemData entry = (ItemData)context.User.EquippedItem.GetData();

                if (CanItemEffectBePassed(entry))
                    DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_EQUIP_SHARE").ToLocal(), context.User.EquippedItem.GetDisplayName(), context.User.GetDisplayName(false)));
            }
            yield break;
        }

        public static bool CanItemEffectBePassed(ItemData entry)
        {
            //no refresh events allowed
            if (entry.OnRefresh.Count > 0)
                return false;

            //no proximity events allowed
            if (entry.ProximityEvent.Radius > -1)
                return false;

            //for every other event list, the priority must be 0
            //foreach (var effect in entry.OnEquips)
            //    if (effect.Key != Priority.Zero)
            //        return false;
            //foreach (var effect in entry.OnPickups)
            //    if (effect.Key != Priority.Zero)
            //    return false;

            foreach (var effect in entry.BeforeStatusAdds)
                if (effect.Key != Priority.Zero)
                    return false;
            foreach (var effect in entry.BeforeStatusAddings)
                if (effect.Key != Priority.Zero)
                    return false;
            foreach (var effect in entry.OnStatusAdds)
                if (effect.Key != Priority.Zero)
                    return false;
            foreach (var effect in entry.OnStatusRemoves)
                if (effect.Key != Priority.Zero)
                    return false;
            foreach (var effect in entry.OnMapStatusAdds)
                if (effect.Key != Priority.Zero)
                    return false;
            foreach (var effect in entry.OnMapStatusRemoves)
                if (effect.Key != Priority.Zero)
                    return false;

            foreach (var effect in entry.OnMapStarts)
                if (effect.Key != Priority.Zero)
                    return false;
            foreach (var effect in entry.OnTurnStarts)
                if (effect.Key != Priority.Zero)
                    return false;
            foreach (var effect in entry.OnTurnEnds)
                if (effect.Key != Priority.Zero)
                    return false;
            foreach (var effect in entry.OnMapTurnEnds)
                if (effect.Key != Priority.Zero)
                    return false;
            foreach (var effect in entry.OnWalks)
                if (effect.Key != Priority.Zero)
                    return false;
            foreach (var effect in entry.OnDeaths)
                if (effect.Key != Priority.Zero)
                    return false;

            foreach (var effect in entry.BeforeTryActions)
                if (effect.Key != Priority.Zero)
                    return false;
            foreach (var effect in entry.BeforeActions)
                if (effect.Key != Priority.Zero)
                    return false;
            foreach (var effect in entry.OnActions)
                if (effect.Key != Priority.Zero)
                    return false;
            foreach (var effect in entry.BeforeHittings)
                if (effect.Key != Priority.Zero)
                    return false;
            foreach (var effect in entry.BeforeBeingHits)
                if (effect.Key != Priority.Zero)
                    return false;
            foreach (var effect in entry.AfterHittings)
                if (effect.Key != Priority.Zero)
                    return false;
            foreach (var effect in entry.AfterBeingHits)
                if (effect.Key != Priority.Zero)
                    return false;
            foreach (var effect in entry.OnHitTiles)
                if (effect.Key != Priority.Zero)
                    return false;
            foreach (var effect in entry.AfterActions)
                if (effect.Key != Priority.Zero)
                    return false;

            foreach (var effect in entry.UserElementEffects)
                if (effect.Key != Priority.Zero)
                    return false;
            foreach (var effect in entry.TargetElementEffects)
                if (effect.Key != Priority.Zero)
                    return false;
            foreach (var effect in entry.ModifyHPs)
                if (effect.Key != Priority.Zero)
                    return false;
            foreach (var effect in entry.RestoreHPs)
                if (effect.Key != Priority.Zero)
                    return false;

            return true;
        }
    }
}
