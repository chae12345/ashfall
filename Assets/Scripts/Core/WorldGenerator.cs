using System;
using System.Collections.Generic;

namespace Ashfall
{
    public static class WorldGenerator
    {
        public static World Generate(int seed, int width = 96, int height = 72, int sourceCount = 5, float sourceFlow = 3)
        {
            if (width < 24 || height < 24 || sourceCount < 1 || sourceCount > 20 || sourceFlow <= 0 || float.IsNaN(sourceFlow) || float.IsInfinity(sourceFlow))
                throw new ArgumentOutOfRangeException("Invalid map settings");
            var world = new World(width, height, seed); var noise = new PerlinNoise(seed); var random = new Random(seed);
            for (int i = 0; i < world.Tiles.Length; i++)
            {
                Cell p = world.Position(i); Tile tile = world.Tiles[i];
                float n = noise.Sample(p.X * .038f + 11.3f, p.Y * .038f + 7.2f);
                n += .45f * noise.Sample(p.X * .085f + 40.5f, p.Y * .085f + 20.1f);
                n += .16f * noise.Sample(p.X * .21f, p.Y * .21f);
                tile.Height = .44f + n * .43f + (float)p.Y / height * .18f;
                tile.Ground = tile.Height > .64f ? Ground.Rock : tile.Height > .48f ? Ground.Grass : Ground.Soil;
                if (tile.Ground != Ground.Rock && random.NextDouble() < .075)
                { tile.Ground = Ground.Rubble; tile.Scrap = random.Next(3, 9); }
            }
            GenerateRivers(world, sourceCount, sourceFlow);
            return world;
        }

        // Priority-flood assigns every tile a parent towards an edge outlet.
        // Parents are popped first, so reverse pop order accumulates flow without cycles.
        private static void GenerateRivers(World world, int count, float flow)
        {
            var heap = new MinHeap(); var visited = new bool[world.Tiles.Length];
            var filled = new float[visited.Length]; var order = new List<int>(visited.Length);
            for (int i = 0; i < visited.Length; i++)
            {
                Cell p = world.Position(i);
                if (p.X != 0 && p.Y != 0 && p.X != world.Width - 1 && p.Y != world.Height - 1) continue;
                visited[i] = true; filled[i] = world.Tiles[i].Height; heap.Push(i, filled[i]);
            }
            while (heap.Count > 0)
            {
                int current = heap.Pop(); order.Add(current); Cell p = world.Position(current);
                foreach (Cell d in Cell.Directions)
                {
                    Cell next = p + d; if (!world.Contains(next)) continue;
                    int id = world.Index(next); if (visited[id]) continue;
                    visited[id] = true; filled[id] = Math.Max(world.Tiles[id].Height, filled[current]);
                    world.Tiles[id].Downstream = current; heap.Push(id, filled[id]);
                }
            }
            var candidates = new List<int>();
            for (int i = 0; i < visited.Length; i++)
            {
                Cell p = world.Position(i);
                if (p.X > 7 && p.Y > 7 && p.X < world.Width - 8 && p.Y < world.Height - 8) candidates.Add(i);
            }
            candidates.Sort(delegate(int a, int b) { int c = world.Tiles[b].Height.CompareTo(world.Tiles[a].Height); return c != 0 ? c : a.CompareTo(b); });
            foreach (int id in candidates)
            {
                bool near = false;
                foreach (int other in world.Sources) if (world.Position(id).Distance(world.Position(other)) < 12) near = true;
                if (near) continue;
                world.Sources.Add(id); world.Tiles[id].Flow += flow;
                if (world.Sources.Count >= count) break;
            }
            for (int n = order.Count - 1; n >= 0; n--)
            {
                Tile tile = world.Tiles[order[n]];
                if (tile.Downstream >= 0) world.Tiles[tile.Downstream].Flow += tile.Flow;
            }
            // Flow stays on the centerline; wet bank tiles are visual/occupancy width only.
            foreach (int id in order)
            {
                Tile tile = world.Tiles[id]; if (tile.Flow <= 0) continue;
                int radius = Math.Min(2, (int)Math.Sqrt(tile.Flow / 5f)); Cell p = world.Position(id);
                for (int y = -radius; y <= radius; y++) for (int x = -radius; x <= radius; x++)
                {
                    if (x * x + y * y > radius * radius) continue;
                    Cell wet = new Cell(p.X + x, p.Y + y); if (!world.Contains(wet)) continue;
                    world.At(wet).Ground = Ground.Water; world.At(wet).Scrap = 0;
                }
            }
            // Cut the river bed downstream. This avoids visible uphill channels after basin filling.
            for (int n = order.Count - 1; n >= 0; n--)
            {
                Tile tile = world.Tiles[order[n]];
                if (tile.Flow > 0 && tile.Downstream >= 0)
                    world.Tiles[tile.Downstream].Height = Math.Min(world.Tiles[tile.Downstream].Height, tile.Height - .0001f);
            }
        }
    }
}
