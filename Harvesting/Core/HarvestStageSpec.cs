using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>What the pointer did on the work surface.</summary>
public enum HarvestPointer
{
    Down,
    Drag,
    Up,
    Move,
}

/// <summary>A stretch of a knife's path that asks for care.</summary>
public enum HarvestZoneKind
{
    /// <summary>Around a face, a paw, a joint: slow down or the cut goes astray.</summary>
    Fiddly,
    /// <summary>A joint along a seam: slow down to find the gap, or the knife hacks through bone.</summary>
    Joint,
    /// <summary>The gut lies right under the knife: rush here and it is opened.</summary>
    Gut,
}

[Serializable]
public class HarvestZone
{
    /// <summary>Where along the path it starts and ends (fractions of its length).</summary>
    public float from, to;
    public HarvestZoneKind kind;

    public HarvestZone() { }
    public HarvestZone(float from, float to, HarvestZoneKind kind)
    {
        this.from = from;
        this.to = to;
        this.kind = kind;
    }
}

/// <summary>A line the knife must follow (one cut of a Trace stage).</summary>
[Serializable]
public class HarvestPath
{
    public List<Vector2> points = new List<Vector2>();
    public List<HarvestZone> zones = new List<HarvestZone>();
    public string name = "";

    public float Length
    {
        get
        {
            float l = 0f;
            for (int i = 1; i < points.Count; i++) l += (points[i] - points[i - 1]).magnitude;
            return l;
        }
    }

    /// <summary>The point <paramref name="distance"/> along the path.</summary>
    public Vector2 At(float distance)
    {
        if (points.Count == 0) return Vector2.zero;
        float left = Math.Max(0f, distance);
        for (int i = 1; i < points.Count; i++)
        {
            float seg = (points[i] - points[i - 1]).magnitude;
            if (left <= seg && seg > 1e-5f)
                return points[i - 1] + (points[i] - points[i - 1]) * (left / seg);
            left -= seg;
        }
        return points[points.Count - 1];
    }

    /// <summary>The kind of zone at a fraction of the path (null when none).</summary>
    public HarvestZone ZoneAt(float fraction)
    {
        if (zones == null) return null;
        foreach (HarvestZone z in zones)
            if (fraction >= z.from && fraction <= z.to) return z;
        return null;
    }
}

/// <summary>A small attachment the knife tip must be held on until it is cut (Steady), or the end a hand grips (Pull).</summary>
[Serializable]
public class HarvestTarget
{
    public Vector2 at;
    public float radius = 10f;
    /// <summary>Seconds of steady cutting it needs.</summary>
    public float need = 1f;
}

/// <summary>Something the knife must not cut: a gut (spills), the organ itself (damages it).</summary>
[Serializable]
public class HarvestHazard
{
    public List<Vector2> shape = new List<Vector2>();
    public string name = "";
    /// <summary>How much harm a second inside does.</summary>
    public float severity = 1f;
    /// <summary>A gut, bladder or gall: cutting it spills into the cavity.</summary>
    public bool gut;
    /// <summary>The piece being taken (cutting it damages the piece itself).</summary>
    public bool self;
}

/// <summary>One shape of a work surface's picture.</summary>
[Serializable]
public class HarvestSceneShape
{
    public List<Vector2> points = new List<Vector2>();
    public Color fill = Color.clear;
    public Color line = Color.clear;
    public float width = 1f;
    public bool closed = true;
    /// <summary>Drawn dimmer (taken pieces, the far side).</summary>
    public bool faint;
    /// <summary>A unit index this shape is (for highlighting the piece being worked), -1 = none.</summary>
    public int unit = -1;

    public HarvestSceneShape() { }

    public HarvestSceneShape(IEnumerable<Vector2> pts, Color fill, Color line, float width = 1.5f, bool closed = true)
    {
        points.AddRange(pts);
        this.fill = fill;
        this.line = line;
        this.width = width;
        this.closed = closed;
    }
}

/// <summary>A piece worked by a Pry or Saw stage (a claw, a horn), where it is on the surface.</summary>
[Serializable]
public class HarvestPieceSpot
{
    public int unit;
    /// <summary>The base the tool grips (Pry) or where the saw cuts (Saw: the first point of the cut line).</summary>
    public Vector2 at;
    /// <summary>Saw: the end of the cut line.</summary>
    public Vector2 to;
    /// <summary>Its condition from the fight (0-1): a cracked claw cracks sooner.</summary>
    public float integrity = 1f;
    public string name = "";
}

/// <summary>
/// Everything one stage of a harvest needs: what it is (<see cref="kind"/>, title, instructions), its geometry on the work
/// surface (pixels of a <see cref="HarvestPlanner.Width"/>×<see cref="HarvestPlanner.Height"/> surface, origin bottom-left),
/// how forgiving it is (from the material's difficulty, the player's skill and the tool) and the picture to draw. Made by
/// the <see cref="HarvestPlanner"/>; a <see cref="HarvestStageModel"/> plays it.
/// </summary>
[Serializable]
public class HarvestStageSpec
{
    public HarvestStageKind kind;
    public string title = "";
    public string instruction = "";
    /// <summary>The piece this stage is for (-1 = the material as a whole).</summary>
    public int unit = -1;

    [Header("Tool")]
    public HarvestToolKind tool;
    public string toolId = "";
    public string toolName = "";
    public float toolFitness = 1f;
    /// <summary>The tool's speed × fitness (how fast the work goes).</summary>
    public float toolSpeed = 1f;

    [Header("Geometry")]
    public List<HarvestPath> paths = new List<HarvestPath>();
    public List<HarvestTarget> targets = new List<HarvestTarget>();
    public List<HarvestHazard> hazards = new List<HarvestHazard>();
    public List<HarvestPieceSpot> pieces = new List<HarvestPieceSpot>();
    /// <summary>Peel / Scrape: the grid's place on the surface.</summary>
    public Rect area;
    public int gridW, gridH;
    /// <summary>Peel: 0 = not hide, 1 = hide, 2 = fiddly hide, 3 = hide holed in the fight. Scrape: tissue in tenths (0-10), 255 = outside.</summary>
    public List<byte> cells = new List<byte>();
    /// <summary>Pull: the direction things come out (unit vector) and how far the hand must draw.</summary>
    public Vector2 pullFrom, pullTo;

    [Header("Margins")]
    /// <summary>Trace: how far off the line still counts (px). Steady: the target's radius. Saw: how far off the kerf. Pry: the inner swing (px).</summary>
    public float tolerance = 10f;
    /// <summary>Trace: the fastest safe knife speed (px/s). Pry: the outer swing (px).</summary>
    public float speedLimit = 160f;
    /// <summary>Peel / Pull: the band of pull that works (0-1.5).</summary>
    public float bandLow = 0.4f, bandHigh = 0.7f;
    /// <summary>Peel: the band on fiddly cells.</summary>
    public float fiddlyLow = 0.5f, fiddlyHigh = 0.62f;
    /// <summary>How fast the work goes (cells/s, share/s, removal/s).</summary>
    public float rate = 1f;
    /// <summary>How quickly mistakes hurt (tearing, snapping, cracking, gouging, cutting into a hazard).</summary>
    public float strain = 1f;
    /// <summary>How far the hand wanders (px).</summary>
    public float tremble = 6f;
    /// <summary>Saw: the ideal stroke (seconds).</summary>
    public float seconds = 0.55f;
    /// <summary>Saw: how far off the beat (seconds) a stroke still counts as even.</summary>
    public float window = 0.25f;
    /// <summary>Saw: strokes needed. Pry: swings needed. Pull: how many times it catches.</summary>
    public int count = 6;
    /// <summary>Show the guides (joint gaps, hidden hazards, catches coming): from the player's Anatomy and techniques.</summary>
    public bool guides;
    /// <summary>Nick chance per cell peeled (Peel).</summary>
    public float nick = 0.03f;
    /// <summary>The fewest seconds real hands could do it in (the server's bound).</summary>
    public float minSeconds;

    [Header("Picture")]
    public List<HarvestSceneShape> scene = new List<HarvestSceneShape>();
    /// <summary>Colours of the work: what is being taken, the flesh under it, its fur.</summary>
    public Color workColor = new Color(0.8f, 0.3f, 0.3f), underColor = new Color(0.72f, 0.24f, 0.22f), furColor = new Color(0.4f, 0.3f, 0.2f);
    /// <summary>Scrape: the colour of what is scraped off (meat and gristle, fat, feathers, scales).</summary>
    public Color tissueColor = new Color(0.72f, 0.3f, 0.26f);
}

/// <summary>A whole harvest planned: its stages in order and what it is for.</summary>
[Serializable]
public class HarvestPlan
{
    public string material = "";
    public List<int> units = new List<int>();
    public List<HarvestStageSpec> stages = new List<HarvestStageSpec>();
    /// <summary>The fewest seconds the whole plan can take.</summary>
    public float minSeconds;
    /// <summary>What it is ("Skin the bear: 2 stages").</summary>
    public string summary = "";
    /// <summary>Why it cannot be done (empty when it can).</summary>
    public string refusal = "";

    public bool Valid => string.IsNullOrEmpty(refusal) && stages.Count > 0;
}
