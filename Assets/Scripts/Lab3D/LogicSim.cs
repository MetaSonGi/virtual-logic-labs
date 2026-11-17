// 3D 회로 실험실 — 회로 데이터, 논리·공유압 시뮬레이션 (저장용 JsonUtility 외 Unity 의존 없음)
using System.Collections.Generic;
using System.Linq;

public enum PartType { Switch, Led, And, Or, Not, Nand, Nor, Xor, Latch, Compressor, Pump, Valve52, Valve32, CylDouble, CylSingle }

[System.Serializable]
public class LPart
{
    public int id;
    public PartType type;
    public int x, z;
    public bool on;
    public string label = "";
    public bool locked;
}

public struct PinRef
{
    public int part;
    public int pin;
    public bool isOut;
    public PinRef(int part, int pin, bool isOut) { this.part = part; this.pin = pin; this.isOut = isOut; }
    public bool Same(PinRef o) => part == o.part && pin == o.pin && isOut == o.isOut;
}

/// <summary>Fluid port of a pneumatic/hydraulic part.</summary>
public struct PortRef
{
    public int part;
    public int port;
    public PortRef(int part, int port) { this.part = part; this.port = port; }
    public bool Same(PortRef o) => part == o.part && port == o.port;
}

[System.Serializable]
public class LWire
{
    public int id;
    public int fromPart, fromPin, toPart, toPin;
}

[System.Serializable]
public class LHose
{
    public int id;
    public int aPart, aPort, bPart, bPort;
}

public class SimResult
{
    public readonly Dictionary<int, int[]> outs = new Dictionary<int, int[]>();
    public readonly Dictionary<int, int[]> ins = new Dictionary<int, int[]>();
    public readonly Dictionary<int, int> wire = new Dictionary<int, int>();
    public bool stable = true;
}

public class FluidResult
{
    public readonly HashSet<long> pressured = new HashSet<long>();
    public readonly HashSet<long> exhaust = new HashSet<long>();
    public readonly Dictionary<int, int> cylDir = new Dictionary<int, int>();
    public readonly Dictionary<int, bool> valveOn = new Dictionary<int, bool>();
    public bool leak;
    public bool oil;
    public bool HasSource;
}

/// <summary>Things that change over time: cylinder stroke (0 retracted … 1 extended) and latch memory.</summary>
public class LabState
{
    public readonly Dictionary<int, float> cyl = new Dictionary<int, float>();
    public readonly Dictionary<int, int> latch = new Dictionary<int, int>();
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
    public List<LHose> hoses = new List<LHose>();
    public int nextId = 1;
}

public class LCircuit
{
    public readonly List<LPart> parts = new List<LPart>();
    public readonly List<LWire> wires = new List<LWire>();
    public readonly List<LHose> hoses = new List<LHose>();
    public int nextId = 1;

    public static readonly PartType[] Gates = { PartType.And, PartType.Or, PartType.Not, PartType.Nand, PartType.Nor, PartType.Xor };

    // ---------------------------------------------------------------- part info
    public static int Inputs(PartType t)
    {
        switch (t)
        {
            case PartType.Switch: case PartType.Compressor: case PartType.Pump: case PartType.CylDouble: case PartType.CylSingle: return 0;
            case PartType.Led: case PartType.Not: case PartType.Valve52: case PartType.Valve32: return 1;
            default: return 2;
        }
    }

    public static int Outputs(PartType t)
    {
        switch (t)
        {
            case PartType.Led: case PartType.Compressor: case PartType.Pump: case PartType.Valve52: case PartType.Valve32: return 0;
            case PartType.CylDouble: case PartType.CylSingle: return 2;
            default: return 1;
        }
    }

    public static readonly string[] NoPorts = new string[0];
    public static string[] Ports(PartType t)
    {
        switch (t)
        {
            case PartType.Compressor: return new[] { "P" };
            case PartType.Pump: return new[] { "P", "T" };
            case PartType.Valve52: return new[] { "P", "A", "B", "R" };
            case PartType.Valve32: return new[] { "P", "A", "R" };
            case PartType.CylDouble: return new[] { "A", "B" };
            case PartType.CylSingle: return new[] { "A" };
            default: return NoPorts;
        }
    }

    public static bool IsGate(PartType t) => t >= PartType.And && t <= PartType.Xor;
    public static bool IsChip(PartType t) => IsGate(t) || t == PartType.Latch;
    public static bool IsFluid(PartType t) => t >= PartType.Compressor;
    public static bool IsCylinder(PartType t) => t == PartType.CylDouble || t == PartType.CylSingle;
    public static bool IsValve(PartType t) => t == PartType.Valve52 || t == PartType.Valve32;

    public static string Name(PartType t)
    {
        switch (t)
        {
            case PartType.Switch: return "스위치";
            case PartType.Led: return "LED";
            case PartType.Latch: return "SR 래치";
            case PartType.Compressor: return "압축기";
            case PartType.Pump: return "유압 펌프";
            case PartType.Valve52: return "5/2 밸브";
            case PartType.Valve32: return "3/2 밸브";
            case PartType.CylDouble: return "복동 실린더";
            case PartType.CylSingle: return "단동 실린더";
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
            case PartType.Latch: return "S 세트 · R 리셋";
            default: return "";
        }
    }

    static int Gate(PartType t, int[] i, bool on)
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

    // ---------------------------------------------------------------- editing
    public LPart Get(int id) => parts.FirstOrDefault(p => p.id == id);

    public LPart Add(PartType t, int x, int z, string label = null, bool locked = false)
    {
        var p = new LPart { id = nextId++, type = t, x = x, z = z, locked = locked };
        if (label != null) p.label = label;
        else if (t == PartType.Switch) p.label = NextLabel(t, new[] { "A", "B", "C", "D", "E", "F", "G", "H" });
        else if (t == PartType.Led) p.label = NextLabel(t, new[] { "Y1", "Y2", "Y3", "Y4", "Y5", "Y6", "Y7", "Y8" });
        else if (IsCylinder(t)) p.label = NextLabelAny(new[] { "C1", "C2", "C3", "C4" });
        parts.Add(p);
        return p;
    }

    string NextLabel(PartType t, string[] pool)
    {
        var used = new HashSet<string>(parts.Where(p => p.type == t).Select(p => p.label));
        return pool.FirstOrDefault(l => !used.Contains(l)) ?? (t == PartType.Switch ? "S" : "Y") + nextId;
    }

    string NextLabelAny(string[] pool)
    {
        var used = new HashSet<string>(parts.Select(p => p.label));
        return pool.FirstOrDefault(l => !used.Contains(l)) ?? "C" + nextId;
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

    /// <summary>Hose between two fluid ports of different parts. A port may take several hoses (T-joint).</summary>
    public LHose ConnectHose(PortRef a, PortRef b)
    {
        if (a.part == b.part) return null;
        bool dup = hoses.Any(h => (h.aPart == a.part && h.aPort == a.port && h.bPart == b.part && h.bPort == b.port)
                               || (h.aPart == b.part && h.aPort == b.port && h.bPart == a.part && h.bPort == a.port));
        if (dup) return null;
        var hose = new LHose { id = nextId++, aPart = a.part, aPort = a.port, bPart = b.part, bPort = b.port };
        hoses.Add(hose);
        return hose;
    }

    public void RemoveWire(int id) => wires.RemoveAll(w => w.id == id);
    public void RemoveHose(int id) => hoses.RemoveAll(h => h.id == id);

    public void RemovePart(int id)
    {
        parts.RemoveAll(p => p.id == id);
        wires.RemoveAll(w => w.fromPart == id || w.toPart == id);
        hoses.RemoveAll(h => h.aPart == id || h.bPart == id);
    }

    // ---------------------------------------------------------------- electric
    /// <summary>Relaxation until stable. Without power every output is 0.
    /// cylPos feeds the cylinder sensors (S0 retracted, S1 extended); latch is read and then updated.</summary>
    public SimResult Simulate(bool power, Dictionary<int, bool> overrides = null, Dictionary<int, float> cylPos = null, Dictionary<int, int> latch = null)
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
                int no = Outputs(p.type);
                if (no == 0) continue;
                var cur = r.outs[p.id];
                for (int k = 0; k < no; k++)
                {
                    int nv = power ? Eval(p, iv, k, overrides, cylPos, latch) : 0;
                    if (cur[k] != nv) { cur[k] = nv; stable = false; }
                }
            }
        }
        r.stable = stable;
        if (latch != null)
            foreach (var p in parts)
                if (p.type == PartType.Latch) latch[p.id] = power ? r.outs[p.id][0] : 0;
        foreach (var w in wires)
            r.wire[w.id] = r.outs.TryGetValue(w.fromPart, out var o) && w.fromPin < o.Length ? o[w.fromPin] : 0;
        return r;
    }

    static int Eval(LPart p, int[] i, int k, Dictionary<int, bool> overrides, Dictionary<int, float> cylPos, Dictionary<int, int> latch)
    {
        switch (p.type)
        {
            case PartType.CylDouble:
            case PartType.CylSingle:
                {
                    float pos = cylPos != null && cylPos.TryGetValue(p.id, out var v) ? v : 0f;
                    return k == 0 ? (pos <= 0.02f ? 1 : 0) : (pos >= 0.98f ? 1 : 0);
                }
            case PartType.Latch:
                {
                    int prev = latch != null && latch.TryGetValue(p.id, out var q) ? q : 0;
                    return i[1] == 1 ? 0 : i[0] == 1 ? 1 : prev;
                }
            default:
                {
                    bool on = overrides != null && overrides.TryGetValue(p.id, out var ov) ? ov : p.on;
                    return Gate(p.type, i, on);
                }
        }
    }

    public static long Key(int part, int pin) => ((long)part << 8) | (uint)pin;

    // ---------------------------------------------------------------- fluid
    public bool IsOil => parts.Any(p => p.type == PartType.Pump);

    /// <summary>Which ports carry supply pressure, which drain (air: valve R ports and open cylinder ports;
    /// oil: the pump's tank T), and which way every cylinder is pushed.</summary>
    public FluidResult Fluid(SimResult sim, bool power)
    {
        var fr = new FluidResult { oil = IsOil };
        var adj = new Dictionary<long, List<long>>();
        void Link(long a, long b)
        {
            if (!adj.TryGetValue(a, out var la)) adj[a] = la = new List<long>();
            if (!adj.TryGetValue(b, out var lb)) adj[b] = lb = new List<long>();
            la.Add(b); lb.Add(a);
        }
        var hosed = new HashSet<long>();
        foreach (var h in hoses)
        {
            long a = Key(h.aPart, h.aPort), b = Key(h.bPart, h.bPort);
            Link(a, b); hosed.Add(a); hosed.Add(b);
        }

        var sources = new List<long>();
        var sinks = new List<long>();
        foreach (var p in parts)
        {
            if (IsValve(p.type))
            {
                bool on = power && sim.ins.TryGetValue(p.id, out var iv) && iv.Length > 0 && iv[0] == 1;
                fr.valveOn[p.id] = on;
                long P = Key(p.id, 0), A = Key(p.id, 1);
                if (p.type == PartType.Valve52)
                {
                    long B = Key(p.id, 2), R = Key(p.id, 3);
                    if (on) { Link(P, A); Link(B, R); } else { Link(P, B); Link(A, R); }
                    if (!fr.oil) sinks.Add(R);
                }
                else
                {
                    long R = Key(p.id, 2);
                    if (on) Link(P, A); else Link(A, R);
                    if (!fr.oil) sinks.Add(R);
                }
            }
            else if (p.type == PartType.Compressor || p.type == PartType.Pump)
            {
                fr.HasSource = true;
                if (power) sources.Add(Key(p.id, 0));
                if (p.type == PartType.Pump) sinks.Add(Key(p.id, 1));
            }
            else if (IsCylinder(p.type) && !fr.oil)
            {
                // an open pneumatic cylinder port just vents to the room
                for (int k = 0; k < Ports(p.type).Length; k++)
                    if (!hosed.Contains(Key(p.id, k))) sinks.Add(Key(p.id, k));
            }
        }

        Fill(fr.pressured, sources, adj);
        Fill(fr.exhaust, sinks, adj);
        fr.leak = fr.pressured.Overlaps(fr.exhaust);

        foreach (var p in parts)
        {
            if (!IsCylinder(p.type)) continue;
            long a = Key(p.id, 0);
            bool pa = fr.pressured.Contains(a) && !fr.exhaust.Contains(a);
            bool ea = fr.exhaust.Contains(a) && !fr.pressured.Contains(a);
            int dir;
            if (p.type == PartType.CylDouble)
            {
                long b = Key(p.id, 1);
                bool pb = fr.pressured.Contains(b) && !fr.exhaust.Contains(b);
                bool eb = fr.exhaust.Contains(b) && !fr.pressured.Contains(b);
                dir = pa && eb ? 1 : pb && ea ? -1 : 0;
            }
            else dir = pa ? 1 : ea ? -1 : 0; // spring return
            fr.cylDir[p.id] = dir;
        }
        return fr;
    }

    static void Fill(HashSet<long> set, List<long> start, Dictionary<long, List<long>> adj)
    {
        var q = new Queue<long>();
        foreach (var s in start) if (set.Add(s)) q.Enqueue(s);
        while (q.Count > 0)
        {
            var n = q.Dequeue();
            if (!adj.TryGetValue(n, out var next)) continue;
            foreach (var m in next) if (set.Add(m)) q.Enqueue(m);
        }
    }

    /// <summary>One time step: logic (with sensors and latches), then valves and cylinders.</summary>
    public SimResult Step(LabState st, bool power, float dt, Dictionary<int, bool> overrides, out FluidResult fr)
    {
        var sim = Simulate(power, overrides, st.cyl, st.latch);
        fr = Fluid(sim, power);
        float speed = fr.oil ? 0.45f : 0.9f;
        foreach (var p in parts)
        {
            if (!IsCylinder(p.type)) continue;
            st.cyl.TryGetValue(p.id, out var pos);
            if (fr.cylDir.TryGetValue(p.id, out var d) && d != 0) pos = System.Math.Max(0f, System.Math.Min(1f, pos + d * speed * dt));
            st.cyl[p.id] = pos;
        }
        return sim;
    }

    // ---------------------------------------------------------------- truth table
    /// <summary>Truth table over the given switches → LEDs, assuming power on.</summary>
    public List<TruthRow> Table(List<LPart> inputs, List<LPart> outputs, out bool stable)
    {
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

    /// <summary>Sandbox table: first 4 switches by label → first 4 LEDs in placement order.</summary>
    public List<TruthRow> Table(out List<LPart> inputs, out List<LPart> outputs, out bool stable)
    {
        inputs = parts.Where(p => p.type == PartType.Switch).OrderBy(p => p.label).Take(4).ToList();
        outputs = parts.Where(p => p.type == PartType.Led).Take(4).ToList();
        return Table(inputs, outputs, out stable);
    }

    public LPart Find(PartType t, string label) => parts.FirstOrDefault(p => p.type == t && p.label == label);

    // ---------------------------------------------------------------- save
    public string ToJson() => UnityEngine.JsonUtility.ToJson(new LCircuitData { parts = parts, wires = wires, hoses = hoses, nextId = nextId });

    public static LCircuit FromJson(string json)
    {
        var d = UnityEngine.JsonUtility.FromJson<LCircuitData>(json);
        if (d == null || d.parts == null) return null;
        var c = new LCircuit();
        c.parts.AddRange(d.parts.Where(p => System.Enum.IsDefined(typeof(PartType), p.type)));
        var ids = new HashSet<int>(c.parts.Select(p => p.id));
        if (d.wires != null) c.wires.AddRange(d.wires.Where(w => ids.Contains(w.fromPart) && ids.Contains(w.toPart)));
        if (d.hoses != null) c.hoses.AddRange(d.hoses.Where(h => ids.Contains(h.aPart) && ids.Contains(h.bPart)));
        c.nextId = d.nextId;
        foreach (var p in c.parts) c.nextId = System.Math.Max(c.nextId, p.id + 1);
        foreach (var w in c.wires) c.nextId = System.Math.Max(c.nextId, w.id + 1);
        foreach (var h in c.hoses) c.nextId = System.Math.Max(c.nextId, h.id + 1);
        return c;
    }

    // ---------------------------------------------------------------- demos
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

    /// <summary>Compressor → 5/2 valve → double-acting cylinder, switch A drives the valve coil.</summary>
    public static LCircuit ExampleFluid()
    {
        var c = new LCircuit();
        var comp = c.Add(PartType.Compressor, -9, -4);
        var a = c.Add(PartType.Switch, -9, 3, "A");
        var v = c.Add(PartType.Valve52, -1, 0);
        var cyl = c.Add(PartType.CylDouble, 7, 0, "C1");
        c.ConnectHose(new PortRef(comp.id, 0), new PortRef(v.id, 0));
        c.ConnectHose(new PortRef(v.id, 1), new PortRef(cyl.id, 0));
        c.ConnectHose(new PortRef(v.id, 2), new PortRef(cyl.id, 1));
        c.Connect(new PinRef(a.id, 0, true), new PinRef(v.id, 0, false));
        return c;
    }
}
