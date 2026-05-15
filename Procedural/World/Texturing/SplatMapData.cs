using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Represents data for a splat map, including its RenderTexture and Texture2D representation.
/// </summary>
public class SplatMapData
{
    private RenderTexture splatMap;
    private Texture2D splatMap2D;
    private ComputeBuffer biomeThresholdBuffer;

    /// <summary>
    /// Gets or sets the RenderTexture representation of the splat map.
    /// </summary>
    public RenderTexture SplatMap { get => splatMap; set => splatMap = value; }

    /// <summary>
    /// Gets or sets the Texture2D representation of the splat map.
    /// </summary>
    public Texture2D SplatMap2D { get => splatMap2D; set => splatMap2D = value; }
}
