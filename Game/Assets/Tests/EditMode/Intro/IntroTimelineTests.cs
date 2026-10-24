using System;
using System.Collections.Generic;
using Boki.Intro;
using NUnit.Framework;

namespace Boki.Tests
{
    public sealed class IntroTimelineTests
    {
        private static IntroTimeline Make(out List<int> started)
        {
            var timeline = new IntroTimeline(new[] { 2f, 1f, 3f });
            var startedShots = new List<int>();
            timeline.ShotStarted += startedShots.Add;
            started = startedShots;
            return timeline;
        }

        [Test]
        public void BeginStartsTheFirstShotOnce()
        {
            var timeline = Make(out var started);
            Assert.That(timeline.Begin(), Is.True);
            Assert.That(timeline.Begin(), Is.False);
            Assert.That(started, Is.EqualTo(new[] { 0 }));
        }

        [Test]
        public void AdvanceReportsProgressInsideTheCurrentShot()
        {
            var timeline = Make(out _);
            timeline.Begin();
            timeline.Advance(0.5f);
            Assert.That(timeline.ShotIndex, Is.Zero);
            Assert.That(timeline.ShotProgress, Is.EqualTo(0.25f).Within(1e-5f));
        }

        [Test]
        public void ADeltaSpanningSeveralShotsStartsEachOneInOrder()
        {
            var timeline = Make(out var started);
            timeline.Begin();
            timeline.Advance(3.5f);
            Assert.That(started, Is.EqualTo(new[] { 0, 1, 2 }));
            Assert.That(timeline.ShotIndex, Is.EqualTo(2));
            Assert.That(timeline.ShotProgress, Is.EqualTo(0.5f / 3f).Within(1e-5f));
        }

        [Test]
        public void PlayingToTheEndFinishesOnceWithoutASkip()
        {
            var timeline = new IntroTimeline(new[] { 1f, 1f });
            int finished = 0;
            timeline.Finished += () => finished++;
            timeline.Begin();
            timeline.Advance(2f);
            timeline.Advance(5f);
            Assert.That(timeline.TrySkip(), Is.False);
            Assert.That(finished, Is.EqualTo(1));
            Assert.That(timeline.IsFinished, Is.True);
            Assert.That(timeline.WasSkipped, Is.False);
            Assert.That(timeline.ShotProgress, Is.EqualTo(1f));
        }

        [Test]
        public void SkipFinishesOnceAndStopsFurtherShots()
        {
            var timeline = new IntroTimeline(new[] { 2f, 1f });
            var started = new List<int>();
            int finished = 0;
            timeline.ShotStarted += started.Add;
            timeline.Finished += () => finished++;
            timeline.Begin();
            timeline.Advance(0.5f);
            Assert.That(timeline.TrySkip(), Is.True);
            Assert.That(timeline.TrySkip(), Is.False);
            timeline.Advance(10f);
            Assert.That(finished, Is.EqualTo(1));
            Assert.That(started, Is.EqualTo(new[] { 0 }));
            Assert.That(timeline.WasSkipped, Is.True);
        }

        [Test]
        public void SkipBeforeBeginIsRejected()
        {
            var timeline = Make(out _);
            Assert.That(timeline.TrySkip(), Is.False);
            Assert.That(timeline.IsFinished, Is.False);
        }

        [Test]
        public void InvalidDeltasAndAdvancingBeforeBeginAreIgnored()
        {
            var timeline = Make(out _);
            timeline.Advance(1f);
            timeline.Begin();
            timeline.Advance(0f);
            timeline.Advance(-1f);
            timeline.Advance(float.NaN);
            Assert.That(timeline.ShotIndex, Is.Zero);
            Assert.That(timeline.ShotProgress, Is.Zero);
        }

        [Test]
        public void ConstructorRejectsMissingOrNonPositiveShots()
        {
            Assert.Throws<ArgumentException>(() => new IntroTimeline(null));
            Assert.Throws<ArgumentException>(() => new IntroTimeline(new float[0]));
            Assert.Throws<ArgumentOutOfRangeException>(() => new IntroTimeline(new[] { 1f, 0f }));
            Assert.Throws<ArgumentOutOfRangeException>(() => new IntroTimeline(new[] { float.NaN }));
            Assert.Throws<ArgumentOutOfRangeException>(() => new IntroTimeline(new[] { float.PositiveInfinity }));
        }
    }
}
