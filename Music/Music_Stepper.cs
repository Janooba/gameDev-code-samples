using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;
using QFSW.QC;
using Archaic.Maxim.Player;

// Steps between different sections of a song
// Includes option to wait till next subdivision

// Uses SECTR_AudioCues which are just wrappers for audio clips

public class Music_Stepper : MonoBehaviour
{
    [System.Serializable]
    public class MusicStep
    {
        public SECTR_AudioCue cue;
        public bool autoAdvance = true;
        public int subdivisions = 1;
    }

    public List<MusicStep> steps = new List<MusicStep>();
    private int currentStep = 0;
    private int currentSubStep = 1;

    public bool advancing = false;

    public SECTR_AudioCueInstance instance;

    [ProgressBar(0, "maxLength"), ReadOnly]
    public float progress;
    private float maxLength
    {
        get
        {
            if (instance && instance.GetInternalAudioSource())
            {
                return instance.GetInternalAudioSource().clip.length;
            }
            else
                return 0f;
        }
    }

    [ProgressBar(0, "maxLength"), ReadOnly]
    public float nextTarget;

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
        StopMusic();
    }

    private void Player_BecameDead(object sender, Archaic.Maxim.Characters.HealthController.RecievedDamagedEventArgs e)
    {
        StopMusic();
    }

    // Update is called once per frame
    void Update()
    {
        if (IsReachingEnd())
        {
            // Last step
            if (currentStep == steps.Count - 1)
            {
                if (steps[currentStep].autoAdvance || advancing)
                {
                    StopMusic();
                }
                else
                {
                    PlayCue(currentStep);
                }
            }
            // auto advance or manually triggered
            else if (steps[currentStep].autoAdvance || advancing)
            {
                advancing = false;
                currentStep++;
                PlayCue(currentStep);
            }
            // not last step
            else if (currentStep < steps.Count)
            {
                PlayCue(currentStep);
            }
        }

        if (instance)
        {
            progress = instance.TimeSeconds;

            float subStepLength = instance.GetInternalAudioSource().clip.length;
            if (steps[currentStep].subdivisions > 0)
            {
                subStepLength = instance.GetInternalAudioSource().clip.length / steps[currentStep].subdivisions;
            }
            
            currentSubStep = Mathf.CeilToInt(progress / subStepLength);
            nextTarget = currentSubStep * subStepLength;
        }
    }

    [ButtonGroup("Controls")]
    public void StartMusic()
    {
        currentStep = 0;
        PlayCue(currentStep);
    }

    [Button][Command("Music.Advance", "Advances the music stepper to the next step")]

    public void AdvanceMusic(bool immediate)
    {
        if (!instance.Active)
            return;

        if (immediate || steps[currentStep].subdivisions < 1)
        {
            if (currentStep + 1 >= steps.Count)
                return;

            PlayCue(++currentStep);
        }
        else
        {
            advancing = true;
        }
    }

    [ButtonGroup("Controls")]
    public void StopMusic()
    {
        if (instance)
            instance.Stop(false);

        currentStep = 0;
    }

    private void PlayCue(int step)
    {
        if (instance)
            instance.Stop(true);

        instance = SECTR_AudioSystem.Play(steps[step].cue, transform, Vector3.zero, false);
    }

    private bool IsReachingEnd()
    {
        if (!instance)
            return false;

        if (advancing)
            return instance.TimeSeconds + Time.deltaTime >= nextTarget;
        else
            return instance.TimeSeconds + Time.deltaTime >= instance.GetInternalAudioSource().clip.length;
    }
}
