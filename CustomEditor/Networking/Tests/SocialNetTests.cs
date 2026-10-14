#if FISHNET && UNITY_INCLUDE_TESTS && UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;

namespace GameNet.Tests
{
    /// <summary>Edit Mode tests for the social relay's pure parts (<see cref="NetSocial"/>): party grouping and the inspect message.</summary>
    public class SocialNetTests
    {
        [Test]
        public void MembersAreGroupedIntoTheirParties()
        {
            var members = new List<NetSocial.PartyMemberView>
            {
                new NetSocial.PartyMemberView { party = 4, leader = 10, member = 10 },
                new NetSocial.PartyMemberView { party = 4, leader = 10, member = 11 },
                new NetSocial.PartyMemberView { party = 7, leader = 21, member = 20 },
                new NetSocial.PartyMemberView { party = 7, leader = 21, member = 21 },
                new NetSocial.PartyMemberView { party = 4, leader = 10, member = 12 },
                new NetSocial.PartyMemberView { party = 0, leader = 1, member = 1 }, // invalid: dropped
            };
            List<PartySystem.PartySnapshot> parties = NetSocial.GroupParties(members);
            Assert.AreEqual(2, parties.Count);
            Assert.AreEqual(4, parties[0].id);
            Assert.AreEqual(10, parties[0].leader);
            CollectionAssert.AreEqual(new[] { 10, 11, 12 }, parties[0].members);
            Assert.AreEqual(7, parties[1].id);
            Assert.AreEqual(21, parties[1].leader);
            CollectionAssert.AreEqual(new[] { 20, 21 }, parties[1].members);
        }

        [Test]
        public void ViewsCompareByValue()
        {
            var a = new NetSocial.SocialView { player = 3, guild = "Knights", tag = "KNT", alliance = "", fame = 5, title = "", allowInspect = true, level = 7 };
            NetSocial.SocialView b = a;
            Assert.IsTrue(a.Equals(b));
            b.level = 8;
            Assert.IsFalse(a.Equals(b));
            var i = new NetSocial.InviteView { from = 1, to = 2, party = 0, expires = 30d };
            NetSocial.InviteView j = i;
            Assert.IsTrue(i.Equals(j));
            j.expires = 31d;
            Assert.IsFalse(i.Equals(j));
        }

        [Test]
        public void InspectionSurvivesTheWire()
        {
            var data = new InspectData
            {
                playerId = 9, error = "", name = "Ana", title = "the Brave", guild = "Knights", guildTag = "KNT", alliance = "North",
                fame = 12, level = 30, race = "Human", className = "Warrior", health = 80f, maxHealth = 120f,
                stats = new[] { new InspectStat { label = "Strength", value = "14" } },
                equipment = new[] { new InspectItem { slot = "Helmet", itemId = "helm_01", itemName = "Iron Helm" } },
            };
            InspectData back = NetSocial.FromMessage(NetSocial.ToMessage(data));
            Assert.IsTrue(back.Ok);
            Assert.AreEqual("Ana", back.name);
            Assert.AreEqual("KNT", back.guildTag);
            Assert.AreEqual(30, back.level);
            Assert.AreEqual(120f, back.maxHealth);
            Assert.AreEqual(1, back.stats.Length);
            Assert.AreEqual("14", back.stats[0].value);
            Assert.AreEqual(1, back.equipment.Length);
            Assert.AreEqual("helm_01", back.equipment[0].itemId);

            InspectData failed = NetSocial.FromMessage(NetSocial.ToMessage(InspectData.Failed(9, "Too far away to inspect.")));
            Assert.IsFalse(failed.Ok);
            Assert.AreEqual("Too far away to inspect.", failed.error);
            Assert.AreEqual(0, failed.equipment.Length);
        }
    }
}
#endif
