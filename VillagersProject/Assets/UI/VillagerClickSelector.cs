using UnityEngine;
using UnityEngine.InputSystem;

public class VillagerClickSelector : MonoBehaviour
{
    [SerializeField] private Camera cam;
    [SerializeField] private VillagerInspectPanelView inspectPanel;
    [SerializeField] private LayerMask hitMask = ~0; // все
    [SerializeField] private float maxDistance = 500f;



    private void Awake()
    {
        if (cam == null) cam = Camera.main;
        if (cam == null) cam = FindFirstObjectByType<Camera>();
    }

    private void Update()
    {
        if (cam == null || inspectPanel == null) return;
        if (Mouse.current == null) return;
        if (!Mouse.current.leftButton.wasPressedThisFrame) return;

        Vector2 screenPos = Mouse.current.position.ReadValue();
        Ray ray = cam.ScreenPointToRay(screenPos);

        if (Physics.Raycast(ray, out RaycastHit hit, maxDistance, hitMask, QueryTriggerInteraction.Ignore))
        {
            var brain = hit.collider.GetComponentInParent<VillagerAgentBrain>();
            if (brain != null)
            {
                GameInstaller.SelectedVillager?.SetSelected(brain.AgentId);
                inspectPanel.Show(brain);
                return;
            }
        }

        GameInstaller.SelectedVillager?.Clear();
        inspectPanel.Hide();
    }
}