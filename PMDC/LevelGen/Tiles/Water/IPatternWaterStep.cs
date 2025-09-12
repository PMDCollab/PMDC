// <copyright file="IPatternWaterStep.cs" company="Audino">
// Copyright (c) Audino
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

using System;
using System.Collections.Generic;
using RogueElements;

namespace PMDC.LevelGen
{
    public interface IPatternWaterStep : IWaterStep
    {
        RandRange Amount { get; set; }
    }
}