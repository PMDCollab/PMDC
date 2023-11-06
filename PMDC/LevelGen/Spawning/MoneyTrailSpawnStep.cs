// <copyright file="MoneyTrailSpawnStep.cs" company="Audino">
// Copyright (c) Audino
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

using System;
using System.Collections.Generic;
using RogueElements;
using RogueEssence.LevelGen;

namespace PMDC.LevelGen
{
    /// <summary>
    /// Spawns money in a trail leading up to an item at the endpoint.
    /// </summary>
    /// <typeparam name="TGenContext"></typeparam>
    /// <typeparam name="TSpawnable"></typeparam>
    [Serializable]
    public class MoneyTrailSpawnStep<TGenContext, TSpawnable> : RoomSpawnStep<TGenContext, TSpawnable>
        where TGenContext : class, IRoomGridGenContext, IPlaceableGenContext<TSpawnable>, IPlaceableGenContext<MoneySpawn>, ISpawningGenContext<MoneySpawn>
        where TSpawnable : ISpawnable
    {
        public MoneyTrailSpawnStep()
            : base()
        {
        }

        public MoneyTrailSpawnStep(IStepSpawner<TGenContext, TSpawnable> spawn, RandRange trailLength, IntRange placementValue)
            : base(spawn)
        {
            this.TrailLength = trailLength;
            this.PlacementValue = placementValue;
        }

        public RandRange TrailLength;

        public IntRange PlacementValue;

        public override void DistributeSpawns(TGenContext map, List<TSpawnable> spawns)
        {
            List<int> spawnedRooms = new List<int>();
            // Choose randomly
            for (int nn = 0; nn < spawns.Count; nn++)
            {
                for (int ii = 0; ii < 10; ii++)
                {
                    int randIdx = map.Rand.Next(map.GridPlan.RoomCount);
                    if (!BaseRoomFilter.PassesAllFilters(map.GridPlan.GetRoomPlan(randIdx), this.Filters))
                        continue;

                    if (spawnItemInRoom(map, randIdx, spawns[nn]))
                    {
                        spawnedRooms.Add(randIdx);
                        break;
                    }
                }
            }


            MoneySpawn total = map.Spawner.Pick(map.Rand);
            int chosenDiv = Math.Min(total.Amount, Math.Max(1, spawnedRooms.Count));
            int avgAmount = total.Amount / chosenDiv;
            for (int ii = 0; ii < chosenDiv; ii++)
            {
                int budget = avgAmount;
                while (budget > 0)
                    this.spawnWithTrail(map, spawnedRooms[ii], map.Rand.Next(360), ref budget);
            }
        }

        private void spawnWithTrail(TGenContext map, int startIdx, int startDegrees, ref int moneyToSpawn)
        {
            int avgCost = (PlacementValue.Min + PlacementValue.Max) / 2;
            int allowedLength = moneyToSpawn / avgCost;
            int chosenLength = Math.Min(TrailLength.Pick(map.Rand), allowedLength);
            int totalCost = chosenLength * avgCost;
            if (moneyToSpawn - totalCost < avgCost)
                totalCost = moneyToSpawn;

            List<MoneySpawn> toSpawns = new List<MoneySpawn>();
            int currentCost = 0;
            for (int ii = 0; ii < chosenLength; ii++)
            {
                if (ii == chosenLength - 1)
                {
                    toSpawns.Add(new MoneySpawn(totalCost - currentCost));
                    currentCost = totalCost;
                }
                else
                {
                    int added = MathUtils.Interpolate(PlacementValue.Min, PlacementValue.Max, ii, chosenLength - 1);
                    toSpawns.Add(new MoneySpawn(added));
                    currentCost += added;
                }
            }

            int curRotation = startDegrees;
            Loc roomLoc = map.GridPlan.GetRoomPlan(startIdx).Bounds.Start;
            for (int ii = chosenLength - 1; ii >= 0; ii--)
            {
                //attempt to move forward and place 3 times
                bool spawned = false;
                for (int jj = 0; jj < 3; jj++)
                {
                    int roomIdx = map.GridPlan.GetRoomIndex(roomLoc);
                    if (roomIdx > -1)
                    {
                        GridRoomPlan curPlan = map.GridPlan.GetRoomPlan(roomIdx);
                        if (!curPlan.PreferHall)
                        {
                            if (spawnInMoneyRoom(map, roomIdx, toSpawns[ii]))
                                spawned = true;
                        }
                    }
                    //move forward
                    roomLoc = moveForward(map, roomLoc, ref curRotation);

                    if (spawned)
                        break;
                }
            }

            moneyToSpawn -= totalCost;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="map"></param>
        /// <param name="inLoc"></param>
        /// <param name="degreeRotation">Rotation is treated as clockwise from down</param>
        /// <returns></returns>
        private Loc moveForward(TGenContext map, Loc inLoc, ref int degreeRotation)
        {
            //move in the chosen direction
            Dir8 moveDir = getRandDirFromDegree(map.Rand, degreeRotation);

            Loc moveLoc = moveDir.GetLoc();
            //check against horizontal border cross
            if (inLoc.X + moveLoc.X < 0 || inLoc.X + moveLoc.X >= map.GridPlan.Size.X)
            {
                //if so, reverse the moveLoc X and reflect degreeRotation
                moveLoc.X = -moveLoc.X;
                degreeRotation = (360 * 2 - degreeRotation) % 360;
            }

            //check against vertical border cross
            if (inLoc.Y + moveLoc.Y < 0 || inLoc.Y + moveLoc.Y >= map.GridPlan.Size.Y)
            {
                //if so, reverse the moveLoc Y and reflect degreeRotation
                moveLoc.Y = -moveLoc.Y;
                degreeRotation = ((360 - (90 + degreeRotation)) % 360 + 270) % 360;
            }

            Loc outLoc = inLoc + moveLoc;

            //modify the rotation
            degreeRotation = (degreeRotation + map.Rand.Next(360-45, 360+45+1)) % 360;

            return outLoc;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="rand"></param>
        /// <param name="degreeRotation">Rotation is treated as clockwise from down</param>
        /// <returns></returns>
        private Dir8 getRandDirFromDegree(IRandom rand, int degreeRotation)
        {
            int dir = degreeRotation / 45;
            int remainder = degreeRotation % 45;
            if (rand.Next(45) < remainder)
                dir = (dir + 1) % 8;
            return (Dir8)dir;
        }



        private bool spawnItemInRoom(TGenContext map, int roomIdx, TSpawnable spawn)
        {
            IRoomGen room = map.GridPlan.GetRoom(roomIdx);
            List<Loc> freeTiles = ((IPlaceableGenContext<TSpawnable>)map).GetFreeTiles(room.Draw);

            if (freeTiles.Count > 0)
            {
                int randIndex = map.Rand.Next(freeTiles.Count);
                map.PlaceItem(freeTiles[randIndex], spawn);
                return true;
            }

            return false;
        }

        private bool spawnInMoneyRoom(TGenContext map, int roomIdx, MoneySpawn spawn)
        {
            IRoomGen room = map.GridPlan.GetRoom(roomIdx);
            List<Loc> freeTiles = ((IPlaceableGenContext<MoneySpawn>)map).GetFreeTiles(room.Draw);

            if (freeTiles.Count > 0)
            {
                int randIndex = map.Rand.Next(freeTiles.Count);
                map.PlaceItem(freeTiles[randIndex], spawn);
                return true;
            }

            return false;
        }
    }
}
