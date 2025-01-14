using System;
using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Util
{
    /// <summary>
    /// 提供简单的 UI 动画功能，包含“出生动画”和“销毁动画”。
    /// 可对任意挂载该脚本的 GameObject（需带 RectTransform + CanvasGroup）进行透明度、缩放、位移等处理。
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    [RequireComponent(typeof(CanvasGroup))]
    public class SimpleUIAnimation : MonoBehaviour
    {
        /// <summary>
        /// 动画配置数据模块，如动画缩放比、位移、延迟、时长、缓动类型等。
        /// </summary>
        [Serializable]
        public class BasicModule
        {
            [Tooltip("sizeDelta 缩放系数，通常用来让 UI 的 sizeDelta 按一定比例进行放大或缩小")]
            public Vector2 sizeDeltaScale = Vector2.one;
            
            [Tooltip("Transform 的缩放系数，会与物体原始 scale 相乘来得到目标大小")]
            public Vector2 transformScale = Vector2.one;
            
            [Tooltip("在动画时要移动的距离偏移量")]
            public Vector2 offset;
            
            [Tooltip("动画开始前的延迟时长")]
            public float delay;
            
            [Tooltip("动画实际播放时长")]
            public float duration;
            
            [Tooltip("缓动类型，如 Ease.OutCubic、Ease.InOutBack 等")]
            public Ease ease = Ease.OutCubic;
        }

        [Tooltip("出生动画期间是否可以交互（即是否屏蔽 Raycast），false 表示直到出生动画播放完才能交互")]
        public bool interactableWhenBorn;

        [Title("动画配置")]
        [Tooltip("出生动画的配置参数")]
        public BasicModule bornBasicModule;
        [Tooltip("销毁动画的配置参数")]
        public BasicModule deathBasicModule;

        [Title("必要组件")]
        [Tooltip("动画所操作的容器，一般是当前物体的 RectTransform")]
        public RectTransform animContainer;
        [Tooltip("CanvasGroup 用于控制透明度和 Raycast 开关")]
        public CanvasGroup canvasGroup;

        private Vector2 _initSizeDelta;  // 记录 UI 初始的 sizeDelta
        private Vector3 _initScale;      // 记录 UI 初始的 localScale
        private Sequence _bornSequence;  // 出生动画序列
        private Sequence _dieSequence;   // 销毁动画序列

        private void Awake()
        {
            // 若未拖拽引用，则尝试自动获取
            if (!animContainer) animContainer = GetComponent<RectTransform>();
            if (!canvasGroup) canvasGroup = GetComponent<CanvasGroup>();

            // 在脚本创建时记录初始 sizeDelta 与 Scale，后续的动画会基于这个初始值进行
            _initSizeDelta = animContainer.sizeDelta;
            _initScale = animContainer.localScale;
        }

        /// <summary>
        /// 播放出生动画：
        /// 1. 先将 alpha 设置为0，再在 sequence 中渐变为1
        /// 2. 如果配置了 sizeDeltaScale 或 transformScale，则先把实际值缩放到配置值，再在Tween中回到原始大小
        /// 3. 若 offset 不为零，则先移动到偏移位置，再移动回原位置
        /// 4. 在动画完成前可禁用 CanvasGroup.blocksRaycasts，从而屏蔽交互
        /// </summary>
        [Button]
        public Tween DoBornAnimation()
        {
            // 如果销毁动画还在播放中，则先终止它
            if (_dieSequence != null && _dieSequence.IsActive()) _dieSequence.Kill(true);

            // 创建出生动画序列
            _bornSequence = DOTween.Sequence().SetUpdate(true);

            // 如果播放时长为 0 或负值，直接返回空序列
            if (bornBasicModule.duration <= 0f) return _bornSequence;

            // 动画开始前的延迟
            _bornSequence.AppendInterval(bornBasicModule.delay);

            // 透明度 0 -> 1
            canvasGroup.alpha = 0f;
            _bornSequence.Append(canvasGroup.DOFade(1f, bornBasicModule.duration));

            // 若配置了 sizeDeltaScale != 1，则先把 sizeDelta 设置为 (初始值×缩放)，在动画中回到初始值
            if (bornBasicModule.sizeDeltaScale != Vector2.one)
            {
                var oriSd = _initSizeDelta;
                animContainer.sizeDelta = Vector2.Scale(oriSd, bornBasicModule.sizeDeltaScale);
                _bornSequence.Join(
                    animContainer.DOSizeDelta(oriSd, bornBasicModule.duration).SetEase(bornBasicModule.ease)
                );
            }

            // 若配置了 transformScale != 1，则同理先把 localScale 设置为 (初始值×缩放)，在动画中回到初始值
            if (bornBasicModule.transformScale != Vector2.one)
            {
                var oriScale = _initScale;
                animContainer.localScale = Vector3.Scale(
                    oriScale,
                    new Vector3(bornBasicModule.transformScale.x, bornBasicModule.transformScale.y, 1f)
                );
                _bornSequence.Join(
                    animContainer.DOScale(oriScale, bornBasicModule.duration).SetEase(bornBasicModule.ease)
                );
            }

            // 若 offset != 0，则先在当前位置基础上 +offset，再在动画中移动回原位置
            if (bornBasicModule.offset != Vector2.zero)
            {
                var localPos = animContainer.localPosition;
                // ReSharper disable once Unity.InefficientPropertyAccess
                animContainer.localPosition += new Vector3(bornBasicModule.offset.x, bornBasicModule.offset.y, 0f);
                _bornSequence.Join(
                    animContainer.DOLocalMove(localPos, bornBasicModule.duration).SetEase(bornBasicModule.ease)
                );
            }

            // 如果设置为出生动画期间不可交互，则在动画完成后才恢复 blocksRaycasts
            if (!interactableWhenBorn)
            {
                canvasGroup.blocksRaycasts = false;
                _bornSequence.AppendCallback(() => canvasGroup.blocksRaycasts = true);
            }

            return _bornSequence;
        }

        /// <summary>
        /// 播放销毁(隐藏)动画：
        /// 1. 透明度由1 -> 0
        /// 2. 如果配置了 sizeDeltaScale != 1，则将 sizeDelta 渐变到 (初始值×scale)
        /// 3. transformScale 同理，从初始值到(初始值×scale)
        /// 4. offset 不为零则移动到 (原位置 + offset)
        /// 5. 交互 blocksRaycasts 会在一开始禁用
        /// </summary>
        [Button]
        public Tween DoDieAnimation()
        {
            // 如果出生动画还在播放中，则先终止它
            if (_bornSequence != null && _bornSequence.IsActive()) _bornSequence.Kill(true);

            // 创建销毁动画序列
            _dieSequence = DOTween.Sequence().SetUpdate(true);

            // 同理，如果配置时长 <= 0，直接返回空序列
            if (deathBasicModule.duration <= 0f) return _dieSequence;

            // 动画延迟
            _dieSequence.AppendInterval(deathBasicModule.delay);

            // 透明度 1 -> 0
            _dieSequence.Append(canvasGroup.DOFade(0f, deathBasicModule.duration));

            // sizeDelta 缩放到 (初始值×deathBasicModule.sizeDeltaScale)
            if (deathBasicModule.sizeDeltaScale != Vector2.one)
            {
                var oriSd = _initSizeDelta;
                var targetSd = Vector2.Scale(oriSd, deathBasicModule.sizeDeltaScale);
                _dieSequence.Join(
                    animContainer.DOSizeDelta(targetSd, deathBasicModule.duration).SetEase(deathBasicModule.ease)
                );
            }

            // localScale 缩放到 (初始值×deathBasicModule.transformScale)
            if (deathBasicModule.transformScale != Vector2.one)
            {
                var oriScale = _initScale;
                var targetScale = Vector3.Scale(
                    oriScale,
                    new Vector3(deathBasicModule.transformScale.x, deathBasicModule.transformScale.y, 1f)
                );
                _dieSequence.Join(
                    animContainer.DOScale(targetScale, deathBasicModule.duration).SetEase(deathBasicModule.ease)
                );
            }

            // 移动到当前 localPosition + offset
            if (deathBasicModule.offset != Vector2.zero)
            {
                var localPos = animContainer.localPosition
                               + new Vector3(deathBasicModule.offset.x, deathBasicModule.offset.y, 0f);
                _dieSequence.Join(
                    animContainer.DOLocalMove(localPos, deathBasicModule.duration).SetEase(deathBasicModule.ease)
                );
            }

            // 在销毁动画开始时就禁用交互
            canvasGroup.blocksRaycasts = false;
            return _dieSequence;
        }

        /// <summary>
        /// 当该物体被Disable时，若动画序列还在播放，立即停止它们，避免出现潜在的内存泄露或引用问题。
        /// </summary>
        private void OnDisable()
        {
            if (_bornSequence != null && _bornSequence.IsActive()) _bornSequence.Kill(true);
            if (_dieSequence != null && _dieSequence.IsActive()) _dieSequence.Kill(true);
        }
    }
}
