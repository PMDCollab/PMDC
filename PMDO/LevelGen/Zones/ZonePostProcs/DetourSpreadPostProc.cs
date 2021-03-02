//using System;
//using System.Collections.Generic;
//using RogueElements;
//using PMDO.Dungeon;

//namespace PMDO.LevelGen
//{
//    //TODO: revive this trainwreck of a class after you've split it up

//    [Serializable]
//    public class DetourSpreadPostProc : ZonePostProc<MapGenContext>
//    {
//        public Range FloorSpacing;
//        public Range FloorRange;
//        public bool RelativeFloor;
//        public int Priority;


//        //so we should have an effect tile spawn range list
//        public SpawnRangeList<EffectTile> TileSpawns;
//        //a guarded room spawn range list
//        public SpawnRangeList<RoomGenGuardedCave<MapGenContext>> GuardRoomSpawns;
//        ////guarded rooms must be singular
//        //a guard spawn range list, which can be applied to everything
//        public SpawnRangeList<MobSpawner> GuardSpawns;
//        //a guard *team* spawn range list, which can be applied to just tiles.  add an empty specificteamspawner to specify "no guard"
//        public SpawnRangeList<TeamSpawner> TeamSpawns;

//        //spreads an item through the floors
//        //ensures that the space in floors between occurrences is kept tame
//        public DetourSpreadPostProc(int priority)
//        {
//            Priority = priority;
//            TileSpawns = new SpawnRangeList<EffectTile>();
//            GuardRoomSpawns = new SpawnRangeList<RoomGenGuardedCave<MapGenContext>>();
//            GuardSpawns = new SpawnRangeList<MobSpawner>();
//            TeamSpawns = new SpawnRangeList<TeamSpawner>();
//        }

//        public DetourSpreadPostProc(int priority, Range spacing, Range floorRange) : this(priority)
//        {
//            FloorSpacing = spacing;
//            FloorRange = floorRange;
//        }

//        public override void Apply(ReRandom rand, List<MapGen<MapGenContext>> floors)
//        {
//            int currentFloor = FloorRange.Min;
//            //int compression = 0;

//            int add = rand.Next(FloorSpacing.Max);
//            currentFloor += add;
//            //compression += (FloorSpacing.Max - add);

//            while (currentFloor < floors.Count && currentFloor < FloorRange.Max)
//            {
//                SpawnList<EffectTile> tiles = TileSpawns.GetSpawnList(currentFloor);
//                if (tiles.Count > 0)
//                {
//                    SpawnList<MobSpawner> guards = GuardSpawns.GetSpawnList(currentFloor);
//                    bool useRoom = (guards.Count > 0) && (rand.Next(5) == 0);//chance of a guarded room appearing where it's possible is always 20; NOTE: hardcoded value!
//                    if (useRoom)
//                    {
//                        SpawnList<RoomGenGuardedCave<MapGenContext>> caves = GuardRoomSpawns.GetSpawnList(currentFloor);
//                        if (caves.Count > 0)
//                        {
//                            RoomGenGuardedCave<MapGenContext> cave = (RoomGenGuardedCave<MapGenContext>)caves.Pick(rand).Copy();
//                            //add tile
//                            for (int ii = 0; ii < tiles.Count; ii++)
//                            {
//                                EffectTile newTile = new EffectTile(tiles.GetSpawn(ii));
//                                if (RelativeFloor)
//                                {
//                                    DestState destState = newTile.GetTileState<DestState>();
//                                    destState.Dest.ID = destState.Dest.ID + currentFloor;
//                                }
//                                cave.TileTreasures.SpecificSpawns.Add(newTile);
//                            }
//                            //add guards
//                            for (int ii = 0; ii < guards.Count; ii++)
//                                cave.GuardTypes.Add(guards.GetSpawn(ii).Copy(), guards.GetSpawnRate(ii));

//                            //find the first postproc that is a GridRoom postproc and add this to its special rooms

//                            AddGridSpecialRoomStep<MapGenContext, GridFloorPlan> specialStep = new AddGridSpecialRoomStep<MapGenContext, GridFloorPlan>();
//                            specialStep.Rooms = new PresetPicker<RoomGen<MapGenContext>>(cave);
//                            floors[currentFloor].GenSteps.Add(new GenPriority<GenStep<MapGenContext>>(Priority, specialStep));
//                        }
//                        else
//                            useRoom = false;
//                    }
//                    if (!useRoom)
//                    {
//                        DetourStep<MapGenContext> postProc = new DetourStep<MapGenContext>();
//                        //add tile
//                        for (int ii = 0; ii < tiles.Count; ii++)
//                        {
//                            EffectTile newTile = new EffectTile(tiles.GetSpawn(ii));
//                            if (RelativeFloor)
//                            {
//                                DestState destState = newTile.GetTileState<DestState>();
//                                destState.Dest.ID = destState.Dest.ID + currentFloor;
//                            }
//                            postProc.Spawns.Add(newTile);
//                        }
//                        //add guards
//                        for (int ii = 0; ii < guards.Count; ii++)
//                        {
//                            SpecificTeamSpawner newSpawn = new SpecificTeamSpawner(guards.GetSpawn(ii).Copy());
//                            postProc.GuardSpawns.Add(newSpawn, guards.GetSpawnRate(ii));
//                        }
//                        SpawnList<TeamSpawner> teams = TeamSpawns.GetSpawnList(currentFloor);
//                        for (int ii = 0; ii < teams.Count; ii++)
//                            postProc.GuardSpawns.Add(teams.GetSpawn(ii).Clone(), teams.GetSpawnRate(ii));//Clone Use Case

//                        floors[currentFloor].GenSteps.Add(new GenPriority<GenStep<MapGenContext>>(Priority, postProc));
//                    }
//                }

//                add = rand.Next(FloorSpacing.Min, FloorSpacing.Max/* + compression*/);
//                currentFloor += add;
//                //compression += (FloorSpacing.Max - add);
//            }
//        }
//    }
//}
