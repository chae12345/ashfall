using System;
using System.Collections.Generic;
using Ashfall;

public static class AshfallCoreChecks
{
    private static int checks;
    private static void Check(bool condition, string message)
    { checks++; if (!condition) throw new Exception(message); }
    private static World Flat()
    {
        var w = new World(30, 30, 12);
        foreach (Tile tile in w.Tiles) tile.Ground = Ground.Soil;
        return w;
    }
    public static void Run()
    {
        checks = 0;
        TestMaps(); TestFootprints(); TestNavigation(); TestCombat(); TestEconomy(); TestRestore();
        Console.WriteLine("PASS: " + checks + " core checks.");
    }
    private static void TestMaps()
    {
        for (int seed = 0; seed < 100; seed++)
        {
            World world = WorldGenerator.Generate(seed); World duplicate = WorldGenerator.Generate(seed);
            Check(world.Sources.Count == 5, "Missing headwaters: " + seed);
            float outletFlow = 0; int water = 0;
            for (int i = 0; i < world.Tiles.Length; i++)
            {
                Tile tile = world.Tiles[i];
                Check(tile.Height == duplicate.Tiles[i].Height && tile.Ground == duplicate.Tiles[i].Ground && tile.Downstream == duplicate.Tiles[i].Downstream, "Seed is not deterministic");
                if (tile.Ground == Ground.Water) water++;
                if (tile.Downstream < 0) outletFlow += tile.Flow;
                else if (tile.Flow > 0)
                {
                    Tile downstream = world.Tiles[tile.Downstream];
                    Check(downstream.Flow >= tile.Flow, "Flow lost downstream");
                    Check(downstream.Height <= tile.Height, "Uphill river");
                    Check(world.Position(i).Distance(world.Position(tile.Downstream)) == 1, "Disconnected stream");
                }
            }
            Check(Math.Abs(outletFlow - 15) < .001, "Flow conservation failed");
            Check(water > 10 && water < world.Tiles.Length / 2, "Invalid wet area");
            foreach (int source in world.Sources)
            {
                int cursor = source; var seen = new HashSet<int>();
                while (cursor >= 0) { Check(seen.Add(cursor), "Drainage cycle"); cursor = world.Tiles[cursor].Downstream; }
            }
            var sim = new Simulation(world); Cell camp = sim.PrepareCamp();
            Check(sim.AddPerson(camp, "A") != null, "Camp spawn blocked");
            Check(sim.AddPerson(camp + new Cell(2, 0), "B") != null, "Second spawn blocked");
            Check(sim.AddPerson(camp + new Cell(0, 2), "C") != null, "Third spawn blocked");
            Check(sim.SpawnRaider(camp), "No reachable raider spawn: " + seed);
        }
        World single = WorldGenerator.Generate(2049, 96, 72, 1, 12);
        Check(single.Sources.Count == 1, "Single river mode");
    }
    private static void TestFootprints()
    {
        var sim = new Simulation(Flat()); var p = sim.AddPerson(new Cell(5, 5), "A");
        Check(p.Occupies(new Cell(5, 5)) && !p.Occupies(new Cell(5, 4)), "Standing must occupy one cell");
        sim.World.At(new Cell(5, 4)).Structure = Structure.Wall;
        Check(!sim.SetProne(p, true), "Prone overlaps wall");
        sim.World.At(new Cell(5, 4)).Structure = Structure.None;
        Check(sim.SetProne(p, true) && p.Occupies(new Cell(5, 4)), "Prone footprint missing");
        Check(!sim.Build(new Cell(5, 4), Structure.Wall), "Build overlaps tail");
        Check(!sim.Move(p, new Cell(6, 6)), "Prone movement must be disabled");
        sim.Tick(.7f);
        sim.AddPerson(new Cell(4, 5), "B");
        Check(!sim.Rotate(p, 1), "Prone rotation overlaps actor");
        Check(p.Facing == 0, "Failed turn mutated facing");
        Check(sim.Rotate(p, 2) && p.Tail.Equals(new Cell(5, 6)), "Prone rotation failed");
        sim.Tick(.7f); Check(sim.SetProne(p, false), "Stand failed");
        Check(!p.Occupies(new Cell(5, 6)), "Standing did not release tail");
        var edge = sim.AddPerson(new Cell(0, 0), "Edge");
        Check(!sim.SetProne(edge, true), "Prone crossed map edge");
    }
    private static void TestNavigation()
    {
        var sim = new Simulation(Flat()); var p = sim.AddPerson(new Cell(2, 2), "A");
        for (int y = 0; y < 30; y++) sim.World.At(new Cell(10, y)).Ground = Ground.Water;
        Check(sim.FindPath(p.Position, new Cell(14, 2), p) == null, "Path crossed river");
        Check(sim.Build(new Cell(10, 2), Structure.Bridge), "Bridge build failed");
        Check(sim.Move(p, new Cell(14, 2)), "Bridge path missing");
        for (int i = 0; i < 20; i++) sim.Tick(.2f);
        Check(p.Position.Equals(new Cell(14, 2)), "Move did not arrive");
        Check(sim.Move(p, new Cell(14, 5)), "Move order failed");
        sim.Build(new Cell(14, 3), Structure.Wall); sim.Tick(.2f);
        Check(p.Position.Equals(new Cell(14, 2)) && p.Path.Count == 0, "Dynamic obstruction ignored");
        sim.World.At(new Cell(20, 20)).Structure = Structure.Wall;
        Check(!sim.HasLineOfSight(new Cell(19, 20), new Cell(20, 21)), "Diagonal wall corner leaked");
    }
    private static void TestCombat()
    {
        var sim = new Simulation(Flat()); var shooter = sim.AddPerson(new Cell(3, 3), "A");
        var target = sim.AddPerson(new Cell(8, 3), "Enemy", true);
        target.Ammo = 0; target.Weapon = Weapon.Knife;
        sim.World.At(new Cell(5, 3)).Structure = Structure.Wall;
        sim.Attack(shooter, target); sim.Tick(.1f);
        Check(shooter.Ammo == 36, "Shot passed through wall");
        sim.World.At(new Cell(5, 3)).Structure = Structure.None;
        target.Path.Clear(); target.PostureClock = 100;
        for (int i = 0; i < 100; i++) sim.Tick(.1f);
        Check(target.Health < 100 && shooter.Ammo < 36, "No combat damage");
        Check(!target.Alive && sim.Kills == 1 && sim.Scrap == 53, "Death rewarded more than once");
        var empty = sim.AddPerson(new Cell(3, 8), "Empty"); empty.Ammo = 0;
        var victim = sim.AddPerson(new Cell(5, 8), "Victim", true); victim.PostureClock = 100;
        sim.Attack(empty, victim); sim.Tick(1);
        Check(victim.Health == 100, "Empty weapon fired");
    }
    private static void TestEconomy()
    {
        var sim = new Simulation(Flat()); var p = sim.AddPerson(new Cell(3, 3), "A");
        Cell site = new Cell(6, 3); sim.World.At(site).Ground = Ground.Rubble; sim.World.At(site).Scrap = 7;
        Check(sim.Salvage(p, site), "Salvage order failed");
        for (int i = 0; i < 50; i++) sim.Tick(.2f);
        Check(sim.Scrap == 52 && sim.World.At(site).Scrap == 0, "Salvage accounting failed");
        Check(sim.Build(new Cell(9, 9), Structure.Wall) && sim.Scrap == 49, "Build price wrong");
        Check(sim.Build(new Cell(9, 9), Structure.None) && sim.Scrap == 50, "Refund wrong");
        Check(!sim.Build(new Cell(9, 9), Structure.None) && sim.Scrap == 50, "Infinite demolition refund");
        sim.Scrap = 0; Check(!sim.Build(new Cell(9, 9), Structure.Wall), "Free wall");
    }
    private static void TestRestore()
    {
        var sim = new Simulation(Flat());
        var dead = new Survivor { Id = 8, Position = new Cell(3, 3), Health = 0 };
        var alive = new Survivor { Id = 12, Position = new Cell(3, 3), Prone = true };
        sim.RestorePeople(new[] { dead, alive });
        Check(sim.People.Count == 2 && sim.PersonAt(new Cell(3, 2)).Id == 12, "Restore corpse overlap or prone footprint failed");
        Check(sim.AddPerson(new Cell(7, 7), "New").Id == 13, "Restored IDs reused");
        Check(sim.People[1].Target == -1 && sim.People[1].Path.Count == 0, "Pending orders survived restore");
        bool rejected = false;
        try { sim.RestorePeople(new[] { alive, new Survivor { Id = 14, Position = new Cell(3, 2) } }); }
        catch (ArgumentException) { rejected = true; }
        Check(rejected && sim.People.Count == 3, "Bad restore must be atomic");
    }
}
