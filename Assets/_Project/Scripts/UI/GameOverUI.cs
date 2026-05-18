using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using LostSouls.Player;

namespace LostSouls.UI
{
    /// <summary>
    /// 플레이어 사망 시 YOU DIED 화면 표시 + 재시작 버튼 제공.
    ///
    /// 흐름:
    /// 1. PlayerHealth.OnDeath 구독
    /// 2. 사망 발생 시 사망 모션이 끝나기 직전까지 대기 (deathDisplayDelay 초)
    /// 3. 검정 배경 + "YOU DIED" 텍스트 + 재시작 버튼 페이드 인
    /// 4. 마우스 커서 노출
    /// 5. 버튼 클릭 시 현재 씬 리로드
    /// </summary>
    public class GameOverUI : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("구독할 PlayerHealth. 비우면 씬에서 자동 검색.")]
        [SerializeField] private PlayerHealth playerHealth;

        [Header("UI Elements")]
        [Tooltip("전체 게임오버 UI의 페이드 제어용. 이 컴포넌트 GameObject나 부모에 부착.")]
        [SerializeField] private CanvasGroup rootCanvasGroup;
        [Tooltip("재시작 버튼. 자체 클릭 이벤트로 RestartCurrentScene 호출.")]
        [SerializeField] private Button restartButton;

        [Header("Timing")]
        [Tooltip("사망 발생 후 UI 페이드 시작까지의 대기 시간 (초). 사망 모션이 거의 끝날 즈음.")]
        [SerializeField] private float deathDisplayDelay = 1.8f;
        [Tooltip("페이드 인 속도.")]
        [SerializeField] private float fadeSpeed = 1.2f;

        private bool _deathSequenceStarted;

        private void Awake()
        {
            if (playerHealth == null)
                playerHealth = FindAnyObjectByType<PlayerHealth>();

            // 시작 상태: 완전히 숨김, 상호작용 불가
            if (rootCanvasGroup != null)
            {
                rootCanvasGroup.alpha = 0f;
                rootCanvasGroup.interactable = false;
                rootCanvasGroup.blocksRaycasts = false;
            }

            if (restartButton != null)
                restartButton.onClick.AddListener(RestartCurrentScene);
        }

        private void OnEnable()
        {
            if (playerHealth != null)
                playerHealth.OnDeath += HandlePlayerDeath;
        }

        private void OnDisable()
        {
            if (playerHealth != null)
                playerHealth.OnDeath -= HandlePlayerDeath;
        }

        private void HandlePlayerDeath()
        {
            if (_deathSequenceStarted) return;
            _deathSequenceStarted = true;

            StartCoroutine(DeathSequence());
        }

        private IEnumerator DeathSequence()
        {
            // 사망 모션이 거의 끝날 때까지 대기 (다크소울 정석 — 모션 보여주고 UI 등장)
            yield return new WaitForSeconds(deathDisplayDelay);

            // 마우스 커서 노출 (게임 중엔 잠겨있을 수 있음)
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;

            // 상호작용 활성 + 페이드 인
            if (rootCanvasGroup != null)
            {
                rootCanvasGroup.interactable = true;
                rootCanvasGroup.blocksRaycasts = true;

                while (rootCanvasGroup.alpha < 1f)
                {
                    rootCanvasGroup.alpha += fadeSpeed * Time.deltaTime;
                    yield return null;
                }
                rootCanvasGroup.alpha = 1f;
            }
        }

        /// <summary>
        /// 재시작 버튼 onClick에 연결됨. 현재 씬 리로드.
        /// </summary>
        public void RestartCurrentScene()
        {
            // Time.timeScale을 멈춰뒀다면 복구 (현재 단계엔 멈춤 없음 — 다만 향후 일시정지 기능 도입 시 안전)
            Time.timeScale = 1f;

            Scene current = SceneManager.GetActiveScene();
            SceneManager.LoadScene(current.buildIndex);
        }
    }
}