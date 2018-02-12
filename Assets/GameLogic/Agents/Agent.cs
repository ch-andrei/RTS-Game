using UnityEngine;
using UnityEngine.AI;
using System.Collections;

public class Agent : MonoBehaviour {

    public GameObject goal;
    NavMeshAgent agent;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
    }

    void Update()
    {
        agent.destination = goal.transform.position;
    }
}
