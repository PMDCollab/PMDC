using System;
using System.Collections.Generic;
using RogueElements;
using RogueEssence.Dungeon;
using PMDC.Dungeon;
using RogueEssence.Dev;
using RogueEssence.LevelGen;
using Newtonsoft.Json;
using RogueEssence.Data;

namespace PMDC.LevelGen
{
    // TODO: v1.1: Delete
    /// <summary>
    /// Generates a boss room with specific tiles and mobs.
    /// DEPRECATED
    /// </summary>
    /// <typeparam name="T"></typeparam>
    [Serializable]
    public class RoomGenSpecificBoss<T> : RoomGenSpecific<T> where T : ListMapGenContext
    {
        public List<MobSpawn> Bosses;
        [JsonConverter(typeof(TileConverter))]
        [DataType(0, DataManager.DataType.Tile, false)]
        public string TriggerTile;
        public Loc Trigger;

        [Music(0)]
        public string Song;

        public RoomGenSpecificBoss()
        {
            Bosses = new List<MobSpawn>();
        }

        public RoomGenSpecificBoss(int width, int height) : base(width, height)
        {
            Bosses = new List<MobSpawn>();
        }

        public RoomGenSpecificBoss(int width, int height, ITile roomTerrain, string triggerTile, Loc trigger, string song)
            : base(width, height, roomTerrain)
        {
            TriggerTile = triggerTile;
            Trigger = trigger;
            Bosses = new List<MobSpawn>();
            Song = song;
        }

        protected RoomGenSpecificBoss(RoomGenSpecificBoss<T> other) : base(other)
        {
            Bosses = new List<MobSpawn>();
            foreach (MobSpawn spawn in other.Bosses)
                Bosses.Add(spawn.Copy());
            TriggerTile = other.TriggerTile;
            Trigger = other.Trigger;
            Song = other.Song;
        }
        public override RoomGen<T> Copy() { return new RoomGenSpecificBoss<T>(this); }

        public override void DrawOnMap(T map)
        {
            if (this.Draw.Width != this.Tiles.Length || this.Draw.Height != this.Tiles[0].Length)
            {
                this.DrawMapDefault(map);
                return;
            }

            base.DrawOnMap(map);

            for (int xx = 0; xx < this.Draw.Width; xx++)
            {
                for (int yy = 0; yy < this.Draw.Height; yy++)
                {
                    map.GetPostProc(new Loc(this.Draw.X + xx, this.Draw.Y + yy)).Status |= PostProcType.Terrain;
                }
            }

            //create new bosses from the spawns, adjusting their locations
            MobSpawnState mobSpawnState = new MobSpawnState();
            foreach (MobSpawn spawn in Bosses)
            {
                MobSpawn newSpawn = spawn.Copy();
                MobSpawnLoc setLoc = null;
                foreach (MobSpawnExtra extra in newSpawn.SpawnFeatures)
                {
                    MobSpawnLoc extraLoc = extra as MobSpawnLoc;
                    if (extraLoc != null)
                    {
                        setLoc = extraLoc;
                        break;
                    }
                }

                if (setLoc != null)
                {
                    setLoc.Loc = setLoc.Loc + this.Draw.Start;
                }
                else
                {
                    setLoc = new MobSpawnLoc(this.Draw.Center);
                    newSpawn.SpawnFeatures.Add(setLoc);
                }
                mobSpawnState.Spawns.Add(newSpawn);
            }


            EffectTile newEffect = new EffectTile(TriggerTile, true, Trigger + this.Draw.Start);
            newEffect.TileStates.Set(new DangerState(true));
            newEffect.TileStates.Set(mobSpawnState);
            newEffect.TileStates.Set(new BoundsState(new Rect(this.Draw.Start - new Loc(1), this.Draw.Size + new Loc(2))));
            newEffect.TileStates.Set(new SongState(Song));
            ((IPlaceableGenContext<EffectTile>)map).PlaceItem(Trigger + this.Draw.Start, newEffect);
            map.GetPostProc(Trigger + this.Draw.Start).Status |= (PostProcType.Panel | PostProcType.Item | PostProcType.Terrain);
        }

        public void Dump(string name)
        {
            Map map = new Map();

            map.AssetName = name;
            int width = this.Tiles.Length;
            int height = this.Tiles[0].Length;
            map.CreateNew(width, height);
            map.Music = Song;

            for (int xx = 0; xx < width; xx++)
            {
                for (int yy = 0; yy < height; yy++)
                {
                    map.Tiles[xx][yy] = (Tile)this.Tiles[xx][yy];
                }
            }
            map.EntryPoints.Add(new LocRay8(Trigger, Dir8.Up));


            MonsterTeam team = new MonsterTeam();
            foreach(MobSpawn spawn in Bosses)
            {
                MonsterID formData = spawn.BaseForm;
                MonsterData dex = DataManager.Instance.GetMonster(formData.Species);
                if (formData.Form == -1)
                {
                    int form = map.Rand.Next(dex.Forms.Count);
                    formData.Form = form;
                }

                BaseMonsterForm formEntry = dex.Forms[formData.Form];

                if (formData.Gender == Gender.Unknown)
                    formData.Gender = formEntry.RollGender(map.Rand);

                if (String.IsNullOrEmpty(formData.Skin))
                    formData.Skin = formEntry.RollSkin(map.Rand);

                CharData character = new CharData();
                character.BaseForm = formData;
                character.Level = spawn.Level.Pick(map.Rand);

                List<string> final_skills = formEntry.RollLatestSkills(character.Level, spawn.SpecifiedSkills);
                for (int ii = 0; ii < final_skills.Count; ii++)
                    character.BaseSkills[ii] = new SlotSkill(final_skills[ii]);

                if (String.IsNullOrEmpty(spawn.Intrinsic))
                    character.SetBaseIntrinsic(formEntry.RollIntrinsic(map.Rand, 2));
                else
                    character.SetBaseIntrinsic(spawn.Intrinsic);
                Character newMob = new Character(character);
                team.Players.Add(newMob);
                AITactic tactic = DataManager.Instance.GetAITactic(spawn.Tactic);
                newMob.Tactic = new AITactic(tactic);

                MobSpawnLoc setLoc = null;
                foreach (MobSpawnExtra extra in spawn.SpawnFeatures)
                {
                    MobSpawnLoc extraLoc = extra as MobSpawnLoc;
                    if (extraLoc != null)
                    {
                        setLoc = extraLoc;
                        break;
                    }
                }

                newMob.CharLoc = setLoc.Loc;
                newMob.CharDir = setLoc.Dir;
            }
            map.MapTeams.Add(team);

            string groundTileset = "test_dungeon_floor";
            string blockTileset = "test_dungeon_wall";
            string waterTileset = "test_dungeon_secondary";

            map.TextureMap[DataManager.Instance.GenFloor] = new AutoTile(groundTileset);
            map.TextureMap[DataManager.Instance.GenWall] = new AutoTile(blockTileset);
            map.TextureMap[DataManager.Instance.GenUnbreakable] = new AutoTile(blockTileset);
            map.TextureMap["water"] = new AutoTile(waterTileset, groundTileset);
            map.TextureMap["lava"] = new AutoTile(waterTileset, groundTileset);
            map.TextureMap["pit"] = new AutoTile(waterTileset, groundTileset);

            map.CalculateTerrainAutotiles(Loc.Zero, new Loc(map.Width, map.Height));

            BattlePositionEvent positionEvent = new BattlePositionEvent();
            positionEvent.StartLocs = new LocRay8[4];
            positionEvent.StartLocs[0] = new LocRay8(Loc.Zero, Dir8.Up);
            positionEvent.StartLocs[1] = new LocRay8(new Loc(0, 1), Dir8.Up);
            positionEvent.StartLocs[2] = new LocRay8(new Loc(-1, 1), Dir8.Up);
            positionEvent.StartLocs[3] = new LocRay8(new Loc(1, 1), Dir8.Up);
            map.MapEffect.OnMapStarts.Add(-15, positionEvent);
            
            DataManager.SaveData(map, DataManager.MAP_PATH, name, DataManager.MAP_EXT);
        }
    }
}
