using UnityEngine;
using UnityEngine.Rendering;

public class GlobalVolume : MonoBehaviour
{
    private Volume postProcessingVolume;
    private UnityEngine.Rendering.Universal.FilmGrain filmGrain;
    private UnityEngine.Rendering.Universal.LensDistortion lensDistortion;
    private UnityEngine.Rendering.Universal.ChromaticAberration chromaticAberration;
    private UnityEngine.Rendering.Universal.Vignette vignette;

    // private AudioSource audioSourceRadioWaves;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        postProcessingVolume = GetComponent<Volume>();
        // AudioSourceGet();
    }

    public void TurnAllOff()
    {
        postProcessingVolume.profile.TryGet(out filmGrain);
        filmGrain.active = false;

        postProcessingVolume.profile.TryGet(out lensDistortion);
        lensDistortion.active = false;

        postProcessingVolume.profile.TryGet(out chromaticAberration);
        chromaticAberration.active = false;

        postProcessingVolume.profile.TryGet(out vignette);
        vignette.active = false;
    }

    public void TurnAllOn()
    {
        postProcessingVolume.profile.TryGet(out filmGrain);
        filmGrain.active = true;

        postProcessingVolume.profile.TryGet(out lensDistortion);
        lensDistortion.active = true;

        postProcessingVolume.profile.TryGet(out chromaticAberration);
        chromaticAberration.active = true;

        postProcessingVolume.profile.TryGet(out vignette);
        vignette.active = true;

    }

    public void Vignette_Display(bool isVisible)
    {
        postProcessingVolume.profile.TryGet(out vignette);
        vignette.active = isVisible;
    }

    public void FilmGrain_Display(bool isVisible)
    {
        postProcessingVolume.profile.TryGet(out filmGrain);
        filmGrain.active = isVisible;
    }

    public void LensDistotion_Display(bool isVisible)
    {
        postProcessingVolume.profile.TryGet(out lensDistortion);
        lensDistortion.active = isVisible;
    }

    public void ChromaticAberration_Display(bool isVisible)
    {
        postProcessingVolume.profile.TryGet(out chromaticAberration);
        chromaticAberration.active = isVisible;
    }

    // private void AudioSourceGet()
    // {
    //     AudioSource[] audioSources = GetComponents<AudioSource>();
    //     foreach (var source in audioSources)
    //     {
    //         if (source.clip.name == "radio-waves-248661")
    //         {
    //             audioSourceRadioWaves = source;
    //             break;
    //         }
    //     }
    // }
}
