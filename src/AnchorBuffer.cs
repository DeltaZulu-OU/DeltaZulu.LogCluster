namespace DeltaZulu.LogCluster;

internal static class AnchorBuffer
{
    public static int[] From(ReadOnlySpan<int> tokens, ReadOnlySpan<bool> frequentWords)
    {
        var count = 0;
        foreach (var token in tokens)
        {
            if (frequentWords[token])
            {
                count++;
            }
        }
        if (count == 0)
        {
            return [];
        }

        var anchors = new int[count];
        var index = 0;
        foreach (var token in tokens)
        {
            if (frequentWords[token])
            {
                anchors[index++] = token;
            }
        }
        return anchors;
    }
}
