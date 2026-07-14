using DeltaZulu.LogCluster.Cli;

namespace DeltaZulu.LogCluster.Tests;

[TestClass]
public class LogClusterMinerTests
{
    /// <summary>
    /// No motif regex tolerates the internal space a 2+-word sample always contains once
    /// joined, so a gap that ever sees 2+ words can never accumulate a parser vote for that
    /// observation; MaxWords > 1 therefore always drags confidence below 1.0 and this branch
    /// always fires. This test pins that invariant directly at the GapStatistics level,
    /// rather than only indirectly through LogClusterMiner.Mine's rendering.
    /// </summary>
    [TestMethod]
    public void GapStatistics_MultiWordGap_IsAlwaysForcedToRestRegardlessOfConfidence()
    {
        var dictionary = new TokenDictionary();
        var gap = new GapStatistics(maxSamples: 8);
        var first = dictionary.GetOrAdd("10", 0, 2);
        var second = dictionary.GetOrAdd("0", 0, 1);
        gap.Observe([first, second], dictionary);
        gap.Observe([first, second], dictionary);

        var output = gap.ToOutput();

        Assert.AreEqual(2, output.MaxWords);
        Assert.AreEqual("rest", output.SuggestedParser);
        Assert.AreEqual(0.0, output.ParserConfidence, 0.001);
    }

    [TestMethod]
    public void GapStatistics_NoObservations_ReturnsNullParserAndZeroConfidence()
    {
        var output = new GapStatistics(maxSamples: 8).ToOutput();

        Assert.AreEqual(0, output.MinWords);
        Assert.AreEqual(0, output.MaxWords);
        Assert.AreEqual(0, output.Observations);
        Assert.IsNull(output.SuggestedParser);
        Assert.AreEqual(0.0, output.ParserConfidence, 0.001);
    }

    [TestMethod]
    public void GapStatistics_SingleWordConsistentMotif_KeepsSpecificParserWithFullConfidence()
    {
        var dictionary = new TokenDictionary();
        var gap = new GapStatistics(maxSamples: 8);
        foreach (var address in new[] { "10.0.0.1", "10.0.0.2", "10.0.0.3" })
        {
            var token = dictionary.GetOrAdd(address, 0, address.Length);
            gap.Observe([token], dictionary);
        }

        var output = gap.ToOutput();

        Assert.AreEqual(1, output.MaxWords);
        Assert.AreEqual("ipv4", output.SuggestedParser);
        Assert.AreEqual(1.0, output.ParserConfidence, 0.001);
    }

    [TestMethod]
    public void InternalMultiwordGaps_AreRenderedAsUnresolvedSketchesNotRestParsers()
    {
        var options = new LogClusterOptions {
            MinSupport = 2
        };

        var records = new[] {
            new LogRecord(1, "Interface Ethernet 1 down at node node1", "test"),
            new LogRecord(2, "Interface Ethernet 1 2 down at node node2", "test"),
        };

        var result = new LogClusterMiner(options).Mine(records);
        var candidate = result.Candidates.Single(c => c.LogClusterPattern.StartsWith("Interface", StringComparison.Ordinal));

        Assert.IsFalse(candidate.IsExecutableRule);
        Assert.Contains("/* unresolved gap:", candidate.LiblognormRule);
        Assert.DoesNotContain("%field1:rest% down at node", candidate.LiblognormRule);
        Assert.IsNotEmpty(candidate.RuleWarnings);
    }

    /// <summary>
    /// Regression test: the rule builder used to label this warning with the executable
    /// %fieldN% counter, which is only incremented when a placeholder is actually emitted.
    /// Because the unresolved gap emits a comment instead of a placeholder, the counter stayed
    /// unadvanced and got reused by the next real field -- e.g. the warning said "Internal gap 1"
    /// while the rule's actual %field1% referred to the unrelated, perfectly valid trailing gap.
    /// The warning must instead be numbered by the gap's own position so it never collides with
    /// an unrelated %fieldN% placeholder.
    /// </summary>
    [TestMethod]
    public void UnresolvedGapWarning_ReferencesItsOwnGapPositionNotAnUnrelatedFieldNumber()
    {
        var options = new LogClusterOptions {
            MinSupport = 2
        };

        var records = new[] {
            new LogRecord(1, "Interface Ethernet 1 down at node node1", "test"),
            new LogRecord(2, "Interface Ethernet 1 2 down at node node2", "test"),
        };

        var result = new LogClusterMiner(options).Mine(records);
        var candidate = result.Candidates.Single(c => c.LogClusterPattern.StartsWith("Interface", StringComparison.Ordinal));

        Assert.AreEqual("Interface Ethernet 1 /* unresolved gap: 0-1 words */ down at node %field1:word%", candidate.LiblognormRule);
        Assert.Contains("Gap 4 spans 0-1 words", candidate.RuleWarnings.Single());
    }

    [TestMethod]
    public void Mine_CustomScoreWeightsChangeTheTotalScore()
    {
        var records = new[] {
            new LogRecord(1, "Interface Ethernet 1 down at node node1", "test"),
            new LogRecord(2, "Interface Ethernet 1 down at node node2", "test"),
        };

        var defaultOptions = new LogClusterOptions {
            MinSupport = 2
        };

        var customOptions = new LogClusterOptions {
            MinSupport = 2,
            WeightSupport = 1,
            WeightAnchor = 0,
            WeightGapConsistency = 0,
            WeightSpecificity = 0
        };

        var defaultScore = new LogClusterMiner(defaultOptions).Mine(records).Candidates.Single().Score;
        var customScore = new LogClusterMiner(customOptions).Mine(records).Candidates.Single().Score;

        Assert.AreNotEqual(defaultScore.Total, customScore.Total);
        Assert.AreEqual(customScore.Support, customScore.Total, 0.001);
    }

    [TestMethod]
    public void Mine_DoesNotMergeHighDiversitySingleTokenVariants()
    {
        var options = new LogClusterOptions {
            MinSupport = 2,
            WordWeightThreshold = 0.01
        };
        var records = new[] {
            new LogRecord(1, "alert from ip1 triggered", "test"),
            new LogRecord(2, "alert from ip1 triggered", "test"),
            new LogRecord(3, "alert from ip2 triggered", "test"),
            new LogRecord(4, "alert from ip2 triggered", "test"),
        };

        var result = new LogClusterMiner(options).Mine(records);
        var matches = result.Candidates.Where(c => c.LogClusterPattern.StartsWith("alert from", StringComparison.Ordinal)).ToArray();

        Assert.HasCount(2, matches);
    }

    [TestMethod]
    public void Mine_DoesNotTrackOutliersWhenOptionIsOff()
    {
        var options = new LogClusterOptions {
            MinSupport = 2
        };
        var records = new[] {
            new LogRecord(1, "Interface down node1", "test"),
            new LogRecord(2, "Interface down node2", "test"),
            new LogRecord(3, "a completely unrelated one-off message", "test"),
        };

        var result = new LogClusterMiner(options).Mine(records);

        Assert.AreEqual(0, result.OutlierCount);
        Assert.IsEmpty(result.OutlierSamples);
    }

    [TestMethod]
    public void Mine_MaterializeAndStreamStrategiesProduceIdenticalOutput()
    {
        var records = new[] {
            new LogRecord(1, "Interface Ethernet 1 down at node node1", "test"),
            new LogRecord(2, "Interface Ethernet 1 down at node node2", "test"),
            new LogRecord(3, "Interface Ethernet 1 down at node node3", "test"),
        };

        var materialized = new LogClusterMiner(new LogClusterOptions { ForceMaterialize = true }).Mine(() => records, estimatedInputBytes: long.MaxValue);
        var streamed = new LogClusterMiner(new LogClusterOptions { ForceMaterialize = false }).Mine(() => records, estimatedInputBytes: 0);

        Assert.AreEqual(materialized.RecordCount, streamed.RecordCount);
        Assert.HasCount(materialized.Candidates.Count, streamed.Candidates);
        Assert.AreSequenceEqual(
            materialized.Candidates.Select(c => c.LogClusterPattern).ToArray(), streamed.Candidates.Select(c => c.LogClusterPattern).ToArray());
        Assert.AreSequenceEqual(
            materialized.Candidates.Select(c => c.Support).ToArray(), streamed.Candidates.Select(c => c.Support).ToArray());
    }

    [TestMethod]
    public void Mine_MergesLowDiversitySingleTokenVariantsIntoOneCandidate()
    {
        var options = new LogClusterOptions {
            MinSupport = 2
        };
        var records = new[] {
            new LogRecord(1, "alert from ip1 triggered", "test"),
            new LogRecord(2, "alert from ip1 triggered", "test"),
            new LogRecord(3, "alert from ip2 triggered", "test"),
            new LogRecord(4, "alert from ip2 triggered", "test"),
            new LogRecord(5, "alert from ip3 triggered", "test"),
            new LogRecord(6, "alert from ip3 triggered", "test"),
        };

        var result = new LogClusterMiner(options).Mine(records);
        var matches = result.Candidates.Where(c => c.LogClusterPattern.StartsWith("alert from", StringComparison.Ordinal)).ToArray();

        Assert.HasCount(1, matches);
        var merged = matches[0];
        Assert.AreEqual(6, merged.Support);
        Assert.AreEqual("alert from *{1,1} triggered", merged.LogClusterPattern);
        var wildcardedGap = merged.Gaps[2];
        Assert.AreEqual(1, wildcardedGap.MinWords);
        Assert.AreEqual(1, wildcardedGap.MaxWords);
        Assert.AreSequenceEqual(new[] { "ip1", "ip2", "ip3" }, wildcardedGap.Samples.ToArray(), Microsoft.VisualStudio.TestTools.UnitTesting.SequenceOrder.InAnyOrder);
    }

    [TestMethod]
    public void Mine_MergesTrailingAnchorShiftedVariantsIntoOneCandidate()
    {
        var options = new LogClusterOptions {
            MinSupport = 2
        };
        var records = new[] {
            new LogRecord(1, "Interface down node1", "test"),
            new LogRecord(2, "Interface down node2", "test"),
            new LogRecord(3, "Interface down node3 restart", "test"),
            new LogRecord(4, "Interface down node4 restart", "test"),
        };

        var result = new LogClusterMiner(options).Mine(records);
        var matches = result.Candidates.Where(c => c.LogClusterPattern.StartsWith("Interface down", StringComparison.Ordinal)).ToArray();

        Assert.HasCount(1, matches);
        var merged = matches[0];
        Assert.AreEqual(4, merged.Support);
        var trailingGap = merged.Gaps[^1];
        Assert.AreEqual(1, trailingGap.MinWords);
        Assert.AreEqual(2, trailingGap.MaxWords);
    }

    [TestMethod]
    public void Mine_PreservesOriginalTabDelimiterInRenderedPattern()
    {
        var options = new LogClusterOptions {
            MinSupport = 2
        };
        var records = new[] {
            new LogRecord(1, "user1\tlogin\tsuccess", "test"),
            new LogRecord(2, "user2\tlogin\tsuccess", "test"),
        };

        var result = new LogClusterMiner(options).Mine(records);
        var candidate = result.Candidates.Single();

        Assert.AreEqual("*{1,1}\tlogin\tsuccess", candidate.LogClusterPattern);
    }

    [TestMethod]
    public void Mine_ReportsLinesMatchingNoSurvivingCandidateAsOutliers()
    {
        var options = new LogClusterOptions {
            MinSupport = 2,
            ShowOutliers = true
        };
        var records = new[] {
            new LogRecord(1, "Interface down node1", "test"),
            new LogRecord(2, "Interface down node2", "test"),
            new LogRecord(3, "a completely unrelated one-off message", "test"),
        };

        var result = new LogClusterMiner(options).Mine(records);

        Assert.AreEqual(1, result.OutlierCount);
        Assert.AreEqual("a completely unrelated one-off message", result.OutlierSamples.Single());
    }

    [TestMethod]
    public void Mine_ThrowsWhenInputBytesExceedMaxInputBytes()
    {
        var options = new LogClusterOptions { MaxInputBytes = 2 };
        var records = new[] {
            new LogRecord(1, "this line is definitely over ten bytes", "test"),
        };

        Assert.ThrowsExactly<LogClusterInputTooLargeException>(() => new LogClusterMiner(options).Mine(records));
    }

    [TestMethod]
    public void Mine_ThrowsWhenRecordCountExceedsMaxRecords()
    {
        var options = new LogClusterOptions { MaxRecords = 2 };
        var records = new[] {
            new LogRecord(1, "line one", "test"),
            new LogRecord(2, "line two", "test"),
            new LogRecord(3, "line three", "test"),
        };

        Assert.ThrowsExactly<LogClusterInputTooLargeException>(() => new LogClusterMiner(options).Mine(records));
    }

    [TestMethod]
    public void Mine_TrailingGapWithContentKeepsSeparatorBeforePlaceholder()
    {
        var options = new LogClusterOptions {
            MinSupport = 2
        };
        var records = new[] {
            new LogRecord(1, "Interface down node1", "test"),
            new LogRecord(2, "Interface down node2", "test"),
        };

        var result = new LogClusterMiner(options).Mine(records);
        var candidate = result.Candidates.Single();

        Assert.AreEqual("Interface down *{1,1}", candidate.LogClusterPattern);
        Assert.AreEqual("Interface down %field1:word%", candidate.LiblognormRule);
    }

    /// <summary>
    /// Regression test: --json used to throw System.InvalidOperationException at runtime
    /// ("Reflection-based serialization has been disabled for this application") because the CLI
    /// project sets PublishAot=true, which disables System.Text.Json's reflection-based
    /// serializer via a runtimeconfig feature switch that applies even to plain `dotnet run`, not
    /// only to AOT-published binaries. The fix (source-generated LogClusterJsonContext) must
    /// actually be reachable from a normal JIT run, not just compile.
    /// </summary>
    [TestMethod]
    public void SerializeJson_WithoutOutliers_DoesNotThrowAndUsesCamelCaseFieldNames()
    {
        var records = new[] {
            new LogRecord(1, "Interface down node1", "test"),
            new LogRecord(2, "Interface down node2", "test"),
        };
        var result = new LogClusterMiner(new LogClusterOptions { MinSupport = 2 }).Mine(records);

        var json = Program.SerializeJson(result, includeOutliers: false);

        Assert.Contains("\"support\"", json);
        Assert.Contains("\"logClusterPattern\"", json);
        Assert.DoesNotContain("\"Support\"", json);
    }

    [TestMethod]
    public void SerializeJson_WithOutliers_IncludesOutlierCountAndSamples()
    {
        var records = new[] {
            new LogRecord(1, "Interface down node1", "test"),
            new LogRecord(2, "Interface down node2", "test"),
            new LogRecord(3, "a completely unrelated one-off message", "test"),
        };
        var result = new LogClusterMiner(new LogClusterOptions { MinSupport = 2, ShowOutliers = true }).Mine(records);

        var json = Program.SerializeJson(result, includeOutliers: true);

        Assert.Contains("\"outlierCount\": 1", json);
        Assert.Contains("a completely unrelated one-off message", json);
    }

    [TestMethod]
    public void ShouldStream_ForcedOptionsOverrideTheHeuristic()
    {
        Assert.IsFalse(LogClusterMiner.ShouldStream(estimatedInputBytes: long.MaxValue, new LogClusterOptions { ForceMaterialize = true }));
        Assert.IsTrue(LogClusterMiner.ShouldStream(estimatedInputBytes: 0, new LogClusterOptions { ForceMaterialize = false }));
    }

    [TestMethod]
    public void ShouldStream_LargeEstimateWithoutOverrideStreams()
    {
        var options = new LogClusterOptions();
        Assert.IsTrue(LogClusterMiner.ShouldStream(estimatedInputBytes: long.MaxValue / 8, options));
        Assert.IsFalse(LogClusterMiner.ShouldStream(estimatedInputBytes: 1, options));
    }
}
