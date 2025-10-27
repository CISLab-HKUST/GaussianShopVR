using OpenAI;
using UnityEngine;
using TMPro;
using UnityEngine.InputSystem;

namespace Samples.Whisper
{
    public class WhisperVRInputField : MonoBehaviour
    {
        [SerializeField]
        private TextMeshProUGUI message;

        private readonly string fileName = "output.wav";
        private AudioClip clip;
        private bool isRecording;
        private OpenAIApi openai;

        private void Awake()
        {
            openai = new OpenAIApi();
        }

        private void Start()
        {
            message.enabled = false;
        }

        public void StartRecording()
        {
            Debug.Log("StartRecording");
            isRecording = true;
            message.enabled = true;
            message.text = "Recording...";
#if !UNITY_WEBGL
            clip = Microphone.Start(null, false, 30, 44100);
#endif
        }

        public async void EndRecording()
        {
            Debug.Log("EndRecording");
            isRecording = false;
            message.text = "Transcripting...";

#if !UNITY_WEBGL
            Microphone.End(null);
#endif

            byte[] data = SaveWav.Save(fileName, clip);

            var req = new CreateAudioTranscriptionsRequest
            {
                FileData = new FileData() { Data = data, Name = "audio.wav" },
                Model = "whisper-1",
                Language = "en"
            };
            var res = await openai.CreateAudioTranscription(req);

            message.text = res.Text;
        }
    }
}
