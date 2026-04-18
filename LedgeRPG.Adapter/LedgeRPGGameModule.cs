using System;
using System.Globalization;
using LedgeRPG.Core.World;
using MagiGameServer.Contracts.Rules;

namespace LedgeRPG.Adapter
{
    /// Registers LedgeRPG with the shared MagiGameServer host. Statically
    /// constructible so the host binary can wire the module up at startup
    /// with a single `new LedgeRPGGameModule()` call site — matching the
    /// M4 static-registration plan ahead of any dynamic plugin loading.
    public sealed class LedgeRPGGameModule : IGameModule
    {
        public string GameId => "ledge-rpg";
        public string DisplayName => "LedgeRPG";

        // V0 is single-seat — the paper rules don't model a party. Widen to a
        // variable range in V1 when party traversal lands and ProjectStateFor
        // starts filtering per seat.
        public int MinSeats => 1;
        public int MaxSeats => 1;

        // Singleton rules adapter. Stateless per the IGameModule contract, so
        // a single instance is safely shared across all sessions of this game.
        public IRulesAdapter Rules { get; } = new LedgeRPGRulesAdapter();

        public Type ActionType => typeof(RPGAction);
        public Type StateType => typeof(RPGState);

        public object CreateInitialState(GameConfig config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));

            int gridSize      = ReadInt(config, "gridSize",      defaultValue: 8,   minValue: 1);
            int foodCount     = ReadInt(config, "foodCount",     defaultValue: 5,   minValue: 0);
            int obstacleCount = ReadInt(config, "obstacleCount", defaultValue: 8,   minValue: 0);
            int stepLimit     = ReadInt(config, "stepLimit",     defaultValue: 100, minValue: 1);

            var world = new World(
                seed: config.Seed,
                gridSize: gridSize,
                foodCount: foodCount,
                obstacleCount: obstacleCount,
                stepLimit: stepLimit);

            return new RPGState(world);
        }

        private static int ReadInt(GameConfig config, string key, int defaultValue, int minValue)
        {
            // Options is an optional bag — treat missing keys as "use default".
            // Any non-integer value is a client error; surface it loudly rather
            // than silently falling back to the default.
            if (config.Options == null || !config.Options.TryGetValue(key, out var raw))
                return defaultValue;

            if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
                throw new ArgumentException(
                    $"LedgeRPG option '{key}' must be an integer, got '{raw}'",
                    nameof(config));

            if (parsed < minValue)
                throw new ArgumentException(
                    $"LedgeRPG option '{key}' must be >= {minValue}, got {parsed}",
                    nameof(config));

            return parsed;
        }
    }
}
