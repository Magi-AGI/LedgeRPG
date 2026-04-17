using System.Collections.Generic;
using LedgeRPG.Core.World;
using UnityEngine;

namespace Magi.LedgeRPG
{
    public sealed class HexGridRenderer : MonoBehaviour
    {
        public float TileSize = 0.55f;

        private static readonly Color EmptyColor    = new Color(0.90f, 0.85f, 0.70f);
        private static readonly Color VisitedColor  = new Color(0.70f, 0.65f, 0.50f);
        private static readonly Color FoodColor     = new Color(0.30f, 0.90f, 0.30f);
        private static readonly Color ObstacleColor = new Color(0.35f, 0.35f, 0.35f);

        private readonly Dictionary<HexCoord, GameObject> _tiles    = new Dictionary<HexCoord, GameObject>();
        private readonly Dictionary<HexCoord, GameObject> _food     = new Dictionary<HexCoord, GameObject>();
        private readonly Dictionary<HexCoord, GameObject> _obstacles= new Dictionary<HexCoord, GameObject>();
        private readonly HashSet<HexCoord>                _visited  = new HashSet<HexCoord>();

        public void Build(World world)
        {
            Clear();
            foreach (var cell in world.GridSnapshot())
            {
                var pos = HexLayout.ToWorld(cell.Coord, TileSize);

                var tile = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                tile.name = $"Tile {cell.Coord.Q},{cell.Coord.R}";
                tile.transform.SetParent(transform, worldPositionStays: true);
                tile.transform.position = pos;
                tile.transform.localScale = new Vector3(TileSize * 1.7f, 0.05f, TileSize * 1.7f);
                Destroy(tile.GetComponent<Collider>());
                _tiles[cell.Coord] = tile;

                if (cell.Type == TileType.Food)
                    _food[cell.Coord] = MakeDecoration(
                        PrimitiveType.Sphere, pos + new Vector3(0, 0.25f, 0), TileSize * 0.55f, FoodColor);
                else if (cell.Type == TileType.Obstacle)
                    _obstacles[cell.Coord] = MakeDecoration(
                        PrimitiveType.Cube, pos + new Vector3(0, 0.35f, 0), TileSize * 0.95f, ObstacleColor);
            }
            Refresh(world);
        }

        public void MarkVisited(HexCoord at) => _visited.Add(at);

        public void Refresh(World world)
        {
            foreach (var kv in _tiles)
            {
                var color = _visited.Contains(kv.Key) ? VisitedColor : EmptyColor;
                kv.Value.GetComponent<Renderer>().material.color = color;
            }
            var consumed = new List<HexCoord>();
            foreach (var kv in _food)
                if (world.TileAt(kv.Key) != TileType.Food)
                    consumed.Add(kv.Key);
            foreach (var at in consumed)
            {
                Destroy(_food[at]);
                _food.Remove(at);
            }
        }

        public Vector3 GridCentroid(int gridSize)
        {
            var minCell = HexLayout.ToWorld(new HexCoord(0, 0), TileSize);
            var maxCell = HexLayout.ToWorld(new HexCoord(gridSize - 1, gridSize - 1), TileSize);
            return (minCell + maxCell) * 0.5f;
        }

        private GameObject MakeDecoration(PrimitiveType type, Vector3 position, float size, Color color)
        {
            var go = GameObject.CreatePrimitive(type);
            go.transform.SetParent(transform, worldPositionStays: true);
            go.transform.position = position;
            go.transform.localScale = Vector3.one * size;
            go.GetComponent<Renderer>().material.color = color;
            Destroy(go.GetComponent<Collider>());
            return go;
        }

        private void Clear()
        {
            for (int i = transform.childCount - 1; i >= 0; --i)
                Destroy(transform.GetChild(i).gameObject);
            _tiles.Clear();
            _food.Clear();
            _obstacles.Clear();
            _visited.Clear();
        }
    }
}
