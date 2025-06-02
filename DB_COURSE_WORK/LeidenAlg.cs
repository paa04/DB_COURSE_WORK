using Antlr4.Runtime.Misc;

namespace DB_COURSE_WORK;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public class LeidenAlg
{
    private readonly GraphService _graphService;

    private Dictionary<int, int> _nodeCommunities; // nodeId -> communityId
    private Dictionary<int, Community> _communities; // communityId -> set of nodeIds
    private Dictionary<int, int> _nodeWeights;
    private double _totalEdgeWeight;

    private double _resolution = 1.0;
    private Random _random = new Random();
    private int _currentLevelIter = 0;

    public LeidenAlg(GraphService graphService)
    {
        _graphService = graphService;
        _nodeCommunities = new Dictionary<int, int>();
        _nodeWeights = new Dictionary<int, int>();
        _communities = new Dictionary<int, Community>();
    }

    public async Task AlgInit()
    {
        await InitializeCommunities();
        await InitializeNodeWeights();
        await CalculateTotalEdgeWeight();
    }

    public int GetCommunityCount()
    {
        return _communities.Count;
    }

    private async Task InitializeNodeWeights()
    {
        var idS = await _graphService.GetAllPersonsIdByLevel(0);

        foreach (var id in idS)
        {
            _nodeWeights.Add(id, 1);
        }
    }

    private async Task InitializeCommunities()
    {
        _nodeCommunities.Clear();
        _communities.Clear();

        var all_id = await _graphService.GetAllPersonsIdByLevel(_currentLevelIter);

        foreach (var nodeId in all_id)
        {
            var nodeDev = await _graphService.GetPowerOfNodeByLevel(nodeId, _currentLevelIter);
            _nodeCommunities[nodeId] = nodeId;
            _communities[nodeId] = new Community();
            _communities[nodeId].Add(nodeId, nodeDev);
        }
    }


    private async Task CalculateTotalEdgeWeight()
    {
        _totalEdgeWeight = await _graphService.GetGraphWeightByLevel(_currentLevelIter);
        _totalEdgeWeight /= 2;
    }

    public async Task ExecuteLeiden(int maxIterations = 10)
    {
        bool improvement = true;
        int iteration = 0;

        while (improvement && iteration < maxIterations)
        {
            Console.WriteLine($"Итерация {iteration + 1}");

            improvement = await LocalMoveNodes();

            if (improvement)
            {
                await AggregateCommunities();
            }

            iteration++;
            Console.WriteLine($"На уровне {_currentLevelIter} полученно {_communities.Count} сообществ");
        }

        // Console.WriteLine(_currentLevelIter);
        await _graphService.FormCommunityNodes(_currentLevelIter);

        //await SaveResults();
    }

    private async Task<bool> LocalMoveNodes()
    {
        bool improvement = false;
        var nodeOrder = (await _graphService.GetAllPersonsIdByLevel(_currentLevelIter)).OrderBy(x => _random.Next())
            .ToList();

        foreach (var nodeId in nodeOrder)
        {
            var neighborLst = await _graphService.GetAllNeighboursIdByLevel(nodeId, _currentLevelIter);

            int originalCommunity = _nodeCommunities[nodeId];

            int bestCommunity = await FindBestCommunity(nodeId, neighborLst);

            if (bestCommunity != originalCommunity)
            {
                var eDev = devOfVertice(neighborLst);
                _communities[originalCommunity].Remove(nodeId, eDev);
                _communities[bestCommunity].Add(nodeId, eDev);
                _nodeCommunities[nodeId] = bestCommunity;

                improvement = true;
            }
        }

        return improvement;
    }

    private async Task<int> FindBestCommunity(int nodeId, List<Pair<int, int>> neighborLst)
    {
        int currentCommunity = _nodeCommunities[nodeId];

        var communityGains = new Dictionary<int, double>();
        double bestGain = 0;
        int bestCommunity = currentCommunity;

        communityGains[currentCommunity] = 0;

        foreach (var neighborId in neighborLst)
        {
            int neighborCommunity = _nodeCommunities[neighborId.a];

            if (!communityGains.ContainsKey(neighborCommunity))
            {
                double gain = CalculateModularityGain(nodeId, neighborCommunity, neighborLst);
                communityGains[neighborCommunity] = gain;

                if (gain > bestGain)
                {
                    bestGain = gain;
                    bestCommunity = neighborCommunity;
                }
            }
        }

        return bestCommunity;
    }

    private int sumBetweenVerticeAndCommunity(int communityId, List<Pair<int, int>> neighbours)
    {
        int sum = 0;

        foreach (var neighbour in neighbours)
        {
            if (_nodeCommunities[neighbour.a] == communityId)
                sum += neighbour.b;
        }

        return sum;
    }

    private async Task<int> sumBetweenVerticesAndCommunity(HashSet<int> v, int communityId)
    {
        int sum = 0;
        foreach (var node in v)
        {
            var neighbour = await _graphService.GetAllNeighboursIdByLevel(node, _currentLevelIter);
            sum += sumBetweenVerticeAndCommunity(communityId, neighbour);
        }

        return sum;
    }

    private int devOfVertice(List<Pair<int, int>> neighbours)
    {
        int sum = 0;
        foreach (var neighbour in neighbours)
        {
            sum += neighbour.b;
        }

        return sum;
    }

    private double CalculateModularityGain(int nodeId, int targetCommunity, List<Pair<int, int>> neighboursOfNode)
    {
        int currentCommunity = _nodeCommunities[nodeId];
        if (currentCommunity == targetCommunity) return 0;

        var diff_target = sumBetweenVerticeAndCommunity(targetCommunity, neighboursOfNode);

        var diff_source = sumBetweenVerticeAndCommunity(currentCommunity, neighboursOfNode);

        var dev_v = devOfVertice(neighboursOfNode);

        var degs_target = _communities[targetCommunity].devSum;
        var degs_source = _communities[currentCommunity].devSum;

        return ((diff_target - diff_source) - _resolution / (2 * _totalEdgeWeight) *
            (dev_v * dev_v + dev_v * (degs_target - degs_source))) / (2 * _totalEdgeWeight);
    }

    private void RefinePartition()
    {
        // foreach (var community in _communities.Keys)
        // {
        //     CheckSubset(community);
        // }
    }

    private Dictionary<int, int> CreateSinglePartition(int communityId, out Dictionary<int, Community> partition)
    {
        var community = _communities[communityId];
        var nodeToCommunity = community.NodesId.ToDictionary(n => n, n => n);

        partition = nodeToCommunity.ToDictionary(n => n.Key, n => new Community(new HashSet<int> { n.Value }));

        return nodeToCommunity;
    }

    private async Task CheckSubset(int communityId)
    {
        var v = _communities[communityId].NodesId.ToList();
        var size = v.Select(x => _nodeWeights[x]).Sum();

        var singlePartition = CreateSinglePartition(communityId, out var partition);

        HashSet<int> R = new HashSet<int>();

        foreach (var node in singlePartition.Keys)
        {
            var neighbours = await _graphService.GetAllNeighboursIdByLevel(node, _currentLevelIter);
            var distance = sumBetweenVerticeAndCommunity(communityId, neighbours);

            var not_distance = _resolution * _nodeWeights[node] * (size - _nodeWeights[node]);

            if (distance >= not_distance)
            {
                R.Add(node);
            }
        }

        foreach (var node in R)
        {
            if (partition[node].NodesId.Count == 1)
            {
                /*
                 * 𝓣 = freeze([
                       C for C in 𝓟
                         if C <= S
                            and nx.cut_size(G, C, S - C, weight=Keys.WEIGHT)
                                >= γ * node_total(G, C) * (size_s - node_total(G, C))
                   ])
                 */

                foreach (var c in partition.Keys)
                {
                    var cutSize = await sumBetweenVerticesAndCommunity(partition[node].NodesId, communityId);
                    var nodeW = partition[node].NodesId.Select(n => _nodeWeights[n]).Sum();
                }


                //  var goodOnes = 
            }
        }
    }

    private async Task<List<Pair<int, int>>> GetAllNeighboursCommunities(int communityId)
    {
        Community community = _communities[communityId];
        var communitiesNeighbours = new Dictionary<int, int>();
        foreach (var node in community.NodesId)
        {
            var neighbours = await _graphService.GetAllNeighboursIdByLevel(node, _currentLevelIter);
            foreach (var nodeNeighbour in neighbours)
            {
                var nComm = _nodeCommunities[nodeNeighbour.a];
                if (communitiesNeighbours.ContainsKey(nComm))
                    communitiesNeighbours[nComm] += nodeNeighbour.b;
                else
                {
                    communitiesNeighbours.Add(nComm, nodeNeighbour.b);
                }
            }
        }

        var resultList = new List<Pair<int, int>>();

        foreach (var node in communitiesNeighbours)
            resultList.Add(new Pair<int, int>(node.Key, node.Value));

        return resultList;
    }

    private int CalcNewVWeight(List<int> v)
    {
        return v.Select(x => _nodeWeights[x]).Sum();
    }

    private async Task AggregateCommunities()
    {
        HashSet<int> used = new HashSet<int>();
        Dictionary<int, int> newNodeW = new Dictionary<int, int>();


        foreach (int nodeId in _nodeCommunities.Keys)
        {
            if (used.Contains(_nodeCommunities[nodeId]))
                continue;

            var communityId = _nodeCommunities[nodeId];

            var edges = await GetAllNeighboursCommunities(communityId);
            var vertices = _communities[communityId].NodesId.ToList();

            newNodeW[communityId] = CalcNewVWeight(vertices);

            await _graphService.CreateSuperNodeSave(communityId, vertices, edges, _currentLevelIter + 1);

            used.Add(communityId);
        }

        _nodeWeights = newNodeW;

        _currentLevelIter++;

        await InitializeCommunities();
        await CalculateTotalEdgeWeight();
    }

    private async Task SaveResults()
    {
        await _graphService.SaveCommunities(_nodeCommunities);
    }

    public Dictionary<int, int> GetCommunityAssignments()
    {
        return new Dictionary<int, int>(_nodeCommunities);
    }
}