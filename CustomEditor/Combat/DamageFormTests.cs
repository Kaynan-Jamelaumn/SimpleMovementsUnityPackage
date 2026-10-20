#if UNITY_INCLUDE_TESTS && UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace InjurySystem.Tests
{
    /// <summary>
    /// Edit Mode tests of damage forms and injury resistances: the form of weapons and hits, the odds of each injury by form
    /// (slashes cut and bleed, blunt blows bruise and break bones, bites tear), limb loss by form, resistances (chance,
    /// severity, bleeding, immunity), the injury character stats, and bringing an older settings asset up to date.
    /// </summary>
    public class DamageFormTests
    {
        private readonly List<Object> created = new List<Object>();

        private T Track<T>(T obj) where T : Object
        {
            created.Add(obj);
            return obj;
        }

        [TearDown]
        public void Cleanup()
        {
            foreach (Object o in created)
                if (o != null) Object.DestroyImmediate(o);
            created.Clear();
        }

        private InjurySettings Settings()
        {
            var s = Track(ScriptableObject.CreateInstance<InjurySettings>());
            InjuryDefaults.Fill(s);
            return s;
        }

        private InjuryController Controller(InjurySettings s)
        {
            var go = Track(new GameObject("Injured"));
            var c = go.AddComponent<InjuryController>();
            var so = new SerializedObject(c);
            so.FindProperty("settingsOverride").objectReferenceValue = s;
            so.ApplyModifiedPropertiesWithoutUndo();
            c.BuildParts();
            return c;
        }

        /// <summary>A hit removing <paramref name="fraction"/> of the part's max condition.</summary>
        private static DamageInfo Hit(InjuryController c, InjuryPartState part, float fraction, DamageForm form)
        {
            float perDamage = c.Settings.conditionPerDamage * Mathf.Max(0.01f, part.Def.conditionDamageMultiplier);
            return new DamageInfo
            {
                amount = fraction * part.MaxCondition / perDamage, type = DamageType.Physical, delivery = DamageDelivery.Melee, form = form,
            };
        }

        private static bool HasBleedingInjury(InjuryPartState p)
        {
            foreach (Injury i in p.Injuries)
                if (i.Type.HasTag(InjuryDefaults.TagBleeding))
                    return true;
            return false;
        }

        /// <summary>Hits a fresh forearm <paramref name="n"/> times; returns how many hits left a bleeding injury.</summary>
        private static int BleedingHits(InjuryController c, DamageForm form, int n, float fraction = 0.15f)
        {
            int bleeding = 0;
            for (int k = 0; k < n; k++)
            {
                c.HealAll(true);
                InjuryPartState arm = c.GetPart("LeftForearm");
                c.ApplyHit(arm, Hit(c, arm, fraction, form));
                if (HasBleedingInjury(arm))
                    bleeding++;
            }
            return bleeding;
        }

        // ------------------------------------------------------------------ forms
        [Test]
        public void Forms_FollowTheWeaponCategory()
        {
            Assert.AreEqual(DamageForm.Slash, DamageForms.ForCategory(WeaponCategory.Sword));
            Assert.AreEqual(DamageForm.Slash, DamageForms.ForCategory(WeaponCategory.Axe));
            Assert.AreEqual(DamageForm.Pierce, DamageForms.ForCategory(WeaponCategory.Spear));
            Assert.AreEqual(DamageForm.Pierce, DamageForms.ForCategory(WeaponCategory.Bow));
            Assert.AreEqual(DamageForm.Blunt, DamageForms.ForCategory(WeaponCategory.Mace));
            Assert.AreEqual(DamageForm.Blunt, DamageForms.ForCategory(WeaponCategory.Staff));
            Assert.AreEqual(DamageForm.None, DamageForms.ForCategory(WeaponCategory.Wand));

            var sword = Track(ScriptableObject.CreateInstance<WeaponSO>());
            sword.SetStats(WeaponCategory.Sword, 5f, 8f, 0f, 1f, 0f, WeaponScaling.None);
            Assert.AreEqual(DamageForm.Slash, DamageForms.Resolve(new DamageInfo { weapon = sword }), "a sword slashes");
            Assert.AreEqual(DamageForm.Pierce, DamageForms.Resolve(new DamageInfo { weapon = sword, form = DamageForm.Pierce }), "unless the attack is a thrust");
            Assert.AreEqual(DamageForm.None, DamageForms.Resolve(new DamageInfo { element = ElementType.Fire }), "a fireball has no form");
        }

        [Test]
        public void Forms_FamiliesAndMasks()
        {
            Assert.AreEqual(DamageForm.Slash, DamageForms.Family(DamageForm.Claw));
            Assert.AreEqual(DamageForm.Pierce, DamageForms.Family(DamageForm.Bite));
            Assert.AreEqual(DamageForm.Blunt, DamageForms.Family(DamageForm.Crush));
            Assert.IsTrue(DamageForms.Contains(DamageFormMask.Physical, DamageForm.Bite));
            Assert.IsFalse(DamageForms.Contains(DamageFormMask.Physical, DamageForm.None), "formless hits are not physical forms");
            Assert.IsTrue(DamageForms.Contains(DamageFormMask.All, DamageForm.None));
            Assert.AreEqual(TraitStat.SlashDamageTaken, TraitStats.DamageTakenFor(DamageForm.Claw));
            Assert.AreEqual(TraitStat.BluntDamageTaken, TraitStats.DamageTakenFor(DamageForm.Crush));
            Assert.IsNull(TraitStats.DamageTakenFor(DamageForm.None));
        }

        // ------------------------------------------------------------------ odds
        [Test]
        public void Odds_SlashesCut_BluntBreaks_BitesTear()
        {
            InjurySettings s = Settings();
            InjuryTrigger laceration = s.FindType(InjuryDefaults.Laceration).trigger;
            InjuryTrigger fracture = s.FindType(InjuryDefaults.Fracture).trigger;
            InjuryTrigger bite = s.FindType(InjuryDefaults.BiteWound).trigger;
            InjuryTrigger bruise = s.FindType(InjuryDefaults.Bruise).trigger;
            Assert.Greater(laceration.ChanceFor(DamageForm.Slash), laceration.ChanceFor(DamageForm.Blunt) * 5f);
            Assert.Greater(laceration.ChanceFor(DamageForm.Blunt), 0f, "not impossible");
            Assert.Greater(fracture.ChanceFor(DamageForm.Blunt), fracture.ChanceFor(DamageForm.Slash) * 3f);
            Assert.Greater(fracture.StrengthFor(DamageForm.Crush), fracture.StrengthFor(DamageForm.Slash), "crushing breaks worse");
            Assert.AreEqual(0f, bite.ChanceFor(DamageForm.Slash), "only teeth make bite wounds");
            Assert.Greater(bite.ChanceFor(DamageForm.Bite), 0.5f);
            Assert.Greater(bruise.ChanceFor(DamageForm.Blunt), bruise.ChanceFor(DamageForm.Slash) * 5f);
        }

        [Test]
        public void Hits_SlashesBleedFarMoreOftenThanBluntBlows()
        {
            Random.InitState(12345);
            InjuryController c = Controller(Settings());
            int slash = BleedingHits(c, DamageForm.Slash, 300);
            int blunt = BleedingHits(c, DamageForm.Blunt, 300);
            Assert.Greater(slash, 200, $"slash {slash}/300 bleeding hits");
            Assert.Less(blunt, 90, $"blunt {blunt}/300 bleeding hits");
            Assert.Greater(blunt, 0, "a mace can still split the skin now and then");
        }

        [Test]
        public void Hits_BluntBlowsBruise()
        {
            Random.InitState(7);
            InjuryController c = Controller(Settings());
            int bruised = 0;
            for (int k = 0; k < 200; k++)
            {
                c.HealAll(true);
                InjuryPartState arm = c.GetPart("LeftForearm");
                c.ApplyHit(arm, Hit(c, arm, 0.15f, DamageForm.Blunt));
                foreach (Injury i in arm.Injuries)
                    if (i.Type.id == InjuryDefaults.Bruise)
                    {
                        bruised++;
                        Assert.AreEqual(DamageForm.Blunt, i.Form, "the injury remembers what caused it");
                    }
            }
            Assert.Greater(bruised, 100);
        }

        [Test]
        public void LimbLoss_BladesSever_BluntBlowsRarely()
        {
            InjurySettings s = Settings();
            LimbLossRules rules = s.FindPart("LeftForearm").limbLoss;
            Assert.Less(rules.formChances.For(DamageForm.Blunt) * 5f, rules.formChances.For(DamageForm.Slash));
            Assert.Greater(rules.formChances.For(DamageForm.Crush), rules.formChances.For(DamageForm.Blunt), "a crushing blow can take a hand");

            InjuryController c = Controller(s);
            InjuryPartState arm = c.GetPart("LeftForearm");
            arm.Def.limbLoss.chance = 1f;
            arm.Def.limbLoss.formChances.blunt = 0f;
            c.ApplyHit(arm, Hit(c, arm, 0.9f, DamageForm.Blunt));
            Assert.IsFalse(arm.Lost, "blunt: never with odds 0");

            c.HealAll(true);
            object sturdy = c.Resistances.Add(new InjuryResistance("Sturdy Limbs") { limbLoss = -100f });
            arm = c.GetPart("LeftForearm");
            c.ApplyHit(arm, Hit(c, arm, 0.9f, DamageForm.Slash));
            Assert.IsFalse(arm.Lost, "a resistance of -100% keeps the limb");

            c.HealAll(true);
            c.Resistances.Remove(sturdy);
            arm = c.GetPart("LeftForearm");
            c.ApplyHit(arm, Hit(c, arm, 0.9f, DamageForm.Slash));
            Assert.IsTrue(arm.Lost, "a heavy slash severs (chance 1)");
        }

        // ------------------------------------------------------------------ resistances
        [Test]
        public void Resistance_ImmuneToWounds()
        {
            Random.InitState(3);
            InjuryController c = Controller(Settings());
            c.Resistances.Add(new InjuryResistance("Tough Skin", InjuryDefaults.TagWound) { injuryChance = -100f });
            Assert.AreEqual(0, BleedingHits(c, DamageForm.Slash, 100), "no cut ever gets through");
            Assert.AreEqual(0f, c.ChanceOf(c.Settings.FindType(InjuryDefaults.Laceration), c.GetPart("LeftForearm"), DamageForm.Slash));
            Assert.Greater(c.ChanceOf(c.Settings.FindType(InjuryDefaults.Fracture), c.GetPart("LeftForearm"), DamageForm.Blunt), 0f, "bones still break");
        }

        [Test]
        public void Resistance_OnlyAgainstItsForms()
        {
            InjuryController c = Controller(Settings());
            InjuryTypeDefinition lac = c.Settings.FindType(InjuryDefaults.Laceration);
            InjuryPartState arm = c.GetPart("LeftArm");
            float before = c.ChanceOf(lac, arm, DamageForm.Claw);
            object token = c.Resistances.Add(new InjuryResistance("Scales") { forms = DamageFormMask.Slash | DamageFormMask.Claw, injuryChance = -50f });
            Assert.AreEqual(before * 0.5f, c.ChanceOf(lac, arm, DamageForm.Claw), 1e-4f);
            Assert.AreEqual(lac.trigger.ChanceFor(DamageForm.Bite) * c.Settings.injuryChanceMultiplier, c.ChanceOf(lac, arm, DamageForm.Bite), 1e-4f, "bites are not covered");
            c.Resistances.Remove(token);
            Assert.AreEqual(before, c.ChanceOf(lac, arm, DamageForm.Claw), 1e-4f, "removed exactly");
        }

        [Test]
        public void Resistance_StrongBonesMakeMilderFractures()
        {
            InjurySettings s = Settings();
            InjuryTypeDefinition fracture = s.FindType(InjuryDefaults.Fracture);
            s.injuryTypes = new List<InjuryTypeDefinition> { fracture };
            fracture.trigger.chance = 1f;
            fracture.trigger.formChances = new DamageFormChances();
            fracture.trigger.formStrength = new DamageFormChances();
            InjuryController c = Controller(s);
            InjuryPartState arm = c.GetPart("LeftUpperArm");
            c.ApplyHit(arm, Hit(c, arm, 0.45f, DamageForm.Blunt));
            Assert.AreEqual(1, arm.Injuries.Count);
            Assert.AreEqual(1, arm.Injuries[0].StageIndex, "a broken bone");

            c.HealAll(true);
            c.Resistances.Add(new InjuryResistance("Strong Bones", InjuryDefaults.TagFracture) { severity = -40f });
            arm = c.GetPart("LeftUpperArm");
            c.ApplyHit(arm, Hit(c, arm, 0.45f, DamageForm.Blunt));
            Assert.AreEqual(1, arm.Injuries.Count);
            Assert.AreEqual(0, arm.Injuries[0].StageIndex, "only cracked");
        }

        [Test]
        public void Resistance_BleedingAndHealing()
        {
            InjuryController c = Controller(Settings());
            Injury cut = c.AddInjury("LeftArm", InjuryDefaults.Laceration);
            float bleed = cut.BleedingPerSecond;
            float heal = cut.HealRate;
            Assert.Greater(bleed, 0f);
            c.Resistances.Add(new InjuryResistance("Thick Blood") { bleeding = -50f, healing = 100f });
            c.RefreshFactors();
            Assert.AreEqual(bleed * 0.5f, cut.BleedingPerSecond, 1e-4f);
            Assert.AreEqual(heal * 2f, cut.HealRate, 1e-4f);

            c.Resistances.Add(new InjuryResistance("Skeleton") { bleeding = -100f });
            c.RefreshFactors();
            Assert.IsFalse(cut.IsBleeding, "nothing to bleed");
            Assert.AreEqual(0f, c.BleedingPerSecond, 1e-5f);
        }

        [Test]
        public void Resistance_DamageTakenByForm()
        {
            var go = Track(new GameObject("Skeleton"));
            var r = go.AddComponent<InjuryResistances>();
            r.Add(new InjuryResistance("Bones") { forms = DamageFormMask.Slash | DamageFormMask.Pierce, damageTaken = -50f });
            r.Add(new InjuryResistance("Brittle") { forms = DamageFormMask.Blunt, damageTaken = 50f });
            IDamageTakenModifier m = r;
            Assert.AreEqual(5f, m.ModifyDamageTaken(new DamageInfo { type = DamageType.Physical, form = DamageForm.Slash }, 10f), 1e-4f);
            Assert.AreEqual(15f, m.ModifyDamageTaken(new DamageInfo { type = DamageType.Physical, form = DamageForm.Blunt }, 10f), 1e-4f);
            Assert.AreEqual(10f, m.ModifyDamageTaken(new DamageInfo { type = DamageType.Magical }, 10f), 1e-4f, "spells are formless");
            Assert.AreEqual(10f, m.ModifyDamageTaken(new DamageInfo { type = DamageType.True, form = DamageForm.Slash }, 10f), 1e-4f, "true damage ignores it");
        }

        [Test]
        public void Resistance_MatchesIdsTagsAndParts()
        {
            InjurySettings s = Settings();
            var skull = new InjuryResistance("Thick Skull") { partKinds = new List<BodyPartKind> { BodyPartKind.Head } };
            Assert.IsTrue(skull.AppliesOn(s.FindPart("Head")));
            Assert.IsFalse(skull.AppliesOn(s.FindPart("LeftArm")));
            var wounds = new InjuryResistance("Tough Skin", InjuryDefaults.TagWound);
            Assert.IsTrue(wounds.AppliesTo(s.FindType(InjuryDefaults.Scratch)));
            Assert.IsTrue(wounds.AppliesTo(s.FindType(InjuryDefaults.BiteWound)));
            Assert.IsFalse(wounds.AppliesTo(s.FindType(InjuryDefaults.Fracture)));
            Assert.IsTrue(new InjuryResistance("x", "fracture").AppliesTo(s.FindType(InjuryDefaults.Fracture)), "by tag");
            Assert.IsTrue(new InjuryResistance("x", "Concussion").AppliesTo(s.FindType(InjuryDefaults.Concussion)), "by id");
            StringAssert.Contains("-40%", new InjuryResistance("Tough Skin", "wound") { injuryChance = -40f }.Describe());
        }

        [Test]
        public void Stats_AreAggregatePercentages()
        {
            foreach (TraitStat stat in InjuryResistances.InjuryStats)
            {
                Assert.IsTrue(TraitStats.IsAggregate(stat), stat.ToString());
                Assert.AreEqual(TraitModifierMode.Percent, TraitStats.Get(stat).EffectiveMode(TraitModifierMode.Flat), stat.ToString());
            }
            Assert.IsFalse(TraitStats.Get(TraitStat.WoundChance).higherIsBetter);
            Assert.IsTrue(TraitStats.Get(TraitStat.InjuryHealing).higherIsBetter);
            Assert.IsTrue(TraitModifier.Percent(TraitStat.FractureChance, -40f).IsBeneficial);
        }

        [Test]
        public void Presets_SkeletonNeverBleeds_AndMacesShatterIt()
        {
            InjuryController c = Controller(Settings());
            InjuryDefaults.ResistancePreset skeleton = InjuryDefaults.ResistancePresets().Find(p => p.name == "Skeleton");
            Assert.IsNotNull(skeleton.resistances);
            c.Resistances.Add(skeleton.resistances, 1f, "Skeleton");
            InjuryPartState arm = c.GetPart("LeftArm");
            Assert.AreEqual(0f, c.ChanceOf(c.Settings.FindType(InjuryDefaults.Laceration), arm, DamageForm.Slash), "no flesh to cut");
            InjuryTypeDefinition fracture = c.Settings.FindType(InjuryDefaults.Fracture);
            Assert.Greater(c.ChanceOf(fracture, arm, DamageForm.Blunt), fracture.trigger.ChanceFor(DamageForm.Blunt) * c.Settings.injuryChanceMultiplier);
            Assert.Less(c.Resistances.DamageMultiplier(DamageForm.Pierce), 1f);
            Assert.Greater(c.Resistances.DamageMultiplier(DamageForm.Blunt), 1f);
            foreach (InjuryDefaults.ResistancePreset p in InjuryDefaults.ResistancePresets())
                foreach (InjuryResistance r in p.resistances)
                    Assert.IsFalse(r.IsEmpty, $"{p.name}: {r.label}");
        }

        // ------------------------------------------------------------------ settings
        [Test]
        public void Defaults_AreValid_WithTheNewInjuries()
        {
            InjurySettings s = Settings();
            var errors = new List<string>();
            var warnings = new List<string>();
            s.Validate(errors, warnings);
            Assert.IsEmpty(errors, string.Join("\n", errors));
            foreach (string id in new[] { InjuryDefaults.Bruise, InjuryDefaults.Puncture, InjuryDefaults.BiteWound, InjuryDefaults.Concussion, InjuryDefaults.Frostbite })
                Assert.IsNotNull(s.FindType(id), id);
            foreach (InjuryTypeDefinition t in s.injuryTypes)
                foreach (InjuryStage st in t.stages)
                    if (!string.IsNullOrEmpty(st.worsensInto))
                        Assert.IsNotNull(s.FindType(st.worsensInto), $"{t.id} worsens into {st.worsensInto}");
            Assert.Less(s.injuryTypes.FindIndex(t => t.id == InjuryDefaults.BiteWound), s.injuryTypes.FindIndex(t => t.id == InjuryDefaults.Laceration),
                "a bite is a bite wound before it is a laceration");
            Assert.Greater(s.injuryTypes.FindIndex(t => t.id == InjuryDefaults.Bruise), s.injuryTypes.FindIndex(t => t.id == InjuryDefaults.Fracture),
                "a blow that broke nothing still bruises");
        }

        [Test]
        public void ApplyDamageForms_UpgradesAnOlderAsset_KeepingItsSettings()
        {
            InjurySettings s = Settings();
            // An asset from before damage forms: no new injuries, no odds by form, a custom value to keep.
            s.injuryTypes.RemoveAll(t => t.id == InjuryDefaults.Bruise || t.id == InjuryDefaults.Puncture || t.id == InjuryDefaults.BiteWound ||
                                         t.id == InjuryDefaults.Concussion || t.id == InjuryDefaults.Frostbite);
            foreach (InjuryTypeDefinition t in s.injuryTypes)
                if (t.trigger != null)
                {
                    t.trigger.formChances = new DamageFormChances();
                    t.trigger.formStrength = new DamageFormChances();
                }
            s.FindType(InjuryDefaults.Laceration).trigger.chance = 0.77f;
            foreach (InjuryBodyPart p in s.bodyParts)
                if (p.limbLoss != null)
                    p.limbLoss.formChances = new DamageFormChances();

            List<string> done = InjuryDefaults.ApplyDamageForms(s);
            Assert.IsNotEmpty(done);
            InjuryTypeDefinition lac = s.FindType(InjuryDefaults.Laceration);
            Assert.AreEqual(0.77f, lac.trigger.chance, "kept");
            Assert.IsFalse(lac.trigger.formChances.IsNeutral);
            Assert.IsNotNull(s.FindType(InjuryDefaults.BiteWound));
            Assert.Less(s.injuryTypes.FindIndex(t => t.id == InjuryDefaults.Bruise), s.injuryTypes.FindIndex(t => t.id == InjuryDefaults.Infection));
            Assert.Less(s.FindPart("LeftForearm").limbLoss.formChances.blunt, 0.5f);
            Assert.IsEmpty(InjuryDefaults.ApplyDamageForms(s).FindAll(x => x.StartsWith("Added")), "twice adds nothing more");
        }

        [Test]
        public void Snapshot_KeepsTheFormAndFactors()
        {
            InjuryController c = Controller(Settings());
            Injury cut = c.AddInjury("LeftArm", InjuryDefaults.Laceration);
            Assert.AreEqual(DamageForm.None, cut.Form, "added by a script, not a hit");
            c.Resistances.Add(new InjuryResistance("Thick Blood") { bleeding = -50f });
            c.RefreshFactors();
            byte[] data = c.CaptureSnapshot();
            Assert.IsNotNull(data);
            Assert.Greater(data.Length, 10);
        }
    }
}
#endif
