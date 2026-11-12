namespace DeltaZulu.LogCluster.Tests;

[TestClass]
public class MiningCorrectnessTests
{
    [TestMethod]
    public void Mine_ZeroAndRepeatedSequenceNumbersStillCountEveryRecord()
    {
        var records = new[]
        {
            new LogRecord(0, "service ready", "test"),
            new LogRecord(0, "service ready", "test"),
            new LogRecord(7, "service ready", "test"),
            new LogRecord(7, "service ready", "test")
        };
        var options = new LogClusterOptions { MinSupport = 2, ShowOutliers = true };
        var result = new LogClusterMiner(options).Mine(records);
        Assert.AreEqual(4, result.RecordCount);
        Assert.AreEqual(4, result.Candidates.Single().Support);
        Assert.AreEqual(0, result.OutlierCount);
    }

    [TestMethod]
    public void Mine_GrowingStreamDoesNotProcessRecordsAddedAfterFirstPass()
    {
        var passes = 0;
        IEnumerable<LogRecord> Source()
        {
            passes++;
            yield return new LogRecord(1, "service ready", "test");
            yield return new LogRecord(2, "service ready", "test");
            if (passes > 1)
            {
                yield return new LogRecord(3, "unexpected newtoken", "test");
            }
        }

        var options = new LogClusterOptions { MinSupport = 2, ForceMaterialize = false, ShowOutliers = true };
        var result = new LogClusterMiner(options).Mine(Source, estimatedInputBytes: 0);
        Assert.AreEqual(2, result.RecordCount);
        Assert.AreEqual(2, result.Candidates.Single().Support);
        Assert.AreEqual(0, result.OutlierCount);
    }

    [TestMethod]
    public void Mine_OverlappingVariantGroupsNeverDuplicateSupport()
    {
        var records = new List<LogRecord>();
        var id = 0;
        foreach (var left in new[] { "up", "down" })
        {
            foreach (var right in new[] { "accept", "reject" })
            {
                for (var n = 0; n < 3; n++)
                {
                    records.Add(new LogRecord(++id, $"status {left} event {right} done", "test"));
                }
            }
        }

        foreach (var stream in new[] { true, false })
        {
            var options = new LogClusterOptions { MinSupport = 2, ShowOutliers = true, ForceMaterialize = !stream };
            var result = new LogClusterMiner(options).Mine(() => records, estimatedInputBytes: 0);
            Assert.AreEqual(records.Count, result.RecordCount);
            Assert.AreEqual(records.Count, result.Candidates.Sum(c => c.Support) + result.OutlierCount);
        }
    }

    [TestMethod]
    public void Mine_StreamAndMaterializeHaveEqualPatternsAndSupportOnOverlappingGroups()
    {
        var records = Enumerable.Range(0, 16)
            .Select(i => new LogRecord(i, $"device {(i % 2 == 0 ? "up" : "down")} code {(i % 4 < 2 ? "accept" : "reject")} done", "test"))
            .ToArray();
        MiningResult Run(bool materialize) => new LogClusterMiner(new LogClusterOptions
        {
            MinSupport = 2,
            ForceMaterialize = materialize,
            ShowOutliers = true
        }).Mine(() => records, estimatedInputBytes: 0);
        var a = Run(true);
        var b = Run(false);
        Assert.AreEqual(a.RecordCount, b.RecordCount);
        Assert.AreEqual(a.OutlierCount, b.OutlierCount);
        Assert.AreSequenceEqual(a.Candidates.Select(x => (x.LogClusterPattern, x.Support)).ToArray(),
            b.Candidates.Select(x => (x.LogClusterPattern, x.Support)).ToArray());
    }
}
