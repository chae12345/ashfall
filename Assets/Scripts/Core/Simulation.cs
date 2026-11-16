using System;
using System.Collections.Generic;

namespace Ashfall
{
    [Serializable]
    public sealed class Survivor
    {
        public int Id, Health = 100, Facing, Ammo = 36;
        public string Name;
        public Cell Position;
        public bool Prone, Hostile;
        public Weapon Weapon = Weapon.Rifle;
        [NonSerialized] public Queue<Cell> Path = new Queue<Cell>();
        [NonSerialized] public float MoveClock, Cooldown, PostureClock;
        [NonSerialized] public int Target = -1;
        [NonSerialized] public int SalvageTile = -1;
        public bool Alive { get { return Health > 0; } }
        public Cell Tail { get { return Position + Cell.Directions[(Facing + 2) % 4]; } }
        public bool Occupies(Cell p) { return Alive && (Position.Equals(p) || Prone && Tail.Equals(p)); }
    }

    public sealed class WeaponSpec
    {
        public readonly float Range, Interval, Accuracy;
        public readonly int Damage;
        public WeaponSpec(float range, float interval, float accuracy, int damage)
        { Range = range; Interval = interval; Accuracy = accuracy; Damage = damage; }
        public static WeaponSpec For(Weapon weapon)
        {
            switch (weapon)
            {
                case Weapon.Knife: return new WeaponSpec(1.5f, .7f, .95f, 19);
                case Weapon.Bow: return new WeaponSpec(11, 1.8f, .73f, 24);
                case Weapon.Shotgun: return new WeaponSpec(7, 1.25f, .85f, 42);
                default: return new WeaponSpec(19, .9f, .8f, 23);
            }
        }
    }

    public sealed class Shot
    {
        public Cell From, To; public bool Hit, Hostile; public float Life = .15f;
    }

    public sealed class Simulation
    {
        public World World;
        public readonly List<Survivor> People = new List<Survivor>();
        public readonly List<Shot> Shots = new List<Shot>();
        public int Scrap = 45, Kills;
        public string Message = "Recover scrap. Build shelter. Survive the wasteland.";
        private readonly Random random;
        private int nextId;
        public Simulation(World world) { World = world; random = new Random(world.Seed ^ 18373); }

        public void RestorePeople(IEnumerable<Survivor> savedPeople)
        {
            var restored = new List<Survivor>(); var ids = new HashSet<int>(); var occupied = new HashSet<Cell>();
            int restoredNextId = 0;
            foreach (Survivor saved in savedPeople)
            {
                if (saved == null || saved.Id < 0 || saved.Id == int.MaxValue || !ids.Add(saved.Id) || !World.Contains(saved.Position) ||
                    saved.Facing < 0 || saved.Facing > 3 || (int)saved.Weapon < 0 || (int)saved.Weapon > 3 || saved.Health < 0 || saved.Health > 100 || saved.Ammo < 0)
                    throw new ArgumentException("Invalid survivor data.");
                var p = new Survivor { Id = saved.Id, Name = saved.Name, Position = saved.Position, Facing = saved.Facing,
                    Health = saved.Health, Ammo = saved.Ammo, Weapon = saved.Weapon, Hostile = saved.Hostile, Prone = saved.Prone && saved.Alive };
                if (p.Alive && (!World.Walkable(p.Position) || !occupied.Add(p.Position) || p.Prone && (!World.Walkable(p.Tail) || !occupied.Add(p.Tail))))
                    throw new ArgumentException("Survivor footprint overlaps blocked space.");
                restored.Add(p); restoredNextId = Math.Max(restoredNextId, p.Id + 1);
            }
            People.Clear(); People.AddRange(restored); nextId = restoredNextId;
        }

        public Cell PrepareCamp()
        {
            // Prefer a large connected landmass, not a visually attractive isolated pocket.
            var componentSize = new int[World.Tiles.Length]; var seen = new bool[World.Tiles.Length];
            for (int i = 0; i < World.Tiles.Length; i++)
            {
                if (seen[i] || !World.Tiles[i].Walkable) continue;
                var members = new List<int>(); var pending = new Queue<int>(); pending.Enqueue(i); seen[i] = true;
                while (pending.Count > 0)
                {
                    int id = pending.Dequeue(); members.Add(id); Cell position = World.Position(id);
                    foreach (Cell direction in Cell.Directions)
                    {
                        Cell next = position + direction;
                        if (!World.Walkable(next)) continue;
                        int ni = World.Index(next); if (seen[ni]) continue;
                        seen[ni] = true; pending.Enqueue(ni);
                    }
                }
                foreach (int id in members) componentSize[id] = members.Count;
            }
            Cell center = new Cell(World.Width / 2, World.Height / 2); Cell best = center; int bestScore = int.MinValue;
            for (int y = 5; y < World.Height - 5; y++) for (int x = 5; x < World.Width - 5; x++)
            {
                Cell p = new Cell(x, y); int score = componentSize[World.Index(p)] * 4 - p.Distance(center); bool dry = true;
                for (int dy = -3; dy <= 3; dy++) for (int dx = -3; dx <= 3; dx++)
                { Tile t = World.At(new Cell(x + dx, y + dy)); if (t.Ground == Ground.Water) dry = false; if (t.Walkable) score += 3; }
                if (dry && score > bestScore) { bestScore = score; best = p; }
            }
            for (int y = -3; y <= 3; y++) for (int x = -3; x <= 3; x++)
            {
                Tile t = World.At(new Cell(best.X + x, best.Y + y));
                if (t.Ground == Ground.Water) continue;
                t.Ground = Ground.Soil; t.Scrap = 0;
            }
            Tile cache = World.At(new Cell(best.X + 3, best.Y + 2)); cache.Ground = Ground.Rubble; cache.Scrap = 20;
            return best;
        }

        public Survivor AddPerson(Cell position, string name, bool hostile = false)
        {
            if (!CanOccupy(position, null)) return null;
            var p = new Survivor { Id = nextId++, Name = name, Position = position, Hostile = hostile };
            People.Add(p); return p;
        }
        public Survivor PersonAt(Cell p)
        { foreach (Survivor person in People) if (person.Occupies(p)) return person; return null; }
        public bool CanOccupy(Cell cell, Survivor self)
        {
            if (!World.Walkable(cell)) return false;
            foreach (Survivor p in People) if (p != self && p.Occupies(cell)) return false;
            return true;
        }
        public bool SetProne(Survivor p, bool prone)
        {
            if (p == null || !p.Alive || p.PostureClock > 0) return false;
            if (prone && !CanOccupy(p.Tail, p)) { Message = "No room to lie down behind this survivor."; return false; }
            p.Prone = prone; p.PostureClock = .65f; p.Path.Clear(); p.SalvageTile = -1; return true;
        }
        public bool Rotate(Survivor p, int direction)
        {
            if (p == null || !p.Alive || p.PostureClock > 0) return false;
            direction = (direction % 4 + 4) % 4;
            Cell tail = p.Position + Cell.Directions[(direction + 2) % 4];
            if (p.Prone && !CanOccupy(tail, p)) { Message = "Rotation blocked."; return false; }
            p.Facing = direction; if (p.Prone) p.PostureClock = .3f; return true;
        }

        public List<Cell> FindPath(Cell start, Cell goal, Survivor self)
        {
            if (!World.Contains(start) || !CanOccupy(goal, self)) return null;
            int from = World.Index(start), to = World.Index(goal), count = World.Tiles.Length;
            var cost = new int[count]; var parent = new int[count]; var closed = new bool[count];
            for (int i = 0; i < count; i++) { cost[i] = int.MaxValue; parent[i] = -1; }
            cost[from] = 0; var heap = new MinHeap(); heap.Push(from, start.Distance(goal));
            while (heap.Count > 0)
            {
                int id = heap.Pop(); if (closed[id]) continue; closed[id] = true;
                if (id == to)
                {
                    var path = new List<Cell>();
                    while (id != from) { path.Add(World.Position(id)); id = parent[id]; }
                    path.Reverse(); return path;
                }
                Cell p = World.Position(id);
                foreach (Cell d in Cell.Directions)
                {
                    Cell next = p + d; if (!CanOccupy(next, self)) continue; int ni = World.Index(next);
                    int value = cost[id] + (World.At(next).Ground == Ground.Rubble ? 2 : 1);
                    if (value >= cost[ni]) continue;
                    parent[ni] = id; cost[ni] = value; heap.Push(ni, value + next.Distance(goal));
                }
            }
            return null;
        }
        public bool Move(Survivor p, Cell goal)
        {
            if (p == null || !p.Alive) return false;
            if (p.Prone) { Message = "Stand up before moving (P)."; return false; }
            List<Cell> path = FindPath(p.Position, goal, p);
            if (path == null) { Message = "No reachable route."; return false; }
            p.Path = new Queue<Cell>(path); p.Target = -1; p.SalvageTile = -1; return true;
        }
        public bool Salvage(Survivor p, Cell tile)
        {
            if (p == null || !p.Alive || p.Prone || !World.Contains(tile) || World.At(tile).Scrap <= 0) return false;
            List<Cell> best = null;
            foreach (Cell d in Cell.Directions)
            {
                var path = FindPath(p.Position, tile + d, p);
                if (path != null && (best == null || path.Count < best.Count)) best = path;
            }
            if (best == null) { Message = "Cannot reach this scrap pile."; return false; }
            p.Path = new Queue<Cell>(best); p.SalvageTile = World.Index(tile); p.Target = -1; return true;
        }
        public bool Build(Cell cell, Structure structure)
        {
            if (!World.Contains(cell) || PersonAt(cell) != null) return false;
            Tile tile = World.At(cell); int price = structure == Structure.Bridge ? 5 : 3;
            if (structure == Structure.None)
            {
                if (tile.Structure == Structure.None) return false;
                tile.Structure = Structure.None; Scrap++; return true;
            }
            if (Scrap < price) { Message = "Not enough scrap."; return false; }
            if (tile.Structure != Structure.None || tile.Scrap > 0 || (structure == Structure.Bridge ? tile.Ground != Ground.Water : !tile.Walkable))
            { Message = "Invalid building location."; return false; }
            tile.Structure = structure; Scrap -= price; return true;
        }

        public bool HasLineOfSight(Cell from, Cell to)
        {
            // Supercover traversal: a shot cannot slip diagonally between touching walls.
            int dx = to.X - from.X, dy = to.Y - from.Y, nx = Math.Abs(dx), ny = Math.Abs(dy);
            int sx = Math.Sign(dx), sy = Math.Sign(dy), ix = 0, iy = 0; Cell p = from;
            while (ix < nx || iy < ny)
            {
                int decision = (1 + 2 * ix) * ny - (1 + 2 * iy) * nx;
                if (decision == 0)
                {
                    if (BlocksSight(new Cell(p.X + sx, p.Y)) || BlocksSight(new Cell(p.X, p.Y + sy))) return false;
                    p.X += sx; p.Y += sy; ix++; iy++;
                }
                else if (decision < 0) { p.X += sx; ix++; } else { p.Y += sy; iy++; }
                if (BlocksSight(p)) return false;
            }
            return true;
        }
        private bool BlocksSight(Cell p)
        { return !World.Contains(p) || World.At(p).Ground == Ground.Rock || World.At(p).Structure == Structure.Wall; }
        private Survivor ById(int id) { return People.Find(delegate(Survivor p) { return p.Id == id && p.Alive; }); }
        public void Attack(Survivor p, Survivor target)
        {
            if (p == null || target == null || !p.Alive || !target.Alive || p.Hostile == target.Hostile) return;
            p.Target = target.Id; p.Path.Clear(); p.SalvageTile = -1; Message = "Target: " + target.Name + ". Move closer if out of range.";
        }
        public bool SpawnRaider(Cell near)
        {
            Survivor friendly = People.Find(delegate(Survivor p) { return p.Alive && !p.Hostile; });
            if (friendly == null) return false;
            // Flood from the colony so raids never spawn in isolated mountain pockets.
            var visited = new HashSet<Cell>(); var queue = new Queue<Cell>(); var choices = new List<Cell>();
            queue.Enqueue(near); visited.Add(near);
            while (queue.Count > 0)
            {
                Cell p = queue.Dequeue(); int distance = p.Distance(near);
                if (distance >= 20 && distance <= 32 && CanOccupy(p, null)) choices.Add(p);
                foreach (Cell d in Cell.Directions)
                { Cell next = p + d; if (World.Walkable(next) && visited.Add(next)) queue.Enqueue(next); }
            }
            if (choices.Count == 0) { Message = "No reachable raid spawn. Open a passage first."; return false; }
            Survivor raider = AddPerson(choices[random.Next(choices.Count)], "Raider " + nextId, true);
            raider.Weapon = Weapon.Bow; raider.Ammo = 50; Message = "A raider is approaching the camp."; return true;
        }

        public void Tick(float delta)
        {
            for (int i = Shots.Count - 1; i >= 0; i--) { Shots[i].Life -= delta; if (Shots[i].Life <= 0) Shots.RemoveAt(i); }
            foreach (Survivor p in People)
            {
                if (!p.Alive) continue;
                p.Cooldown = Math.Max(0, p.Cooldown - delta); p.PostureClock = Math.Max(0, p.PostureClock - delta);
                if (p.PostureClock > 0) continue;
                if (p.Hostile) UpdateRaider(p);
                if (p.Path.Count > 0 && !p.Prone)
                {
                    p.MoveClock += delta;
                    if (p.MoveClock >= .18f)
                    {
                        p.MoveClock = 0; Cell next = p.Path.Peek();
                        if (CanOccupy(next, p))
                        {
                            for (int d = 0; d < 4; d++) if ((p.Position + Cell.Directions[d]).Equals(next)) p.Facing = d;
                            p.Position = p.Path.Dequeue();
                        }
                        else { p.Path.Clear(); p.SalvageTile = -1; if (!p.Hostile) Message = "Route blocked; give a new move order."; }
                    }
                }
                else
                {
                    p.MoveClock = 0;
                    if (p.SalvageTile >= 0)
                    {
                        Cell site = World.Position(p.SalvageTile);
                        if (p.Position.Distance(site) == 1 && p.Cooldown <= 0)
                        {
                            Tile tile = World.At(site); int amount = Math.Min(2, tile.Scrap); tile.Scrap -= amount; Scrap += amount;
                            p.Cooldown = .6f; if (tile.Scrap == 0) { tile.Ground = Ground.Soil; p.SalvageTile = -1; Message = "Scrap recovered."; }
                        }
                    }
                    Survivor target = ById(p.Target);
                    if (target != null) Fire(p, target);
                }
            }
        }
        private void UpdateRaider(Survivor p)
        {
            Survivor target = null; int best = int.MaxValue;
            foreach (Survivor other in People)
            { int distance = p.Position.Distance(other.Position); if (other.Alive && !other.Hostile && distance < best) { best = distance; target = other; } }
            if (target == null) { p.Path.Clear(); p.Target = -1; return; }
            if (p.Ammo == 0) p.Weapon = Weapon.Knife;
            p.Target = target.Id;
            if (Distance(p.Position, target.Position) <= WeaponSpec.For(p.Weapon).Range && HasLineOfSight(p.Position, target.Position)) { p.Path.Clear(); return; }
            if (p.Path.Count > 0 || p.Cooldown > 0) return;
            foreach (Cell d in Cell.Directions)
            { List<Cell> path = FindPath(p.Position, target.Position + d, p); if (path != null) { p.Path = new Queue<Cell>(path); break; } }
            p.Cooldown = .5f;
        }
        private static float Distance(Cell a, Cell b)
        { float x = a.X - b.X, y = a.Y - b.Y; return (float)Math.Sqrt(x * x + y * y); }
        private void Fire(Survivor p, Survivor target)
        {
            if (p.Cooldown > 0 || p.Weapon == Weapon.Knife && p.Prone) return;
            WeaponSpec spec = WeaponSpec.For(p.Weapon);
            Cell aim = target.Position;
            if (target.Prone && (Distance(p.Position, target.Tail) < Distance(p.Position, aim) || !HasLineOfSight(p.Position, aim))) aim = target.Tail;
            float distance = Distance(p.Position, aim);
            if (distance > spec.Range || !HasLineOfSight(p.Position, aim)) return;
            if (p.Weapon != Weapon.Knife && p.Ammo <= 0) { if (!p.Hostile) Message = "Out of ammunition. Switch to knife or resupply."; return; }
            int dx = aim.X - p.Position.X, dy = aim.Y - p.Position.Y;
            int facing = Math.Abs(dx) > Math.Abs(dy) ? (dx > 0 ? 1 : 3) : (dy > 0 ? 0 : 2);
            if (p.Facing != facing) { if (!Rotate(p, facing)) return; if (p.Prone) return; }
            p.Cooldown = spec.Interval; if (p.Weapon != Weapon.Knife) p.Ammo--;
            float chance = spec.Accuracy + (p.Prone ? .13f : 0) - distance / spec.Range * .12f;
            if (target.Prone && p.Weapon != Weapon.Knife) chance -= .23f;
            if (World.At(aim).Ground == Ground.Rubble && p.Weapon != Weapon.Knife) chance -= .12f;
            bool hit = random.NextDouble() < chance;
            Shots.Add(new Shot { From = p.Position, To = aim, Hit = hit, Hostile = p.Hostile });
            if (!hit) return;
            int damage = spec.Damage;
            if (p.Weapon == Weapon.Shotgun) damage = (int)(damage * (1 - .55f * distance / spec.Range));
            target.Health = Math.Max(0, target.Health - damage);
            if (!target.Alive)
            {
                target.Path.Clear(); target.Prone = false;
                if (target.Hostile) { Kills++; Scrap += 8; }
                Message = target.Name + " was killed.";
            }
        }
    }
}
