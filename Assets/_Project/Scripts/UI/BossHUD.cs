using UnityEngine;
using UnityEngine.UI;
using TMPro;
using LostSouls.Boss;

namespace LostSouls.UI
{
    /// <summary>
    /// 보스 체력/Poise 바 + 이름 표시. 화면 하단 가운데에 위치.
    ///
    /// 표시 규칙 (다크소울 정석):
    /// - 보스 살아있으면 표시, 죽으면 페이드 아웃 후 숨김
    /// - 체력 바: 항상 표시
    /// - Poise 바: 100%면 숨김, 한 번이라도 깎이면 표시. 풀로 회복되면 일정 시간 후 다시 숨김.
    /// - Poise 바는 별도 CanvasGroup으로 독립적 페이드
    ///
    /// PlayerHUD와 같은 패턴 — Lerp 보간으로 막대바 부드럽게 차감.
    /// </summary>
    public class BossHUD : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("표시할 보스. 비우면 비활성. 보스룸 시스템 도입 시 동적으로 SetBoss로 교체.")]
        [SerializeField] private BossHealth bossHealth;
        [SerializeField] private BossPoise bossPoise;

        [Header("UI Elements")]
        [Tooltip("전체 HUD 페이드 인/아웃용. 이 컴포넌트가 부착된 GameObject나 부모에 있어야 함.")]
        [SerializeField] private CanvasGroup rootCanvasGroup;
        [SerializeField] private Image healthFill;
        [Tooltip("Poise 바 자체의 CanvasGroup. 자동 숨김에 사용.")]
        [SerializeField] private CanvasGroup poiseCanvasGroup;
        [SerializeField] private Image poiseFill;
        [Tooltip("보스 이름 텍스트. TextMeshPro 사용.")]
        [SerializeField] private TMP_Text nameText;

        [Header("Display")]
        [Tooltip("보스 이름. 인스펙터에서 직접 입력. 향후 BossData SO로 분리 가능.")]
        [SerializeField] private string bossName = "Iron Knight";

        [Header("Animation")]
        [Tooltip("막대바 변화 부드러움 (체력/Poise 공용).")]
        [SerializeField] private float fillSmoothSpeed = 8f;
        [Tooltip("전체 HUD 페이드 속도 (등장/사망 시).")]
        [SerializeField] private float fadeSpeed = 3f;
        [Tooltip("Poise 가득 찬 후 자동 숨김까지의 대기 시간 (초).")]
        [SerializeField] private float poiseHideDelay = 1.5f;
        [Tooltip("Poise 바 페이드 속도.")]
        [SerializeField] private float poiseFadeSpeed = 4f;

        // 표시값 (Lerp용)
        private float _displayedHealthRatio = 1f;
        private float _displayedPoiseRatio = 1f;

        // 페이드 타겟 (root)
        private float _rootTargetAlpha;

        // Poise 표시 상태 관리
        // _poiseEverDamaged: 한 번이라도 100% 미만이 된 적 있는가
        // _poiseFullSince: Poise가 100%가 된 시각. hideDelay 이후 숨김.
        private bool _poiseEverDamaged;
        private float _poiseFullSince;
        private float _poiseTargetAlpha;

        private void Start()
        {
            // 초기 상태: 숨김. Update에서 보스 살아있으면 페이드 인.
            if (rootCanvasGroup != null) rootCanvasGroup.alpha = 0f;
            if (poiseCanvasGroup != null) poiseCanvasGroup.alpha = 0f;

            if (nameText != null) nameText.text = bossName;

            // 초기 fill값 (보스 풀체력 가정)
            if (healthFill != null) healthFill.fillAmount = 1f;
            if (poiseFill != null) poiseFill.fillAmount = 1f;
        }

        private void Update()
        {
            UpdateRootVisibility();
            UpdateHealthBar();
            UpdatePoiseBar();
        }

        /// <summary>
        /// 전체 HUD 페이드. 보스 살아있으면 1, 죽으면 0으로.
        /// </summary>
        private void UpdateRootVisibility()
        {
            if (rootCanvasGroup == null) return;

            bool shouldShow = bossHealth != null && !bossHealth.IsDead;
            _rootTargetAlpha = shouldShow ? 1f : 0f;

            rootCanvasGroup.alpha = Mathf.MoveTowards(
                rootCanvasGroup.alpha, _rootTargetAlpha, fadeSpeed * Time.deltaTime);
        }

        private void UpdateHealthBar()
        {
            if (bossHealth == null || healthFill == null) return;

            float target = Mathf.Clamp01(bossHealth.CurrentHealth / bossHealth.MaxHealth);

            _displayedHealthRatio = Mathf.Lerp(_displayedHealthRatio, target, fillSmoothSpeed * Time.deltaTime);
            healthFill.fillAmount = _displayedHealthRatio;
        }

        /// <summary>
        /// Poise 바 — 한 번이라도 깎이면 등장, 풀로 회복되면 일정 시간 후 숨김.
        /// </summary>
        private void UpdatePoiseBar()
        {
            if (bossPoise == null || poiseFill == null || poiseCanvasGroup == null) return;

            float target = Mathf.Clamp01(bossPoise.CurrentPoise / bossPoise.MaxPoise);

            _displayedPoiseRatio = Mathf.Lerp(_displayedPoiseRatio, target, fillSmoothSpeed * Time.deltaTime);
            poiseFill.fillAmount = _displayedPoiseRatio;

            // 표시 여부 결정
            //   - 처음엔 숨김 (_poiseEverDamaged=false)
            //   - 깎이면 ON 유지
            //   - 풀로 회복된 후 hideDelay 지나면 OFF
            //   - Break 직후 (가득 차 있어 보이지만 사실 IsBroken이라 다음 데미지 들어오면 다시 보임)도 일관 처리
            bool poiseFull = target >= 0.999f && !bossPoise.IsBroken;

            if (!poiseFull)
            {
                // 깎인 상태
                _poiseEverDamaged = true;
                _poiseFullSince = -1f;
            }
            else if (_poiseEverDamaged && _poiseFullSince < 0f)
            {
                // 방금 풀로 회복된 순간
                _poiseFullSince = Time.time;
            }

            // 알파 타겟 결정
            bool shouldShow;
            if (!_poiseEverDamaged) shouldShow = false;                              // 한 번도 안 깎임
            else if (_poiseFullSince < 0f) shouldShow = true;                        // 현재 깎인 상태
            else shouldShow = (Time.time - _poiseFullSince) < poiseHideDelay;        // 풀 회복 후 대기 중

            _poiseTargetAlpha = shouldShow ? 1f : 0f;
            poiseCanvasGroup.alpha = Mathf.MoveTowards(
                poiseCanvasGroup.alpha, _poiseTargetAlpha, poiseFadeSpeed * Time.deltaTime);
        }

        /// <summary>
        /// 다른 보스로 교체 (보스룸 시스템 도입 시 사용).
        /// </summary>
        public void SetBoss(BossHealth health, BossPoise poise, string displayName)
        {
            bossHealth = health;
            bossPoise = poise;
            bossName = displayName;

            if (nameText != null) nameText.text = displayName;

            // 표시 상태 리셋
            _poiseEverDamaged = false;
            _poiseFullSince = -1f;
            _displayedHealthRatio = 1f;
            _displayedPoiseRatio = 1f;
            if (poiseCanvasGroup != null) poiseCanvasGroup.alpha = 0f;
        }
    }
}