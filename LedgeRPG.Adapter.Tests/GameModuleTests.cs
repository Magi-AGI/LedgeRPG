using System;
using System.Collections.Generic;
using LedgeRPG.Adapter;
using MagiGameServer.Contracts.Rules;
using Xunit;

namespace LedgeRPG.Adapter.Tests
{
    public class GameModuleTests
    {
        [Fact]
        public void GameIdAndSeatRangeMatchV0Contract()
        {
            var module = new LedgeRPGGameModule();
            Assert.Equal("ledge-rpg", module.GameId);
            Assert.Equal("LedgeRPG", module.DisplayName);
            Assert.Equal(1, module.MinSeats);
            Assert.Equal(1, module.MaxSeats);
        }

        [Fact]
        public void CreateInitialStateHonoursSeedFromConfig()
        {
            var module = new LedgeRPGGameModule();
            var a = (RPGState)module.CreateInitialState(new GameConfig
            {
                Seed = 42, SeatCount = 1, Options = new Dictionary<string, string>()
            });
            var b = (RPGState)module.CreateInitialState(new GameConfig
            {
                Seed = 42, SeatCount = 1, Options = new Dictionary<string, string>()
            });
            var c = (RPGState)module.CreateInitialState(new GameConfig
            {
                Seed = 7, SeatCount = 1, Options = new Dictionary<string, string>()
            });

            Assert.Equal(a.AgentPos, b.AgentPos);
            Assert.NotEqual(a.AgentPos, c.AgentPos);
        }

        [Fact]
        public void CreateInitialStateAppliesOverrides()
        {
            var module = new LedgeRPGGameModule();
            var state = (RPGState)module.CreateInitialState(new GameConfig
            {
                Seed = 0,
                SeatCount = 1,
                Options = new Dictionary<string, string>
                {
                    ["gridSize"] = "4",
                    ["foodCount"] = "2",
                    ["obstacleCount"] = "1",
                    ["stepLimit"] = "25"
                }
            });

            Assert.Equal(4, state.GridSize);
            Assert.Equal(2, state.FoodCount);
            Assert.Equal(1, state.ObstacleCount);
            Assert.Equal(25, state.StepLimit);
            Assert.Equal(4 * 4 - 1, state.TotalPassable);
        }

        [Fact]
        public void CreateInitialStateRejectsNonIntegerOption()
        {
            var module = new LedgeRPGGameModule();
            Assert.Throws<ArgumentException>(() => module.CreateInitialState(new GameConfig
            {
                Seed = 0,
                SeatCount = 1,
                Options = new Dictionary<string, string> { ["gridSize"] = "not-a-number" }
            }));
        }

        [Fact]
        public void CreateInitialStateRejectsOutOfRangeOption()
        {
            var module = new LedgeRPGGameModule();
            Assert.Throws<ArgumentException>(() => module.CreateInitialState(new GameConfig
            {
                Seed = 0,
                SeatCount = 1,
                Options = new Dictionary<string, string> { ["gridSize"] = "0" }
            }));
        }
    }
}
