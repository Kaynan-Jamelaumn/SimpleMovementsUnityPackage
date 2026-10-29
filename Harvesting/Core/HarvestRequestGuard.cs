using System;
using System.Collections.Generic;

/// <summary>
/// The server's check of a harvest a client played: the client measured the minigames, the server made the plan of that
/// work when it began (<see cref="HarvestPlanner"/>) and holds the result to it - the same stages in the same order for
/// the planned pieces, every measure within 0-1, the tool and its fitness as the server knows them (not as the client
/// says) and the hide's cells the size of the hide. A result that cannot be the planned work is refused. Work done faster
/// than careful hands can do it counts as rushed: its precision drops and it comes out rough, as rushing does in the
/// minigames themselves.
/// </summary>
public static class HarvestRequestGuard
{
    /// <summary>A stage may come in this much under its fewest seconds (frame timing, a quick hand) before it counts as rushed.</summary>
    public const float TimeSlack = 0.8f;

    /// <summary>
    /// Checks <paramref name="r"/> against <paramref name="plan"/> (made by the server for the same carcass, player, pieces and
    /// tools) and bounds it in place. <paramref name="peltCells"/>: the cells of the creature's hide. False (with why) when it
    /// cannot be the work the plan describes; <paramref name="rushed"/> when some of it was done too fast to be careful.
    /// </summary>
    public static bool Check(HarvestPlan plan, HarvestTaskResult r, int peltCells, out string why, out bool rushed)
    {
        why = "";
        rushed = false;
        if (plan == null || !plan.Valid)
        {
            why = plan != null && plan.refusal.Length > 0 ? plan.refusal : "That cannot be harvested now.";
            return false;
        }
        if (r == null || r.stages == null || r.rough || !string.Equals(r.material, plan.material, StringComparison.OrdinalIgnoreCase))
        {
            why = "That is not the work that was started.";
            return false;
        }

        // The stages the plan expects: one result each, one per piece for prying.
        var expected = new List<(HarvestStageSpec spec, int unit, float min)>();
        foreach (HarvestStageSpec st in plan.stages)
        {
            if (st.kind == HarvestStageKind.Pry)
                foreach (HarvestPieceSpot p in st.pieces) expected.Add((st, p.unit, st.minSeconds / Math.Max(1, st.pieces.Count)));
            else
                expected.Add((st, st.unit, st.minSeconds));
        }
        if (r.stages.Count != expected.Count)
        {
            why = "The work was not finished.";
            return false;
        }

        for (int i = 0; i < expected.Count; i++)
        {
            HarvestStageResult res = r.stages[i];
            (HarvestStageSpec spec, int unit, float min) = expected[i];
            if (res == null || res.kind != spec.kind || res.unit != unit)
            {
                why = "The work does not match what was started.";
                return false;
            }
            res.precision = Bound(res.precision);
            res.damage = Bound(res.damage);
            res.completeness = Bound(res.completeness);
            res.spill = Bound(res.spill);
            res.seconds = float.IsNaN(res.seconds) || float.IsInfinity(res.seconds) ? 0f : Math.Max(0f, res.seconds);
            res.mistakes = Math.Max(0, res.mistakes);
            // The server's tool, not the client's word for it; only the marks a harvest can leave.
            res.tool = spec.toolId ?? "";
            res.toolFitness = spec.toolFitness;
            res.marks &= HarvestMarksInfo.Harvest;
            if (spec.kind == HarvestStageKind.Peel)
            {
                if (!CellsOk(res.cellDamage, peltCells) || !CellsOk(res.cellFlesh, peltCells))
                {
                    why = "The hide does not match.";
                    return false;
                }
                if (res.cellDamage != null)
                    for (int k = 0; k < res.cellDamage.Count; k++) res.cellDamage[k] = Bound(res.cellDamage[k]);
                if (res.cellFlesh != null)
                    for (int k = 0; k < res.cellFlesh.Count; k++) res.cellFlesh[k] = Bound(res.cellFlesh[k]);
                if (res.lostCells != null)
                    res.lostCells.RemoveAll(k => k < 0 || k >= peltCells);
            }
            else
            {
                res.cellDamage = null;
                res.cellFlesh = null;
                res.lostCells = null;
            }

            // A scrape is as clean as it is complete (the hands report them equal): half the work cannot claim a clean bone.
            if (spec.kind == HarvestStageKind.Scrape)
                res.precision = Math.Min(res.precision, res.completeness);
            // Faster than careful hands (a scrape left half done needs only half the time).
            float least = min * TimeSlack * (spec.kind == HarvestStageKind.Scrape ? res.completeness : 1f);
            if (least > 0f && res.seconds < least)
            {
                float k = res.seconds / least;
                res.precision *= k;
                res.damage = Math.Max(res.damage, (1f - k) * 0.8f);
                res.marks |= HarvestMarks.Rough;
                rushed = true;
            }
        }
        // The pieces are the plan's.
        r.units = new List<int>(plan.units);
        return true;
    }

    /// <summary>
    /// Holds a result's claimed times to the time that really passed on this machine between the start of the work and
    /// its result (<paramref name="elapsedSeconds"/>, plus <paramref name="slack"/>): a client cannot claim it worked
    /// longer than it did. When it claims more, every stage's seconds are scaled down by the same factor (the check then
    /// finds the work rushed if it was).
    /// </summary>
    public static void LimitToElapsed(HarvestTaskResult r, double elapsedSeconds, double slack = 0.5)
    {
        if (r?.stages == null)
            return;
        double real = Math.Max(0, elapsedSeconds) + Math.Max(0, slack);
        // No stage took longer than the whole work (an absurd claim, +Infinity included, is held to it), and what is
        // scaled below is what was added up.
        double claimed = 0;
        foreach (HarvestStageResult st in r.stages)
            if (st != null && st.seconds > 0f) // NaN fails the test too
            {
                st.seconds = (float)Math.Min(st.seconds, Math.Min(real, 1e6));
                claimed += st.seconds;
            }
        if (claimed <= real)
            return;
        float k = (float)(real / claimed);
        foreach (HarvestStageResult st in r.stages)
            if (st != null && st.seconds > 0f)
                st.seconds *= k;
    }

    private static float Bound(float v) => float.IsNaN(v) ? 0f : v < 0f ? 0f : v > 1f ? 1f : v;

    private static bool CellsOk(List<float> cells, int count) => cells == null || cells.Count == count;
}
