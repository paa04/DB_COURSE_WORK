namespace DB_COURSE_WORK;

public class Community
{
    public HashSet<int> NodesId { get; } = new HashSet<int>();
    public int devSum { get; set; } = 0;

    public Community()
    {
    }

    public Community(HashSet<int> nodesId)
    {
        NodesId = nodesId;
    }
    
    public void Add(int nodeId, int dev)
    {
        NodesId.Add(nodeId);
        devSum += dev;
    }

    public void Remove(int nodeId, int dev)
    {
        NodesId.Remove(nodeId);
        devSum -= dev;
    }
}