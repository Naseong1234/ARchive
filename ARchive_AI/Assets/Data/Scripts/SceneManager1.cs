using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneManager1 : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }


     public void SceneChange(string sceneName)
    {
       SceneManager.LoadScene(sceneName);
    }
}
