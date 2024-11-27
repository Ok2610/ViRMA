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
using System.IO;

public class VideoPlayerController : MonoBehaviour
{
    public SteamVR_ActionSet actionSet;
    public SteamVR_Action_Boolean selectAction;
    private VideoPlayer videoPlayer;
    private AudioSource audioSource;
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
    private readonly string localMediaDirectory = "C:/Users/r-u-t/Desktop/Medias";
    private ViRMA_GlobalsAndActions globals;

    void Awake()
    {
        actionSet.Activate();
        globals = Player.instance.gameObject.GetComponent<ViRMA_GlobalsAndActions>();

        videoPlayer = gameObject.GetComponent<VideoPlayer>();
        audioSource = gameObject.AddComponent<AudioSource>();

        RenderTexture renderTexture = new RenderTexture(1280, 720, 24)
        {
            name = "VideoRenderTexture"
        };
        videoPlayer.targetTexture = renderTexture;
        screen.GetComponent<Renderer>().material.mainTexture = renderTexture;

        videoPlayer.prepareCompleted += (_) => {
            videoProgressBar.GetComponent<Slider>().maxValue = videoPlayer.frameCount;
            TimeSpan totalTime = TimeSpan.FromSeconds(videoPlayer.length);
            durationText.text = "0:00/" + totalTime.Minutes + ":" + totalTime.Seconds.ToString("00");
        };

        segmentData = new List<ViRMA_GlobalsAndActions.SegmentData>();
    }

    void Update()
    {
        if (videoPlayer.isPrepared)
        {
            TimeSpan elapsed = TimeSpan.FromSeconds(videoPlayer.time);
            string elapsedTime = elapsed.Minutes.ToString() + ":" + elapsed.Seconds.ToString("00");
            durationText.text = Regex.Replace(durationText.text, updateTimeRegex, elapsedTime);
            videoProgressBar.GetComponent<Slider>().value = videoPlayer.frame;
        }
        else if (audioSource.clip != null)
        {
            TimeSpan elapsed = TimeSpan.FromSeconds(audioSource.time);
            string elapsedTime = elapsed.Minutes.ToString() + ":" + elapsed.Seconds.ToString("00");
            durationText.text = Regex.Replace(durationText.text, updateTimeRegex, elapsedTime);
            videoProgressBar.GetComponent<Slider>().value = audioSource.time / audioSource.clip.length;
        }

        playButtonText.text = (videoPlayer.isPlaying || audioSource.isPlaying) ? "Pause" : "Play";
        pauseImage.enabled = (videoPlayer.isPlaying || audioSource.isPlaying);
        playImage.enabled = !(videoPlayer.isPlaying || audioSource.isPlaying);

        MovePlayer();
    }

    public void SetVideo(string fileName, bool originalVideo = false)
    {
        PlayMedia(fileName, originalVideo);
    }

    private void PlayMedia(string fileName, bool originalMedia = false)
    {
        Debug.Log("filename: " + fileName);
        string lastPartFileName = Path.GetFileNameWithoutExtension(fileName);
        Debug.Log("lastPartFileName: " + lastPartFileName);

        string[] files = Directory.GetFiles(localMediaDirectory, lastPartFileName + ".*");

        Debug.Log("Files found: " + string.Join(", ", files));

        if (files.Length > 0)
        {
            // If the file is found, determine its type by its extension and play it
            string foundFile = files[0];
            string extension = Path.GetExtension(foundFile).ToLower();

            Debug.Log("Found local file: " + foundFile);

            if (extension == ".mp4")
            {
                PlayVideo(foundFile);
            }
            else if (extension == ".mp3")
            {
                PlayAudio(foundFile);
            }
            else
            {
                Debug.LogError("Unsupported file type: " + extension);
            }
        }
        else if (fileName.Contains("spotify"))
        {
            Debug.Log("File not found locally, attempting to download from Spotify: " + fileName);
            DownloadAndPlayFromSpotify(fileName, lastPartFileName);
        }
        else
        {
            Debug.LogError("File not found locally and is not a Spotify link: " + fileName);
        }
    }

    private void PlayVideo(string fileName)
    {
        videoPlayer.url = fileName;
        videoPlayer.Prepare();
        videoPlayer.Play();
        videoSource.text = $"Source: {videoPlayer.url}";
    }

    private void PlayAudio(string fileName)
    {
        StartCoroutine(LoadAudioClip(fileName));
    }

    private IEnumerator<UnityEngine.Networking.UnityWebRequestAsyncOperation> LoadAudioClip(string fileURI)
    {
        using (UnityEngine.Networking.UnityWebRequest uwr = UnityEngine.Networking.UnityWebRequestMultimedia.GetAudioClip("file://" + fileURI, AudioType.MPEG))
        {
            yield return uwr.SendWebRequest();

            if (uwr.result == UnityEngine.Networking.UnityWebRequest.Result.ConnectionError || uwr.result == UnityEngine.Networking.UnityWebRequest.Result.ProtocolError)
            {
                Debug.LogError(uwr.error);
            }
            else
            {
                audioSource.clip = UnityEngine.Networking.DownloadHandlerAudioClip.GetContent(uwr);
                audioSource.Play();
                videoSource.text = $"Source: {fileURI}";
            }
        }
    }

    private void DownloadAndPlayFromSpotify(string spotifyURI, string lastPartFileName)
    {
        // Placeholder for Spotify download logic
        Debug.Log("Downloading from Spotify: " + spotifyURI);

        // Download with Python script

        string pythonScriptPath = "C:/Users/r-u-t/Desktop/Work/spotify-data-parser/utils/unitySongDownload.py";
        string arguments = $"{spotifyURI} {lastPartFileName} {localMediaDirectory}";

        System.Diagnostics.ProcessStartInfo start = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "python",
            Arguments = $"{pythonScriptPath} {arguments}",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using (System.Diagnostics.Process process = System.Diagnostics.Process.Start(start))
        {
            using (System.IO.StreamReader reader = process.StandardOutput)
            {
                string result = reader.ReadToEnd();
                Debug.Log("Results: " + result);
            }
        }

        string downloadedFilePath  = Path.Combine(localMediaDirectory, lastPartFileName + ".mp3");

        if (File.Exists(downloadedFilePath ))
        {
            // Play the downloaded file
            PlayAudio(downloadedFilePath );
        }
        else
        {
            Debug.LogError("Failed to download or save the file from Spotify.");
        }
    }

    public void SetContextVideo(string fileName)
    {
        float progressWidth = videoProgressBar.GetComponent<RectTransform>().rect.width;
        var highlight = GameObject.Find("SegmentHighlight").GetComponent<RectTransform>();

        startFrame = segmentData[activeSegment].startFrame;
        endFrame = segmentData[activeSegment].endFrame;

        highlight.anchoredPosition = Vector3.right * ((float)startFrame / videoPlayer.frameCount * progressWidth);
        highlight.sizeDelta = new Vector2((float)segmentData[activeSegment].frameCount / videoPlayer.frameCount * progressWidth, highlight.sizeDelta.y);

        segmentID.text = $"Segment: {activeSegment}";

        videoPlayer.frame = startFrame;
        segmentStart = TimeSpan.FromSeconds(videoPlayer.time);
    }

    public void Mute(bool newStatus)
    {
        videoPlayer.SetDirectAudioMute(0, newStatus);
        audioSource.mute = newStatus;
    }

    public void SetPlaybackSpeed(Text label)
    {
        float speed = label.text == "Normal" ? 1.0f : float.Parse(label.text, CultureInfo.InvariantCulture);
        videoPlayer.playbackSpeed = speed;
        audioSource.pitch = speed;
    }

    public void Play(TMP_Text t)
    {
        switch (t.text)
        {
            case "Play":
                if (videoPlayer.isPrepared) videoPlayer.Play();
                else if (audioSource.clip != null) audioSource.Play();
                break;
            case "Pause":
                if (videoPlayer.isPlaying) videoPlayer.Pause();
                else if (audioSource.isPlaying) audioSource.Pause();
                break;
        }
    }

    public void Loop(bool newStatus)
    {
        videoPlayer.isLooping = newStatus;
        audioSource.loop = newStatus;
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
        if (playerMoving)
        {
            if (selectAction.GetState(handInteracting.handType))
            {
                if (handInteracting)
                {
                    if (transform.parent != handInteracting) transform.parent = handInteracting.transform;
                }
            }
            else
            {
                if (handInteracting && transform.parent == handInteracting.transform)
                {
                    transform.parent = null;
                    playerMoving = false;
                }
            }
        }
    }
}
