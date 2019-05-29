using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class NavController : MonoBehaviour
{

    public AStar AStar;
    private Transform destination;
    private bool _initialized = false;
    private bool _initializedComplete = false;
    private List<Node> path = new List<Node>();
    private int currNodeIndex = 0;
    private float maxDistance = 1.1f;

    private void Start()
    {
#if UNITY_EDITOR
        InitializeNavigation();
#endif
    }

    public void InitializeNavigation()
    {
        StopAllCoroutines();
        StartCoroutine(DelayNavigation());
    }

    IEnumerator DelayNavigation()
    {
        while (FindObjectsOfType<TextMesh>().Count() != FindObjectOfType<CustomShapeManager>().numDest)
        {
            yield return new WaitForSeconds(.5f);
        }
    }

    public void InitNav(int id)
    {
        if (!_initialized)
        {
            _initialized = true;
            Node[] allNodes = FindObjectsOfType<Node>();
            Debug.Log("Number of Nodes: " + allNodes.Length);
            Node closestNode = ReturnClosestNode(allNodes, transform.position);
            Debug.Log("Closest: " + closestNode.gameObject.name);
            
            TextMesh[] dests = FindObjectsOfType<TextMesh>();

            Node target = null;
            foreach (TextMesh dest in dests)
            {
                Node node = dest.gameObject.GetComponent<Node>();
                if (node.id == id)
                {
                    target = node;
                    break;
                }
            }

            if (target == null)
            {
                Debug.Log("Destination not found");
                return;
            }
        
            Debug.Log("Target: " + target.gameObject.name);
            foreach (Node node in allNodes)
            {
                node.FindNeighbors(maxDistance);
            }

            path = AStar.FindPath(closestNode, target, allNodes);
            if (path == null)
            {
                maxDistance += .1f;
                Debug.Log("Increasing search distance: " + maxDistance);
                _initialized = false;
                InitNav(id);
                return;
            }

            string str = ""; 
            for (int i = 0; i < path.Count - 1; i++)
            {
                str += path[i].id + " >> ";
                path[i].NextInList = path[i + 1];
            }
            Debug.Log(str);

            path[0].Activate(true);
            _initializedComplete = true;
        }
    }

    Node ReturnClosestNode(Node[] nodes, Vector3 point)
    {
        float minDist = Mathf.Infinity;
        Node closestNode = null;
        foreach (Node node in nodes)
        {
            float dist = Vector3.Distance(node.pos, point);
            if (dist < minDist)
            {
                closestNode = node;
                minDist = dist;
            }
        }
        return closestNode;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_initializedComplete && other.CompareTag("Node"))
        {
            currNodeIndex = path.IndexOf(other.GetComponent<Node>());
            if (currNodeIndex < path.Count - 1)
            {
                path[currNodeIndex + 1].Activate(true);
            }
        }
    }

    public void SetInitialized(bool init)
    {
        _initialized = init;
    }

    public void SetComplete(bool complete)
    {
        _initializedComplete = complete;
    }

}
