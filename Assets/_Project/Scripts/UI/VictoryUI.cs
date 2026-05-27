using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using LostSouls.Boss;
using LostSouls.Player;

namespace LostSouls.UI
{
    /// <summary>
    /// 보스 처치 시 표시되는 승리 화면.
    ///
    /// 흐름:
    /// 1. BossHealth.OnDeath 구독
    /// 2. 사망 발생 시 victoryDisplayDelay 후 페이드인 시작
    /// 3. 효과음 재생 (선택)
    /// 4. 화면 페이드인 (검정 배경 + "VICTORY" 텍스트 + Return to Title 버튼)
    /// 5. 마우스 입력 차단 (PauseMenu와 동일 방식 — 카메라 회전/공격 불가)
    /// 6. 사용자가 Return to Title 클릭하면 타이틀로
    ///
    /// GameOverUI와 다른 점:
    /// - 자동 복귀 X — 사용자가 음미하고 명시적으로 버튼 클릭
    /// - 마우스 커서 노출 (버튼 클릭 필요)
    /// - 마우스 입력 차단 (사체에 칼질하거나 카메라 빙빙 도는 거 방지)
    /// </summary>
    public class VictoryUI : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("구독할 BossHealth. 비우면 씬에서 자동 검색.")]
        [SerializeField] private BossHealth bossHealth;
        [Tooltip("마우스 입력 차단할 PlayerController. 비우면 자동 검색.")]
        [SerializeField] private PlayerController playerController;
        [Tooltip("승리 화면 시 비활성할 카메라 컴포넌트들 (보통 CinemachineInputAxisController). " +
                 "PauseMenuController에 연결한 것과 같은 컴포넌트.")]
        [SerializeField] private List<Behaviour> cameraInputComponentsToDisable = new List<Behaviour>();

        [Header("UI Elements")]
        [Tooltip("전체 승리 UI의 페이드 제어용. 이 컴포넌트 GameObject나 부모에 부착.")]
        [SerializeField] private CanvasGroup rootCanvasGroup;
        [Tooltip("타이틀로 돌아가기 버튼.")]
        [SerializeField] private Button returnToTitleButton;

        [Header("Timing")]
        [Tooltip("보스 사망 후 UI 페이드 시작까지의 대기 시간 (초). " +
                 "보스 사망 모션이 보이고 잠시 음미할 시간.")]
        [SerializeField] private float victoryDisplayDelay = 2.5f;
        [Tooltip("승리 효과음 재생까지의 대기 시간 (초). " +
                 "victoryDisplayDelay보다 작아야 효과음이 페이드보다 일찍 시작됨.")]
        [SerializeField] private float victorySoundDelay = 0.5f;
        [Tooltip("페이드 인 속도.")]
        [SerializeField] private float fadeSpeed = 1.0f;

        [Header("Scene")]
        [Tooltip("Return to Title 클릭 시 로드할 씬 이름.")]
        [SerializeField] private string titleSceneName = "TitleScene";

        private bool _victorySequenceStarted;

        private void Awake()
        {
            if (bossHealth == null)
                bossHealth = FindAnyObjectByType<BossHealth>();
            if (playerController == null)
                playerController = FindAnyObjectByType<PlayerController>();

            // 시작 상태: 완전히 숨김
            if (rootCanvasGroup != null)
            {
                rootCanvasGroup.alpha = 0f;
                rootCanvasGroup.interactable = false;
                rootCanvasGroup.blocksRaycasts = false;
            }

            if (returnToTitleButton != null)
                returnToTitleButton.onClick.AddListener(ReturnToTitle);
        }

        private void OnEnable()
        {
            if (bossHealth != null)
                bossHealth.OnDeath += HandleBossDeath;
        }

        private void OnDisable()
        {
            if (bossHealth != null)
                bossHealth.OnDeath -= HandleBossDeath;
        }

        private void HandleBossDeath()
        {
            if (_victorySequenceStarted) return;
            _victorySequenceStarted = true;

            // 보스 사망 즉시 임팩트 사운드 — "결정타" 느낌. 2D UI 사운드라 위치 무관 화면 전체 울림.
            // 이 사운드 이후에 보스 사망 보이스 (3D, BossAudio) + 사망 모션이 이어지고,
            // 잠시 후 VictoryUI 페이드인 + victorySting 효과음.
            // ClipBank의 bossDefeated가 비어있으면 조용히 스킵.
            if (LostSouls.Audio.AudioManager.Instance != null &&
                LostSouls.Audio.AudioManager.Instance.ClipBank != null)
            {
                LostSouls.Audio.AudioManager.Instance.PlayUISound(
                    LostSouls.Audio.AudioManager.Instance.ClipBank.bossDefeated);
            }

            StartCoroutine(VictorySequence());
        }

        private IEnumerator VictorySequence()
        {
            // 1단계: 효과음 먼저 (짧은 딜레이). 페이드보다 일찍 시작.
            yield return new WaitForSeconds(victorySoundDelay);

            // 승리 효과음 (선택) — 2D 사운드, SFX 그룹 출력.
            // ClipBank의 victorySting이 비어있으면 조용히 스킵.
            if (LostSouls.Audio.AudioManager.Instance != null &&
                LostSouls.Audio.AudioManager.Instance.ClipBank != null)
            {
                LostSouls.Audio.AudioManager.Instance.PlayUISound(
                    LostSouls.Audio.AudioManager.Instance.ClipBank.victorySting);
            }

            // 2단계: 페이드 시작까지 남은 시간 대기
            float remainingDelay = victoryDisplayDelay - victorySoundDelay;
            if (remainingDelay > 0f)
                yield return new WaitForSeconds(remainingDelay);

            // 마우스 커서 노출 (버튼 클릭 필요)
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;

            // 마우스 입력 차단 — 사체에 칼질 / 카메라 빙빙 도는 거 방지.
            // 키보드는 그대로 (실제론 사망/승리 후 키보드 입력도 의미 없지만 굳이 막을 필요는 없음).
            // PauseMenu와 동일 방식 — Return to Title 누르면 씬 전환이라 복구 불필요.
            if (playerController != null) playerController.SetMouseInputEnabled(false);
            SetCameraInputComponents(false);

            // 페이드 인 + 상호작용 활성
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

        private void ReturnToTitle()
        {
            // Time.timeScale 안전 복원
            Time.timeScale = 1f;

            // 마우스 커서 노출 유지 (TitleScene에서 사용)
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;

            // 안전망 — BGM 정지. 보스 사망 시 BossRoomTrigger.HandleBossDeath가 이미 StopBGM 호출하므로
            // 현재 흐름에선 무음 상태일 가능성 높지만, 향후 BGM 흐름 바뀌어도 TitleScene으로 BGM 따라가지 않게 보호.
            if (LostSouls.Audio.AudioManager.Instance != null)
                LostSouls.Audio.AudioManager.Instance.StopBGM(0.5f);

            if (string.IsNullOrEmpty(titleSceneName))
            {
                Debug.LogError("[VictoryUI] titleSceneName이 비어있음. 인스펙터에서 시작 씬 이름 지정 필요.");
                return;
            }

            SceneManager.LoadScene(titleSceneName);
        }

        private void SetCameraInputComponents(bool enabled)
        {
            if (cameraInputComponentsToDisable == null) return;
            foreach (var c in cameraInputComponentsToDisable)
            {
                if (c != null) c.enabled = enabled;
            }
        }
    }
}