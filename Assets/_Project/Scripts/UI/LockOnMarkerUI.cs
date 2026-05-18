using UnityEngine;
using UnityEngine.UI;
using LostSouls.Player;

namespace LostSouls.UI
{
    /// <summary>
    /// 락온된 대상의 LockOnPoint 위에 화면 마커를 띄운다.
    ///
    /// 구현 방식:
    /// - Screen Space - Overlay Canvas에 Image 하나
    /// - 매 프레임 WorldToScreenPoint로 대상의 화면 위치 계산
    /// - 카메라 뒤에 있거나 락온 안 됐으면 숨김
    ///
    /// PlayerLockOn의 CurrentTargetTransform이 락온 상태/대상을 노출하므로 그걸 구독한다.
    /// </summary>
    public class LockOnMarkerUI : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("락온 상태를 읽을 PlayerLockOn 컴포넌트.")]
        [SerializeField] private PlayerLockOn playerLockOn;
        [Tooltip("WorldToScreenPoint 변환에 쓸 카메라. 비우면 Camera.main 자동.")]
        [SerializeField] private Camera worldCamera;

        [Header("UI Elements")]
        [Tooltip("실제 마커 이미지의 RectTransform. 다이아몬드/원 sprite 등.")]
        [SerializeField] private RectTransform markerRect;
        [Tooltip("마커 페이드 인/아웃 제어용 CanvasGroup.")]
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("Behavior")]
        [Tooltip("페이드 속도. 락온/해제 시 깜빡임 방지.")]
        [SerializeField] private float fadeSpeed = 12f;
        [Tooltip("락온 중 마커가 살짝 회전. 0이면 정지, 양수면 시계방향 회전 (도/초).")]
        [SerializeField] private float idleSpinSpeed = 0f;

        private float _currentRotation;

        private void Awake()
        {
            if (worldCamera == null) worldCamera = Camera.main;
            if (canvasGroup != null) canvasGroup.alpha = 0f;
        }

        private void LateUpdate()
        {
            // LateUpdate 사용 이유:
            // 카메라가 LateUpdate에서 위치/회전 결정되는 경우가 많음 (Cinemachine 포함).
            // 같은 프레임의 카메라 최종 상태를 기준으로 변환해야 마커가 한 프레임 늦지 않음.

            bool shouldShow = false;
            Vector3 screenPos = Vector3.zero;

            if (playerLockOn != null && playerLockOn.IsLockedOn && worldCamera != null)
            {
                Transform targetTr = playerLockOn.CurrentTargetTransform;
                if (targetTr != null)
                {
                    screenPos = worldCamera.WorldToScreenPoint(targetTr.position);

                    // z > 0이면 카메라 앞. z <= 0이면 뒤 또는 카메라 바로 위 → 숨김.
                    if (screenPos.z > 0f)
                        shouldShow = true;
                }
            }

            if (shouldShow && markerRect != null)
            {
                // 화면 좌표 그대로 마커 위치에 적용.
                // Screen Space - Overlay Canvas는 screenPos.xy 그대로 사용 가능.
                markerRect.position = new Vector3(screenPos.x, screenPos.y, 0f);

                if (idleSpinSpeed != 0f)
                {
                    _currentRotation += idleSpinSpeed * Time.deltaTime;
                    markerRect.localRotation = Quaternion.Euler(0f, 0f, _currentRotation);
                }
            }

            if (canvasGroup != null)
            {
                float target = shouldShow ? 1f : 0f;
                canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, target, fadeSpeed * Time.deltaTime);
            }
        }
    }
}