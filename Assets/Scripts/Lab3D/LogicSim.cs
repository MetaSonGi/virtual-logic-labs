// 3D 회로 실험실 — 회로 데이터와 논리 시뮬레이션 (저장용 JsonUtility 외 Unity 의존 없음)
using System.Collections.Generic;
using System.Linq;

public enum PartType { Switch, Led, And, Or, Not, Nand, Nor, Xor }

[System.Serializable]
public class LPart
{
    public int id;
    public PartType type;
    public int x, z;
    public bool on;
    public string label = "";
}

public struct PinRef
{
    public int part;
    public int pin;
    public bool isOut;
    public PinRef(int part, int pin, bool isOut) { this.part = part; this.pin = pin; this.isOut = isOut; }
    public bool Same(PinRef o) => part == o.part && pin == o.pin && isOut == o.isOut;
}

[System.Serializable]
public class LWire
{
    public int id;
    public int fromPart, fromPin, toPart, toPin;
}

public class SimResult
{
    public readonly Dictionary<int, int[]> outs = new Dictionary<int, int[]>();
    public readonly Dictionary<int, int[]> ins = new Dictionary<int, int[]>();
    public readonly Dictionary<int, int> wire = new Dictionary<int, int>();
    public bool stable = true;
}

public class TruthRow
{
    public int[] bits;
    public int[] outs;
    public bool current;
}

[System.Serializable]
public class LCircuitData
{
    public List<LPart> parts = new List<LPart>();
    public List<LWire> wires = new List<LWire>();
    public int nextId = 1;
}

public class LCircuit
{
    public readonly List<LPart> parts = new List<LPart>();
    public readonly List<LWire> wires = new List<LWire>();
    public int nextId = 1;

    public static readonly PartType[] Gates = { PartType.And, PartType.Or, PartType.Not, PartType.Nand, PartType.Nor, PartType.Xor };

    public static int Inputs(PartType t) => t == PartType.Switch ? 0 : (t == PartType.Not || t == PartType.Led) ? 1 : 2;
    public static int Outputs(PartType t) => t == PartType.Led ? 0 : 1;
    public static bool IsGate(PartType t) => t != PartType.Switch && t != PartType.Led;

    public static string Name(PartType t)
    {
        switch (t)
        {
            case PartType.Switch: return "스위치";
            case PartType.Led: return "LED";
            default: return t.ToString().ToUpperInvariant();
        }
    }

    public static string Expr(PartType t)
    {
        switch (t)
        {
            case PartType.And: return "A·B";
            case PartType.Or: return "A+B";
            case PartType.Not: return "NOT A";
            case PartType.Nand: return "(A·B)'";
            case PartType.Nor: return "(A+B)'";
            case PartType.Xor: return "A xor B";
            default: return "";
        }
    }

    static int Eval(PartType t, int[] i, bool on)
    {
        switch (t)
        {
            case PartType.Switch: return on ? 1 : 0;
            case PartType.And: return i[0] & i[1];
            case PartType.Or: return i[0] | i[1];
            case PartType.Not: return i[0] == 1 ? 0 : 1;
            case PartType.Nand: return (i[0] & i[1]) == 1 ? 0 : 1;
            case PartType.Nor: return (i[0] | i[1]) == 1 ? 0 : 1;
            case PartType.Xor: return i[0] ^ i[1];
            default: return 0;
        }
    }

    public LPart Get(int id) => parts.FirstOrDefault(p => p.id == id);

    public LPart Add(PartType t, int x, int z, string label = null)
    {
        var p = new LPart { id = nextId++, type = t, x = x, z = z };
        if (label != null) p.label = label;
        else if (t == PartType.Switch) p.label = NextLabel(t, new[] { "A", "B", "C", "D", "E", "F", "G", "H" });
        else if (t == PartType.Led) p.label = NextLabel(t, new[] { "Y1", "Y2", "Y3", "Y4", "Y5", "Y6", "Y7", "Y8" });
        parts.Add(p);
        return p;
    }

    string NextLabel(PartType t, string[] pool)
    {
        var used = new HashSet<string>(parts.Where(p => p.type == t).Select(p => p.label));
        return pool.FirstOrDefault(l => !used.Contains(l)) ?? (t == PartType.Switch ? "S" : "Y") + nextId;
    }

    /// <summary>Connects an output to an input (any order). An input keeps only its newest wire.</summary>
    public LWire Connect(PinRef a, PinRef b)
    {
        if (a.part == b.part || a.isOut == b.isOut) return null;
        var from = a.isOut ? a : b;
        var to = a.isOut ? b : a;
        wires.RemoveAll(w => w.toPart == to.part && w.toPin == to.pin);
        var wire = new LWire { id = nextId++, fromPart = from.part, fromPin = from.pin, toPart = to.part, toPin = to.pin };
        wires.Add(wire);
        return wire;
    }

    public void RemoveWire(int id) => wires.RemoveAll(w => w.id == id);

    public void RemovePart(int id)
    {
        parts.RemoveAll(p => p.id == id);
        wires.RemoveAll(w => w.fromPart == id || w.toPart == id);
    }

    /// <summary>Relaxation until stable. Without power every output is 0.</summary>
    public SimResult Simulate(bool power, Dictionary<int, bool> overrides = null)
    {
        var r = new SimResult();
        foreach (var p in parts) r.outs[p.id] = new int[Outputs(p.type)];
        var feed = new Dictionary<long, LWire>();
        foreach (var w in wires) feed[Key(w.toPart, w.toPin)] = w;

        int maxIter = parts.Count * 2 + 4;
        bool stable = false;
        for (int iter = 0; iter < maxIter && !stable; iter++)
        {
            stable = true;
            foreach (var p in parts)
            {
                int n = Inputs(p.type);
                var iv = new int[n];
                for (int i = 0; i < n; i++)
                    if (feed.TryGetValue(Key(p.id, i), out var w) && r.outs.TryGetValue(w.fromPart, out var src) && w.fromPin < src.Length)
                        iv[i] = src[w.fromPin];
                r.ins[p.id] = iv;
                if (Outputs(p.type) == 0) continue;
                bool on = overrides != null && overrides.TryGetValue(p.id, out var ov) ? ov : p.on;
                int nv = power ? Eval(p.type, iv, on) : 0;
                if (r.outs[p.id][0] != nv) { r.outs[p.id][0] = nv; stable = false; }
            }
        }
        r.stable = stable;
        foreach (var w in wires)
            r.wire[w.id] = r.outs.TryGetValue(w.fromPart, out var o) && w.fromPin < o.Length ? o[w.fromPin] : 0;
        return r;
    }

    static long Key(int part, int pin) => ((long)part << 8) | (uint)pin;

    /// <summary>Truth table of switches (first 4, by label) → LEDs (placement order, first 4), assuming power on.</summary>
    public List<TruthRow> Table(out List<LPart> inputs, out List<LPart> outputs, out bool stable)
    {
        inputs = parts.Where(p => p.type == PartType.Switch).OrderBy(p => p.label).Take(4).ToList();
        outputs = parts.Where(p => p.type == PartType.Led).Take(4).ToList();
        stable = true;
        var rows = new List<TruthRow>();
        int n = inputs.Count;
        for (int k = 0; k < (1 << n); k++)
        {
            var bits = new int[n];
            var ov = new Dictionary<int, bool>();
            bool current = true;
            for (int i = 0; i < n; i++)
            {
                bits[i] = (k >> (n - 1 - i)) & 1;
                ov[inputs[i].id] = bits[i] == 1;
                if (inputs[i].on != (bits[i] == 1)) current = false;
            }
            var r = Simulate(true, ov);
            if (!r.stable) stable = false;
            rows.Add(new TruthRow { bits = bits, outs = outputs.Select(l => r.ins[l.id][0]).ToArray(), current = current });
        }
        return rows;
    }

    public string ToJson() => UnityEngine.JsonUtility.ToJson(new LCircuitData { parts = parts, wires = wires, nextId = nextId });

    public static LCircuit FromJson(string json)
    {
        var d = UnityEngine.JsonUtility.FromJson<LCircuitData>(json);
        var c = new LCircuit();
        if (d == null || d.parts == null) return null;
        c.parts.AddRange(d.parts);
        var ids = new HashSet<int>(c.parts.Select(p => p.id));
        if (d.wires != null) c.wires.AddRange(d.wires.Where(w => ids.Contains(w.fromPart) && ids.Contains(w.toPart)));
        c.nextId = System.Math.Max(d.nextId, c.parts.Concat<object>(c.wires).Count() + 1);
        foreach (var p in c.parts) c.nextId = System.Math.Max(c.nextId, p.id + 1);
        foreach (var w in c.wires) c.nextId = System.Math.Max(c.nextId, w.id + 1);
        return c;
    }

    /// <summary>Half adder demo.</summary>
    public static LCircuit Example()
    {
        var c = new LCircuit();
        var a = c.Add(PartType.Switch, -8, -3, "A");
        var b = c.Add(PartType.Switch, -8, 3, "B");
        var x = c.Add(PartType.Xor, 0, -3);
        var n = c.Add(PartType.And, 0, 3);
        var s = c.Add(PartType.Led, 8, -3, "S");
        var k = c.Add(PartType.Led, 8, 3, "C");
        c.Connect(new PinRef(a.id, 0, true), new PinRef(x.id, 0, false));
        c.Connect(new PinRef(b.id, 0, true), new PinRef(x.id, 1, false));
        c.Connect(new PinRef(a.id, 0, true), new PinRef(n.id, 0, false));
        c.Connect(new PinRef(b.id, 0, true), new PinRef(n.id, 1, false));
        c.Connect(new PinRef(x.id, 0, true), new PinRef(s.id, 0, false));
        c.Connect(new PinRef(n.id, 0, true), new PinRef(k.id, 0, false));
        a.on = true;
        return c;
    }
}
