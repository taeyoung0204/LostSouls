using UnityEngine;
using TMPro;

namespace LostSouls.UI
{
    /// <summary>
    /// 상호작용 가능 표시 UI. 표지판 등 가까이 갔을 때 "Press E to interact" 같은 안내 표시.
    /// 다크소울/엘든링 정석 — 상호작용 가능 시점을 사용자에게 알림.
    ///
    /// 사용:
    /// - SignpostInteractable이 OnTriggerEnter 시 Show(message)
    /// - OnTriggerExit 시 Hide()
    /// - 표지판과 무관하게 다른 상호작용 가능 오브젝트(미래)에서도 재사용 가능
    /// </summary>
    public class InteractionPromptUI : MonoBehaviour
    {
        public static InteractionPromptUI Instance { get; private set; }

        [Header("UI")]
        [Tooltip("프롬프트 GameObject. 시작 시 비활성 상태.")]
        [SerializeField] private GameObject promptObject;
        [Tooltip("안내 텍스트 (예: 'Press E to read sign').")]
        [SerializeField] private TextMeshProUGUI promptText;

        private void Awake()
        {
            // 싱글톤 — 씬에 하나만 존재. 여러 상호작용 오브젝트가 같은 UI 공유.
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (promptObject != null)
                promptObject.SetActive(false);
        }

        public void Show(string message)
        {
            if (promptText != null) promptText.text = message;
            if (promptObject != null) promptObject.SetActive(true);
        }

        public void Hide()
        {
            if (promptObject != null) promptObject.SetActive(false);
        }
    }
}