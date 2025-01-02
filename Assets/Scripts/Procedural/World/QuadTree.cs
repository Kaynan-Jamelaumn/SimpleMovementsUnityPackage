using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

public class Quadtree
{
    private readonly Rect bounds;
    private readonly int capacity;
    private List<Vector2> points;
    private Quadtree[] children;
    private bool divided;

    public Quadtree(Rect bounds, int capacity)
    {
        this.bounds = bounds;
        this.capacity = capacity;
        points = new List<Vector2>();
        divided = false;
    }

    public bool Insert(Vector2 point)
    {
        if (!bounds.Contains(point))
            return false;

        if (points.Count < capacity)
        {
            points.Add(point);
            return true;
        }

        if (!divided)
            Subdivide();

        foreach (var child in children)
        {
            if (child.Insert(point))
                return true;
        }

        return false;
    }

    public void Query(Rect range, List<Vector2> found)
    {
        if (!bounds.Overlaps(range))
            return;

        foreach (var point in points)
        {
            if (range.Contains(point))
                found.Add(point);
        }

        if (!divided) return;

        foreach (var child in children)
        {
            child.Query(range, found);
        }
    }

    private void Subdivide()
    {
        float x = bounds.xMin, y = bounds.yMin, w = bounds.width / 2, h = bounds.height / 2;
        children = new Quadtree[]
        {
            new Quadtree(new Rect(x, y, w, h), capacity),
            new Quadtree(new Rect(x + w, y, w, h), capacity),
            new Quadtree(new Rect(x, y + h, w, h), capacity),
            new Quadtree(new Rect(x + w, y + h, w, h), capacity)
        };
        divided = true;
    }
}
