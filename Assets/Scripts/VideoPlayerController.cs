using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using Valve.VR;

public class VideoPlayerController : MonoBehaviour
{
    public SteamVR_ActionSet actionSet;
    private Slider videoTimeline;
    private VideoPlayer videoPlayer;
    public GameObject screen;

    // Temporary
    private readonly string prefix = "10vids/";
    private readonly string suffix = ".mp4";
    private readonly string localMediaDirectory = "C:/Users/ViRMA-Video/Datasets/VBS/";

    void Awake()
    {
        actionSet.Activate();
        videoPlayer = gameObject.GetComponent<VideoPlayer>();
        videoTimeline = gameObject.GetComponentInChildren<Slider>();

        RenderTexture renderTexture = new RenderTexture(1280, 720, 24)
        {
            name = "VideoRenderTexture"
        };
        videoPlayer.targetTexture = renderTexture;
        screen.GetComponent<Renderer>().material.mainTexture = renderTexture;
    }

    public void setVideo(string fileName)
    {
        string pattern = @"keyframes/(\d+)/\d+_(\d+).jpg";

        string result = Regex.Replace(fileName, pattern, m => $"{m.Groups[1].Value}/{m.Groups[1].Value}_{m.Groups[2].Value}");
        videoPlayer.url = localMediaDirectory + prefix + result + suffix;
        videoPlayer.Prepare();
    }

    void Update()
    {
        videoTimeline.maxValue = videoPlayer.frameCount; // This should be possible somewhere else
        videoTimeline.value = videoPlayer.frame;
    }

    public void Mute(bool newStatus)
    {
        videoPlayer.SetDirectAudioMute(0, newStatus);
    }

    public void SetPlaybackSpeed(Text label)
    {
        videoPlayer.playbackSpeed = label.text == "Normal" ? 1.0f : float.Parse(label.text);
    }

    public void Play(Text t)
    {
        switch(t.text) {
            case "Play": {
                t.text = "Pause";
                videoPlayer.Play();
                break;
            }
            case "Pause": {
                t.text = "Play";
                videoPlayer.Pause();
                break;
            }
        }
    }

    public void Scroll(float p)
    {
        Debug.Log(p);
    }

    public void Loop(bool newStatus)
    {
        videoPlayer.isLooping = newStatus;
    }
}
