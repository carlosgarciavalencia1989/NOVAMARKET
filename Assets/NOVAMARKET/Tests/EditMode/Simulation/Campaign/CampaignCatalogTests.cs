using System.Linq;
using NovaMarket.Simulation.Campaign;
using NUnit.Framework;

namespace NovaMarket.Tests.Simulation.Campaign
{
    public class CampaignCatalogTests
    {
        private static CampaignCatalog Sample() => new CampaignCatalog(
            new[]
            {
                new RoleDefinition("planeador", "Planeador", ChainLane.Plan, "", true, new[] { "Pronóstico" }),
                new RoleDefinition("abastecer", "Abastecer", ChainLane.Execute, "", false)
            },
            new[]
            {
                new SectorDefinition("cosmetica", "Cosmética", "", 200, "", true),
                new SectorDefinition("repuestos", "Repuestos", "", 500, "", false)
            },
            new[]
            {
                new CharacterProfile("andres", "Andrés", "Director de S&OP", null, "", ""),
                new CharacterProfile("paula", "Paula", "Compras", "abastecer", "", "")
            });

        [Test]
        public void OnlyEnabledRoleAndSectorTogetherCanBePlayed()
        {
            CampaignCatalog catalog = Sample();

            Assert.IsTrue(catalog.CanPlay("planeador", "cosmetica"));
            Assert.IsFalse(catalog.CanPlay("planeador", "repuestos"));
            Assert.IsFalse(catalog.CanPlay("abastecer", "cosmetica"));
            Assert.IsFalse(catalog.CanPlay("nadie", "cosmetica"));
        }

        [Test]
        public void Cast_LeavesOutTheCharactersOfThePlayersRole()
        {
            CampaignCatalog catalog = Sample();

            CollectionAssert.AreEquivalent(new[] { "andres", "paula" },
                catalog.CreateCast("planeador").Select(c => c.Profile.Id));
            CollectionAssert.AreEquivalent(new[] { "andres" },
                catalog.CreateCast("abastecer").Select(c => c.Profile.Id), "Playing Abastecer, the player is Paula.");
        }

        [Test]
        public void Validate_FindsDuplicatedIdsAndUnknownRoles()
        {
            var catalog = new CampaignCatalog(
                new[]
                {
                    new RoleDefinition("planeador", "Planeador", ChainLane.Plan, "", true, new[] { "Pronóstico" }),
                    new RoleDefinition("planeador", "Otro", ChainLane.Plan, "", false)
                },
                new SectorDefinition[0],
                new[] { new CharacterProfile("wilmer", "Wilmer", "Transporte", "transporte", "", "") });

            var errors = catalog.Validate();

            Assert.AreEqual(2, errors.Count);
            Assert.IsTrue(errors.Any(e => e.Contains("Duplicated role")));
            Assert.IsTrue(errors.Any(e => e.Contains("unknown role 'transporte'")));
        }

        [Test]
        public void Trust_StartsAtTheProfileValueAndStaysWithin0And100()
        {
            var npc = new NpcCharacter(new CharacterProfile("valeria", "Valeria", "Comercial", null, "", "",
                0.05, 0.5, initialTrust: 40));
            int changes = 0;
            npc.TrustChanged += _ => changes++;

            npc.ChangeTrust(15);
            Assert.AreEqual(55, npc.Trust);
            npc.ChangeTrust(200);
            Assert.AreEqual(100, npc.Trust);
            npc.ChangeTrust(10);
            npc.ChangeTrust(-150);
            Assert.AreEqual(0, npc.Trust);
            Assert.IsTrue(npc.HasLowTrust);
            Assert.AreEqual(3, changes, "No event when the value does not move.");
        }

        [Test]
        public void Initials_UseTheFirstLetterOrDonPlusName()
        {
            Assert.AreEqual("A", new CharacterProfile("andres", "Andrés", "", null, "", "").Initials);
            Assert.AreEqual("DH", new CharacterProfile("don-hernan", "Don Hernán", "", null, "", "").Initials);
        }

        [Test]
        public void Profile_RejectsAnInvertedBiasRange()
        {
            Assert.Throws<System.ArgumentException>(() =>
                new CharacterProfile("x", "X", "", null, "", "", biasMin: 0.5, biasMax: 0.1));
        }
    }
}
