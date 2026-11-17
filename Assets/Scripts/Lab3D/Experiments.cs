// 실험 목록 — 논리 회로 · 공압 · 유압 미션과 자동 채점
using System.Collections.Generic;
using System.Linq;

public enum ExpCheck { None, Truth, Fluid, AutoCycle }

public class Experiment
{
    public string id, group, title, goal, desc;
    public PartType[] palette;
    public string[] inputs = new string[0];
    public string[] outputs = new string[0];      // LED labels (Truth) or cylinder labels (Fluid)
    public System.Func<int[], int[]> fn;          // expected LED values, or 1 = extended / 0 = retracted
    public System.Func<LCircuit> setup;
    public ExpCheck check;
    public bool fluid;                             // shows the fluid status panel instead of the truth table
}

public static class Experiments
{
    public static int Current;

    static readonly PartType[] Gates = LCircuit.Gates;
    static readonly PartType[] LogicAll = { PartType.Switch, PartType.Led, PartType.And, PartType.Or, PartType.Not, PartType.Nand, PartType.Nor, PartType.Xor, PartType.Latch };
    static readonly PartType[] FluidAll = { PartType.Switch, PartType.Led, PartType.And, PartType.Or, PartType.Not, PartType.Latch,
        PartType.Compressor, PartType.Pump, PartType.Valve52, PartType.Valve32, PartType.CylDouble, PartType.CylSingle };

    public static readonly Experiment[] All =
    {
        new Experiment { id = "logic-free", group = "논리 회로", title = "논리 회로 자유 실험", goal = "",
            desc = "스위치·LED·게이트·래치로 원하는 회로를 만들어 보세요. 반가산기 예제가 놓여 있어요.",
            palette = LogicAll, setup = LCircuit.Example, check = ExpCheck.None },
        Logic("not", "NOT 게이트", "Y = NOT A", "입력을 뒤집어 보세요. A가 켜지면 Y가 꺼져야 합니다.",
            new[] { "A" }, new[] { "Y" }, Gates, b => new[] { 1 - b[0] }),
        Logic("and", "AND 게이트", "Y = A · B", "A와 B가 모두 켜졌을 때만 Y가 켜지게 연결하세요.",
            new[] { "A", "B" }, new[] { "Y" }, Gates, b => new[] { b[0] & b[1] }),
        Logic("xor", "XOR 직접 만들기", "Y = A xor B (AND·OR·NOT만)", "XOR 칩 없이, 둘 중 하나만 켜졌을 때 Y가 켜지게 만드세요.",
            new[] { "A", "B" }, new[] { "Y" }, new[] { PartType.And, PartType.Or, PartType.Not }, b => new[] { b[0] ^ b[1] }),
        Logic("half-adder", "반가산기", "S = A xor B,  C = A · B", "1비트 두 개를 더합니다. S는 합, C는 올림(캐리)입니다.",
            new[] { "A", "B" }, new[] { "S", "C" }, Gates, b => new[] { b[0] ^ b[1], b[0] & b[1] }),
        Logic("majority", "다수결 회로", "Y = 3개 중 2개 이상 ON", "세 사람의 투표에서 과반이 찬성하면 Y가 켜집니다.",
            new[] { "A", "B", "C" }, new[] { "Y" }, Gates, b => new[] { b[0] + b[1] + b[2] >= 2 ? 1 : 0 }),

        new Experiment { id = "pn-double", group = "공압", title = "복동 실린더 전진·후진", goal = "A ON → C1 전진,  A OFF → C1 후진",
            desc = "밸브를 놓고 호스로 압축기 P → 밸브 P, 밸브 A → 실린더 A, 밸브 B → 실린더 B를 이으세요. 스위치 A를 밸브 코일(왼쪽 핀)에 전선으로 연결하면 됩니다.",
            palette = new[] { PartType.Valve52, PartType.Valve32 }, inputs = new[] { "A" }, outputs = new[] { "C1" },
            fn = b => new[] { b[0] }, check = ExpCheck.Fluid, fluid = true,
            setup = () => FluidBase(PartType.Compressor, PartType.CylDouble, "A") },
        new Experiment { id = "pn-twohand", group = "공압", title = "양수 안전 회로", goal = "A와 B를 함께 눌러야 C1 전진",
            desc = "프레스는 두 손을 모두 버튼에 두어야 움직여야 안전합니다. AND 회로로 3/2 밸브를 켜서 단동 실린더를 전진시키세요.",
            palette = new[] { PartType.And, PartType.Or, PartType.Not, PartType.Valve32, PartType.Valve52 },
            inputs = new[] { "A", "B" }, outputs = new[] { "C1" }, fn = b => new[] { b[0] & b[1] }, check = ExpCheck.Fluid, fluid = true,
            setup = () => FluidBase(PartType.Compressor, PartType.CylSingle, "A", "B") },
        new Experiment { id = "pn-cycle", group = "공압", title = "자동 왕복 운동", goal = "START ON 동안 C1이 계속 전진·후진",
            desc = "실린더의 센서 S0(후진 끝)·S1(전진 끝)을 쓰세요. SR 래치 S에 'START AND S0', R에 S1을 넣고 래치 출력 Q로 5/2 밸브를 켜면 자기유지 왕복이 됩니다.",
            palette = new[] { PartType.And, PartType.Or, PartType.Not, PartType.Latch, PartType.Valve52 },
            inputs = new[] { "START" }, outputs = new[] { "C1" }, check = ExpCheck.AutoCycle, fluid = true,
            setup = () => FluidBase(PartType.Compressor, PartType.CylDouble, "START") },
        new Experiment { id = "hy-double", group = "유압", title = "유압 실린더 제어", goal = "A ON → C1 전진,  A OFF → C1 후진",
            desc = "유압은 기름이 탱크로 돌아가야 움직입니다. 펌프 P → 밸브 P, 밸브 A·B → 실린더 A·B, 그리고 밸브 R → 펌프 T(탱크)까지 호스로 이으세요.",
            palette = new[] { PartType.Valve52, PartType.Valve32 }, inputs = new[] { "A" }, outputs = new[] { "C1" },
            fn = b => new[] { b[0] }, check = ExpCheck.Fluid, fluid = true,
            setup = () => FluidBase(PartType.Pump, PartType.CylDouble, "A") },
        new Experiment { id = "fluid-free", group = "공유압", title = "공유압 자유 실험", goal = "",
            desc = "압축기·유압 펌프·밸브·실린더와 논리 회로를 섞어 자유롭게 만들어 보세요. 공압 예제가 놓여 있어요.",
            palette = FluidAll, setup = LCircuit.ExampleFluid, check = ExpCheck.None, fluid = true },
    };

    public static Experiment Cur => All[System.Math.Max(0, System.Math.Min(All.Length - 1, Current))];

    static Experiment Logic(string id, string title, string goal, string desc, string[] ins, string[] outs, PartType[] palette, System.Func<int[], int[]> fn)
    {
        return new Experiment
        {
            id = id, group = "논리 회로", title = title, goal = goal, desc = desc, palette = palette,
            inputs = ins, outputs = outs, fn = fn, check = ExpCheck.Truth,
            setup = () =>
            {
                var c = new LCircuit();
                for (int i = 0; i < ins.Length; i++) c.Add(PartType.Switch, -9, Spread(ins.Length, i), ins[i], true);
                for (int i = 0; i < outs.Length; i++) c.Add(PartType.Led, 9, Spread(outs.Length, i), outs[i], true);
                return c;
            }
        };
    }

    static int Spread(int n, int i) => (int)System.Math.Round((i - (n - 1) / 2f) * 3.5f);

    static LCircuit FluidBase(PartType source, PartType cylinder, params string[] switches)
    {
        var c = new LCircuit();
        c.Add(source, -9, -4, null, true);
        for (int i = 0; i < switches.Length; i++) c.Add(PartType.Switch, -9, 1 + i * 3, switches[i], true);
        c.Add(cylinder, 8, -2, "C1", true);
        return c;
    }

    /// <summary>The locked parts a mission needs are still on the board (otherwise rebuild it).</summary>
    public static bool HasFixedParts(LCircuit c, Experiment e)
    {
        var fresh = e.setup();
        foreach (var p in fresh.parts.Where(p => p.locked))
            if (!c.parts.Any(q => q.locked && q.type == p.type && q.label == p.label)) return false;
        return true;
    }

    public static List<PartType> Violations(LCircuit c, Experiment e) =>
        c.parts.Where(p => !p.locked && !e.palette.Contains(p.type)).Select(p => p.type).Distinct().ToList();

    // ---------------------------------------------------------------- checks
    public class TruthCheck
    {
        public List<TruthRow> rows = new List<TruthRow>();
        public List<int[]> expected = new List<int[]>();
        public bool stable = true;
        public int okRows;
    }

    public static TruthCheck CheckTruth(LCircuit c, Experiment e)
    {
        var tc = new TruthCheck();
        var ins = e.inputs.Select(l => c.Find(PartType.Switch, l)).ToList();
        var outs = e.outputs.Select(l => c.Find(PartType.Led, l)).ToList();
        if (ins.Any(p => p == null) || outs.Any(p => p == null)) return tc;
        tc.rows = c.Table(ins, outs, out tc.stable);
        foreach (var r in tc.rows)
        {
            var exp = e.fn(r.bits);
            tc.expected.Add(exp);
            if (exp.SequenceEqual(r.outs)) tc.okRows++;
        }
        return tc;
    }

    /// <summary>Runs the mission headless and returns a message; pass = everything matched.</summary>
    public static string Check(LCircuit c, Experiment e, out bool pass)
    {
        pass = false;
        var bad = Violations(c, e);
        if (bad.Count > 0) return string.Join(", ", bad.Select(LCircuit.Name)) + " 부품은 이 실험에서 쓸 수 없어요";

        switch (e.check)
        {
            case ExpCheck.Truth:
                {
                    var tc = CheckTruth(c, e);
                    if (!tc.stable) return "피드백 루프 때문에 값이 안정되지 않아요";
                    int wrong = tc.rows.Count - tc.okRows;
                    if (wrong > 0) return $"진리표 {wrong}줄이 아직 달라요";
                    pass = true;
                    return "미션 통과!";
                }
            case ExpCheck.Fluid:
                {
                    var sw = e.inputs.Select(l => c.Find(PartType.Switch, l)).ToList();
                    var cyl = e.outputs.Select(l => c.parts.FirstOrDefault(p => LCircuit.IsCylinder(p.type) && p.label == l)).ToList();
                    int n = sw.Count, wrong = 0;
                    string first = null;
                    for (int k = 0; k < (1 << n); k++)
                    {
                        var bits = new int[n];
                        var ov = new Dictionary<int, bool>();
                        for (int i = 0; i < n; i++) { bits[i] = (k >> (n - 1 - i)) & 1; ov[sw[i].id] = bits[i] == 1; }
                        var st = new LabState();
                        FluidResult fr = null;
                        for (int t = 0; t < 160; t++) c.Step(st, true, 0.05f, ov, out fr);
                        var exp = e.fn(bits);
                        for (int j = 0; j < cyl.Count; j++)
                        {
                            float pos = st.cyl.TryGetValue(cyl[j].id, out var v) ? v : 0;
                            bool ok = exp[j] == 1 ? pos > 0.95f : pos < 0.05f;
                            if (ok) continue;
                            wrong++;
                            if (first == null)
                            {
                                string state = string.Join(" ", sw.Select((s, i) => $"{s.label}={bits[i]}"));
                                first = $"{state}일 때 {cyl[j].label}이(가) {(exp[j] == 1 ? "전진" : "후진")}해야 해요";
                                if (fr != null && fr.leak) first += " (공급이 배기로 새고 있어요)";
                            }
                        }
                    }
                    if (wrong > 0)
                    {
                        var pump = c.parts.FirstOrDefault(p => p.type == PartType.Pump);
                        if (pump != null && !c.hoses.Any(h => (h.aPart == pump.id && h.aPort == 1) || (h.bPart == pump.id && h.bPort == 1)))
                            first += " — 밸브 R에서 펌프 T(탱크)로 돌아가는 호스가 있는지 확인하세요";
                        return first;
                    }
                    pass = true;
                    return "미션 통과!";
                }
            case ExpCheck.AutoCycle:
                {
                    var start = c.Find(PartType.Switch, "START");
                    var cyl = c.parts.FirstOrDefault(p => LCircuit.IsCylinder(p.type) && p.label == "C1");
                    if (start == null || cyl == null) return "START 스위치와 실린더 C1이 필요해요";
                    var st = new LabState();
                    var ov = new Dictionary<int, bool> { { start.id, true } };
                    int ext = 0, ret = 0;
                    bool atExt = false, atRet = true;
                    for (int t = 0; t < 400; t++)
                    {
                        c.Step(st, true, 0.05f, ov, out _);
                        float pos = st.cyl[cyl.id];
                        if (pos >= 0.98f && !atExt) { ext++; atExt = true; }
                        if (pos < 0.5f) atExt = false;
                        if (pos <= 0.02f && !atRet) { ret++; atRet = true; }
                        if (pos > 0.5f) atRet = false;
                    }
                    if (ext < 2 || ret < 2) return $"START를 켠 20초 동안 전진 끝 {ext}번 · 후진 끝 {ret}번 — 끝까지 왕복을 2번 이상 해야 해요";
                    ov[start.id] = false;
                    for (int t = 0; t < 200; t++) c.Step(st, true, 0.05f, ov, out _);
                    if (st.cyl[cyl.id] > 0.05f) return "START를 끄면 실린더가 후진한 채로 멈춰야 해요";
                    pass = true;
                    return "미션 통과! 자기유지 왕복 회로 완성";
                }
        }
        return "";
    }

    // ---------------------------------------------------------------- progress
    const string DoneKey = "lab3d.done";

    public static HashSet<string> Done()
    {
        var s = UnityEngine.PlayerPrefs.GetString(DoneKey, "");
        return new HashSet<string>(s.Split(new[] { ',' }, System.StringSplitOptions.RemoveEmptyEntries));
    }

    public static void MarkDone(string id)
    {
        var d = Done();
        if (!d.Add(id)) return;
        UnityEngine.PlayerPrefs.SetString(DoneKey, string.Join(",", d));
        UnityEngine.PlayerPrefs.Save();
    }
}
