using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class ButtonBackgroundSwitcher : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    private Image backgroundImage;

    void Awake()
    {
        CreateBackgroundLayer();
        backgroundImage.gameObject.SetActive(false);
    }

    private void CreateBackgroundLayer()
    {
        GameObject backgroundObj = new GameObject("ButtonBackground");
        backgroundObj.transform.SetParent(transform);
        backgroundObj.transform.SetAsFirstSibling();

        backgroundImage = backgroundObj.AddComponent<Image>();

        RectTransform backgroundRect = backgroundImage.rectTransform;
        backgroundRect.localPosition = Vector3.zero;
        backgroundRect.sizeDelta = new Vector2(100f, 100f);
        backgroundRect.pivot = new Vector2(0.5f, 0.5f);
        backgroundRect.localRotation = Quaternion.identity;
        backgroundRect.localScale = Vector3.one;

        backgroundImage.color = new Color(1f, 1f, 1f, 0.3f);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        backgroundImage.gameObject.SetActive(true);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        backgroundImage.gameObject.SetActive(false);
    }
}
