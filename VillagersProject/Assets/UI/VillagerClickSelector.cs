using UnityEngine;

public class VillagerClickSelector : MonoBehaviour
{
    [SerializeField] private Camera cam;
    [SerializeField] private VillagerInspectPanelView inspectPanel;
    [SerializeField] private LayerMask hitMask = ~0; // все
    [SerializeField] private float maxDistance = 500f;

    private void Awake()
    {
        if (cam == null) cam = Camera.main;
    }

    private void Update()
    {
        if (cam == null || inspectPanel == null) return;

        if (Input.GetMouseButtonDown(0))
        {
            var ray = cam.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out var hit, maxDistance, hitMask))
            {
                var brain = hit.collider.GetComponentInParent<VillagerAgentBrain>();
                if (brain != null)
                {
                    inspectPanel.Show(brain);
                    return;
                }

                // клік в “порожнечу/не-віліджера” → закрити
                inspectPanel.Hide();
            }
            else
            {
                inspectPanel.Hide();
            }
        }

        // ESC теж зручно
        if (Input.GetKeyDown(KeyCode.Escape))
            inspectPanel.Hide();
    }
}