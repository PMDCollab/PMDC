using System;
using System.Collections.Generic;
using RogueElements;
using RogueEssence.Dungeon;
using PMDC.Dungeon;
using RogueEssence.Dev;
using RogueEssence.LevelGen;

namespace PMDC.LevelGen
{
    [Serializable]
    public class RoomGenSpecificBoss<T> : RoomGenSpecific<T> where T : ListMapGenContext
    {
        public List<MobSpawn> Bosses;
        public int TriggerTile;
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

        public RoomGenSpecificBoss(int width, int height, ITile roomTerrain, bool fulfillAll, int triggerTile, Loc trigger, string song)
            : base(width, height, roomTerrain, fulfillAll)
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

            ////place the boss bounds here
            //CheckIntrudeBoundsEvent trapZone = new CheckIntrudeBoundsEvent();
            //trapZone.Bounds = new Rect(Trigger.Start + new Loc(this.Draw.Start), Trigger.Size);
            //{
            //    LockdownMapEvent lockdown = new LockdownMapEvent();
            //    lockdown.Bounds = new Rect(this.Draw.Start - new Loc(1), this.Draw.Size + new Loc(2));
            //    trapZone.Effects.Add(lockdown);
            //    trapZone.Effects.Add(new BossSpawnEvent(this.Draw, mobSpawns.ToArray()));
            //}
            //map.CheckEvents.Add(trapZone);


            EffectTile newEffect = new EffectTile(TriggerTile, true, Trigger + this.Draw.Start);
            newEffect.TileStates.Set(mobSpawnState);
            newEffect.TileStates.Set(new BoundsState(new Rect(this.Draw.Start - new Loc(1), this.Draw.Size + new Loc(2))));
            newEffect.TileStates.Set(new SongState(Song));
            ((IPlaceableGenContext<EffectTile>)map).PlaceItem(Trigger + this.Draw.Start, newEffect);
        }
    }
}
