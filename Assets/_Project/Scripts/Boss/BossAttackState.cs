using System.Collections.Generic;
using UnityEngine;

namespace LostSouls.Boss
{
    /// <summary>
    /// Attack State: 패턴 선택 + 패턴 라이프사이클 관리.
    ///
    /// 행동은 패턴 자체에 위임 (Strategy 패턴).
    /// State는 다음만 담당:
    /// - 후보 패턴 필터링 + 선택 (거리, 연속 방지, 가중 랜덤)
    /// - Enter/Update/Exit 위임 호출
    /// - Animation Event로부터 종료 신호 수신
    /// - 쿨다운 설정
    ///
    /// 새 종류의 패턴 추가 = AttackPattern 상속한 새 클래스 + 새 SO 에셋.
    /// 이 State는 건드릴 일 없음.
    /// </summary>
    public class BossAttackState : BossStateBase
    {
        private AttackPattern _currentPattern;
        private bool _animationEnded;
        private float _stateTimer;

        // Animation Event 누락 등으로 AttackState에 영원히 갇히는 버그를 방지하는 안전망.
        // 정상 공격은 가장 긴 것도 3초 안에 끝남 (DashJump, SlowDoubleSlash 등).
        // 이 시간 안에 _animationEnded가 안 오면 무언가 잘못된 것 — 강제 ChaseState 복귀.
        private const float MaxStateTime = 8f;

        // 후보 임시 저장용 (매 Enter마다 new 안 하려고 필드로)
        private readonly List<AttackPattern> _candidates = new List<AttackPattern>();

        public BossAttackState(BossController boss) : base(boss) { }

        public override void Enter()
        {
            _animationEnded = false;
            _stateTimer = 0f;

            _currentPattern = SelectPattern();

            if (_currentPattern == null)
            {
                Debug.LogWarning($"[Boss] No attackable pattern at distance {boss.DistanceToPlayer:F1}. Returning to Chase.");
                boss.ChangeState(boss.ChaseState);
                return;
            }

            Debug.Log($"[Boss] Attack pattern: {_currentPattern.patternName} (dist={boss.DistanceToPlayer:F1})");

            // 패턴이 자기 시작 처리 (트리거 발동, 이동 제어 등)
            _currentPattern.Enter(boss);
        }

        public override void Update()
        {
            if (_currentPattern == null) return;

            _stateTimer += Time.deltaTime;

            // 패턴이 자기 행동 처리 (회전, 위치 보정 등)
            _currentPattern.Update(boss, _stateTimer);

            // 애니메이션 종료 신호 받으면 다음 State로
            if (_animationEnded)
            {
                boss.ChangeState(boss.ChaseState);
                return;
            }

            // 안전망: Animation Event(OnAttackAnimationEnd) 누락으로 stuck됐을 가능성.
            // MaxStateTime 초과 시 경고 로그 + 강제 ChaseState 복귀.
            // 이 로그가 나오면 해당 패턴의 클립에 Animation Event가 빠졌거나 Exit Time보다 뒤에 있다.
            if (_stateTimer > MaxStateTime)
            {
                Debug.LogError($"[Boss] AttackState STUCK in pattern '{_currentPattern.patternName}' for {_stateTimer:F1}s. " +
                               $"Animation Event 'OnAttackAnimationEnd' may be missing or placed after Exit Time. Forcing recovery.");
                boss.ChangeState(boss.ChaseState);
            }
        }

        public override void Exit()
        {
            if (_currentPattern != null)
            {
                // 패턴이 자기 정리 처리
                _currentPattern.Exit(boss);

                // 연속 방지용 기록
                boss.LastUsedPattern = _currentPattern;

                // 패턴별 쿨다운 등록 (cooldownDuration 0이면 무시됨)
                boss.RegisterPatternUsage(_currentPattern);
            }

            // 쿨다운 설정
            float cooldown = Random.Range(boss.MinAttackCooldown, boss.MaxAttackCooldown);
            boss.NextAttackTime = Time.time + cooldown;

            _currentPattern = null;
        }

        /// <summary>
        /// Animation Event 중계에서 호출.
        /// </summary>
        public void NotifyAnimationEnd()
        {
            _animationEnded = true;
        }

        /// <summary>
        /// 패턴 선택 (B+C 로직).
        /// 1. IsAvailable 통과 + 쿨다운 안 걸린 패턴만 후보
        /// 2. 직전 패턴 제외 (후보 2개 이상일 때만)
        /// 3. 가중 랜덤
        /// </summary>
        private AttackPattern SelectPattern()
        {
            _candidates.Clear();

            // 1. 발동 가능 + 쿨다운 통과 패턴만 후보
            foreach (AttackPattern p in boss.AttackPatterns)
            {
                if (p == null) continue;
                if (!p.IsAvailable(boss)) continue;

                if (boss.IsPatternOnCooldown(p)) continue;

                _candidates.Add(p);
            }

            if (_candidates.Count == 0) return null;

            // 2. 연속 방지
            if (_candidates.Count > 1 && boss.LastUsedPattern != null)
            {
                _candidates.Remove(boss.LastUsedPattern);
            }

            // 3. 가중 랜덤
            return WeightedRandomPick(_candidates, boss.DistanceToPlayer);
        }

        private AttackPattern WeightedRandomPick(List<AttackPattern> candidates, float distance)
        {
            if (candidates.Count == 1) return candidates[0];

            float totalWeight = 0f;
            foreach (AttackPattern p in candidates)
            {
                totalWeight += p.GetWeight(distance);
            }

            if (totalWeight <= 0f) return candidates[0];

            float roll = Random.Range(0f, totalWeight);
            float accumulated = 0f;

            foreach (AttackPattern p in candidates)
            {
                accumulated += p.GetWeight(distance);
                if (roll <= accumulated) return p;
            }

            return candidates[candidates.Count - 1];
        }
    }
}