using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEditor;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif
using UnityEngine;

public class InSceneFireworkHandler : NetworkBehaviour
{
    [HideInInspector]
    [SerializeField]
    private GameObject TargetPrefab;

#if UNITY_EDITOR
    /// <summary>
    /// Get a reference to the source prefab asset
    /// </summary>
    private void OnValidate()
    {
        var originalSource = PrefabUtility.GetCorrespondingObjectFromOriginalSource(gameObject);
        if (originalSource != null)
        {
            TargetPrefab = originalSource;
        }
    }
#endif

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
        if (TargetPrefab == null)
        {
            return;
        }
        // Get the instance's GlobalObjectIdHash value
        var globalObjectIdHash = GetComponent<NetworkObject>().PrefabIdHash;
        var targetGlobalObjectIdHash = TargetPrefab.GetComponent<NetworkObject>().PrefabIdHash;
        // Add the override on the client side (using singleton because the instance is not yet spawned)
        // Ignore this if we are the Server or we are the override target prefab (identify by GlobalObjecIdHash)
        if (!NetworkManager.Singleton.IsServer && globalObjectIdHash != targetGlobalObjectIdHash)
        {
            if (!NetworkManager.Singleton.NetworkConfig.Prefabs.Contains(gameObject))
            {
                var networkPrefab = new NetworkPrefab()
                {
                    Prefab = gameObject,
                    Override = NetworkPrefabOverride.Prefab, // Make sure we set the override to be of type "Prefab"
                    SourcePrefabToOverride = gameObject,
                    OverridingTargetPrefab = TargetPrefab,   // The prefab to spawn in place of the instance
                };
                NetworkManager.Singleton.NetworkConfig.Prefabs.Add(networkPrefab);
            }
        }
    }
}
