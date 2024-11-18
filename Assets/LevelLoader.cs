using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

public class LevelLoader : MonoBehaviour
{
    public GameObject buttonPrefab;
    public Transform buttonContainer;
    public int levelCount;

    void Start()
    {
        for (int i = 1; i <= levelCount; i++)
        {
            int level = i;
            GameObject button = Instantiate(buttonPrefab, buttonContainer);
            button.GetComponentInChildren<TextMeshProUGUI>().text = "" + level;
            button.GetComponent<Button>().onClick.AddListener(() => OnLevelButtonClicked(level));
        }
    }

    public void OnLevelButtonClicked(int level)
    {
        string levelFile = "level" + level;

        // Save the selected level
        PlayerPrefs.SetString("selectedLevel", levelFile);

        // Switch to the play scene
        SceneManager.LoadScene("PlayScene");
    }
}
