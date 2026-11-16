using System;
using System.IO;
using UnityEngine;

namespace Ashfall
{
    // All presentation assets are generated locally. No downloaded art, fonts or audio.
    public sealed class AshfallGame : MonoBehaviour
    {
        private Simulation sim;
        private Survivor selected;
        private Texture2D mapTexture, personTexture, proneTexture;
        private GUIStyle title, heading, body, small, button;
        private Cell camp;
        private string seedText = "2049";
        private int sourceCount = 5, tool, speed = 1;
        private float sourceFlow = 3, zoom = 1, mapRefresh;
        private Vector2 pan;
        private Rect viewport, mapRect;
        private bool paused, showHelp = true;
        private Color ink = new Color(.82f, .86f, .82f);
        private readonly Color accent = new Color(.91f, .65f, .34f);
        private readonly Color mint = new Color(.46f, .82f, .69f);
        private string savePath;
        private float uiScale = 1;
        private float CanvasWidth { get { return Screen.width / uiScale; } }
        private float CanvasHeight { get { return Screen.height / uiScale; } }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindAnyObjectByType<AshfallGame>() == null) new GameObject("Ashfall Prototype").AddComponent<AshfallGame>();
        }
        private void Awake()
        {
            Application.targetFrameRate = 60;
            var cameraObject = new GameObject("Overview Camera");
            var camera = cameraObject.AddComponent<Camera>(); camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.035f, .055f, .06f); camera.orthographic = true;
            camera.transform.position = new Vector3(0, 0, -10);
            savePath = Path.Combine(Application.persistentDataPath, "ashfall-save.json");
            personTexture = MakePerson(false); proneTexture = MakePerson(true);
            NewWorld(2049);
        }
        private void NewWorld(int seed)
        {
            sim = new Simulation(WorldGenerator.Generate(seed, 96, 72, sourceCount, sourceFlow));
            camp = sim.PrepareCamp();
            selected = sim.AddPerson(camp, "MARA");
            sim.AddPerson(camp + new Cell(2, 0), "ELI").Weapon = Weapon.Shotgun;
            sim.AddPerson(camp + new Cell(0, 2), "JUN").Weapon = Weapon.Bow;
            paused = false; tool = 0; pan = Vector2.zero; zoom = 1; seedText = seed.ToString();
            RefreshMap();
        }
        private void Update()
        {
            if (sim == null) return;
            if (!paused && !showHelp) sim.Tick(Mathf.Min(Time.deltaTime, .1f) * speed);
            mapRefresh += Time.unscaledDeltaTime;
            if (mapRefresh > .5f) { mapRefresh = 0; RefreshMap(); }
        }
        private void OnDestroy()
        {
            if (mapTexture != null) Destroy(mapTexture);
            if (personTexture != null) Destroy(personTexture);
            if (proneTexture != null) Destroy(proneTexture);
        }
        private void Styles()
        {
            if (body != null) return;
            body = new GUIStyle(GUI.skin.label) { fontSize = 13, wordWrap = true };
            body.normal.textColor = ink;
            small = new GUIStyle(body) { fontSize = 11 };
            small.normal.textColor = new Color(.54f, .64f, .63f);
            title = new GUIStyle(body) { fontSize = 29, fontStyle = FontStyle.Bold };
            heading = new GUIStyle(body) { fontSize = 14, fontStyle = FontStyle.Bold };
            heading.normal.textColor = accent;
            button = new GUIStyle(GUI.skin.button) { fontSize = 12, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(10, 5, 3, 3) };
            button.normal.textColor = ink;
        }
        private void OnGUI()
        {
            if (sim == null) return;
            uiScale = Mathf.Min(1, Mathf.Min(Screen.width / 1100f, Screen.height / 760f));
            GUI.matrix = Matrix4x4.Scale(new Vector3(uiScale, uiScale, 1));
            Styles(); float w = CanvasWidth, h = CanvasHeight;
            viewport = new Rect(274, 88, w - 292, h - 152);
            float unit = Mathf.Min(viewport.width / sim.World.Width, viewport.height / sim.World.Height) * zoom;
            mapRect = new Rect((viewport.width - sim.World.Width * unit) / 2 + pan.x,
                (viewport.height - sim.World.Height * unit) / 2 + pan.y, sim.World.Width * unit, sim.World.Height * unit);
            HandleInput(unit);
            Fill(new Rect(0, 0, w, 74), new Color(.055f, .075f, .078f));
            GUI.Label(new Rect(24, 10, 245, 36), "ASHFALL", title);
            GUI.Label(new Rect(26, 47, 250, 20), "A POST-NUCLEAR COLONY", small);
            GUI.Label(new Rect(290, 17, 350, 25), "DAY 01  /  THE RIVERLANDS", heading);
            GUI.Label(new Rect(290, 43, 420, 20), "SEED " + sim.World.Seed + "   /   " + sim.World.Sources.Count + " HEADWATERS   /   " + AliveCount() + " SURVIVORS", small);
            GUI.Label(new Rect(w - 330, 17, 190, 22), "SCRAP  " + sim.Scrap + "    KILLS  " + sim.Kills, heading);
            if (Button(new Rect(w - 130, 16, 108, 30), paused ? "RESUME" : "PAUSE", paused)) paused = !paused;
            DrawSidebar(h);
            Fill(viewport, new Color(.035f, .05f, .052f));
            GUI.BeginGroup(viewport);
            GUI.DrawTexture(mapRect, mapTexture, ScaleMode.StretchToFill);
            if (unit > 15)
            {
                for (int x = 0; x <= sim.World.Width; x++) Fill(new Rect(mapRect.x + x * unit, mapRect.y, 1, mapRect.height), new Color(0, 0, 0, .12f));
                for (int y = 0; y <= sim.World.Height; y++) Fill(new Rect(mapRect.x, mapRect.y + y * unit, mapRect.width, 1), new Color(0, 0, 0, .12f));
            }
            DrawPeople(unit);
            foreach (Shot shot in sim.Shots)
            {
                Vector2 a = CellRect(shot.From, unit).center, b = CellRect(shot.To, unit).center;
                DrawLine(a, b, shot.Hostile ? new Color(1, .4f, .25f) : new Color(1, .87f, .48f), 2);
            }
            Vector2 mouse = Event.current.mousePosition;
            Cell hover = ScreenCell(mouse, unit, false);
            if (sim.World.Contains(hover)) Outline(CellRect(hover, unit), tool == 0 ? new Color(1, 1, 1, .5f) : accent, 1);
            GUI.EndGroup();
            Fill(new Rect(274, h - 56, w - 292, 40), new Color(.075f, .10f, .105f));
            GUI.Label(new Rect(287, h - 46, w - 320, 26), sim.Message, body);
            if (showHelp) DrawHelp(w, h);
            GUI.matrix = Matrix4x4.identity;
        }
        private int AliveCount() { return sim.People.FindAll(p => p.Alive && !p.Hostile).Count; }
        private void DrawSidebar(float h)
        {
            Fill(new Rect(14, 88, 246, h - 104), new Color(.065f, .086f, .089f));
            float y = 102;
            GUI.Label(new Rect(28, y, 210, 22), "COLONY / 01", heading); y += 28;
            foreach (Survivor p in sim.People)
            {
                if (p.Hostile) continue;
                if (Button(new Rect(26, y, 222, 30), p.Name + "    " + (p.Alive ? p.Health + " HP" + (p.Prone ? "  / PRONE" : "") : "DEAD"), selected == p)) selected = p;
                y += 35;
            }
            y += 10;
            if (selected != null && selected.Alive)
            {
                GUI.Label(new Rect(28, y, 212, 22), "ORDERS / " + selected.Name, heading); y += 27;
                if (Button(new Rect(26, y, 141, 29), selected.Prone ? "P  Stand up" : "P  Lie down", selected.Prone)) sim.SetProne(selected, !selected.Prone);
                if (Button(new Rect(173, y, 75, 29), "R  Turn")) sim.Rotate(selected, selected.Facing + 1); y += 35;
                string[] names = { "1  Knife", "2  Bow", "3  Rifle", "4  Shotgun" };
                for (int i = 0; i < 4; i++)
                    if (Button(new Rect(26 + i % 2 * 114, y + i / 2 * 32, 108, 28), names[i], (int)selected.Weapon == i)) selected.Weapon = (Weapon)i;
                y += 69;
                GUI.Label(new Rect(28, y, 210, 22), "AMMO " + selected.Ammo + "  /  RANGE " + WeaponSpec.For(selected.Weapon).Range, small); y += 24;
                if (Button(new Rect(26, y, 222, 28), "Resupply  +12 ammo / 6 scrap"))
                { if (sim.Scrap >= 6) { sim.Scrap -= 6; selected.Ammo += 12; } else sim.Message = "Need 6 scrap to resupply."; }
                y += 39;
            }
            GUI.Label(new Rect(28, y, 210, 22), "CONSTRUCTION", heading); y += 27;
            string[] build = { "Select / orders", "Wall / 3 scrap", "Bridge / 5 scrap", "Dismantle / +1 scrap" };
            for (int i = 0; i < 4; i++)
            { if (Button(new Rect(26, y, 222, 27), build[i], tool == i)) tool = i; y += 31; }
            y += 9;
            if (Button(new Rect(26, y, 106, 28), "Call raider")) sim.SpawnRaider(selected != null && selected.Alive ? selected.Position : camp);
            if (Button(new Rect(138, y, 110, 28), "Speed " + speed + "x")) speed = speed == 1 ? 2 : speed == 2 ? 4 : 1;
            y += 34;
            if (Button(new Rect(26, y, 106, 28), "Save")) Save();
            if (Button(new Rect(138, y, 110, 28), "Load")) Load(); y += 34;
            if (Button(new Rect(26, y, 222, 28), "Help / new world")) showHelp = true;
        }
        private void DrawHelp(float w, float h)
        {
            Fill(new Rect(0, 0, w, h), new Color(0, 0, 0, .82f));
            Rect box = new Rect((w - 680) / 2, (h - 590) / 2, 680, 590);
            Fill(box, new Color(.07f, .095f, .10f)); Outline(box, new Color(.28f, .37f, .35f), 1);
            float x = box.x + 32, y = box.y + 24;
            GUI.Label(new Rect(x, y, 620, 40), "BUILD AFTER THE END", title); y += 45;
            GUI.Label(new Rect(x, y, 610, 44), "The war is over. Three survivors reach a mountain watershed. Recover the ruins and build a place worth defending.", body); y += 58;
            GUI.Label(new Rect(x, y, 600, 24), "FIELD MANUAL", heading); y += 30;
            GUI.Label(new Rect(x, y, 610, 174),
                "LEFT CLICK   Select a survivor / place selected structure\n" +
                "RIGHT CLICK   Move / salvage rubble / attack a hostile\n" +
                "P   Stand or lie down     R   Rotate clockwise     1-4   Weapons\n" +
                "SPACE   Pause     ESC   Cancel tool / close this panel\n" +
                "WHEEL   Zoom     MIDDLE DRAG   Pan     HOME   Reset view\n\n" +
                "Standing: 1 tile. Prone: 2 tiles, stationary fire, better accuracy.\n" +
                "Rivers and rock block movement. Bridges cross water.\n" +
                "Walls and rock stop shots. Rubble provides light cover.", body); y += 184;
            GUI.Label(new Rect(x, y, 600, 22), "NEW WORLD / replaces current unsaved progress", heading); y += 30;
            GUI.Label(new Rect(x, y + 4, 45, 22), "Seed", body);
            seedText = GUI.TextField(new Rect(x + 45, y, 124, 28), seedText, 11);
            if (Button(new Rect(x + 179, y, 98, 28), "Random")) seedText = UnityEngine.Random.Range(1, int.MaxValue).ToString();
            GUI.Label(new Rect(x + 300, y + 4, 280, 22), "Headwaters: " + sourceCount + "  |  Flow: " + sourceFlow.ToString("0"), body); y += 37;
            sourceCount = Mathf.RoundToInt(GUI.HorizontalSlider(new Rect(x + 300, y, 125, 20), sourceCount, 1, 8));
            sourceFlow = Mathf.Round(GUI.HorizontalSlider(new Rect(x + 450, y, 125, 20), sourceFlow, 1, 12));
            GUI.Label(new Rect(x, y - 1, 270, 25), "1 source = a single main river", small); y += 39;
            if (Button(new Rect(x, y, 276, 38), "GENERATE WORLD"))
            { int seed; if (int.TryParse(seedText, out seed)) { NewWorld(seed); showHelp = false; } else sim.Message = "Seed must be a 32-bit integer."; }
            if (Button(new Rect(x + 292, y, 314, 38), "CONTINUE TO COLONY")) showHelp = false;
            GUI.Label(new Rect(x, y + 48, 610, 35), "Prototype: instant construction, shared ammunition, manual raids. No hunger, radiation or underground layers yet.", small);
        }
        private void HandleInput(float unit)
        {
            Event e = Event.current;
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape) { showHelp = false; tool = 0; e.Use(); return; }
            if (showHelp) return;
            if (e.type == EventType.KeyDown)
            {
                switch (e.keyCode)
                {
                    case KeyCode.Space: paused = !paused; break;
                    case KeyCode.P: if (selected != null) sim.SetProne(selected, !selected.Prone); break;
                    case KeyCode.R: if (selected != null) sim.Rotate(selected, selected.Facing + 1); break;
                    case KeyCode.Home: pan = Vector2.zero; zoom = 1; break;
                    case KeyCode.Alpha1: if (selected != null) selected.Weapon = Weapon.Knife; break;
                    case KeyCode.Alpha2: if (selected != null) selected.Weapon = Weapon.Bow; break;
                    case KeyCode.Alpha3: if (selected != null) selected.Weapon = Weapon.Rifle; break;
                    case KeyCode.Alpha4: if (selected != null) selected.Weapon = Weapon.Shotgun; break;
                }
                e.Use();
            }
            if (!viewport.Contains(e.mousePosition)) return;
            if (e.type == EventType.ScrollWheel)
            {
                float previous = zoom; zoom = Mathf.Clamp(zoom * (1 - e.delta.y * .07f), 1, 5);
                Vector2 anchor = e.mousePosition - viewport.center;
                pan = anchor - (anchor - pan) * (zoom / previous); e.Use();
            }
            if (e.type == EventType.MouseDrag && e.button == 2) { pan += e.delta; e.Use(); }
            if (e.type != EventType.MouseDown) return;
            Cell cell = ScreenCell(e.mousePosition, unit, true); if (!sim.World.Contains(cell)) return;
            Survivor person = sim.PersonAt(cell);
            if (e.button == 0)
            {
                if (tool != 0) { sim.Build(cell, tool == 1 ? Structure.Wall : tool == 2 ? Structure.Bridge : Structure.None); RefreshMap(); }
                else if (person != null && !person.Hostile) selected = person;
            }
            if (e.button == 1 && selected != null && selected.Alive)
            {
                tool = 0;
                if (person != null && person.Hostile) sim.Attack(selected, person);
                else if (sim.World.At(cell).Scrap > 0) sim.Salvage(selected, cell);
                else sim.Move(selected, cell);
            }
            e.Use();
        }
        private Cell ScreenCell(Vector2 mouse, float unit, bool global)
        {
            if (global) mouse -= viewport.position;
            return new Cell(Mathf.FloorToInt((mouse.x - mapRect.x) / unit), sim.World.Height - 1 - Mathf.FloorToInt((mouse.y - mapRect.y) / unit));
        }
        private Rect CellRect(Cell p, float unit)
        { return new Rect(mapRect.x + p.X * unit, mapRect.y + (sim.World.Height - 1 - p.Y) * unit, unit, unit); }
        private void DrawPeople(float unit)
        {
            if (selected != null && selected.Alive)
            {
                foreach (Cell cell in selected.Path)
                { Rect r = CellRect(cell, unit); Fill(new Rect(r.center.x - 1.5f, r.center.y - 1.5f, 3, 3), mint); }
            }
            foreach (Survivor p in sim.People)
            {
                Rect r = CellRect(p.Position, unit);
                if (!p.Alive) { DrawLine(r.position, r.position + r.size, new Color(.5f, .2f, .15f), 2); continue; }
                Color color = p.Hostile ? new Color(.91f, .39f, .27f) : mint;
                if (p == selected)
                {
                    Fill(r, new Color(.5f, 1, .8f, .17f)); Outline(r, color, 2);
                    if (p.Prone) { Fill(CellRect(p.Tail, unit), new Color(.5f, 1, .8f, .17f)); Outline(CellRect(p.Tail, unit), color, 2); }
                }
                Matrix4x4 previous = GUI.matrix;
                GUIUtility.RotateAroundPivot(p.Facing * 90, r.center);
                GUI.color = color;
                GUI.DrawTexture(p.Prone ? new Rect(r.x, r.y, unit, unit * 2) : r, p.Prone ? proneTexture : personTexture);
                GUI.color = Color.white; GUI.matrix = previous;
                Fill(new Rect(r.x, r.y - 4, unit, 2), new Color(.1f, .12f, .13f));
                Fill(new Rect(r.x, r.y - 4, unit * p.Health / 100f, 2), color);
                if (unit >= 15 || p == selected) GUI.Label(new Rect(r.x - 6, r.y - 20, 85, 16), p.Name, small);
            }
        }
        private static void Fill(Rect r, Color color)
        { GUI.color = color; GUI.DrawTexture(r, Texture2D.whiteTexture); GUI.color = Color.white; }
        private static void Outline(Rect r, Color color, float thickness)
        {
            Fill(new Rect(r.x, r.y, r.width, thickness), color); Fill(new Rect(r.x, r.yMax - thickness, r.width, thickness), color);
            Fill(new Rect(r.x, r.y, thickness, r.height), color); Fill(new Rect(r.xMax - thickness, r.y, thickness, r.height), color);
        }
        private static void DrawLine(Vector2 from, Vector2 to, Color color, float thickness)
        {
            Matrix4x4 previous = GUI.matrix; Vector2 d = to - from;
            GUIUtility.RotateAroundPivot(Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg, from);
            Fill(new Rect(from.x, from.y - thickness / 2, d.magnitude, thickness), color); GUI.matrix = previous;
        }
        private bool Button(Rect rect, string text, bool active = false)
        {
            GUI.backgroundColor = active ? new Color(.32f, .61f, .49f) : new Color(.23f, .30f, .30f);
            bool clicked = GUI.Button(rect, text, button); GUI.backgroundColor = Color.white; return clicked;
        }

        private static Texture2D MakePerson(bool prone)
        {
            int width = 12, height = prone ? 24 : 12;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            var pixels = new Color[width * height];
            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
            {
                bool head = y >= height - 5 && y <= height - 2 && x >= 4 && x <= 7;
                bool torso = y >= (prone ? 8 : 2) && y < height - 5 && x >= 2 && x <= 9;
                bool legs = prone && y >= 1 && y < 9 && (x >= 3 && x <= 4 || x >= 7 && x <= 8);
                pixels[y * width + x] = head ? new Color(1, .92f, .78f) : torso ? Color.white : legs ? new Color(.5f, .55f, .5f) : Color.clear;
            }
            texture.SetPixels(pixels); texture.Apply(); return texture;
        }
        private void RefreshMap()
        {
            const int pixelsPerTile = 8;
            int width = sim.World.Width * pixelsPerTile, height = sim.World.Height * pixelsPerTile;
            if (mapTexture == null) mapTexture = new Texture2D(width, height, TextureFormat.RGB24, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[width * height];
            for (int i = 0; i < sim.World.Tiles.Length; i++)
            {
                Tile tile = sim.World.Tiles[i]; Cell p = sim.World.Position(i); Color baseColor;
                switch (tile.Ground)
                {
                    case Ground.Water: baseColor = new Color(.10f, .31f, .37f); break;
                    case Ground.Rock: baseColor = new Color(.34f, .37f, .34f); break;
                    case Ground.Grass: baseColor = new Color(.28f, .34f, .22f); break;
                    case Ground.Rubble: baseColor = new Color(.39f, .34f, .28f); break;
                    default: baseColor = new Color(.37f, .36f, .27f); break;
                }
                float elevation = Mathf.Clamp(tile.Height, 0, 1);
                for (int y = 0; y < pixelsPerTile; y++) for (int x = 0; x < pixelsPerTile; x++)
                {
                    uint hash = unchecked((uint)(i * 73856093 ^ x * 19349663 ^ y * 83492791 ^ sim.World.Seed));
                    float grain = (hash % 13 - 6f) * .004f; Color c = baseColor * (.84f + elevation * .3f) + new Color(grain, grain, grain, 0);
                    if (tile.Ground == Ground.Rock)
                    { if (y == 6 || x == 1) c *= 1.22f; if (y == 0 || x == 7) c *= .68f; }
                    if (tile.Ground == Ground.Water && (x + y + p.X) % 7 == 0) c *= 1.25f;
                    if (tile.Scrap > 0 && (x == 2 && y >= 2 && y < 6 || y == 4 && x >= 3 && x < 6)) c = new Color(.66f, .52f, .33f);
                    if (tile.Structure == Structure.Wall) c = y == 7 ? new Color(.76f, .72f, .59f) : y == 0 || x == 0 ? new Color(.19f, .23f, .22f) : new Color(.52f, .52f, .44f);
                    if (tile.Structure == Structure.Bridge) c = y % 3 == 0 ? new Color(.32f, .25f, .17f) : new Color(.65f, .49f, .30f);
                    pixels[(p.Y * pixelsPerTile + y) * width + p.X * pixelsPerTile + x] = c;
                }
            }
            mapTexture.SetPixels32(pixels); mapTexture.Apply();
        }

        [Serializable]
        private sealed class SaveData
        {
            public int Version = 1, Scrap, Kills, SelectedId;
            public World World;
            public Survivor[] People;
            public Cell Camp;
        }
        private void Save()
        {
            try
            {
                var data = new SaveData { World = sim.World, People = sim.People.ToArray(), Scrap = sim.Scrap, Kills = sim.Kills, Camp = camp, SelectedId = selected == null ? -1 : selected.Id };
                string temporary = savePath + ".tmp";
                File.WriteAllText(temporary, JsonUtility.ToJson(data, true));
                if (File.Exists(savePath)) File.Copy(savePath, savePath + ".bak", true);
                File.Copy(temporary, savePath, true); File.Delete(temporary);
                sim.Message = "Colony saved. Orders are cleared when loading.";
            }
            catch (Exception ex) { sim.Message = "Save failed: " + ex.Message; }
        }
        private void Load()
        {
            try
            {
                if (!File.Exists(savePath)) { sim.Message = "No saved colony yet."; return; }
                SaveData data = JsonUtility.FromJson<SaveData>(File.ReadAllText(savePath));
                if (data == null || data.Version != 1 || data.World == null || data.World.Width != 96 || data.World.Height != 72 ||
                    data.World.Tiles == null || data.World.Tiles.Length != 96 * 72 || data.People == null || data.People.Length > 1000)
                    throw new InvalidDataException("Unsupported save format.");
                foreach (Tile tile in data.World.Tiles)
                    if (tile == null || (int)tile.Ground < 0 || (int)tile.Ground > 4 || (int)tile.Structure < 0 || (int)tile.Structure > 2 ||
                        tile.Scrap < 0 || float.IsNaN(tile.Height) || float.IsInfinity(tile.Height)) throw new InvalidDataException("Invalid terrain.");
                var loaded = new Simulation(data.World) { Scrap = Math.Max(0, data.Scrap), Kills = Math.Max(0, data.Kills) };
                loaded.RestorePeople(data.People);
                if (!data.World.Contains(data.Camp)) throw new InvalidDataException("Invalid camp position.");
                sim = loaded; camp = data.Camp; selected = sim.People.Find(p => p.Id == data.SelectedId && !p.Hostile);
                if (selected == null) selected = sim.People.Find(p => !p.Hostile);
                seedText = sim.World.Seed.ToString(); pan = Vector2.zero; zoom = 1; paused = true;
                sim.Message = "Colony loaded and paused. Pending orders were cleared."; RefreshMap();
            }
            catch (Exception ex) { sim.Message = "Load failed: " + ex.Message; }
        }
    }
}
