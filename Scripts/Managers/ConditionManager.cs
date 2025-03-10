using UnityEngine;

public class ConditionManager : MonoBehaviour
{
    public AgentCondition currentCondition = AgentCondition.Coexistence;

    public static ConditionManager Instance { get; private set; }

    // Singleton setup
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
}

