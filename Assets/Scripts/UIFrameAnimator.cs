using UnityEngine;
using UnityEngine.UI;

namespace TavernParty.UI
{
    /// <summary>
    /// Plays a list of sprites as a looping animation on a UI Image
    /// (used for the animated main menu background).
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class UIFrameAnimator : MonoBehaviour
    {
        [SerializeField] Sprite[] frames;
        [SerializeField] float framesPerSecond = 8f;

        Image _image;
        float _timer;
        int _index;

        void Awake()
        {
            _image = GetComponent<Image>();
        }

        void OnEnable()
        {
            _timer = 0f;
            _index = 0;
            if (_image != null && frames != null && frames.Length > 0)
                _image.sprite = frames[0];
        }

        void Update()
        {
            if (frames == null || frames.Length < 2 || framesPerSecond <= 0f) return;

            _timer += Time.unscaledDeltaTime;
            float secondsPerFrame = 1f / framesPerSecond;

            while (_timer >= secondsPerFrame)
            {
                _timer -= secondsPerFrame;
                _index = (_index + 1) % frames.Length;
                _image.sprite = frames[_index];
            }
        }
    }
}