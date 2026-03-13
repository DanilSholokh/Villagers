using TMPro;
using UnityEngine;

public class WorldDebugPopupView : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI text;
    [SerializeField] private Vector3 worldOffset = new Vector3(0f, 1.8f, 0f);
    [SerializeField] private float riseSpeed = 0.75f;
    [SerializeField] private float lifetime = 1.5f;

    private Camera _cam;
    private float _timeLeft;
    private Vector3 _worldAnchor;
    private Color _baseColor;

    public void Show(Camera cam, Vector3 worldPos, string message, Color color, float customLifetime)
    {
        _cam = cam;
        _worldAnchor = worldPos + worldOffset;
        _timeLeft = customLifetime > 0f ? customLifetime : lifetime;

        if (text != null)
        {
            text.text = message;
            text.color = color;
            _baseColor = color;
        }

        UpdatePosition();
        gameObject.SetActive(true);
    }

    private void Update()
    {
        if (_cam == null)
            _cam = Camera.main;

        _timeLeft -= Time.deltaTime;
        _worldAnchor += Vector3.up * (riseSpeed * Time.deltaTime);

        float t = Mathf.Clamp01(_timeLeft / Mathf.Max(0.01f, lifetime));
        if (text != null)
        {
            var c = _baseColor;
            c.a = t;
            text.color = c;
        }

        UpdatePosition();

        if (_timeLeft <= 0f)
            Hide();
    }

    private void UpdatePosition()
    {
        if (_cam == null) return;

        Vector3 screenPos = _cam.WorldToScreenPoint(_worldAnchor);
        if (screenPos.z <= 0f)
        {
            return;
        }

        transform.position = screenPos;
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }
}