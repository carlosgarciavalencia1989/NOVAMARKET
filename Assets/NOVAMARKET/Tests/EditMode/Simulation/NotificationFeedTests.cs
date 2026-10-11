using System.Collections.Generic;
using NovaMarket.Simulation;
using NUnit.Framework;

namespace NovaMarket.Tests.Simulation
{
    public class NotificationFeedTests
    {
        [Test]
        public void Notice_ExpiresAfterItsLifetime()
        {
            var feed = new NotificationFeed(lifetimeSeconds: 5, maxVisible: 4);
            var expired = new List<HudNotification>();
            feed.Expired += expired.Add;

            HudNotification notice = feed.Push("Hola", NotificationSeverity.Info, now: 10);
            feed.Tick(14.9);
            Assert.AreEqual(1, feed.Active.Count);

            feed.Tick(15);
            Assert.AreEqual(0, feed.Active.Count);
            Assert.AreSame(notice, expired[0]);
        }

        [Test]
        public void Overflow_ExpiresTheOldestFirst()
        {
            var feed = new NotificationFeed(lifetimeSeconds: 5, maxVisible: 2);
            var expired = new List<string>();
            feed.Expired += n => expired.Add(n.Message);

            feed.Push("a", NotificationSeverity.Info, 0);
            feed.Push("b", NotificationSeverity.Info, 1);
            feed.Push("c", NotificationSeverity.Info, 2);

            CollectionAssert.AreEqual(new[] { "a" }, expired);
            Assert.AreEqual("b", feed.Active[0].Message);
            Assert.AreEqual("c", feed.Active[1].Message);
        }

        [Test]
        public void Tick_RemovesOnlyExpiredNotices()
        {
            var feed = new NotificationFeed(lifetimeSeconds: 5, maxVisible: 4);
            feed.Push("old", NotificationSeverity.Warning, 0);
            feed.Push("new", NotificationSeverity.Critical, 3);

            feed.Tick(6);

            Assert.AreEqual(1, feed.Active.Count);
            Assert.AreEqual("new", feed.Active[0].Message);
        }

        [Test]
        public void Ids_AreUnique()
        {
            var feed = new NotificationFeed();
            Assert.AreNotEqual(feed.Push("a", NotificationSeverity.Info, 0).Id, feed.Push("b", NotificationSeverity.Info, 0).Id);
        }
    }
}
