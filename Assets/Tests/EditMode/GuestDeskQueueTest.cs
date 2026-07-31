using NUnit.Framework;
using UnityEngine;

public class GuestDeskQueueTest
{
    [Test]
    public void QueuePositions_AreEvenlySpacedBehindCounter()
    {
        Vector3 counter = new Vector3(1f, 0f, 2f);
        Vector3 direction = new Vector3(1f, 0f, -1f);

        Vector3 first = GuestLifeDirector.DeskQueuePosition(counter, direction, 0.8f, 0);
        Vector3 second = GuestLifeDirector.DeskQueuePosition(counter, direction, 0.8f, 1);
        Vector3 third = GuestLifeDirector.DeskQueuePosition(counter, direction, 0.8f, 2);

        Assert.That(first, Is.EqualTo(counter));
        Assert.That(Vector3.Distance(first, second), Is.EqualTo(0.8f).Within(0.001f));
        Assert.That(Vector3.Distance(second, third), Is.EqualTo(0.8f).Within(0.001f));
        Assert.That(second.x, Is.GreaterThan(first.x));
        Assert.That(second.z, Is.LessThan(first.z));
    }

    [Test]
    public void QueuePosition_ClampsNegativeIndexAndSpacing()
    {
        Vector3 counter = new Vector3(1f, 2f, 3f);
        Vector3 result = GuestLifeDirector.DeskQueuePosition(
            counter,
            Vector3.zero,
            -2f,
            -1);

        Assert.That(result, Is.EqualTo(counter));
    }
}
