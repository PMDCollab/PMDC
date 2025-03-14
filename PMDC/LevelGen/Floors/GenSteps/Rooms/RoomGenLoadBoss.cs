using System;
using System.Collections.Generic;
using RogueElements;
using RogueEssence.Dungeon;
using RogueEssence.Dev;
using RogueEssence.LevelGen;
using RogueEssence.Data;
using RogueEssence.Content;
using RogueEssence.Ground;
using PMDC.Dungeon;
using Newtonsoft.Json;


namespace PMDC.LevelGen
{

    /// <summary>
    /// Generates a room by loading a map as the room.
    /// Includes tiles, items, enemies, and mapstarts.
    /// Borders are specified by a tile.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    [Serializable]
    public class RoomGenLoadBoss<T> : RoomGenLoadMapBase<T> where T : BaseMapGenContext
    {
        /// <summary>
        /// The terrain that counts as room.  Halls will only attach to room tiles, or tiles specified with Borders.
        /// </summary>
        public ITile RoomTerrain { get; set; }

        /// <summary>
        /// The ID of the tile used to trigger the boss battle.
        /// </summary>
        [JsonConverter(typeof(TileConverter))]
        [DataType(0, DataManager.DataType.Tile, false)]
        public string TriggerTile;

        /// <summary>
        /// NOTE: this is a temporary feature that will be removed when boss rooms can take in mob spawns!
        /// </summary>
        public List<MobSpawnExtra> SpawnDetails;

        public RoomGenLoadBoss()
        {
            TriggerTile = "";
            SpawnDetails = new List<MobSpawnExtra>();
        }

        public RoomGenLoadBoss(string mapID, ITile roomTerrain, string triggerTile)
        {
            MapID = mapID;
            this.RoomTerrain = roomTerrain;
            TriggerTile = triggerTile;
            SpawnDetails = new List<MobSpawnExtra>();
        }

        protected RoomGenLoadBoss(RoomGenLoadBoss<T> other) : base(other)
        {
            MapID = other.MapID;
            this.RoomTerrain = other.RoomTerrain;
            TriggerTile = other.TriggerTile;
            SpawnDetails = new List<MobSpawnExtra>();
            foreach (MobSpawnExtra extra in other.SpawnDetails)
                SpawnDetails.Add(extra.Copy());
        }

        public override RoomGen<T> Copy() { return new RoomGenLoadBoss<T>(this); }


        public override void DrawOnMap(T map)
        {
            if (this.Draw.Width != this.roomMap.Width || this.Draw.Height != this.roomMap.Height)
            {
                this.DrawMapDefault(map);
                return;
            }

            //no copying is needed here since the map is disposed of after use

            DrawTiles(map);

            DrawDecorations(map);

            DrawItems(map);

            for (int xx = 0; xx < this.Draw.Width; xx++)
            {
                for (int yy = 0; yy < this.Draw.Height; yy++)
                {
                    map.SetTile(new Loc(this.Draw.X + xx, this.Draw.Y + yy), this.roomMap.Tiles[xx][yy]);
                }
            }

            if (this.roomMap.EntryPoints.Count < 1)
                throw new InvalidOperationException("Could not find an entry point!");


            MobSpawnState mobSpawnState = new MobSpawnState();
            foreach (Team team in this.roomMap.MapTeams)
            {
                foreach (Character member in team.EnumerateChars())
                {
                    MobSpawn newSpawn = new MobSpawn();
                    newSpawn.BaseForm = member.BaseForm;
                    newSpawn.Level = new RandRange(member.Level);
                    foreach (SlotSkill skill in member.BaseSkills)
                    {
                        if (!String.IsNullOrEmpty(skill.SkillNum))
                            newSpawn.SpecifiedSkills.Add(skill.SkillNum);
                    }
                    newSpawn.Intrinsic = member.BaseIntrinsics[0];

                    newSpawn.Tactic = member.Tactic.ID;

                    MobSpawnLoc setLoc = new MobSpawnLoc(this.Draw.Start + member.CharLoc);
                    newSpawn.SpawnFeatures.Add(setLoc);

                    foreach (MobSpawnExtra extra in SpawnDetails)
                        newSpawn.SpawnFeatures.Add(extra.Copy());

                    mobSpawnState.Spawns.Add(newSpawn);
                }
            }

            Loc triggerLoc = this.roomMap.EntryPoints[0].Loc;
            EffectTile newEffect = new EffectTile(TriggerTile, true, triggerLoc + this.Draw.Start);
            newEffect.TileStates.Set(new DangerState(true));
            newEffect.TileStates.Set(mobSpawnState);
            newEffect.TileStates.Set(new BoundsState(new Rect(this.Draw.Start - new Loc(1), this.Draw.Size + new Loc(2))));
            newEffect.TileStates.Set(new SongState(this.roomMap.Music));

            MapStartEventState beginEvent = new MapStartEventState();
            foreach (Priority priority in this.roomMap.MapEffect.OnMapStarts.GetPriorities())
            {
                foreach (SingleCharEvent step in this.roomMap.MapEffect.OnMapStarts.GetItems(priority))
                    beginEvent.OnMapStarts.Add(priority, step);
            }
            newEffect.TileStates.Set(beginEvent);
            ((IPlaceableGenContext<EffectTile>)map).PlaceItem(triggerLoc + this.Draw.Start, newEffect);
            map.GetPostProc(triggerLoc + this.Draw.Start).Status |= (PostProcType.Panel | PostProcType.Item | PostProcType.Terrain);

            //this.FulfillRoomBorders(map, this.FulfillAll);
            this.SetRoomBorders(map);

            for (int xx = 0; xx < Draw.Width; xx++)
            {
                for (int yy = 0; yy < Draw.Height; yy++)
                    map.GetPostProc(new Loc(Draw.X + xx, Draw.Y + yy)).AddMask(new PostProcTile(PreventChanges));
            }
        }


        protected override void PrepareFulfillableBorders(IRandom rand)
        {
            // NOTE: Because the context is not passed in when preparing borders,
            // the tile ID representing an opening must be specified on this class instead.
            if (this.Draw.Width != this.roomMap.Width || this.Draw.Height != this.roomMap.Height)
            {
                foreach (Dir4 dir in DirExt.VALID_DIR4)
                {
                    for (int jj = 0; jj < this.FulfillableBorder[dir].Length; jj++)
                        this.FulfillableBorder[dir][jj] = true;
                }
            }
            else
            {
                for (int ii = 0; ii < this.Draw.Width; ii++)
                {
                    this.FulfillableBorder[Dir4.Up][ii] = this.roomMap.Tiles[ii][0].TileEquivalent(this.RoomTerrain);
                    this.FulfillableBorder[Dir4.Down][ii] = this.roomMap.Tiles[ii][this.Draw.Height - 1].TileEquivalent(this.RoomTerrain);
                }

                for (int ii = 0; ii < this.Draw.Height; ii++)
                {
                    this.FulfillableBorder[Dir4.Left][ii] = this.roomMap.Tiles[0][ii].TileEquivalent(this.RoomTerrain);
                    this.FulfillableBorder[Dir4.Right][ii] = this.roomMap.Tiles[this.Draw.Width - 1][ii].TileEquivalent(this.RoomTerrain);
                }
            }
        }
    }
}
