using UnityEngine;
using UnityEngine.UI;

namespace TavernParty.Rendering
{
    /// <summary>
    /// Renders the game camera into a low-res RenderTexture whose aspect ratio always
    /// matches the screen, so the retro image fills the whole screen without stretching.
    /// Height is fixed (e.g. 240); width follows the screen aspect ratio.
    /// Put this on the game camera and assign a full-screen RawImage.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class PixelScreen : MonoBehaviour
    {
        [Tooltip("Vertical resolution in pixels. Lower = chunkier. Try 180, 240 or 270.")]
        [SerializeField] public int targetHeight = 240;

        [Tooltip("RawImage stretched to fill the canvas. Do NOT add an Aspect Ratio Fitter to it.")]
        [SerializeField] public RawImage display;

        Camera _cam;
        RenderTexture _rt;
        int _lastWidth;
        int _lastHeight;

        void Awake()
        {
            _cam = GetComponent<Camera>();
        }

        void OnEnable()
        {
            Rebuild();
        }

        void OnDisable()
        {
            Release();
        }

        void Update()
        {
            if (Screen.width != _lastWidth || Screen.height != _lastHeight)
                Rebuild();
        }

        void Rebuild()
        {
            _lastWidth = Screen.width;
            _lastHeight = Screen.height;
            if (_lastWidth <= 0 || _lastHeight <= 0) return;

            Release();

            int h = Mathf.Max(1, targetHeight);
            int w = Mathf.Max(1, Mathf.RoundToInt(h * (float)_lastWidth / _lastHeight));

            _rt = new RenderTexture(w, h, 24, RenderTextureFormat.Default)
            {
                name = "PixelScreenRT",
                filterMode = FilterMode.Point,
                useMipMap = false,
                autoGenerateMips = false
            };
            _rt.Create();

            _cam.targetTexture = _rt;
            if (display != null) display.texture = _rt;
        }

        void Release()
        {
            if (_cam != null) _cam.targetTexture = null;
            if (display != null) display.texture = null;
            if (_rt != null)
            {
                _rt.Release();
                Destroy(_rt);
                _rt = null;
            }
        }
    }
}