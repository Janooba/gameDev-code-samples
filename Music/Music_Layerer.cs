using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;
using Archaic.Maxim.Player;

// Layers music tracks depending on "intensity"

// Uses SECTR_AudioCues which are just wrappers for audio clips

public class Music_Layerer : MonoBehaviour
{
    public static void StopAllMusic()
    {
        var controllers = FindObjectsOfType<Music_Layerer>();
        foreach (var controller in controllers)
        {
            controller.Stop();
        }
    }

    [DisableInPlayMode]
    public List<SECTR_AudioCue> layers = new List<SECTR_AudioCue>();
    
    [PropertyRange(0f, "@layers.Count")][SerializeField]
    private float intensity = 0;
    public float changeSharpness = 10f;
    public bool fadeIntensity;

    private List<SECTR_AudioCueInstance> layerInstances;

    private float currIntensity = 0f;

    private void Start()
    {
        // So that music can stop when player dies or is otherwise unloaded
        if (PlayerController.IsPlayerLoaded)
        {
            PlayerController.I.Actor.Health.BecameDead += Player_BecameDead;
            PlayerController.PlayerUnloaded += PlayerController_PlayerUnloaded;
        }
        else
        {
            PlayerController.PlayerLoaded += () => { PlayerController.I.Actor.Health.BecameDead += Player_BecameDead; };
            PlayerController.PlayerLoaded += () => { PlayerController.PlayerUnloaded += PlayerController_PlayerUnloaded; };
        }
    }

    private void PlayerController_PlayerUnloaded()
    {
        Stop();
    }

    private void Player_BecameDead(object sender, Archaic.Maxim.Characters.HealthController.RecievedDamagedEventArgs e)
    {
        Stop();
    }

    private void Update()
    {
        if (layerInstances == null)
            return;

        currIntensity = Mathf.MoveTowards(currIntensity, intensity, changeSharpness * Time.deltaTime);

        for (int i = 0; i < layers.Count; i++)
        {
            var instance = layerInstances[i];
            instance.Volume = GetLayerVolume(i);
            layerInstances[i] = instance;
        }
    }

    public void SetIntensity(float intensity)
    {
        this.intensity = Mathf.Clamp(intensity, 0f, layers.Count);
    }

    [Button]
    public void Play()
    {
        Play(1f);
    }

    public void Play(float intensity = 1f)
    {
        layerInstances = new List<SECTR_AudioCueInstance>();

        this.intensity = intensity;

        for (int i = 0; i < layers.Count; i++)
        {
            var layer = layers[i];

            var instance = SECTR_AudioSystem.Play(layer, transform, Vector3.zero, true);
            instance.Volume = GetLayerVolume(i);
            layerInstances.Add(instance);
        }
    }

    [Button]
    public void Stop()
    {
        if (layerInstances == null)
            return;

        for (int i = 0; i < layers.Count; i++)
        {
            layerInstances[i].Stop(true);
        }
    }

    private float GetLayerVolume(int layerIndex)
    {
        if (fadeIntensity)
        {
            return Mathf.Clamp01(currIntensity - layerIndex);
        }
        else
        {
            return (currIntensity >= layerIndex + 1) ? 1f : 0f;
        }
        
    }
}
