using LargerMaps;

int checks = 0;
void Check(bool value, string label)
{
    checks++;
    if (!value) throw new Exception(label);
}
var budget = new AIRouteBudget();
Check(budget.Observe(1, 1, 10, "MOVE", "a", 2, true) == 1, "first move");
for (int i = 0; i < 10000; i++)
    Check(budget.Observe(1, 1, 10, "MOVE", "a", 2, true) == 0, "civilian poll dedup");
Check(budget.Observe(1, 1, 10, "MOVE", "b", 2, true) == 1, "changed destination");
Check(budget.Observe(1, 1, 10, "MOVE", "c", 2, true) == 2, "explicit cap");
Check(budget.Observe(1, 1, 10, "MOVE", "d", 2, true) == 0, "cap warning only once");
Check(budget.Observe(1, 1, 20, "MOVE", "a", 2, true) == 1, "hostile independent of civilian");
Check(budget.Observe(1, 1, 10, "STATE", "a", 2, true) == 1, "snapshot independent of moves");
Check(budget.Summary(10).Contains("repeat=10000,limited=2"), "full polling/suppression accounting");
for (int round = 2; round <= 100; round++)
    Check(budget.Observe(1, round, 10, "MOVE", "a", 2, true) == 1, "coverage beyond rounds 7/8");
Check(budget.Observe(2, 100, 10, "MOVE", "a", 2, true) == 1, "new map, same round/actor");
Check(budget.Observe(2, 1, 10, "MOVE", "a", 2, true) == 1, "loaded earlier round");
Check(budget.Observe(2, 1, 10, "SKILL", "same shot", 24, false) == 1, "first real shot");
Check(budget.Observe(2, 1, 10, "SKILL", "same shot", 24, false) == 1, "identical second shot retained");
for (int i = 0; i < AIRouteBudget.MaxEntries; i++)
    budget.Observe(3, 1, i, "STATE", "a", 1, true);
Check(budget.Observe(3, 1, -1, "STATE", "a", 1, true) == 3, "bounded memory warns");
Check(budget.Observe(3, 1, -2, "STATE", "a", 1, true) == 0, "overflow warning once");
Check(budget.Observe(3, 2, -1, "STATE", "a", 1, true) == 1, "overflow recovers next round");
budget.Reset();
Check(budget.Observe(3, 2, -1, "STATE", "a", 1, true) == 1, "same native pointers after mission reload");
Check(AIRouteBudget.Distance(47, 40, 47, 40, 13, 17) == 0, "zone minimum");
Check(AIRouteBudget.Distance(59, 56, 47, 40, 13, 17) == 0, "inclusive last tile");
Check(AIRouteBudget.Distance(60, 57, 47, 40, 13, 17) == Math.Sqrt(2), "outside corner");
Check(AIRouteBudget.Distance(36, 78, 47, 40, 13, 17) == Math.Sqrt(605), "observed spawn");
Check(double.IsNaN(AIRouteBudget.Distance(-1, -1, 47, 40, 13, 17)), "unknown tile not arrival");
Check(double.IsNaN(AIRouteBudget.Distance(47, 40, 47, 40, 0, 17)), "invalid zone");
Console.WriteLine($"AI route runtime checks passed: {checks}");