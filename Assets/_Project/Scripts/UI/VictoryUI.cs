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
    /// 6. (최초 Nightmare 해금 시) VICTORY 후 일정 시간 뒤 포효 + 시그널 텍스트 연출
    /// 7. Return to Title 버튼 활성화 (해금 연출이 있으면 그 연출이 끝난 뒤에 활성)
    /// 8. 사용자가 Return to Title 클릭하면 타이틀로
    ///
    /// Return to Title 버튼 활성 타이밍:
    /// - 버튼은 Awake에서 비활성(interactable=false)으로 시작.
    /// - 최초 해금 연출이 있는 판: 시그널 연출(포효+텍스트)이 완전히 끝난 뒤 버튼 활성.
    ///   → 플레이어가 연출 도중 버튼 눌러서 연출을 놓치는 걸 방지.
    /// - 연출 없는 일반 판: VICTORY 페이드인 완료 즉시 버튼 활성 (기다릴 이유 없음).
    /// 주의: rootCanvasGroup.interactable과 별개로 버튼 자체의 interactable을 직접 제어.
    ///       rootCanvasGroup은 페이드인 시 활성화하되, 버튼만 따로 잠갔다 푼다.
    ///
    /// 난이도 해금 + 시그널:
    /// - 보스 처치 = 클리어 확정 → HandleBossDeath에서 GameSettings.MarkNormalClearedIfApplicable() 호출.
    /// - 그 반환값(최초 해금 여부)이 true면 _justUnlockedNightmare=true.
    /// - VICTORY 페이드인 완료 후, 최초 해금이면 시그널 연출 코루틴 실행:
    ///   unlockSignalDelay 대기 → 포효 사운드 + 텍스트 페이드인 → 유지 → 페이드아웃.
    /// - 이미 해금돼 있었거나 Normal이 아니면 시그널 없음 (1회성 연출).
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

        [Header("Unlock Signal (Nightmare 최초 해금 연출)")]
        [Tooltip("'불길한 소리가 들린다...' 시그널 텍스트의 페이드 제어용 CanvasGroup. " +
                 "VICTORY 화면 위에 별도로 뜨는 텍스트 그룹. 비우면 텍스트 연출 스킵(사운드만).")]
        [SerializeField] private CanvasGroup unlockSignalCanvasGroup;
        [Tooltip("VICTORY 페이드인 완료 후 시그널 연출 시작까지 대기 시간(초).")]
        [SerializeField] private float unlockSignalDelay = 2.0f;
        [Tooltip("시그널 텍스트 페이드인 시간(초).")]
        [SerializeField] private float unlockSignalFadeInTime = 1.0f;
        [Tooltip("시그널 텍스트가 완전히 보인 채 유지되는 시간(초). 읽을 시간.")]
        [SerializeField] private float unlockSignalHoldTime = 3.5f;
        [Tooltip("시그널 텍스트 페이드아웃 시간(초).")]
        [SerializeField] private float unlockSignalFadeOutTime = 1.5f;

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
        // 이번 클리어로 Nightmare가 '처음' 해금됐는지. HandleBossDeath에서 결정.
        private bool _justUnlockedNightmare;

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

            // 시그널 텍스트도 시작 시 숨김
            if (unlockSignalCanvasGroup != null)
            {
                unlockSignalCanvasGroup.alpha = 0f;
                unlockSignalCanvasGroup.interactable = false;
                unlockSignalCanvasGroup.blocksRaycasts = false;
            }

            if (returnToTitleButton != null)
            {
                returnToTitleButton.onClick.AddListener(ReturnToTitle);
                // 버튼은 처음엔 잠금 — VICTORY 페이드인(+ 해금 연출) 끝난 뒤 활성화.
                // 연출 도중 눌러서 시그널을 놓치는 걸 방지.
                returnToTitleButton.interactable = false;
            }
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

            // 난이도 해금 기록 — 보스 처치 = 클리어 확정.
            // 반환값 = '이번 클리어로 Nightmare가 처음 해금됐는지'.
            // true면 VICTORY 후 시그널 연출(포효 + 텍스트) 발동.
            // (Easy 클리어거나 이미 해금돼 있었으면 false → 연출 없음.)
            // GameSettings 없는 단독 씬 테스트에선 조용히 스킵.
            if (LostSouls.Settings.GameSettings.Instance != null)
                _justUnlockedNightmare = LostSouls.Settings.GameSettings.Instance.MarkNormalClearedIfApplicable();

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

            // 페이드 인 + 상호작용 활성 (단, Return to Title 버튼은 아직 잠금 유지)
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

            // 3단계: 최초 Nightmare 해금이면 시그널 연출 이어붙이기.
            // VICTORY가 완전히 뜬 뒤에 시작 (그 위에 텍스트가 잠깐 떴다 사라짐).
            // 연출이 끝나야 버튼을 활성화 — 도중에 타이틀로 못 가게.
            if (_justUnlockedNightmare)
                yield return StartCoroutine(UnlockSignalSequence());

            // 4단계: 이제 Return to Title 버튼 활성화.
            // - 해금 연출 있던 판: 연출 끝난 뒤 (위 yield 완료 후)
            // - 일반 판: 페이드인 직후 즉시
            EnableReturnButton();
        }

        /// <summary>
        /// Nightmare 최초 해금 시그널 연출.
        /// 대기 → 포효 사운드 + 텍스트 페이드인 → 유지 → 페이드아웃.
        /// 텍스트 CanvasGroup이 없으면 사운드만 재생하고 종료.
        /// </summary>
        private IEnumerator UnlockSignalSequence()
        {
            // VICTORY 뜬 뒤 잠깐 텀 — 승리 음미 후 불길한 시그널.
            yield return new WaitForSeconds(unlockSignalDelay);

            // 포효 사운드 — 2D UI 사운드로 화면 전체 울림. 보스 포효와 구분되는 전용 사운드.
            // ClipBank의 nightmareUnlockRoar가 비어있으면 조용히 스킵.
            if (LostSouls.Audio.AudioManager.Instance != null &&
                LostSouls.Audio.AudioManager.Instance.ClipBank != null)
            {
                LostSouls.Audio.AudioManager.Instance.PlayUISound(
                    LostSouls.Audio.AudioManager.Instance.ClipBank.nightmareUnlockRoar);
            }

            // 텍스트 CanvasGroup 없으면 사운드만 내고 종료
            if (unlockSignalCanvasGroup == null)
                yield break;

            // 페이드 인
            yield return StartCoroutine(FadeCanvasGroup(unlockSignalCanvasGroup, 0f, 1f, unlockSignalFadeInTime));

            // 유지 (읽을 시간)
            if (unlockSignalHoldTime > 0f)
                yield return new WaitForSeconds(unlockSignalHoldTime);

            // 페이드 아웃
            yield return StartCoroutine(FadeCanvasGroup(unlockSignalCanvasGroup, 1f, 0f, unlockSignalFadeOutTime));
        }

        /// <summary>Return to Title 버튼 활성화. 연출 종료(또는 일반 판이면 페이드인) 후 호출.</summary>
        private void EnableReturnButton()
        {
            if (returnToTitleButton != null)
                returnToTitleButton.interactable = true;
        }

        /// <summary>CanvasGroup alpha를 from→to로 duration초에 걸쳐 보간.</summary>
        private IEnumerator FadeCanvasGroup(CanvasGroup cg, float from, float to, float duration)
        {
            if (cg == null) yield break;

            if (duration <= 0f)
            {
                cg.alpha = to;
                yield break;
            }

            float elapsed = 0f;
            cg.alpha = from;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                cg.alpha = Mathf.Lerp(from, to, elapsed / duration);
                yield return null;
            }
            cg.alpha = to;
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