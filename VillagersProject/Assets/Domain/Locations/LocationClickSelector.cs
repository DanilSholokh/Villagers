using UnityEngine;
using UnityEngine.InputSystem;

public class LocationClickSelector : MonoBehaviour
{
    [SerializeField] private Camera targetCamera;
    [SerializeField] private LayerMask layerMask = ~0;

    private void Awake()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;
    }

    private void Update()
    {
        if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame)
            return;

        if (targetCamera == null)
            return;

        Ray ray = targetCamera.ScreenPointToRay(Mouse.current.position.ReadValue());

        if (Physics.Raycast(ray, out RaycastHit hit, 1000f, layerMask))
        {
            var selectable = hit.collider.GetComponentInParent<LocationSelectable>();
            if (selectable != null)
            {
                GameInstaller.SelectedLocation?.Set(selectable.LocationId);
            }
        }
    }
}