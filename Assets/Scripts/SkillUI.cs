using UnityEngine;

public class SkillUI : MonoBehaviour
{
    private GameManager gm;
    
    void Start()
    {
        gm = GameManager.Instance;
    }

    void OnGUI()
    {
        float w = 200;
        float h = 150;
        float x = Screen.width - w - 10;
        float y = 10;

        GUI.Box(new Rect(x, y, w, h), "Skills");

        GUILayout.BeginArea(new Rect(x + 10, y + 30, w - 20, h - 40));
        
        GUIStyle style = new GUIStyle(GUI.skin.button);
        style.fontSize = 14;

        if (GUILayout.Button("[1] TNT (Bomb)", style)) gm.UseSkillTNT();
        if (GUILayout.Button("[2] Enlarge (Big)", style)) gm.UseSkillEnlarge();
        if (GUILayout.Button("[3] Copy (Clone)", style)) gm.UseSkillCopy();
        if (GUILayout.Button("[4] Balls (Support)", style)) gm.UseSkillBalls();

        GUILayout.EndArea();
        
        // Money UI
        GUI.Box(new Rect(10, 10, 150, 40), $"Money: ${GameManager.Instance.Money}");
    }
}
