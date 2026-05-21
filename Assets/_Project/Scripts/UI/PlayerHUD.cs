using UnityEngine;
using UnityEngine.UI;
using TMPro;
using LostSouls.Player;

namespace LostSouls.UI
{
    /// <summary>
    /// 플레이어의 체력/스태미나/포션 잔량을 UI에 표시한다.
    /// </summary>
    public class PlayerHUD : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerHealth playerHealth;
        [SerializeField] private PlayerStamina playerStamina;
        [SerializeField] private PlayerPotion playerPotion;

        [Header("UI Elements")]
        [SerializeField] private Image healthFill;
        [SerializeField] private Image staminaFill;
        [Tooltip("포션 잔량 표시 텍스트 (숫자만 표시). 비워두면 포션 UI 비활성.")]
        [SerializeField] private TextMeshProUGUI potionCountText;
        [Tooltip("포션 아이콘 Image. 잔량 0이면 어둡게 처리하여 사용 불가 시각화. 비워둬도 무방.")]
        [SerializeField] private Image potionIcon;
        [Tooltip("포션 텍스트가 변경될 때 살짝 깜빡이는 연출용 (선택). 비워도 무방.")]
        [SerializeField] private CanvasGroup potionGroup;

        [Header("Animation")]
        [SerializeField] private float fillSmoothSpeed = 8f;  // 막대바 변화 부드러움
        [Tooltip("포션 개수 변화 시 깜빡임 지속 시간 (초). 0이면 깜빡임 없음.")]
        [SerializeField] private float potionFlashDuration = 0.2f;
        [Tooltip("포션 잔량 0일 때 아이콘 색상 (보통 어두운 회색).")]
        [SerializeField] private Color potionEmptyColor = new Color(0.4f, 0.4f, 0.4f, 0.7f);
        [Tooltip("포션 잔량 있을 때 아이콘 색상 (보통 흰색 = 원본 색).")]
        [SerializeField] private Color potionAvailableColor = Color.white;

        private float _displayedHealthRatio;
        private float _displayedStaminaRatio;

        // 포션 깜빡임 상태
        private int _lastPotionCount = -1;
        private float _potionFlashTimer;

        private void Start()
        {
            // 시작 시 가득 채움
            if (playerHealth != null)
                _displayedHealthRatio = playerHealth.CurrentHealth / playerHealth.MaxHealth;

            if (playerStamina != null)
                _displayedStaminaRatio = playerStamina.Normalized;

            if (healthFill != null) healthFill.fillAmount = _displayedHealthRatio;
            if (staminaFill != null) staminaFill.fillAmount = _displayedStaminaRatio;

            if (playerPotion != null)
            {
                _lastPotionCount = playerPotion.CurrentCharges;
                UpdatePotionText();
            }
        }

        private void Update()
        {
            UpdateHealthBar();
            UpdateStaminaBar();
            UpdatePotionDisplay();
        }

        private void UpdateHealthBar()
        {
            if (playerHealth == null || healthFill == null) return;

            float targetRatio = playerHealth.CurrentHealth / playerHealth.MaxHealth;
            targetRatio = Mathf.Clamp01(targetRatio);

            // 부드럽게 보간
            _displayedHealthRatio = Mathf.Lerp(_displayedHealthRatio, targetRatio, fillSmoothSpeed * Time.deltaTime);
            healthFill.fillAmount = _displayedHealthRatio;
        }

        private void UpdateStaminaBar()
        {
            if (playerStamina == null || staminaFill == null) return;

            // 스태미나는 음수 허용이라 Clamp01로 정리
            float targetRatio = Mathf.Clamp01(playerStamina.CurrentStamina / playerStamina.MaxStamina);

            _displayedStaminaRatio = Mathf.Lerp(_displayedStaminaRatio, targetRatio, fillSmoothSpeed * Time.deltaTime);
            staminaFill.fillAmount = _displayedStaminaRatio;
        }

        private void UpdatePotionDisplay()
        {
            if (playerPotion == null || potionCountText == null) return;

            int currentCount = playerPotion.CurrentCharges;

            // 개수 변화 감지 → 텍스트 갱신 + 깜빡임 시작
            if (currentCount != _lastPotionCount)
            {
                _lastPotionCount = currentCount;
                UpdatePotionText();

                if (potionFlashDuration > 0f && potionGroup != null)
                    _potionFlashTimer = potionFlashDuration;
            }

            // 깜빡임 처리 — 시작 시 alpha 약하게, 시간 지나면 1.0으로 복귀
            if (_potionFlashTimer > 0f && potionGroup != null)
            {
                _potionFlashTimer -= Time.deltaTime;
                float t = Mathf.Clamp01(_potionFlashTimer / potionFlashDuration);
                // 0.4 → 1.0 사이 깜빡임
                potionGroup.alpha = Mathf.Lerp(1f, 0.4f, t);
                if (_potionFlashTimer <= 0f)
                    potionGroup.alpha = 1f;
            }
        }

        private void UpdatePotionText()
        {
            if (potionCountText == null || playerPotion == null) return;

            int count = playerPotion.CurrentCharges;
            potionCountText.text = count.ToString();

            // 잔량 0 vs 있음에 따라 아이콘 색상 변경 (다크소울 정석 — 회색 처리)
            if (potionIcon != null)
                potionIcon.color = (count > 0) ? potionAvailableColor : potionEmptyColor;
        }
    }
}