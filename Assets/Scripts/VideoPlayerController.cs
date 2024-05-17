using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Valve.VR.InteractionSystem;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using Valve.VR;
using System.Globalization;

public class VideoPlayerController : MonoBehaviour
{
    public SteamVR_ActionSet actionSet;
    public SteamVR_Action_Boolean selectAction;
    private VideoPlayer videoPlayer;
    public GameObject screen;
    private readonly string updateTimeRegex = @"^[^/]+";
    public bool inContext = false;
    private Hand handInteracting;
    private bool playerMoving = false;

    // UI Elements
    public TMP_Text durationText;
    public TMP_Text videoSource;
    public TMP_Text segmentID;
    public TMP_Text segmentDurationText;
    public TMP_Text playButtonText;
    public RawImage playImage;
    public RawImage pauseImage;
    public GameObject exitButton;
    public GameObject moveButton;
    public GameObject videoProgressBar;
    public List<string> localSegments;

    // Main player info
    private string videoName = "";
    private int activeSegment;
    private int startFrame;
    private int endFrame;
    public TimeSpan segmentStart;
    private List<ViRMA_GlobalsAndActions.SegmentData> segmentData;


    // Temporary
    private readonly string prefix = "10vids/";
    private readonly string suffix = ".mp4";

    private readonly string localMediaDirectory = "C:/Users/ViRMA-Video/Datasets/VBS/";
    private ViRMA_GlobalsAndActions globals;


    void Awake()
    {
        actionSet.Activate();
        globals = Player.instance.gameObject.GetComponent<ViRMA_GlobalsAndActions>();
        videoPlayer = gameObject.GetComponent<VideoPlayer>();

        RenderTexture renderTexture = new RenderTexture(1280, 720, 24)
        {
            name = "VideoRenderTexture"
        };
        videoPlayer.targetTexture = renderTexture;
        screen.GetComponent<Renderer>().material.mainTexture = renderTexture;

        // Set the total length when prepare is finished
        videoPlayer.prepareCompleted += (_) => {
            videoProgressBar.GetComponent<Slider>().maxValue = videoPlayer.frameCount;
            TimeSpan totalTime = TimeSpan.FromSeconds(videoPlayer.length);
            durationText.text = "0:00/" + totalTime.Minutes + ":" + totalTime.Seconds.ToString("00");
        };
        
        segmentData = new List<ViRMA_GlobalsAndActions.SegmentData>();
    }

    public void Start()
    {
        if (inContext)
        {
        segmentData = globals.globalSegmentData[videoName];
        }
        // Cross reference (Cell Explorer)
        //Dictionary<string, SegmentData> videoScrollLine = new List<string>();
    }

    void Update()
    {
        // Update elapsed time
        TimeSpan elapsed = TimeSpan.FromSeconds(videoPlayer.time);
        string elapsedTime = elapsed.Minutes.ToString() + ":" + elapsed.Seconds.ToString("00");
        durationText.text = Regex.Replace(durationText.text, updateTimeRegex, elapsedTime);

        if (inContext)
        {
            TimeSpan segmentElapsed = TimeSpan.FromSeconds(videoPlayer.time) - segmentStart;
            string segmentElapsedTime = segmentElapsed.Minutes.ToString() + ":" + segmentElapsed.Seconds.ToString("00");
            segmentDurationText.text = Regex.Replace(segmentDurationText.text, updateTimeRegex, segmentElapsedTime);
        }

        videoProgressBar.GetComponent<Slider>().value = videoPlayer.frame;

        //  if (inContext){
        //     if (videoPlayer.isLooping && videoPlayer.frame >= endFrame)
        //     {
        //         videoPlayer.frame = startFrame;

        //     }
        //  }
        // else {
        //     //start_frame = segmentData[]
            
        // }

        // Update play button text and image
        playButtonText.text = videoPlayer.isPlaying ? "Pause" : "Play";
        pauseImage.enabled = videoPlayer.isPlaying;
        playImage.enabled = !videoPlayer.isPlaying;

        MovePlayer();
    }

    private bool CanStep(bool direction)
    {
        if (direction) {
            if ((ulong)(videoPlayer.frame + 1) > videoPlayer.frameCount)
                return false;
        } else {
            if ((ulong)(videoPlayer.frame - 1) < 0)
                return false;
        }
        return true;
    }

    public void FrameForward()
    {
        if (videoPlayer.isPlaying) {
            videoPlayer.Pause();
        }
        if (CanStep(true)) {
            videoPlayer.frame++;
        }
    }

    public void FrameBackward()
    {
        if (videoPlayer.isPlaying) {
            videoPlayer.Pause();
        }
        if (CanStep(false)) {
            videoPlayer.frame--;
        }
    }

    private string ParseVideoURL(string fileName, bool originalVideo)
    {
        string pattern = @"(\d+)_(\d+)\.jpg$";
        string result;
        string videoIndex;
        string segmentIndex;

        Match match = Regex.Match(fileName, pattern);
        if (match.Success)
        {
            videoIndex = match.Groups[1].Value;
            segmentIndex = match.Groups[2].Value;
        }
        else
        {
            return "";
        }

        if (!originalVideo) {
            result = $"{videoIndex}/{videoIndex}_{segmentIndex}";
        } else {
            result = $"{videoIndex}/{videoIndex}";
            videoName = videoIndex;
            activeSegment = int.Parse(segmentIndex);
        }
        videoSource.text = $"Source: {result + suffix}"; 
        
        return localMediaDirectory + prefix + result + suffix;
    }

    public void SetVideo(string fileName, bool originalVideo=false)
    {
        videoPlayer.url = ParseVideoURL(fileName, originalVideo);
        if (originalVideo) {
            exitButton.SetActive(false);
            videoPlayer.prepareCompleted += (_) => SetContextVideo(fileName);
        }
        videoPlayer.Prepare();
    }

    public void SetContextVideo(string fileName)
    {
        float progressWidth = videoProgressBar.GetComponent<RectTransform>().rect.width;
        var highlight = GameObject.Find("SegmentHighlight").GetComponent<RectTransform>();
        //This is disgusting but used to set videoName and activeSegment...
        ParseVideoURL(fileName, true);

        startFrame = segmentData[activeSegment].startFrame;
        endFrame = segmentData[activeSegment].endFrame;

        highlight.anchoredPosition = Vector3.right * ((float)startFrame / videoPlayer.frameCount * progressWidth);
        highlight.sizeDelta = new Vector2((float)segmentData[activeSegment].frameCount / videoPlayer.frameCount * progressWidth, highlight.sizeDelta.y);
        
        segmentID.text = $"Segment: {activeSegment}";

        videoPlayer.frame = startFrame;
        segmentStart = TimeSpan.FromSeconds(videoPlayer.time);

        
        //TimeSpan segmentTotalTime = TimeSpan.FromSeconds((double) new decimal(segmentData[activeSegment].frameCount / videoPlayer.frameRate));
        //segmentDurationText.text = "0:00/" + segmentTotalTime.Minutes + ":" + segmentTotalTime.Seconds.ToString("00");
        // videoPlayer.Prepare();
        // videoPlayer.Pause();
    }


    public void Mute(bool newStatus)
    {
        videoPlayer.SetDirectAudioMute(0, newStatus);
    }

    public void SetPlaybackSpeed(Text label)
    {
        videoPlayer.playbackSpeed = label.text == "Normal" ? 1.0f : float.Parse(label.text, CultureInfo.InvariantCulture);
    }

    public void Play(TMP_Text t)
    {
        switch(t.text) {
            case "Play": {
                videoPlayer.Play();
                break;
            }
            case "Pause": {
                videoPlayer.Pause();
                break;
            }
        }
    }

    public void Loop(bool newStatus)
    {
        videoPlayer.isLooping = newStatus;
    }

    public void DestroySelf()
    {
        Destroy(gameObject);
    }

    public void SetPlayerMoving()
    {
        Debug.Log("Setting player moving");
        handInteracting = moveButton.GetComponent<ViRMA_UiElement>().handInteractingWithUi;
        playerMoving = true;
    }

    public void MovePlayer()
    {
        if (playerMoving) {
            // Is select (A) pressed 
            if (selectAction.GetState(handInteracting.handType)) {
                if (handInteracting) {
                    
                    if (transform.parent != handInteracting) {
                        transform.parent = handInteracting.transform;
                    }
                }
            } else {
                if (handInteracting)
                {
                    if (transform.parent == handInteracting.transform)
                    {
                        transform.parent = null;
                        playerMoving = false;
                    }
                }
            }
        }
    }
}
