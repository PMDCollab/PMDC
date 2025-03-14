using System;
using RogueElements;
using RogueEssence.Dungeon;
using RogueEssence;
using RogueEssence.LevelGen;
using PMDC.Dungeon;
using System.Collections.Generic;
using RogueEssence.Dev;
using RogueEssence.Data;
using Newtonsoft.Json;

namespace PMDC.LevelGen
{
    /// <summary>
    /// One part of several steps used to create a boss room.
    /// This step takes an already-placed boss room, with an already-placed summoning tile and fills it with data on which tiles to lock down before summoning the boss.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    [Serializable]
    public class BossSealStep<T> : BaseSealStep<T> where T : ListMapGenContext
    {
        /// <summary>
        /// The tile used to seal the room.
        /// </summary>
        [JsonConverter(typeof(TileConverter))]
        [DataType(0, DataManager.DataType.Tile, false)]
        public string SealedTile;

        /// <summary>
        /// The tile used to summon the battle.
        /// </summary>
        [JsonConverter(typeof(TileConverter))]
        [DataType(0, DataManager.DataType.Tile, false)]
        public string BossTile;

        public BossSealStep()
        {
            BossFilters = new List<BaseRoomFilter>();
        }

        public BossSealStep(string sealedTile, string bossTile) : base()
        {
            SealedTile = sealedTile;
            BossTile = bossTile;
            BossFilters = new List<BaseRoomFilter>();
        }

        /// <summary>
        /// Singles out the boss room intended for this process.
        /// </summary>
        public List<BaseRoomFilter> BossFilters { get; set; }

        protected override void PlaceBorders(T map, Dictionary<Loc, SealType> sealList)
        {
            Rect? bossRect = null;

            for (int ii = 0; ii < map.RoomPlan.RoomCount; ii++)
            {
                FloorRoomPlan plan = map.RoomPlan.GetRoomPlan(ii);
                if (!BaseRoomFilter.PassesAllFilters(plan, this.BossFilters))
                    continue;
                bossRect = plan.RoomGen.Draw;
                break;
            }

            //if there's no way to open the door, there cannot be a door; give the player the treasure unguarded
            if (bossRect == null)
                return;

            EffectTile bossEffect = null;

            for (int xx = bossRect.Value.Start.X; xx < bossRect.Value.End.X; xx++)
            {
                for (int yy = bossRect.Value.Start.Y; yy < bossRect.Value.End.Y; yy++)
                {
                    Tile tile = (Tile)map.GetTile(new Loc(xx, yy));
                    if (tile.Effect.ID == BossTile)
                    {
                        bossEffect = tile.Effect;
                        break;
                    }
                }
                if (bossEffect != null)
                    break;
            }

            if (bossEffect == null)
                return;


            List<Loc> lockList = new List<Loc>();

            foreach (Loc loc in sealList.Keys)
            {
                switch (sealList[loc])
                {
                    case SealType.Blocked:
                        map.SetTile(loc, map.UnbreakableTerrain.Copy());
                        break;
                    default:
                        lockList.Add(loc);
                        break;
                }
            }

            foreach (Loc loc in lockList)
            {
                map.SetTile(loc, map.UnbreakableTerrain.Copy());
                EffectTile newEffect = new EffectTile(SealedTile, true, loc);
                ((IPlaceableGenContext<EffectTile>)map).PlaceItem(loc, newEffect);
            }

            ResultEventState resultEvent = new ResultEventState();
            resultEvent.ResultEvents.Add(new OpenVaultEvent(lockList));
            bossEffect.TileStates.Set(resultEvent);
        }

    }
}
