using Unity.Netcode;
using UnityEditor;
using UnityEngine;

public class InSceneFireworkHandler : NetworkBehaviour
{
    /// <summary>
    /// Store the GlobalObjectIdHash value to identify the target network prefab during runtime
    /// </summary>
    [HideInInspector]
    [SerializeField]
    private uint m_TargetGlobalObjectIdHash;

    public bool EnableDebugLogging;

#if UNITY_EDITOR
    /// <summary>
    /// Get a reference to the source prefab asset
    /// </summary>
    private void OnValidate()
    {
        var originalSource = PrefabUtility.GetCorrespondingObjectFromOriginalSource(gameObject);
        if (!EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isPlaying)
        {
            var globalObjectIdHash = GetComponent<NetworkObject>().PrefabIdHash;
            m_TargetGlobalObjectIdHash = originalSource.GetComponent<NetworkObject>().PrefabIdHash;
            LogMessage($"[OnValidate] Local GID: {globalObjectIdHash} --> Target GID: {m_TargetGlobalObjectIdHash}");
            // If this is a prefab instance
            if (PrefabUtility.IsPartOfAnyPrefab(this))
            {
                // Mark the prefab instance as "dirty"
                PrefabUtility.RecordPrefabInstancePropertyModifications(this);
            }
        }
    }
#endif


    private void LogMessage(string message, int displayTime = 30)
    {
        if (!EnableDebugLogging)
        {
            return;
        }

        if (NetworkManagerHelper.Instance != null)
        {
            NetworkManagerHelper.Instance.LogMessage(message, displayTime);
        }
        else
        {
            Debug.Log(message);
        }
    }

    /// <summary>
    /// Synchronize Firework's Unique Settings
    /// Since the NetworkObject is being dynamically spawned, a new instance will
    /// be created which means the unique settings will no longer exist. This might
    /// be the best way to handle passing this information along to the client.
    /// </summary>
    protected override void OnSynchronize<T>(ref BufferSerializer<T> serializer)
    {
        if (serializer.IsWriter)
        {
            // Server would set the unique firework configuration settings
            SetConfigurationSetttings();
        }

        serializer.SerializeNetworkSerializable(ref m_UniqueFireworkSettings);

        if (serializer.IsReader)
        {
            // Client would apply the unique firework configuration settings
            ApplyConfigurationSettings();
        }

        base.OnSynchronize(ref serializer);
    }

    /// <summary>
    /// Server side
    /// </summary>
    private void SetConfigurationSetttings()
    {
        // Add any pertinent/unique information about the in-scene placed firework object
        // to the UniqueFireworkSettings struct (as an example)
    }

    /// <summary>
    /// Client side
    /// </summary>
    private void ApplyConfigurationSettings()
    {
        // Apply the UniqueFireworkSettings information to the newly instantiated firework object
    }

    /// <summary>
    ///  The unique firework configuration settings (pseudo)
    /// </summary>
    private struct UniqueFireworkSettings : INetworkSerializable
    {
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {

        }
    }

    private UniqueFireworkSettings m_UniqueFireworkSettings = new UniqueFireworkSettings();

    /// <summary>
    /// Returns the target prefab's GameObject
    /// </summary>
    private GameObject GetTargetNetworkPrefab()
    {
        if (NetworkManager != null)
        {
            foreach(var prefab in NetworkManager.NetworkConfig.Prefabs.Prefabs)
            {
                var globalObjectIdHash = prefab.Prefab.GetComponent<NetworkObject>().PrefabIdHash;
                if (globalObjectIdHash == m_TargetGlobalObjectIdHash)
                {
                    return prefab.Prefab;
                }
            }
        }
        return null;
    }

    /// <summary>
    /// Start is invoked during scene loading which happens prior to spawning.
    /// This is a good place to register your override entry for the in-scene
    /// placed NetworkObject's GlobalObjectIdHash value to the source prefab's
    /// GlobalObjectIdHash value.
    /// </summary>
    /// <remarks>
    /// If you can't have this component on each in-scene placed NetworkObject
    /// prior to the mod being loaded, then it needs to be added before the client
    /// receives the connection approved message (which contains the NetworkObjects
    /// to spawn).
    /// </remarks>
    private void Start()
    {
        // Ingore if we are the original pefab or do not have a target prefab assigned
        if (m_TargetGlobalObjectIdHash == 0 || NetworkManager.Singleton.IsServer)
        {
            if (m_TargetGlobalObjectIdHash == 0)
            {
                LogMessage($"[GID NOT ASSIGNED] Target GID: {m_TargetGlobalObjectIdHash}!!!", 120);
            }            
            return;
        }

        // Get the instance's GlobalObjectIdHash value
        var globalObjectIdHash = GetComponent<NetworkObject>().PrefabIdHash;
        LogMessage($"Local GID: {globalObjectIdHash} --> Target GID: {m_TargetGlobalObjectIdHash}", 30);

        // Add the override on the client side (using singleton because the instance is not yet spawned)
        // Ignore this if we are the Server or we are the override target prefab (identify by GlobalObjecIdHash)
        if (!NetworkManager.Singleton.IsServer && globalObjectIdHash != m_TargetGlobalObjectIdHash)
        {
            if (!NetworkManager.Singleton.NetworkConfig.Prefabs.Contains(gameObject))
            {
                var networkPrefab = new NetworkPrefab()
                {
                    Prefab = gameObject,
                    Override = NetworkPrefabOverride.Prefab, // Make sure we set the override to be of type "Prefab"
                    SourcePrefabToOverride = gameObject,
                    OverridingTargetPrefab = GetTargetNetworkPrefab(),   // We get the target prefab from the target prefab's GlobalObjectIdHash value
                };
                NetworkManager.Singleton.NetworkConfig.Prefabs.Add(networkPrefab);
            }
        }
    }
}
