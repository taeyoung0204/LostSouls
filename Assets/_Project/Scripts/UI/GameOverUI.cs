using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using LostSouls.Player;

namespace LostSouls.UI
{
    /// <summary>
    /// 플레이어 사망 시 YOU DIED 화면 표시 후 일정 시간 후 자동으로 현재 씬을 리로드.
    ///
    /// 흐름:
    /// 1. PlayerHealth.OnDeath 구독
    /// 2. 사망 발생 시 deathSoundDelay 후 YOU DIED 사운드 재생
    /// 3. deathDisplayDelay 후 화면 페이드 인 (검정 배경 + "YOU DIED" 텍스트)
    /// 4. 페이드 완료 후 reloadDelay 동안 더 보여줌
    /// 5. 현재 GameScene 자동 리로드 (= 마지막 모닥불에서 다시 시작 느낌)
    ///
    /// 다크소울 정석 — 사망은 버튼 누를 필요 없이 자연스럽게 리스폰.
    /// 타이틀 화면으로 명시적 복귀는 ESC 메뉴에서 따로 처리.
    /// </summary>
    public class GameOverUI : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("구독할 PlayerHealth. 비우면 씬에서 자동 검색.")]
        [SerializeField] private PlayerHealth playerHealth;

        [Header("UI Elements")]
        [Tooltip("전체 게임오버 UI의 페이드 제어용. 이 컴포넌트 GameObject나 부모에 부착.")]
        [SerializeField] private CanvasGroup rootCanvasGroup;

        [Header("Timing")]
        [Tooltip("사망 발생 후 UI 페이드 시작까지의 대기 시간 (초). 사망 모션이 거의 끝날 즈음.")]
        [SerializeField] private float deathDisplayDelay = 1.8f;
        [Tooltip("사망 발생 후 YOU DIED 효과음 재생까지의 대기 시간 (초). " +
                 "deathDisplayDelay보다 작아야 효과음이 페이드보다 일찍 시작됨.")]
        [SerializeField] private float deathSoundDelay = 0.3f;
        [Tooltip("페이드 인 속도.")]
        [SerializeField] private float fadeSpeed = 1.2f;
        [Tooltip("페이드 완료 후 씬 리로드까지 대기 시간 (초). 사용자가 YOU DIED를 충분히 음미하는 시간.")]
        [SerializeField] private float reloadDelay = 3f;

        private bool _deathSequenceStarted;

        private void Awake()
        {
            if (playerHealth == null)
                playerHealth = FindAnyObjectByType<PlayerHealth>();

            // 시작 상태: 완전히 숨김
            if (rootCanvasGroup != null)
            {
                rootCanvasGroup.alpha = 0f;
                rootCanvasGroup.interactable = false;
                rootCanvasGroup.blocksRaycasts = false;
            }
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
            // 1단계: 효과음 먼저 (짧은 딜레이). 페이드보다 일찍 시작되어
            //         페이드 시점에는 효과음의 클라이맥스/후반부가 들리는 구조.
            yield return new WaitForSeconds(deathSoundDelay);

            // YOU DIED 효과음 — 2D 사운드, SFX 그룹 출력 (옵션 메뉴 SFX 볼륨 영향).
            if (LostSouls.Audio.AudioManager.Instance != null &&
                LostSouls.Audio.AudioManager.Instance.ClipBank != null)
            {
                LostSouls.Audio.AudioManager.Instance.PlayUISound(
                    LostSouls.Audio.AudioManager.Instance.ClipBank.youDiedSting);
            }

            // 2단계: 페이드 시작까지 남은 시간 대기.
            float remainingDelay = deathDisplayDelay - deathSoundDelay;
            if (remainingDelay > 0f)
                yield return new WaitForSeconds(remainingDelay);

            // 페이드 인 (마우스 커서는 노출 안 함 — 자동 리로드라 입력 필요 없음)
            if (rootCanvasGroup != null)
            {
                while (rootCanvasGroup.alpha < 1f)
                {
                    rootCanvasGroup.alpha += fadeSpeed * Time.deltaTime;
                    yield return null;
                }
                rootCanvasGroup.alpha = 1f;
            }

            // 3단계: YOU DIED 화면 머무는 시간 (사용자가 음미)
            yield return new WaitForSeconds(reloadDelay);

            // 4단계: 현재 씬 리로드 (마지막 체크포인트에서 재시작 느낌)
            ReloadCurrentScene();
        }

        private void ReloadCurrentScene()
        {
            // Time.timeScale 안전 복원 (혹시 다른 시스템이 멈춰뒀을 경우 대비)
            Time.timeScale = 1f;

            // 마우스 커서 복원 (게임 중 잠겨있을 수 있음 — 다음 씬에서 다시 잠금)
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;

            // 현재 활성 씬 리로드. buildIndex 사용 — 씬 이름 변경에도 강함.
            Scene current = SceneManager.GetActiveScene();
            SceneManager.LoadScene(current.buildIndex);
        }
    }
}