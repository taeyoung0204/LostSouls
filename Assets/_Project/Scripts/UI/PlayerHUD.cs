using UnityEngine;
using UnityEngine.UI;
using LostSouls.Player;

namespace LostSouls.UI
{
    /// <summary>
    /// 플레이어의 체력/스태미나를 UI 막대바에 표시한다.
    /// </summary>
    public class PlayerHUD : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerHealth playerHealth;
        [SerializeField] private PlayerStamina playerStamina;

        [Header("UI Elements")]
        [SerializeField] private Image healthFill;
        [SerializeField] private Image staminaFill;

        [Header("Animation")]
        [SerializeField] private float fillSmoothSpeed = 8f;  // 막대바 변화 부드러움

        private float _displayedHealthRatio;
        private float _displayedStaminaRatio;

        private void Start()
        {
            // 시작 시 가득 채움
            if (playerHealth != null)
                _displayedHealthRatio = playerHealth.CurrentHealth / playerHealth.MaxHealth;

            if (playerStamina != null)
                _displayedStaminaRatio = playerStamina.Normalized;

            if (healthFill != null) healthFill.fillAmount = _displayedHealthRatio;
            if (staminaFill != null) staminaFill.fillAmount = _displayedStaminaRatio;
        }

        private void Update()
        {
            UpdateHealthBar();
            UpdateStaminaBar();
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
    }
}