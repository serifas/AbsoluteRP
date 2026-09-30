using System;
using System.Collections.Generic;
using System.Linq;

namespace AbsoluteRP.Immersive.Themes;

// Ready-made effects - the ones the built-in themes use, expressed as FxDefs so they can be assigned as-is or copied into a theme's Effects list and tweaked. Ids are "preset:<key>" and never change.
public static class FxPresets
{
    public static readonly IReadOnlyList<FxDef> All = new List<FxDef>
    {
        new() { Id = "preset:spinner", Name = "Orbit rings (Allagan avatar)", Kind = FxKind.Spinner, Shape = FxShape.Ring,
                Direction = FxDirection.Clockwise, Rings = 2, Segments = 3, Coverage = 0.6f, Ticks = true },
        new() { Id = "preset:dial", Name = "Dial ring", Kind = FxKind.Spinner, Shape = FxShape.Ring,
                Direction = FxDirection.Counterclockwise, Rings = 1, Segments = 8, Coverage = 0.45f, Ticks = false, Size = 1.4f },
        new() { Id = "preset:aether_glow", Name = "Aether shimmer (avatar)", Kind = FxKind.Glow, Shape = FxShape.Ring,
                Direction = FxDirection.Outward, Amount = 1.2f, Spread = 1.2f },
        new() { Id = "preset:aether_arc", Name = "Aether arcs (avatar rim)", Kind = FxKind.Shimmer, Shape = FxShape.Ring,
                Direction = FxDirection.Clockwise, Amount = 1.5f },
        new() { Id = "preset:border", Name = "Border effect", Kind = FxKind.Particles, Shape = FxShape.Edge,
                Direction = FxDirection.Outward, Particle = FxParticle.Mote, Amount = 0.8f, Spread = 0.8f },
        new() { Id = "preset:embers", Name = "Rising embers", Kind = FxKind.Particles, Shape = FxShape.Bottom,
                Direction = FxDirection.Up, Particle = FxParticle.Ember, Spread = 1.4f },
        new() { Id = "preset:motes", Name = "Edge motes", Kind = FxKind.Particles, Shape = FxShape.Edge,
                Direction = FxDirection.Outward, Particle = FxParticle.Mote, Amount = 0.8f },
        new() { Id = "preset:dust", Name = "Drifting dust", Kind = FxKind.Particles, Shape = FxShape.Edge,
                Direction = FxDirection.Still, Particle = FxParticle.Dot, Amount = 0.7f, Strength = 0.6f, Glow = false },
        new() { Id = "preset:sparks", Name = "Falling sparks", Kind = FxKind.Particles, Shape = FxShape.Top,
                Direction = FxDirection.Down, Particle = FxParticle.Spark, Spread = 1.6f },
        new() { Id = "preset:shards", Name = "Void shards", Kind = FxKind.Particles, Shape = FxShape.Edge,
                Direction = FxDirection.Outward, Particle = FxParticle.Shard, Amount = 0.6f, Speed = 0.7f },
        new() { Id = "preset:halo", Name = "Halo", Kind = FxKind.Glow, Shape = FxShape.Edge, Direction = FxDirection.Outward },
        new() { Id = "preset:underglow", Name = "Underglow", Kind = FxKind.Glow, Shape = FxShape.Bottom, Direction = FxDirection.Outward, Spread = 0.8f },
        new() { Id = "preset:sweep", Name = "Scan sweep", Kind = FxKind.Sweep, Shape = FxShape.Edge, Direction = FxDirection.Down },
        new() { Id = "preset:sweep_side", Name = "Side sweep", Kind = FxKind.Sweep, Shape = FxShape.Edge, Direction = FxDirection.Right, Size = 1.4f },
        new() { Id = "preset:rim", Name = "Rim shimmer", Kind = FxKind.Shimmer, Shape = FxShape.Edge, Direction = FxDirection.Clockwise },
        new() { Id = "preset:rays", Name = "Radiant rays", Kind = FxKind.Rays, Shape = FxShape.Ring, Direction = FxDirection.Clockwise },
        new() { Id = "preset:burst", Name = "Light burst", Kind = FxKind.Rays, Shape = FxShape.Center, Direction = FxDirection.Still, Amount = 1.6f, Spread = 1.6f },
    };

    // Plain-language help for the editor's tooltips.
    public static string Describe(FxKind k) => k switch
    {
        FxKind.Particles => "Little bits that appear on the shape and travel in the direction you pick.",
        FxKind.Spinner   => "Broken rings that rotate around the shape, like the Allagan avatar rings.",
        FxKind.Glow      => "A soft, breathing halo hugging the shape.",
        FxKind.Sweep     => "A band of light that passes across the element.",
        FxKind.Shimmer   => "A bright spot that runs along the shape's rim.",
        FxKind.Rays      => "Flickering lines that radiate out of the shape.",
        _ => "",
    };

    public static string Describe(FxShape s) => s switch
    {
        FxShape.Ring   => "The circle inside the element — the avatar's portrait when used on an avatar.",
        FxShape.Edge   => "The element's outline.",
        FxShape.Bottom => "The element's bottom edge.",
        FxShape.Top    => "The element's top edge.",
        FxShape.Center => "The element's middle point.",
        _ => "",
    };

    public static string Describe(FxDirection d) => d switch
    {
        FxDirection.Outward => "Away from the shape's centre.",
        FxDirection.Inward  => "Toward the shape's centre.",
        FxDirection.Clockwise or FxDirection.Counterclockwise => "Around the shape's centre.",
        FxDirection.Still   => "Stays where it appears and just twinkles.",
        _ => "Straight " + d.ToString().ToLowerInvariant() + " across the screen.",
    };

    public static string Describe(FxParticle p) => p switch
    {
        FxParticle.Dot   => "A small bright point.",
        FxParticle.Spark => "A short streak along the direction of travel.",
        FxParticle.Ember => "A tumbling glowing chip.",
        FxParticle.Shard => "A sharp sliver pointing where it travels.",
        FxParticle.Mote  => "A soft glowing speck.",
        FxParticle.Ring  => "A tiny hollow circle.",
        _ => "",
    };

    public static string Describe(FxOutline o) => o switch
    {
        FxOutline.Circle => "Runs around a circle inside the element.",
        FxOutline.Square => "Runs around the element's rectangle.",
        _ => "Circle on avatars, rectangle on everything else.",
    };

    public static FxDef? Preset(string id)
    {
        if (id == "preset:sparkles") id = "preset:border";   // renamed
        return All.FirstOrDefault(p => p.Id == id);
    }

    // Finds an effect by id: a preset, or one of the document's own.
    public static FxDef? Resolve(ThemeDocument? doc, string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        if (id.StartsWith("preset:", StringComparison.Ordinal)) return Preset(id);
        return doc?.Effects?.FirstOrDefault(e => e.Id == id);
    }

    public static string NameOf(ThemeDocument? doc, string id) => Resolve(doc, id)?.Name ?? "(missing effect)";
}
