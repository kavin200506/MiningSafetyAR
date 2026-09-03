using UnityEngine;
using UnityEditor;

public class ClearUserDataScript
{
    [MenuItem("Tools/Mining Safety/Clear All User Data")]
    public static void ClearAllData()
    {
        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();
        Debug.Log("Successfully wiped all user progress, certificates, and cached logins! Start Play Mode to see a fresh slate.");
    }
}
