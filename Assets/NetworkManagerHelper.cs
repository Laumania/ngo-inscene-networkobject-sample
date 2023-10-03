using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
#endif
using Unity.Netcode;

public class NetworkManagerHelper : MonoBehaviour
{
    public static NetworkManagerHelper Instance;

    [HideInInspector]
    [SerializeField]
    private string m_SceneNameToLoad = string.Empty;

    private Scene m_LoadedScene;

#if UNITY_EDITOR
    public SceneAsset SceneToLoad;
    private void OnValidate()
    {
        if (SceneToLoad != null)
        {
            m_SceneNameToLoad = SceneToLoad.name;
        }
    }
#endif

    private enum NetworkManagerModes
    {
        Client,
        Host
    }

    private NetworkManagerModes m_NetworkManagerMode;

    private void Start()
    {
        Screen.SetResolution((int)(Screen.currentResolution.width * 0.40f), (int)(Screen.currentResolution.height * 0.40f), FullScreenMode.Windowed);
    }

    private void HandleSceneLoading()
    {
        if (m_SceneNameToLoad == string.Empty) 
        {
            return;
        }

        SceneManager.sceneLoaded += SceneManager_sceneLoaded;
        SceneManager.LoadSceneAsync(m_SceneNameToLoad, LoadSceneMode.Additive);
    }

    private void SceneManager_sceneLoaded(Scene scene, LoadSceneMode mode)
    {
        SceneManager.sceneLoaded -= SceneManager_sceneLoaded;
        m_LoadedScene = scene;
        switch(m_NetworkManagerMode) 
        { 
            case NetworkManagerModes.Host: 
                {
                    NetworkManager.Singleton.StartHost();
                    break;
                }
            case NetworkManagerModes.Client:
                {
                    NetworkManager.Singleton.StartClient();
                    break;
                }
        }
    }

    private void OnGUI()
    {
        var networkManager = NetworkManager.Singleton;
        if (!networkManager.IsClient && !networkManager.IsServer)
        {
            GUILayout.BeginArea(new Rect(10, 10, 300, 800));
            if (GUILayout.Button("Host"))
            {
                m_NetworkManagerMode = NetworkManagerModes.Host;
                HandleSceneLoading();
            }

            if (GUILayout.Button("Client"))
            {
                m_NetworkManagerMode = NetworkManagerModes.Client;
                // Handle NetworkObject clean up in case the client side disconnects and the active scene 
                // was not the same scene the in-scene NetworkObjects were originally placed within.
                networkManager.OnClientStopped -= NetworkManager_OnClientStopped;
                networkManager.OnClientStopped += NetworkManager_OnClientStopped;
                HandleSceneLoading();
            }
            GUILayout.EndArea();

            GUILayout.BeginArea(new Rect(10, Display.main.renderingHeight - 40, Display.main.renderingWidth - 10, 30));
            var scenesPreloaded = new System.Text.StringBuilder();
            scenesPreloaded.Append("Scenes Preloaded: ");
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                scenesPreloaded.Append($"[{scene.name}]");
            }
            GUILayout.Label(scenesPreloaded.ToString());
            GUILayout.EndArea();
        }
        else
        {
            GUILayout.BeginArea(new Rect(10, 10, 300, 800));
            GUILayout.Label($"Mode: {(networkManager.IsHost ? "Host" : networkManager.IsServer ? "Server" : "Client")}");

            if (m_MessageLogs.Count > 0)
            {
                GUILayout.Label("-----------(Log)-----------");
                // Display any messages logged to screen
                foreach (var messageLog in m_MessageLogs)
                {
                    GUILayout.Label(messageLog.Message);
                }
                GUILayout.Label("---------------------------");
            }
            GUILayout.EndArea();

            GUILayout.BeginArea(new Rect(Display.main.renderingWidth - 40, 10, 30, 30));

            if (GUILayout.Button("X"))
            {
                
                networkManager.Shutdown();

                if (m_LoadedScene.IsValid() && m_LoadedScene.isLoaded)
                {
                    SceneManager.UnloadSceneAsync(m_LoadedScene);
                }
            }
            GUILayout.EndArea();
        }
    }

    /// <summary>
    /// Handles cleaning up NetworkObjects that were spawned in the active scene.
    /// </summary>
    /// <remarks>
    /// This is only needed if the active scene is not the scene that held the in-scene
    /// placed NetworkObjects. Alternately, you could make the In-SceneObjects scene the
    /// active scene when it is loaded and upon unloading that scene, when the client 
    /// disconnects on its side, the NetworkObjects would be destroyed.
    /// </remarks>    
    private void NetworkManager_OnClientStopped(bool obj)
    {
        var networkObjects = FindObjectsByType<NetworkObject>(FindObjectsSortMode.None);
        foreach(var networkObject in networkObjects) 
        {
            Object.Destroy(networkObject.gameObject);
        }
    }

    private void Update()
    {
        if (m_MessageLogs.Count == 0)
        {
            return;
        }

        for (int i = m_MessageLogs.Count - 1; i >= 0; i--)
        {
            if (m_MessageLogs[i].ExpirationTime < Time.realtimeSinceStartup)
            {
                m_MessageLogs.RemoveAt(i);
            }
        }
    }

    private List<MessageLog> m_MessageLogs = new List<MessageLog>();

    private class MessageLog
    {
        public string Message { get; private set; }
        public float ExpirationTime { get; private set; }

        public MessageLog(string msg, float timeToLive)
        {
            Message = msg;
            ExpirationTime = Time.realtimeSinceStartup + timeToLive;
        }
    }

    public void LogMessage(string msg, float timeToLive = 10.0f)
    {
        if (m_MessageLogs.Count > 0)
        {
            m_MessageLogs.Insert(0, new MessageLog(msg, timeToLive));
        }
        else
        {
            m_MessageLogs.Add(new MessageLog(msg, timeToLive));
        }

        Debug.Log(msg);
    }

    public NetworkManagerHelper()
    {
        Instance = this;
    }
}
