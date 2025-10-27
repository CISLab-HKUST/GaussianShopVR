using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class ReminderManager : MonoBehaviour
{
    public static ReminderManager Instance { get; private set; }
    public TextMeshProUGUI reminderText;
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
        reminderText.gameObject.SetActive(false);
    }

    public void ShowReminder(string text)
    {
        StartCoroutine(ShowReminderCoroutine(text));
    }

    private IEnumerator ShowReminderCoroutine(string text)
    {
        reminderText.text = text;
        reminderText.gameObject.SetActive(true);
        yield return new WaitForSeconds(2);
        reminderText.gameObject.SetActive(false);
    }

    public void BeginReminder(string text)
    {
        reminderText.text = text;
        reminderText.gameObject.SetActive(true);
    }

    public void EndReminder()
    {
        reminderText.text = " ";
        reminderText.gameObject.SetActive(false);
    }
}
