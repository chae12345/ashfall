using System;
using System.Collections.Generic;

namespace Ashfall
{
    public enum Ground { Soil, Grass, Rock, Water, Rubble }
    public enum Structure { None, Wall, Bridge }
    public enum Weapon { Knife, Bow, Rifle, Shotgun }

    [Serializable]
    public struct Cell : IEquatable<Cell>
    {
        public int X, Y;
        public Cell(int x, int y) { X = x; Y = y; }
        public static readonly Cell[] Directions = { new Cell(0, 1), new Cell(1, 0), new Cell(0, -1), new Cell(-1, 0) };
        public static Cell operator +(Cell a, Cell b) { return new Cell(a.X + b.X, a.Y + b.Y); }
        public bool Equals(Cell other) { return X == other.X && Y == other.Y; }
        public override bool Equals(object obj) { return obj is Cell && Equals((Cell)obj); }
        public override int GetHashCode() { return X * 397 ^ Y; }
        public int Distance(Cell other) { return Math.Abs(X - other.X) + Math.Abs(Y - other.Y); }
    }

    [Serializable]
    public sealed class Tile
    {
        public float Height, Flow;
        public int Downstream = -1;
        public Ground Ground;
        public Structure Structure;
        public int Scrap;
        public bool Walkable { get { return Structure != Structure.Wall && (Ground != Ground.Rock && Ground != Ground.Water || Structure == Structure.Bridge); } }
    }

    [Serializable]
    public sealed class World
    {
        public int Width, Height, Seed;
        public Tile[] Tiles;
        public List<int> Sources = new List<int>();
        public World(int width, int height, int seed)
        {
            Width = width; Height = height; Seed = seed;
            Tiles = new Tile[width * height];
            for (int i = 0; i < Tiles.Length; i++) Tiles[i] = new Tile();
        }
        public bool Contains(Cell p) { return p.X >= 0 && p.Y >= 0 && p.X < Width && p.Y < Height; }
        public int Index(Cell p) { return p.Y * Width + p.X; }
        public Cell Position(int i) { return new Cell(i % Width, i / Width); }
        public Tile At(Cell p) { return Tiles[Index(p)]; }
        public bool Walkable(Cell p) { return Contains(p) && At(p).Walkable; }
    }

    // A small deterministic binary heap shared by drainage and path finding.
    internal sealed class MinHeap
    {
        private struct Entry { public int Id; public float Priority; public Entry(int id, float priority) { Id = id; Priority = priority; } }
        private readonly List<Entry> items = new List<Entry>();
        public int Count { get { return items.Count; } }
        private static bool Before(Entry a, Entry b) { return a.Priority < b.Priority || a.Priority == b.Priority && a.Id < b.Id; }
        public void Push(int id, float priority)
        {
            Entry item = new Entry(id, priority); int i = items.Count; items.Add(item);
            while (i > 0)
            {
                int parent = (i - 1) / 2;
                if (!Before(item, items[parent])) break;
                items[i] = items[parent]; i = parent;
            }
            items[i] = item;
        }
        public int Pop()
        {
            int result = items[0].Id, last = items.Count - 1; Entry tail = items[last]; items.RemoveAt(last);
            if (last == 0) return result;
            int i = 0;
            while (i * 2 + 1 < last)
            {
                int child = i * 2 + 1;
                if (child + 1 < last && Before(items[child + 1], items[child])) child++;
                if (!Before(items[child], tail)) break;
                items[i] = items[child]; i = child;
            }
            items[i] = tail; return result;
        }
    }

    internal sealed class PerlinNoise
    {
        private readonly int[] permutation = new int[512];
        public PerlinNoise(int seed)
        {
            var random = new Random(seed); var values = new int[256];
            for (int i = 0; i < 256; i++) values[i] = i;
            for (int i = 255; i > 0; i--) { int j = random.Next(i + 1), v = values[i]; values[i] = values[j]; values[j] = v; }
            for (int i = 0; i < 512; i++) permutation[i] = values[i & 255];
        }
        private static float Fade(float t) { return t * t * t * (t * (t * 6 - 15) + 10); }
        private static float Lerp(float a, float b, float t) { return a + (b - a) * t; }
        private static float Gradient(int hash, float x, float y)
        {
            switch (hash & 7) { case 0: return x; case 1: return -x; case 2: return y; case 3: return -y;
                case 4: return (x + y) * .7071f; case 5: return (x - y) * .7071f; case 6: return (-x + y) * .7071f; default: return (-x - y) * .7071f; }
        }
        public float Sample(float x, float y)
        {
            int ix = (int)Math.Floor(x), iy = (int)Math.Floor(y); x -= ix; y -= iy; ix &= 255; iy &= 255;
            float u = Fade(x), v = Fade(y);
            int a = permutation[ix] + iy, b = permutation[ix + 1] + iy;
            return Lerp(Lerp(Gradient(permutation[a], x, y), Gradient(permutation[b], x - 1, y), u),
                Lerp(Gradient(permutation[a + 1], x, y - 1), Gradient(permutation[b + 1], x - 1, y - 1), u), v);
        }
    }
}
