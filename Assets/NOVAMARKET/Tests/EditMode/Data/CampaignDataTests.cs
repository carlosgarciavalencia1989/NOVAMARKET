using System.Linq;
using NovaMarket.Data;
using NovaMarket.Simulation.Campaign;
using NUnit.Framework;
using UnityEditor;

namespace NovaMarket.Tests.Data
{
    /// <summary>Checks the real data assets in Data/ (Campaign.asset and what it references).</summary>
    public class CampaignDataTests
    {
        private const string CampaignPath = "Assets/NOVAMARKET/Data/Campaign.asset";

        private CampaignCatalog catalog;

        [SetUp]
        public void SetUp()
        {
            var database = AssetDatabase.LoadAssetAtPath<CampaignDatabase>(CampaignPath);
            Assert.IsNotNull(database, CampaignPath + " is missing.");
            Assert.IsTrue(database.Roles.All(r => r != null) && database.Sectors.All(s => s != null)
                && database.Characters.All(c => c != null), "The database has empty slots.");
            catalog = database.ToCatalog();
        }

        [Test]
        public void Data_IsValid() => CollectionAssert.IsEmpty(catalog.Validate());

        [Test]
        public void OnlyPlannerAndCosmeticsAreEnabled()
        {
            CollectionAssert.AreEqual(new[] { "planeador" }, catalog.Roles.Where(r => r.IsPlayable).Select(r => r.Id));
            CollectionAssert.AreEqual(new[] { "cosmetica" }, catalog.Sectors.Where(s => s.IsPlayable).Select(s => s.Id));
            Assert.IsTrue(catalog.CanPlay("planeador", "cosmetica"));
        }

        [Test]
        public void Roles_AreTheEightLinksOfTheChainMap()
        {
            Assert.AreEqual(8, catalog.Roles.Count);
            Assert.AreEqual(1, catalog.Roles.Count(r => r.Lane == ChainLane.Plan));
            Assert.AreEqual(6, catalog.Roles.Count(r => r.Lane == ChainLane.Execute));
            Assert.AreEqual(1, catalog.Roles.Count(r => r.Lane == ChainLane.Return));
            CollectionAssert.AreEqual(new[] { "Pronóstico", "Gestión de la demanda", "Materiales (MRP · DDMRP)" },
                catalog.Role("planeador").Worlds);
        }

        [Test]
        public void Sectors_HaveTheirPortfolioSizes()
        {
            Assert.AreEqual(5, catalog.Sectors.Count);
            Assert.AreEqual(200, catalog.Sector("cosmetica").PortfolioSize);
        }

        [Test]
        public void PlannerCast_HasEveryCharacterAndValeriaOverestimates()
        {
            var cast = catalog.CreateCast("planeador");

            Assert.AreEqual(10, cast.Count);
            CharacterProfile valeria = catalog.Character("valeria");
            Assert.AreEqual(0.05, valeria.BiasMin, 1e-6);
            Assert.AreEqual(0.50, valeria.BiasMax, 1e-6);
            Assert.AreEqual("inventarios", catalog.Character("sofia").LinkedRoleId);
            Assert.IsNull(catalog.Character("andres").LinkedRoleId);
        }
    }
}
