using UnityEngine;

public class ConditionSceneSetup : MonoBehaviour
{
    [Header("Cooperation Objects")]
    public GameObject cooperationRobot;
    public GameObject cooperationWall;

    [Header("Coexistence Objects")]
    public GameObject coexistenceRobot;
    public GameObject coexistenceConveyorBelt;

    [Header("Agent Prompt System")]
    public AgentPromptSystem agentPromptSystem;

    private void Start()
    {
        // 1) Grab the current condition from ConditionManager
        var condition = ConditionManager.Instance.currentCondition;

        // 2) Enable/disable objects based on that condition
        if (condition == AgentCondition.Cooperation)
        {
            // Turn ON cooperation objects
            if (cooperationRobot) cooperationRobot.SetActive(true);
            if (cooperationWall) cooperationWall.SetActive(true);

            // Turn OFF coexistence objects
            if (coexistenceRobot) coexistenceRobot.SetActive(false);
            if (coexistenceConveyorBelt) coexistenceConveyorBelt.SetActive(false);
        }
        else // Coexistence
        {
            // Turn ON coexistence objects
            if (coexistenceRobot) coexistenceRobot.SetActive(true);
            if (coexistenceConveyorBelt) coexistenceConveyorBelt.SetActive(true);

            // Turn OFF cooperation objects
            if (cooperationRobot) cooperationRobot.SetActive(false);
            if (cooperationWall) cooperationWall.SetActive(false);
        }

        // Set the audio source mode for the AgentPromptSystem based on the condition.
        if (agentPromptSystem != null)
        {
            if (condition == AgentCondition.Cooperation)
                agentPromptSystem.SwitchMode(true);
            else
                agentPromptSystem.SwitchMode(false);
        }
    }
}
