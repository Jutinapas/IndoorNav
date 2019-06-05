using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using TMPro;

public class Node : MonoBehaviour
{

    public Vector3 pos;
    public int id;

    [Header("A*")]
    public List<Node> neighbors = new List<Node>();
    public float FCost { get { return GCost + HCost; } }
    public float HCost { get; set; }
    public float GCost { get; set; }
    public float Cost { get; set; }
    public Node Parent { get; set; }

    private Vector3 scale;
    private bool isDestination = false;

    private void Awake()
    {
        transform.GetChild(0).gameObject.SetActive(false);
        scale = transform.localScale;
        if (transform.childCount > 1 && transform.GetChild(1).gameObject.GetComponent<TextMeshPro>() != null)
        {
            isDestination = true;
        }

#if UNITY_EDITOR
        pos = transform.position;
#endif
    }

    public void Activate(Node nextNode)
    {
        transform.GetChild(0).gameObject.SetActive(true);
        if (isDestination)
        {
            GetComponent<Rotate>().enabled = false;
            transform.GetChild(1).gameObject.SetActive(false);
        }
        transform.LookAt(nextNode.transform);
    }

    public void Deactivate()
    {
        transform.GetChild(0).gameObject.SetActive(false);
        if (isDestination)
        {
            GetComponent<Rotate>().enabled = true;
            transform.GetChild(1).gameObject.SetActive(true);
        }
        transform.rotation = new Quaternion(0, 0, 0, 0);
    }

    void Update()
    {
        if (!isDestination)
        {
            transform.localScale = scale * (1 + Mathf.Sin(Mathf.PI * Time.time) * .05f);
        }
    }

    public void FindNeighbors(float maxDistance)
    {
        foreach (Node node in FindObjectsOfType<Node>())
        {
            if (Vector3.Distance(node.pos, this.pos) < maxDistance)
            {
                neighbors.Add(node);
            }
        }
    }
}
