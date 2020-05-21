using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class LogDisplay : MonoBehaviour
{
    public void Clear()
    {
        m_Log.Clear();

        m_Dirty = true;
    }

    public void Log(string msg)
    {
        if (msg.Length > MAX_MSG_LENGTH)
            throw new Exception("Message: \"" + msg + "\" exceeds " + MAX_MSG_LENGTH + " characters");

        while (m_Log.Count > (MAX_LOG_LINES - 1))
            m_Log.Dequeue();

        m_Log.Enqueue(msg);
        Debug.Log(msg);

        m_Dirty = true;
    }

    void Awake()
    {
        m_Text = GetComponent<Text>();
        if (!m_Text)
            throw new Exception("Missing Text");
    }

    void Update()
    {
        if (m_Dirty)
        {
            m_LogText = "";
            foreach (string msg in m_Log)
                m_LogText += msg + "\n";
            m_Dirty = false;
        }

        m_Text.text = m_LogText;
        if (((int)Time.time % 2) == 0)
            m_Text.text += ">_";
    }

    const int MAX_MSG_LENGTH = 120;

    const int MAX_LOG_LINES = 40;

    static bool m_Dirty = false;

    static Queue m_Log = new Queue(MAX_LOG_LINES);

    static String m_LogText;

    static Text m_Text;
}
