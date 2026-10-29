using System;
using System.Collections.Generic;
using UnityEngine;
using static HarvestBuild;

/// <summary>
/// The built-in creatures and their anatomies: bear, wolf, deer, boar, rabbit and sheep (four-legged), a drake (four legs
/// and wings, scaled, fire-proof), a troll (two-legged, regenerating), an eagle (winged), a salmon (a fish), a giant
/// spider (a shell and eight legs) and a giant serpent. Each describes only its body - regions, tissues, weak points and
/// what it gives - and reuses every mechanic. A Harvest Database starts from these; copies are handed out, so nothing
/// edits the built-in content.
/// </summary>
public static class HarvestDefaults
{
    /// <summary>Every built-in anatomy (fresh copies).</summary>
    public static List<HarvestAnatomyData> Anatomies() => new List<HarvestAnatomyData>
    {
        Bear(), Wolf(), Deer(), Boar(), Rabbit(), Sheep(), Drake(), Troll(), Eagle(), Salmon(), GiantSpider(), GiantSerpent(),
    };

    /// <summary>A built-in anatomy by id (a fresh copy), or null.</summary>
    public static HarvestAnatomyData Anatomy(string id)
    {
        foreach (HarvestAnatomyData a in Anatomies())
            if (string.Equals(a.id, id, StringComparison.OrdinalIgnoreCase))
                return a;
        return null;
    }

    private static readonly Color Ivory = new Color(0.93f, 0.9f, 0.8f), Horn = new Color(0.45f, 0.38f, 0.3f), Red = new Color(0.68f, 0.18f, 0.2f),
        Liver = new Color(0.42f, 0.14f, 0.14f), Sinew = new Color(0.88f, 0.84f, 0.72f), Fat = new Color(0.96f, 0.92f, 0.78f), Gut = new Color(0.86f, 0.62f, 0.6f),
        Bile = new Color(0.45f, 0.62f, 0.22f), Meat = new Color(0.72f, 0.24f, 0.22f), Venom = new Color(0.55f, 0.8f, 0.35f);

    // ================================================================== body plans
    /// <summary>The regions of a four-legged animal seen from the side (head to the right), opened along the belly.</summary>
    public static List<HarvestRegion> QuadrupedRegions(bool wings = false)
    {
        var l = new List<HarvestRegion>
        {
            Region("head", 1f, 0.3f, 0.9f, 0.2f, 0.15f).Pelt(0.36f, 0f, 0.28f, 0.13f).Band(0.45f, 1f, 0.86f, 1f).Weak(1.5f).React(HarvestRegionReaction.Stagger).Fiddly()
                .Parts("Head", "Skull", "Face", "Snout", "Jaw", "Eyes"),
            Region("neck", 1f, 0.8f, 0.5f, 0.4f, 0f).Pelt(0.3f, 0.11f, 0.4f, 0.11f).Band(0.55f, 1f, 0.76f, 0.87f).Weak(1.6f).Parts("Neck", "Throat"),
            Region("shoulder", 1f, 1.4f, 0.6f, 0.4f, 0.3f).Pelt(0.18f, 0.2f, 0.64f, 0.17f).Band(0.42f, 1f, 0.6f, 0.78f)
                .Parts("Shoulder", "Shoulders", "Forequarter", "UpperTorso"),
            Region("back", 1f, 1.6f, 0.6f, 0.5f, 0f).Pelt(0.38f, 0.34f, 0.24f, 0.36f).Band(0.78f, 1f, 0.24f, 0.62f).Parts("Back", "Spine", "Loin", "Torso"),
            Region("ribs", 1f, 1f, 0.7f, 0.1f, 1f).Pelt(0.16f, 0.36f, 0.68f, 0.3f).Band(0.55f, 0.8f, 0.42f, 0.62f).Weak(1.3f)
                .Parts("Ribs", "Chest", "Flank", "Body").Named("Ribs (the vitals)"),
            Region("belly", 1f, 0.8f, 0.2f, 0.1f, 1f).Pelt(0.05f, 0.36f, 0.9f, 0.34f).Band(0.36f, 0.56f, 0.24f, 0.62f).Mult(1.15f).Defense(-2f)
                .Parts("Belly", "Abdomen", "Stomach", "LowerTorso"),
            Region("haunch", 1f, 1.8f, 0.7f, 0.5f, 0.2f).Pelt(0.18f, 0.66f, 0.64f, 0.22f).Band(0.38f, 1f, 0.08f, 0.26f)
                .Parts("Haunch", "Rump", "Hips", "Pelvis", "Groin", "Hindquarter"),
            Leg("foreleg_l", BodySide.Left, true), Leg("foreleg_r", BodySide.Right, true), Leg("hindleg_l", BodySide.Left, false), Leg("hindleg_r", BodySide.Right, false),
            Region("tail", 1f, 0.1f, 0.3f, 0.3f, 0f).Pelt(0.4f, 0.86f, 0.2f, 0.14f).Band(0.5f, 0.92f, 0f, 0.1f).Mult(0.5f).Fiddly().Parts("Tail"),
        };
        if (wings)
        {
            l.Add(Region("wing_l", 0f, 0.3f, 0.6f, 0.7f, 0f).Band(0.75f, 1f, 0.35f, 0.65f).Side(BodySide.Left, 0.45f).Mult(0.7f).React(HarvestRegionReaction.Slow)
                .Parts("LeftWing", "Wings", "Wing"));
            l.Add(Region("wing_r", 0f, 0.3f, 0.6f, 0.7f, 0f).Band(0.75f, 1f, 0.35f, 0.65f).Side(BodySide.Right, 0.45f).Mult(0.7f).React(HarvestRegionReaction.Slow)
                .Parts("RightWing", "Wings", "Wing"));
        }
        return l;
    }

    private static HarvestRegion Leg(string id, BodySide side, bool front)
    {
        bool left = side == BodySide.Left;
        string s = left ? "Left" : "Right";
        HarvestRegion r = Region(id, 1f, 0.5f, 0.8f, 1f, 0f)
            .Band(0f, 0.45f, front ? 0.6f : 0.06f, front ? 0.82f : 0.3f).Side(side, 0f).Mult(0.8f).React(HarvestRegionReaction.Slow).Fiddly();
        if (front)
            r.Pelt(left ? 0f : 0.8f, 0.15f, 0.2f, 0.24f).Parts(s + "Foreleg", s + "FrontLeg", s + "Arm", s + "UpperArm", s + "Forearm", s + "Hand", s + "Paw", "Forelegs", "Legs");
        else
            r.Pelt(left ? 0f : 0.78f, 0.64f, 0.22f, 0.3f).Parts(s + "Hindleg", s + "HindLeg", s + "BackLeg", s + "Leg", s + "Thigh", s + "Shin", s + "Foot", "Hindlegs", "Legs");
        return r;
    }

    /// <summary>The regions of an upright, two-legged creature seen from the front.</summary>
    public static List<HarvestRegion> BipedRegions() => new List<HarvestRegion>
    {
        Region("head", 1f, 0.2f, 0.9f, 0.1f, 0.1f).Pelt(0.38f, 0f, 0.24f, 0.12f).Band(0.86f, 1f).Weak(1.6f).React(HarvestRegionReaction.Stagger).Fiddly()
            .Parts("Head", "Skull", "Face", "Jaw"),
        Region("neck", 1f, 0.6f, 0.4f, 0.4f, 0f).Pelt(0.38f, 0.1f, 0.24f, 0.08f).Band(0.8f, 0.87f).Weak(1.8f).Parts("Neck", "Throat"),
        Region("chest", 1f, 1.2f, 0.7f, 0.2f, 1f).Pelt(0.25f, 0.17f, 0.5f, 0.25f).Band(0.6f, 0.82f).Parts("Chest", "UpperTorso", "Torso", "Ribs", "Body", "Back"),
        Region("belly", 1f, 0.9f, 0.2f, 0.1f, 1f).Pelt(0.27f, 0.41f, 0.46f, 0.2f).Band(0.48f, 0.62f).Mult(1.1f).Parts("Belly", "Abdomen", "LowerTorso", "Stomach"),
        Region("pelvis", 1f, 1f, 0.7f, 0.4f, 0.3f).Pelt(0.28f, 0.6f, 0.44f, 0.12f).Band(0.42f, 0.5f).Parts("Pelvis", "Groin", "Hips"),
        Region("arm_l", 1f, 0.8f, 0.7f, 0.9f, 0f).Pelt(0f, 0.17f, 0.25f, 0.3f).Band(0.4f, 0.82f).Side(BodySide.Left, 0.65f).Mult(0.8f).Fiddly()
            .Parts("LeftArm", "LeftUpperArm", "LeftForearm", "LeftHand", "Arms"),
        Region("arm_r", 1f, 0.8f, 0.7f, 0.9f, 0f).Pelt(0.75f, 0.17f, 0.25f, 0.3f).Band(0.4f, 0.82f).Side(BodySide.Right, 0.65f).Mult(0.8f).Fiddly()
            .Parts("RightArm", "RightUpperArm", "RightForearm", "RightHand", "Arms"),
        Region("leg_l", 1f, 1.2f, 0.8f, 1f, 0f).Pelt(0.12f, 0.7f, 0.3f, 0.3f).Band(0f, 0.45f).Side(BodySide.Left, 0f).Mult(0.85f).React(HarvestRegionReaction.Slow).Fiddly()
            .Parts("LeftLeg", "LeftThigh", "LeftShin", "LeftFoot", "Legs"),
        Region("leg_r", 1f, 1.2f, 0.8f, 1f, 0f).Pelt(0.58f, 0.7f, 0.3f, 0.3f).Band(0f, 0.45f).Side(BodySide.Right, 0f).Mult(0.85f).React(HarvestRegionReaction.Slow).Fiddly()
            .Parts("RightLeg", "RightThigh", "RightShin", "RightFoot", "Legs"),
    };

    /// <summary>The regions of a bird seen from below, its wings spread.</summary>
    public static List<HarvestRegion> AvianRegions() => new List<HarvestRegion>
    {
        Region("head", 1f, 0.1f, 0.8f, 0.1f, 0.2f).Pelt(0.4f, 0f, 0.2f, 0.12f).Band(0.82f, 1f, 0.55f, 1f).Weak(1.6f).React(HarvestRegionReaction.Stagger).Fiddly()
            .Parts("Head", "Skull", "Beak", "Eyes"),
        Region("neck", 1f, 0.4f, 0.4f, 0.3f, 0f).Pelt(0.4f, 0.1f, 0.2f, 0.12f).Band(0.72f, 0.86f).Weak(1.4f).Parts("Neck", "Throat"),
        Region("breast", 1f, 2f, 0.5f, 0.2f, 1f).Pelt(0.3f, 0.22f, 0.4f, 0.3f).Band(0.45f, 0.76f).Parts("Breast", "Chest", "Torso", "Body", "UpperTorso"),
        Region("belly", 1f, 0.6f, 0.2f, 0.1f, 1f).Pelt(0.32f, 0.5f, 0.36f, 0.18f).Band(0.32f, 0.48f).Parts("Belly", "Abdomen", "LowerTorso"),
        Region("wing_l", 1f, 0.5f, 0.6f, 0.8f, 0f).Pelt(0f, 0.2f, 0.3f, 0.3f).Band(0.4f, 0.9f).Side(BodySide.Left, 0.55f).Mult(0.7f).React(HarvestRegionReaction.Slow).Fiddly()
            .Parts("LeftWing", "Wings", "Wing"),
        Region("wing_r", 1f, 0.5f, 0.6f, 0.8f, 0f).Pelt(0.7f, 0.2f, 0.3f, 0.3f).Band(0.4f, 0.9f).Side(BodySide.Right, 0.55f).Mult(0.7f).React(HarvestRegionReaction.Slow).Fiddly()
            .Parts("RightWing", "Wings", "Wing"),
        Region("tail", 1f, 0.1f, 0.3f, 0.2f, 0f).Pelt(0.38f, 0.68f, 0.24f, 0.32f).Band(0.2f, 0.5f, 0f, 0.3f).Mult(0.6f).Fiddly().Parts("Tail", "TailFeathers"),
        Region("leg_l", 0.5f, 0.4f, 0.7f, 0.8f, 0f).Pelt(0.28f, 0.66f, 0.1f, 0.2f).Band(0f, 0.32f).Side(BodySide.Left, 0f).React(HarvestRegionReaction.Slow).Fiddly()
            .Parts("LeftLeg", "LeftFoot", "Legs", "Talons"),
        Region("leg_r", 0.5f, 0.4f, 0.7f, 0.8f, 0f).Pelt(0.62f, 0.66f, 0.1f, 0.2f).Band(0f, 0.32f).Side(BodySide.Right, 0f).React(HarvestRegionReaction.Slow).Fiddly()
            .Parts("RightLeg", "RightFoot", "Legs", "Talons"),
    };

    /// <summary>The regions of a fish seen from the side (head to the right).</summary>
    public static List<HarvestRegion> FishRegions() => new List<HarvestRegion>
    {
        Region("head", 0.6f, 0.2f, 0.9f, 0f, 0.4f).Pelt(0.3f, 0f, 0.4f, 0.15f).Band(0f, 1f, 0.82f, 1f).Weak(1.5f).React(HarvestRegionReaction.Stagger).Fiddly()
            .Parts("Head", "Gills", "Skull"),
        Region("back", 1f, 2f, 0.6f, 0.2f, 0f).Pelt(0.3f, 0.15f, 0.4f, 0.65f).Band(0.5f, 1f, 0.15f, 0.82f).Parts("Back", "Body", "Torso", "UpperTorso", "Chest"),
        Region("belly", 1f, 1f, 0.4f, 0.1f, 1f).Pelt(0.08f, 0.15f, 0.84f, 0.65f).Band(0f, 0.5f, 0.15f, 0.82f).Mult(1.1f).Parts("Belly", "Abdomen", "LowerTorso", "Stomach"),
        Region("tail", 0.8f, 0.4f, 0.6f, 0.4f, 0f).Pelt(0.35f, 0.8f, 0.3f, 0.2f).Band(0f, 1f, 0f, 0.15f).Mult(0.7f).Fiddly().Parts("Tail", "TailFin"),
        Region("fins", 0f, 0f, 0.3f, 0.2f, 0f).Band(0.92f, 1f, 0.35f, 0.6f).Mult(0.5f).Parts("Fins", "Fin", "DorsalFin"),
    };

    /// <summary>The regions of a serpent seen from above.</summary>
    public static List<HarvestRegion> SerpentRegions() => new List<HarvestRegion>
    {
        Region("head", 1f, 0.1f, 0.9f, 0.1f, 0.4f).Pelt(0.2f, 0f, 0.6f, 0.08f).Band(0f, 1f, 0.9f, 1f).Weak(1.8f).React(HarvestRegionReaction.Stagger).Fiddly()
            .Parts("Head", "Skull", "Jaw", "Fangs"),
        Region("neck", 1f, 0.6f, 0.6f, 0.4f, 0.2f).Pelt(0.15f, 0.08f, 0.7f, 0.12f).Band(0f, 1f, 0.78f, 0.9f).Weak(1.4f).Parts("Neck", "Throat"),
        Region("body", 1f, 2f, 0.7f, 0.4f, 1f).Pelt(0.05f, 0.2f, 0.9f, 0.55f).Band(0f, 1f, 0.2f, 0.78f).Parts("Body", "Torso", "Belly", "Chest", "Back"),
        Region("tail", 1f, 0.5f, 0.5f, 0.3f, 0f).Pelt(0.25f, 0.75f, 0.5f, 0.25f).Band(0f, 1f, 0f, 0.2f).Mult(0.6f).Parts("Tail"),
    };

    /// <summary>The regions of a spider-like creature seen from above (no hide: its shell is plates).</summary>
    public static List<HarvestRegion> ArthropodRegions() => new List<HarvestRegion>
    {
        Region("head", 0f, 0.1f, 0f, 0f, 0.6f).Armor(0.4f).Band(0f, 1f, 0.82f, 1f).Weak(1.4f).React(HarvestRegionReaction.Stagger).Parts("Head", "Eyes", "Fangs", "Chelicerae"),
        Region("thorax", 0f, 0.6f, 0f, 0.2f, 0.6f).Armor(0.5f).Band(0.2f, 1f, 0.55f, 0.82f).Parts("Thorax", "Cephalothorax", "Body", "Torso", "Chest", "Back"),
        Region("abdomen", 0f, 1f, 0f, 0f, 1f).Armor(0.15f).Band(0.2f, 1f, 0f, 0.55f).Weak(1.3f).Parts("Abdomen", "Belly", "LowerTorso"),
        Region("legs_l", 0f, 0.4f, 0f, 0.8f, 0f).Armor(0.3f).Band(0f, 0.55f).Side(BodySide.Left, 0.5f).Mult(0.6f).React(HarvestRegionReaction.Slow).Parts("LeftLegs", "Legs", "LeftLeg"),
        Region("legs_r", 0f, 0.4f, 0f, 0.8f, 0f).Armor(0.3f).Band(0f, 0.55f).Side(BodySide.Right, 0.5f).Mult(0.6f).React(HarvestRegionReaction.Slow).Parts("RightLegs", "Legs", "RightLeg"),
    };

    // ================================================================== shared materials
    /// <summary>
    /// The meat of a four-legged animal: haunch, backstrap, shoulder, ribs, neck and shanks, together about
    /// <paramref name="portions"/> portions from a typical adult, each worth <paramref name="value"/> × its cut.
    /// </summary>
    private static void QuadrupedMeat(HarvestAnatomyData a, string prefix, float portions, float value, float spoil = 45f)
    {
        void Cut(string id, string name, float share, float difficulty, float worth, float hunger, params (string region, float weight)[] from)
        {
            float p = Math.Max(0.6f, portions * share);
            HarvestMaterial m = Material(id, $"{prefix} {name}", HarvestMaterialKind.Meat, HarvestMethod.Butchering, "cut")
                .Yield(Math.Max(0.6f, p * 0.8f), Math.Max(1f, p * 1.2f)).Hard(difficulty).Needs(HarvestMethods.Hide).Spoils(spoil)
                .Worth(value * worth, 1f, 5f).Food(hunger).Tint(Meat);
            foreach ((string region, float weight) in from) m.From(region, 1, weight);
            a.materials.Add(m);
        }
        Cut("haunch", "haunch", 0.3f, 0.4f, 1.1f, 28f, ("haunch", 1f), ("hindleg_l", 0.2f), ("hindleg_r", 0.2f));
        Cut("backstrap", "backstrap", 0.14f, 0.45f, 1.6f, 30f, ("back", 1f));
        Cut("shoulder", "shoulder", 0.22f, 0.4f, 1f, 26f, ("shoulder", 1f));
        Cut("ribs", "ribs", 0.1f, 0.35f, 0.9f, 24f, ("ribs", 1f));
        Cut("neck", "neck meat", 0.08f, 0.3f, 0.7f, 22f, ("neck", 1f));
        Cut("shank", "shanks", 0.08f, 0.35f, 0.6f, 20f, ("foreleg_l", 1f), ("foreleg_r", 1f), ("hindleg_l", 1f), ("hindleg_r", 1f));
    }

    /// <summary>Sinew of the four legs (one each) and the long sinew along the back (the best bowstring).</summary>
    private static void QuadrupedTendons(HarvestAnatomyData a, string prefix, float value)
    {
        a.materials.Add(Material("leg_sinew", $"{prefix} leg sinew", HarvestMaterialKind.Tendon, HarvestMethod.Tendons, "tendon")
            .From("foreleg_l").From("foreleg_r").From("hindleg_l").From("hindleg_r").Count().Hard(0.5f).Needs(HarvestMethods.Hide).Spoils(60f)
            .Worth(value, 0.1f, 7f).Tint(Sinew).Describe("A tendon from a leg: bindings, lashings, glue."));
        a.materials.Add(Material("back_sinew", $"{prefix} back sinew", HarvestMaterialKind.Tendon, HarvestMethod.Tendons, "sinew")
            .From("back").Count().Hard(0.6f).Needs(HarvestMethods.Hide).Spoils(60f)
            .Worth(value * 2f, 0.15f, 10f).Tint(Sinew).Describe("The long sinew along the spine: the finest bowstrings and sinew backing."));
    }

    /// <summary>The skull, the long bones of the legs, the ribs, the spine and the pelvis.</summary>
    private static void QuadrupedBones(HarvestAnatomyData a, string prefix, float value)
    {
        a.materials.Add(Material("skull", $"{prefix} skull", HarvestMaterialKind.Bone, HarvestMethod.Bones, "skull").From("head").Count().Hard(0.55f)
            .Needs(HarvestMethods.Hide).Worth(value * 3f, 1f, 10f).Tint(Ivory).Describe("A trophy, a totem, a talisman."));
        a.materials.Add(Material("femur", $"{prefix} femur", HarvestMaterialKind.Bone, HarvestMethod.Bones, "longbone").From("hindleg_l").From("hindleg_r").Count()
            .Hard(0.45f).Needs(HarvestMethods.Hide).Worth(value * 1.2f, 0.6f, 7f).Tint(Ivory).Describe("A long, strong bone: handles, tools, bone armour."));
        a.materials.Add(Material("humerus", $"{prefix} humerus", HarvestMaterialKind.Bone, HarvestMethod.Bones, "longbone").From("foreleg_l").From("foreleg_r").Count()
            .Hard(0.45f).Needs(HarvestMethods.Hide).Worth(value, 0.5f, 7f).Tint(Ivory));
        a.materials.Add(Material("rib_bones", $"{prefix} rib bones", HarvestMaterialKind.Bone, HarvestMethod.Bones, "ribs").From("ribs").Count()
            .Hard(0.4f).Needs(HarvestMethods.Hide).Worth(value * 1.2f, 1f, 6f).Tint(Ivory).Describe("Curved bones: needles, awls, frames, hooks."));
        a.materials.Add(Material("vertebrae", $"{prefix} vertebrae", HarvestMaterialKind.Bone, HarvestMethod.Bones, "spine").From("back").Count()
            .Hard(0.5f).Needs(HarvestMethods.Hide).Worth(value, 1f, 6f).Tint(Ivory));
        a.materials.Add(Material("pelvis", $"{prefix} pelvis", HarvestMaterialKind.Bone, HarvestMethod.Bones, "pelvis").From("haunch").Count()
            .Hard(0.5f).Needs(HarvestMethods.Hide).Worth(value * 0.8f, 0.8f, 6f).Tint(Ivory));
    }

    /// <summary>
    /// The organs of the chest and belly: heart and lungs (behind the ribs), liver with its gall bladder, kidneys, stomach
    /// and guts. The gall, the stomach and the guts soil what is around them when torn.
    /// </summary>
    private static void Organs(HarvestAnatomyData a, string prefix, float value, string chest = "ribs", string belly = "belly", float gallValue = 0f)
    {
        a.materials.Add(Material("heart", $"{prefix} heart", HarvestMaterialKind.Organ, HarvestMethod.Organs, "heart").From(chest).Count().Hard(0.5f)
            .Spoils(18f).Worth(value * 8f, 0.4f, 8f).Food(18f).Tint(Red).Alchemy("sanguine_sap", 2f, 0.7f).Describe("Strong medicine, they say, and a hunter's due."));
        a.materials.Add(Material("lungs", $"{prefix} lungs", HarvestMaterialKind.Organ, HarvestMethod.Organs, "lungs").From(chest).Count().Hard(0.45f)
            .Spoils(14f).Worth(value * 3f, 0.6f, 6f).Tint(new Color(0.85f, 0.5f, 0.55f)));
        a.materials.Add(Material("liver", $"{prefix} liver", HarvestMaterialKind.Organ, HarvestMethod.Organs, "liver").From(belly).Count().Hard(0.55f)
            .Spoils(15f).Worth(value * 6f, 0.8f, 8f).Food(22f).Tint(Liver).Alchemy("sanguine_sap", 1.5f, 0.6f).Describe("Rich food; mind the gall bladder lying against it."));
        a.materials.Add(Material("kidneys", $"{prefix} kidney", HarvestMaterialKind.Organ, HarvestMethod.Organs, "kidney").From(belly, 2).Count().Hard(0.5f)
            .Spoils(15f).Worth(value * 3f, 0.2f, 6f).Food(10f).Tint(new Color(0.5f, 0.16f, 0.16f)));
        a.materials.Add(Material("gallbladder", $"{prefix} gall bladder", HarvestMaterialKind.Organ, HarvestMethod.Organs, "gall").From(belly).Count().Hard(0.75f)
            .Spoils(20f).Worth(gallValue > 0f ? gallValue : value * 4f, 0.1f, 10f).Hazard().Fragile().Tint(Bile).Alchemy("bitter_resin", 2f, 0.8f)
            .Describe("Bitter bile: alchemists pay for it whole. Torn, it spoils the liver beside it."));
        a.materials.Add(Material("stomach", $"{prefix} stomach", HarvestMaterialKind.Organ, HarvestMethod.Organs, "stomach").From(belly).Count().Hard(0.5f)
            .Spoils(12f).Worth(value * 2f, 1f, 5f).Hazard().Tint(Gut).Describe("Tripe, or a waterskin once cleaned."));
        a.materials.Add(Material("intestines", $"{prefix} intestines", HarvestMaterialKind.Organ, HarvestMethod.Organs, "intestines").From(belly).Count().Hard(0.45f)
            .Spoils(10f).Worth(value * 1.5f, 2f, 5f).Hazard().Tint(Gut).Describe("Casings and catgut. Taking them out guts the carcass: its meat keeps longer."));
    }

    private static void Claws(HarvestAnatomyData a, string id, string name, int front, int hind, float value, float difficulty = 0.45f)
    {
        HarvestMaterial m = Material(id, name, HarvestMaterialKind.Claw, HarvestMethod.Trophies, "claw").Count().Hard(difficulty).Worth(value, 0.03f, 4f).Tint(Horn);
        if (front > 0) m.From("foreleg_l", front).From("foreleg_r", front);
        if (hind > 0) m.From("hindleg_l", hind).From("hindleg_r", hind);
        a.materials.Add(m);
    }

    private static HarvestAnatomyData New(string id, string name, HarvestBodyPlan plan, float mass, float height, int peltW, int peltH, string description, params string[] mobTypes) =>
        new HarvestAnatomyData
        {
            id = id, displayName = name, plan = plan, mass = mass, referenceHeight = height, peltSize = new Vector2Int(peltW, peltH),
            description = description, mobTypes = new List<string>(mobTypes),
        };

    // ================================================================== four-legged animals
    public static HarvestAnatomyData Bear()
    {
        HarvestAnatomyData a = New("bear", "Bear", HarvestBodyPlan.Quadruped, 250f, 1.4f, 12, 16,
            "A heavy, thick-furred predator: a great pelt, fat, claws and the prized gall.", "Bear", "Grizzly");
        a.hideColor = new Color(0.45f, 0.32f, 0.22f);
        a.hairColor = new Color(0.28f, 0.19f, 0.12f);
        a.regions = QuadrupedRegions();
        a.Region("head").bonusDefense = 3f; // a thick skull
        a.materials.Add(Material("pelt", "Bear pelt", HarvestMaterialKind.Hide, HarvestMethod.Skinning).From("back").Yield(1f, 1f, "pelt").Hard(0.55f).Spoils(120f)
            .Worth(45f, 9f, 16f, 2).Tannery("bear").Tint(new Color(0.35f, 0.24f, 0.15f)).Describe("A huge, very thick pelt: warm cloaks, rugs, heavy leather."));
        QuadrupedMeat(a, "Bear", 30f, 2.2f);
        a.materials.Add(Material("fat", "Bear fat", HarvestMaterialKind.Fat, HarvestMethod.Butchering, "fat").From("belly", 1, 1f).From("back", 1, 0.5f)
            .Yield(3f, 5f, "slab").Hard(0.3f).Needs(HarvestMethods.Hide).Spoils(80f).Worth(3f, 0.8f, 4f).Food(12f).Tint(Fat)
            .Alchemy("oil", 3f, 0.6f).Describe("Renders into tallow and oil: lamps, waterproofing, salves."));
        QuadrupedTendons(a, "Bear", 5f);
        QuadrupedBones(a, "Bear", 4f);
        Organs(a, "Bear", 2f, gallValue: 40f);
        Claws(a, "claws", "Bear claw", 5, 5, 5f, 0.5f);
        a.materials.Add(Material("fangs", "Bear fang", HarvestMaterialKind.Tooth, HarvestMethod.Trophies, "fang").From("head", 4).Count().Hard(0.55f)
            .Worth(6f, 0.03f, 5f).Tint(Ivory));
        a.materials.Add(Material("bezoar", "Bezoar stone", HarvestMaterialKind.Other, HarvestMethod.Organs, "gland").From("belly").Count().Rare(0.12f, 15)
            .Hard(0.45f).Worth(60f, 0.1f, 15f, 3).Tint(new Color(0.6f, 0.55f, 0.4f)).Alchemy("antitoxin", 2f, 0.5f).Describe("A smooth stone from the stomach, said to draw out any poison."));
        return a;
    }

    public static HarvestAnatomyData Wolf()
    {
        HarvestAnatomyData a = New("wolf", "Wolf", HarvestBodyPlan.Quadruped, 45f, 0.85f, 8, 11,
            "A lean pack hunter: a thick winter pelt, fangs and claws.", "Wolf", "Warg", "Fox", "Hound", "Dog", "Coyote", "Jackal");
        a.hideColor = new Color(0.55f, 0.52f, 0.48f);
        a.hairColor = new Color(0.42f, 0.4f, 0.38f);
        a.regions = QuadrupedRegions();
        a.materials.Add(Material("pelt", "Wolf pelt", HarvestMaterialKind.Hide, HarvestMethod.Skinning).From("back").Yield(1f, 1f, "pelt").Hard(0.45f).Spoils(120f)
            .Worth(20f, 3f, 12f).Tannery("wolf").Tint(new Color(0.5f, 0.48f, 0.45f)).Describe("A thick fur: hoods, cloaks, linings."));
        QuadrupedMeat(a, "Wolf", 5f, 1f);
        QuadrupedTendons(a, "Wolf", 3f);
        QuadrupedBones(a, "Wolf", 2f);
        Organs(a, "Wolf", 1f);
        Claws(a, "claws", "Wolf claw", 5, 4, 2f);
        a.materials.Add(Material("fangs", "Wolf fang", HarvestMaterialKind.Tooth, HarvestMethod.Trophies, "fang").From("head", 4).Count().Hard(0.5f)
            .Worth(5f, 0.02f, 5f).Tint(Ivory).Describe("Necklaces, charms, arrowheads."));
        a.materials.Add(Material("musk_gland", "Scent gland", HarvestMaterialKind.Gland, HarvestMethod.Organs, "gland").From("haunch").Count().Rare(0.35f, 10)
            .Hard(0.65f).Spoils(30f).Worth(20f, 0.05f, 12f, 2).Fragile().Tint(new Color(0.7f, 0.6f, 0.3f)).Alchemy("esters", 1.5f, 0.7f).Describe("Lures for traps, and a base for scents."));
        return a;
    }

    public static HarvestAnatomyData Deer()
    {
        HarvestAnatomyData a = New("deer", "Deer", HarvestBodyPlan.Quadruped, 80f, 1.1f, 8, 10,
            "A swift grazer: supple hide, lean venison, and a stag's antlers.", "Deer", "Stag", "Doe", "Elk", "Moose", "Reindeer");
        a.hideColor = new Color(0.62f, 0.45f, 0.3f);
        a.hairColor = new Color(0.55f, 0.38f, 0.22f);
        a.regions = QuadrupedRegions();
        a.materials.Add(Material("hide", "Deer hide", HarvestMaterialKind.Hide, HarvestMethod.Skinning).From("back").Yield(1f, 1f, "hide").Hard(0.4f).Spoils(120f)
            .Worth(14f, 3f, 12f).Tannery("deer").Tint(new Color(0.6f, 0.43f, 0.28f)).Describe("Thin and supple: gloves, wraps, garments."));
        QuadrupedMeat(a, "Venison", 9f, 1.4f);
        QuadrupedTendons(a, "Deer", 3f);
        QuadrupedBones(a, "Deer", 2f);
        Organs(a, "Deer", 1f);
        a.materials.Add(Material("antlers", "Antler", HarvestMaterialKind.Horn, HarvestMethod.Trophies, "antler").From("head", 2).Count().Rare(0.6f, 0)
            .Hard(0.45f).Worth(18f, 0.8f, 8f).Tint(new Color(0.7f, 0.6f, 0.45f)).Describe("Only stags carry them: handles, picks, glue, trophies."));
        a.materials.Add(Material("hooves", "Hoof", HarvestMaterialKind.Horn, HarvestMethod.Trophies, "claw").From("foreleg_l").From("foreleg_r").From("hindleg_l").From("hindleg_r")
            .Count().Hard(0.35f).Worth(1.5f, 0.1f, 3f).Tint(Horn).Describe("Boiled down for glue."));
        a.materials.Add(Material("musk_pod", "Musk pod", HarvestMaterialKind.Gland, HarvestMethod.Organs, "gland").From("belly").Count().Rare(0.15f, 15)
            .Hard(0.7f).Spoils(40f).Worth(45f, 0.05f, 14f, 2).Fragile().Tint(new Color(0.55f, 0.4f, 0.25f)).Alchemy("esters", 2.5f, 0.7f).Describe("Perfumers pay dearly for it."));
        return a;
    }

    public static HarvestAnatomyData Boar()
    {
        HarvestAnatomyData a = New("boar", "Boar", HarvestBodyPlan.Quadruped, 90f, 0.9f, 9, 12,
            "A bristly, ill-tempered brute: tough hide, rich pork, tusks.", "Boar", "Pig", "Hog", "Swine", "Sow");
        a.hideColor = new Color(0.4f, 0.32f, 0.27f);
        a.hairColor = new Color(0.25f, 0.2f, 0.17f);
        a.toughness = 1.15f;
        a.regions = QuadrupedRegions();
        a.Region("shoulder").bonusDefense = 2f; // the shield of gristle over a boar's shoulders
        a.materials.Add(Material("hide", "Boar hide", HarvestMaterialKind.Hide, HarvestMethod.Skinning).From("back").Yield(1f, 1f, "hide").Hard(0.5f).Spoils(120f)
            .Worth(12f, 4f, 12f).Tannery("boar").Tint(new Color(0.38f, 0.3f, 0.25f)).Describe("Tough and bristly: belts, soles, armour."));
        QuadrupedMeat(a, "Pork", 11f, 1.2f);
        a.materials.Add(Material("fat", "Lard", HarvestMaterialKind.Fat, HarvestMethod.Butchering, "fat").From("belly", 1, 1f).From("back", 1, 0.6f)
            .Yield(2f, 4f, "slab").Hard(0.3f).Needs(HarvestMethods.Hide).Spoils(80f).Worth(2.5f, 0.6f, 4f).Food(12f).Tint(Fat).Alchemy("oil", 2.5f, 0.6f));
        QuadrupedTendons(a, "Boar", 3f);
        QuadrupedBones(a, "Boar", 2f);
        Organs(a, "Boar", 1f);
        a.materials.Add(Material("tusks", "Boar tusk", HarvestMaterialKind.Tooth, HarvestMethod.Trophies, "tusk").From("head", 2).Count().Hard(0.5f)
            .Worth(10f, 0.1f, 7f).Tint(Ivory).Describe("Hooked ivory: knives, pendants, trophies."));
        a.materials.Add(Material("hooves", "Hoof", HarvestMaterialKind.Horn, HarvestMethod.Trophies, "claw").From("foreleg_l").From("foreleg_r").From("hindleg_l").From("hindleg_r")
            .Count().Hard(0.35f).Worth(1.5f, 0.1f, 3f).Tint(Horn));
        return a;
    }

    public static HarvestAnatomyData Rabbit()
    {
        HarvestAnatomyData a = New("rabbit", "Rabbit", HarvestBodyPlan.Quadruped, 2f, 0.3f, 3, 4,
            "A small game animal: a soft pelt, a little meat, a lucky foot.", "Rabbit", "Hare", "Bunny");
        a.hideColor = new Color(0.7f, 0.62f, 0.52f);
        a.hairColor = new Color(0.62f, 0.55f, 0.46f);
        a.regions = QuadrupedRegions();
        a.materials.Add(Material("pelt", "Rabbit pelt", HarvestMaterialKind.Hide, HarvestMethod.Skinning).From("back").Yield(1f, 1f, "pelt").Hard(0.35f).Spoils(100f)
            .Worth(2f, 0.2f, 5f).Tannery("rabbit").Tint(new Color(0.66f, 0.58f, 0.48f)).Describe("Small and soft: linings, mittens."));
        a.materials.Add(Material("saddle", "Rabbit saddle", HarvestMaterialKind.Meat, HarvestMethod.Butchering, "cut").From("back").From("ribs", 1, 0.5f)
            .Yield(1f, 1f).Hard(0.3f).Needs(HarvestMethods.Hide).Spoils(45f).Worth(1.5f, 0.4f, 3f).Food(14f).Tint(Meat));
        a.materials.Add(Material("legs", "Rabbit legs", HarvestMaterialKind.Meat, HarvestMethod.Butchering, "cut").From("haunch").From("hindleg_l", 1, 0.5f).From("hindleg_r", 1, 0.5f)
            .Yield(1f, 2f).Hard(0.3f).Needs(HarvestMethods.Hide).Spoils(45f).Worth(1f, 0.3f, 3f).Food(12f).Tint(Meat));
        a.materials.Add(Material("foot", "Rabbit's foot", HarvestMaterialKind.Claw, HarvestMethod.Trophies, "paw").From("foreleg_l").From("foreleg_r").From("hindleg_l").From("hindleg_r")
            .Count().Hard(0.3f).Worth(3f, 0.03f, 3f).Tint(new Color(0.75f, 0.7f, 0.62f)).Describe("For luck."));
        return a;
    }

    public static HarvestAnatomyData Sheep()
    {
        HarvestAnatomyData a = New("sheep", "Sheep", HarvestBodyPlan.Quadruped, 60f, 0.9f, 8, 10,
            "A woolly grazer: a fleece-covered skin, mutton, a ram's horns.", "Sheep", "Ram", "Lamb", "Ewe", "Goat");
        a.hideColor = new Color(0.82f, 0.78f, 0.7f);
        a.hairColor = new Color(0.9f, 0.88f, 0.82f);
        a.regions = QuadrupedRegions();
        a.materials.Add(Material("sheepskin", "Sheepskin", HarvestMaterialKind.Hide, HarvestMethod.Skinning).From("back").Yield(1f, 1f, "skin").Hard(0.4f).Spoils(120f)
            .Worth(12f, 3f, 10f).Tannery("deer").Tint(new Color(0.88f, 0.85f, 0.78f)).Describe("The wool still on: rugs, saddle pads, warm linings."));
        QuadrupedMeat(a, "Mutton", 7f, 1f);
        QuadrupedTendons(a, "Sheep", 2f);
        QuadrupedBones(a, "Sheep", 1.5f);
        Organs(a, "Sheep", 0.8f);
        a.materials.Add(Material("horns", "Ram horn", HarvestMaterialKind.Horn, HarvestMethod.Trophies, "horn").From("head", 2).Count().Rare(0.5f, 0)
            .Hard(0.45f).Worth(8f, 0.4f, 6f).Tint(new Color(0.65f, 0.58f, 0.45f)).Describe("Only rams carry them: cups, bows, horns to blow."));
        return a;
    }

    // ================================================================== creatures of legend
    public static HarvestAnatomyData Drake()
    {
        HarvestAnatomyData a = New("drake", "Drake", HarvestBodyPlan.Quadruped, 400f, 1.8f, 10, 14,
            "A wingless dragon's cousin in scales: fire-proof hide, scales, a fire gland - and, rarely, a heartstone.",
            "Drake", "Wyvern", "Dragon", "Wyrmling");
        a.hideColor = new Color(0.3f, 0.38f, 0.26f);
        a.hairColor = new Color(0.22f, 0.3f, 0.2f);
        a.meatColor = new Color(0.6f, 0.2f, 0.18f);
        a.toughness = 1.3f;
        a.resistances.Add(new HarvestElementResistance { element = ElementType.Fire, resist = 0.9f });
        a.regions = QuadrupedRegions(true);
        foreach (string id in new[] { "back", "shoulder", "haunch", "tail", "neck", "head" }) a.Region(id).armor = 0.5f;
        a.Region("belly").armor = 0.1f;
        a.Region("belly").Weak(1.4f).Named("Belly (soft scales)");
        a.materials.Add(Material("hide", "Drake hide", HarvestMaterialKind.Hide, HarvestMethod.Skinning).From("back").Yield(1f, 1f, "hide").Hard(0.65f, 15).Spoils(240f)
            .Worth(150f, 14f, 30f, 4).Tannery("drake").Tint(new Color(0.3f, 0.4f, 0.27f)).Describe("Scaled and fire-proof by nature: the finest armour leather."));
        a.materials.Add(Material("scales", "Drake scale", HarvestMaterialKind.Scales, HarvestMethod.Trophies, "scale").From("back", 6).From("shoulder", 4).From("haunch", 4).From("tail", 3)
            .Count().Hard(0.5f).Worth(8f, 0.05f, 5f, 3).Tint(new Color(0.32f, 0.45f, 0.3f)).Describe("Loose scales for scale mail and shields."));
        a.materials.Add(Material("membrane", "Wing membrane", HarvestMaterialKind.Hide, HarvestMethod.Skinning, "membrane").From("wing_l").From("wing_r").Count()
            .Hard(0.7f, 10).Spoils(180f).Worth(30f, 1.5f, 14f, 3).Tint(new Color(0.45f, 0.35f, 0.3f)).Describe("Thin, tough, light: kites, sails, fine armour panels."));
        a.materials.Add(Material("meat", "Drake meat", HarvestMaterialKind.Meat, HarvestMethod.Butchering, "cut").From("haunch").From("back", 1, 0.8f).From("shoulder", 1, 0.8f)
            .Yield(10f, 16f).Hard(0.5f).Needs(HarvestMethods.Hide).Spoils(60f).Worth(5f, 1f, 6f, 2).Food(30f).Tint(a.meatColor).Describe("Tough and smoky; it warms you from inside."));
        QuadrupedTendons(a, "Drake", 12f);
        a.materials.Add(Material("skull", "Drake skull", HarvestMaterialKind.Bone, HarvestMethod.Bones, "skull").From("head").Count().Hard(0.6f)
            .Needs(HarvestMethods.Hide).Worth(60f, 4f, 16f, 3).Tint(Ivory));
        a.materials.Add(Material("femur", "Drake femur", HarvestMaterialKind.Bone, HarvestMethod.Bones, "longbone").From("hindleg_l").From("hindleg_r").Count()
            .Hard(0.55f).Needs(HarvestMethods.Hide).Worth(20f, 2f, 10f, 3).Tint(Ivory));
        a.materials.Add(Material("heart", "Drake heart", HarvestMaterialKind.Organ, HarvestMethod.Organs, "heart").From("ribs").Count().Hard(0.55f)
            .Spoils(30f).Worth(50f, 2f, 14f, 3).Tint(Red).Alchemy("draconic_ichor", 2f, 0.6f).Describe("Still warm long after death."));
        a.materials.Add(Material("fire_gland", "Fire gland", HarvestMaterialKind.Gland, HarvestMethod.Organs, "gland").From("belly").Count().Hard(0.8f, 20)
            .Spoils(40f).Worth(80f, 1f, 20f, 4).Fragile().Hazard().Tint(new Color(1f, 0.45f, 0.15f)).Alchemy("ember_spirit", 3f, 0.8f)
            .Describe("The sac of oil a drake breathes fire with. Burst, it burns what is around it."));
        a.materials.Add(Material("heartstone", "Heartstone", HarvestMaterialKind.Essence, HarvestMethod.Organs, "gland").From("ribs").Count().Rare(0.25f, 20)
            .Hard(0.6f).Worth(200f, 0.3f, 30f, 5).Tint(new Color(1f, 0.3f, 0.25f)).Alchemy("wyrmblood_essence", 2f, 0.4f).Describe("A crystal grown around an old drake's heart, glowing faintly."));
        a.materials.Add(Material("horns", "Drake horn", HarvestMaterialKind.Horn, HarvestMethod.Trophies, "horn").From("head", 2).Count().Hard(0.55f)
            .Worth(30f, 1.5f, 12f, 3).Tint(new Color(0.3f, 0.26f, 0.22f)));
        Claws(a, "claws", "Drake claw", 4, 4, 12f, 0.55f);
        a.materials.Add(Material("fangs", "Drake fang", HarvestMaterialKind.Tooth, HarvestMethod.Trophies, "fang").From("head", 4).Count().Hard(0.6f)
            .Worth(15f, 0.1f, 8f, 3).Tint(Ivory));
        return a;
    }

    public static HarvestAnatomyData Troll()
    {
        HarvestAnatomyData a = New("troll", "Troll", HarvestBodyPlan.Biped, 300f, 2.6f, 12, 16,
            "A hulking regenerator: wounds close in the fight unless burned. Its heart keeps beating after death.", "Troll", "Ogre");
        a.hideColor = new Color(0.4f, 0.45f, 0.36f);
        a.hairColor = new Color(0.3f, 0.32f, 0.26f);
        a.meatColor = new Color(0.5f, 0.3f, 0.3f);
        a.toughness = 1.4f;
        a.regeneration = 0.5f;
        a.regions = BipedRegions();
        a.materials.Add(Material("hide", "Troll hide", HarvestMaterialKind.Hide, HarvestMethod.Skinning).From("chest").Yield(1f, 1f, "hide").Hard(0.6f, 10).Spoils(200f)
            .Worth(60f, 12f, 20f, 3).Tannery("troll").Tint(new Color(0.4f, 0.45f, 0.36f)).Describe("Thick as a door: heavy leather armour that shrugs off blows."));
        a.materials.Add(Material("flesh", "Troll flesh", HarvestMaterialKind.Meat, HarvestMethod.Butchering, "cut").From("leg_l").From("leg_r").From("chest", 1, 0.6f)
            .Yield(6f, 10f, "chunk").Hard(0.45f).Needs(HarvestMethods.Hide).Spoils(90f).Worth(4f, 1.5f, 6f, 2).Tint(a.meatColor).Alchemy("vital_essence", 0.5f, 0.3f)
            .Describe("Not food. Alchemists want it: it still tries to heal itself."));
        a.materials.Add(Material("fat", "Troll fat", HarvestMaterialKind.Fat, HarvestMethod.Butchering, "fat").From("belly").Yield(3f, 5f, "slab").Hard(0.3f)
            .Needs(HarvestMethods.Hide).Spoils(90f).Worth(3f, 1f, 4f).Tint(Fat).Alchemy("oil", 3f, 0.6f));
        a.materials.Add(Material("sinew", "Troll sinew", HarvestMaterialKind.Tendon, HarvestMethod.Tendons, "tendon").From("arm_l").From("arm_r").From("leg_l").From("leg_r")
            .Count().Hard(0.55f).Needs(HarvestMethods.Hide).Spoils(90f).Worth(10f, 0.3f, 9f, 2).Tint(Sinew).Describe("Thick, elastic: siege bows, crossbow prods."));
        a.materials.Add(Material("skull", "Troll skull", HarvestMaterialKind.Bone, HarvestMethod.Bones, "skull").From("head").Count().Hard(0.55f)
            .Needs(HarvestMethods.Hide).Worth(40f, 4f, 14f, 2).Tint(Ivory));
        a.materials.Add(Material("femur", "Troll femur", HarvestMaterialKind.Bone, HarvestMethod.Bones, "longbone").From("leg_l").From("leg_r").Count().Hard(0.5f)
            .Needs(HarvestMethods.Hide).Worth(15f, 3f, 10f, 2).Tint(Ivory).Describe("A club as it is."));
        a.materials.Add(Material("heart", "Regenerating heart", HarvestMaterialKind.Organ, HarvestMethod.Organs, "heart").From("chest").Count().Hard(0.6f, 10)
            .Spoils(60f).Worth(120f, 3f, 24f, 4).Tint(new Color(0.55f, 0.15f, 0.25f)).Alchemy("vital_essence", 3f, 0.5f).Describe("It beats on its own. The base of every regeneration draught."));
        a.materials.Add(Material("liver", "Troll liver", HarvestMaterialKind.Organ, HarvestMethod.Organs, "liver").From("belly").Count().Hard(0.5f)
            .Spoils(30f).Worth(20f, 3f, 10f, 2).Tint(Liver));
        a.materials.Add(Material("tusks", "Troll tusk", HarvestMaterialKind.Tooth, HarvestMethod.Trophies, "tusk").From("head", 2).Count().Hard(0.5f)
            .Worth(12f, 0.3f, 8f, 2).Tint(Ivory));
        a.materials.Add(Material("claws", "Troll claw", HarvestMaterialKind.Claw, HarvestMethod.Trophies, "claw").From("arm_l", 5).From("arm_r", 5).Count().Hard(0.45f)
            .Worth(6f, 0.05f, 5f, 2).Tint(Horn));
        return a;
    }

    public static HarvestAnatomyData Eagle()
    {
        HarvestAnatomyData a = New("eagle", "Eagle", HarvestBodyPlan.Avian, 6f, 0.8f, 8, 8,
            "A great bird of prey: flight feathers, talons, a beak - and eyes alchemists covet.", "Eagle", "Hawk", "Falcon", "Bird", "Vulture", "Owl", "Raven", "Griffon");
        a.hideColor = new Color(0.85f, 0.75f, 0.65f);
        a.hairColor = new Color(0.4f, 0.3f, 0.2f);
        a.regions = AvianRegions();
        a.materials.Add(Material("feathers", "Flight feather", HarvestMaterialKind.Feathers, HarvestMethod.Skinning, "feathers").From("wing_l").From("wing_r").From("tail", 1, 0.6f)
            .Yield(8f, 14f, "feather").Hard(0.4f).Worth(2f, 0.01f, 5f).Tint(new Color(0.42f, 0.32f, 0.22f)).Describe("Fletching for the finest arrows; quills."));
        a.materials.Add(Material("breast", "Bird breast", HarvestMaterialKind.Meat, HarvestMethod.Butchering, "cut").From("breast").Yield(1f, 2f).Hard(0.35f)
            .Needs(HarvestMethods.Hide).Spoils(40f).Worth(2f, 0.3f, 4f).Food(16f).Tint(Meat));
        a.materials.Add(Material("talons", "Talon", HarvestMaterialKind.Claw, HarvestMethod.Trophies, "talon").From("leg_l", 4).From("leg_r", 4).Count().Hard(0.45f)
            .Worth(6f, 0.01f, 4f, 2).Tint(new Color(0.2f, 0.18f, 0.16f)));
        a.materials.Add(Material("beak", "Beak", HarvestMaterialKind.Horn, HarvestMethod.Trophies, "beak").From("head").Count().Hard(0.45f)
            .Worth(10f, 0.05f, 6f, 2).Tint(new Color(0.85f, 0.7f, 0.25f)));
        a.materials.Add(Material("wishbone", "Wishbone", HarvestMaterialKind.Bone, HarvestMethod.Bones, "jaw").From("breast").Count().Hard(0.6f)
            .Needs(HarvestMethods.Hide).Worth(5f, 0.01f, 5f).Tint(Ivory).Describe("Fragile. Make a wish."));
        a.materials.Add(Material("eyes", "Eagle eye", HarvestMaterialKind.Organ, HarvestMethod.Organs, "gland").From("head", 2).Count().Hard(0.7f, 5)
            .Spoils(20f).Worth(15f, 0.01f, 9f, 2).Fragile().Tint(new Color(0.95f, 0.75f, 0.2f)).Alchemy("zephyr_essence", 1f, 0.7f).Describe("Alchemists brew far sight from them."));
        a.materials.Add(Material("heart", "Bird heart", HarvestMaterialKind.Organ, HarvestMethod.Organs, "heart").From("breast").Count().Hard(0.5f)
            .Spoils(18f).Worth(6f, 0.05f, 5f).Tint(Red));
        return a;
    }

    public static HarvestAnatomyData Salmon()
    {
        HarvestAnatomyData a = New("salmon", "Salmon", HarvestBodyPlan.Fish, 5f, 0.25f, 4, 10,
            "A river fish: two fillets, roe in season, scales.", "Fish", "Salmon", "Trout", "Carp", "Pike", "Bass", "Cod");
        a.hideColor = new Color(0.7f, 0.72f, 0.75f);
        a.hairColor = new Color(0.55f, 0.6f, 0.65f);
        a.meatColor = new Color(0.95f, 0.55f, 0.42f);
        a.regions = FishRegions();
        a.materials.Add(Material("scales", "Fish scales", HarvestMaterialKind.Scales, HarvestMethod.Skinning, "scale").From("back").From("belly", 1, 0.8f)
            .Yield(10f, 20f, "scale").Hard(0.3f).Worth(0.2f, 0.005f, 3f).Tint(new Color(0.8f, 0.85f, 0.9f)).Describe("Glue, sequins, alchemists' glitter."));
        a.materials.Add(Material("fillet", "Fish fillet", HarvestMaterialKind.Meat, HarvestMethod.Butchering, "cut").From("back").From("belly", 1, 0.6f)
            .Yield(2f, 2f, "fillet").Hard(0.5f).Spoils(30f).Worth(3f, 0.6f, 5f).Food(20f).Tint(a.meatColor).Describe("Follow the backbone with the knife; leave nothing on it."));
        a.materials.Add(Material("roe", "Roe", HarvestMaterialKind.Organ, HarvestMethod.Organs, "gland").From("belly").Count().Rare(0.5f, 0).Hard(0.6f)
            .Spoils(20f).Worth(8f, 0.2f, 6f).Fragile().Food(12f).Tint(new Color(1f, 0.45f, 0.25f)).Alchemy("albumen", 1.5f, 0.9f).Describe("A delicacy, and bait."));
        a.materials.Add(Material("bones", "Fish bones", HarvestMaterialKind.Bone, HarvestMethod.Bones, "spine").From("back").Count().Hard(0.35f)
            .Worth(1f, 0.05f, 3f).Tint(Ivory).Describe("Needles and glue."));
        return a;
    }

    public static HarvestAnatomyData GiantSpider()
    {
        HarvestAnatomyData a = New("giant_spider", "Giant spider", HarvestBodyPlan.Arthropod, 120f, 1.2f, 2, 2,
            "A hunting spider the size of a pony: venom, silk, chitin plates. No hide: its shell comes off plate by plate.",
            "Spider", "Arachnid", "Broodmother", "Crawler");
        a.hideColor = new Color(0.2f, 0.18f, 0.16f);
        a.meatColor = new Color(0.75f, 0.78f, 0.6f);
        a.regions = ArthropodRegions();
        a.materials.Add(Material("plates", "Chitin plate", HarvestMaterialKind.Chitin, HarvestMethod.Trophies, "plate").From("thorax", 4).From("abdomen", 6).From("head", 2)
            .Count().Hard(0.45f).Worth(6f, 0.3f, 5f, 2).Tint(new Color(0.22f, 0.2f, 0.2f)).Describe("Light, hard plates: shields, armour, bowls."));
        a.materials.Add(Material("fangs", "Spider fang", HarvestMaterialKind.Tooth, HarvestMethod.Trophies, "fang").From("head", 2).Count().Hard(0.55f)
            .Worth(20f, 0.1f, 8f, 3).Tint(new Color(0.15f, 0.12f, 0.12f)).Describe("Hollow: they still drip."));
        a.materials.Add(Material("venom_gland", "Venom gland", HarvestMaterialKind.Gland, HarvestMethod.Organs, "venom").From("head", 2).Count().Hard(0.7f, 10)
            .Spoils(30f).Worth(35f, 0.1f, 14f, 3).Fragile().Tint(Venom).Alchemy("venom", 3f, 0.8f).Describe("Poisons, and their antidotes."));
        a.materials.Add(Material("silk_gland", "Silk gland", HarvestMaterialKind.Gland, HarvestMethod.Organs, "gland").From("abdomen").Count().Hard(0.6f)
            .Spoils(45f).Worth(40f, 0.4f, 12f, 3).Fragile().Tint(new Color(0.9f, 0.9f, 0.85f)).Describe("Draw spider silk from it: the strongest thread there is."));
        a.materials.Add(Material("heart", "Spider heart", HarvestMaterialKind.Organ, HarvestMethod.Organs, "heart").From("abdomen").Count().Hard(0.55f)
            .Spoils(20f).Worth(12f, 0.3f, 6f, 2).Tint(new Color(0.4f, 0.55f, 0.5f)));
        a.materials.Add(Material("legs", "Spider leg meat", HarvestMaterialKind.Meat, HarvestMethod.Butchering, "cut").From("legs_l").From("legs_r")
            .Yield(2f, 4f).Hard(0.4f).Spoils(30f).Worth(2f, 0.5f, 4f).Food(14f).Tint(a.meatColor).Describe("Tastes like crab. Mostly."));
        a.materials.Add(Material("egg_sac", "Egg sac", HarvestMaterialKind.Gland, HarvestMethod.Organs, "gland").From("abdomen").Count().Rare(0.3f, 10).Hard(0.65f)
            .Spoils(60f).Worth(60f, 1f, 16f, 3).Fragile().Hazard().Tint(new Color(0.85f, 0.85f, 0.7f)).Describe("Hundreds of eggs in silk. Break it and they spill everywhere."));
        return a;
    }

    public static HarvestAnatomyData GiantSerpent()
    {
        HarvestAnatomyData a = New("giant_serpent", "Giant serpent", HarvestBodyPlan.Serpent, 60f, 0.4f, 5, 18,
            "A great snake: one long skin, venom, fangs.", "Snake", "Serpent", "Viper", "Python", "Cobra", "Asp", "Basilisk");
        a.hideColor = new Color(0.45f, 0.5f, 0.3f);
        a.hairColor = new Color(0.3f, 0.36f, 0.2f);
        a.meatColor = new Color(0.85f, 0.75f, 0.7f);
        a.regions = SerpentRegions();
        a.materials.Add(Material("skin", "Snakeskin", HarvestMaterialKind.Hide, HarvestMethod.Skinning).From("body").Yield(1f, 1f, "skin").Hard(0.5f).Spoils(150f)
            .Worth(18f, 1.5f, 12f, 2).Tannery("snake").Tint(new Color(0.45f, 0.5f, 0.3f)).Describe("Thin, scaled, beautiful: belts, wraps, sheaths."));
        a.materials.Add(Material("meat", "Snake meat", HarvestMaterialKind.Meat, HarvestMethod.Butchering, "cut").From("body").Yield(3f, 6f).Hard(0.35f)
            .Needs(HarvestMethods.Hide).Spoils(40f).Worth(1.5f, 0.5f, 4f).Food(14f).Tint(a.meatColor));
        a.materials.Add(Material("venom_gland", "Venom gland", HarvestMaterialKind.Gland, HarvestMethod.Organs, "venom").From("head", 2).Count().Hard(0.7f, 5)
            .Spoils(30f).Worth(25f, 0.05f, 12f, 2).Fragile().Tint(Venom).Alchemy("venom", 2.5f, 0.8f));
        a.materials.Add(Material("fangs", "Serpent fang", HarvestMaterialKind.Tooth, HarvestMethod.Trophies, "fang").From("head", 2).Count().Hard(0.55f)
            .Worth(10f, 0.02f, 6f, 2).Tint(Ivory));
        a.materials.Add(Material("vertebrae", "Serpent vertebrae", HarvestMaterialKind.Bone, HarvestMethod.Bones, "spine").From("body").Count().Hard(0.45f)
            .Needs(HarvestMethods.Hide).Worth(6f, 0.5f, 6f).Tint(Ivory).Describe("Hundreds of small bones: beads, chimes, charms."));
        return a;
    }
}
