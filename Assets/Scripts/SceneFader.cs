using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SceneFader : MonoBehaviour
{
    public static SceneFader Instance { get; private set; }

    [Header("페이드 설정")]
    [SerializeField] private float fadeDuration = 0.5f;

    private CanvasGroup _fadeCanvasGroup;
    private bool _isFading = false;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            CreateFadeUI();
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    private void Start()
    {
        // 첫 씬 시작 시 밝아지는 연출
        if (_fadeCanvasGroup != null)
        {
            _fadeCanvasGroup.alpha = 1f;
            StartCoroutine(FadeInRoutine());
        }
    }

    /// <summary>
    /// 인스펙터 설정 없이 코드로 완벽한 최상단 검은 캔버스를 자동 생성
    /// </summary>
    private void CreateFadeUI()
    {
        GameObject canvasObj = new GameObject("Runtime_FadeCanvas");
        canvasObj.transform.SetParent(transform);

        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32767; // 유니티 최상단 정렬 값

        canvasObj.AddComponent<CanvasScaler>();
        canvasObj.AddComponent<GraphicRaycaster>();

        _fadeCanvasGroup = canvasObj.AddComponent<CanvasGroup>();
        _fadeCanvasGroup.alpha = 0f;
        _fadeCanvasGroup.blocksRaycasts = false;

        GameObject imageObj = new GameObject("FadeBlackImage");
        imageObj.transform.SetParent(canvasObj.transform);

        Image img = imageObj.AddComponent<Image>();
        img.color = Color.black;
        img.raycastTarget = false;

        RectTransform rt = imageObj.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;
    }

    public void FadeToScene(string sceneName)
    {
        if (_isFading) return;
        StartCoroutine(FadeOutAndLoadSceneRoutine(sceneName));
    }

    private IEnumerator FadeOutAndLoadSceneRoutine(string sceneName)
    {
        _isFading = true;

        if (_fadeCanvasGroup != null)
        {
            _fadeCanvasGroup.blocksRaycasts = true;

            // 1. 화면 어두워짐
            float timer = 0f;
            while (timer < fadeDuration)
            {
                timer += Time.unscaledDeltaTime;
                _fadeCanvasGroup.alpha = Mathf.Clamp01(timer / fadeDuration);
                yield return null;
            }
            _fadeCanvasGroup.alpha = 1f;
        }

        // 2. 씬 로드
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName);
        while (!asyncLoad.isDone)
        {
            yield return null;
        }

        // 씬 로드 완료 후 1프레임 대기
        yield return null;

        // 3. 화면 밝아짐
        yield return StartCoroutine(FadeInRoutine());

        _isFading = false;
    }

    private IEnumerator FadeInRoutine()
    {
        if (_fadeCanvasGroup == null) yield break;

        float timer = 0f;
        while (timer < fadeDuration)
        {
            timer += Time.unscaledDeltaTime;
            _fadeCanvasGroup.alpha = Mathf.Clamp01(1f - (timer / fadeDuration));
            yield return null;
        }
        _fadeCanvasGroup.alpha = 0f;
        _fadeCanvasGroup.blocksRaycasts = false;
    }
}