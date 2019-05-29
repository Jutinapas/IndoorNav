using System.Collections.Generic;
using UnityEngine;

public class AStar : MonoBehaviour
{

    public List<Node> FindPath(Node startNode, Node targetNode, Node[] allNodes)
    {

        List<Node> openSet = new List<Node>();
        openSet.Add(startNode);

        List<Node> closedSet = new List<Node>();

        while (openSet.Count > 0)
        {
            Node currentNode = openSet[0];
            for (int i = 1; i < openSet.Count; i++)
            {
                if (openSet[i].FCost < currentNode.FCost
                    || (openSet[i].FCost.Equals(currentNode.FCost)
                        && openSet[i].HCost < currentNode.HCost))
                {
                    currentNode = openSet[i];
                }
            }

            openSet.Remove(currentNode);
            closedSet.Add(currentNode);

            if (currentNode == targetNode)
            {
                Debug.Log("Return correct node");
                return RetracePath(startNode, targetNode);
            }

            foreach (Node neighbor in currentNode.neighbors)
            {
                if (!closedSet.Contains(neighbor))
                {
                    float costToNeighbor = currentNode.GCost + GetEstimate(currentNode, neighbor) + neighbor.Cost;

                    if (costToNeighbor < neighbor.GCost || !openSet.Contains(neighbor))
                    {
                        neighbor.GCost = costToNeighbor;
                        neighbor.HCost = GetEstimate(neighbor, targetNode);
                        neighbor.Parent = currentNode;

                        if (!openSet.Contains(neighbor))
                        {
                            openSet.Add(neighbor);
                        }
                    }
                }
            }
        }
        Debug.Log("Return null");
        return null;
    }

    private static List<Node> RetracePath(Node startNode, Node endNode)
    {
        List<Node> path = new List<Node>();

        Node currentNode = endNode;

        while (currentNode != startNode)
        {
            path.Add(currentNode);
            currentNode = currentNode.Parent;
        }

        path.Reverse();

        return path;
    }

    private float GetEstimate(Node first, Node second)
    {
        float distance;

        float xDistance = Mathf.Abs(first.pos.x - second.pos.x);
        float yDistance = Mathf.Abs(first.pos.z - second.pos.z);

        if (xDistance > yDistance)
        {
            distance = 14 * yDistance + 10 * (xDistance - yDistance);
        }
        else
        {
            distance = 14 * xDistance + 10 * (yDistance - xDistance);
        }

        return distance;
    }
}